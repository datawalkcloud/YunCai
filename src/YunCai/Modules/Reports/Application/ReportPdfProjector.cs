using System.Globalization;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Ledgers.Application;
using YunCai.Modules.Reports.Domain;
using YunCai.SharedKernel.Infrastructure.Pdf;

namespace YunCai.Modules.Reports.Application
{
    /// <summary>
    /// 报表打印 PDF 的表格投影：列名、分组表头、行次、缩进与合计行按各报表页面的屏幕表格 1:1 描述，
    /// 版式（A4、边框、跨页续表、页脚页码与打印时间）由 <see cref="PrintSheetPdfBuilder"/> 一处负责。
    /// 与 <see cref="ReportExportService"/>（压平成单行表头写 xlsx）取数口径相同，只是保留表头层级与标准行次模板。
    /// </summary>
    public static class ReportPdfProjector
    {
        /// <summary>页头与文件名需要的账套信息：报表 DTO 只带期间文本，编制单位与账套名由控制器注入；Standard 决定利润表版式。</summary>
        public sealed record PrintContext(string AccountSetName, AccountingStandard Standard, string? CompanyName = null);

        /// <summary>
        /// 投影为打印稿。<paramref name="page"/> 是与 <paramref name="kind"/> 对应的报表 DTO；
        /// <paramref name="supplement"/> 只给现金流量表月报（补充资料与主表同页打印）；
        /// <paramref name="options"/> 只给科目余额表（科目区间写进页头）；
        /// <paramref name="showLastYear"/> 对应页面上的「显示上年累计金额」开关。
        /// 未做打印版式的报表抛 <see cref="ReportException"/>，由控制器翻成 400。
        /// </summary>
        public static PrintSheet Project(
            ReportExportKind kind,
            object page,
            PrintContext context,
            CashFlowSupplementDto? supplement = null,
            LedgerQueryOptions? options = null,
            bool showLastYear = false) => kind switch
        {
            ReportExportKind.TrialBalance => TrialBalance((TrialBalanceDto)page, context, options),
            ReportExportKind.IncomeStatement => Income((IncomeStatementDto)page, context, showLastYear),
            ReportExportKind.IncomeQuarter => IncomeQuarter((IncomeStatementQuarterDto)page, context, showLastYear),
            ReportExportKind.DepartmentIncome => DepartmentIncome((DepartmentIncomeDto)page, context),
            ReportExportKind.CashFlow => CashFlow((CashFlowDto)page, context, supplement, showLastYear),
            ReportExportKind.CashFlowQuarter => CashFlowQuarter((CashFlowQuarterDto)page, context, showLastYear),
            ReportExportKind.ExpenseDetail => ExpenseDetail((ExpenseDetailDto)page, context),
            ReportExportKind.TaxStatistics => Tax((TaxStatisticsDto)page, context),
            ReportExportKind.FinancialSummary => FinancialSummary((FinancialSummaryDto)page, context),
            _ => throw new ReportException("该报表暂不支持打印。"),
        };

        /// <summary>文件名后缀：月报 yyyyMM、季报 yyyyQn、科目余额表为区间 yyyyMM-yyyyMM。</summary>
        public static string SuffixOf(object page) => page switch
        {
            TrialBalanceDto dto => $"{MonthOf(dto.StartDate)}-{MonthOf(dto.EndDate)}",
            IncomeStatementDto dto => $"{dto.Year:D4}{dto.Month:D2}",
            IncomeStatementQuarterDto dto => $"{dto.Year:D4}Q{dto.Quarter}",
            DepartmentIncomeDto dto => $"{dto.Year:D4}{dto.Month:D2}",
            CashFlowDto dto => $"{dto.Year:D4}{dto.Month:D2}",
            CashFlowQuarterDto dto => $"{dto.Year:D4}Q{dto.Quarter}",
            ExpenseDetailDto dto => $"{dto.Year:D4}{dto.Month:D2}",
            TaxStatisticsDto dto => $"{dto.Year:D4}{dto.Month:D2}",
            FinancialSummaryDto dto => $"{dto.Year:D4}{dto.Month:D2}",
            _ => string.Empty,
        };

        /* ---------- 科目余额表 ---------- */

        /// <summary>科目余额表：编码/名称 + 期初余额、本期发生额、期末余额三组借贷列，末行合计只累加一级科目。</summary>
        private static PrintSheet TrialBalance(TrialBalanceDto dto, PrintContext context, LedgerQueryOptions? options)
        {
            var columns = new List<PrintColumn>
            {
                new("科目编码", Width: "13%", Align: PdfAlign.Center),
                new("科目名称", Width: "21%"),
                new("借方", "期初余额", "11%", PdfAlign.Right),
                new("贷方", "期初余额", "11%", PdfAlign.Right),
                new("借方", "本期发生额", "11%", PdfAlign.Right),
                new("贷方", "本期发生额", "11%", PdfAlign.Right),
                new("借方", "期末余额", "11%", PdfAlign.Right),
                new("贷方", "期末余额", "11%", PdfAlign.Right),
            };

            var rows = new List<PrintRow>();
            decimal openingDebit = 0m;
            decimal openingCredit = 0m;
            decimal periodDebit = 0m;
            decimal periodCredit = 0m;
            decimal endingDebit = 0m;
            decimal endingCredit = 0m;
            foreach (var row in dto.Rows ?? [])
            {
                // 上级行已含下级合计，合计只累加一级科目，否则会重复计入（与页面表尾同口径）。
                if (row.ParentCode is null)
                {
                    openingDebit += row.OpeningDebit;
                    openingCredit += row.OpeningCredit;
                    periodDebit += row.PeriodDebit;
                    periodCredit += row.PeriodCredit;
                    endingDebit += row.EndingDebit;
                    endingCredit += row.EndingCredit;
                }

                rows.Add(new PrintRow(
                [
                    new PrintCell(row.Code, Align: PdfAlign.Center),
                    new PrintCell(row.Name, Css: IndentOfLevel(row.Level)),
                    new PrintCell(row.OpeningDebit),
                    new PrintCell(row.OpeningCredit),
                    new PrintCell(row.PeriodDebit),
                    new PrintCell(row.PeriodCredit),
                    new PrintCell(row.EndingDebit),
                    new PrintCell(row.EndingCredit),
                ]));
            }

            rows.Add(new PrintRow(
            [
                new PrintCell("合计", ColSpan: 2, Css: PdfCss.Strong),
                new PrintCell(PdfText.AmountAlways(openingDebit)),
                new PrintCell(PdfText.AmountAlways(openingCredit)),
                new PrintCell(PdfText.AmountAlways(periodDebit)),
                new PrintCell(PdfText.AmountAlways(periodCredit)),
                new PrintCell(PdfText.AmountAlways(endingDebit)),
                new PrintCell(PdfText.AmountAlways(endingCredit)),
            ], PdfCss.Strong));

            var balanced = SameMoney(openingDebit, openingCredit)
                           && SameMoney(periodDebit, periodCredit)
                           && SameMoney(endingDebit, endingCredit);

            return Sheet(
                "科目余额表",
                context,
                dto.PeriodText,
                columns,
                rows,
                subject: SubjectOf(options),
                notes:
                [
                    balanced ? "试算平衡：借方合计 = 贷方合计。" : "试算不平衡，请核对数据。",
                    "仅统计正常凭证。",
                ]);
        }

