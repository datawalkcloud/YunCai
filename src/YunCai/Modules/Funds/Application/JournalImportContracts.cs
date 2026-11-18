namespace YunCai.Modules.Funds.Application
{
    /// <summary>
    /// 日记账导入文件读取器。将上传的文件流解析为原始流水行集合，屏蔽具体文件格式（如 .xlsx）。
    /// </summary>
    public interface IJournalImportReader
    {
        /// <summary>
        /// 从文件流解析出全部原始流水行。解析失败（格式不符、缺列等）时抛出 <see cref="JournalImportException"/>。
        /// </summary>
        IReadOnlyList<ImportedJournalRow> Read(Stream stream);
    }

    /// <summary>日记账导入过程中的可预期错误（如文件格式不符、内容缺失），用于向前端返回友好提示。</summary>
    public sealed class JournalImportException : Exception
    {
        public JournalImportException(string message) : base(message)
        {
        }
    }

    /// <summary>从导入文件解析出的一条原始日记账流水行（未经业务校验）。</summary>
    public sealed class ImportedJournalRow
    {
        /// <summary>数据行序号（不含表头，自 1 递增），用于告警定位。</summary>
        public int RowNumber { get; init; }

        public DateOnly Date { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string? Category { get; init; }

        public string? Counterparty { get; init; }

        public decimal Income { get; init; }

        public decimal Expense { get; init; }
    }

    /// <summary>日记账导入结果汇总，返回给前端展示。</summary>
    public sealed class JournalImportResult
    {
        /// <summary>解析到的原始数据行数（不含表头）。</summary>
        public int TotalRows { get; init; }

        /// <summary>成功导入的分录条数。</summary>
        public int ImportedEntries { get; init; }

        /// <summary>因内容非法而跳过的行数。</summary>
        public int SkippedRows { get; init; }

        /// <summary>过程中的告警信息（如被跳过的行），不阻断导入。</summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }
}
