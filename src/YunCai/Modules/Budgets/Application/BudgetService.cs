using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Budgets.Domain;

namespace YunCai.Modules.Budgets.Application
{
    /// <summary>
    /// 预算管理与执行对比：预算单维护 + 已过账凭证的实际发生额对比。
    /// 实际数按科目编码前缀归集到预算行（末级发生的金额汇总到上级预算科目），
    /// 同一笔发生额只计入「最长匹配」的预算行，避免父子科目都建预算时重复统计。
    /// </summary>
    public sealed class BudgetService
    {
        private readonly IBudgetRepository _budgets;
        private readonly IAccountRepository _accounts;

        public BudgetService(IBudgetRepository budgets, IAccountRepository accounts)
        {
            _budgets = budgets;
            _accounts = accounts;
        }

        public async Task<IReadOnlyList<BudgetSummaryDto>> GetListAsync(Guid accountSetId, int? year,
            CancellationToken cancellationToken = default)
        {
            var list = await _budgets.GetListAsync(accountSetId, year, cancellationToken);
            return list.Select(b => new BudgetSummaryDto
            {
                Id = b.Id,
                Year = b.Year,
                Name = b.Name,
                Note = b.Note,
                LineCount = b.Lines.Count,
                Total = b.Lines.Sum(l => l.Total),
                CreatedBy = b.CreatedBy,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
            }).ToList();
        }

        public async Task<BudgetDto?> GetAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var budget = await _budgets.GetByIdAsync(accountSetId, id, cancellationToken);
            if (budget is null)
            {
                return null;
            }

            var accounts = await _accounts.GetAllAsync(accountSetId, cancellationToken);
            return ToDto(budget, accounts.ToDictionary(a => a.Id));
        }

        public async Task<BudgetDto> CreateAsync(Guid accountSetId, SaveBudgetRequest request, string createdBy,
            CancellationToken cancellationToken = default)
        {
            var accounts = await _accounts.GetAllAsync(accountSetId, cancellationToken);
            var budget = Budget.Create(accountSetId, request.Year, request.Name, request.Note, createdBy);
            budget.ReplaceLines(NormalizeLines(request.Lines, accounts.ToDictionary(a => a.Id)));

            if (await _budgets.ExistsNameAsync(accountSetId, budget.Year, budget.Name, null, cancellationToken))
            {
                throw new BudgetException($"{budget.Year} 年度已存在同名预算「{budget.Name}」。");
            }

            await _budgets.AddAsync(budget, cancellationToken);
            await _budgets.SaveChangesAsync(cancellationToken);
            return ToDto(budget, accounts.ToDictionary(a => a.Id));
        }

        public async Task<BudgetDto?> UpdateAsync(Guid accountSetId, Guid id, SaveBudgetRequest request,
            CancellationToken cancellationToken = default)
        {
            var budget = await _budgets.GetByIdAsync(accountSetId, id, cancellationToken);
            if (budget is null)
            {
                return null;
            }

            if (request.Year != 0 && request.Year != budget.Year)
            {
                throw new BudgetException("预算年度创建后不可修改，请新建对应年度的预算。");
            }

            var accounts = await _accounts.GetAllAsync(accountSetId, cancellationToken);
            budget.Update(request.Name, request.Note);
            budget.ReplaceLines(NormalizeLines(request.Lines, accounts.ToDictionary(a => a.Id)));

            if (await _budgets.ExistsNameAsync(accountSetId, budget.Year, budget.Name, budget.Id, cancellationToken))
            {
                throw new BudgetException($"{budget.Year} 年度已存在同名预算「{budget.Name}」。");
            }

            await _budgets.SaveChangesAsync(cancellationToken);
            return ToDto(budget, accounts.ToDictionary(a => a.Id));
        }

        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var budget = await _budgets.GetByIdAsync(accountSetId, id, cancellationToken);
            if (budget is null)
            {
                return false;
            }

