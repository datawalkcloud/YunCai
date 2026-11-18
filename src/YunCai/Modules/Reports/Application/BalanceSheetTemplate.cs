using YunCai.Modules.AccountSets.Domain;

namespace YunCai.Modules.Reports.Application
{
    /// <summary>资产负债表版式。由账套选用的会计标准决定，。</summary>
    public enum BalanceSheetTemplate
    {
        /// <summary>企业会计准则（2007）：会企01表。</summary>
        Enterprise = 1,

        /// <summary>小企业会计准则（2011）：会小企01表。</summary>
        SmallBusiness = 2,
    }

    public static class BalanceSheetTemplates
    {
        /// <summary>按账套的会计标准选版式；民间非营利组织暂沿用会企01表。</summary>
        public static BalanceSheetTemplate Resolve(AccountingStandard standard)
            => standard == AccountingStandard.SmallBusiness
                ? BalanceSheetTemplate.SmallBusiness
                : BalanceSheetTemplate.Enterprise;

        /// <summary>版式角标（报表右上角与导出抬头第二行）。</summary>
        public static string Title(BalanceSheetTemplate template)
            => template == BalanceSheetTemplate.SmallBusiness ? "会小企01表" : "会企01表";
    }
}
