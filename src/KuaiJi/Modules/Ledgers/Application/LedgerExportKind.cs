namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>可导出的账页种类，与查询接口的路径段一一对应。</summary>
    public enum LedgerExportKind
    {
        /// <summary>明细账。</summary>
        Subsidiary = 0,

        /// <summary>总账。</summary>
        General = 1,

        /// <summary>凭证序时账。</summary>
        Journal = 2,

        /// <summary>凭证汇总表。</summary>
        VoucherSummary = 3,

        /// <summary>核算项目余额表。</summary>
        AssistBalance = 4,

        /// <summary>核算项目明细账。</summary>
        AssistSubsidiary = 5,

        /// <summary>核算项目组合表。</summary>
        AssistCombine = 6,

        /// <summary>多栏账。</summary>
        MultiColumn = 7,

        /// <summary>账龄分析表。</summary>
        Ageing = 8,
    }

    /// <summary>账页导出种类的路径段与中文文案。</summary>
    public static class LedgerExportKindText
    {
        /// <summary>解析路径段（如 voucher-summary）；不识别时返回假。</summary>
        public static bool TryParse(string? kind, out LedgerExportKind exportKind)
        {
            var key = (kind ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            exportKind = key switch
            {
                "subsidiary" => LedgerExportKind.Subsidiary,
                "general" => LedgerExportKind.General,
                "journal" => LedgerExportKind.Journal,
                "vouchersummary" => LedgerExportKind.VoucherSummary,
                "assistbalance" => LedgerExportKind.AssistBalance,
                "assistsubsidiary" => LedgerExportKind.AssistSubsidiary,
                "assistcombine" => LedgerExportKind.AssistCombine,
                "multicolumn" => LedgerExportKind.MultiColumn,
                "ageing" => LedgerExportKind.Ageing,
                _ => default,
            };

            return key is "subsidiary" or "general" or "journal" or "vouchersummary" or "assistbalance"
                or "assistsubsidiary" or "assistcombine" or "multicolumn" or "ageing";
        }

        /// <summary>中文账页名，同时用作工作表名与文件名前缀。</summary>
        public static string Of(LedgerExportKind kind) => kind switch
        {
            LedgerExportKind.Subsidiary => "明细账",
            LedgerExportKind.General => "总账",
            LedgerExportKind.Journal => "序时账",
            LedgerExportKind.VoucherSummary => "凭证汇总表",
            LedgerExportKind.AssistBalance => "核算项目余额表",
            LedgerExportKind.AssistSubsidiary => "核算项目明细账",
            LedgerExportKind.AssistCombine => "核算项目组合表",
            LedgerExportKind.MultiColumn => "多栏账",
            LedgerExportKind.Ageing => "账龄分析表",
            _ => kind.ToString(),
        };
    }
}
