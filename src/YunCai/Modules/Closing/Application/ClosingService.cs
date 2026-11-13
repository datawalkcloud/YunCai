using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Closing.Domain;
using YunCai.Modules.FixedAssets.Domain;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Salary.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Closing.Application
{
    /// <summary>期末结转与结账中的可预期错误（期间顺序、数据校验等），用于向前端返回友好提示。</summary>
    public sealed class ClosingException : Exception
    {
        public ClosingException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 期末结转与结账应用服务：按自然月顺序执行「期末检查 → 结转损益 → 结账」闭环。
    /// 结转损益把损益类科目的累计净发生额（截至目标月末，仅已过账凭证）结平并转入「本年利润」，
    /// 生成一张（或按收入/成本拆成两张）结转凭证并直接过账；可选再把「本年利润」余额一次性
    /// 结转至「未分配利润」（通常用于年末）。系统生成的结转凭证带固定摘要（见常量），
    /// 用于「撤销结转」时精确识别。结账必须先完成当期结转损益（由 <see cref="AccountSet.ClosePeriod"/> 前置校验）。
    /// 结转凭证与手工凭证同样进入账簿与报表：资产负债表按 4103/3103 口径归集「本年利润」，
    /// 利润表按摘要为「结转损益」的凭证整体剔除，保证结转前后报表口径稳定。
    /// </summary>
    public sealed class ClosingService
    {
        /// <summary>结转损益凭证的分录摘要（同时作为系统结转凭证的识别标记）。</summary>
        public const string CarrySummary = "期末结转损益";

        /// <summary>本年利润结转未分配利润凭证的分录摘要（识别标记之一）。</summary>
        public const string RetainedSummary = "结转未分配利润";

        /// <summary>「本年利润」科目编码前缀（新准则 4103，兼容旧准则 3103）。</summary>
        private static readonly string[] ProfitAccountPrefixes = ["4103", "3103"];

        /// <summary>「未分配利润」科目编码前缀（利润分配 4104，兼容旧准则 3104）。</summary>
        private static readonly string[] RetainedAccountPrefixes = ["4104", "3104"];

        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IClosingTemplateRepository _closingTemplateRepository;
        private readonly ISalaryRepository _salaryRepository;
        private readonly IJournalRepository _journalRepository;
        private readonly IFixedAssetRepository _fixedAssetRepository;

        public ClosingService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            IClosingTemplateRepository closingTemplateRepository,
            ISalaryRepository salaryRepository,
            IJournalRepository journalRepository,
            IFixedAssetRepository fixedAssetRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _closingTemplateRepository = closingTemplateRepository;
            _salaryRepository = salaryRepository;
            _journalRepository = journalRepository;
            _fixedAssetRepository = fixedAssetRepository;
        }

        /// <summary>获取账套的期末结转总体状态（含逐月状态卡片）。</summary>
        public async Task<ClosingStatusDto> GetStatusAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);

            // 没有损益发生的期间自动视为已结转，不再要求用户逐月点「标记已结转」。
            await AutoAdvanceCarriedAsync(accountSet, accounts, vouchers, cancellationToken);

            var startKey = PeriodKey(accountSet.StartYear, accountSet.StartMonth);
            var latestVoucherKey = vouchers.Count == 0
                ? 0
                : vouchers.Max(v => PeriodKey(v.Date.Year, v.Date.Month));
            var (nextCarryYear, nextCarryMonth) = accountSet.NextPeriodToCarry();
            var (nextCloseYear, nextCloseMonth) = accountSet.NextPeriodToClose();

            // 月份卡片列到「最近有凭证的月份 / 已结转 / 已结账 / 下一个待结转期间」的最远者。
            var endKey = new[]
            {
                startKey,
                latestVoucherKey,
                accountSet.CarriedPeriod,
                accountSet.ClosedPeriod,
                PeriodKey(nextCarryYear, nextCarryMonth),
            }.Max();

            var periods = new List<ClosingPeriodDto>();
            var cursorYear = accountSet.StartYear;
            var cursorMonth = accountSet.StartMonth;
            while (PeriodKey(cursorYear, cursorMonth) <= endKey && periods.Count < 240)
            {
                var key = PeriodKey(cursorYear, cursorMonth);
                var monthVouchers = vouchers
                    .Where(v => v.Date.Year == cursorYear && v.Date.Month == cursorMonth)
                    .ToList();

                periods.Add(new ClosingPeriodDto
                {
                    Year = cursorYear,
                    Month = cursorMonth,
                    PeriodText = PeriodText(cursorYear, cursorMonth),
                    State = key <= accountSet.ClosedPeriod
                        ? "Closed"
                        : key <= accountSet.CarriedPeriod
                            ? "Carried"
                            : "NotCarried",
                    VoucherCount = monthVouchers.Count,
                    UnpostedCount = monthVouchers.Count(v => v.Status != VoucherStatus.Posted),
                });

                (cursorYear, cursorMonth) = NextMonth(cursorYear, cursorMonth);
            }

            return new ClosingStatusDto
            {
                StartPeriod = accountSet.StartPeriodText(),
                CarriedPeriod = accountSet.CarriedPeriodText(),
                ClosedPeriod = accountSet.ClosedPeriodText(),
                NextCarryYear = nextCarryYear,
                NextCarryMonth = nextCarryMonth,
                NextCloseYear = nextCloseYear,
                NextCloseMonth = nextCloseMonth,
                CanReopen = accountSet.ClosedPeriod != 0,
                CanCancelCarry = accountSet.CarriedPeriod != 0 && accountSet.CarriedPeriod > accountSet.ClosedPeriod,
                Periods = periods,
            };
        }

        /// <summary>
        /// 期末检查（第 1 步）：列出预置结转项的本期金额与可用性，供逐项生成凭证。
        /// 只读账簿数据，不落库；工资模块两项的金额与状态取自工资单。
        /// </summary>
        public async Task<PreCloseListDto> GetPreCloseAsync(Guid accountSetId, int year, int month, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(year, month);

            var monthEnd = MonthEnd(year, month);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();
            var balanceById = ComputeBalanceById(accounts, posted, monthEnd);

            var bills = await _salaryRepository.GetBillsAsync(accountSetId, year * 100 + month, cancellationToken);
            var surtaxRates = SurtaxRatesOf(accountSet);
            var items = new List<PreCloseItemDto>();
            var (debitById, creditById) = ClosingMath.ComputePeriodSums(posted, MonthStart(year, month), monthEnd);

            foreach (var definition in PreClosePlanner.Definitions)
            {
                items.Add(definition.Source switch
                {
                    PreCloseSource.Salary => BuildSalaryItem(definition, bills),
                    PreCloseSource.Check => BuildCheckItem(definition, accounts, balanceById, posted, year, month, accountSet.IncomeTaxRate, surtaxRates),
                    PreCloseSource.Ledger => BuildLedgerItem(definition, accounts, balanceById, posted, vouchers, year, month, accountSet.IncomeTaxRate, surtaxRates),
                    _ => new PreCloseItemDto
                    {
                        Key = definition.Key,
                        Title = definition.Title,
                        Source = "unsupported",
                        Available = false,
                        Hint = PreClosePlanner.UnsupportedHint(definition.Key),
                    },
                });
            }

            // 自定义结转模板同样在期末检查出卡片（柠檬云的计提工资、结转制造费用等即模板项）。
            var templates = await _closingTemplateRepository.GetAllAsync(accountSetId, cancellationToken);
            foreach (var template in templates.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                items.Add(BuildTemplateItem(template, vouchers, year, month, debitById, creditById, balanceById));
            }

            return new PreCloseListDto
            {
                Year = year,
                Month = month,
                PeriodText = PeriodText(year, month),
                Items = items,
            };
        }

        /// <summary>生成单个期末检查项的凭证并直接过账；工资类由调用方走对应模块，已入账检查项不出票。</summary>
        public async Task<GeneratePreCloseResultDto> GeneratePreCloseVoucherAsync(
            Guid accountSetId,
            string operatorName,
            GeneratePreCloseRequest request,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(request.Year, request.Month);
            EnsurePeriodOpen(accountSet, request.Year, request.Month);

            if (request.Key.StartsWith(TemplateKeyPrefix, StringComparison.Ordinal))
            {
                var idText = request.Key[TemplateKeyPrefix.Length..];
                if (!Guid.TryParse(idText, out var templateId))
                {
                    throw new ClosingException("结转模板标识不合法。");
                }

                return await GenerateTemplateVoucherAsync(
                    accountSet, accountSetId, operatorName, templateId, request.Year, request.Month, cancellationToken);
            }

            var definition = PreClosePlanner.Definitions.FirstOrDefault(d => d.Key == request.Key)
                             ?? throw new ClosingException("未知的结转项。");
            if (definition.Source == PreCloseSource.Salary)
            {
                throw new ClosingException("工资类结转项请在「工资」模块生成凭证。");
            }

            if (definition.Source == PreCloseSource.Check)
            {
                throw new ClosingException($"「{definition.Title}」是已入账检查项，本期不需要生成凭证。");
            }

            if (definition.Source != PreCloseSource.Ledger)
            {
                throw new ClosingException(PreClosePlanner.UnsupportedHint(definition.Key));
            }

            var year = request.Year;
            var month = request.Month;
            var monthEnd = MonthEnd(year, month);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();
            var balanceById = ComputeBalanceById(accounts, posted, monthEnd);

            var summary = ItemSummary(month, definition.Title);
            var existing = vouchers
                .Where(v => v.Date.Year == year && v.Date.Month == month
                            && v.Entries.Count > 0
                            && v.Entries.All(e => e.Summary == summary))
                .OrderBy(v => v.Number)
                .FirstOrDefault();
            if (existing is not null)
            {
                throw new ClosingException($"本期已生成过「{definition.Title}」凭证（{existing.Word}-{existing.Number}），如需重做请先删除该凭证。");
            }

            var plan = PreClosePlanner.Build(
                definition.Key, summary, accounts, balanceById, accountSet.IncomeTaxRate, SurtaxRatesOf(accountSet), posted, year, monthEnd);
            if (!plan.Available)
            {
                throw new ClosingException(plan.Hint);
            }

            if (plan.Amount <= 0m || plan.Lines.Count == 0)
            {
                throw new ClosingException($"本期「{definition.Title}」无可结转金额。");
            }

            var word = PreferredWord(accountSet);
            var nextNumber = await _voucherRepository.GetMaxNumberAsync(accountSetId, word, cancellationToken) + 1;
            var voucher = Voucher.Create(accountSetId, word, nextNumber, monthEnd, operatorName);
            foreach (var line in plan.Lines)
            {
                voucher.AddEntry(line.Summary, line.AccountId, line.Debit, line.Credit);
            }

            voucher.Post();
            await _voucherRepository.AddAsync(voucher, cancellationToken);
            // 仓储的 AddAsync 只入跟踪，不提交；漏掉这一步会让「已生成」的凭证随请求结束丢失，
            // 结转时也就看不到计提的所得税。
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            return new GeneratePreCloseResultDto
            {
                Message = $"已生成「{definition.Title}」凭证并自动过账：{voucher.Word}-{voucher.Number}，金额 {plan.Amount:0.00} 元。",
                Voucher = new CarryVoucherRefDto
                {
                    Id = voucher.Id,
                    Word = voucher.Word,
                    Number = voucher.Number,
                    Label = $"{voucher.Word}-{voucher.Number}",
                    Summary = summary,
                    Kind = "PreClose",
                },
            };
        }

        /// <summary>期间闸门：已结账或已结转损益的期间不再生成期末检查项凭证。</summary>
        private static void EnsurePeriodOpen(AccountSet accountSet, int year, int month)
        {
            var key = PeriodKey(year, month);
            if (key <= accountSet.ClosedPeriod)
            {
                throw new ClosingException($"该期间已结账（已结账至 {accountSet.ClosedPeriodText()}），不能再生成凭证。");
            }

            if (key <= accountSet.CarriedPeriod)
            {
                throw new ClosingException("该期间已结转损益，如需补录请先「撤销结转」。");
            }
        }

        /// <summary>按单个自定义结转模板生成凭证并直接过账（第 1 步期末检查用，不推进结转进度）。</summary>
        private async Task<GeneratePreCloseResultDto> GenerateTemplateVoucherAsync(
            AccountSet accountSet,
            Guid accountSetId,
            string operatorName,
            Guid templateId,
            int year,
            int month,
            CancellationToken cancellationToken)
        {
            var template = await _closingTemplateRepository.GetByIdAsync(accountSetId, templateId, cancellationToken)
                           ?? throw new ClosingException("结转模板不存在，请刷新后重试。");

            var monthEnd = MonthEnd(year, month);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();
            var balanceById = ComputeBalanceById(accounts, posted, monthEnd);
            var (debitById, creditById) = ClosingMath.ComputePeriodSums(posted, MonthStart(year, month), monthEnd);

            var lines = TemplateLines(template, debitById, creditById, balanceById);
            if (lines.Count == 0)
            {
                throw new ClosingException($"本期「{template.Name}」按模板取数全为零，无可结转金额。");
            }

            var existing = FindTemplateVoucher(vouchers, year, month, lines);
            if (existing is not null)
            {
                throw new ClosingException($"本期已生成过「{template.Name}」凭证（{existing.Word}-{existing.Number}），如需重做请先删除该凭证。");
            }

            var word = PreferredWord(accountSet);
            var nextNumber = await _voucherRepository.GetMaxNumberAsync(accountSetId, word, cancellationToken) + 1;
            var voucher = BuildTemplateVoucher(
                accountSetId, word, nextNumber, monthEnd, operatorName, template, debitById, creditById, balanceById)
                ?? throw new ClosingException($"本期「{template.Name}」无可结转金额。");

            await _voucherRepository.AddAsync(voucher, cancellationToken);
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            var totalDebit = lines.Where(x => x.Line.Direction == BalanceDirection.Debit).Sum(x => x.Amount);
            return new GeneratePreCloseResultDto
            {
                Message = $"已按模板「{template.Name}」生成凭证并自动过账：{voucher.Word}-{voucher.Number}，金额 {totalDebit:0.00} 元。",
                Voucher = new CarryVoucherRefDto
                {
                    Id = voucher.Id,
                    Word = voucher.Word,
                    Number = voucher.Number,
                    Label = $"{voucher.Word}-{voucher.Number}",
                    Summary = voucher.Entries.Count > 0 ? voucher.Entries[0].Summary : string.Empty,
                    Kind = "Template",
                },
            };
        }

        /// <summary>
        /// 年末补做：为已结转损益的 12 月生成「本年利润 → 利润分配—未分配利润」凭证并自动过账。
        /// 不改变结转进度；已有同类凭证或本年利润无余额时直接返回说明，不重复生成。
        /// </summary>
        public async Task<TransferRetainedResultDto> TransferRetainedEarningsAsync(
            Guid accountSetId,
            string operatorName,
            int year,
            int month,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(year, month);
            if (month != 12)
            {
                throw new ClosingException("年末结转未分配利润仅适用于 12 月。");
            }

            var key = PeriodKey(year, month);
            if (key <= accountSet.ClosedPeriod)
            {
                throw new ClosingException($"该期间已结账（已结账至 {accountSet.ClosedPeriodText()}），不能再生成凭证。");
            }

            if (key > accountSet.CarriedPeriod)
            {
                throw new ClosingException("该期间尚未结转损益，请先完成结转损益后再生成年末结转凭证。");
            }

            var monthEnd = MonthEnd(year, month);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();

            var unpostedCount = vouchers.Count(v => v.Status != VoucherStatus.Posted && v.Date <= monthEnd);
            if (unpostedCount > 0)
            {
                throw new ClosingException($"截至 {PeriodText(year, month)} 存在 {unpostedCount} 张未过账凭证，请先保存入账后再生成。");
            }

            var profitAccountIds = ProfitAccounts(accounts).Select(a => a.Id).ToHashSet();
            var retainedAccountIds = accounts
                .Where(a => RetainedAccountPrefixes.Contains(Prefix(a.Code)))
                .Select(a => a.Id)
                .ToHashSet();

            var existing = vouchers
                .Where(v => v.Date.Year == year && v.Date.Month == month
                            && IsRetainedVoucher(v, profitAccountIds, retainedAccountIds))
                .OrderBy(v => v.Number)
                .FirstOrDefault();
            if (existing is not null)
            {
                return new TransferRetainedResultDto
                {
                    Message = $"本期已生成结转未分配利润凭证 {existing.Word}-{existing.Number}，无需重复生成。",
                    Voucher = new CarryVoucherRefDto
                    {
                        Id = existing.Id,
                        Word = existing.Word,
                        Number = existing.Number,
                        Label = $"{existing.Word}-{existing.Number}",
                        Summary = RetainedSummary,
                        Kind = "Retained",
                    },
                };
            }

            var balanceById = ComputeBalanceById(accounts, posted, monthEnd);
            var profitBalances = ProfitBalances(accounts, balanceById);
            var profitNet = profitBalances.Sum(x => x.Value);
            if (profitNet == 0m)
            {
                return new TransferRetainedResultDto { Message = "「本年利润」余额为 0，无需结转未分配利润。" };
            }

            var retainedAccount = ResolveRetainedAccount(accounts)
                ?? throw new ClosingException("未找到「未分配利润」科目（编码 4104 或 3104），请先在「设置 - 科目」中建立后再结转。");

            var word = PreferredWord(accountSet);
            var nextNumber = await _voucherRepository.GetMaxNumberAsync(accountSetId, word, cancellationToken) + 1;
            var voucher = BuildRetainedVoucher(
                accountSetId, profitBalances, retainedAccount, word, nextNumber, monthEnd, operatorName);
            await _voucherRepository.AddAsync(voucher, cancellationToken);
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            var amountText = Math.Abs(profitNet).ToString("0.00");
            return new TransferRetainedResultDto
            {
                Message = profitNet < 0m
                    ? $"已生成年末结转凭证 {voucher.Word}-{voucher.Number}：本年利润 {amountText} 元转入未分配利润。"
                    : $"已生成年末结转凭证 {voucher.Word}-{voucher.Number}：本年亏损 {amountText} 元转入未分配利润。",
                Created = true,
                Voucher = new CarryVoucherRefDto
                {
                    Id = voucher.Id,
                    Word = voucher.Word,
                    Number = voucher.Number,
                    Label = $"{voucher.Word}-{voucher.Number}",
                    Summary = RetainedSummary,
                    Kind = "Retained",
                },
            };
        }

        /// <summary>期末检查：给出指定期间的检查项清单与「可否结转 / 可否结账」结论。</summary>
        public async Task<ClosingCheckResultDto> CheckAsync(Guid accountSetId, int year, int month, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(year, month);

            var key = PeriodKey(year, month);
            var monthEnd = MonthEnd(year, month);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            await AutoAdvanceCarriedAsync(accountSet, accounts, vouchers, cancellationToken);
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();
            var upToEnd = vouchers.Where(v => v.Date <= monthEnd).ToList();
            var monthVouchers = vouchers.Where(v => v.Date.Year == year && v.Date.Month == month).ToList();

            var items = new List<ClosingCheckItemDto>();

            var unposted = upToEnd
                .Where(v => v.Status != VoucherStatus.Posted)
                .OrderBy(v => v.Date)
                .ToList();
            var openingNetTotal = accounts
                .Where(a => a.IsLeaf && a.OpeningBalance != 0m)
                .Sum(a => a.Direction == BalanceDirection.Debit ? a.OpeningBalance : -a.OpeningBalance);
            var openingBalanced = openingNetTotal == 0m;

            // 1) 期末检查：截至本期末的凭证是否已全部过账，期初是否平衡。
            if (unposted.Count > 0)
            {
                items.Add(Check("preclose", "期末检查", "error",
                    $"截至 {PeriodText(year, month)} 存在 {unposted.Count} 张未过账凭证（最早 {unposted[0].Date:yyyy-MM-dd}），请先保存入账后再结账。"));
            }
            else if (!openingBalanced)
            {
                items.Add(Check("preclose", "期末检查", "warn",
                    $"凭证均已过账，但期初余额借贷不平衡（差额 {openingNetTotal:0.00}），请在「设置 - 期初」中调整。"));
            }
            else
            {
                items.Add(Check("preclose", "期末检查", "ok", "截至本期末的凭证均已过账。"));
            }

            // 2) 结转损益：本期已结转；或本期无损益发生额（无需结转）。
            var balanceById = ComputeBalanceById(accounts, posted, monthEnd);
            var pnlBalances = PnlBalances(accounts, balanceById);
            if (key <= accountSet.CarriedPeriod)
            {
                items.Add(Check("carry", "结转损益", "ok", $"{PeriodText(year, month)} 已结转损益。"));
            }
            else if (pnlBalances.Count == 0)
            {
                items.Add(Check("carry", "结转损益", "ok", "本期无损益发生额，无需结转。"));
            }
            else
            {
                items.Add(Check("carry", "结转损益", "warn", "本期尚未结转损益，请先执行「结转损益」。"));
            }

            // 3) 凭证审核：本系统没有审核环节，保存即入账（与柠檬云「未启用」口径一致）。
            items.Add(Check("review", "凭证审核", "ok", "未启用凭证审核，保存即入账。"));

            // 4) 断号检查：同一凭证字内号码是否连续。
            var gaps = FindNumberingGaps(monthVouchers);
            items.Add(gaps.Count == 0
                ? Check("numbering", "断号检查", "ok", "本期凭证号码连续，未发现断号。")
                : Check("numbering", "断号检查", "warn",
                    $"本期存在断号凭证：{string.Join("、", gaps.Take(10))}。",
                    actionKey: "numbering", actionText: "查看"));

            // 5) 报表检查：期初余额 + 截至本期末的已过账发生额试算平衡。
            var postedNet = posted
                .Where(v => v.Date <= monthEnd)
                .Sum(v => v.Entries.Sum(e => e.Debit - e.Credit));
            var trialNet = openingNetTotal + postedNet;
            items.Add(trialNet == 0m
                ? Check("report", "报表检查", "ok", "试算平衡，报表数据完整。")
                : Check("report", "报表检查", "error", $"试算不平衡（差额 {trialNet:0.00}），请检查凭证与期初数据。"));

            // 6) 资金检查（仅提示）：本期资金流水是否都已生成凭证。
            var fundEntries = await _journalRepository.GetFundEntriesRangeAsync(accountSetId, null, monthEnd, cancellationToken);
            var unvoucheredFunds = fundEntries.Count(x =>
                x.Entry.Date.Year == year && x.Entry.Date.Month == month && x.Entry.VoucherId is null);
            items.Add(unvoucheredFunds == 0
                ? Check("funds", "资金检查", "ok", "本期资金流水均已生成凭证。", "info")
                : Check("funds", "资金检查", "warn", $"本期有 {unvoucheredFunds} 笔资金流水尚未生成凭证。", "info", "funds", "点击查看"));

            // 7) 资产检查（仅提示）：资产类科目是否出现贷方余额。
            var negativeAssets = accounts
                .Where(a => a.IsLeaf && a.Category == AccountCategory.Asset && balanceById.GetValueOrDefault(a.Id) < 0m)
                .ToList();
            items.Add(negativeAssets.Count == 0
                ? Check("asset", "资产检查", "ok", "资产类科目余额方向正常。", "info")
                : Check("asset", "资产检查", "warn",
                    $"有 {negativeAssets.Count} 个资产类科目为贷方余额（如 {negativeAssets[0].Name}），请核实。", "info"));

            // 8) 资产检查-未折旧（仅提示）：启用资产模块时，本期应完成折旧。
            if (!accountSet.AssetsModuleEnabled)
            {
                items.Add(Check("depreciation", "资产检查-未折旧", "ok", "未启用固定资产模块。", "info"));
            }
            else
            {
                var assets = await _fixedAssetRepository.GetAssetsAsync(accountSetId, cancellationToken);
                var activeAssetCount = assets.Count(a => a.Status == FixedAssetStatus.InUse);
                var changes = await _fixedAssetRepository.GetChangesAsync(accountSetId, null, cancellationToken);
                var depreciated = changes.Any(c => c.Kind == FixedAssetChangeKind.Depreciation && c.Period == key);
                if (activeAssetCount == 0)
                {
                    items.Add(Check("depreciation", "资产检查-未折旧", "ok", "无使用中的固定资产。", "info"));
                }
                else if (depreciated)
                {
                    items.Add(Check("depreciation", "资产检查-未折旧", "ok", "本期已完成折旧计提。", "info"));
                }
                else
                {
                    items.Add(Check("depreciation", "资产检查-未折旧", "warn",
                        $"资产模块本期未进行折旧（使用中资产 {activeAssetCount} 张）。", "info", "depreciation", "点击查看"));
                }
            }

            var blockingError = items.Any(i => i.Group == "blocking" && i.Status == "error");
            var isCarried = key <= accountSet.CarriedPeriod;
            var (nextCarryYear, nextCarryMonth) = accountSet.NextPeriodToCarry();
            var (nextCloseYear, nextCloseMonth) = accountSet.NextPeriodToClose();

            return new ClosingCheckResultDto
            {
                Year = year,
                Month = month,
                PeriodText = PeriodText(year, month),
                CanCarry = !blockingError
                           && key > accountSet.ClosedPeriod
                           && (isCarried || key == PeriodKey(nextCarryYear, nextCarryMonth)),
                CanClose = !blockingError
                           && isCarried
                           && key > accountSet.ClosedPeriod
                           && key == PeriodKey(nextCloseYear, nextCloseMonth),
                Items = items,
            };
        }

        /// <summary>
        /// 结转损益：生成结转凭证并自动过账（模板 → 损益结转 → 可选未分配利润），随后把账套标记为已结转。
        /// 仅在下一个待结转期间、且不存在未过账凭证时可执行。
        /// </summary>
        public async Task<CarryForwardResultDto> CarryForwardAsync(
            Guid accountSetId,
            string operatorName,
            CarryForwardRequest request,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(request.Year, request.Month);

            var year = request.Year;
            var month = request.Month;
            var key = PeriodKey(year, month);
            var monthEnd = MonthEnd(year, month);

            if (key <= accountSet.ClosedPeriod)
            {
                throw new ClosingException($"该期间已结账（已结账至 {accountSet.ClosedPeriodText()}），不能再结转。");
            }

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);

            // 目标期间之前的空期间自动标记为已结转，允许直接结转第一个有损益的月份。
            await AutoAdvanceCarriedAsync(accountSet, accounts, vouchers, cancellationToken);

            if (key <= accountSet.CarriedPeriod)
            {
                throw new ClosingException($"该期间已结转损益（已结转至 {accountSet.CarriedPeriodText()}），如需重做请先「撤销结转」。");
            }

            var (nextYear, nextMonth) = accountSet.NextPeriodToCarry();
            if (year != nextYear || month != nextMonth)
            {
                throw new ClosingException($"只能按顺序结转，下一个待结转期间为 {PeriodText(nextYear, nextMonth)}。");
            }

            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();

            var unpostedCount = vouchers.Count(v => v.Status != VoucherStatus.Posted && v.Date <= monthEnd);
            if (unpostedCount > 0)
            {
                throw new ClosingException($"截至 {PeriodText(year, month)} 存在 {unpostedCount} 张未过账凭证，请先保存入账后再结账。");
            }

            var balanceById = ComputeBalanceById(accounts, posted, monthEnd);
            var word = PreferredWord(accountSet);
            var nextNumber = await _voucherRepository.GetMaxNumberAsync(accountSetId, word, cancellationToken) + 1;
            var build = await BuildCarryVouchersAsync(
                accountSetId,
                accounts,
                posted,
                balanceById,
                word,
                nextNumber,
                monthEnd,
                year,
                month,
                operatorName,
                request,
                cancellationToken);

            accountSet.MarkCarried(year, month);

            foreach (var (voucher, _) in build.Vouchers)
            {
                await _voucherRepository.AddAsync(voucher, cancellationToken);
            }

            await _accountSetRepository.SaveChangesAsync(cancellationToken);

            return new CarryForwardResultDto
            {
                PeriodText = PeriodText(year, month),
                ProfitAmount = build.ProfitAmount,
                Message = CarryMessage(build),
                Vouchers = build.Vouchers
                    .Select(x => new CarryVoucherRefDto
                    {
                        Id = x.Voucher.Id,
                        Word = x.Voucher.Word,
                        Number = x.Voucher.Number,
                        Label = $"{x.Voucher.Word}-{x.Voucher.Number}",
                        Summary = x.Voucher.Entries.Count > 0 ? x.Voucher.Entries[0].Summary : string.Empty,
                        Kind = x.Kind,
                    })
                    .ToList(),
            };
        }

        /// <summary>构造检查项（默认计入「影响结账」分组）。</summary>
        private static ClosingCheckItemDto Check(
            string key,
            string title,
            string status,
            string message,
            string group = "blocking",
            string? actionKey = null,
            string? actionText = null) =>
            new()
            {
                Key = key,
                Title = title,
                Status = status,
                Message = message,
                Group = group,
                ActionKey = actionKey,
                ActionText = actionText,
            };

        /// <summary>查找同一凭证字内的断号（返回「字-号」文本）。</summary>
        private static List<string> FindNumberingGaps(IReadOnlyList<Voucher> vouchers)
        {
            var gaps = new List<string>();
            foreach (var group in vouchers.Where(v => v.Number > 0).GroupBy(v => v.Word))
            {
                var numbers = group.Select(v => v.Number).Distinct().OrderBy(n => n).ToList();
                for (var i = 1; i < numbers.Count; i++)
                {
                    for (var n = numbers[i - 1] + 1; n < numbers[i]; n++)
                    {
                        gaps.Add($"{group.Key}-{n}");
                    }
                }
            }

            return gaps;
        }

        /// <summary>同一凭证字内号码是否不连续。</summary>
        private static bool HasNumberingGap(IEnumerable<int> numbers)
        {
            var sorted = numbers.Distinct().OrderBy(n => n).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] - sorted[i - 1] > 1)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>已入账检查项：金额取本期发生额，命中凭证供「查看凭证」，不生成新凭证。</summary>
        private static PreCloseItemDto BuildCheckItem(
            PreCloseDefinition definition,
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> balanceById,
            IReadOnlyList<Voucher> postedVouchers,
            int year,
            int month,
            decimal incomeTaxRate,
            SurtaxRates surtaxRates)
        {
            var plan = PreClosePlanner.Build(
                definition.Key, string.Empty, accounts, balanceById, incomeTaxRate, surtaxRates, postedVouchers, year, MonthEnd(year, month));
            var first = plan.SourceVouchers.FirstOrDefault();

            return new PreCloseItemDto
            {
                Key = definition.Key,
                Title = definition.Title,
                Amount = plan.Amount,
                Available = false,
                Source = "check",
                Hint = plan.Hint,
                Generated = first is not null,
                VoucherLabel = first is null ? null : $"{first.Word}-{first.Number}",
                VoucherId = first?.Id,
            };
        }

        /// <summary>账簿类预置结转项：按余额计算金额，并识别本期是否已生成过凭证。</summary>
        private static PreCloseItemDto BuildLedgerItem(
            PreCloseDefinition definition,
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> balanceById,
            IReadOnlyList<Voucher> postedVouchers,
            IReadOnlyList<Voucher> vouchers,
            int year,
            int month,
            decimal incomeTaxRate,
            SurtaxRates surtaxRates)
        {
            var summary = ItemSummary(month, definition.Title);
            var existing = vouchers
                .Where(v => v.Date.Year == year && v.Date.Month == month
                            && v.Entries.Count > 0
                            && v.Entries.All(e => e.Summary == summary))
                .OrderBy(v => v.Number)
                .FirstOrDefault();
            var plan = PreClosePlanner.Build(
                definition.Key, summary, accounts, balanceById, incomeTaxRate, surtaxRates, postedVouchers, year, MonthEnd(year, month));

            return new PreCloseItemDto
            {
                Key = definition.Key,
                Title = definition.Title,
                Amount = plan.Amount,
                Available = plan.Available && existing is null && plan.Amount > 0m,
                Source = "ledger",
                Hint = existing is not null
                    ? $"本期已生成凭证 {existing.Word}-{existing.Number}。"
                    : plan.Hint,
                Generated = existing is not null,
                VoucherLabel = existing is null ? null : $"{existing.Word}-{existing.Number}",
                VoucherId = existing?.Id,
            };
        }

        /// <summary>工资类预置结转项：金额与生成状态取自工资单。</summary>
        private static PreCloseItemDto BuildSalaryItem(
            PreCloseDefinition definition,
            IReadOnlyList<SalaryBill> bills)
        {
            var isAccrue = definition.Key == "salary-accrue";
            var amount = isAccrue ? bills.Sum(b => b.Cost) : bills.Sum(b => b.Net);
            var generated = bills.Count > 0
                            && bills.All(b => isAccrue ? b.AccruedVoucherId is not null : b.PaidVoucherId is not null);

            var hint = bills.Count == 0
                ? "本期工资单尚未生成，请先在「工资」模块录入并生成工资单。"
                : generated
                    ? "本期已生成工资凭证。"
                    : isAccrue
                        ? $"按工资单员工成本合计 {amount:0.00} 计提（含公司承担款项）。"
                        : $"按工资单实发合计 {amount:0.00} 发放。";

            return new PreCloseItemDto
            {
                Key = definition.Key,
                Title = definition.Title,
                Amount = amount,
                Available = bills.Count > 0 && !generated,
                Source = "salary",
                Hint = hint,
                Generated = generated,
            };
        }

        /// <summary>自定义结转模板项：金额按模板行本期取数，可单独生成一张凭证。</summary>
        private static PreCloseItemDto BuildTemplateItem(
            ClosingTemplate template,
            IReadOnlyList<Voucher> vouchers,
            int year,
            int month,
            IReadOnlyDictionary<Guid, decimal> debitById,
            IReadOnlyDictionary<Guid, decimal> creditById,
            IReadOnlyDictionary<Guid, decimal> balanceById)
        {
            var lines = TemplateLines(template, debitById, creditById, balanceById);
            var totalDebit = lines.Where(x => x.Line.Direction == BalanceDirection.Debit).Sum(x => x.Amount);
            var totalCredit = lines.Where(x => x.Line.Direction == BalanceDirection.Credit).Sum(x => x.Amount);
            var balanced = totalDebit == totalCredit;
            var existing = FindTemplateVoucher(vouchers, year, month, lines);

            var hint = existing is not null
                ? $"本期已生成凭证 {existing.Word}-{existing.Number}。"
                : lines.Count == 0
                    ? "本期按模板取数全为零，无需生成。"
                    : balanced
                        ? $"按模板本期取数计算，借贷各 {totalDebit:0.00}。"
                        : $"模板试算不平衡（借方 {totalDebit:0.00}，贷方 {totalCredit:0.00}），请检查模板设置。";

            return new PreCloseItemDto
            {
                Key = TemplateKey(template.Id),
                Title = template.Name,
                Amount = balanced ? totalDebit : 0m,
                Available = balanced && lines.Count > 0 && existing is null,
                Source = "template",
                Hint = hint,
                Generated = existing is not null,
                VoucherLabel = existing is null ? null : $"{existing.Word}-{existing.Number}",
                VoucherId = existing?.Id,
            };
        }

        private const string TemplateKeyPrefix = "template:";

        /// <summary>模板类结转项的标识（前端原样回传给生成接口）。</summary>
        private static string TemplateKey(Guid templateId) => TemplateKeyPrefix + templateId;

        /// <summary>模板行按本期数据取数；零金额行不参与，与生成凭证同一口径。</summary>
        private static List<(ClosingTemplateLine Line, decimal Amount)> TemplateLines(
            ClosingTemplate template,
            IReadOnlyDictionary<Guid, decimal> debitById,
            IReadOnlyDictionary<Guid, decimal> creditById,
            IReadOnlyDictionary<Guid, decimal> balanceById)
        {
            return template.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => (Line: l, Amount: ClosingMath.ComputeLineAmount(l, debitById, creditById, balanceById)))
                .Where(x => x.Amount != 0m)
                .ToList();
        }

        /// <summary>
        /// 本期是否已有与该模板试算结果逐行一致的凭证。
        /// 模板凭证的摘要取自模板行、不带期间标识，只能按「科目 + 方向 + 金额」比对。
        /// </summary>
        private static Voucher? FindTemplateVoucher(
            IReadOnlyList<Voucher> vouchers,
            int year,
            int month,
            IReadOnlyList<(ClosingTemplateLine Line, decimal Amount)> lines)
        {
            if (lines.Count == 0)
            {
                return null;
            }

            var expected = lines
                .Select(x => (x.Line.AccountId,
                    Debit: x.Line.Direction == BalanceDirection.Debit ? x.Amount : 0m,
                    Credit: x.Line.Direction == BalanceDirection.Credit ? x.Amount : 0m))
                .OrderBy(x => x.AccountId).ThenBy(x => x.Debit).ThenBy(x => x.Credit)
                .ToList();

            return vouchers
                .Where(v => v.Date.Year == year && v.Date.Month == month && v.Entries.Count == expected.Count)
                .Where(v => v.Entries
                    .Select(e => (e.AccountId, e.Debit, e.Credit))
                    .OrderBy(x => x.AccountId).ThenBy(x => x.Debit).ThenBy(x => x.Credit)
                    .SequenceEqual(expected))
                .OrderBy(v => v.Number)
                .FirstOrDefault();
        }

        /// <summary>预置结转项的分录摘要（同时作为「本期是否已生成」的识别标记）。</summary>
        private static string ItemSummary(int month, string title) => $"{month}月 {title}";

        /// <summary>账套设置的附加税费率。</summary>
        private static SurtaxRates SurtaxRatesOf(AccountSet accountSet) =>
            new(accountSet.CityBuildTaxRate, accountSet.EducationSurchargeRate, accountSet.LocalEducationSurchargeRate);

        /// <summary>
        /// 结转损益预览：按当前数据计算将要生成的凭证（含选中的结转模板），不落库。
        /// 本期已结转时返回已生成的结转凭证，供第 2 步展示与撤销结转。
        /// </summary>
        public async Task<CarryForwardPreviewDto> PreviewCarryForwardAsync(
            Guid accountSetId,
            CarryForwardRequest request,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(request.Year, request.Month);

            var year = request.Year;
            var month = request.Month;
            var key = PeriodKey(year, month);
            var monthEnd = MonthEnd(year, month);

            if (key <= accountSet.ClosedPeriod)
            {
                throw new ClosingException($"该期间已结账（已结账至 {accountSet.ClosedPeriodText()}），不能再结转。");
            }

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            await AutoAdvanceCarriedAsync(accountSet, accounts, vouchers, cancellationToken);
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();
            var accountsById = accounts.ToDictionary(a => a.Id);

            if (key <= accountSet.CarriedPeriod)
            {
                var profitAccountIds = accounts
                    .Where(a => ProfitAccountPrefixes.Contains(Prefix(a.Code)))
                    .Select(a => a.Id)
                    .ToHashSet();
                var retainedAccountIds = accounts
                    .Where(a => RetainedAccountPrefixes.Contains(Prefix(a.Code)))
                    .Select(a => a.Id)
                    .ToHashSet();
                var existing = vouchers
                    .Where(v => v.Date.Year == year && v.Date.Month == month && IsSystemCarryVoucher(v, profitAccountIds))
                    .OrderBy(v => v.Number)
                    .ToList();

                var endBalanceById = ComputeBalanceById(accounts, posted, monthEnd);

                // 年末（12 月）补做结转未分配利润所需的余额与去重标记（4103/3103 全部合计）。
                var retainedProfitBalances = month == 12
                    ? ProfitBalances(accounts, endBalanceById)
                    : new List<KeyValuePair<Account, decimal>>();
                var retainedProfitAmount = -retainedProfitBalances.Sum(x => x.Value);
                var hasRetainedVoucher = vouchers.Any(v => v.Date.Year == year && v.Date.Month == month
                    && IsRetainedVoucher(v, profitAccountIds, retainedAccountIds));

                // 未生成时给出年末凭证预览，让用户在确认前看到与生成结果一致的分录。
                CarryPreviewVoucherDto? retainedVoucher = null;
                if (retainedProfitAmount != 0m && !hasRetainedVoucher
                    && ResolveRetainedAccount(accounts) is { } retainedAccount)
                {
                    var previewWord = PreferredWord(accountSet);
                    var previewNumber = await _voucherRepository.GetMaxNumberAsync(accountSetId, previewWord, cancellationToken) + 1;
                    retainedVoucher = ToPreviewVoucher(
                        BuildRetainedVoucher(
                            accountSetId, retainedProfitBalances, retainedAccount, previewWord, previewNumber, monthEnd, "预览"),
                        "Retained",
                        accountsById,
                        new Dictionary<Guid, decimal>(endBalanceById));
                }

                // 逐行余额的起点：期末余额扣掉已有结转凭证的影响，按凭证顺序回放。
                var carryDeltaById = existing
                    .SelectMany(v => v.Entries)
                    .GroupBy(e => e.AccountId)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Debit - e.Credit));
                var balanceBeforeCarry = endBalanceById.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value - carryDeltaById.GetValueOrDefault(kv.Key));

                return new CarryForwardPreviewDto
                {
                    Year = year,
                    Month = month,
                    PeriodText = PeriodText(year, month),
                    AlreadyCarried = true,
                    NeedsCarry = false,
                    ProfitAmount = existing
                        .SelectMany(v => v.Entries)
                        .Where(e => profitAccountIds.Contains(e.AccountId) && IsCarrySummary(e.Summary))
                        .Sum(e => -(e.Debit - e.Credit)),
                    RetainedProfitAmount = retainedProfitAmount,
                    HasRetainedVoucher = hasRetainedVoucher,
                    RetainedVoucher = retainedVoucher,
                    Vouchers = ToPreviewVouchers(
                        existing.Select(v => (v, KindOf(v))), accountsById, balanceBeforeCarry),
                };
            }

            var (nextYear, nextMonth) = accountSet.NextPeriodToCarry();
            if (year != nextYear || month != nextMonth)
            {
                throw new ClosingException($"只能按顺序结转，下一个待结转期间为 {PeriodText(nextYear, nextMonth)}。");
            }

            var warnings = new List<string>();
            var unpostedCount = vouchers.Count(v => v.Status != VoucherStatus.Posted && v.Date <= monthEnd);
            if (unpostedCount > 0)
            {
                warnings.Add($"截至 {PeriodText(year, month)} 存在 {unpostedCount} 张未过账凭证，结转前需先保存入账。");
            }

            var balanceById = ComputeBalanceById(accounts, posted, monthEnd);
            var word = PreferredWord(accountSet);
            var nextNumber = await _voucherRepository.GetMaxNumberAsync(accountSetId, word, cancellationToken) + 1;
            var build = await BuildCarryVouchersAsync(
                accountSetId,
                accounts,
                posted,
                balanceById,
                word,
                nextNumber,
                monthEnd,
                year,
                month,
                // 预览凭证不落库，制单人用占位名称（Voucher.Create 要求非空）。
                "预览",
                request,
                cancellationToken);

            return new CarryForwardPreviewDto
            {
                Year = year,
                Month = month,
                PeriodText = PeriodText(year, month),
                AlreadyCarried = false,
                NeedsCarry = build.Vouchers.Count > 0,
                ProfitAmount = build.ProfitAmount,
                Vouchers = ToPreviewVouchers(build.Vouchers, accountsById, balanceById),
                Warnings = warnings,
            };
        }

        /// <summary>结转结果（内存对象，尚未落库）。</summary>
        private sealed record CarryBuildResult(
            List<(Voucher Voucher, string Kind)> Vouchers,
            decimal ProfitAmount,
            int TemplateCount,
            int CarryCount,
            int RetainedCount);

        /// <summary>
        /// 按当前数据构建结转凭证（模板凭证 → 结转损益凭证 → 结转未分配利润凭证），只创建内存对象，不落库。
        /// 结转与预览共用此逻辑，保证预览与实际生成完全一致。
        /// </summary>
        private async Task<CarryBuildResult> BuildCarryVouchersAsync(
            Guid accountSetId,
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> posted,
            IReadOnlyDictionary<Guid, decimal> balanceById,
            string word,
            int nextNumber,
            DateOnly monthEnd,
            int year,
            int month,
            string operatorName,
            CarryForwardRequest request,
            CancellationToken cancellationToken)
        {
            var pnlBalances = PnlBalances(accounts, balanceById);

            var created = new List<(Voucher Voucher, string Kind)>();
            var step1NetTotal = 0m; // 结转损益凭证对「本年利润」的净影响（借 - 贷）
            // 本年利润可能按新旧准则分成 4103 / 3103 两个科目，逐个跟踪余额（净额 = 借 - 贷）。
            var profitAccounts = ProfitAccounts(accounts);
            var profitFinalById = profitAccounts.ToDictionary(a => a.Id, a => balanceById.GetValueOrDefault(a.Id));

            // 本期有损益发生额、或需要结转未分配利润时，先解析「本年利润」科目。
            Account? profitAccount = null;
            if (request.TransferToRetainedEarnings || pnlBalances.Count > 0)
            {
                profitAccount = ResolveProfitAccount(accounts)
                    ?? throw new ClosingException("未找到「本年利润」科目（编码 4103 或 3103），请先在「设置 - 科目」中建立后再结转。");
            }

            // 先执行选中的自定义结转模板：各生成一张凭证（金额按当月数据计算，全零时跳过）。
            var templateVoucherCount = 0;
            var templateDeltaById = new Dictionary<Guid, decimal>(); // 模板凭证各科目净影响（借 - 贷）
            if (request.TemplateIds is { Count: > 0 })
            {
                var (debitById, creditById) = ClosingMath.ComputePeriodSums(posted, MonthStart(year, month), monthEnd);
                foreach (var templateId in request.TemplateIds.Distinct())
                {
                    var template = await _closingTemplateRepository.GetByIdAsync(accountSetId, templateId, cancellationToken)
                        ?? throw new ClosingException("选中的结转模板不存在，请刷新后重试。");

                    // 第 1 步期末检查已单独出过该模板凭证时不再重复生成。
                    if (FindTemplateVoucher(posted, year, month, TemplateLines(template, debitById, creditById, balanceById)) is not null)
                    {
                        continue;
                    }

                    var templateVoucher = BuildTemplateVoucher(accountSetId, word, nextNumber, monthEnd, operatorName, template, debitById, creditById, balanceById);
                    if (templateVoucher is null)
                    {
                        continue;
                    }

                    nextNumber++;
                    created.Add((templateVoucher, "Template"));
                    templateVoucherCount++;
                    foreach (var entry in templateVoucher.Entries)
                    {
                        templateDeltaById[entry.AccountId] = templateDeltaById.GetValueOrDefault(entry.AccountId)
                            + entry.Debit - entry.Credit;
                        if (profitFinalById.ContainsKey(entry.AccountId))
                        {
                            profitFinalById[entry.AccountId] += entry.Debit - entry.Credit;
                        }
                    }
                }
            }

            // 结转损益的取数须包含模板凭证的影响（模板先行生成，再据此结平损益类科目）。
            var balanceWithTemplates = MergeDelta(balanceById, templateDeltaById);
            var carryPnlBalances = PnlBalances(accounts, balanceWithTemplates);
            if (carryPnlBalances.Count > 0 && profitAccount is null)
            {
                profitAccount = ResolveProfitAccount(accounts)
                    ?? throw new ClosingException("未找到「本年利润」科目（编码 4103 或 3103），请先在「设置 - 科目」中建立后再结转。");
            }

            var carryVoucherCount = 0;
            if (carryPnlBalances.Count > 0)
            {
                var profitTarget = profitAccount!; // 本期有损益发生额，上方已保证解析成功。

                // 按余额方向拆分：贷方向的收入类一组、借方向的成本费用类一组；不拆分时合并为一组。
                List<List<KeyValuePair<Account, decimal>>> groups = request.SplitIncomeCost
                    ?
                    [
                        carryPnlBalances.Where(x => x.Key.Direction == BalanceDirection.Credit).ToList(),
                        carryPnlBalances.Where(x => x.Key.Direction == BalanceDirection.Debit).ToList(),
                    ]
                    : [carryPnlBalances];

                foreach (var group in groups.Where(g => g.Count > 0))
                {
                    var voucher = Voucher.Create(accountSetId, word, nextNumber++, monthEnd, operatorName);
                    var netTotal = group.Sum(x => x.Value); // 借 - 贷

                    // 本年利润排在首行（与柠檬云一致），金额使整张凭证借贷平衡。
                    if (netTotal != 0m)
                    {
                        voucher.AddEntry(
                            CarrySummary, profitTarget.Id, netTotal > 0m ? netTotal : 0m, netTotal < 0m ? -netTotal : 0m);
                    }

                    // 按科目余额方向反向结平：收入类（贷方向）走借方、成本费用类（借方向）走贷方；
                    // 异常余额以红字（负数）留在原结平方向，与柠檬云口径一致。
                    foreach (var pair in group)
                    {
                        if (pair.Key.Direction == BalanceDirection.Credit)
                        {
                            voucher.AddEntry(CarrySummary, pair.Key.Id, -pair.Value, 0m);
                        }
                        else
                        {
                            voucher.AddEntry(CarrySummary, pair.Key.Id, 0m, pair.Value);
                        }
                    }

                    step1NetTotal += netTotal;
                    profitFinalById[profitTarget.Id] += netTotal;
                    voucher.Post();
                    created.Add((voucher, "Carry"));
                    carryVoucherCount++;
                }
            }

            // 可选：把「本年利润」余额一次性结转至「未分配利润」（净值 = 原有余额 + 本次全部生成凭证的影响）。
            var retainedVoucherCount = 0;
            if (request.TransferToRetainedEarnings)
            {
                var profitNet = profitFinalById.Values.Sum();
                if (profitNet != 0m)
                {
                    var retainedAccount = ResolveRetainedAccount(accounts)
                        ?? throw new ClosingException("未找到「未分配利润」科目（编码 4104 或 3104），请先在「设置 - 科目」中建立后再结转。");

                    var retainedBalances = profitAccounts
                        .Select(a => new KeyValuePair<Account, decimal>(a, profitFinalById.GetValueOrDefault(a.Id)))
                        .Where(x => x.Value != 0m)
                        .ToList();
                    var voucher = BuildRetainedVoucher(
                        accountSetId, retainedBalances, retainedAccount, word, nextNumber++, monthEnd, operatorName);
                    created.Add((voucher, "Retained"));
                    retainedVoucherCount++;
                }
            }

            return new CarryBuildResult(created, -step1NetTotal, templateVoucherCount, carryVoucherCount, retainedVoucherCount);
        }

        /// <summary>结转完成提示（按模板 / 结转损益 / 未分配利润拆分说明）。</summary>
        private static string CarryMessage(CarryBuildResult build)
        {
            if (build.Vouchers.Count == 0)
            {
                return "本期无损益发生额、也未执行结转模板，已标记为「已结转损益」（未生成凭证）。";
            }

            var parts = new List<string>();
            if (build.TemplateCount > 0)
            {
                parts.Add($"结转模板 {build.TemplateCount} 张");
            }

            if (build.CarryCount > 0)
            {
                parts.Add($"结转损益 {build.CarryCount} 张");
            }

            if (build.RetainedCount > 0)
            {
                parts.Add("结转未分配利润 1 张");
            }

            var sign = build.ProfitAmount >= 0m ? "净利润" : "净亏损";
            return $"已生成 {build.Vouchers.Count} 张凭证并自动过账（{string.Join("、", parts)}）；损益结转净额：{sign} {Math.Abs(build.ProfitAmount):0.00} 元。";
        }

        /// <summary>按凭证顺序映射预览行，runningBalance 记录各科目逐行过账后的余额（借 - 贷）。</summary>
        private static List<CarryPreviewVoucherDto> ToPreviewVouchers(
            IEnumerable<(Voucher Voucher, string Kind)> vouchers,
            IReadOnlyDictionary<Guid, Account> accountsById,
            IReadOnlyDictionary<Guid, decimal> balanceBefore)
        {
            var running = new Dictionary<Guid, decimal>(balanceBefore);
            return vouchers
                .Select(x => ToPreviewVoucher(x.Voucher, x.Kind, accountsById, running))
                .ToList();
        }

        /// <summary>把凭证映射为预览行（科目编码/名称与过账后余额由账套科目表和在途余额补齐）。</summary>
        private static CarryPreviewVoucherDto ToPreviewVoucher(
            Voucher voucher,
            string kind,
            IReadOnlyDictionary<Guid, Account> accountsById,
            IDictionary<Guid, decimal> runningBalance)
        {
            var lines = voucher.Entries
                .OrderBy(e => e.LineNumber)
                .Select(e =>
                {
                    accountsById.TryGetValue(e.AccountId, out var account);
                    runningBalance.TryGetValue(e.AccountId, out var priorBalance);
                    var net = priorBalance + e.Debit - e.Credit;
                    runningBalance[e.AccountId] = net;
                    return new CarryPreviewLineDto
                    {
                        Summary = e.Summary,
                        AccountId = e.AccountId,
                        AccountCode = account?.Code ?? string.Empty,
                        AccountName = account?.Name ?? string.Empty,
                        Debit = e.Debit,
                        Credit = e.Credit,
                        // 余额按科目正常方向展示：贷方向科目取「贷 - 借」。
                        Balance = account is null || account.Direction == BalanceDirection.Debit ? net : -net,
                    };
                })
                .ToList();

            return new CarryPreviewVoucherDto
            {
                Kind = kind,
                Word = voucher.Word,
                Number = voucher.Number,
                Label = $"{voucher.Word}-{voucher.Number}",
                Date = voucher.Date,
                Summary = lines.Count > 0 ? lines[0].Summary : string.Empty,
                TotalDebit = lines.Sum(l => l.Debit),
                TotalCredit = lines.Sum(l => l.Credit),
                Lines = lines,
            };
        }

        /// <summary>结转凭证的类别：未分配利润 / 结转损益。</summary>
        private static string KindOf(Voucher voucher) =>
            voucher.Entries.Count > 0 && voucher.Entries.All(e => IsRetainedSummary(e.Summary))
                ? "Retained"
                : "Carry";

        /// <summary>结转使用的凭证字：优先「转」，否则取账套第一个凭证字。</summary>
        private static string PreferredWord(AccountSet accountSet)
        {
            var words = accountSet.VoucherWordList();
            return words.Contains("转") ? "转" : words[0];
        }

        /// <summary>
        /// 撤销结转：删除目标期间及其后全部已结转期间的系统结转凭证（含「结转未分配利润」凭证），
        /// 并把账套结转进度退回目标期间的上一个月。目标期间须已完成结转且未结账
        /// （已结账的需先反结账），因此较早的月份撤销后其后月份会一并回到待结转。
        /// </summary>
        public async Task<string> CancelCarryAsync(Guid accountSetId, int year, int month, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(year, month);

            if (accountSet.CarriedPeriod == 0)
            {
                throw new ClosingException("尚未结转损益，无需撤销。");
            }

            var key = PeriodKey(year, month);
            if (key > accountSet.CarriedPeriod)
            {
                throw new ClosingException($"该期间尚未结转（当前已结转至 {accountSet.CarriedPeriodText()}）。");
            }

            if (key <= accountSet.ClosedPeriod)
            {
                throw new ClosingException($"该期间已结账（已结账至 {accountSet.ClosedPeriodText()}），请先「反结账」后再撤销结转。");
            }

            var carriedKey = accountSet.CarriedPeriod;
            var carriedText = accountSet.CarriedPeriodText()!;

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var profitAccountIds = accounts
                .Where(a => ProfitAccountPrefixes.Contains(Prefix(a.Code)))
                .Select(a => a.Id)
                .ToHashSet();

            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            // 目标期间及其后的已结转期间一并回退，让较早的月份也能重新结转。
            var toRemove = vouchers
                .Where(v =>
                {
                    var voucherKey = PeriodKey(v.Date.Year, v.Date.Month);
                    return voucherKey >= key && voucherKey <= carriedKey && IsSystemCarryVoucher(v, profitAccountIds);
                })
                .OrderBy(v => PeriodKey(v.Date.Year, v.Date.Month))
                .ThenBy(v => v.Number)
                .ToList();

            foreach (var voucher in toRemove)
            {
                _voucherRepository.Remove(voucher);
            }

            accountSet.ClearCarriedTo(year, month);
            await _accountSetRepository.SaveChangesAsync(cancellationToken);

            var scope = key == carriedKey
                ? PeriodText(year, month)
                : $"{PeriodText(year, month)} 起的已结转期间（至 {carriedText}）";
            return toRemove.Count == 0
                ? key == carriedKey
                    ? "已撤销结转状态（未找到由系统生成的结转凭证）。"
                    : $"已撤销 {scope}，未找到由系统生成的结转凭证。"
                : $"已撤销 {scope}，删除 {toRemove.Count} 张结转凭证（{string.Join("、", toRemove.Select(v => $"{v.Word}-{v.Number}"))}）。";
        }

        /// <summary>
        /// 整理断号：把本期存在断号的凭证字重新连续编号（仅改凭证号，不动金额与分录）。
        /// 凭证号在账套内按凭证字连续分配，因此整理时从本期之前的最后一个号码起，
        /// 把本期及其后各期的同类凭证一并顺延，避免与其他期间重号；已结账期间不可整理。
        /// </summary>
        public async Task<RenumberGapsResultDto> RenumberGapsAsync(
            Guid accountSetId,
            int year,
            int month,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(year, month);

            var key = PeriodKey(year, month);
            if (key <= accountSet.ClosedPeriod)
            {
                throw new ClosingException($"该期间已结账（已结账至 {accountSet.ClosedPeriodText()}），不能整理凭证号。");
            }

            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            var monthStart = MonthStart(year, month);
            var monthVouchers = vouchers
                .Where(v => v.Date.Year == year && v.Date.Month == month && v.Number > 0)
                .ToList();
            var gapWords = monthVouchers
                .GroupBy(v => v.Word)
                .Where(g => HasNumberingGap(g.Select(v => v.Number)))
                .Select(g => g.Key)
                .ToList();
            if (gapWords.Count == 0)
            {
                return new RenumberGapsResultDto
                {
                    Message = $"{PeriodText(year, month)} 凭证号码连续，无需整理。",
                };
            }

            var changed = 0;
            foreach (var word in gapWords)
            {
                var wordVouchers = vouchers.Where(v => v.Word == word).ToList();
                var before = wordVouchers.Where(v => v.Date < monthStart).ToList();
                var targets = wordVouchers
                    .Where(v => v.Date >= monthStart)
                    .OrderBy(v => v.Date)
                    .ThenBy(v => v.Number)
                    .ToList();

                var next = before.Count == 0 ? 1 : before.Max(v => v.Number) + 1;
                foreach (var item in targets)
                {
                    if (item.Number != next)
                    {
                        var voucher = await _voucherRepository.GetByIdAsync(accountSetId, item.Id, cancellationToken);
                        if (voucher is null)
                        {
                            next++;
                            continue;
                        }

                        voucher.SetNumber(next);
                        changed++;
                    }

                    next++;
                }
            }

            if (changed > 0)
            {
                await _voucherRepository.SaveChangesAsync(cancellationToken);
            }

            return new RenumberGapsResultDto
            {
                Message = changed == 0
                    ? $"{PeriodText(year, month)} 凭证号码连续，无需整理。"
                    : $"已整理 {PeriodText(year, month)} 的断号（{string.Join("、", gapWords)}），共调整 {changed} 张凭证的号码。",
                Changed = changed,
            };
        }

        /// <summary>结账：把下一个待结期间标记为已结账（须先完成结转，且期间内凭证全部已过账）。</summary>
        public async Task<ClosingStatusDto> CloseAsync(Guid accountSetId, int year, int month, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(year, month);

            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            var pending = vouchers.Count(v => v.Date.Year == year && v.Date.Month == month && v.Status != VoucherStatus.Posted);
            if (pending > 0)
            {
                throw new ClosingException($"{PeriodText(year, month)} 期间尚有 {pending} 张凭证未过账，请先保存入账后再结账。");
            }

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            await AutoAdvanceCarriedAsync(accountSet, accounts, vouchers, cancellationToken);

            try
            {
                // 域层保证：只能按顺序结账，且必须先完成该期间的结转损益。
                accountSet.ClosePeriod(year, month);
            }
            catch (InvalidOperationException ex)
            {
                throw new ClosingException(ex.Message);
            }

            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            return await GetStatusAsync(accountSetId, cancellationToken);
        }

        /// <summary>
        /// 反结账：从指定已结期间起撤销结账（该期间及其后的已结期间全部退回，可指定任意已结期间）；
        /// 不传期间时退回最近一次结账。结转进度保持不变。
        /// </summary>
        public async Task<ClosingStatusDto> ReopenAsync(
            Guid accountSetId,
            int? year = null,
            int? month = null,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            if (accountSet.ClosedPeriod == 0)
            {
                throw new ClosingException("尚未结账，无需反结账。");
            }

            var targetYear = accountSet.ClosedPeriod / 100;
            var targetMonth = accountSet.ClosedPeriod % 100;
            if (year is { } y && month is { } m)
            {
                targetYear = y;
                targetMonth = m;
            }

            ValidatePeriod(targetYear, targetMonth);

            try
            {
                accountSet.ReopenFrom(targetYear, targetMonth);
            }
            catch (InvalidOperationException ex)
            {
                throw new ClosingException(ex.Message);
            }

            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            return await GetStatusAsync(accountSetId, cancellationToken);
        }

        /// <summary>
        /// 批量结账：从当前待结期间开始连续结账，直到「尚未结转损益」的期间为止；
        /// 中途某期间存在未过账凭证时停在该期间（已结部分保留并说明原因；首个期间即受阻时报错）。
        /// </summary>
        public async Task<CloseBatchResultDto> CloseBatchAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            await AutoAdvanceCarriedAsync(accountSet, accounts, vouchers, cancellationToken);

            var closedPeriods = new List<string>();
            string? blockedReason = null;

            while (closedPeriods.Count < 240)
            {
                var (year, month) = accountSet.NextPeriodToClose();
                var key = year * 100 + month;
                if (key > accountSet.CarriedPeriod)
                {
                    break; // 该期间尚未结转损益，其后都不可结账。
                }

                var pending = vouchers.Count(v => v.Date.Year == year && v.Date.Month == month && v.Status != VoucherStatus.Posted);
                if (pending > 0)
                {
                    blockedReason = $"{PeriodText(year, month)} 存在 {pending} 张未过账凭证";
                    break;
                }

                try
                {
                    accountSet.ClosePeriod(year, month);
                }
                catch (InvalidOperationException ex)
                {
                    blockedReason = ex.Message;
                    break;
                }

                closedPeriods.Add(PeriodText(year, month));

                // 结账后下一个空期间可以自动结转，继续往后结账。
                await AutoAdvanceCarriedAsync(accountSet, accounts, vouchers, cancellationToken);
            }

            if (closedPeriods.Count == 0)
            {
                throw new ClosingException(blockedReason is null
                    ? "没有可批量结账的期间（请先完成结转损益）。"
                    : $"无法批量结账：{blockedReason}。");
            }

            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            var status = await GetStatusAsync(accountSetId, cancellationToken);
            var message = blockedReason is null
                ? $"已批量结账 {closedPeriods.Count} 期：{string.Join("、", closedPeriods)}。"
                : $"已结账 {closedPeriods.Count} 期（{string.Join("、", closedPeriods)}）；因{blockedReason}已停止。";

            return new CloseBatchResultDto
            {
                Message = message,
                ClosedPeriods = closedPeriods,
                Status = status,
            };
        }

        /// <summary>
        /// 批量标记结转：从下一个待结转期间起，把损益余额已为 0 的连续期间依次标记为已结转
        /// （用于导入了外部结转凭证的历史账套），直到最新有凭证的期间；不生成任何凭证。
        /// 遇到仍有损益余额或未过账凭证的期间即停止并说明原因。
        /// </summary>
        public async Task<CarryBatchResultDto> CarryBatchAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await LoadLiveVouchers(accountSetId, cancellationToken);
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();
            var latestKey = vouchers.Count == 0 ? 0 : vouchers.Max(v => PeriodKey(v.Date.Year, v.Date.Month));

            var markedPeriods = new List<string>();
            string? blockedReason = null;

            while (markedPeriods.Count < 240)
            {
                var (year, month) = accountSet.NextPeriodToCarry();
                var key = PeriodKey(year, month);
                if (key > latestKey)
                {
                    break; // 已覆盖到最新有凭证的期间。
                }

                var pending = vouchers.Count(v => v.Date.Year == year && v.Date.Month == month && v.Status != VoucherStatus.Posted);
                if (pending > 0)
                {
                    blockedReason = $"{PeriodText(year, month)} 存在 {pending} 张未过账凭证";
                    break;
                }

                var balanceById = ComputeBalanceById(accounts, posted, MonthEnd(year, month));
                if (PnlBalances(accounts, balanceById).Count > 0)
                {
                    blockedReason = $"{PeriodText(year, month)} 有损益余额未结转";
                    break;
                }

                try
                {
                    accountSet.MarkCarried(year, month);
                }
                catch (InvalidOperationException ex)
                {
                    blockedReason = ex.Message;
                    break;
                }

                markedPeriods.Add(PeriodText(year, month));
            }

            if (markedPeriods.Count == 0)
            {
                throw new ClosingException(blockedReason is null
                    ? "没有可批量标记的期间（已结转至最新有凭证的期间）。"
                    : $"无法批量标记：{blockedReason}。");
            }

            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            var status = await GetStatusAsync(accountSetId, cancellationToken);
            var message = blockedReason is null
                ? $"已标记 {markedPeriods.Count} 个期间为已结转：{string.Join("、", markedPeriods)}。"
                : $"已标记 {markedPeriods.Count} 个期间（{string.Join("、", markedPeriods)}）；因{blockedReason}已停止。";

            return new CarryBatchResultDto
            {
                Message = message,
                MarkedPeriods = markedPeriods,
                Status = status,
            };
        }

        /* ------------------------------------------------------------------ */
        /*  内部工具                                                           */
        /* ------------------------------------------------------------------ */

        private async Task<AccountSet> LoadAccountSet(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = accountSetId == Guid.Empty
                ? null
                : await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            return accountSet ?? throw new ClosingException("指定的账套不存在。");
        }

        /// <summary>
        /// 读取参与期末计算的凭证（剔除回收站中的软删除凭证）。
        /// 回收站里的草稿仍是「未过账」状态，若不排除，删掉一张草稿就会让该期间永久无法结转/结账。
        /// </summary>
        private async Task<IReadOnlyList<Voucher>> LoadLiveVouchers(Guid accountSetId, CancellationToken cancellationToken) =>
            (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken)).Where(v => !v.IsDeleted).ToList();

        /// <summary>
        /// 自动推进结转水位：从下一个待结转期间起，把损益余额已为 0 且无未过账凭证的连续期间
        /// 直接标记为已结转——这些期间本来就没有要结转的损益，不应再要求用户逐月点「标记已结转」。
        /// 最新凭证所在月份是当前做账的前沿：只有该月已有结转凭证（例如导入的历史结转）时才自动标记，
        /// 否则保留人工确认，避免把还没做期末计提的当月提前关掉。
        /// </summary>
        private async Task AutoAdvanceCarriedAsync(
            AccountSet accountSet,
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> vouchers,
            CancellationToken cancellationToken)
        {
            if (vouchers.Count == 0)
            {
                return;
            }

            var latestKey = vouchers.Max(v => PeriodKey(v.Date.Year, v.Date.Month));
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();
            var profitAccountIds = accounts
                .Where(a => ProfitAccountPrefixes.Contains(Prefix(a.Code)))
                .Select(a => a.Id)
                .ToHashSet();
            var advanced = false;

            for (var guard = 0; guard < 240; guard++)
            {
                var (year, month) = accountSet.NextPeriodToCarry();
                var key = PeriodKey(year, month);
                if (key > latestKey)
                {
                    break;
                }

                var monthVouchers = vouchers
                    .Where(v => v.Date.Year == year && v.Date.Month == month)
                    .ToList();
                if (monthVouchers.Any(v => v.Status != VoucherStatus.Posted))
                {
                    break;
                }

                var isFrontier = key == latestKey;
                var hasCarryVoucher = monthVouchers.Any(v => IsSystemCarryVoucher(v, profitAccountIds));
                if (isFrontier && !hasCarryVoucher)
                {
                    break;
                }

                var balanceById = ComputeBalanceById(accounts, posted, MonthEnd(year, month));
                if (PnlBalances(accounts, balanceById).Count > 0)
                {
                    break;
                }

                accountSet.MarkCarried(year, month);
                advanced = true;
            }

            if (advanced)
            {
                await _accountSetRepository.SaveChangesAsync(cancellationToken);
            }
        }

        /// <summary>计算各科目截至指定日期（含当日）的累计净发生额（借 - 贷，仅已过账凭证 + 启用期初）。</summary>
        private static Dictionary<Guid, decimal> ComputeBalanceById(
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> postedVouchers,
            DateOnly endDate)
            => ClosingMath.ComputeBalanceById(accounts, postedVouchers, endDate);

        /// <summary>筛选出余额非零的损益类末级科目（净额 = 借 - 贷）。</summary>
        private static List<KeyValuePair<Account, decimal>> PnlBalances(
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> balanceById)
        {
            return accounts
                .Where(a => a.Category == AccountCategory.ProfitAndLoss && a.IsLeaf)
                .Select(a => new KeyValuePair<Account, decimal>(a, balanceById.GetValueOrDefault(a.Id)))
                .Where(x => x.Value != 0m)
                .ToList();
        }

        /// <summary>
        /// 解析全部「本年利润」末级科目：新旧准则可能同时建了 4103 与 3103，年末需一并结转到未分配利润；
        /// 前缀找不到时退化为按名称匹配的单个科目。
        /// </summary>
        private static List<Account> ProfitAccounts(IReadOnlyList<Account> accounts)
        {
            var byPrefix = accounts
                .Where(a => a.IsLeaf && ProfitAccountPrefixes.Contains(Prefix(a.Code)))
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .ToList();
            if (byPrefix.Count > 0)
            {
                return byPrefix;
            }

            return ResolveProfitAccount(accounts) is { } fallback ? [fallback] : [];
        }

        /// <summary>筛选出余额非零的「本年利润」末级科目（净额 = 借 - 贷）。</summary>
        private static List<KeyValuePair<Account, decimal>> ProfitBalances(
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> balanceById)
            => ProfitAccounts(accounts)
                .Select(a => new KeyValuePair<Account, decimal>(a, balanceById.GetValueOrDefault(a.Id)))
                .Where(x => x.Value != 0m)
                .ToList();

        /// <summary>
        /// 构建「本年利润 → 未分配利润」凭证（未落库）：逐个结平 4103/3103，净额转入未分配利润。
        /// 预览与生成共用，保证界面上看到的就是最终生成的凭证。
        /// </summary>
        private static Voucher BuildRetainedVoucher(
            Guid accountSetId,
            IReadOnlyList<KeyValuePair<Account, decimal>> profitBalances,
            Account retainedAccount,
            string word,
            int number,
            DateOnly monthEnd,
            string operatorName)
        {
            var voucher = Voucher.Create(accountSetId, word, number, monthEnd, operatorName);

            foreach (var pair in profitBalances)
            {
                if (pair.Value < 0m)
                {
                    voucher.AddEntry(RetainedSummary, pair.Key.Id, -pair.Value, 0m);
                }
                else
                {
                    voucher.AddEntry(RetainedSummary, pair.Key.Id, 0m, pair.Value);
                }
            }

            var profitNet = profitBalances.Sum(x => x.Value);
            if (profitNet < 0m)
            {
                // 盈利（贷方余额）：贷 未分配利润
                voucher.AddEntry(RetainedSummary, retainedAccount.Id, 0m, -profitNet);
            }
            else
            {
                // 亏损（借方余额）：借 未分配利润
                voucher.AddEntry(RetainedSummary, retainedAccount.Id, profitNet, 0m);
            }

            voucher.Post();
            return voucher;
        }

        /// <summary>
        /// 解析「本年利润」科目：优先编码前缀 4103/3103 的末级启用科目，编码等长时取最短；
        /// 前缀找不到时退化为按名称包含「本年利润」匹配。
        /// </summary>
        private static Account? ResolveProfitAccount(IReadOnlyList<Account> accounts)
        {
            var byPrefix = accounts
                .Where(a => a.IsLeaf && a.IsEnabled && ProfitAccountPrefixes.Contains(Prefix(a.Code)))
                .OrderBy(a => a.Code.Length)
                .ThenBy(a => a.Code, StringComparer.Ordinal)
                .FirstOrDefault();
            return byPrefix ?? accounts
                .Where(a => a.IsLeaf && a.IsEnabled && a.Name.Contains("本年利润"))
                .OrderBy(a => a.Code.Length)
                .ThenBy(a => a.Code, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// 解析「未分配利润」科目：优先前缀 4104/3104 且名称含「未分配利润」的末级启用科目，
        /// 其次任意 4104/3104 末级启用科目，最后退化为按名称包含「未分配利润」匹配。
        /// </summary>
        private static Account? ResolveRetainedAccount(IReadOnlyList<Account> accounts)
        {
            var candidates = accounts
                .Where(a => a.IsLeaf && a.IsEnabled && RetainedAccountPrefixes.Contains(Prefix(a.Code)))
                .ToList();
            return candidates
                       .Where(a => a.Name.Contains("未分配利润"))
                       .OrderBy(a => a.Code, StringComparer.Ordinal)
                       .FirstOrDefault()
                   ?? candidates.OrderBy(a => a.Code.Length).ThenBy(a => a.Code, StringComparer.Ordinal).FirstOrDefault()
                   ?? accounts
                       .Where(a => a.IsLeaf && a.IsEnabled && a.Name.Contains("未分配利润"))
                       .OrderBy(a => a.Code.Length)
                       .ThenBy(a => a.Code, StringComparer.Ordinal)
                       .FirstOrDefault();
        }

        /// <summary>识别系统或外部导入的结转凭证：全部分录摘要为结转标记，且至少一条分录落在「本年利润」科目上。</summary>
        private static bool IsSystemCarryVoucher(Voucher voucher, IReadOnlySet<Guid> profitAccountIds)
        {
            return voucher.Entries.Count > 0
                   && voucher.Entries.All(e => IsCarrySummary(e.Summary) || IsRetainedSummary(e.Summary))
                   && voucher.Entries.Any(e => profitAccountIds.Contains(e.AccountId));
        }

        /// <summary>
        /// 识别「结转未分配利润」凭证：摘要为结转标记，且分录同时涉及「本年利润」与「未分配利润」。
        /// 按科目结构识别，兼容外部系统把摘要写成「年末结转损益」的导入数据。
        /// </summary>
        private static bool IsRetainedVoucher(
            Voucher voucher,
            IReadOnlySet<Guid> profitAccountIds,
            IReadOnlySet<Guid> retainedAccountIds)
        {
            return voucher.Entries.Count > 0
                   && voucher.Entries.All(e => IsCarrySummary(e.Summary) || IsRetainedSummary(e.Summary))
                   && voucher.Entries.Any(e => profitAccountIds.Contains(e.AccountId))
                   && voucher.Entries.Any(e => retainedAccountIds.Contains(e.AccountId));
        }

        /// <summary>
        /// 结转损益摘要的宽松判定：去空白后以「结转损益」结尾。
        /// 兼容外部系统导入的「11月  结转损益」写法，系统自身生成的是「期末结转损益」。
        /// </summary>
        private static bool IsCarrySummary(string summary) =>
            StripWhitespace(summary).EndsWith("结转损益", StringComparison.Ordinal);

        /// <summary>「结转未分配利润」摘要的宽松判定（同样去空白后按后缀匹配）。</summary>
        private static bool IsRetainedSummary(string summary) =>
            StripWhitespace(summary).EndsWith(RetainedSummary, StringComparison.Ordinal);

        /// <summary>去掉摘要中的全部空白字符（含全角空格），用于兼容外部系统的多空格写法。</summary>
        private static string StripWhitespace(string summary) =>
            string.Concat(summary.Where(c => !char.IsWhiteSpace(c)));

        /// <summary>把增量字典并入基字典的副本（余额 + 已生成凭证的净影响），用于让后续取数包含同批次凭证。</summary>
        private static Dictionary<Guid, decimal> MergeDelta(
            IReadOnlyDictionary<Guid, decimal> baseline,
            IReadOnlyDictionary<Guid, decimal> delta)
        {
            if (delta.Count == 0)
            {
                return new Dictionary<Guid, decimal>(baseline);
            }

            var merged = new Dictionary<Guid, decimal>(baseline);
            foreach (var pair in delta)
            {
                merged[pair.Key] = merged.GetValueOrDefault(pair.Key) + pair.Value;
            }

            return merged;
        }

        /// <summary>把选中的结转模板按当月数据实例化为一张已过账凭证；全部金额为零时返回 null（不生成空凭证）。</summary>
        private static Voucher? BuildTemplateVoucher(
            Guid accountSetId,
            string word,
            int number,
            DateOnly monthEnd,
            string operatorName,
            ClosingTemplate template,
            IReadOnlyDictionary<Guid, decimal> debitById,
            IReadOnlyDictionary<Guid, decimal> creditById,
            IReadOnlyDictionary<Guid, decimal> balanceById)
        {
            var entries = TemplateLines(template, debitById, creditById, balanceById);

            if (entries.Count == 0)
            {
                return null;
            }

            var totalDebit = entries.Where(x => x.Line.Direction == BalanceDirection.Debit).Sum(x => x.Amount);
            var totalCredit = entries.Where(x => x.Line.Direction == BalanceDirection.Credit).Sum(x => x.Amount);
            if (totalDebit != totalCredit)
            {
                throw new ClosingException($"结转模板「{template.Name}」试算不平衡（借方 {totalDebit:0.00}，贷方 {totalCredit:0.00}），请检查模板设置。");
            }

            var voucher = Voucher.Create(accountSetId, word, number, monthEnd, operatorName);
            foreach (var (line, amount) in entries)
            {
                if (line.Direction == BalanceDirection.Debit)
                {
                    voucher.AddEntry(line.Summary, line.AccountId, amount, 0m);
                }
                else
                {
                    voucher.AddEntry(line.Summary, line.AccountId, 0m, amount);
                }
            }

            voucher.Post();
            return voucher;
        }

        private static DateOnly MonthStart(int year, int month) => new(year, month, 1);

        private static void ValidatePeriod(int year, int month)
        {
            if (year < 1900 || year > 9999)
            {
                throw new ClosingException("会计期间的年份不合法。");
            }

            if (month < 1 || month > 12)
            {
                throw new ClosingException("会计期间的月份必须在 1 到 12 之间。");
            }
        }

        private static int PeriodKey(int year, int month) => year * 100 + month;

        private static string PeriodText(int year, int month) => $"{year:D4}-{month:D2}";

        private static DateOnly MonthEnd(int year, int month) =>
            new(year, month, DateTime.DaysInMonth(year, month));

        private static (int Year, int Month) NextMonth(int year, int month) =>
            month == 12 ? (year + 1, 1) : (year, month + 1);

        private static string Prefix(string code) => code.Length <= 4 ? code : code[..4];
    }
}