        /* ---------- 利润表（月报 / 季报） ---------- */

        /// <summary>利润表合计行类型：决定行次公式的取值种类。</summary>
        private enum IncomeTotalKind { None, Operating, Profit, Net, Comprehensive }

        /// <summary>企业会计准则（会企02表）行模板：行次 1-32，名称与缩进层级与页面一致。DataLine 为后端行次（取数键），DisplayLine 为打印行次。</summary>
        private static readonly (int DataLine, int DisplayLine, string Label, int Indent, IncomeTotalKind Total)[] IncomeTemplateEnterprise =
        [
            (1, 1, "一、营业收入", 0, IncomeTotalKind.None),
            (2, 2, "减：营业成本", 1, IncomeTotalKind.None),
            (3, 3, "税金及附加", 1, IncomeTotalKind.None),
            (4, 4, "销售费用", 1, IncomeTotalKind.None),
            (5, 5, "管理费用", 1, IncomeTotalKind.None),
            (6, 6, "财务费用", 1, IncomeTotalKind.None),
            (33, 33, "其中：利息费用（收入以“-”号填列）", 2, IncomeTotalKind.None),
            (7, 7, "资产减值损失", 1, IncomeTotalKind.None),
            (8, 8, "加：公允价值变动收益（损失以“－”号填列）", 1, IncomeTotalKind.None),
            (9, 9, "投资收益（损失以“－”号填列）", 1, IncomeTotalKind.None),
            (10, 10, "其中：对联营企业和合营企业的投资收益", 2, IncomeTotalKind.None),
            (11, 11, "二、营业利润（亏损以“－”号填列）", 0, IncomeTotalKind.Operating),
            (12, 12, "加：营业外收入", 1, IncomeTotalKind.None),
            (34, 34, "其中：政府补助", 2, IncomeTotalKind.None),
            (13, 13, "其中：非流动资产处置利得", 2, IncomeTotalKind.None),
            (14, 14, "减：营业外支出", 1, IncomeTotalKind.None),
            (15, 15, "其中：非流动资产处置损失", 2, IncomeTotalKind.None),
            (16, 16, "三、利润总额（亏损总额以“－”号填列）", 0, IncomeTotalKind.Profit),
            (17, 17, "减：所得税费用", 1, IncomeTotalKind.None),
            (18, 18, "四、净利润（净亏损以“－”号填列）", 0, IncomeTotalKind.Net),
            (19, 19, "五、其他综合收益的税后净额", 0, IncomeTotalKind.None),
            (20, 20, "(一)以后不能重分类进损益的其他综合收益", 1, IncomeTotalKind.None),
            (21, 21, "1.重新计量设定受益计划净负债或净资产的变动", 2, IncomeTotalKind.None),
            (22, 22, "2.权益法下在被投资单位不能重分类进损益的其他综合收益中享有的份额", 2, IncomeTotalKind.None),
            (23, 23, "(二)以后将重分类进损益的其他综合收益", 1, IncomeTotalKind.None),
            (24, 24, "1.权益法下在被投资单位以后将重分类进损益的其他综合收益中享有的份额", 2, IncomeTotalKind.None),
            (25, 25, "2.可供出售金融资产公允价值变动损益", 2, IncomeTotalKind.None),
            (26, 26, "3.持有至到期投资重分类可供出售金融资产损益", 2, IncomeTotalKind.None),
            (27, 27, "4.现金流量套期损益的有效部分", 2, IncomeTotalKind.None),
            (28, 28, "5.外币财务报表折算差额", 2, IncomeTotalKind.None),
            (29, 29, "六、综合收益总额", 0, IncomeTotalKind.Comprehensive),
            (30, 30, "七、每股收益：", 0, IncomeTotalKind.None),
            (31, 31, "(一) 基本每股收益", 1, IncomeTotalKind.None),
            (32, 32, "(二) 稀释每股收益", 1, IncomeTotalKind.None),
        ];

        /// <summary>小企业会计准则（会小企02表）行模板：仅营业收入/成本/税金及附加/三项费用/投资收益/营业外收支/所得税/净利润；
        /// 不含「其中」子项、资产减值损失、公允价值变动收益、其他综合收益与每股收益。DisplayLine 为小企业准则行次（1-13）。</summary>
        private static readonly (int DataLine, int DisplayLine, string Label, int Indent, IncomeTotalKind Total)[] IncomeTemplateSmallBusiness =
        [
            (1, 1, "一、营业收入", 0, IncomeTotalKind.None),
            (2, 2, "减：营业成本", 1, IncomeTotalKind.None),
            (3, 3, "税金及附加", 1, IncomeTotalKind.None),
            (4, 4, "销售费用", 1, IncomeTotalKind.None),
            (5, 5, "管理费用", 1, IncomeTotalKind.None),
            (6, 6, "财务费用", 1, IncomeTotalKind.None),
            (9, 7, "加：投资收益（损失以“-”号填列）", 1, IncomeTotalKind.None),
            (0, 8, "二、营业利润（亏损以“-”号填列）", 0, IncomeTotalKind.Operating),
            (12, 9, "加：营业外收入", 1, IncomeTotalKind.None),
            (14, 10, "减：营业外支出", 1, IncomeTotalKind.None),
            (0, 11, "三、利润总额（亏损总额以“-”号填列）", 0, IncomeTotalKind.Profit),
            (17, 12, "减：所得税费用", 1, IncomeTotalKind.None),
            (0, 13, "四、净利润（净亏损以“-”号填列）", 0, IncomeTotalKind.Net),
        ];

