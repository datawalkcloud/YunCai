namespace KuaiJi.Application.Vouchers
{
    /// <summary>导入时遇到已存在凭证的冲突处理策略。</summary>
    public enum VoucherImportConflict
    {
        /// <summary>跳过已存在的凭证，只导入新增的。</summary>
        Skip = 0,

        /// <summary>删除已有凭证后重新导入。</summary>
        Overwrite = 1,
    }

    /// <summary>凭证导入结果汇总，返回给前端展示。</summary>
    public sealed class VoucherImportResult
    {
        /// <summary>解析到的原始数据行数（不含表头）。</summary>
        public int TotalRows { get; init; }

        /// <summary>成功导入的凭证张数。</summary>
        public int ImportedVouchers { get; init; }

        /// <summary>成功导入的分录条数。</summary>
        public int ImportedEntries { get; init; }

        /// <summary>因已存在而跳过的凭证张数。</summary>
        public int SkippedVouchers { get; init; }

        /// <summary>因覆盖而被删除的旧凭证张数。</summary>
        public int OverwrittenVouchers { get; init; }

        /// <summary>新建的会计科目数量。</summary>
        public int CreatedAccounts { get; init; }

        /// <summary>过程中的告警信息（如借贷不平衡的凭证、被跳过的行等），不阻断导入。</summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }
}
