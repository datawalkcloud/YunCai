using KuaiJi.SharedKernel.Infrastructure.Pdf;

namespace KuaiJi.Modules.Salary.Application
{
    /// <summary>
    /// 工资打印 PDF 的表格投影：工资表（一行一员工）与工资统计表（按部门汇总）按页面表格 1:1 描述，
    /// 列名、顺序与末行合计均照页面来；工资条是「一人一条」的裁切版式，由 Infrastructure 的 PayslipPdfBuilder 单独负责，不走这里。
    /// </summary>
    public static class SalaryPdfProjector
    {
        /// <summary>页头需要的账套信息：工资 DTO 不带编制单位，由控制器注入。</summary>
        public sealed record PrintContext(string AccountSetName, string? CompanyName = null);

        /// <summary>
        /// 工资表：编号/姓名/部门 + 十二项工资口径，末行「合计」。
        /// 与页面一致，金额一律显示数值（0 也印 0.00），故走 <see cref="PdfText.AmountAlways"/>。
        /// </summary>
        public static PrintSheet Bills(IReadOnlyList<SalaryBillDto> bills, int period, PrintContext context)
        {
            var columns = new List<PrintColumn>
            {
                new("编号", Width: "5%", Align: PdfAlign.Center),
                new("姓名", Width: "6%"),
                new("部门", Width: "6%"),
                new("工资总额", Width: "8%", Align: PdfAlign.Right),
                new("应扣工资", Width: "8%", Align: PdfAlign.Right),
                new("应发工资", Width: "8%", Align: PdfAlign.Right),
                new("代扣个人款项", Width: "9%", Align: PdfAlign.Right),
                new("个税免征额", Width: "8%", Align: PdfAlign.Right),
                new("本月应缴个税", Width: "8%", Align: PdfAlign.Right),
                new("实发工资调整", Width: "8%", Align: PdfAlign.Right),
                new("实发工资", Width: "8%", Align: PdfAlign.Right),
                new("公司承担款项", Width: "9%", Align: PdfAlign.Right),
                new("员工成本", Width: "9%", Align: PdfAlign.Right),
            };

            var rows = bills
                .Select(b => new PrintRow(CellList(
                    b.EmployeeCode, b.EmployeeName, b.Dept,
                    PdfText.AmountAlways(b.Gross), PdfText.AmountAlways(b.Deductible), PdfText.AmountAlways(b.Payable),
                    PdfText.AmountAlways(b.PersonalDeduction), PdfText.AmountAlways(b.TaxThreshold),
                    PdfText.AmountAlways(b.Tax), PdfText.AmountAlways(b.Adjust), PdfText.AmountAlways(b.Net),
                    PdfText.AmountAlways(b.CompanyPart), PdfText.AmountAlways(b.Cost))))
                .ToList();

            if (rows.Count > 0)
            {
                rows.Add(new PrintRow(
                [
                    new PrintCell("合计", ColSpan: 3, Css: PdfCss.Strong),
                    Total(bills.Sum(b => b.Gross)),
                    Total(bills.Sum(b => b.Deductible)),
                    Total(bills.Sum(b => b.Payable)),
                    Total(bills.Sum(b => b.PersonalDeduction)),
                    Total(bills.Sum(b => b.TaxThreshold)),
                    Total(bills.Sum(b => b.Tax)),
                    Total(bills.Sum(b => b.Adjust)),
                    Total(bills.Sum(b => b.Net)),
                    Total(bills.Sum(b => b.CompanyPart)),
                    Total(bills.Sum(b => b.Cost)),
                ]));
            }

            return Sheet(
                "工资表",
                context,
                columns,
                rows,
                PeriodTextOf(period),
                [$"共 {bills.Count} 人。", "实发工资 = 应发工资 − 代扣个人款项 − 本月应缴个税 + 实发工资调整。"],
                emptyText: $"{PeriodTextOf(period)}还没有工资单，请先在「工资列表」录入。",
                landscape: true);
        }

        /// <summary>工资统计表：按部门汇总，末行「合计」含人数求和，与页面表尾一致。</summary>
        public static PrintSheet Summary(
            IReadOnlyList<SalarySummaryRowDto> rows, int startPeriod, int endPeriod, PrintContext context)
        {
            var columns = new List<PrintColumn>
            {
                new("部门", Width: "28%"),
                new("人数", Width: "8%", Align: PdfAlign.Right),
                new("应发工资", Width: "14%", Align: PdfAlign.Right),
                new("代扣个人款项", Width: "14%", Align: PdfAlign.Right),
                new("公司承担款项", Width: "14%", Align: PdfAlign.Right),
                new("实发工资", Width: "11%", Align: PdfAlign.Right),
                new("员工成本", Width: "11%", Align: PdfAlign.Right),
            };

            var body = rows
                .Select(r => new PrintRow(CellList(
                    r.Dept, r.EmployeeCount,
                    PdfText.AmountAlways(r.Payable), PdfText.AmountAlways(r.PersonalDeduction),
                    PdfText.AmountAlways(r.CompanyPart), PdfText.AmountAlways(r.Net), PdfText.AmountAlways(r.Cost))))
                .ToList();

            if (body.Count > 0)
            {
                body.Add(new PrintRow(
                [
                    new PrintCell("合计", Css: PdfCss.Strong),
                    new(rows.Sum(r => r.EmployeeCount), Align: PdfAlign.Right, Css: PdfCss.Strong),
                    Total(rows.Sum(r => r.Payable)),
                    Total(rows.Sum(r => r.PersonalDeduction)),
                    Total(rows.Sum(r => r.CompanyPart)),
                    Total(rows.Sum(r => r.Net)),
                    Total(rows.Sum(r => r.Cost)),
                ]));
            }

            return Sheet(
                "工资统计表",
                context,
                columns,
                body,
                $"{PeriodTextOf(startPeriod)}—{PeriodTextOf(endPeriod)}",
                [$"共 {rows.Count} 个部门、{rows.Sum(r => r.EmployeeCount)} 人。"],
                emptyText: "所选区间暂无工资单，请先在「工资列表」录入。");
        }

        /// <summary>期间标识 202601 → 「2026年1月」。</summary>
        private static string PeriodTextOf(int period) => $"{period / 100:D4}年{period % 100:D1}月";

        private static PrintCell Total(decimal value)
            => new(PdfText.AmountAlways(value), Align: PdfAlign.Right, Css: PdfCss.Strong);

        private static PrintSheet Sheet(
            string title,
            PrintContext context,
            IReadOnlyList<PrintColumn> columns,
            IReadOnlyList<PrintRow> rows,
            string periodText,
            IReadOnlyList<string> notes,
            string emptyText,
            bool landscape = false)
        {
            var company = string.IsNullOrWhiteSpace(context.CompanyName) ? context.AccountSetName : context.CompanyName!;

            return new PrintSheet
            {
                Title = title,
                Meta =
                [
                    new PrintMeta($"编制单位：{company}"),
                    new PrintMeta($"账套：{context.AccountSetName}", PdfAlign.Center),
                    new PrintMeta($"期间：{periodText}", PdfAlign.Right),
                ],
                Columns = columns,
                Rows = rows,
                Notes = ["单位：元", .. notes],
                Landscape = landscape,
                PrintedAt = DateTimeOffset.Now,
                EmptyText = emptyText,
            };
        }

        /// <summary>把一行值转成单元格：文本与金额按 <see cref="PrintSheetPdfBuilder"/> 的规则各自格式化。</summary>
        private static IReadOnlyList<PrintCell> CellList(params object?[] values) =>
            values.Select(value => new PrintCell(value)).ToList();
    }
}