        private static PrintSheet Income(IncomeStatementDto dto, PrintContext context, bool showLastYear)
            => IncomeSheet("利润表", "本期金额", dto.PeriodText, MapOf(dto.Lines), context, showLastYear);

        private static PrintSheet IncomeQuarter(IncomeStatementQuarterDto dto, PrintContext context, bool showLastYear)
            => IncomeSheet("利润表季报", "本季金额", dto.PeriodText, MapOf(dto.Lines), context, showLastYear);

        private static PrintSheet IncomeSheet(
            string title,
            string currentTitle,
            string? periodText,
            IReadOnlyDictionary<int, LineAmount> map,
            PrintContext context,
            bool showLastYear)
        {
            var columns = ReportColumns(currentTitle, showLastYear, withFormula: false);
            var template = context.Standard == AccountingStandard.SmallBusiness ? IncomeTemplateSmallBusiness : IncomeTemplateEnterprise;
            var rows = new List<PrintRow>();
            foreach (var (dataLine, displayLine, label, indent, total) in template)
            {
                var isTotal = total != IncomeTotalKind.None;
                rows.Add(new PrintRow(
                    IncomeCells(map, dataLine, displayLine, label, indent, total, context.Standard, showLastYear),
                    isTotal ? PdfCss.Strong : string.Empty));
            }

            var subtitle = context.Standard == AccountingStandard.SmallBusiness ? "会小企02表" : "会企02表";
            return Sheet(title, context, periodText, columns, rows, subtitle,
                notes: ["明细行次取自正常凭证发生额；结转损益凭证不计入。"]);
        }

        private static List<PrintCell> IncomeCells(
            IReadOnlyDictionary<int, LineAmount> map,
            int dataLine,
            int displayLine,
            string label,
            int indent,
            IncomeTotalKind total,
            AccountingStandard standard,
            bool showLastYear)
        {
            decimal Of(Func<LineAmount, decimal> pick)
                => total == IncomeTotalKind.None
                    ? (map.TryGetValue(dataLine, out var hit) ? pick(hit) : 0m)
                    : IncomeTotal(key => map.TryGetValue(key, out var hit) ? pick(hit) : 0m, total, standard);

            return ReportCells(label, displayLine, indent, Of(a => a.YearToDate), Of(a => a.LastYear), Of(a => a.Current), showLastYear, total != IncomeTotalKind.None);
        }

        /// <summary>营业利润 / 利润总额 / 净利润 / 综合收益总额的行次公式：减项行取负，「其中」行不参与；小企业准则不含资产减值损失与公允价值变动收益。
        /// valueOf(key) 返回已取好口径（本期/累计/上年）的某行次金额，由调用方按会计标准提供正确行次数据，便于部门利润表逐维度复用同一公式。</summary>
        private static decimal IncomeTotal(
            Func<int, decimal> valueOf,
            IncomeTotalKind total,
            AccountingStandard standard)
        {
            decimal Of(int key) => valueOf(key);
            // 小企业会计准则：营业利润 = 营业收入 − 成本 − 税金及附加 − 销售费用 − 管理费用 − 财务费用 + 投资收益。
            if (standard == AccountingStandard.SmallBusiness)
            {
                decimal Operating() => Of(1) - Of(2) - Of(3) - Of(4) - Of(5) - Of(6) + Of(9);
                decimal Profit() => Operating() + Of(12) - Of(14);
                decimal Net() => Profit() - Of(17);
                return total switch
                {
                    IncomeTotalKind.Operating => Operating(),
                    IncomeTotalKind.Profit => Profit(),
                    IncomeTotalKind.Net => Net(),
                    _ => 0m,
                };
            }

            decimal NetE() => Of(1) + Of(8) + Of(9) - Of(2) - Of(3) - Of(4) - Of(5) - Of(6) - Of(7);
            decimal TotalE() => NetE() + Of(12) - Of(14);
            decimal ProfitE() => TotalE() - Of(17);
            return total switch
            {
                IncomeTotalKind.Operating => NetE(),
                IncomeTotalKind.Profit => TotalE(),
                IncomeTotalKind.Net => ProfitE(),
                IncomeTotalKind.Comprehensive => ProfitE() + Of(19),
                _ => 0m,
            };
        }

        /* ---------- 部门利润表 ---------- */

