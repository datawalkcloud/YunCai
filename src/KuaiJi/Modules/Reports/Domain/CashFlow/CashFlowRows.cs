namespace KuaiJi.Modules.Reports.Domain.CashFlow
{
    /// <summary>现金流量表活动分类。</summary>
    public enum CashFlowSection
    {
        /// <summary>经营活动。</summary>
        Operating = 1,

        /// <summary>投资活动。</summary>
        Investing = 2,

        /// <summary>筹资活动。</summary>
        Financing = 3,
    }

    /// <summary>现金流量表行次的性质。</summary>
    public enum CashFlowRowKind
    {
        /// <summary>现金流入项。</summary>
        Inflow = 1,

        /// <summary>现金流出项。</summary>
        Outflow = 2,

        /// <summary>活动小计/净额行（由流入流出行汇总而来）。</summary>
        Subtotal = 3,

        /// <summary>汇率变动影响。</summary>
        ExchangeEffect = 4,

        /// <summary>现金及现金等价物净增加额。</summary>
        NetIncrease = 5,

        /// <summary>期初现金余额。</summary>
        OpeningBalance = 6,

        /// <summary>期末现金余额。</summary>
        EndingBalance = 7,
    }

    /// <summary>现金流量表的一个标准行次定义。</summary>
    public sealed record CashFlowRowDef(int Line, string Name, CashFlowSection? Section, CashFlowRowKind Kind);

    /// <summary>一般企业现金流量表（主表）标准行次。行次顺序与财政部主表格式一致。</summary>
    public static class CashFlowStandardRows
    {
        public static readonly IReadOnlyList<CashFlowRowDef> All = new List<CashFlowRowDef>
        {
            new(1, "销售商品、提供劳务收到的现金", CashFlowSection.Operating, CashFlowRowKind.Inflow),
            new(2, "收到的税费返还", CashFlowSection.Operating, CashFlowRowKind.Inflow),
            new(3, "收到其他与经营活动有关的现金", CashFlowSection.Operating, CashFlowRowKind.Inflow),
            new(4, "经营活动现金流入小计", CashFlowSection.Operating, CashFlowRowKind.Subtotal),
            new(5, "购买商品、接受劳务支付的现金", CashFlowSection.Operating, CashFlowRowKind.Outflow),
            new(6, "支付给职工以及为职工支付的现金", CashFlowSection.Operating, CashFlowRowKind.Outflow),
            new(7, "支付的各项税费", CashFlowSection.Operating, CashFlowRowKind.Outflow),
            new(8, "支付其他与经营活动有关的现金", CashFlowSection.Operating, CashFlowRowKind.Outflow),
            new(9, "经营活动现金流出小计", CashFlowSection.Operating, CashFlowRowKind.Subtotal),
            new(10, "经营活动产生的现金流量净额", CashFlowSection.Operating, CashFlowRowKind.Subtotal),
            new(11, "收回投资收到的现金", CashFlowSection.Investing, CashFlowRowKind.Inflow),
            new(12, "取得投资收益收到的现金", CashFlowSection.Investing, CashFlowRowKind.Inflow),
            new(13, "处置固定资产、无形资产和其他长期资产收回的现金净额", CashFlowSection.Investing, CashFlowRowKind.Inflow),
            new(14, "处置子公司及其他营业单位收到的现金净额", CashFlowSection.Investing, CashFlowRowKind.Inflow),
            new(15, "收到其他与投资活动有关的现金", CashFlowSection.Investing, CashFlowRowKind.Inflow),
            new(16, "投资活动现金流入小计", CashFlowSection.Investing, CashFlowRowKind.Subtotal),
            new(17, "购建固定资产、无形资产和其他长期资产支付的现金", CashFlowSection.Investing, CashFlowRowKind.Outflow),
            new(18, "投资支付的现金", CashFlowSection.Investing, CashFlowRowKind.Outflow),
            new(19, "取得子公司及其他营业单位支付的现金净额", CashFlowSection.Investing, CashFlowRowKind.Outflow),
            new(20, "支付其他与投资活动有关的现金", CashFlowSection.Investing, CashFlowRowKind.Outflow),
            new(21, "投资活动现金流出小计", CashFlowSection.Investing, CashFlowRowKind.Subtotal),
            new(22, "投资活动产生的现金流量净额", CashFlowSection.Investing, CashFlowRowKind.Subtotal),
            new(23, "吸收投资收到的现金", CashFlowSection.Financing, CashFlowRowKind.Inflow),
            new(24, "取得借款收到的现金", CashFlowSection.Financing, CashFlowRowKind.Inflow),
            new(25, "收到其他与筹资活动有关的现金", CashFlowSection.Financing, CashFlowRowKind.Inflow),
            new(26, "筹资活动现金流入小计", CashFlowSection.Financing, CashFlowRowKind.Subtotal),
            new(27, "偿还债务支付的现金", CashFlowSection.Financing, CashFlowRowKind.Outflow),
            new(28, "分配股利、利润或偿付利息支付的现金", CashFlowSection.Financing, CashFlowRowKind.Outflow),
            new(29, "支付其他与筹资活动有关的现金", CashFlowSection.Financing, CashFlowRowKind.Outflow),
            new(30, "筹资活动现金流出小计", CashFlowSection.Financing, CashFlowRowKind.Subtotal),
            new(31, "筹资活动产生的现金流量净额", CashFlowSection.Financing, CashFlowRowKind.Subtotal),
            new(32, "四、汇率变动对现金及现金等价物的影响", null, CashFlowRowKind.ExchangeEffect),
            new(33, "五、现金及现金等价物净增加额", null, CashFlowRowKind.NetIncrease),
            new(34, "加：期初现金及现金等价物余额", null, CashFlowRowKind.OpeningBalance),
            new(35, "六、期末现金及现金等价物余额", null, CashFlowRowKind.EndingBalance),
        };

        /// <summary>行次 → 定义索引，便于服务层快速取行元数据。</summary>
        public static readonly IReadOnlyDictionary<int, CashFlowRowDef> ByLine =
            All.ToDictionary(r => r.Line, r => r);

        /// <summary>按行次归集后计算的小计行来源：流入小计/流出小计/净额各包含哪些明细行。</summary>
        public static IReadOnlyList<int> InflowLines(CashFlowSection section) =>
            All.Where(r => r.Section == section && r.Kind == CashFlowRowKind.Inflow).Select(r => r.Line).ToList();

        public static IReadOnlyList<int> OutflowLines(CashFlowSection section) =>
            All.Where(r => r.Section == section && r.Kind == CashFlowRowKind.Outflow).Select(r => r.Line).ToList();

        /// <summary>某活动小计行行号（流入小计 / 流出小计 / 净额）。</summary>
        public static (int InflowSubtotal, int OutflowSubtotal, int Net) SubtotalLines(CashFlowSection section)
        {
            return section switch
            {
                CashFlowSection.Operating => (4, 9, 10),
                CashFlowSection.Investing => (16, 21, 22),
                CashFlowSection.Financing => (26, 30, 31),
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
        }
    }
}
