namespace YunCai.Modules.Accounting.Application
{
    /// <summary>科目导入结果汇总，返回给前端展示。</summary>
    public sealed class AccountImportResult
    {
        /// <summary>解析到的原始数据行数（不含表头）。</summary>
        public int TotalRows { get; init; }

        /// <summary>成功导入（新建）的科目数量。</summary>
        public int ImportedAccounts { get; init; }

        /// <summary>因编码已存在而跳过的科目数量。</summary>
        public int SkippedAccounts { get; init; }

        /// <summary>过程中的告警信息（如上级不存在、类别无法识别、被跳过的行等），不阻断导入。</summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }
}
