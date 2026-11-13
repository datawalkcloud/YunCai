namespace YunCai.Modules.AccountSets.Domain
{
    /// <summary>
    /// 会计准则（会计制度）。决定账套采用的科目体系与报表格式。
    /// </summary>
    public enum AccountingStandard
    {
        /// <summary>企业会计准则。</summary>
        Enterprise = 1,

        /// <summary>小企业会计准则。</summary>
        SmallBusiness = 2,

        /// <summary>民间非营利组织会计制度。</summary>
        NonProfit = 3,
    }

    /// <summary>
    /// 纳税人性质。影响增值税核算方式。
    /// </summary>
    public enum TaxpayerType
    {
        /// <summary>一般纳税人。</summary>
        General = 1,

        /// <summary>小规模纳税人。</summary>
        SmallScale = 2,
    }

    /// <summary>
    /// 资产负债表取辅助核算余额的范围。往来类科目挂了多种辅助核算时，
    /// 只取供应商/客户能让报表的应收应付与往来对账单严格对应；取全部则把部门、项目等维度一并计入。
    /// </summary>
    public enum BalanceAssistScope
    {
        /// <summary>仅供应商 / 客户辅助核算余额参与计算。</summary>
        VendorCustomerOnly = 1,

        /// <summary>所有辅助核算余额参与计算。</summary>
        All = 2,
    }

    /// <summary>
    /// 币别折算方式：原币金额折算为本位币金额的算法。
    /// 报价习惯为「1 外币 = 若干本位币」的用乘法（美元、欧元等），
    /// 习惯按「多少外币 = 1 本位币」报价的（如日元、韩元常按百元报价）可用除法。
    /// </summary>
    public enum CurrencyConvertType
    {
        /// <summary>原币 * 汇率 = 本位币。</summary>
        Multiply = 1,

        /// <summary>原币 / 汇率 = 本位币。</summary>
        Divide = 2,
    }
}
