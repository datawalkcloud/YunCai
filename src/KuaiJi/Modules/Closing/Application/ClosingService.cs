using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Closing.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Closing.Application
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

        public ClosingService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            IClosingTemplateRepository closingTemplateRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _closingTemplateRepository = closingTemplateRepository;
        }

        /// <summary>获取账套的期末结转总体状态（含逐月状态卡片）。</summary>
        public async Task<ClosingStatusDto> GetStatusAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

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

        /// <summary>期末检查：给出指定期间的检查项清单与「可否结转 / 可否结账」结论。</summary>
        public async Task<ClosingCheckResultDto> CheckAsync(Guid accountSetId, int year, int month, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(year, month);

            var key = PeriodKey(year, month);
            var monthEnd = MonthEnd(year, month);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();

            var items = new List<ClosingCheckItemDto>();

            // 1) 截至本期末的未过账凭证：结转按已过账取数、结账会冻结期间，任一未过账都会让结果不完整。
            var unposted = vouchers
                .Where(v => v.Status != VoucherStatus.Posted && v.Date <= monthEnd)
                .OrderBy(v => v.Date)
                .ToList();
            items.Add(unposted.Count > 0
                ? new ClosingCheckItemDto
                {
                    Key = "unposted",
                    Title = "未过账凭证",
                    Status = "error",
                    Message = $"截至 {PeriodText(year, month)} 存在 {unposted.Count} 张未过账凭证（最早 {unposted[0].Date:yyyy-MM-dd}），请先完成审核与过账（可在「凭证管理」中按期间筛选处理）。",
                }
                : new ClosingCheckItemDto
                {
                    Key = "unposted",
                    Title = "未过账凭证",
                    Status = "ok",
                    Message = "截至本期末的凭证均已过账。",
                });

            // 2) 期初余额试算平衡：期初如有差额，结账前应先调整（设置 - 期初）。
            var openingNetTotal = accounts
                .Where(a => a.IsLeaf && a.OpeningBalance != 0m)
                .Sum(a => a.Direction == BalanceDirection.Debit ? a.OpeningBalance : -a.OpeningBalance);
            var openingBalanced = openingNetTotal == 0m;
            items.Add(openingBalanced
                ? new ClosingCheckItemDto
                {
                    Key = "opening",
                    Title = "期初余额平衡",
                    Status = "ok",
                    Message = "期初余额借贷平衡。",
                }
                : new ClosingCheckItemDto
                {
                    Key = "opening",
                    Title = "期初余额平衡",
                    Status = "warn",
                    Message = $"期初余额借贷不平衡（差额 {openingNetTotal:0.00}），结账前请在「设置 - 期初」中调整。",
                });

            // 3) 损益发生额与「本年利润」科目：有发生额就必须能找到结转目标科目。
            var balanceById = ComputeBalanceById(accounts, posted, monthEnd);
            var pnlBalances = PnlBalances(accounts, balanceById);
            var profitAccount = ResolveProfitAccount(accounts);
            if (pnlBalances.Count == 0)
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "profit-account",
                    Title = "本年利润科目",
                    Status = "ok",
                    Message = "本期无损益发生额，结转不生成凭证（仅推进状态）。",
                });
            }
            else if (profitAccount is null)
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "profit-account",
                    Title = "本年利润科目",
                    Status = "error",
                    Message = "未找到「本年利润」科目（编码 4103 或 3103），请先在「设置 - 科目」中建立后再结转。",
                });
            }
            else
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "profit-account",
                    Title = "本年利润科目",
                    Status = "ok",
                    Message = $"结转目标科目：{profitAccount.Code} {profitAccount.Name}。",
                });
            }

            // 4) 结转顺序：必须从下一个待结转期间开始，按月顺序推进。
            var (nextCarryYear, nextCarryMonth) = accountSet.NextPeriodToCarry();
            if (key <= accountSet.ClosedPeriod)
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "period",
                    Title = "期间状态",
                    Status = "error",
                    Message = $"该期间已结账（已结账至 {accountSet.ClosedPeriodText()}）。",
                });
            }
            else if (key <= accountSet.CarriedPeriod)
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "period",
                    Title = "期间状态",
                    Status = "ok",
                    Message = "本期已结转损益。",
                });
            }
            else if (key == PeriodKey(nextCarryYear, nextCarryMonth))
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "period",
                    Title = "期间状态",
                    Status = "ok",
                    Message = "处于待结转状态，可执行结转损益。",
                });
            }
            else
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "period",
                    Title = "期间状态",
                    Status = "error",
                    Message = $"请按顺序结转：下一个待结转期间为 {PeriodText(nextCarryYear, nextCarryMonth)}。",
                });
            }

            // 5) 结账前置：先结转后结账，且须从上一個待结期间开始。
            var carriedForPeriod = key <= accountSet.CarriedPeriod;
            var (nextCloseYear, nextCloseMonth) = accountSet.NextPeriodToClose();
            if (key != PeriodKey(nextCloseYear, nextCloseMonth))
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "close-prereq",
                    Title = "结账条件",
                    Status = "warn",
                    Message = $"非当前待结期间（下一个待结期间为 {PeriodText(nextCloseYear, nextCloseMonth)}）。",
                });
            }
            else if (!carriedForPeriod)
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "close-prereq",
                    Title = "结账条件",
                    Status = "warn",
                    Message = "本期尚未结转损益，请先完成结转后再结账。",
                });
            }
            else
            {
                items.Add(new ClosingCheckItemDto
                {
                    Key = "close-prereq",
                    Title = "结账条件",
                    Status = "ok",
                    Message = "已满足「先结转、后结账」条件。",
                });
            }

            var blocking = items
                .Where(i => i.Status == "error" && i.Key is "unposted" or "profit-account" or "period")
                .Any();

            return new ClosingCheckResultDto
            {
                Year = year,
                Month = month,
                PeriodText = PeriodText(year, month),
                CanCarry = key == PeriodKey(nextCarryYear, nextCarryMonth) && !blocking,
                CanClose = key == PeriodKey(nextCloseYear, nextCloseMonth)
                           && carriedForPeriod
                           && !items.Any(i => i.Status == "error" && i.Key == "unposted")
                           && openingBalanced,
                Items = items,
            };
        }

        /// <summary>
        /// 结转损益：生成结转凭证（损益类科目 → 本年利润）并直接过账，随后把账套标记为「已结转至本期」。
        /// 支持按收入/成本拆分为两张凭证；可选择同时把「本年利润」余额结转至「未分配利润」。
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

            if (key <= accountSet.CarriedPeriod)
            {
                throw new ClosingException($"该期间已结转损益（已结转至 {accountSet.CarriedPeriodText()}），如需重做请先「撤销结转」。");
            }

            var (nextYear, nextMonth) = accountSet.NextPeriodToCarry();
            if (year != nextYear || month != nextMonth)
            {
                throw new ClosingException($"只能按顺序结转，下一个待结转期间为 {PeriodText(nextYear, nextMonth)}。");
            }

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            var posted = vouchers.Where(v => v.Status == VoucherStatus.Posted).ToList();

            var unpostedCount = vouchers.Count(v => v.Status != VoucherStatus.Posted && v.Date <= monthEnd);
            if (unpostedCount > 0)
            {
                throw new ClosingException($"截至 {PeriodText(year, month)} 存在 {unpostedCount} 张未过账凭证，请先完成审核与过账。");
            }

            var balanceById = ComputeBalanceById(accounts, posted, monthEnd);
            var pnlBalances = PnlBalances(accounts, balanceById);

            // 凭证字优先取「转」（转账凭证），否则取账套第一个凭证字。
            var words = accountSet.VoucherWordList();
            var word = words.Contains("转") ? "转" : words[0];
            var nextNumber = await _voucherRepository.GetMaxNumberAsync(accountSetId, word, cancellationToken) + 1;

            var created = new List<(Voucher Voucher, string Kind)>();
            var step1NetTotal = 0m; // 结转损益凭证对「本年利润」的净影响（借 - 贷）
            var allProfitDelta = 0m; // 所有已生成凭证对「本年利润」的净影响（借 - 贷），供结转未分配利润使用

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
                    }

                    if (profitAccount is not null)
                    {
                        allProfitDelta += VoucherProfitDelta(templateVoucher, profitAccount.Id);
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

                    // 逐科目结平：借方净额的科目按贷方冲回，贷方净额的科目按借方冲回（净额 = 借 - 贷）。
                    foreach (var pair in group)
                    {
                        if (pair.Value > 0m)
                        {
                            voucher.AddEntry(CarrySummary, pair.Key.Id, 0m, pair.Value);
                        }
                        else
                        {
                            voucher.AddEntry(CarrySummary, pair.Key.Id, -pair.Value, 0m);
                        }
                    }

                    // 本年利润对侧：使整张凭证借贷平衡（组内净额合计为 0 时无需再补分录）。
                    var netTotal = group.Sum(x => x.Value);
                    if (netTotal > 0m)
                    {
                        voucher.AddEntry(CarrySummary, profitTarget.Id, netTotal, 0m);
                    }
                    else if (netTotal < 0m)
                    {
                        voucher.AddEntry(CarrySummary, profitTarget.Id, 0m, -netTotal);
                    }

                    step1NetTotal += netTotal;
                    allProfitDelta += netTotal;
                    voucher.Review(operatorName);
                    voucher.Post();
                    created.Add((voucher, "Carry"));
                    carryVoucherCount++;
                }
            }

            // 可选：把「本年利润」余额一次性结转至「未分配利润」（净值 = 原有余额 + 本次全部生成凭证的影响）。
            var retainedVoucherCount = 0;
            if (request.TransferToRetainedEarnings)
            {
                var retainedProfitTarget = profitAccount!; // 勾选结转未分配利润时，上方已保证解析成功。
                var profitNet = balanceById.GetValueOrDefault(retainedProfitTarget.Id) + allProfitDelta;
                if (profitNet != 0m)
                {
                    var retainedAccount = ResolveRetainedAccount(accounts)
                        ?? throw new ClosingException("未找到「未分配利润」科目（编码 4104 或 3104），请先在「设置 - 科目」中建立后再结转。");

                    var voucher = Voucher.Create(accountSetId, word, nextNumber++, monthEnd, operatorName);
                    if (profitNet < 0m)
                    {
                        // 盈利（贷方余额）：借 本年利润 / 贷 未分配利润
                        voucher.AddEntry(RetainedSummary, retainedProfitTarget.Id, -profitNet, 0m);
                        voucher.AddEntry(RetainedSummary, retainedAccount.Id, 0m, -profitNet);
                    }
                    else
                    {
                        // 亏损（借方余额）：借 未分配利润 / 贷 本年利润
                        voucher.AddEntry(RetainedSummary, retainedProfitTarget.Id, 0m, profitNet);
                        voucher.AddEntry(RetainedSummary, retainedAccount.Id, profitNet, 0m);
                    }

                    voucher.Review(operatorName);
                    voucher.Post();
                    created.Add((voucher, "Retained"));
                    retainedVoucherCount++;
                }
            }

            accountSet.MarkCarried(year, month);

            foreach (var (voucher, _) in created)
            {
                await _voucherRepository.AddAsync(voucher, cancellationToken);
            }

            await _accountSetRepository.SaveChangesAsync(cancellationToken);

            var profitAmount = -step1NetTotal;
            string message;
            if (created.Count == 0)
            {
                message = "本期无损益发生额、也未执行结转模板，已标记为「已结转损益」（未生成凭证）。";
            }
            else
            {
                var parts = new List<string>();
                if (templateVoucherCount > 0)
                {
                    parts.Add($"结转模板 {templateVoucherCount} 张");
                }

                if (carryVoucherCount > 0)
                {
                    parts.Add($"结转损益 {carryVoucherCount} 张");
                }

                if (retainedVoucherCount > 0)
                {
                    parts.Add("结转未分配利润 1 张");
                }

                message = $"已生成 {created.Count} 张凭证并自动过账（{string.Join("、", parts)}）；损益结转净额：{(profitAmount >= 0m ? "净利润" : "净亏损")} {Math.Abs(profitAmount):0.00} 元。";
            }

            return new CarryForwardResultDto
            {
                PeriodText = PeriodText(year, month),
                ProfitAmount = profitAmount,
                Message = message,
                Vouchers = created
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

        /// <summary>
        /// 撤销结转：删除最近一次结转生成的凭证（含「结转未分配利润」凭证），并把账套结转进度退回一个月。
        /// 仅当目标期间是最近一次已结转期间、且尚未结账时可执行（已结账的需先反结账）。
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
            if (key != accountSet.CarriedPeriod)
            {
                throw new ClosingException($"只能撤销最近一次结转（当前已结转至 {accountSet.CarriedPeriodText()}）。");
            }

            if (key <= accountSet.ClosedPeriod)
            {
                throw new ClosingException($"该期间已结账（已结账至 {accountSet.ClosedPeriodText()}），请先「反结账」后再撤销结转。");
            }

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var profitAccountIds = accounts
                .Where(a => ProfitAccountPrefixes.Contains(Prefix(a.Code)))
                .Select(a => a.Id)
                .ToHashSet();

            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            var toRemove = vouchers
                .Where(v => v.Date.Year == year && v.Date.Month == month && IsSystemCarryVoucher(v, profitAccountIds))
                .ToList();

            foreach (var voucher in toRemove)
            {
                _voucherRepository.Remove(voucher);
            }

            accountSet.ClearCarried();
            await _accountSetRepository.SaveChangesAsync(cancellationToken);

            return toRemove.Count == 0
                ? "已撤销结转状态（未找到由系统生成的结转凭证）。"
                : $"已撤销结转，删除 {toRemove.Count} 张结转凭证（{string.Join("、", toRemove.Select(v => $"{v.Word}-{v.Number}"))}）。";
        }

        /// <summary>结账：把下一个待结期间标记为已结账（须先完成结转，且期间内凭证全部已过账）。</summary>
        public async Task<ClosingStatusDto> CloseAsync(Guid accountSetId, int year, int month, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            ValidatePeriod(year, month);

            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            var pending = vouchers.Count(v => v.Date.Year == year && v.Date.Month == month && v.Status != VoucherStatus.Posted);
            if (pending > 0)
            {
                throw new ClosingException($"{PeriodText(year, month)} 期间尚有 {pending} 张凭证未过账，请先完成审核与过账。");
            }

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

        /// <summary>反结账：撤销最近一次结账（已结期间退回一个月）；结转进度保持不变。</summary>
        public async Task<ClosingStatusDto> ReopenAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            try
            {
                accountSet.ReopenPeriod();
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
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

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

        /// <summary>识别系统生成的结转凭证：全部分录摘要为结转标记，且至少一条分录落在「本年利润」科目上。</summary>
        private static bool IsSystemCarryVoucher(Voucher voucher, IReadOnlySet<Guid> profitAccountIds)
        {
            return voucher.Entries.Count > 0
                   && voucher.Entries.All(e => e.Summary == CarrySummary || e.Summary == RetainedSummary)
                   && voucher.Entries.Any(e => profitAccountIds.Contains(e.AccountId));
        }

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
            var entries = template.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => (Line: l, Amount: ClosingMath.ComputeLineAmount(l, debitById, creditById, balanceById)))
                .Where(x => x.Amount != 0m)
                .ToList();

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

            voucher.Review(operatorName);
            voucher.Post();
            return voucher;
        }

        /// <summary>统计一张凭证对指定科目的净影响（该科目上的 借方 - 贷方 合计）。</summary>
        private static decimal VoucherProfitDelta(Voucher voucher, Guid accountId)
        {
            return voucher.Entries
                .Where(e => e.AccountId == accountId)
                .Sum(e => e.Debit - e.Credit);
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
