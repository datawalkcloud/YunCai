using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.SharedKernel.Infrastructure.Excel;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>报表导出类型（与前端 /reports/export 的 kind 参数对应）。</summary>
    public enum ReportExportKind
    {
        TrialBalance = 1,
        BalanceSheet,
        IncomeStatement,
        IncomeQuarter,
        CashFlow,
        CashFlowQuarter,
        DepartmentIncome,
        ProjectIncome,
        ExpenseDetail,
        EquityChange,
        TaxStatistics,
        FinancialSummary,
    }

    /// <summary>kind 文本（kebab-case）解析。</summary>
    public static class ReportExportKindText
    {
        public static bool TryParse(string kind, out ReportExportKind value)
        {
            value = kind switch
            {
                "trial-balance" => ReportExportKind.TrialBalance,
                "balance-sheet" => ReportExportKind.BalanceSheet,
                "income-statement" => ReportExportKind.IncomeStatement,
                "income-quarter" => ReportExportKind.IncomeQuarter,
                "cash-flow" => ReportExportKind.CashFlow,
                "cash-flow-quarter" => ReportExportKind.CashFlowQuarter,
                "department-income" => ReportExportKind.DepartmentIncome,
                "project-income" => ReportExportKind.ProjectIncome,
                "expense-detail" => ReportExportKind.ExpenseDetail,
                "equity-change" => ReportExportKind.EquityChange,
                "tax" => ReportExportKind.TaxStatistics,
                "financial-summary" => ReportExportKind.FinancialSummary,
                _ => ReportExportKind.TrialBalance,
            };
            return kind is "trial-balance" or "balance-sheet" or "income-statement" or "income-quarter"
                or "cash-flow" or "cash-flow-quarter" or "department-income" or "project-income"
                or "expense-detail" or "equity-change" or "tax" or "financial-summary";
        }
    }

    /// <summary>
    /// 报表导出服务：按类型调用对应报表服务取数，投影成通用 <see cref="FlatWorkbook"/>（页头信息 + 压平表体）。
    /// 金额列为数值类型；比率/同比列为已格式化的百分比文本。
    /// </summary>
    public sealed class ReportExportService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly TrialBalanceService _trialBalanceService;
        private readonly BalanceSheetService _balanceSheetService;
        private readonly IncomeStatementService _incomeStatementService;
        private readonly CashFlowService _cashFlowService;
        private readonly AssistIncomeService _assistIncomeService;
        private readonly ExpenseDetailService _expenseDetailService;
        private readonly EquityChangeService _equityChangeService;
        private readonly TaxStatisticsService _taxStatisticsService;
        private readonly FinancialSummaryService _financialSummaryService;

        public ReportExportService(
            IAccountSetRepository accountSetRepository,
            TrialBalanceService trialBalanceService,
            BalanceSheetService balanceSheetService,
            IncomeStatementService incomeStatementService,
            CashFlowService cashFlowService,
            AssistIncomeService assistIncomeService,
            ExpenseDetailService expenseDetailService,
            EquityChangeService equityChangeService,
            TaxStatisticsService taxStatisticsService,
            FinancialSummaryService financialSummaryService)
        {
            _accountSetRepository = accountSetRepository;
            _trialBalanceService = trialBalanceService;
            _balanceSheetService = balanceSheetService;
            _incomeStatementService = incomeStatementService;
            _cashFlowService = cashFlowService;
            _assistIncomeService = assistIncomeService;
            _expenseDetailService = expenseDetailService;
            _equityChangeService = equityChangeService;
            _taxStatisticsService = taxStatisticsService;
            _financialSummaryService = financialSummaryService;
        }

        /// <summary>导出指定报表。year/month/quarter 为可选期间参数，省略时各报表取最近有凭证期间。</summary>
        public async Task<FlatWorkbook> ExportAsync(
            Guid accountSetId,
            ReportExportKind kind,
            int? year,
            int? month,
            int? quarter,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");
            var setName = accountSet.Name ?? "账套";

            FlatWorkbook book = kind switch
            {
                ReportExportKind.TrialBalance => await TrialBalanceAsync(accountSetId, setName, cancellationToken),
                ReportExportKind.BalanceSheet => await BalanceSheetAsync(accountSetId, setName, cancellationToken),
                ReportExportKind.IncomeStatement => await IncomeAsync(accountSetId, setName, false, cancellationToken),
                ReportExportKind.IncomeQuarter => await IncomeAsync(accountSetId, setName, true, cancellationToken),
                ReportExportKind.CashFlow => await CashFlowAsync(accountSetId, setName, false, cancellationToken),
                ReportExportKind.CashFlowQuarter => await CashFlowAsync(accountSetId, setName, true, cancellationToken),
                ReportExportKind.DepartmentIncome => await DepartmentIncomeAsync(accountSetId, setName, cancellationToken),
                ReportExportKind.ProjectIncome => await ProjectIncomeAsync(accountSetId, setName, cancellationToken),
                ReportExportKind.ExpenseDetail => await ExpenseDetailAsync(accountSetId, setName, cancellationToken),
                ReportExportKind.EquityChange => await EquityChangeAsync(accountSetId, setName, cancellationToken),
                ReportExportKind.TaxStatistics => await TaxAsync(accountSetId, setName, cancellationToken),
                _ => await FinancialSummaryAsync(accountSetId, setName, cancellationToken),
            };
            return book;
        }

        private FlatWorkbook Wrap(string reportName, string periodText, string setName, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<object?>> rows, string? note = null)
        {
            var info = new List<(string Label, string Value)>
            {
                ("账套", setName),
                ("报表", reportName),
                ("期间", periodText),
            };
            if (!string.IsNullOrWhiteSpace(note))
            {
                info.Add(("说明", note!));
            }

            return new FlatWorkbook(
                "报表信息",
                info,
                reportName,
                columns,
                rows,
                $"{Sanitize(reportName)}_{Sanitize(setName)}_{Sanitize(periodText)}.xlsx");
        }

        private async Task<FlatWorkbook> TrialBalanceAsync(Guid accountSetId, string setName, CancellationToken ct)
        {
            var dto = await _trialBalanceService.GetAsync(accountSetId, null, null, ct);
            var columns = new[] { "科目编码", "科目名称", "期初借方", "期初贷方", "本期借方发生", "本期贷方发生", "期末借方", "期末贷方" };
            var rows = new List<IReadOnlyList<object?>>();
            decimal[] totals = new decimal[6];
            foreach (var r in dto.Rows ?? [])
            {
                rows.Add(new object?[] { r.Code, r.Name, r.OpeningDebit, r.OpeningCredit, r.PeriodDebit, r.PeriodCredit, r.EndingDebit, r.EndingCredit });
                totals[0] += r.OpeningDebit; totals[1] += r.OpeningCredit;
                totals[2] += r.PeriodDebit; totals[3] += r.PeriodCredit;
                totals[4] += r.EndingDebit; totals[5] += r.EndingCredit;
            }

            rows.Add(new object?[] { "", "合计", totals[0], totals[1], totals[2], totals[3], totals[4], totals[5] });
            return Wrap("科目余额表", dto.PeriodText ?? "", setName, columns, rows, $"{dto.StartDate} 至 {dto.EndDate}，仅统计已过账凭证");
        }

        private async Task<FlatWorkbook> BalanceSheetAsync(Guid accountSetId, string setName, CancellationToken ct)
        {
            var dto = await _balanceSheetService.GetAsync(accountSetId, null, null, ct);
            var columns = new[] { "行次", "项目", "期末余额", "年初余额" };
            var rows = new List<IReadOnlyList<object?>>();
            decimal assetEnd = 0, assetOpen = 0, liabEnd = 0, liabOpen = 0, eqEnd = 0, eqOpen = 0;
            foreach (var l in dto.Lines ?? [])
            {
                var name = BalanceSheetService.LineName(l.Line);
                rows.Add(new object?[] { l.Line, name, l.Ending, l.Opening });
                if (l.Line is >= 1 and <= 16)
                {
                    assetEnd += l.Ending; assetOpen += l.Opening;
                }
                else if (l.Line is >= 31 and <= 44)
                {
                    liabEnd += l.Ending; liabOpen += l.Opening;
                }
                else
                {
                    eqEnd += l.Ending; eqOpen += l.Opening;
                }
            }

            rows.Add(new object?[] { "", "资产总计", assetEnd, assetOpen });
            rows.Add(new object?[] { "", "负债合计", liabEnd, liabOpen });
            rows.Add(new object?[] { "", "所有者权益合计", eqEnd, eqOpen });
            rows.Add(new object?[] { "", "负债和所有者权益总计", liabEnd + eqEnd, liabOpen + eqOpen });
            return Wrap("资产负债表", dto.EndingDate ?? "", setName, columns, rows, "金额已按报表方向净额列示；未结转损益并入未分配利润");
        }

        private async Task<FlatWorkbook> IncomeAsync(Guid accountSetId, string setName, bool quarter, CancellationToken ct)
        {
            if (quarter)
            {
                var q = await _incomeStatementService.GetQuarterAsync(accountSetId, null, null, ct);
                var columns = new[] { "行次", "项目", "本季金额", "本年累计金额" };
                var rows = ProjectIncomeLines(q.Lines, curTitle: null);
                return Wrap("利润表季报", q.PeriodText ?? "", setName, columns, rows, "明细行次取自凭证发生额；营业利润/利润总额/净利润等合计行未含，需按行次公式汇总");
            }

            var dto = await _incomeStatementService.GetAsync(accountSetId, null, null, ct);
            var cols = new[] { "行次", "项目", "本期金额", "本年累计金额" };
            var body = ProjectIncomeLines(dto.Lines, curTitle: null);
            return Wrap("利润表", dto.PeriodText ?? "", setName, cols, body, "明细行次取自凭证发生额；营业利润/利润总额/净利润等合计行未含，需按行次公式汇总");
        }

        private static List<IReadOnlyList<object?>> ProjectIncomeLines(
            IReadOnlyList<IncomeStatementLineDto>? lines, string? curTitle)
        {
            var rows = new List<IReadOnlyList<object?>>();
            foreach (var l in lines ?? [])
            {
                rows.Add(new object?[] { l.Line, IncomeLineMapper.LineName(l.Line), l.Current, l.YearToDate });
            }

            return rows;
        }

        private async Task<FlatWorkbook> CashFlowAsync(Guid accountSetId, string setName, bool quarter, CancellationToken ct)
        {
            var columns = new[] { "行次", "项目", (quarter ? "本季金额" : "本期金额"), "本年累计金额" };
            List<IReadOnlyList<object?>> rows = new();
            string period;
            if (quarter)
            {
                var dto = await _cashFlowService.GetQuarterAsync(accountSetId, null, null, ct);
                period = dto.PeriodText ?? "";
                foreach (var l in dto.Lines ?? [])
                {
                    rows.Add(new object?[] { l.Line, l.Name, l.Current, l.YearToDate });
                }
            }
            else
            {
                var dto = await _cashFlowService.GetPeriodAsync(accountSetId, null, null, ct);
                period = dto.PeriodText ?? "";
                foreach (var l in dto.Lines ?? [])
                {
                    rows.Add(new object?[] { l.Line, l.Name, l.Current, l.YearToDate });
                }
            }

            return Wrap(quarter ? "现金流量表季报" : "现金流量表", period, setName, columns, rows, "直接法简化工作底稿口径；现金科目=1001/1002/1012");
        }

        private async Task<FlatWorkbook> DepartmentIncomeAsync(Guid accountSetId, string setName, CancellationToken ct)
        {
            var dto = await _assistIncomeService.GetByDepartmentAsync(accountSetId, null, null, ct);
            var departments = dto.Departments ?? [];
            var columns = new List<string> { "项目", "行次" };
            foreach (var d in departments)
            {
                columns.Add($"{d.Name}-本期金额");
                columns.Add($"{d.Name}-本年累计金额");
            }

            var rows = new List<IReadOnlyList<object?>>();
            foreach (var r in dto.Rows ?? [])
            {
                var row = new List<object?> { r.Item, r.Line };
                for (var i = 0; i < departments.Count; i++)
                {
                    row.Add(r.Amounts?.Count > i ? r.Amounts[i].Current : null);
                    row.Add(r.Amounts?.Count > i ? r.Amounts[i].YearToDate : null);
                }

                rows.Add(row);
            }

            return Wrap("部门利润表", dto.PeriodText ?? "", setName, columns, rows, "仅列示本年有损益发生的部门");
        }

        private async Task<FlatWorkbook> ProjectIncomeAsync(Guid accountSetId, string setName, CancellationToken ct)
        {
            var dto = await _assistIncomeService.GetByProjectAsync(accountSetId, null, null, ct);
            var columns = new[] { "项目", "行次", "本期利润", "本年累计利润" };
            var rows = (dto.Rows ?? []).Select(r => (IReadOnlyList<object?>)new object?[] { r.Name, r.Line, r.Current, r.YearToDate }).ToList();
            return Wrap("项目利润表", dto.PeriodText ?? "", setName, columns, rows, "利润=收入-成本费用，按分录辅助核算「项目」归集");
        }

        private async Task<FlatWorkbook> ExpenseDetailAsync(Guid accountSetId, string setName, CancellationToken ct)
        {
            var dto = await _expenseDetailService.GetAsync(accountSetId, null, null, ct);
            var columns = new[] { "科目编码", "科目名称", "本期金额", "合计（本年累计）" };
            var rows = new List<IReadOnlyList<object?>>();
            foreach (var r in dto.Rows ?? [])
            {
                rows.Add(new object?[] { r.Code, r.Name, r.Current, r.Total });
            }

            var top = (dto.Rows ?? []).Where(r => r.Level == 1).ToList();
            rows.Add(new object?[] { "", "总计", top.Sum(r => r.Current), top.Sum(r => r.Total) });
            return Wrap("费用明细表", dto.PeriodText ?? "", setName, columns, rows, "成本类与损益类（借方向）科目；非叶级次由下级汇总");
        }

        private async Task<FlatWorkbook> EquityChangeAsync(Guid accountSetId, string setName, CancellationToken ct)
        {
            var dto = await _equityChangeService.GetAsync(accountSetId, null, ct);
            var columns = new[] { "项目", "上年期初", "上年期末", "本年期初", "本年期末" };
            var rows = (dto.Rows ?? []).Select(r => (IReadOnlyList<object?>)new object?[] { r.Item, r.PriorBegin, r.PriorEnd, r.CurrentBegin, r.CurrentEnd }).ToList();
            return Wrap("所有者权益变动表", dto.PeriodText ?? "", setName, columns, rows, "贷方为正；未结转损益并入未分配利润");
        }

        private async Task<FlatWorkbook> TaxAsync(Guid accountSetId, string setName, CancellationToken ct)
        {
            var dto = await _taxStatisticsService.GetAsync(accountSetId, null, null, ct);
            var columns = new[] { "科目编码", "税费项目", "本期计提净额", "本期已缴", "本期同比", "累计计提净额", "累计已缴", "累计同比" };
            var rows = (dto.Rows ?? [])
                .Select(r => (IReadOnlyList<object?>)new object?[]
                {
                    r.Code, r.Name, r.CurrentAmount, r.CurrentPaidAmount, FormatPct(r.CurrentYoyPercent),
                    r.YearToDateAmount, r.YearToDatePaidAmount, FormatPct(r.YearToDateYoyPercent),
                })
                .ToList();
            return Wrap("纳税统计表", dto.PeriodText ?? "", setName, columns, rows, "应交税费类科目；计提净额=贷方发生-借方缴纳；同比相对上年同期");
        }

        private async Task<FlatWorkbook> FinancialSummaryAsync(Guid accountSetId, string setName, CancellationToken ct)
        {
            var dto = await _financialSummaryService.GetAsync(accountSetId, null, null, ct);
            var columns = new[] { "项目", "本年累计", "累计同比", "本期", "本期同比", "本期环比" };
            var rows = (dto.Rows ?? [])
                .Select(r => (IReadOnlyList<object?>)new object?[]
                {
                    r.Item, r.YearToDateAmount, FormatPct(r.YearToDateYoyPercent),
                    r.CurrentAmount, FormatPct(r.CurrentYoyPercent), FormatPct(r.CurrentMomPercent),
                })
                .ToList();
            return Wrap("财务概要信息", dto.PeriodText ?? "", setName, columns, rows, "存量指标取期末余额，流量指标取发生额");
        }

        private static string FormatPct(decimal? value)
        {
            return value == null ? string.Empty : $"{Math.Round(value.Value, 2).ToString("0.##")}%";
        }

        private static string Sanitize(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }
    }
}
