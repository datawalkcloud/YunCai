namespace YunCai.Modules.Accounting.Domain
{
    /// <summary>
    /// 会计科目类别。对应《企业会计准则》的科目大类，决定科目在报表中的归属。
    /// </summary>
    public enum AccountCategory
    {
        /// <summary>资产。</summary>
        Asset = 1,

        /// <summary>负债。</summary>
        Liability = 2,

        /// <summary>权益（所有者权益）。</summary>
        Equity = 3,

        /// <summary>成本。</summary>
        Cost = 4,

        /// <summary>损益（收入、费用）。</summary>
        ProfitAndLoss = 5,
    }

    /// <summary>
    /// 科目余额方向（记账方向）。资产、成本类通常为借方；负债、权益类通常为贷方。
    /// </summary>
    public enum BalanceDirection
    {
        /// <summary>借方。</summary>
        Debit = 1,

        /// <summary>贷方。</summary>
        Credit = 2,
    }

    /// <summary>
    /// 科目二级类别：在 <see cref="AccountCategory"/> 五大类之下再分一档，供科目维护与筛选展示。
    /// 报表取数仍按大类与科目编码归集，本字段不参与取数口径。
    /// </summary>
    public enum AccountSubCategory
    {
        /// <summary>流动资产（资产类）。</summary>
        CurrentAsset = 1,

        /// <summary>非流动资产（资产类）。</summary>
        NonCurrentAsset = 2,

        /// <summary>流动负债（负债类）。</summary>
        CurrentLiability = 3,

        /// <summary>非流动负债（负债类）。</summary>
        NonCurrentLiability = 4,

        /// <summary>所有者权益（权益类）。</summary>
        OwnerEquity = 5,

        /// <summary>成本（成本类）。</summary>
        Cost = 6,

        /// <summary>收入（损益类）。</summary>
        Income = 7,

        /// <summary>费用（损益类）。</summary>
        Expense = 8,
    }

    /// <summary>
    /// 科目大类的中文名，供校验文案与导出复用。
    /// </summary>
    public static class AccountCategoryCatalog
    {
        public static string LabelOf(AccountCategory category) => category switch
        {
            AccountCategory.Asset => "资产",
            AccountCategory.Liability => "负债",
            AccountCategory.Equity => "权益",
            AccountCategory.Cost => "成本",
            AccountCategory.ProfitAndLoss => "损益",
            _ => category.ToString(),
        };
    }

    /// <summary>
    /// 科目大类与二级类别的对应关系：每个大类只允许挂本类下的二级类别，
    /// 权益与成本类各只有一个取值（等同没有细分，但保留展示口径）。
    /// </summary>
    public static class AccountSubCategoryCatalog
    {
        private static readonly Dictionary<AccountCategory, AccountSubCategory[]> OptionsByCategory = new()
        {
            [AccountCategory.Asset] = [AccountSubCategory.CurrentAsset, AccountSubCategory.NonCurrentAsset],
            [AccountCategory.Liability] = [AccountSubCategory.CurrentLiability, AccountSubCategory.NonCurrentLiability],
            [AccountCategory.Equity] = [AccountSubCategory.OwnerEquity],
            [AccountCategory.Cost] = [AccountSubCategory.Cost],
            [AccountCategory.ProfitAndLoss] = [AccountSubCategory.Income, AccountSubCategory.Expense],
        };

        private static readonly Dictionary<AccountSubCategory, string> Labels = new()
        {
            [AccountSubCategory.CurrentAsset] = "流动资产",
            [AccountSubCategory.NonCurrentAsset] = "非流动资产",
            [AccountSubCategory.CurrentLiability] = "流动负债",
            [AccountSubCategory.NonCurrentLiability] = "非流动负债",
            [AccountSubCategory.OwnerEquity] = "所有者权益",
            [AccountSubCategory.Cost] = "成本",
            [AccountSubCategory.Income] = "收入",
            [AccountSubCategory.Expense] = "费用",
        };

        /// <summary>该大类可选的二级类别；未知大类返回空集合。</summary>
        public static IReadOnlyList<AccountSubCategory> OptionsFor(AccountCategory category)
            => OptionsByCategory.TryGetValue(category, out var options) ? options : [];

        /// <summary>二级类别是否属于该大类。</summary>
        public static bool IsValid(AccountCategory category, AccountSubCategory subCategory)
            => OptionsFor(category).Contains(subCategory);

        /// <summary>二级类别中文名；未知取值返回空串。</summary>
        public static string LabelOf(AccountSubCategory subCategory)
            => Labels.TryGetValue(subCategory, out var label) ? label : string.Empty;
    }
}
