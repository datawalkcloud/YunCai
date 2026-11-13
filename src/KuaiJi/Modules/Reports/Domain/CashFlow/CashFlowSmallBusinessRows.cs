using System.Globalization;

namespace KuaiJi.Modules.Reports.Domain.CashFlow
{
    /// <summary>
    /// 小企业会计准则现金流量表（会小企03表）标准行次：22 行，不含流入/流出小计、
    /// 汇率变动影响与补充资料；筹资流出拆分为偿还借款本金、偿还借款利息、分配利润三行。
    /// </summary>
    public static class CashFlowSmallBusinessRows
    {
        public static readonly IReadOnlyList<CashFlowRowDef> All = new List<CashFlowRowDef>
        {
            new(1, "销售产成品、商品、提供劳务收到的现金", CashFlowSection.Operating, CashFlowRowKind.Inflow),
            new(2, "收到其他与经营活动有关的现金", CashFlowSection.Operating, CashFlowRowKind.Inflow),
            new(3, "购买原材料、商品、接受劳务支付的现金", CashFlowSection.Operating, CashFlowRowKind.Outflow),
            new(4, "支付的职工薪酬", CashFlowSection.Operating, CashFlowRowKind.Outflow),
            new(5, "支付的税费", CashFlowSection.Operating, CashFlowRowKind.Outflow),
            new(6, "支付其他与经营活动有关的现金", CashFlowSection.Operating, CashFlowRowKind.Outflow),
            new(7, "经营活动产生的现金流量净额", CashFlowSection.Operating, CashFlowRowKind.Subtotal),
            new(8, "收回短期投资、长期债券投资和长期股权投资收到的现金", CashFlowSection.Investing, CashFlowRowKind.Inflow),
            new(9, "取得投资收益收到的现金", CashFlowSection.Investing, CashFlowRowKind.Inflow),
            new(10, "处置固定资产、无形资产和其他非流动资产收回的现金净额", CashFlowSection.Investing, CashFlowRowKind.Inflow),
            new(11, "短期投资、长期债券投资和长期股权投资支付的现金", CashFlowSection.Investing, CashFlowRowKind.Outflow),
            new(12, "购建固定资产、无形资产和其他非流动资产支付的现金", CashFlowSection.Investing, CashFlowRowKind.Outflow),
            new(13, "投资活动产生的现金流量净额", CashFlowSection.Investing, CashFlowRowKind.Subtotal),
            new(14, "取得借款收到的现金", CashFlowSection.Financing, CashFlowRowKind.Inflow),
            new(15, "吸收投资者投资收到的现金", CashFlowSection.Financing, CashFlowRowKind.Inflow),
            new(16, "偿还借款本金支付的现金", CashFlowSection.Financing, CashFlowRowKind.Outflow),
            new(17, "偿还借款利息支付的现金", CashFlowSection.Financing, CashFlowRowKind.Outflow),
            new(18, "分配利润支付的现金", CashFlowSection.Financing, CashFlowRowKind.Outflow),
            new(19, "筹资活动产生的现金流量净额", CashFlowSection.Financing, CashFlowRowKind.Subtotal),
            new(20, "四、现金净增加额", null, CashFlowRowKind.NetIncrease),
            new(21, "加：期初现金余额", null, CashFlowRowKind.OpeningBalance),
            new(22, "五、期末现金余额", null, CashFlowRowKind.EndingBalance),
        };

        public static readonly IReadOnlyDictionary<int, CashFlowRowDef> ByLine =
            All.ToDictionary(r => r.Line, r => r);

        /// <summary>
        /// 一般企业主表行次 → 会小企03表行次。小准则没有税费返还、投资/筹资「其他」收支与流入流出小计，
        /// 这些行次按性质并入最接近的明细行；筹资第 28 行需先按对手方拆出利息（见 CashFlowClassifier）。
        /// </summary>
        public static int FromEnterpriseLine(int enterpriseLine) => enterpriseLine switch
        {
            1 => 1,
            2 or 3 => 2,      // 税费返还、其他经营流入 → 收到其他与经营活动有关的现金
            5 => 3,
            6 => 4,
            7 => 5,
            8 => 6,
            11 => 8,
            12 => 9,
            13 or 14 or 15 => 10,
            17 => 12,
            18 or 19 or 20 => 11,
            23 or 25 => 15,
            24 => 14,
            27 => 16,
            28 or 29 => 18,
            _ => throw new ArgumentOutOfRangeException(nameof(enterpriseLine), enterpriseLine, "一般企业现金流量表的汇总行次不能映射到小企业明细行。"),
        };

