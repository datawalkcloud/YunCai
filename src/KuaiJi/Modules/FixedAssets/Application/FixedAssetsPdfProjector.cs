using KuaiJi.SharedKernel.Infrastructure.Pdf;

namespace KuaiJi.Modules.FixedAssets.Application
{
    /// <summary>
    /// 固定资产打印 PDF 的表格投影：列、合计行与分组行按各页屏幕表格 1:1 描述，
    /// 版式（A4、边框、跨页续表、页脚页码与打印时间）由 <see cref="PrintSheetPdfBuilder"/> 一处负责。
    /// 与 <see cref="FixedAssetExportBuilder"/>（压平成单行表头写 xlsx）取数口径相同，只是保留表格结构。
    /// </summary>
    public static class FixedAssetsPdfProjector
    {
        /// <summary>页头需要的账套信息：资产 DTO 不带编制单位，由控制器注入。</summary>
        public sealed record PrintContext(string AccountSetName, string? CompanyName = null);

        /// <summary>固定资产清单（资产列表页签）：卡片全字段 + 末行原值 / 本期折旧 / 净值合计。</summary>
        public static PrintSheet Assets(IReadOnlyList<FixedAssetDto> assets, PrintContext context, string? periodText = null)
        {
            var columns = new List<PrintColumn>
            {
                new("资产编号", Width: "10%", Align: PdfAlign.Center),
                new("资产类别", Width: "11%"),
                new("资产属性", Width: "8%", Align: PdfAlign.Center),
                new("资产名称", Width: "15%"),
                new("规格型号", Width: "10%"),
                new("开始使用日期", Width: "10%", Align: PdfAlign.Center),
                new("资产原值", Width: "12%", Align: PdfAlign.Right),
                new("本期折旧", Width: "11%", Align: PdfAlign.Right),
                new("资产净值", Width: "12%", Align: PdfAlign.Right),
            };

            var rows = assets
                .Select(a => new PrintRow(CellList(
                    a.Code, a.CategoryName, a.AttrText, a.Name, a.Model, a.StartDate,
                    a.OriginValue, a.CurrentPeriodDepreciation, a.NetValue)))
                .ToList();

            if (rows.Count > 0)
            {
                // 页面上合计挂在表格上方的统计条里，打印折成表尾合计行。
                rows.Add(new PrintRow(
                [
                    new PrintCell("合计", ColSpan: 6, Css: PdfCss.Strong),
                    new(PdfText.AmountAlways(assets.Sum(a => a.OriginValue)), Align: PdfAlign.Right, Css: PdfCss.Strong),
                    new(PdfText.AmountAlways(assets.Sum(a => a.CurrentPeriodDepreciation)), Align: PdfAlign.Right, Css: PdfCss.Strong),
                    new(PdfText.AmountAlways(assets.Sum(a => a.NetValue)), Align: PdfAlign.Right, Css: PdfCss.Strong),
                ]));
            }

            return Sheet(
                "固定资产清单",
                context,
                columns,
                rows,
                periodText,
                notes: [$"共 {assets.Count} 项资产。"],
                landscape: true,
                emptyText: "还没有资产卡片，请先在「资产初始化」录入期初资产。");
        }

        /// <summary>资产初始化清单（资产初始化页签）：期初口径，列比资产清单少「资产属性」「本期折旧」、多「期初累计折旧」。</summary>
        public static PrintSheet InitialAssets(IReadOnlyList<FixedAssetDto> assets, PrintContext context, string? periodText = null)
        {
            var columns = new List<PrintColumn>
            {
                new("资产编号", Width: "12%", Align: PdfAlign.Center),
                new("资产类别", Width: "14%"),
                new("资产名称", Width: "18%"),
                new("规格型号", Width: "12%"),
                new("开始使用日期", Width: "12%", Align: PdfAlign.Center),
                new("资产原值", Width: "12%", Align: PdfAlign.Right),
                new("期初累计折旧", Width: "10%", Align: PdfAlign.Right),
                new("资产净值", Width: "10%", Align: PdfAlign.Right),
            };

            var rows = assets
                .Select(a => new PrintRow(CellList(
                    a.Code, a.CategoryName, a.Name, a.Model, a.StartDate,
                    a.OriginValue, a.AccumulatedDepreciation, a.NetValue)))
                .ToList();

            return Sheet(
                "资产初始化清单",
                context,
                columns,
                rows,
                periodText,
                notes:
                [
                    $"共 {assets.Count} 项期初资产。",
                    "期初资产数据须与实物账保持一致，录入后请核对原值与期初累计折旧。",
                ],
                emptyText: "还没有期初资产，请先在「资产初始化」录入期初卡片。");
        }

