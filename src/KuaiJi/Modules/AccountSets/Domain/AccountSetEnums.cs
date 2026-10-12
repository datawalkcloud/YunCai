namespace KuaiJi.Modules.AccountSets.Domain
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
}
