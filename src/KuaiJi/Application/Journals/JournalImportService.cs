using KuaiJi.Domain.Journals;

namespace KuaiJi.Application.Journals
{
    /// <summary>
    /// 日记账导入应用服务：将上传文件解析为流水行后，逐行登记到指定日记账。
    /// 非法行（如摘要为空、收支均为零或均非零、日期缺失）跳过并告警，不阻断整体导入。
    /// </summary>
    public sealed class JournalImportService
    {
        private readonly IJournalRepository _journalRepository;
        private readonly IJournalImportReader _reader;

        public JournalImportService(IJournalRepository journalRepository, IJournalImportReader reader)
        {
            _journalRepository = journalRepository;
            _reader = reader;
        }

        /// <summary>
        /// 导入流水到指定日记账。日记账不存在时抛出 <see cref="JournalImportException"/>。
        /// </summary>
        public async Task<JournalImportResult> ImportAsync(Guid accountSetId, Guid journalId, Stream stream, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, journalId, cancellationToken)
                          ?? throw new JournalImportException("指定的日记账不存在。");

            var rows = _reader.Read(stream);

            var warnings = new List<string>();
            var imported = 0;
            var skipped = 0;

            foreach (var row in rows)
            {
                if (row.Date == default)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：日期缺失或格式不正确，已跳过。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.Summary))
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：摘要为空，已跳过。");
                    continue;
                }

                if (row.Income < 0m || row.Expense < 0m)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：收入或支出为负数，已跳过。");
                    continue;
                }

                if (row.Income == 0m && row.Expense == 0m)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：收入与支出均为零，已跳过。");
                    continue;
                }

                try
                {
                    journal.AddEntry(row.Date, row.Summary, row.Income, row.Expense, row.Category, row.Counterparty);
                    imported++;
                }
                catch (ArgumentException ex)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：{ex.Message}，已跳过。");
                }
            }

            if (imported > 0)
            {
                await _journalRepository.SaveChangesAsync(cancellationToken);
            }

            return new JournalImportResult
            {
                TotalRows = rows.Count,
                ImportedEntries = imported,
                SkippedRows = skipped,
                Warnings = warnings,
            };
        }
    }
}
