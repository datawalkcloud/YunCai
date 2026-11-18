using System.Globalization;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>资金报表与核对总账中的可预期错误。</summary>
    public sealed class FundReportException : Exception
    {
        public FundReportException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 资金报表应用服务：按 账户 / 收支类别 / 往来单位 / 项目 / 部门 / 组合 六个维度汇总区间内收付，
    /// 并「资金报表」的分段结构（现金账户｜银行账户、收入类别｜支出类别 …）输出小计行；
    /// 同时提供出纳账与总账的余额核对（核对总账）。
    /// 口径说明：日记账为手工登记台账（含未生成凭证的流水，全量参与汇总）；
    /// 核对总账的账面侧仅统计已过账凭证（与账簿一致）。
    /// 项目 / 部门维度取流水上登记的项目、部门（在「批量操作 - 指定项目/指定部门」中写入），
    /// 未指定的流水不出现在辅助维度报表中（与同一口径）。
    /// </summary>
    public sealed class FundReportService
    {
        private const string CurrencyNoteText = "总计金额统计为人民币，外币账户会根据日记账中的本币金额进行计算，可能会存在一定的误差";

        private const string DimCategory = "category";
        private const string DimCounterparty = "counterparty";
        private const string DimProject = "project";
        private const string DimDepartment = "department";

        /// <summary>维度取值：从流水上取哪个字段作为该维度的分组值。</summary>
        private static readonly IReadOnlyDictionary<string, Func<JournalEntry, string?>> DimFields =
            new Dictionary<string, Func<JournalEntry, string?>>
            {
                [DimCategory] = e => e.Category,
                [DimCounterparty] = e => e.Counterparty,
                [DimProject] = e => e.Project,
                [DimDepartment] = e => e.Department,
            };

        /// <summary>维度显示名（页头「主类别 / 次类别」与段名用）。</summary>
        private static readonly IReadOnlyDictionary<string, string> DimLabels =
            new Dictionary<string, string>
            {
                [DimCategory] = "收支类别",
                [DimCounterparty] = "往来单位",
                [DimProject] = "项目",
                [DimDepartment] = "部门",
            };

        /// <summary>组合维度的主类别固定为收支类别。</summary>
        private const string ComboPrimary = DimCategory;

        /// <summary>组合维度可选的次类别。</summary>
        public static readonly IReadOnlyList<string> ComboSecondaries = [DimProject, DimDepartment, DimCounterparty, DimCategory];

        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IJournalRepository _journalRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IEntryCategoryRepository _entryCategoryRepository;

        public FundReportService(
            IAccountSetRepository accountSetRepository,
            IJournalRepository journalRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            IEntryCategoryRepository entryCategoryRepository)
        {
            _accountSetRepository = accountSetRepository;
            _journalRepository = journalRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _entryCategoryRepository = entryCategoryRepository;
        }

        /// <summary>按维度生成资金报表。</summary>
        /// <param name="dimension">汇总维度：account（默认）/ category / counterparty / project / department / combo。</param>
        /// <param name="journalId">按账户筛选（仅汇总该日记账的流水）；为空表示全部账户。</param>
        /// <param name="keyword">维度值名称模糊搜索；为空表示全部。</param>
        /// <param name="secondary">组合维度的次类别：project（默认）/ department / counterparty / category。</param>
        public async Task<FundReportDto> GetReportAsync(
            Guid accountSetId,
            string? dimension,
            DateOnly? startDate,
            DateOnly? endDate,
            Guid? journalId = null,
            string? keyword = null,
            string? secondary = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var dim = NormalizeDimension(dimension);
            var (start, end) = ResolveRange(startDate, endDate);
            var journals = await LoadJournalsWithEntriesAsync(accountSetId, cancellationToken);
            if (journalId is { } id && id != Guid.Empty)
            {
                journals = journals.Where(j => j.Id == id).ToList();
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);

            var sections = dim switch
            {
                FundReportDimensions.Category => await BuildCategorySectionsAsync(accountSetId, journals, start, end, keyword, cancellationToken),
                FundReportDimensions.Counterparty => [BuildSingleSection(journals, start, end, keyword, DimFields[DimCounterparty], "往来单位", "往来单位合计")],
                FundReportDimensions.Project => [BuildSingleSection(journals, start, end, keyword, DimFields[DimProject], "项目", "项目合计")],
                FundReportDimensions.Department => [BuildSingleSection(journals, start, end, keyword, DimFields[DimDepartment], "部门", "部门合计")],
                FundReportDimensions.Combo => [BuildComboSection(journals, start, end, keyword, ResolveSecondary(secondary))],
                _ => BuildAccountSections(journals, accountsById, start, end),
            };

            return new FundReportDto
            {
                Dimension = dim,
                StartDate = start,
                EndDate = end,
                Sections = sections,
                Total = BuildTotal(sections),
                Note = NoteOf(dim),
                CurrencyNote = dim == FundReportDimensions.Account ? CurrencyNoteText : null,
                SupportsAccountFilter = dim != FundReportDimensions.Account,
                PrimaryText = dim == FundReportDimensions.Combo ? DimLabels[ComboPrimary] : null,
                SecondaryText = dim == FundReportDimensions.Combo ? DimLabels[ResolveSecondary(secondary)] : null,
            };
        }

        /// <summary>
        /// 核对总账：按会计期间（核对日所在月 1 日 ~ 核对日）逐账户列出
        /// 期初余额 / 借方(收入) / 贷方(支出) / 余额，并与总账科目余额对照给出差异。
        /// </summary>
        /// <param name="journalId">只核对指定资金账户；为空表示全部账户。</param>
        public async Task<FundReconciliationDto> GetReconciliationAsync(
            Guid accountSetId,
            DateOnly? date,
            Guid? journalId = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var asOf = date ?? DateOnly.FromDateTime(DateTime.Today);
            var periodStart = new DateOnly(asOf.Year, asOf.Month, 1);
            var journals = await LoadJournalsWithEntriesAsync(accountSetId, cancellationToken);
            if (journalId is { } id && id != Guid.Empty)
            {
                journals = journals.Where(j => j.Id == id).ToList();
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);

            // 总账侧：各科目截至核对日的累计净额（借 - 贷，仅已过账凭证 + 科目期初）。
            var ledgerNet = new Dictionary<Guid, decimal>();
            foreach (var account in accountsById.Values)
            {
                if (account.IsLeaf && account.OpeningBalance != 0m)
                {
                    var netDebit = account.Direction == BalanceDirection.Debit
                        ? account.OpeningBalance
                        : -account.OpeningBalance;
                    ledgerNet[account.Id] = ledgerNet.GetValueOrDefault(account.Id) + netDebit;
                }
            }

            var posted = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();
            foreach (var voucher in posted)
            {
                if (voucher.Date > asOf)
                {
                    continue;
                }

                foreach (var entry in voucher.Entries)
                {
                    ledgerNet[entry.AccountId] = ledgerNet.GetValueOrDefault(entry.AccountId)
                        + entry.Debit - entry.Credit;
                }
            }

            var rows = new List<FundReconciliationRowDto>();
            foreach (var journal in journals
                         .OrderBy(j => j.Type)
                         .ThenBy(j => accountsById.GetValueOrDefault(j.AccountId)?.Code ?? string.Empty, StringComparer.Ordinal)
                         .ThenBy(j => j.Name, StringComparer.Ordinal))
            {
                // 期初：账户期初 + 期间起始日之前的流水；借/贷：期间内的收入与支出。
                var opening = journal.OpeningBalance
                    + journal.Entries.Where(e => e.Date < periodStart).Sum(e => e.Income - e.Expense);
                var inPeriod = journal.Entries.Where(e => e.Date >= periodStart && e.Date <= asOf).ToList();
                var debit = inPeriod.Sum(e => e.Income);
                var credit = inPeriod.Sum(e => e.Expense);
                var balance = opening + debit - credit;

                string? note = null;
                var ledgerBalance = 0m;
                if (accountsById.TryGetValue(journal.AccountId, out var account))
                {
                    ledgerBalance = ledgerNet.GetValueOrDefault(account.Id);
                }
                else
                {
                    note = "未关联科目，请到「账户设置」中重新关联。";
                }

                var difference = balance - ledgerBalance;
                rows.Add(new FundReconciliationRowDto
                {
                    JournalId = journal.Id,
                    JournalName = journal.Name,
                    JournalType = (int)journal.Type,
                    ProjectText = journal.Type == JournalType.Cash ? "现金" : "银行存款",
                    AccountCode = accountsById.GetValueOrDefault(journal.AccountId)?.Code ?? string.Empty,
                    AccountName = accountsById.GetValueOrDefault(journal.AccountId)?.Name ?? string.Empty,
                    OpeningBalance = opening,
                    Debit = debit,
                    Credit = credit,
                    Balance = balance,
                    LedgerBalance = ledgerBalance,
                    Difference = difference,
                    Balanced = note is null && difference == 0m,
                    Note = note,
                });
            }

            return new FundReconciliationDto
            {
                StartDate = periodStart,
                Date = asOf,
                AllBalanced = rows.All(r => r.Balanced),
                HasUnlinkedAccounts = rows.Any(r => r.Note is not null),
                Rows = rows,
            };
        }

        /* ---------------- 账户维度：现金账户｜银行账户 两段 ---------------- */

        private static List<FundReportSectionDto> BuildAccountSections(
            IReadOnlyList<Journal> journals,
            IReadOnlyDictionary<Guid, Account> accountsById,
            DateOnly start,
            DateOnly end)
        {
            return
            [
                BuildAccountSection("现金账户", "人民币合计", JournalType.Cash),
                BuildAccountSection("银行账户", "人民币合计", JournalType.BankDeposit),
            ];

            FundReportSectionDto BuildAccountSection(string name, string subtotalLabel, JournalType type)
            {
                var rows = new List<FundReportRowDto>();
                foreach (var journal in journals.Where(j => j.Type == type))
                {
                    var opening = journal.OpeningBalance
                        + journal.Entries.Where(e => e.Date < start).Sum(e => e.Income - e.Expense);
                    var inRange = journal.Entries.Where(e => e.Date >= start && e.Date <= end).ToList();
                    var totalIncome = inRange.Sum(e => e.Income);
                    var totalExpense = inRange.Sum(e => e.Expense);

                    rows.Add(new FundReportRowDto
                    {
                        Name = journal.Name,
                        Code = accountsById.GetValueOrDefault(journal.AccountId)?.Code,
                        OpeningBalance = opening,
                        TotalIncome = totalIncome,
                        TotalExpense = totalExpense,
                        Balance = opening + totalIncome - totalExpense,
                        IncomeCount = inRange.Count(e => e.Income > 0m),
                        ExpenseCount = inRange.Count(e => e.Expense > 0m),
                    });
                }

                rows = rows
                    .OrderBy(r => r.Code ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(r => r.Name, StringComparer.Ordinal)
                    .ToList();

                return new FundReportSectionDto
                {
                    Name = name,
                    SubtotalLabel = subtotalLabel,
                    Rows = rows,
                    Subtotal = SumRows(subtotalLabel, rows, withOpening: true),
                };
            }
        }

        /* ---------------- 收支类别维度：收入类别｜支出类别 两段 ---------------- */

        private async Task<List<FundReportSectionDto>> BuildCategorySectionsAsync(
            Guid accountSetId,
            IReadOnlyList<Journal> journals,
            DateOnly start,
            DateOnly end,
            string? keyword,
            CancellationToken cancellationToken)
        {
            var categories = await _entryCategoryRepository.GetAllAsync(accountSetId, cancellationToken);
            var infoByName = new Dictionary<string, (EntryCategoryType Type, string Code)>(StringComparer.Ordinal);
            foreach (var category in categories)
            {
                infoByName[category.Name] = (category.Type, category.Code);
            }

            var buckets = SumBy(journals, start, end, e => string.IsNullOrWhiteSpace(e.Category) ? "未分类" : e.Category!, IsNotTransfer);

            var term = keyword?.Trim();
            var incomeRows = new List<FundReportRowDto>();
            var expenseRows = new List<FundReportRowDto>();
            foreach (var (name, value) in buckets
                         .Where(p => term is null || term.Length == 0 || p.Key.Contains(term, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(p => p.Value.Income + p.Value.Expense)
                         .ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                // 类别归属优先取收支类别档案；档案缺失（如已删除）时按金额方向判定。
                var isIncome = infoByName.TryGetValue(name, out var info)
                    ? info.Type == EntryCategoryType.Income
                    : value.Income >= value.Expense;

                var row = new FundReportRowDto
                {
                    // 报表只显示业务名称，去掉类别档案上的「收- / 支-」方向前缀（同一口径）。
                    Name = StripTypePrefix(name),
                    Code = info.Code,
                    TotalIncome = value.Income,
                    TotalExpense = value.Expense,
                    Balance = value.Income - value.Expense,
                    IncomeCount = value.IncomeCount,
                    ExpenseCount = value.ExpenseCount,
                };

                (isIncome ? incomeRows : expenseRows).Add(row);
            }

            return
            [
                Section("收入类别", "收入合计", incomeRows),
                Section("支出类别", "支出合计", expenseRows),
            ];

            static FundReportSectionDto Section(string name, string subtotalLabel, List<FundReportRowDto> rows)
            {
                var numbered = rows
                    .Select((r, i) => new FundReportRowDto
                    {
                        Name = r.Name,
                        Code = r.Code ?? (i + 1).ToString(CultureInfo.InvariantCulture),
                        TotalIncome = r.TotalIncome,
                        TotalExpense = r.TotalExpense,
                        Balance = r.Balance,
                        IncomeCount = r.IncomeCount,
                        ExpenseCount = r.ExpenseCount,
                    })
                    .ToList();

                return new FundReportSectionDto
                {
                    Name = name,
                    SubtotalLabel = subtotalLabel,
                    Rows = numbered,
                    Subtotal = SumRows(subtotalLabel, numbered, withOpening: false),
                };
            }
        }

        /* ---------------- 单维度（往来单位 / 项目 / 部门）：段名与标签由调用方给定 ---------------- */

        private static FundReportSectionDto BuildSingleSection(
            IReadOnlyList<Journal> journals,
            DateOnly start,
            DateOnly end,
            string? keyword,
            Func<JournalEntry, string?> field,
            string name,
            string subtotalLabel)
        {
            // 未挂该维度的流水不入表（辅助维度报表只列已指定维度值的行）。
            var buckets = SumBy(
                journals,
                start,
                end,
                e => field(e)!,
                e => IsNotTransfer(e) && !string.IsNullOrWhiteSpace(field(e)));
            var term = keyword?.Trim();
            var rows = buckets
                .Where(p => term is null || term.Length == 0 || p.Key.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Value.Income + p.Value.Expense)
                .ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new FundReportRowDto
                {
                    Name = p.Key,
                    TotalIncome = p.Value.Income,
                    TotalExpense = p.Value.Expense,
                    Balance = p.Value.Income - p.Value.Expense,
                    IncomeCount = p.Value.IncomeCount,
                    ExpenseCount = p.Value.ExpenseCount,
                })
                .ToList();

            return new FundReportSectionDto
            {
                Name = name,
                SubtotalLabel = subtotalLabel,
                Rows = rows,
                Subtotal = SumRows(subtotalLabel, rows, withOpening: false),
            };
        }

        /* ---------------- 组合维度：主类别（收支类别）× 次类别，一一对应分组 ---------------- */

        private FundReportSectionDto BuildComboSection(
            IReadOnlyList<Journal> journals,
            DateOnly start,
            DateOnly end,
            string? keyword,
            string secondary)
        {
            var primaryField = DimFields[ComboPrimary];
            var secondaryField = DimFields[secondary];
            var subtotalLabel = "组合合计";

            var buckets = SumBy(
                journals,
                start,
                end,
                e => $"{primaryField(e)!}\u0001{secondaryField(e)!}",
                // 组合维度只统计两个维度都已指定的流水（，未填写的行不入表）。
                e => IsNotTransfer(e)
                     && !string.IsNullOrWhiteSpace(primaryField(e))
                     && !string.IsNullOrWhiteSpace(secondaryField(e)));
            var term = keyword?.Trim();
            var rows = buckets
                .Select(p =>
                {
                    var parts = p.Key.Split('\u0001');
                    return (Primary: parts[0], Secondary: parts[1], Value: p.Value);
                })
                .Where(p => term is null || term.Length == 0
                            || p.Primary.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || p.Secondary.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Primary, StringComparer.Ordinal)
                .ThenByDescending(p => p.Value.Income + p.Value.Expense)
                .Select(p => new FundReportRowDto
                {
                    Name = p.Primary,
                    SecondaryName = p.Secondary,
                    TotalIncome = p.Value.Income,
                    TotalExpense = p.Value.Expense,
                    Balance = p.Value.Income - p.Value.Expense,
                    IncomeCount = p.Value.IncomeCount,
                    ExpenseCount = p.Value.ExpenseCount,
                })
                .ToList();

            return new FundReportSectionDto
            {
                Name = $"{DimLabels[ComboPrimary]}×{DimLabels[secondary]}",
                SubtotalLabel = subtotalLabel,
                Rows = rows,
                Subtotal = SumRows(subtotalLabel, rows, withOpening: false),
            };
        }

        /// <summary>组合次类别归一：非法值回落项目（默认）。</summary>
        private static string ResolveSecondary(string? secondary)
        {
            var value = (secondary ?? DimProject).Trim().ToLowerInvariant();
            return ComboSecondaries.Contains(value) ? value : DimProject;
        }

        /// <summary>去掉类别档案上的方向前缀（收- / 支-），报表只显示业务名称。</summary>
        private static string StripTypePrefix(string name) =>
            name.StartsWith("收-", StringComparison.Ordinal) || name.StartsWith("支-", StringComparison.Ordinal)
                ? name[2..]
                : name;

        /// <summary>内部转账只影响账户余额，不计入收支 / 往来 / 项目 / 部门等业务维度（与资金报表一致）。</summary>
        private static bool IsNotTransfer(JournalEntry entry) =>
            !string.Equals(entry.Source, "内部转账", StringComparison.Ordinal);

        /// <summary>按 <paramref name="keyOf"/> 分组累计区间内的收入 / 支出与笔数；<paramref name="include"/> 为空表示全部计入。</summary>
        private static Dictionary<string, (decimal Income, decimal Expense, int IncomeCount, int ExpenseCount)> SumBy(
            IReadOnlyList<Journal> journals,
            DateOnly start,
            DateOnly end,
            Func<JournalEntry, string> keyOf,
            Func<JournalEntry, bool>? include = null)
        {
            var buckets = new Dictionary<string, (decimal Income, decimal Expense, int IncomeCount, int ExpenseCount)>(StringComparer.Ordinal);
            foreach (var journal in journals)
            {
                foreach (var entry in journal.Entries)
                {
                    if (entry.Date < start || entry.Date > end)
                    {
                        continue;
                    }

                    if (include is not null && !include(entry))
                    {
                        continue;
                    }

                    var key = keyOf(entry);
                    var current = buckets.GetValueOrDefault(key);
                    buckets[key] = (
                        current.Income + entry.Income,
                        current.Expense + entry.Expense,
                        current.IncomeCount + (entry.Income > 0m ? 1 : 0),
                        current.ExpenseCount + (entry.Expense > 0m ? 1 : 0));
                }
            }

            return buckets;
        }

        /* ---------------- 合计与工具 ---------------- */

        private static FundReportRowDto SumRows(string label, IReadOnlyList<FundReportRowDto> rows, bool withOpening)
        {
            return new FundReportRowDto
            {
                Name = label,
                OpeningBalance = withOpening ? rows.Sum(r => r.OpeningBalance ?? 0m) : null,
                TotalIncome = rows.Sum(r => r.TotalIncome),
                TotalExpense = rows.Sum(r => r.TotalExpense),
                Balance = rows.Sum(r => r.Balance),
                IncomeCount = rows.Sum(r => r.IncomeCount),
                ExpenseCount = rows.Sum(r => r.ExpenseCount),
            };
        }

        private static FundReportRowDto BuildTotal(IReadOnlyList<FundReportSectionDto> sections)
        {
            var all = sections.SelectMany(s => s.Rows).ToList();
            var withOpening = sections.Count > 0 && sections[0].Subtotal.OpeningBalance is not null;
            return SumRows("合计", all, withOpening);
        }

        private static string? NoteOf(string dimension) => dimension switch
        {
            FundReportDimensions.Category => "内部转账，不计入收支",
            FundReportDimensions.Counterparty => "内部转账，不计入往来",
            FundReportDimensions.Project => "内部转账，不计入项目",
            FundReportDimensions.Department => "内部转账，不计入部门",
            FundReportDimensions.Combo => "内部转账，不计入往来",
            _ => null,
        };

        private static string NormalizeDimension(string? dimension)
        {
            var value = (dimension ?? FundReportDimensions.Account).Trim().ToLowerInvariant();
            return FundReportDimensions.All.Contains(value) ? value : FundReportDimensions.Account;
        }

        private static (DateOnly Start, DateOnly End) ResolveRange(DateOnly? startDate, DateOnly? endDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var start = startDate ?? new DateOnly(today.Year, today.Month, 1);
            var end = endDate ?? today;
            if (start > end)
            {
                throw new FundReportException("起始日期不能晚于截止日期。");
            }

            return (start, end);
        }

        private async Task EnsureAccountSetExists(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new FundReportException("指定的账套不存在。");
            }
        }

        /// <summary>装载全部日记账（含分录）；日记账数量有限，逐本装载以复用聚合根的余额排序口径。</summary>
        private async Task<IReadOnlyList<Journal>> LoadJournalsWithEntriesAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var journals = await _journalRepository.GetAllAsync(accountSetId, cancellationToken);
            var result = new List<Journal>(journals.Count);
            foreach (var journal in journals)
            {
                var full = await _journalRepository.GetByIdAsync(accountSetId, journal.Id, cancellationToken);
                if (full is not null)
                {
                    result.Add(full);
                }
            }

            return result;
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsById(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }
    }
}