            _budgets.Remove(budget);
            await _budgets.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>预算执行对比；throughMonth 缺省为 12（全年），用于「截至本月」口径。</summary>
        public async Task<BudgetExecutionDto?> GetExecutionAsync(Guid accountSetId, Guid id, int? throughMonth,
            CancellationToken cancellationToken = default)
        {
            var budget = await _budgets.GetByIdAsync(accountSetId, id, cancellationToken);
            if (budget is null)
            {
                return null;
            }

            var through = Math.Clamp(throughMonth ?? 12, 1, 12);
            var accounts = (await _accounts.GetAllAsync(accountSetId, cancellationToken))
                .ToDictionary(a => a.Id);
            var actuals = await _budgets.GetActualsAsync(accountSetId, budget.Year, cancellationToken);

            // 实际发生额按科目方向折成正数：借方科目取「借 - 贷」，贷方科目取「贷 - 借」。
            var netByAccountMonth = new Dictionary<(Guid AccountId, int Month), decimal>();
            foreach (var row in actuals)
            {
                var direction = accounts.TryGetValue(row.AccountId, out var account)
                    ? account.Direction
                    : BalanceDirection.Debit;
                var net = direction == BalanceDirection.Debit ? row.Debit - row.Credit : row.Credit - row.Debit;
                if (net == 0)
                {
                    continue;
                }

                var key = (row.AccountId, row.Month);
                netByAccountMonth[key] = netByAccountMonth.GetValueOrDefault(key) + net;
            }

            // 每条实际发生额只归到最长匹配的预算科目，父子科目都建预算时不重复统计。
            var lines = budget.Lines
                .Select(line => new
                {
                    Line = line,
                    Account = accounts.GetValueOrDefault(line.AccountId),
                })
                .Where(x => x.Account is not null)
                .OrderByDescending(x => x.Account!.Code.Length)
                .ToList();

            var actualBuckets = lines.ToDictionary(x => x.Line.Id, _ => new decimal[BudgetLine.MonthCount]);
            foreach (var ((accountId, month), net) in netByAccountMonth)
            {
                if (!accounts.TryGetValue(accountId, out var actualAccount))
                {
                    continue;
                }

                var owner = lines.FirstOrDefault(x => actualAccount.Code.StartsWith(x.Account!.Code, StringComparison.Ordinal));
                if (owner is null)
                {
                    continue;
                }

                actualBuckets[owner.Line.Id][month - 1] += net;
            }

            var rows = new List<BudgetExecutionRowDto>(lines.Count);
            foreach (var item in lines.OrderBy(x => x.Account!.Code, StringComparer.Ordinal))
            {
                var line = item.Line;
                var account = item.Account!;
                var budgetMonths = line.ToMonths();
                var actualMonths = actualBuckets[line.Id];
                var budgetToDate = budgetMonths.Take(through).Sum();
                var actualToDate = actualMonths.Take(through).Sum();
                rows.Add(new BudgetExecutionRowDto
                {
                    AccountId = line.AccountId,
                    AccountCode = account.Code,
                    AccountName = account.Name,
                    Direction = (int)account.Direction,
                    BudgetMonths = budgetMonths,
                    ActualMonths = actualMonths,
                    BudgetToDate = budgetToDate,
                    ActualToDate = actualToDate,
                    Variance = actualToDate - budgetToDate,
                    Rate = RateOf(budgetToDate, actualToDate),
                });
            }

            var totalBudgetMonths = SumMonths(rows.Select(r => r.BudgetMonths));
            var totalActualMonths = SumMonths(rows.Select(r => r.ActualMonths));
            var totalBudget = totalBudgetMonths.Take(through).Sum();
            var totalActual = totalActualMonths.Take(through).Sum();

            return new BudgetExecutionDto
            {
                BudgetId = budget.Id,
                Year = budget.Year,
                Name = budget.Name,
                Note = budget.Note,
                ThroughMonth = through,
                Rows = rows,
                BudgetMonths = totalBudgetMonths,
                ActualMonths = totalActualMonths,
                BudgetToDate = totalBudget,
                ActualToDate = totalActual,
                Variance = totalActual - totalBudget,
                Rate = RateOf(totalBudget, totalActual),
            };
        }

        private static decimal? RateOf(decimal budget, decimal actual)
            => budget == 0 ? null : decimal.Round(actual / budget * 100m, 2, MidpointRounding.AwayFromZero);

        private static decimal[] SumMonths(IEnumerable<IReadOnlyList<decimal>> months)
        {
            var total = new decimal[BudgetLine.MonthCount];
            foreach (var row in months)
            {
                for (var i = 0; i < total.Length && i < row.Count; i++)
                {
                    total[i] += row[i];
                }
            }

            return total;
        }

        private static IReadOnlyList<(Guid AccountId, IReadOnlyList<decimal> Months)> NormalizeLines(
            IReadOnlyList<SaveBudgetLineRequest> lines, IReadOnlyDictionary<Guid, Account> accounts)
        {
            if (lines.Count == 0)
            {
                throw new BudgetException("请至少为一个科目填写预算。");
            }

            var result = new List<(Guid, IReadOnlyList<decimal>)>(lines.Count);
            foreach (var line in lines)
            {
                if (!accounts.ContainsKey(line.AccountId))
                {
                    throw new BudgetException("预算行引用了不属于当前账套的科目。");
                }

                var months = new decimal[BudgetLine.MonthCount];
                for (var i = 0; i < months.Length && i < line.Months.Count; i++)
                {
                    months[i] = line.Months[i];
                }

                result.Add((line.AccountId, months));
            }

            return result;
        }

        private static BudgetDto ToDto(Budget budget, IReadOnlyDictionary<Guid, Account> accounts)
        {
            var lines = budget.Lines
                .Select(line =>
                {
                    accounts.TryGetValue(line.AccountId, out var account);
                    return new BudgetLineDto
                    {
                        AccountId = line.AccountId,
                        AccountCode = account?.Code ?? string.Empty,
                        AccountName = account?.Name ?? "（科目已删除）",
                        IsLeaf = account?.IsLeaf ?? false,
                        Months = line.ToMonths(),
                        Total = line.Total,
                    };
                })
                .OrderBy(l => l.AccountCode, StringComparer.Ordinal)
                .ToList();

            return new BudgetDto
            {
                Id = budget.Id,
                Year = budget.Year,
                Name = budget.Name,
                Note = budget.Note,
                CreatedBy = budget.CreatedBy,
                CreatedAt = budget.CreatedAt,
                UpdatedAt = budget.UpdatedAt,
                Lines = lines,
                Total = lines.Sum(l => l.Total),
            };
        }
    }
}
