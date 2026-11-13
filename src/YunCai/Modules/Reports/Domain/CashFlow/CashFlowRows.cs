using System.Globalization;

namespace YunCai.Modules.Reports.Domain.CashFlow
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

        /// <summary>
        /// 系统预置的现金流辅助项目名称 → 主表行次。预置项目编码是 01–18 的顺序号而非行号
        /// （如「03 购买原材料…」对应第 5 行、「04 支付的职工薪酬」对应第 6 行），
        /// 必须按名称归集；名称匹配不到时再沿用「编码前两位即行号」的旧约定。
        /// </summary>
        private static readonly IReadOnlyDictionary<string, int> AssistItemLinesByName =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["销售产成品、商品、提供劳务收到的现金"] = 1,
                ["销售商品、提供劳务收到的现金"] = 1,
                ["收到的税费返还"] = 2,
                ["税费返还"] = 2,
                ["收到其他与经营活动有关的现金"] = 3,
                ["购买原材料、商品、接受劳务支付的现金"] = 5,
                ["购买商品、接受劳务支付的现金"] = 5,
                ["支付的职工薪酬"] = 6,
                ["支付给职工以及为职工支付的现金"] = 6,
                ["支付的税费"] = 7,
                ["支付的各项税费"] = 7,
                ["支付其他与经营活动有关的现金"] = 8,
                ["收回短期投资、长期债券投资和长期股权投资收到的现金"] = 11,
                ["收回投资收到的现金"] = 11,
                ["取得投资收益收到的现金"] = 12,
                ["处置固定资产、无形资产和其他非流动资产收回的现金净额"] = 13,
                ["处置固定资产、无形资产和其他长期资产收回的现金净额"] = 13,
                ["收到其他与投资活动有关的现金"] = 15,
                ["购建固定资产、无形资产和其他非流动资产支付的现金"] = 17,
                ["购建固定资产、无形资产和其他长期资产支付的现金"] = 17,
                ["短期投资、长期债券投资和长期股权投资支付的现金"] = 18,
                ["投资支付的现金"] = 18,
                ["支付其他与投资活动有关的现金"] = 20,
                ["吸收投资者投资收到的现金"] = 23,
                ["吸收投资收到的现金"] = 23,
                ["取得借款收到的现金"] = 24,
                ["收到其他与筹资活动有关的现金"] = 25,
                ["偿还借款本金支付的现金"] = 27,
                ["偿还债务支付的现金"] = 27,
                ["偿还借款利息支付的现金"] = 28,
                ["分配利润支付的现金"] = 28,
                ["分配股利、利润或偿付利息支付的现金"] = 28,
                ["支付其他与筹资活动有关的现金"] = 29,
            };

        /// <summary>不参与主表归集的预置项目（内部划转、期初余额），即使名称或编码碰巧可解析也不能入行。</summary>
        private static readonly IReadOnlySet<string> AssistItemIgnoredNames =
            new HashSet<string>(StringComparer.Ordinal) { "现金内部流转", "期初现金余额" };

        /// <summary>
        /// 现金流辅助项目 → 主表行次：先按预置名称精确匹配，再退回「编码前两位即行号」，
        /// 只接受可挂项目的流入/流出明细行；内部划转、期初余额与无法解析的项目返回 null。
        /// </summary>
        public static int? ResolveAssistItemLine(string? code, string? name)
        {
            var key = NormalizeAssistName(name);
            if (AssistItemIgnoredNames.Contains(key))
            {
                return null;
            }

            if (AssistItemLinesByName.TryGetValue(key, out var byName))
            {
                return byName;
            }

            var trimmed = (code ?? string.Empty).Trim();
            var digits = trimmed.Length >= 2 ? trimmed[..2] : trimmed;
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var line))
            {
                return null;
            }

            return ByLine.TryGetValue(line, out var def)
                   && def.Kind is CashFlowRowKind.Inflow or CashFlowRowKind.Outflow
                ? line
                : null;
        }

        private static string NormalizeAssistName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var chars = name.Where(c => !char.IsWhiteSpace(c) && c != '　').ToArray();
            return new string(chars).Trim();
        }

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
