using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>币别口径。一期只有本位币，其余两值为外币核算批次预留（传非 Base 时按 Base 处理并在 DTO 标注）。</summary>
    public enum LedgerCurrencyScope
    {
        /// <summary>综合本位币。</summary>
        Base = 0,

        /// <summary>所有币别。</summary>
        All = 1,

        /// <summary>仅人民币。</summary>
        Rmb = 2,
    }

    /// <summary>账簿排序依据。</summary>
    public enum LedgerSortBy
    {
        /// <summary>按凭证字号（凭证字 → 凭证号 → 日期 → 行号）。</summary>
        VoucherNumber = 0,

        /// <summary>按凭证日期（日期 → 凭证字 → 凭证号 → 行号）。</summary>
        VoucherDate = 1,
    }

    /// <summary>
    /// 账簿查询条件。九张账页共用同一个条件模型，缺省值即「不做额外裁剪」，
    /// 与柠檬云那张统一的「查询条件」面板一一对应。
    /// </summary>
    public sealed record LedgerQueryOptions
    {
        /// <summary>起始日期；装载时会归一到所在月首日。</summary>
        public DateOnly? StartDate { get; init; }

        /// <summary>截止日期；装载时会归一到所在月末日。</summary>
        public DateOnly? EndDate { get; init; }

        /// <summary>
        /// 科目条件，支持逗号与区间串（如「1001,1122,1501-1503」）；为空表示全部科目。
        /// 命中语义为前缀匹配：选中 1122 即带出 112201 等下级。
        /// </summary>
        public string? AccountFrom { get; init; }

        /// <summary>科目区间终点（与 <see cref="AccountFrom"/> 组合成显式起止时使用）。</summary>
        public string? AccountTo { get; init; }

        /// <summary>科目级次上限；为空表示不限。</summary>
        public int? MaxLevel { get; init; }

        /// <summary>币别口径。</summary>
        public LedgerCurrencyScope Currency { get; init; } = LedgerCurrencyScope.Base;

        /// <summary>摘要关键字（包含匹配）。</summary>
        public string? SummaryKeyword { get; init; }

        /// <summary>排序依据。</summary>
        public LedgerSortBy SortBy { get; init; } = LedgerSortBy.VoucherNumber;

        /// <summary>是否显示辅助核算列。</summary>
        public bool ShowAssist { get; init; }

        /// <summary>是否只显示最明细科目。</summary>
        public bool ShowOnlyLeaf { get; init; } = true;

        /// <summary>是否显示对方科目列。</summary>
        public bool ShowCounterAccount { get; init; }

        /// <summary>余额为 0 的科目不显示。</summary>
        public bool HideZeroBalance { get; init; }

        /// <summary>无发生额且余额为 0 不显示。</summary>
        public bool HideNoActivity { get; init; }

        /// <summary>是否列示全部科目（含本期无发生额的启用科目）。默认假，行为与既有科目余额表一致。</summary>
        public bool IncludeIdleAccounts { get; init; }

        /// <summary>
        /// 数量金额式。依赖尚未实现的数量核算，一期恒为假，仅保留字段以免日后重写九张账页的条件模型。
        /// </summary>
        public bool ShowQtyAmount { get; init; }

        /// <summary>核算项目类账页的主维度。</summary>
        public AssistType? AssistType { get; init; }

        /// <summary>核算项目组合表的次维度（与主维度交叉）。</summary>
        public AssistType? SecondaryAssistType { get; init; }

        /// <summary>主维度下限定的具体档案；为空表示该维度全部档案。</summary>
        public Guid? AssistItemId { get; init; }

        /// <summary>多栏账的单科目（其直接下级科目展开成栏）。</summary>
        public Guid? SingleAccountId { get; init; }

        /// <summary>
        /// 合并成 <see cref="AccountCodeSelector"/> 可解析的条件串。
        /// 起止编码等长时视为闭区间（「1001-1122」）；不等长时视为两个独立前缀条件（起点带下级、终点仅精确匹配）。
        /// </summary>
        public string? AccountExpression
        {
            get
            {
                var from = AccountFrom?.Trim();
                var to = AccountTo?.Trim();
                if (string.IsNullOrEmpty(to))
                {
                    return from;
                }

                if (string.IsNullOrEmpty(from))
                {
                    return to;
                }

                return from.Length == to.Length ? $"{from}-{to}" : $"{from},{to}";
            }
        }

        /// <summary>校验条件本身的合法性，返回错误文案；合法时返回 null。</summary>
        public string? Validate()
        {
            if (StartDate is { } start && EndDate is { } end && start > end)
            {
                return "起始日期不能晚于截止日期。";
            }

            if (MaxLevel is { } level && level < 1)
            {
                return "科目级次必须不小于 1。";
            }

            if (ShowQtyAmount)
            {
                return "数量金额式尚未启用，请先在科目上设置计量单位。";
            }

            if (AssistType is { } type && !Enum.IsDefined(type))
            {
                return "辅助核算类别不合法。";
            }

            if (SecondaryAssistType is { } secondary && !Enum.IsDefined(secondary))
            {
                return "次辅助核算类别不合法。";
            }

            if (SecondaryAssistType is { } second && second == AssistType)
            {
                return "组合表的主、次辅助核算类别不能相同。";
            }

            if (!string.IsNullOrWhiteSpace(AccountTo) && AccountFrom?.Contains('-') == true)
            {
                return "填写了终止科目时，起始科目请只填编码，不要再写区间。";
            }

            return AccountCodeSelector.Validate(AccountExpression);
        }
    }
}
