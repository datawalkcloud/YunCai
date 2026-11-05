namespace KuaiJi.Modules.Reports.Application
{
    public enum BalanceSheetRowKind
    {
        /// <summary>分类小标题，如「流动资产」。</summary>
        Section,

        /// <summary>明细项目。</summary>
        Item,

        /// <summary>「其中」明细（存货明细行），不计入合计。</summary>
        Detail,

        /// <summary>合计行。</summary>
        Total,
    }

    /// <summary>导出用的一行：左半或右半。金额为 null 表示该行不展示金额（小标题/留白）。</summary>
    public sealed record BalanceSheetExportRow(
        string Label,
        int? Line,
        decimal? Ending,
        decimal? Opening,
        BalanceSheetRowKind Kind);

    /// <summary>
    /// 把按行次归集的金额铺成两栏版式：企业会计准则（会企01表）或小企业会计准则（会小企01表）。
    /// 合计行在此计算，与前端页面口径一致。
    /// </summary>
    internal static class BalanceSheetExportLayout
    {
        public static (IReadOnlyList<BalanceSheetExportRow> Left, IReadOnlyList<BalanceSheetExportRow> Right) Build(
            BalanceSheetTemplate template, IReadOnlyList<BalanceSheetLineDto> lines)
        {
            var map = new Dictionary<int, (decimal Ending, decimal Opening)>();
            foreach (var line in lines)
            {
                map[line.Line] = (line.Ending, line.Opening);
            }

            return template == BalanceSheetTemplate.SmallBusiness ? BuildSmallBusiness(map) : BuildEnterprise(map);
        }

        // ── 企业会计准则（会企01表）：62 行次，左右各 34 行 ──

        private static (List<BalanceSheetExportRow> Left, List<BalanceSheetExportRow> Right) BuildEnterprise(
            Dictionary<int, (decimal Ending, decimal Opening)> map)
        {
            var left = new List<BalanceSheetExportRow> { Section("流动资产") };
            for (var line = 1; line <= 11; line++)
            {
                left.Add(Item(BalanceSheetService.LineName(line), line, map));
            }

            var currentAssets = Sum(map, 1, 11);
            left.Add(Total("流动资产合计", 12, currentAssets));
            left.Add(Blank());
            left.Add(Section("非流动资产"));
            for (var line = 13; line <= 29; line++)
            {
                left.Add(Item(BalanceSheetService.LineName(line), line, map));
            }

            var nonCurrentAssets = Sum(map, 13, 29);
            left.Add(Total("非流动资产合计", 30, nonCurrentAssets));
            left.Add(Total("资产总计", 31, Add(currentAssets, nonCurrentAssets)));

            var right = new List<BalanceSheetExportRow> { Section("流动负债") };
            for (var line = 32; line <= 43; line++)
            {
                right.Add(Item(BalanceSheetService.LineName(line), line, map));
            }

            var currentLiabilities = Sum(map, 32, 43);
            right.Add(Total("流动负债合计", 44, currentLiabilities));
            right.Add(Section("非流动负债"));
            for (var line = 45; line <= 52; line++)
            {
                right.Add(Item(BalanceSheetService.LineName(line), line, map));
            }

            var nonCurrentLiabilities = Sum(map, 45, 52);
            right.Add(Total("非流动负债合计", 53, nonCurrentLiabilities));
            right.Add(Total("负债合计", 54, Add(currentLiabilities, nonCurrentLiabilities)));
            right.Add(Section("所有者权益（或股东权益）"));
            for (var line = 55; line <= 60; line++)
            {
                right.Add(Item(BalanceSheetService.LineName(line), line, map));
            }

            var equity = Sum(map, 55, 60);
            right.Add(Total("所有者权益（或股东权益）合计", 61, equity));
            right.Add(Total(
                "负债和所有者权益（或股东权益）总计",
                62,
                Add(Add(currentLiabilities, nonCurrentLiabilities), equity)));

            return (left, right);
        }

        // ── 小企业会计准则（会小企01表）：53 行次，左右各 32 行 ──

        private static (List<BalanceSheetExportRow> Left, List<BalanceSheetExportRow> Right) BuildSmallBusiness(
            Dictionary<int, (decimal Ending, decimal Opening)> map)
        {
            var left = new List<BalanceSheetExportRow> { Section("流动资产") };
            for (var line = 1; line <= 9; line++)
            {
                left.Add(Item(SmallBusinessLineName(line), line, map));
            }

            for (var line = 10; line <= 13; line++)
            {
                left.Add(Detail(SmallBusinessLineName(line), line, map));
            }

            left.Add(Item(SmallBusinessLineName(14), 14, map));
            var currentAssets = Add(Sum(map, 1, 9), (Amount(map, 14, true), Amount(map, 14, false)));
            left.Add(Total("流动资产合计", 15, currentAssets));

            left.Add(Section("非流动资产"));
            left.Add(Item(SmallBusinessLineName(16), 16, map));
            left.Add(Item(SmallBusinessLineName(17), 17, map));
            left.Add(Item(SmallBusinessLineName(18), 18, map));
            left.Add(Item(SmallBusinessLineName(19), 19, map));
            left.Add(new BalanceSheetExportRow(
                SmallBusinessLineName(20),
                20,
                Amount(map, 18, true) - Amount(map, 19, true),
                Amount(map, 18, false) - Amount(map, 19, false),
                BalanceSheetRowKind.Item));
            for (var line = 21; line <= 28; line++)
            {
                left.Add(Item(SmallBusinessLineName(line), line, map));
            }

            // 固定资产以「账面价值」参与合计，原价与累计折旧仅作展示。
            var nonCurrentAssets = Sum(map, 16, 17);
            nonCurrentAssets = Add(nonCurrentAssets, (
                Amount(map, 18, true) - Amount(map, 19, true),
                Amount(map, 18, false) - Amount(map, 19, false)));
            nonCurrentAssets = Add(nonCurrentAssets, Sum(map, 21, 28));
            left.Add(Total("非流动资产合计", 29, nonCurrentAssets));
            left.Add(Total("资产合计", 30, Add(currentAssets, nonCurrentAssets)));

            var right = new List<BalanceSheetExportRow> { Section("流动负债") };
            for (var line = 31; line <= 39; line++)
            {
                right.Add(Item(SmallBusinessLineName(line), line, map));
            }

            // 与左侧存货明细行对齐的留白（4 行）。
            for (var i = 0; i < 4; i++)
            {
                right.Add(Blank());
            }

            right.Add(Item(SmallBusinessLineName(40), 40, map));
            var currentLiabilities = Add(Sum(map, 31, 39), (Amount(map, 40, true), Amount(map, 40, false)));
            right.Add(Total("流动负债合计", 41, currentLiabilities));

            right.Add(Section("非流动负债"));
            for (var line = 42; line <= 45; line++)
            {
                right.Add(Item(SmallBusinessLineName(line), line, map));
            }

            var nonCurrentLiabilities = Sum(map, 42, 45);
            right.Add(Total("非流动负债合计", 46, nonCurrentLiabilities));
            right.Add(Total("负债合计", 47, Add(currentLiabilities, nonCurrentLiabilities)));

            // 与左侧固定资产清理/生物资产对齐的留白（2 行）。
            right.Add(Blank());
            right.Add(Blank());
            right.Add(Section("所有者权益（或股东权益）"));
            for (var line = 48; line <= 51; line++)
            {
                right.Add(Item(SmallBusinessLineName(line), line, map));
            }

            var equity = Sum(map, 48, 51);
            right.Add(Total("所有者权益（或股东权益）合计", 52, equity));
            right.Add(Total(
                "负债和所有者权益（或股东权益）",
                53,
                Add(Add(currentLiabilities, nonCurrentLiabilities), equity)));

            return (left, right);
        }

        private static string SmallBusinessLineName(int line) => line switch
        {
            1 => "货币资金",
            2 => "短期投资",
            3 => "应收票据",
            4 => "应收账款",
            5 => "预付账款",
            6 => "应收股利",
            7 => "应收利息",
            8 => "其他应收款",
            9 => "存货",
            10 => "其中：原材料",
            11 => "在产品",
            12 => "库存商品",
            13 => "周转材料",
            14 => "其他流动资产",
            16 => "长期债券投资",
            17 => "长期股权投资",
            18 => "固定资产原价",
            19 => "减：累计折旧",
            20 => "固定资产账面价值",
            21 => "在建工程",
            22 => "工程物资",
            23 => "固定资产清理",
            24 => "生产性生物资产",
            25 => "无形资产",
            26 => "开发支出",
            27 => "长期待摊费用",
            28 => "其他非流动资产",
            31 => "短期借款",
            32 => "应付票据",
            33 => "应付账款",
            34 => "预收账款",
            35 => "应付职工薪酬",
            36 => "应交税费",
            37 => "应付利息",
            38 => "应付利润",
            39 => "其他应付款",
            40 => "其他流动负债",
            42 => "长期借款",
            43 => "长期应付款",
            44 => "递延收益",
            45 => "其他非流动负债",
            48 => "实收资本（或股本）",
            49 => "资本公积",
            50 => "盈余公积",
            51 => "未分配利润",
            _ => $"行次{line}",
        };

        private static BalanceSheetExportRow Section(string label)
            => new(label, null, null, null, BalanceSheetRowKind.Section);

        private static BalanceSheetExportRow Blank()
            => new(string.Empty, null, null, null, BalanceSheetRowKind.Section);

        private static BalanceSheetExportRow Item(
            string label, int line, Dictionary<int, (decimal Ending, decimal Opening)> map)
            => new(label, line, Amount(map, line, true), Amount(map, line, false), BalanceSheetRowKind.Item);

        private static BalanceSheetExportRow Detail(
            string label, int line, Dictionary<int, (decimal Ending, decimal Opening)> map)
            => new(label, line, Amount(map, line, true), Amount(map, line, false), BalanceSheetRowKind.Detail);

        private static BalanceSheetExportRow Total(string label, int line, (decimal Ending, decimal Opening) amount)
            => new(label, line, amount.Ending, amount.Opening, BalanceSheetRowKind.Total);

        private static decimal Amount(
            Dictionary<int, (decimal Ending, decimal Opening)> map, int line, bool ending)
        {
            if (!map.TryGetValue(line, out var value))
            {
                return 0m;
            }

            return ending ? value.Ending : value.Opening;
        }

        private static (decimal Ending, decimal Opening) Sum(
            Dictionary<int, (decimal Ending, decimal Opening)> map, int first, int last)
        {
            decimal ending = 0m, opening = 0m;
            for (var line = first; line <= last; line++)
            {
                ending += Amount(map, line, true);
                opening += Amount(map, line, false);
            }

            return (ending, opening);
        }

        private static (decimal Ending, decimal Opening) Add(
            (decimal Ending, decimal Opening) left, (decimal Ending, decimal Opening) right)
        {
            return (left.Ending + right.Ending, left.Opening + right.Opening);
        }
    }
}