        /// <summary>
        /// 部门利润表：项目/行次 + 每个部门两列（本期、本年累计），行模板与「利润表」一致；
        /// 营业利润/利润总额/净利润三个合计行由后端按部门现算，部门多时转横向。
        /// </summary>
        private static PrintSheet DepartmentIncome(DepartmentIncomeDto dto, PrintContext context)
        {
            var departments = dto.Departments ?? [];
            // 部门多时列宽不够，转横向；两个部门以内纵向即可一页放下整张 32 行模板。
            var landscape = departments.Count >= 3;
            // 百分比列宽必须是整数：渲染器遇到小数百分比会退回按内容自动分配，金额列被压窄折行。
            var amountWidth = departments.Count switch
            {
                <= 1 => 22,
                2 => 13,
                3 => 10,
                4 => 8,
                _ => 7,
            };
            var labelWidth = Math.Max(10, 94 - amountWidth * departments.Count * 2);
            var columns = new List<PrintColumn>
            {
                new("项目", Width: $"{labelWidth}%"),
                // 表头不折行：列宽偏窄时「行次」会被拆成两行。
                new("行次", Width: "6%", Align: PdfAlign.Center, Css: PdfCss.NoWrap),
            };

            foreach (var department in departments)
            {
                columns.Add(new PrintColumn("本期金额", department.Name, $"{amountWidth}%", PdfAlign.Right));
                columns.Add(new PrintColumn("本年累计金额", department.Name, $"{amountWidth}%", PdfAlign.Right));
            }

            var byLine = (dto.Rows ?? [])
                .GroupBy(r => r.Line)
                .ToDictionary(g => g.Key, g => g.First().Amounts ?? []);

            // 明细行直接按行次取数；合计行（营业利润/利润总额/净利润）按与「利润表」相同的公式逐部门计算，
            // 保证部门利润表合计列与主表一致，并随账套会计标准切换版式。
            decimal DimOf(int key, int i, Func<IncomeDimensionAmountDto, decimal> pick)
                => byLine.TryGetValue(key, out var amts) && i < amts.Count ? pick(amts[i]) : 0m;

            var isSmall = context.Standard == AccountingStandard.SmallBusiness;
            var template = isSmall ? IncomeTemplateSmallBusiness : IncomeTemplateEnterprise;
            var subtitle = isSmall ? "会小企02表" : "会企02表";

            var rows = new List<PrintRow>();
            if (departments.Count > 0)
            {
                foreach (var (dataLine, displayLine, label, indent, total) in template)
                {
                    var isTotal = total != IncomeTotalKind.None;
                    var cells = new List<PrintCell>
                    {
                        new(label, Css: isTotal ? PdfCss.Strong : IndentOfClass(indent)),
                        new(displayLine.ToString(), Align: PdfAlign.Center),
                    };
                    for (var i = 0; i < departments.Count; i++)
                    {
                        IncomeDimensionAmountDto? amount = null;
                        if (isTotal)
                        {
                            // 合计行按主表公式逐部门计算，dataLine 不参与取数。
                            amount = new IncomeDimensionAmountDto
                            {
                                Current = IncomeTotal(key => DimOf(key, i, a => a.Current), total, context.Standard),
                                YearToDate = IncomeTotal(key => DimOf(key, i, a => a.YearToDate), total, context.Standard),
                            };
                        }
                        else if (byLine.TryGetValue(dataLine, out var amts) && i < amts.Count)
                        {
                            amount = amts[i];
                        }

                        // 合计行 0 也印 0.00，明细行按账页惯例留白；金额不折行，列宽随数字撑开。
                        cells.Add(isTotal
                            ? new PrintCell(PdfText.AmountAlways(amount?.Current), Css: PdfCss.NoWrap)
                            : new PrintCell(amount?.Current, Css: PdfCss.NoWrap));
                        cells.Add(isTotal
                            ? new PrintCell(PdfText.AmountAlways(amount?.YearToDate), Css: PdfCss.NoWrap)
                            : new PrintCell(amount?.YearToDate, Css: PdfCss.NoWrap));
                    }

                    rows.Add(new PrintRow(cells, isTotal ? PdfCss.Strong : string.Empty));
                }
            }

            return Sheet("部门利润表", context, dto.PeriodText, columns, rows, subtitle,
                notes:
                [
                    "按部门辅助核算归集收入与费用，仅列示本年有损益发生的部门。",
                    "明细行次取自正常凭证发生额；结转损益凭证不计入。",
                ],
                landscape: landscape);
        }

        /* ---------- 现金流量表（月报 / 季报） ---------- */

        /// <summary>主表 38 行（3 个活动小标题 + 35 个标准行次），行序与文案同页面；小标题行无行次。</summary>
        private static readonly (string Label, int Line, bool IsSection)[] CashFlowTemplate =
        [
            ("一、经营活动产生的现金流量：", 0, true),
            ("销售商品、提供劳务收到的现金", 1, false),
            ("收到的税费返还", 2, false),
            ("收到其他与经营活动有关的现金", 3, false),
            ("经营活动现金流入小计", 4, false),
            ("购买商品、接受劳务支付的现金", 5, false),
            ("支付给职工以及为职工支付的现金", 6, false),
            ("支付的各项税费", 7, false),
            ("支付其他与经营活动有关的现金", 8, false),
            ("经营活动现金流出小计", 9, false),
            ("经营活动产生的现金流量净额", 10, false),
            ("二、投资活动产生的现金流量：", 0, true),
            ("收回投资收到的现金", 11, false),
            ("取得投资收益收到的现金", 12, false),
            ("处置固定资产、无形资产和其他长期资产收回的现金净额", 13, false),
            ("处置子公司及其他营业单位收到的现金净额", 14, false),
            ("收到其他与投资活动有关的现金", 15, false),
            ("投资活动现金流入小计", 16, false),
            ("购建固定资产、无形资产和其他长期资产支付的现金", 17, false),
            ("投资支付的现金", 18, false),
            ("取得子公司及其他营业单位支付的现金净额", 19, false),
            ("支付其他与投资活动有关的现金", 20, false),
            ("投资活动现金流出小计", 21, false),
            ("投资活动产生的现金流量净额", 22, false),
            ("三、筹资活动产生的现金流量：", 0, true),
            ("吸收投资收到的现金", 23, false),
            ("取得借款收到的现金", 24, false),
            ("收到其他与筹资活动有关的现金", 25, false),
            ("筹资活动现金流入小计", 26, false),
            ("偿还债务支付的现金", 27, false),
            ("分配股利、利润或偿付利息支付的现金", 28, false),
            ("支付其他与筹资活动有关的现金", 29, false),
            ("筹资活动现金流出小计", 30, false),
            ("筹资活动产生的现金流量净额", 31, false),
            ("四、汇率变动对现金及现金等价物的影响", 32, false),
            ("五、现金及现金等价物净增加额", 33, false),
            ("加：期初现金及现金等价物的余额", 34, false),
            ("六、期末现金及现金等价物余额", 35, false),
        ];

