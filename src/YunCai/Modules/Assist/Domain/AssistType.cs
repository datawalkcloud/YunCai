namespace YunCai.Modules.Assist.Domain
{
    /// <summary>
    /// 辅助核算类别（核算项目维度）。与账簿中的「核算项目明细账/余额表/组合表」以及
    /// 现金流量表取数一一对应，类别集合固定为七类，不提供自定义类别。
    /// </summary>
    public enum AssistType
    {
        /// <summary>客户。</summary>
        Customer = 1,

        /// <summary>供应商。</summary>
        Vendor = 2,

        /// <summary>职员。</summary>
        Employee = 3,

        /// <summary>部门。</summary>
        Department = 4,

        /// <summary>项目。</summary>
        Project = 5,

        /// <summary>存货。</summary>
        Inventory = 6,

        /// <summary>现金流（现金流量项目，仅挂在现金类科目分录上）。</summary>
        CashFlow = 7,
    }

    /// <summary>指向某个辅助核算档案的引用：维度 + 档案标识。凭证分录与账簿聚合共用此结构。</summary>
    public readonly record struct AssistRef(AssistType Type, Guid ItemId);

    /// <summary>辅助核算类别的中文文案与固有属性，供 DTO、账簿表头与导出共用，避免各处重复 switch。</summary>
    public static class AssistTypeText
    {
        /// <summary>全部类别（按展示顺序）。</summary>
        public static readonly AssistType[] All =
        [
            AssistType.Customer,
            AssistType.Vendor,
            AssistType.Employee,
            AssistType.Department,
            AssistType.Project,
            AssistType.Inventory,
            AssistType.CashFlow,
        ];

        /// <summary>类别中文名。</summary>
        public static string Of(AssistType type) => type switch
        {
            AssistType.Customer => "客户",
            AssistType.Vendor => "供应商",
            AssistType.Employee => "职员",
            AssistType.Department => "部门",
            AssistType.Project => "项目",
            AssistType.Inventory => "存货",
            AssistType.CashFlow => "现金流",
            _ => type.ToString(),
        };

        /// <summary>
        /// 该类别的档案是否需要层级（部门/项目/存货/现金流 可建树，其余档案一律平铺）。
        /// 平铺类别的档案参与记账时不强制末级（本身即为末级）。
        /// </summary>
        public static bool SupportsHierarchy(AssistType type) =>
            type is AssistType.Department or AssistType.Project or AssistType.Inventory or AssistType.CashFlow;
    }
}
