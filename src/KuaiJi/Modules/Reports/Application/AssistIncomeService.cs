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
            var (currentByDeptLine, ytdByDeptLine, departmentIds) = AccumulateByAssist(
                data, AssistType.Department, resolvedYear, resolvedMonth);

            var departments = departmentIds
                .Select(id => data.AssistItemsById.GetValueOrDefault(id))
                .Where(a => a is not null)
                .Select(a => new IncomeDepartmentDto { Id = a!.Id, Name = a.Name })
                .ToList();

            var rows = BuildDepartmentRows(currentByDeptLine, ytdByDeptLine, departmentIds, departments.Count);
            return new DepartmentIncomeDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Departments = departments,
                Rows = rows,
            };
        }

        /// <summary>项目利润表：每个项目一行，本期利润额与本年累计利润额。</summary>
        public async Task<ProjectIncomeDto> GetByProjectAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var data = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(data.AccountSet, data.PostedVouchers, year, month);
            var (currentByProject, ytdByProject, _) = AccumulateProfitByAssist(
                data, AssistType.Project, resolvedYear, resolvedMonth);

            var rows = new List<IncomeProjectRowDto>();
            foreach (var projectId in currentByProject.Keys.Union(ytdByProject.Keys).OrderBy(id =>
                     data.AssistItemsById.GetValueOrDefault(id)?.Code ?? string.Empty))
            {
                var item = data.AssistItemsById.GetValueOrDefault(projectId);
                if (item is null)
                {
                    continue;
                }

                rows.Add(new IncomeProjectRowDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Line = rows.Count + 1,
                    Current = Math.Round(currentByProject.GetValueOrDefault(projectId), 2, MidpointRounding.ToEven),
                    YearToDate = Math.Round(ytdByProject.GetValueOrDefault(projectId), 2, MidpointRounding.ToEven),
                });
            }

            return new ProjectIncomeDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
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

        /// <summary>项目维度的损益净额（收入为正、成本费用为负），返回 (本期, 本年累计, 出现过的档案 id)。</summary>
        private (Dictionary<Guid, decimal> currentByProject,
                 Dictionary<Guid, decimal> ytdByProject,
                 List<Guid> assistItemIds) AccumulateProfitByAssist(
            (AccountSet AccountSet, Dictionary<Guid, Account> AccountsById, List<Voucher> PostedVouchers, Dictionary<Guid, AssistItem> AssistItemsById) data,
            AssistType assistType,
            int resolvedYear,
            int resolvedMonth)
        {
            var (currentByAssist, ytdByAssist, assistItemIds) = AccumulateByAssist(data, assistType, resolvedYear, resolvedMonth);
            return (SumProfit(currentByAssist), SumProfit(ytdByAssist), assistItemIds);
        }

        /// <summary>把行次金额折叠成项目利润：收入行（贷方为正）加总、费用行（借方为正）取负后相加。</summary>
        private static Dictionary<Guid, decimal> SumProfit(Dictionary<Guid, Dictionary<int, decimal>> byAssist)
        {
            var result = new Dictionary<Guid, decimal>();
            foreach (var (assistItemId, byLine) in byAssist)
            {
                decimal profit = 0m;
                foreach (var (line, amount) in byLine)
                {
                    profit += IncomeLineMapper.IsRevenueLine(line) ? amount : -amount;
                }

                result[assistItemId] = profit;
            }

            return result;
        }

        /// <summary>组装部门利润表行集：明细项目行 + 营业利润/利润总额/净利润计算行。</summary>
        private static List<IncomeDepartmentRowDto> BuildDepartmentRows(
            Dictionary<Guid, Dictionary<int, decimal>> currentByAssist,
            Dictionary<Guid, Dictionary<int, decimal>> ytdByAssist,
            IReadOnlyList<Guid> departmentIds,
            int departmentCount)
        {
            // 部门利润表展示行：损益明细行（按行次升序去重）+ 三个计算行。
            var detailLines = currentByAssist.Values.SelectMany(d => d.Keys)
                .Union(ytdByAssist.Values.SelectMany(d => d.Keys))
                .Distinct()
                .OrderBy(l => l)
                .ToList();

            decimal LineAmount(Dictionary<Guid, Dictionary<int, decimal>> source, int line, int deptIndex)
            {
                if (deptIndex >= departmentIds.Count)
                {
                    return 0m;
                }

                return source.GetValueOrDefault(departmentIds[deptIndex]).GetValueOrDefault(line);
            }

            decimal Profit(Dictionary<Guid, Dictionary<int, decimal>> source, int deptIndex)
            {
                // 利润 = Σ收入行 + Σ(-费用行)，再扣减营业外收支（含在行集里，直接对全部行求「收入正费用负」）。
                decimal total = 0m;
                foreach (var line in detailLines)
                {
                    var amount = LineAmount(source, line, deptIndex);
                    total += IncomeLineMapper.IsRevenueLine(line) ? amount : -amount;
                }

                return total;
            }

            var rows = new List<IncomeDepartmentRowDto>();
            foreach (var line in detailLines)
            {
                var amounts = new List<IncomeDepartmentAmountDto>();
                for (var deptIndex = 0; deptIndex < departmentCount; deptIndex++)
                {
                    amounts.Add(new IncomeDepartmentAmountDto
                    {
                        Current = Math.Round(LineAmount(currentByAssist, line, deptIndex), 2, MidpointRounding.ToEven),
                        YearToDate = Math.Round(LineAmount(ytdByAssist, line, deptIndex), 2, MidpointRounding.ToEven),
                    });
                }

                rows.Add(new IncomeDepartmentRowDto
                {
                    Item = IncomeLineMapper.LineName(line),
                    Line = rows.Count + 1,
                    Amounts = amounts,
                });
            }

            // 营业利润 = 收入行 - 营业成本/税金及附加/期间费用（不含营业外与所得税）；
            // 利润总额 = 营业利润 + 营业外收支；净利润 = 利润总额 - 所得税费用。
            void AddComputedRow(string name, Func<int, Dictionary<Guid, Dictionary<int, decimal>>, decimal> compute)
            {
                var amounts = new List<IncomeDepartmentAmountDto>();
                for (var deptIndex = 0; deptIndex < departmentCount; deptIndex++)
                {
                    amounts.Add(new IncomeDepartmentAmountDto
                    {
                        Current = Math.Round(compute(deptIndex, currentByAssist), 2, MidpointRounding.ToEven),
                        YearToDate = Math.Round(compute(deptIndex, ytdByAssist), 2, MidpointRounding.ToEven),
                    });
                }

                rows.Add(new IncomeDepartmentRowDto
                {
                    Item = name,
                    Line = rows.Count + 1,
                    Amounts = amounts,
                });
            }

            decimal OperatingProfit(int deptIndex, Dictionary<Guid, Dictionary<int, decimal>> source)
            {
                decimal revenue = 0m;
                decimal expense = 0m;
                foreach (var line in detailLines)
                {
                    var amount = LineAmount(source, line, deptIndex);
                    if (line is 19)
                    {
                        continue; // 所得税不进营业利润
                    }

                    if (IncomeLineMapper.IsRevenueLine(line))
                    {
                        revenue += amount;
                    }
                    else if (line is 16 or 17)
                    {
                        continue; // 营业外收支不进营业利润
                    }
                    else
                    {
                        expense += amount;
                    }
                }

                return revenue - expense;
            }

            decimal TotalProfit(int deptIndex, Dictionary<Guid, Dictionary<int, decimal>> source)
            {
                var operating = OperatingProfit(deptIndex, source);
                var nonOperating = detailLines
                    .Where(l => l is 16 or 17)
                    .Sum(l => LineAmount(source, l, deptIndex) * (l == 16 ? 1m : -1m));
                return operating + nonOperating;
            }

            decimal NetProfit(int deptIndex, Dictionary<Guid, Dictionary<int, decimal>> source)
            {
                return TotalProfit(deptIndex, source) - LineAmount(source, 19, deptIndex);
            }

            AddComputedRow("营业利润", OperatingProfit);
            AddComputedRow("利润总额", TotalProfit);
            AddComputedRow("净利润", NetProfit);
            return rows;
        }
    }
}