        /// <summary>会小企03表模板：3 个活动小标题 + 22 个标准行次（无流入/流出小计、无补充资料）。</summary>
        private static readonly (string Label, int Line, bool IsSection)[] CashFlowSmallBusinessTemplate =
        [
            ("一、经营活动产生的现金流量：", 0, true),
            ("销售产成品、商品、提供劳务收到的现金", 1, false),
            ("收到其他与经营活动有关的现金", 2, false),
            ("购买原材料、商品、接受劳务支付的现金", 3, false),
            ("支付的职工薪酬", 4, false),
            ("支付的税费", 5, false),
            ("支付其他与经营活动有关的现金", 6, false),
            ("经营活动产生的现金流量净额", 7, false),
            ("二、投资活动产生的现金流量：", 0, true),
            ("收回短期投资、长期债券投资和长期股权投资收到的现金", 8, false),
            ("取得投资收益收到的现金", 9, false),
            ("处置固定资产、无形资产和其他非流动资产收回的现金净额", 10, false),
            ("短期投资、长期债券投资和长期股权投资支付的现金", 11, false),
            ("购建固定资产、无形资产和其他非流动资产支付的现金", 12, false),
            ("投资活动产生的现金流量净额", 13, false),
            ("三、筹资活动产生的现金流量：", 0, true),
            ("取得借款收到的现金", 14, false),
            ("吸收投资者投资收到的现金", 15, false),
            ("偿还借款本金支付的现金", 16, false),
            ("偿还借款利息支付的现金", 17, false),
            ("分配利润支付的现金", 18, false),
            ("筹资活动产生的现金流量净额", 19, false),
            ("四、现金净增加额", 20, false),
            ("加：期初现金余额", 21, false),
            ("五、期末现金余额", 22, false),
        ];

        /// <summary>补充资料三段小标题（按行的 Group 切换时插入，同页面文案）。</summary>
        private static readonly Dictionary<int, string> SupplementTitles = new()
        {
            [1] = "1.将净利润调节为经营活动现金流量：",
            [2] = "2.不涉及现金收支的重大投资和筹资活动",
            [3] = "3.现金及现金等价物净变动情况",
        };

        private static PrintSheet CashFlow(
            CashFlowDto dto,
            PrintContext context,
            CashFlowSupplementDto? supplement,
            bool showLastYear)
        {
            var map = MapOf(dto.Lines);
            var smallBusiness = context.Standard == AccountingStandard.SmallBusiness;
            // 「取数公式」列只在公式法取数时出现（直接法、辅助核算法后端不回公式）。
            var withFormula = (dto.Lines ?? []).Any(l => !string.IsNullOrWhiteSpace(l.Formula));
            var columns = ReportColumns("本期金额", showLastYear, withFormula);
            var template = smallBusiness ? CashFlowSmallBusinessTemplate : CashFlowTemplate;
            var rows = CashFlowRows(map, columns.Count, withFormula, showLastYear, template, smallBusiness);

            var notes = new List<string>
            {
                smallBusiness
                    ? "小企业会计准则（会小企03表）版式；直接法简化工作底稿口径；现金科目=1001/1002/1012。"
                    : "直接法简化工作底稿口径；现金科目=1001/1002/1012。",
            };
            if (!smallBusiness && supplement is { Lines.Count: > 0 })
            {
                rows.Add(FullWidthRow("补充资料（将净利润调节为经营活动现金流量）", columns.Count));
                var lastGroup = 0;
                foreach (var line in supplement.Lines)
                {
                    var group = (int)line.Group;
                    if (group != lastGroup)
                    {
                        lastGroup = group;
                        rows.Add(FullWidthRow(SupplementTitles.GetValueOrDefault(group, string.Empty), columns.Count));
                    }

                    // 补充资料列序与主表相同（本年累计 / 本期），故复用同一行构造；无上年与公式列。
                    rows.Add(new PrintRow(
                        ReportCells(line.Name, line.Line, line.IsTotal ? 0 : 1, line.YearToDate, 0m, line.Current, showLastYear, line.IsTotal),
                        line.IsTotal ? PdfCss.Strong : string.Empty));
                }

                notes.Add($"补充资料与主表勾稽：主表「经营活动产生的现金流量净额」{PdfText.AmountAlways(supplement.MainTableOperatingNet)} 元，" +
                          $"差异 {PdfText.AmountAlways(supplement.Difference)} 元。");
            }

            return Sheet("现金流量表", context, dto.PeriodText, columns, rows,
                smallBusiness ? "会小企03表" : "会企03表", notes: notes);
        }

        private static PrintSheet CashFlowQuarter(CashFlowQuarterDto dto, PrintContext context, bool showLastYear)
        {
            var map = MapOf(dto.Lines);
            var smallBusiness = context.Standard == AccountingStandard.SmallBusiness;
            var columns = ReportColumns("本季金额", showLastYear, withFormula: false);
            var template = smallBusiness ? CashFlowSmallBusinessTemplate : CashFlowTemplate;
            var rows = CashFlowRows(map, columns.Count, withFormula: false, showLastYear, template, smallBusiness);
            return Sheet("现金流量表季报", context, dto.PeriodText, columns, rows,
                smallBusiness ? "会小企03表" : "会企03表",
                notes:
                [
                    smallBusiness
                        ? "小企业会计准则（会小企03表）版式；直接法简化工作底稿口径；现金科目=1001/1002/1012。"
                        : "直接法简化工作底稿口径；现金科目=1001/1002/1012。",
                ]);
        }

        private static List<PrintRow> CashFlowRows(
            IReadOnlyDictionary<int, LineAmount> map,
            int columnCount,
            bool withFormula,
            bool showLastYear,
            IReadOnlyList<(string Label, int Line, bool IsSection)> template,
            bool smallBusiness)
        {
            var rows = new List<PrintRow>();
            foreach (var (label, line, isSection) in template)
            {
                if (isSection)
                {
                    rows.Add(FullWidthRow(label, columnCount));
                    continue;
                }

                // 小准则的净额、净增加额、期末余额行加粗，与主表观感一致（该表没有流入/流出小计行）。
                var strong = smallBusiness && line is 7 or 13 or 19 or 20 or 22;
                var amount = map.TryGetValue(line, out var hit) ? hit : new LineAmount(0m, 0m, 0m);
                rows.Add(new PrintRow(ReportCells(label, line, 1, amount.YearToDate, amount.LastYear, amount.Current,
                    showLastYear, strong, formula: withFormula ? amount.Formula : null),
                    strong ? PdfCss.Strong : string.Empty));
            }

            return rows;
        }

        /* ---------- 费用明细表 ---------- */