        /// <summary>折旧摊销明细表：期间列取所选会计期间，减值准备按页面口径单列（模型暂未启用，恒为 0）。</summary>
        public static PrintSheet DepreciationLedger(
            IReadOnlyList<DepreciationLedgerRowDto> rows, int period, PrintContext context)
        {
            var columns = new List<PrintColumn>
            {
                new("会计期间", Width: "7%", Align: PdfAlign.Center),
                new("资产编号", Width: "8%", Align: PdfAlign.Center),
                new("资产类别", Width: "9%"),
                new("资产属性", Width: "7%", Align: PdfAlign.Center),
                new("资产名称", Width: "12%"),
                new("规格型号", Width: "8%"),
                new("使用部门", Width: "7%"),
                new("资产原值", Width: "9%", Align: PdfAlign.Right),
                new("本期折旧", Width: "9%", Align: PdfAlign.Right),
                new("本年累计折旧", Width: "9%", Align: PdfAlign.Right),
                new("累计折旧", Width: "9%", Align: PdfAlign.Right),
                new("减值准备", Width: "6%", Align: PdfAlign.Right),
                new("资产净值", Width: "8%", Align: PdfAlign.Right),
            };

            var periodText = PeriodTextOf(period);
            var body = rows
                .Select(r => new PrintRow(CellList(
                    periodText, r.AssetCode, r.CategoryName, r.AttrText, r.AssetName, r.Model, r.Dept,
                    r.OriginValue, r.PeriodDepreciation, r.YearAccumulated, r.TotalAccumulated, r.Impairment, r.NetValue)))
                .ToList();

            if (body.Count > 0)
            {
                body.Add(new PrintRow(
                [
                    new PrintCell("合计", ColSpan: 7, Css: PdfCss.Strong),
                    new(PdfText.AmountAlways(rows.Sum(r => r.OriginValue)), Align: PdfAlign.Right, Css: PdfCss.Strong),
                    new(PdfText.AmountAlways(rows.Sum(r => r.PeriodDepreciation)), Align: PdfAlign.Right, Css: PdfCss.Strong),
                    new(PdfText.AmountAlways(rows.Sum(r => r.YearAccumulated)), Align: PdfAlign.Right, Css: PdfCss.Strong),
                    new(PdfText.AmountAlways(rows.Sum(r => r.TotalAccumulated)), Align: PdfAlign.Right, Css: PdfCss.Strong),
                    new(PdfText.AmountAlways(rows.Sum(r => r.Impairment)), Align: PdfAlign.Right, Css: PdfCss.Strong),
                    new(PdfText.AmountAlways(rows.Sum(r => r.NetValue)), Align: PdfAlign.Right, Css: PdfCss.Strong),
                ]));
            }

            return Sheet(
                "折旧摊销明细表",
                context,
                columns,
                body,
                periodText,
                notes: [$"共 {rows.Count} 项资产参与本期折旧摊销。"],
                landscape: true,
                emptyText: $"{periodText}暂无资产卡片，无法计提折旧。");
        }

        /// <summary>
        /// 资产核对总账：类别行是页面上一整行的分组头（这里跨满所有列），
        /// 其下依次是会计科目、资产卡片与差异三行；差异行加粗，与页面的蓝色强调同权重。
        /// </summary>
        public static PrintSheet Check(IReadOnlyList<FixedAssetCheckRowDto> rows, PrintContext context)
        {
            var columns = new List<PrintColumn>
            {
                new("项目", Width: "20%"),
                new("名称", Width: "28%"),
                new("期初余额", Width: "13%", Align: PdfAlign.Right),
                new("借方", Width: "13%", Align: PdfAlign.Right),
                new("贷方", Width: "13%", Align: PdfAlign.Right),
                new("余额", Width: "13%", Align: PdfAlign.Right),
            };

            var body = new List<PrintRow>();
            foreach (var row in rows)
            {
                var item = row.Item ?? string.Empty;
                if (item.StartsWith("▼ ", StringComparison.Ordinal))
                {
                    body.Add(new PrintRow([new PrintCell(item[2..], ColSpan: columns.Count, Css: PdfCss.Strong)]));
                    continue;
                }

                body.Add(new PrintRow(CellList(
                    row.IsDiff ? "差异" : item, row.Name, row.Opening, row.Debit, row.Credit, row.Balance),
                    row.IsDiff ? PdfCss.Strong : ""));
            }

            return Sheet(
                "资产核对总账",
                context,
                columns,
                body,
                null,
                notes: ["差异 = 会计科目余额 − 资产卡片对应金额，非 0 时请核对科目期初与资产卡片。"],
                emptyText: "尚未配置资产类别，无法核对总账。");
        }

        /// <summary>期间标识 202601 → 「2026年1月」，与页面期间列一致。</summary>
        private static string PeriodTextOf(int period) => $"{period / 100:D4}年{period % 100:D1}月";

        private static PrintSheet Sheet(
            string title,
            PrintContext context,
            IReadOnlyList<PrintColumn> columns,
            IReadOnlyList<PrintRow> rows,
            string? periodText,
            string emptyText,
            IReadOnlyList<string>? notes = null,
            bool landscape = false)
        {
            var company = string.IsNullOrWhiteSpace(context.CompanyName) ? context.AccountSetName : context.CompanyName!;
            var head = new List<PrintMeta>
            {
                new($"编制单位：{company}"),
                new($"账套：{context.AccountSetName}", PdfAlign.Center),
            };
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
                EmptyText = emptyText,
            };
        }

        /// <summary>把一行值转成单元格：文本与金额按 <see cref="PrintSheetPdfBuilder"/> 的规则各自格式化。</summary>
        private static IReadOnlyList<PrintCell> CellList(params object?[] values) =>
            values.Select(value => new PrintCell(value)).ToList();
    }
}
