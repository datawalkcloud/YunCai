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
        private readonly IncomeStatementService _incomeStatementService;
        private readonly CashFlowService _cashFlowService;
        private readonly CashFlowSupplementService _cashFlowSupplementService;
        private readonly AssistIncomeService _assistIncomeService;
        private readonly ExpenseDetailService _expenseDetailService;
        private readonly EquityChangeService _equityChangeService;
        private readonly TaxStatisticsService _taxStatisticsService;
        private readonly FinancialSummaryService _financialSummaryService;

        public ReportExportService(
            IAccountSetRepository accountSetRepository,
            TrialBalanceService trialBalanceService,
            IncomeStatementService incomeStatementService,
            CashFlowService cashFlowService,
            CashFlowSupplementService cashFlowSupplementService,
            AssistIncomeService assistIncomeService,
            ExpenseDetailService expenseDetailService,
            EquityChangeService equityChangeService,
            TaxStatisticsService taxStatisticsService,
            FinancialSummaryService financialSummaryService)
        {
            _accountSetRepository = accountSetRepository;
            _trialBalanceService = trialBalanceService;
            _incomeStatementService = incomeStatementService;
            _cashFlowService = cashFlowService;
            _cashFlowSupplementService = cashFlowSupplementService;
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
                ReportExportKind.TrialBalance => await TrialBalanceAsync(accountSetId, setName, year, month, cancellationToken),
                // 资产负债表按账套准则导出两栏 .xls，由 BalanceSheetXlsExportService 处理。
                ReportExportKind.BalanceSheet => throw new ReportException("资产负债表导出使用专用 .xls 版式。"),
                ReportExportKind.IncomeStatement => await IncomeAsync(accountSetId, setName, false, year, month, quarter, accountSet.Standard, cancellationToken),
                ReportExportKind.IncomeQuarter => await IncomeAsync(accountSetId, setName, true, year, month, quarter, accountSet.Standard, cancellationToken),
                ReportExportKind.CashFlow => await CashFlowAsync(accountSetId, setName, false, year, month, quarter, accountSet.Standard, cancellationToken),
                ReportExportKind.CashFlowQuarter => await CashFlowAsync(accountSetId, setName, true, year, month, quarter, accountSet.Standard, cancellationToken),
                ReportExportKind.DepartmentIncome => await DepartmentIncomeAsync(accountSetId, setName, year, month, accountSet.Standard, cancellationToken),
                ReportExportKind.ProjectIncome => await ProjectIncomeAsync(accountSetId, setName, year, month, accountSet.Standard, cancellationToken),
                ReportExportKind.ExpenseDetail => await ExpenseDetailAsync(accountSetId, setName, year, month, cancellationToken),
                ReportExportKind.EquityChange => await EquityChangeAsync(accountSetId, setName, year, accountSet.Standard, cancellationToken),
                ReportExportKind.TaxStatistics => await TaxAsync(accountSetId, setName, year, month, cancellationToken),
                _ => await FinancialSummaryAsync(accountSetId, setName, year, month, cancellationToken),
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

        private async Task<FlatWorkbook> TrialBalanceAsync(Guid accountSetId, string setName, int? year, int? month, CancellationToken ct)
        {
            // 试算平衡表按日期区间取数，导出侧把年/月换算成当月首尾日。
            var start = year is { } y && month is { } m ? new DateOnly(y, m, 1) : (DateOnly?)null;
            var dto = await _trialBalanceService.GetAsync(accountSetId, start, start?.AddMonths(1).AddDays(-1), ct);
            var columns = new[] { "科目编码", "科目名称", "期初借方", "期初贷方", "本期借方发生", "本期贷方发生", "期末借方", "期末贷方" };
            var rows = new List<IReadOnlyList<object?>>();
            decimal[] totals = new decimal[6];
            foreach (var r in dto.Rows ?? [])
            {
                rows.Add(new object?[] { r.Code, r.Name, r.OpeningDebit, r.OpeningCredit, r.PeriodDebit, r.PeriodCredit, r.EndingDebit, r.EndingCredit });
                // 上级行已含下级合计，合计只累加一级科目，否则会重复计入。
                if (r.ParentCode is not null)
                {
                    continue;
                }

                totals[0] += r.OpeningDebit; totals[1] += r.OpeningCredit;
                totals[2] += r.PeriodDebit; totals[3] += r.PeriodCredit;
                totals[4] += r.EndingDebit; totals[5] += r.EndingCredit;
            }

            rows.Add(new object?[] { "", "合计", totals[0], totals[1], totals[2], totals[3], totals[4], totals[5] });
            return Wrap("科目余额表", dto.PeriodText ?? "", setName, columns, rows, $"{dto.StartDate} 至 {dto.EndDate}，仅统计正常凭证");
        }

        private async Task<FlatWorkbook> IncomeAsync(Guid accountSetId, string setName, bool quarter, int? year, int? month, int? quarterNo, AccountingStandard standard, CancellationToken ct)
        {
            if (quarter)
            {
                var q = await _incomeStatementService.GetQuarterAsync(accountSetId, year, quarterNo, ct);
                var columns = new[] { "行次", "项目", "本季金额", "本年累计金额" };
                var rows = ProjectIncomeLines(q.Lines, standard);
                var note = standard == AccountingStandard.SmallBusiness
                    ? "小企业会计准则（会小企02表）版式；合计行由系统按行次公式汇总"
                    : "明细行次取自凭证发生额；营业利润/利润总额/净利润等合计行未含，需按行次公式汇总";
                return Wrap("利润表季报", q.PeriodText ?? "", setName, columns, rows, note);
            }

            var dto = await _incomeStatementService.GetAsync(accountSetId, year, month, ct);
            var cols = new[] { "行次", "项目", "本期金额", "本年累计金额" };
            var body = ProjectIncomeLines(dto.Lines, standard);
            var note2 = standard == AccountingStandard.SmallBusiness
                ? "小企业会计准则（会小企02表）版式；合计行由系统按行次公式汇总"
                : "明细行次取自凭证发生额；营业利润/利润总额/净利润等合计行未含，需按行次公式汇总";
            return Wrap("利润表", dto.PeriodText ?? "", setName, cols, body, note2);
        }

        private static List<IReadOnlyList<object?>> ProjectIncomeLines(
            IReadOnlyList<IncomeStatementLineDto>? lines, AccountingStandard standard)
        {
            // 小企业会计准则（会小企02表）：只列营业收入/成本/税金及附加/三项费用/投资收益/营业外收支/所得税/净利润，
            // 不含「其中」子项、资产减值损失、公允价值变动收益、其他综合收益与每股收益；合计行由行次公式现算。
            if (standard == AccountingStandard.SmallBusiness)
            {
                var map = (lines ?? []).GroupBy(l => l.Line)
                    .ToDictionary(g => g.Key, g => (Current: g.Sum(l => l.Current), YearToDate: g.Sum(l => l.YearToDate)));
                decimal Of(int k) => map.TryGetValue(k, out var v) ? v.Current : 0m;
                decimal Ytd(int k) => map.TryGetValue(k, out var v) ? v.YearToDate : 0m;
                decimal Op() => Of(1) - Of(2) - Of(3) - Of(4) - Of(5) - Of(6) + Of(9);
                decimal OpYtd() => Ytd(1) - Ytd(2) - Ytd(3) - Ytd(4) - Ytd(5) - Ytd(6) + Ytd(9);
                decimal Profit() => Op() + Of(12) - Of(14);
                decimal ProfitYtd() => OpYtd() + Ytd(12) - Ytd(14);
                decimal Net() => Profit() - Of(17);
                decimal NetYtd() => ProfitYtd() - Ytd(17);
                var rows = new List<IReadOnlyList<object?>>();
                void Row(int displayLine, string label, decimal cur, decimal ytd) => rows.Add(new object?[] { displayLine, label, cur, ytd });
                Row(1, "一、营业收入", Of(1), Ytd(1));
                Row(2, "减：营业成本", Of(2), Ytd(2));
                Row(3, "税金及附加", Of(3), Ytd(3));
                Row(4, "销售费用", Of(4), Ytd(4));
                Row(5, "管理费用", Of(5), Ytd(5));
                Row(6, "财务费用", Of(6), Ytd(6));
                Row(7, "加：投资收益（损失以“-”号填列）", Of(9), Ytd(9));
                Row(8, "二、营业利润（亏损以“-”号填列）", Op(), OpYtd());
                Row(9, "加：营业外收入", Of(12), Ytd(12));
                Row(10, "减：营业外支出", Of(14), Ytd(14));
                Row(11, "三、利润总额（亏损总额以“-”号填列）", Profit(), ProfitYtd());
                Row(12, "减：所得税费用", Of(17), Ytd(17));
                Row(13, "四、净利润（净亏损以“-”号填列）", Net(), NetYtd());
                return rows;
            }

            var raw = new List<IReadOnlyList<object?>>();
            foreach (var l in lines ?? [])
            {
                raw.Add(new object?[] { l.Line, IncomeLineMapper.LineName(l.Line), l.Current, l.YearToDate });
            }

            return raw;
        }

        private async Task<FlatWorkbook> CashFlowAsync(
            Guid accountSetId,
            string setName,
            bool quarter,
            int? year,
            int? month,
            int? quarterNo,
            AccountingStandard standard,
            CancellationToken ct)
        {
            var smallBusiness = standard == AccountingStandard.SmallBusiness;
            var columns = new[] { "行次", "项目", (quarter ? "本季金额" : "本期金额"), "本年累计金额" };
            List<IReadOnlyList<object?>> rows = new();
            string period;
            if (quarter)
            {
                var dto = await _cashFlowService.GetQuarterAsync(accountSetId, year, quarterNo, ct);
                period = dto.PeriodText ?? "";
                foreach (var l in dto.Lines ?? [])
                {
                    rows.Add(new object?[] { l.Line, l.Name, l.Current, l.YearToDate });
                }
            }
            else
            {
                var dto = await _cashFlowService.GetPeriodAsync(accountSetId, year, month, ct);
                period = dto.PeriodText ?? "";
                foreach (var l in dto.Lines ?? [])
                {
                    rows.Add(new object?[] { l.Line, l.Name, l.Current, l.YearToDate });
                }

                // 补充资料（间接法）只随一般企业月报导出；小企业会计准则（会小企03表）没有补充资料。
                if (!smallBusiness)
                {
                    var supplement = await _cashFlowSupplementService.GetPeriodAsync(accountSetId, year, month, ct);
                    rows.Add(new object?[] { null, "现金流量表补充资料", null, null });
                    foreach (var l in supplement.Lines ?? [])
                    {
                        rows.Add(new object?[] { l.Line, l.Name, l.Current, l.YearToDate });
                    }

                    rows.Add(new object?[]
                    {
                        null,
                        $"补充资料调节净额与主表经营活动净额差异：{(supplement.Difference):0.00}",
                        null,
                        null,
                    });
                }
            }

            var note = smallBusiness
                ? "小企业会计准则（会小企03表）版式；直接法简化工作底稿口径；现金科目=1001/1002/1012"
                : "直接法简化工作底稿口径；现金科目=1001/1002/1012；月报附补充资料（间接法）";
            return Wrap(quarter ? "现金流量表季报" : "现金流量表", period, setName, columns, rows, note);
        }

        /// <summary>部门/项目利润表小准则（会小企02表）行集：13 行简化版，合计行（营业利润/利润总额/净利润）逐维度按小准则公式现算，
        /// 与利润表主表、PDF 部门利润表保持一致。includeTotalColumn 用于项目利润表（多一列各维度求和）。</summary>
        private static List<IReadOnlyList<object?>> BuildSmallBusinessDimensionRows(
            IReadOnlyDictionary<int, IReadOnlyList<IncomeDimensionAmountDto>> byLine,
            int dimensionCount,
            bool includeTotalColumn)
        {
            decimal Dim(int key, int i, Func<IncomeDimensionAmountDto, decimal> pick)
                => byLine.TryGetValue(key, out var amts) && i < amts.Count ? pick(amts[i]) : 0m;
            decimal Op(int i, Func<IncomeDimensionAmountDto, decimal> p)
                => Dim(1, i, p) - Dim(2, i, p) - Dim(3, i, p) - Dim(4, i, p) - Dim(5, i, p) - Dim(6, i, p) + Dim(9, i, p);
            decimal Tot(int i, Func<IncomeDimensionAmountDto, decimal> p) => Op(i, p) + Dim(12, i, p) - Dim(14, i, p);
            decimal Net(int i, Func<IncomeDimensionAmountDto, decimal> p) => Tot(i, p) - Dim(17, i, p);

            // DisplayLine, DataLine, Label, TotalKind(0=明细, 1=营业利润, 2=利润总额, 3=净利润)
            var layout = new (int DisplayLine, int DataLine, string Label, int TotalKind)[]
            {
                (1, 1, "一、营业收入", 0),
                (2, 2, "减：营业成本", 0),
                (3, 3, "税金及附加", 0),
                (4, 4, "销售费用", 0),
                (5, 5, "管理费用", 0),
                (6, 6, "财务费用", 0),
                (7, 9, "加：投资收益（损失以“-”号填列）", 0),
                (8, 0, "二、营业利润（亏损以“-”号填列）", 1),
                (9, 12, "加：营业外收入", 0),
                (10, 14, "减：营业外支出", 0),
                (11, 0, "三、利润总额（亏损总额以“-”号填列）", 2),
                (12, 17, "减：所得税费用", 0),
                (13, 0, "四、净利润（净亏损以“-”号填列）", 3),
            };

            var rows = new List<IReadOnlyList<object?>>();
            foreach (var (displayLine, dataLine, label, totalKind) in layout)
            {
                var row = new List<object?> { label, displayLine };
                decimal curSum = 0m, ytdSum = 0m;
                for (var i = 0; i < dimensionCount; i++)
                {
                    decimal cur, ytd;
                    if (totalKind == 0)
                    {
                        var amts = byLine.GetValueOrDefault(dataLine);
                        cur = amts is not null && i < amts.Count ? amts[i].Current : 0m;
                        ytd = amts is not null && i < amts.Count ? amts[i].YearToDate : 0m;
                    }
                    else
                    {
                        cur = totalKind switch { 1 => Op(i, a => a.Current), 2 => Tot(i, a => a.Current), _ => Net(i, a => a.Current) };
                        ytd = totalKind switch { 1 => Op(i, a => a.YearToDate), 2 => Tot(i, a => a.YearToDate), _ => Net(i, a => a.YearToDate) };
                    }

                    row.Add(cur);
                    row.Add(ytd);
                    curSum += cur;
                    ytdSum += ytd;
                }

                if (includeTotalColumn)
                {
                    row.Add(curSum);
                    row.Add(ytdSum);
                }

                rows.Add(row);
            }

            return rows;
        }

        private async Task<FlatWorkbook> DepartmentIncomeAsync(Guid accountSetId, string setName, int? year, int? month, AccountingStandard standard, CancellationToken ct)
        {
            var dto = await _assistIncomeService.GetByDepartmentAsync(accountSetId, year, month, ct);
            var departments = dto.Departments ?? [];
            var columns = new List<string> { "项目", "行次" };
            foreach (var d in departments)
            {
                columns.Add($"{d.Name}-本期金额");
                columns.Add($"{d.Name}-本年累计金额");
            }

            List<IReadOnlyList<object?>> rows;
            if (standard == AccountingStandard.SmallBusiness)
            {
                rows = BuildSmallBusinessDimensionRows(
                    (dto.Rows ?? []).GroupBy(r => r.Line).ToDictionary(g => g.Key, g => g.First().Amounts ?? []),
                    departments.Count, includeTotalColumn: false);
            }
            else
            {
                rows = new List<IReadOnlyList<object?>>();
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
            }

            var note = standard == AccountingStandard.SmallBusiness
                ? "小企业会计准则（会小企02表）版式；合计行由系统按行次公式汇总"
                : "按部门辅助核算归集收入与费用，仅列示本年有损益发生的部门";
            return Wrap("部门利润表", dto.PeriodText ?? "", setName, columns, rows, note);
        }

        private async Task<FlatWorkbook> ProjectIncomeAsync(Guid accountSetId, string setName, int? year, int? month, AccountingStandard standard, CancellationToken ct)
        {
            var dto = await _assistIncomeService.GetByProjectAsync(accountSetId, year, month, ct);
            var projects = dto.Projects ?? [];
            var columns = new List<string> { "项目", "行次" };
            foreach (var p in projects)
            {
                columns.Add($"{p.Name}-本期金额");
                columns.Add($"{p.Name}-本年累计金额");
            }

            columns.Add("合计-本期金额");
            columns.Add("合计-本年累计金额");

            List<IReadOnlyList<object?>> rows;
            if (standard == AccountingStandard.SmallBusiness)
            {
                rows = BuildSmallBusinessDimensionRows(
                    (dto.Rows ?? []).GroupBy(r => r.Line).ToDictionary(g => g.Key, g => g.First().Amounts ?? []),
                    projects.Count, includeTotalColumn: true);
            }
            else
            {
                rows = new List<IReadOnlyList<object?>>();
                foreach (var r in dto.Rows ?? [])
                {
                    var row = new List<object?> { r.Item, r.Line };
                    for (var i = 0; i < projects.Count; i++)
                    {
                        row.Add(r.Amounts?.Count > i ? r.Amounts[i].Current : null);
                        row.Add(r.Amounts?.Count > i ? r.Amounts[i].YearToDate : null);
                    }

                    row.Add(r.Total?.Current);
                    row.Add(r.Total?.YearToDate);
                    rows.Add(row);
                }
            }

            var note = standard == AccountingStandard.SmallBusiness
                ? "小企业会计准则（会小企02表）版式；合计行由系统按行次公式汇总"
                : "按项目辅助核算归集收入与成本费用；各项目列=该项目的收入-成本费用，合计列为各项目求和";
            return Wrap("项目利润表", dto.PeriodText ?? "", setName, columns, rows, note);
        }

        private async Task<FlatWorkbook> ExpenseDetailAsync(Guid accountSetId, string setName, int? year, int? month, CancellationToken ct)
        {
            var dto = await _expenseDetailService.GetAsync(accountSetId, year, month, ct);
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

        private async Task<FlatWorkbook> EquityChangeAsync(Guid accountSetId, string setName, int? year, AccountingStandard standard, CancellationToken ct)
        {
            var dto = await _equityChangeService.GetAsync(accountSetId, year, ct);
            var columns = new[] { "项目", "上年期初", "上年期末", "本年期初", "本年期末" };
            var rows = (dto.Rows ?? []).Select(r => (IReadOnlyList<object?>)new object?[] { r.Item, r.PriorBegin, r.PriorEnd, r.CurrentBegin, r.CurrentEnd }).ToList();
            var note = standard == AccountingStandard.SmallBusiness
                ? "小企业会计准则不要求编制本表，按管理参考口径列示；会计政策变更与差错更正采用未来适用法，不追溯调整；贷方为正，未结转损益并入未分配利润"
                : "贷方为正；未结转损益并入未分配利润";
            return Wrap("所有者权益变动表", dto.PeriodText ?? "", setName, columns, rows, note);
        }

        private async Task<FlatWorkbook> TaxAsync(Guid accountSetId, string setName, int? year, int? month, CancellationToken ct)
        {
            var dto = await _taxStatisticsService.GetAsync(accountSetId, year, month, ct);
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

        private async Task<FlatWorkbook> FinancialSummaryAsync(Guid accountSetId, string setName, int? year, int? month, CancellationToken ct)
        {
            var dto = await _financialSummaryService.GetAsync(accountSetId, year, month, ct);
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