        /// <summary>费用明细表：科目编码（按级次缩进）/名称 + 当月金额 + 合计（本年累计），末行总计只累加一级科目。</summary>
        private static PrintSheet ExpenseDetail(ExpenseDetailDto dto, PrintContext context)
        {
            var columns = new List<PrintColumn>
            {
                new("科目编码", Width: "18%", Align: PdfAlign.Center),
                new("科目名称"),
                new($"{dto.Year}年{dto.Month}月金额", Width: "20%", Align: PdfAlign.Right),
                new("合计", Width: "20%", Align: PdfAlign.Right),
            };

            var rows = new List<PrintRow>();
            decimal current = 0m;
            decimal total = 0m;
            foreach (var row in dto.Rows ?? [])
            {
                if (row.Level == 1)
                {
                    current += row.Current;
                    total += row.Total;
                }

                rows.Add(new PrintRow(
                [
                    new PrintCell(row.Code, Align: PdfAlign.Center, Css: IndentOfLevel(row.Level)),
                    new PrintCell(row.Name),
                    new PrintCell(row.Current),
                    new PrintCell(row.Total),
                ]));
            }

            rows.Add(new PrintRow(
            [
                PrintCell.Blank,
                new PrintCell("总计", Css: PdfCss.Strong),
                new PrintCell(PdfText.AmountAlways(current)),
                new PrintCell(PdfText.AmountAlways(total)),
            ], PdfCss.Strong));

            return Sheet("费用明细表", context, dto.PeriodText, columns, rows,
                notes: ["成本类与损益类（借方向）科目；上级行由下级汇总。"]);
        }

        /* ---------- 纳税统计表 ---------- */

        /// <summary>税种分组：与页面一致的固定四组（按科目名称包含关键字归集）+「其他税费」兜底 + 合计，每组列「应交 / 已交」两行。</summary>
        private static readonly (string Title, string FallbackLabel, string[] Keywords)[] TaxGroups =
        [
            ("（一）增值税：", "增值税", ["增值税", "销项税额", "进项税额"]),
            ("（二）所得税：", "所得税", ["所得税"]),
            ("（三）消费税：", "消费税", ["消费税"]),
            ("（四）城建税：", "城建税", ["城建税", "城市维护建设税"]),
        ];

        private static PrintSheet Tax(TaxStatisticsDto dto, PrintContext context)
        {
            var columns = new List<PrintColumn>
            {
                new("项目"),
                new("行次", Width: "6%", Align: PdfAlign.Center),
                new("金额", "本年累计金额", "18%", PdfAlign.Right),
                new("同比变化", "本年累计金额", "12%", PdfAlign.Right),
                new("金额", "本期金额", "18%", PdfAlign.Right),
                new("同比变化", "本期金额", "12%", PdfAlign.Right),
            };

            var accounts = dto.Rows ?? [];
            var used = new HashSet<string>();
            var rows = new List<PrintRow>();
            var seq = 0;
            decimal totalCur = 0m;
            decimal totalYtd = 0m;
            decimal totalCurPaid = 0m;
            decimal totalYtdPaid = 0m;

            // 科目名若已带「应交/已交」前缀（如种子账套的「应交所得税」），先剥离，避免叠加成「应交应交所得税」。
            static string Clean(string s) => s.StartsWith("应交") || s.StartsWith("已交") ? s[2..] : s;

            void Pair(string label, decimal ytd, decimal cur, decimal ytdPaid, decimal curPaid, bool strong = false)
            {
                var L = Clean(label);
                seq += 1;
                rows.Add(TaxRow($"应交{L}", seq, ytd, cur, strong));
                seq += 1;
                rows.Add(TaxRow($"已交{L}", seq, ytdPaid, curPaid, strong));
            }

            foreach (var (title, fallback, keywords) in TaxGroups)
            {
                rows.Add(FullWidthRow(title, columns.Count));
                var label = fallback;
                decimal groupCur = 0m;
                decimal groupYtd = 0m;
                decimal groupCurPaid = 0m;
                decimal groupYtdPaid = 0m;
                foreach (var account in accounts)
                {
                    var name = account.Name ?? string.Empty;
                    if (!keywords.Any(keyword => name.Contains(keyword, StringComparison.Ordinal))) continue;

                    used.Add(account.Code);
                    groupCur += account.CurrentAmount;
                    groupYtd += account.YearToDateAmount;
                    groupCurPaid += account.CurrentPaidAmount;
                    groupYtdPaid += account.YearToDatePaidAmount;
                    label = name;
                }

                totalCur += groupCur;
                totalYtd += groupYtd;
                totalCurPaid += groupCurPaid;
                totalYtdPaid += groupYtdPaid;
                Pair(label, groupYtd, groupCur, groupYtdPaid, groupCurPaid);
            }

            // （五）其他税费：未落入前四组的税费科目；（六）合计为全表计提净额与已缴之和。
            rows.Add(FullWidthRow("（五）其他税费：", columns.Count));
            decimal otherCur = 0m;
            decimal otherYtd = 0m;
            decimal otherCurPaid = 0m;
            decimal otherYtdPaid = 0m;
            foreach (var account in accounts.Where(a => !used.Contains(a.Code)))
            {
                otherCur += account.CurrentAmount;
                otherYtd += account.YearToDateAmount;
                otherCurPaid += account.CurrentPaidAmount;
                otherYtdPaid += account.YearToDatePaidAmount;
            }

            totalCur += otherCur;
            totalYtd += otherYtd;
            totalCurPaid += otherCurPaid;
            totalYtdPaid += otherYtdPaid;
            Pair("其他税费", otherYtd, otherCur, otherYtdPaid, otherCurPaid);

            rows.Add(FullWidthRow("（六）合计：", columns.Count));
            seq += 1;
            rows.Add(TaxRow("应交税费合计", seq, totalYtd, totalCur, strong: true));
            seq += 1;
            rows.Add(TaxRow("已交税费合计", seq, totalYtdPaid, totalCurPaid, strong: true));

            return Sheet("纳税统计表", context, dto.PeriodText, columns, rows,
                notes:
                [
                    "应交税费类科目；计提净额=贷方发生-借方缴纳。",
                    "同比变化按科目统计，页面同列留空，故打印稿不汇总同比。",
                ]);
        }