        /// <summary>预置现金流辅助项目名称 → 会小企03表行次（名称按小准则项目名，另兼容一般企业名称）。</summary>
        private static readonly IReadOnlyDictionary<string, int> AssistItemLinesByName =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["销售产成品、商品、提供劳务收到的现金"] = 1,
                ["销售商品、提供劳务收到的现金"] = 1,
                ["收到的税费返还"] = 2,
                ["税费返还"] = 2,
                ["收到其他与经营活动有关的现金"] = 2,
                ["购买原材料、商品、接受劳务支付的现金"] = 3,
                ["购买商品、接受劳务支付的现金"] = 3,
                ["支付的职工薪酬"] = 4,
                ["支付给职工以及为职工支付的现金"] = 4,
                ["支付的税费"] = 5,
                ["支付的各项税费"] = 5,
                ["支付其他与经营活动有关的现金"] = 6,
                ["收回短期投资、长期债券投资和长期股权投资收到的现金"] = 8,
                ["收回投资收到的现金"] = 8,
                ["取得投资收益收到的现金"] = 9,
                ["处置固定资产、无形资产和其他非流动资产收回的现金净额"] = 10,
                ["处置固定资产、无形资产和其他长期资产收回的现金净额"] = 10,
                ["处置子公司及其他营业单位收到的现金净额"] = 10,
                ["收到其他与投资活动有关的现金"] = 10,
                ["短期投资、长期债券投资和长期股权投资支付的现金"] = 11,
                ["投资支付的现金"] = 11,
                ["取得子公司及其他营业单位支付的现金净额"] = 11,
                ["支付其他与投资活动有关的现金"] = 11,
                ["购建固定资产、无形资产和其他非流动资产支付的现金"] = 12,
                ["购建固定资产、无形资产和其他长期资产支付的现金"] = 12,
                ["取得借款收到的现金"] = 14,
                ["吸收投资者投资收到的现金"] = 15,
                ["吸收投资收到的现金"] = 15,
                ["收到其他与筹资活动有关的现金"] = 15,
                ["偿还借款本金支付的现金"] = 16,
                ["偿还债务支付的现金"] = 16,
                ["偿还借款利息支付的现金"] = 17,
                ["分配利润支付的现金"] = 18,
                ["分配股利、利润或偿付利息支付的现金"] = 18,
                ["支付其他与筹资活动有关的现金"] = 18,
            };

        private static readonly IReadOnlySet<string> IgnoredNames =
            new HashSet<string>(StringComparer.Ordinal) { "现金内部流转", "期初现金余额" };

        /// <summary>
        /// 辅助项目 → 会小企03表行次：先按预置名称匹配，名称未知时把编码前两位当小准则行号解析；
        /// 只接受流入/流出明细行，汇总行、期初余额与内部划转返回 null。
        /// </summary>
        public static int? ResolveAssistItemLine(string? code, string? name)
        {
            var key = NormalizeName(name);
            if (IgnoredNames.Contains(key))
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

        private static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var chars = name.Where(c => !char.IsWhiteSpace(c) && c != '　').ToArray();
            return new string(chars).Trim();
        }

        public static IReadOnlyList<int> InflowLines(CashFlowSection section) =>
            All.Where(r => r.Section == section && r.Kind == CashFlowRowKind.Inflow).Select(r => r.Line).ToList();

        public static IReadOnlyList<int> OutflowLines(CashFlowSection section) =>
            All.Where(r => r.Section == section && r.Kind == CashFlowRowKind.Outflow).Select(r => r.Line).ToList();

        /// <summary>某活动净额行（经营 7 / 投资 13 / 筹资 19）：无流入流出小计，由明细行直接相减。</summary>
        public static int NetLine(CashFlowSection section) => section switch
        {
            CashFlowSection.Operating => 7,
            CashFlowSection.Investing => 13,
            CashFlowSection.Financing => 19,
            _ => throw new ArgumentOutOfRangeException(nameof(section)),
        };

        public const int NetIncreaseLine = 20;

        public const int OpeningCashLine = 21;

        public const int EndingCashLine = 22;
    }
}
