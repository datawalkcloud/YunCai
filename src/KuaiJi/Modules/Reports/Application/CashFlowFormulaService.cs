using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.Reports.Domain.CashFlow;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 现金流量表公式法：每行金额由「行取数公式」计算，支持科目发生额/余额、现金流辅助项目、
    /// 行次引用与系统默认归类（AUTO()）。直接法主表不受影响，这里只提供可配置的另一套口径。
    /// </summary>
    public sealed class CashFlowFormulaService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IAssistItemRepository _assistItemRepository;
        private readonly ICashFlowFormulaRepository _formulaRepository;

        public CashFlowFormulaService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            IAssistItemRepository assistItemRepository,
            ICashFlowFormulaRepository formulaRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _assistItemRepository = assistItemRepository;
            _formulaRepository = formulaRepository;
        }

        /// <summary>读取 35 行的生效公式（自定义优先，否则默认公式）。</summary>
        public async Task<CashFlowFormulaSettingsDto> GetSettingsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await RequireAccountSetAsync(accountSetId, cancellationToken);
            return await BuildSettingsAsync(accountSetId, cancellationToken);
        }

        /// <summary>
        /// 保存取数公式。只处理请求中出现的行次；公式为空表示删除该行自定义配置、恢复默认公式。
        /// </summary>
        public async Task<CashFlowFormulaSettingsDto> SaveAsync(
            Guid accountSetId,
            SaveCashFlowFormulasRequest request,
            CancellationToken cancellationToken = default)
        {
            await RequireAccountSetAsync(accountSetId, cancellationToken);

            var items = request?.Formulas ?? [];
            var seen = new HashSet<int>();
            var normalized = new List<(int Line, string Expression)>();
            foreach (var item in items)
            {
                if (!CashFlowStandardRows.ByLine.ContainsKey(item.Line))
                {
                    throw new ReportException($"现金流量表不存在行次 {item.Line}。");
                }

                if (!seen.Add(item.Line))
                {
                    throw new ReportException($"行次 {item.Line} 重复提交。");
                }

                var expression = (item.Expression ?? string.Empty).Trim();
                if (expression.Length > CashFlowFormula.MaxExpressionLength)
                {
                    throw new ReportException($"第 {item.Line} 行取数公式不能超过 {CashFlowFormula.MaxExpressionLength} 个字符。");
                }

                if (expression.Length > 0)
                {
                    Validate(expression, item.Line);
                }

                normalized.Add((item.Line, expression));
            }

            var existing = await _formulaRepository.GetAllAsync(accountSetId, cancellationToken);
            var byLine = existing.ToDictionary(f => f.Line);
            var now = DateTime.UtcNow;
            foreach (var (line, expression) in normalized)
            {
                if (expression.Length == 0)
                {
                    if (byLine.TryGetValue(line, out var stale))
                    {
                        _formulaRepository.Remove(stale);
                    }

                    continue;
                }

                if (byLine.TryGetValue(line, out var current))
                {
                    current.Update(expression, now);
                }
                else
                {
                    await _formulaRepository.AddAsync(CashFlowFormula.Create(accountSetId, line, expression, now), cancellationToken);
                }
            }

            await _formulaRepository.SaveChangesAsync(cancellationToken);
            return await BuildSettingsAsync(accountSetId, cancellationToken);
        }

        /// <summary>
        /// 按公式口径生成月度现金流量表：本期 / 本年累计 / 上年同期三列分别用同一套公式
        /// 在各自时间窗口内取值；行次引用只允许指向更小的行次。
        /// </summary>
        public async Task<CashFlowDto> GetPeriodAsync(
            Guid accountSetId,
            int? year,
            int? month,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await RequireAccountSetAsync(accountSetId, cancellationToken);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);
            var monthStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var yearStart = new DateOnly(resolvedYear, 1, 1);
            var lastYearStart = new DateOnly(resolvedYear - 1, 1, 1);
            var lastYearEnd = new DateOnly(resolvedYear - 1, resolvedMonth, 1).AddMonths(1).AddDays(-1);

            var stored = await _formulaRepository.GetAllAsync(accountSetId, cancellationToken);
            var expressions = MergeExpressions(stored);
            var formulas = ParseAll(expressions);
            var assistItems = await _assistItemRepository.GetByTypeAsync(accountSetId, AssistType.CashFlow, cancellationToken);

            var current = EvaluateWindow(accounts, vouchers, assistItems, formulas, monthStart, monthEnd);
            var yearToDate = EvaluateWindow(accounts, vouchers, assistItems, formulas, yearStart, monthEnd);
            var lastYear = EvaluateWindow(accounts, vouchers, assistItems, formulas, lastYearStart, lastYearEnd);

            return new CashFlowDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Lines = CashFlowStandardRows.All.Select(def => new CashFlowLineDto
                {
                    Line = def.Line,
                    Name = def.Name,
                    Section = def.Section,
                    Kind = def.Kind,
                    Current = Round(current.GetValueOrDefault(def.Line)),
                    YearToDate = Round(yearToDate.GetValueOrDefault(def.Line)),
                    LastYear = Round(lastYear.GetValueOrDefault(def.Line)),
                    Formula = expressions.GetValueOrDefault(def.Line),
                }).ToList(),
            };
        }

        private async Task<AccountSet> RequireAccountSetAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            return await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                   ?? throw new ReportException("指定的账套不存在。");
        }

        private async Task<CashFlowFormulaSettingsDto> BuildSettingsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var stored = await _formulaRepository.GetAllAsync(accountSetId, cancellationToken);
            var byLine = stored.ToDictionary(f => f.Line);
            return new CashFlowFormulaSettingsDto
            {
                Lines = CashFlowStandardRows.All.Select(def => new CashFlowFormulaDto
                {
                    Line = def.Line,
                    Name = def.Name,
                    Expression = byLine.TryGetValue(def.Line, out var custom)
                        ? custom.Expression
                        : CashFlowFormulaDefaults.For(def.Line),
                    DefaultExpression = CashFlowFormulaDefaults.For(def.Line),
                    IsCustomized = byLine.ContainsKey(def.Line),
                }).ToList(),
            };
        }

        /// <summary>默认公式打底，再用账套自定义公式按行覆盖。</summary>
        private static Dictionary<int, string> MergeExpressions(IReadOnlyList<CashFlowFormula> stored)
        {
            var merged = CashFlowFormulaDefaults.All.ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (var formula in stored)
            {
                merged[formula.Line] = formula.Expression;
            }

            return merged;
        }

        private static Dictionary<int, CashFlowFormulaNode> ParseAll(IReadOnlyDictionary<int, string> expressions)
        {
            var formulas = new Dictionary<int, CashFlowFormulaNode>(expressions.Count);
            foreach (var (line, expression) in expressions)
            {
                try
                {
                    var node = CashFlowFormulaParser.Parse(expression);
                    CashFlowFormulaParser.EnsureRowOrder(node, line);
                    formulas[line] = node;
                }
                catch (ReportException ex)
                {
                    var name = CashFlowStandardRows.ByLine.TryGetValue(line, out var def) ? def.Name : $"行次 {line}";
                    throw new ReportException($"第 {line} 行「{name}」取数公式无效：{ex.Message}");
                }
            }

            return formulas;
        }

        private static void Validate(string expression, int line)
        {
            try
            {
                var node = CashFlowFormulaParser.Parse(expression);
                CashFlowFormulaParser.EnsureRowOrder(node, line);
            }
            catch (ReportException ex)
            {
                throw new ReportException($"第 {line} 行取数公式无效：{ex.Message}");
            }
        }

        private static Dictionary<int, decimal> EvaluateWindow(
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> vouchers,
            IReadOnlyList<AssistItem> assistItems,
            IReadOnlyDictionary<int, CashFlowFormulaNode> formulas,
            DateOnly start,
            DateOnly end)
        {
            var data = CashFlowFormulaData.Build(accounts, vouchers, assistItems, start, end);
            return CashFlowFormulaEvaluator.Evaluate(formulas, data);
        }

        private static decimal Round(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.ToEven);
        }
    }
}