        /// <summary>纳税统计表的应交/已交行：金额为借-贷净额，同比两列按页面口径留白（合计行为 0 也印 0.00）。</summary>
        private static PrintRow TaxRow(string label, int seq, decimal yearToDate, decimal current, bool strong)
        {
            // 列位固定：金额落在「本年累计-金额」与「本期-金额」，同比列必须显式留空补齐，不能靠补白。
            object Money(decimal value) => strong ? PdfText.AmountAlways(value) : value;

            return new PrintRow(new List<PrintCell>
            {
                new PrintCell(label, Css: strong ? PdfCss.Strong : PdfCss.Indent1),
                new PrintCell(seq.ToString(), Align: PdfAlign.Center),
                new PrintCell(Money(yearToDate), Css: strong ? PdfCss.Strong : string.Empty),
                new PrintCell(string.Empty),
                new PrintCell(Money(current), Css: strong ? PdfCss.Strong : string.Empty),
                new PrintCell(string.Empty),
            }, strong ? PdfCss.Strong : string.Empty);
        }

        /* ---------- 财务概要信息 ---------- */

        private static PrintSheet FinancialSummary(FinancialSummaryDto dto, PrintContext context)
        {
            var columns = new List<PrintColumn>
            {
                new("项目"),
                new("行次", Width: "6%", Align: PdfAlign.Center),
                new("金额", "本年累计金额", "16%", PdfAlign.Right),
                new("同比变化", "本年累计金额", "11%", PdfAlign.Right),
                new("金额", "本期金额", "16%", PdfAlign.Right),
                new("同比变化", "本期金额", "11%", PdfAlign.Right),
                new("环比变化", "本期金额", "11%", PdfAlign.Right),
            };

            var byItem = new Dictionary<string, FinancialSummaryRowDto>();
            foreach (var row in dto.Rows ?? [])
            {
                if (!string.IsNullOrWhiteSpace(row.Item)) byItem[row.Item] = row;
            }

            decimal Ytd(string name) => byItem.TryGetValue(name, out var row) ? row.YearToDateAmount : 0m;
            decimal Cur(string name) => byItem.TryGetValue(name, out var row) ? row.CurrentAmount : 0m;

            var rows = new List<PrintRow>();
            void Fill(string name, int seq)
            {
                var row = byItem.TryGetValue(name, out var hit) ? hit : null;
                rows.Add(new PrintRow(new List<PrintCell>
                {
                    new PrintCell(name, Css: PdfCss.Indent1),
                    new PrintCell(seq.ToString(), Align: PdfAlign.Center),
                    new PrintCell(row?.YearToDateAmount ?? 0m),
                    new PrintCell(Percent(row?.YearToDateYoyPercent)),
                    new PrintCell(row?.CurrentAmount ?? 0m),
                    new PrintCell(Percent(row?.CurrentYoyPercent)),
                    new PrintCell(Percent(row?.CurrentMomPercent)),
                }));
            }

            void Ratio(int seq, string label, decimal ytdNumerator, decimal ytdDenominator, decimal curNumerator, decimal curDenominator)
                => rows.Add(new PrintRow(new List<PrintCell>
                {
                    new PrintCell(label, Css: PdfCss.Indent1),
                    new PrintCell(seq.ToString(), Align: PdfAlign.Center),
                    new PrintCell(RatioPct(ytdNumerator, ytdDenominator)),
                    new PrintCell(string.Empty),
                    new PrintCell(RatioPct(curNumerator, curDenominator)),
                    new PrintCell(string.Empty),
                    new PrintCell(string.Empty),
                }));

            rows.Add(FullWidthRow("(一) 盈利状况：", columns.Count));
            Fill("营业收入", 1);
            Fill("净利润", 2);
            Ratio(3, "营业收入净利率", Ytd("净利润"), Ytd("营业收入"), Cur("净利润"), Cur("营业收入"));

            rows.Add(FullWidthRow("(二) 三项费用：", columns.Count));
            Fill("管理费用", 4);
            Fill("销售费用", 5);
            Fill("财务费用", 6);
            decimal ExpenseYtd(Func<string, decimal> pick) => pick("管理费用") + pick("销售费用") + pick("财务费用");
            Ratio(7, "费用比率", ExpenseYtd(Ytd), Ytd("营业收入"), ExpenseYtd(Cur), Cur("营业收入"));

            rows.Add(FullWidthRow("(三) 应收应付：", columns.Count));
            Fill("应收款", 8);
            Fill("应付款", 9);

            rows.Add(FullWidthRow("(四) 资金状况：", columns.Count));
            Fill("资金收入", 10);
            Fill("资金支付", 11);
            Fill("资金收支", 12);

            rows.Add(FullWidthRow("(五) 税金状况：", columns.Count));
            Fill("应交税费", 13);
            Ratio(14, "综合税负率", Ytd("应交税费"), Ytd("营业收入"), Cur("应交税费"), Cur("营业收入"));

            return Sheet("财务概要信息", context, dto.PeriodText, columns, rows,
                notes: ["存量指标取期末余额，流量指标取发生额；比率行由本表金额现算。"]);
        }

        /* ---------- 公共构造 ---------- */

        /// <summary>报表的一行单元格：项目（含缩进/加粗）+ 行次 + 本年累计（+ 可选上年累计）+ 本期（+ 可选取数公式）。</summary>
        private static List<PrintCell> ReportCells(
            string label,
            int line,
            int indent,
            decimal yearToDate,
            decimal lastYear,
            decimal current,
            bool showLastYear,
            bool strong,
            string? formula = null)
        {
            // 合计行即使为 0 也要印出 0.00；明细行直接交 decimal，由渲染层按账页惯例把 0 留白。
            var cells = new List<PrintCell>
            {
                new PrintCell(label, Css: strong ? PdfCss.Strong : IndentOfClass(indent)),
                new PrintCell(line == 0 ? null : line.ToString(), Align: PdfAlign.Center),
            };
            if (strong)
            {
                cells.Add(new PrintCell(PdfText.AmountAlways(yearToDate), Css: PdfCss.Strong));
                if (showLastYear) cells.Add(new PrintCell(PdfText.AmountAlways(lastYear), Css: PdfCss.Strong));
                cells.Add(new PrintCell(PdfText.AmountAlways(current), Css: PdfCss.Strong));
            }
            else
            {
                cells.Add(new PrintCell(yearToDate));
                if (showLastYear) cells.Add(new PrintCell(lastYear));
                cells.Add(new PrintCell(current));
            }

            if (formula is not null) cells.Add(new PrintCell(formula));
            return cells;
        }

