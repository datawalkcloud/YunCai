using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Funds.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>资金报表与核对总账中的可预期错误。</summary>
    public sealed class FundReportException : Exception
    {
        public FundReportException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 资金报表应用服务：按 账户 / 收支类别 / 往来单位 三个维度汇总区间内收付，
    /// 并提供出纳账与总账的余额核对（核对总账）。
    /// 口径说明：日记账为手工登记台账（含未生成凭证的流水，全量参与汇总）；
    /// 核对总账的账面侧仅统计已过账凭证（与账簿一致）。
    /// </summary>
    public sealed class FundReportService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IJournalRepository _journalRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public FundReportService(
            IAccountSetRepository accountSetRepository,
            IJournalRepository journalRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _journalRepository = journalRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>按维度生成资金报表。</summary>
        public async Task<FundReportDto> GetReportAsync(
            Guid accountSetId,
            string? dimension,
            DateOnly? startDate,
            DateOnly? endDate,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var dim = NormalizeDimension(dimension);
            var (start, end) = ResolveRange(startDate, endDate);
            var journals = await LoadJournalsWithEntriesAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);

            var rows = dim switch
            {
                FundReportDimensions.Account => BuildAccountRows(journals, accountsById, start, end),
                FundReportDimensions.Category => BuildGroupRows(journals, start, end, e => e.Category, "未分类"),
                _ => BuildGroupRows(journals, start, end, e => e.Counterparty, "未填写"),
            };

            return new FundReportDto
            {
                Dimension = dim,
                StartDate = start,
                EndDate = end,
                Rows = rows,
                Total = BuildTotal(dim, rows),
            };
        }

        /// <summary>核对总账：逐账户对照出纳余额与总账科目余额，列出差异。</summary>
        public async Task<FundReconciliationDto> GetReconciliationAsync(
            Guid accountSetId,
            DateOnly? date,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var asOf = date ?? DateOnly.FromDateTime(DateTime.Today);
            var journals = await LoadJournalsWithEntriesAsync(accountSetId, cancellationToken);
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
            foreach (var journal in journals.OrderBy(j => j.Name, StringComparer.Ordinal))
            {
                // 出纳余额：期初 + 截至核对日的收入 − 支出（手工台账全量参与）。
                var journalBalance = journal.OpeningBalance
                    + journal.Entries.Where(e => e.Date <= asOf).Sum(e => e.Income - e.Expense);

                string? note = null;
                var ledgerBalance = 0m;
                if (accountsById.TryGetValue(journal.AccountId, out var account))
                {
                    ledgerBalance = ledgerNet.GetValueOrDefault(account.Id);
                }
                else
                {
                    note = "绑定科目不存在，请到「账户设置」中重新绑定。";
                }

                var difference = journalBalance - ledgerBalance;
                rows.Add(new FundReconciliationRowDto
                {
                    JournalId = journal.Id,
                    JournalName = journal.Name,
                    JournalType = (int)journal.Type,
                    AccountCode = accountsById.GetValueOrDefault(journal.AccountId)?.Code ?? string.Empty,
                    AccountName = accountsById.GetValueOrDefault(journal.AccountId)?.Name ?? string.Empty,
                    JournalBalance = journalBalance,
                    LedgerBalance = ledgerBalance,
                    Difference = difference,
                    Balanced = note is null && difference == 0m,
                    Note = note,
                });
            }

            return new FundReconciliationDto
            {
                Date = asOf,
                AllBalanced = rows.All(r => r.Balanced),
                Rows = rows,
            };
        }

        private static List<FundReportRowDto> BuildAccountRows(
            IReadOnlyList<Journal> journals,
            IReadOnlyDictionary<Guid, Account> accountsById,
            DateOnly start,
            DateOnly end)
        {
            var rows = new List<FundReportRowDto>();
            foreach (var journal in journals.OrderBy(j => j.Name, StringComparer.Ordinal))
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

            return rows;
        }

        private static List<FundReportRowDto> BuildGroupRows(
            IReadOnlyList<Journal> journals,
            DateOnly start,
            DateOnly end,
            Func<JournalEntry, string?> keySelector,
            string emptyLabel)
        {
            var groups = new Dictionary<string, (decimal Income, decimal Expense, int IncomeCount, int ExpenseCount)>();
            foreach (var journal in journals)
            {
                foreach (var entry in journal.Entries)
                {
                    if (entry.Date < start || entry.Date > end)
                    {
                        continue;
                    }

                    var key = keySelector(entry) ?? emptyLabel;
                    var current = groups.GetValueOrDefault(key);
                    groups[key] = (
                        current.Income + entry.Income,
                        current.Expense + entry.Expense,
                        current.IncomeCount + (entry.Income > 0m ? 1 : 0),
                        current.ExpenseCount + (entry.Expense > 0m ? 1 : 0));
                }
            }

            return groups
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
        }

        private static FundReportRowDto BuildTotal(string dimension, IReadOnlyList<FundReportRowDto> rows)
        {
            return new FundReportRowDto
            {
                Name = "总计",
                OpeningBalance = dimension == FundReportDimensions.Account
                    ? rows.Sum(r => r.OpeningBalance ?? 0m)
                    : null,
                TotalIncome = rows.Sum(r => r.TotalIncome),
                TotalExpense = rows.Sum(r => r.TotalExpense),
                Balance = rows.Sum(r => r.Balance),
                IncomeCount = rows.Sum(r => r.IncomeCount),
                ExpenseCount = rows.Sum(r => r.ExpenseCount),
            };
        }

        private static string NormalizeDimension(string? dimension)
        {
            var value = (dimension ?? FundReportDimensions.Account).Trim().ToLowerInvariant();
            return value switch
            {
                FundReportDimensions.Category => FundReportDimensions.Category,
                FundReportDimensions.Counterparty => FundReportDimensions.Counterparty,
                _ => FundReportDimensions.Account,
            };
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
