using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 部门利润表与项目利润表应用服务：从已过账凭证的损益类分录出发，
    /// 按分录登记的辅助核算维度（部门/项目）拆解收入、成本费用，并计算各行利润。
    /// 含「本年利润」科目的结转凭证剔除，与利润表口径一致。
    /// 部门与项目共用同一套行归集逻辑（<see cref="BuildAssistRows"/>），仅维度类型与列头不同。
    /// </summary>
    public sealed class AssistIncomeService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IAssistItemRepository _assistItemRepository;

        public AssistIncomeService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            IAssistItemRepository assistItemRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _assistItemRepository = assistItemRepository;
        }

        /// <summary>部门利润表。year/month 省略时取最近有凭证的月份。</summary>
        public async Task<DepartmentIncomeDto> GetByDepartmentAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var data = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(data.AccountSet, data.PostedVouchers, year, month);
            var (currentByDept, ytdByDept, departmentIds) = AccumulateByAssist(
                data, AssistType.Department, resolvedYear, resolvedMonth);

            var departments = departmentIds
                .Select(id => data.AssistItemsById.GetValueOrDefault(id))
                .Where(a => a is not null)
                .Select(a => new IncomeDepartmentDto { Id = a!.Id, Name = a.Name })
                .ToList();

            var rows = BuildAssistRows(departmentIds, currentByDept, ytdByDept)
                .Select(r => new IncomeDepartmentRowDto
                {
                    Item = r.Item,
                    Line = r.Line,
                    Amounts = r.Amounts,
                    Total = r.Total,
                })
                .ToList();

            return new DepartmentIncomeDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Departments = departments,
                Rows = rows,
            };
        }

        /// <summary>项目利润表：按辅助核算「项目」维度拆解收入、成本费用与利润，每个项目一列并附合计列。</summary>
        public async Task<ProjectIncomeDto> GetByProjectAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var data = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(data.AccountSet, data.PostedVouchers, year, month);
            var (currentByProject, ytdByProject, projectIds) = AccumulateByAssist(
                data, AssistType.Project, resolvedYear, resolvedMonth);

            // 项目列按编码排序，与金额列下标保持一致。
            var orderedProjectIds = projectIds
                .OrderBy(id => data.AssistItemsById.GetValueOrDefault(id)?.Code ?? string.Empty)
                .ToList();

            var projects = orderedProjectIds
                .Select(id => data.AssistItemsById.GetValueOrDefault(id))
                .Where(a => a is not null)
                .Select(a => new IncomeProjectColumnDto { Id = a!.Id, Name = a.Name })
                .ToList();

            var rows = BuildAssistRows(orderedProjectIds, currentByProject, ytdByProject)
                .Select(r => new IncomeProjectRowDto
                {
                    Item = r.Item,
                    Line = r.Line,
                    Amounts = r.Amounts,
                    Total = r.Total,
                })
                .ToList();

            return new ProjectIncomeDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Projects = projects,
                Rows = rows,
            };
        }

        private async Task<(
            AccountSet AccountSet,
            Dictionary<Guid, Account> AccountsById,
            List<Voucher> PostedVouchers,
            Dictionary<Guid, AssistItem> AssistItemsById)> LoadAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");
            var accountsById = (await _accountRepository.GetAllAsync(accountSetId, cancellationToken))
                .ToDictionary(a => a.Id, a => a);
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();
            var assistItemsById = (await _assistItemRepository.GetAllAsync(accountSetId, cancellationToken))
                .ToDictionary(a => a.Id, a => a);
            return (accountSet, accountsById, vouchers, assistItemsById);
        }

        /// <summary>结转损益凭证识别（与利润表一致：含本年利润/旧准则本年利润科目的凭证）。</summary>
        private static HashSet<Guid> CarryVoucherIds(IReadOnlyList<Voucher> vouchers, IReadOnlySet<Guid> carryAccountIds)
        {
            return vouchers
                .Where(v => v.Entries.Any(e => carryAccountIds.Contains(e.AccountId)))
                .Select(v => v.Id)
                .ToHashSet();
        }

        /// <summary>按辅助核算维度逐行归集损益金额：返回 (本期, 本年累计, 出现过的档案 id)。</summary>
        private (Dictionary<Guid, Dictionary<int, decimal>> currentByAssist,
                 Dictionary<Guid, Dictionary<int, decimal>> ytdByAssist,
                 List<Guid> assistItemIds) AccumulateByAssist(
            (AccountSet AccountSet, Dictionary<Guid, Account> AccountsById, List<Voucher> PostedVouchers, Dictionary<Guid, AssistItem> AssistItemsById) data,
            AssistType assistType,
            int resolvedYear,
            int resolvedMonth)
        {
            var periodStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var periodEnd = periodStart.AddMonths(1);
            var yearStart = new DateOnly(resolvedYear, 1, 1);

            var carryAccountIds = data.AccountsById.Values
                .Where(a => IncomeLineMapper.Prefix(a.Code) is "4103" or "3103")
                .Select(a => a.Id)
                .ToHashSet();
            var carryVoucherIds = CarryVoucherIds(data.PostedVouchers, carryAccountIds);

            var currentByAssist = new Dictionary<Guid, Dictionary<int, decimal>>();
            var ytdByAssist = new Dictionary<Guid, Dictionary<int, decimal>>();

            void Accumulate(Voucher voucher, Dictionary<Guid, Dictionary<int, decimal>> target)
            {
                foreach (var entry in voucher.Entries)
                {
                    if (!data.AccountsById.TryGetValue(entry.AccountId, out var account)
                        || account.Category != AccountCategory.ProfitAndLoss)
                    {
                        continue;
                    }

                    var line = IncomeLineMapper.MapLine(account.Code, account.Direction);
                    var signed = IncomeLineMapper.IsRevenueLine(line) ? -1m : 1m;
                    var amount = (entry.Debit - entry.Credit) * signed;
                    foreach (var assist in entry.Assists.Where(a => a.Type == assistType))
                    {
                        var byLine = target.TryGetValue(assist.AssistItemId, out var dict)
                            ? dict
                            : target[assist.AssistItemId] = new Dictionary<int, decimal>();
                        byLine[line] = byLine.GetValueOrDefault(line) + amount;
                    }
                }
            }

            foreach (var voucher in data.PostedVouchers)
            {
                if (carryVoucherIds.Contains(voucher.Id))
                {
                    continue;
                }

                if (voucher.Date >= yearStart && voucher.Date < periodEnd)
                {
                    Accumulate(voucher, ytdByAssist);
                }

                if (voucher.Date >= periodStart && voucher.Date < periodEnd)
                {
                    Accumulate(voucher, currentByAssist);
                }
            }

            var assistItemIds = currentByAssist.Keys.Union(ytdByAssist.Keys).ToList();
            return (currentByAssist, ytdByAssist, assistItemIds);
        }

        /// <summary>
        /// 组装维度利润表行集：损益明细行（按行次升序去重）+ 营业利润/利润总额/净利润计算行。
        /// 每个维度一列（金额与列头顺序对齐），另计算合计列（所有维度同行的本期/累计之和）。
        /// 合计列由各维度金额直接相加得出——计算行也是线性运算，故合计等于按合计口径再算一次。
        /// </summary>
        private static List<AssistRow> BuildAssistRows(
            IReadOnlyList<Guid> itemIds,
            Dictionary<Guid, Dictionary<int, decimal>> currentByAssist,
            Dictionary<Guid, Dictionary<int, decimal>> ytdByAssist)
        {
            var detailLines = currentByAssist.Values.SelectMany(d => d.Keys)
                .Union(ytdByAssist.Values.SelectMany(d => d.Keys))
                .Distinct()
                .OrderBy(l => l)
                .ToList();

            decimal LineAmount(Dictionary<Guid, Dictionary<int, decimal>> source, int line, int index)
            {
                if (index >= itemIds.Count)
                {
                    return 0m;
                }

                return source.GetValueOrDefault(itemIds[index]).GetValueOrDefault(line);
            }

            IncomeDimensionAmountDto Round(decimal current, decimal yearToDate) =>
                new()
                {
                    Current = Math.Round(current, 2, MidpointRounding.ToEven),
                    YearToDate = Math.Round(yearToDate, 2, MidpointRounding.ToEven),
                };

            IncomeDimensionAmountDto SumAmounts(IReadOnlyList<IncomeDimensionAmountDto> list) =>
                Round(list.Sum(a => a.Current), list.Sum(a => a.YearToDate));

            var rows = new List<AssistRow>();
            foreach (var line in detailLines)
            {
                var amounts = new List<IncomeDimensionAmountDto>();
                for (var i = 0; i < itemIds.Count; i++)
                {
                    amounts.Add(Round(LineAmount(currentByAssist, line, i), LineAmount(ytdByAssist, line, i)));
                }

                rows.Add(new AssistRow(IncomeLineMapper.LineName(line), line, amounts, SumAmounts(amounts)));
            }

            void AddComputedRow(string name, int line, Func<int, Dictionary<Guid, Dictionary<int, decimal>>, decimal> compute)
            {
                var amounts = new List<IncomeDimensionAmountDto>();
                for (var i = 0; i < itemIds.Count; i++)
                {
                    amounts.Add(Round(compute(i, currentByAssist), compute(i, ytdByAssist)));
                }

                rows.Add(new AssistRow(name, line, amounts, SumAmounts(amounts)));
            }

            decimal OperatingProfit(int index, Dictionary<Guid, Dictionary<int, decimal>> source)
            {
                decimal revenue = 0m;
                decimal expense = 0m;
                foreach (var line in detailLines)
                {
                    if (line is 12 or 14 or 17)
                    {
                        continue; // 营业外收支与所得税不计入营业利润
                    }

                    var amount = LineAmount(source, line, index);
                    if (IncomeLineMapper.IsRevenueLine(line))
                    {
                        revenue += amount;
                    }
                    else
                    {
                        expense += amount;
                    }
                }

                return revenue - expense;
            }

            decimal TotalProfit(int index, Dictionary<Guid, Dictionary<int, decimal>> source)
            {
                var operating = OperatingProfit(index, source);
                var nonOperating = LineAmount(source, 12, index) - LineAmount(source, 14, index);
                return operating + nonOperating;
            }

            decimal NetProfit(int index, Dictionary<Guid, Dictionary<int, decimal>> source)
            {
                return TotalProfit(index, source) - LineAmount(source, 17, index);
            }

            AddComputedRow("营业利润", 11, OperatingProfit);
            AddComputedRow("利润总额", 16, TotalProfit);
            AddComputedRow("净利润", 18, NetProfit);
            return rows.OrderBy(r => r.Line).ToList();
        }

        /// <summary>维度利润表中间行：项目名称、行次、各维度金额与合计。</summary>
        private sealed record AssistRow(
            string Item,
            int Line,
            IReadOnlyList<IncomeDimensionAmountDto> Amounts,
            IncomeDimensionAmountDto Total);
    }
}