        /// <summary>段落/分组标题行：整行合并成一格，与小计标题一致的观感（页面上该行只占项目列）。</summary>
        private static PrintRow FullWidthRow(string text, int columnCount)
            => new(new List<PrintCell> { new(text, ColSpan: Math.Max(1, columnCount)) });

        /// <summary>利润表 / 现金流量表的公共列：项目 + 行次 + 本年累计（+ 可选上年累计）+ 本期（+ 可选取数公式）。</summary>
        private static List<PrintColumn> ReportColumns(string currentTitle, bool showLastYear, bool withFormula)
        {
            var columns = new List<PrintColumn>
            {
                new("项目"),
                new("行次", Width: "6%", Align: PdfAlign.Center),
                new("本年累计金额", Width: "18%", Align: PdfAlign.Right),
            };
            if (showLastYear) columns.Add(new PrintColumn("上年累计金额", Width: "18%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn(currentTitle, Width: withFormula ? "14%" : "18%", Align: PdfAlign.Right));
            if (withFormula) columns.Add(new PrintColumn("取数公式", Width: "26%"));
            return columns;
        }

        private static PrintSheet Sheet(
            string title,
            PrintContext context,
            string? periodText,
            IReadOnlyList<PrintColumn> columns,
            IReadOnlyList<PrintRow> rows,
            string? subtitle = null,
            string? subject = null,
            IReadOnlyList<string>? notes = null,
            bool landscape = false)
        {
            var company = string.IsNullOrWhiteSpace(context.CompanyName) ? context.AccountSetName : context.CompanyName!;
            var head = new List<PrintMeta>
            {
                new($"编制单位：{company}"),
                new($"账套：{context.AccountSetName}", PdfAlign.Center),
            };
            if (!string.IsNullOrWhiteSpace(subtitle)) head.Add(new PrintMeta(subtitle!, PdfAlign.Center));
            if (!string.IsNullOrWhiteSpace(subject)) head.Add(new PrintMeta($"科目：{subject}", PdfAlign.Center));
            if (!string.IsNullOrWhiteSpace(periodText)) head.Add(new PrintMeta($"期间：{periodText}", PdfAlign.Right));

            var tail = new List<string> { "单位：元" };
            if (notes is { Count: > 0 }) tail.AddRange(notes);

            return new PrintSheet
            {
                Title = title,
                Meta = head,
                Columns = columns,
                Rows = rows,
                Notes = tail,
                Landscape = landscape,
                PrintedAt = DateTimeOffset.Now,
                EmptyText = "该期间暂无数据，请确认凭证为正常状态。",
            };
        }

        /// <summary>一行金额，按行次归集；月报与季报共用（本期即所选期间）。</summary>
        private sealed record LineAmount(decimal Current, decimal YearToDate, decimal LastYear, string? Formula = null);

        private static Dictionary<int, LineAmount> MapOf(IReadOnlyList<IncomeStatementLineDto>? lines)
            => (lines ?? [])
                .GroupBy(l => l.Line)
                .ToDictionary(g => g.Key, g => new LineAmount(
                    g.Sum(l => l.Current), g.Sum(l => l.YearToDate), g.Sum(l => l.LastYear)));

        private static Dictionary<int, LineAmount> MapOf(IReadOnlyList<CashFlowLineDto>? lines)
            => (lines ?? [])
                .GroupBy(l => l.Line)
                .ToDictionary(g => g.Key, g => new LineAmount(
                    g.Sum(l => l.Current), g.Sum(l => l.YearToDate), g.Sum(l => l.LastYear),
                    g.Select(l => l.Formula).FirstOrDefault(f => !string.IsNullOrWhiteSpace(f))));

        /// <summary>页头「科目」：只在指定了编码区间时输出，与账页打印同口径。</summary>
        private static string? SubjectOf(LedgerQueryOptions? options)
        {
            var from = options?.AccountFrom;
            var to = options?.AccountTo;
            if (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to)) return null;
            if (!string.IsNullOrEmpty(from) && from == to) return from;
            return $"{from} 至 {to}";
        }

        /// <summary>科目级次 → 缩进样式：一级不缩进，二级缩一档，三级及以上缩两档。</summary>
        private static string IndentOfLevel(int level) => level switch
        {
            <= 1 => string.Empty,
            2 => PdfCss.Indent1,
            _ => PdfCss.Indent2,
        };

        /// <summary>模板里的缩进层级（0/1/2）→ 样式类。</summary>
        private static string IndentOfClass(int indent) => indent switch
        {
            <= 0 => string.Empty,
            1 => PdfCss.Indent1,
            _ => PdfCss.Indent2,
        };

        /// <summary>百分比文本：后端已是百分数值（8.36 表示 8.36%），无对比基数时留白。</summary>
        private static string Percent(decimal? value)
            => value is { } percent ? Fixed(percent) + "%" : string.Empty;

        /// <summary>两个金额相除的百分比（页面同口径：分子或分母为 0 时留白）。</summary>
        private static string RatioPct(decimal numerator, decimal denominator)
            => numerator == 0m || denominator == 0m ? string.Empty : Fixed(numerator / denominator * 100m) + "%";

        private static string Fixed(decimal value)
            => Math.Round(value, 2).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>「2026-07-01」→「202607」；非日期文本取数字前六位，保证文件名干净。</summary>
        private static string MonthOf(string? date)
        {
            var digits = new string((date ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length >= 6 ? digits[..6] : digits;
        }

        /// <summary>金额按分比较，规避累计误差（页面的试算平衡同口径）。</summary>
        private static bool SameMoney(decimal left, decimal right) => Math.Round(left, 2) == Math.Round(right, 2);
    }
}
