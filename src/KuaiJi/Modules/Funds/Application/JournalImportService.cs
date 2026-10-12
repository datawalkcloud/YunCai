using KuaiJi.Modules.Funds.Domain;

namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>
    /// 日记账导入应用服务：将上传文件解析为流水行后，逐行登记到指定日记账。
    /// 非法行（如摘要为空、收支均为零或均非零、日期缺失）跳过并告警，不阻断整体导入。
    /// 文件中未填收支类别的行，按类别的智能匹配关键字与摘要做包含匹配自动归类
    /// （同类型类别中命中关键字最长者优先，命中多个类别时取先加载到的）。
    /// </summary>
    public sealed class JournalImportService
    {
        private readonly IJournalRepository _journalRepository;
        private readonly IJournalImportReader _reader;
        private readonly IEntryCategoryRepository _categoryRepository;

        public JournalImportService(
            IJournalRepository journalRepository,
            IJournalImportReader reader,
            IEntryCategoryRepository categoryRepository)
        {
            _journalRepository = journalRepository;
            _reader = reader;
            _categoryRepository = categoryRepository;
        }

        /// <summary>
        /// 导入流水到指定日记账。日记账不存在时抛出 <see cref="JournalImportException"/>。
        /// </summary>
        public async Task<JournalImportResult> ImportAsync(Guid accountSetId, Guid journalId, Stream stream, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, journalId, cancellationToken)
                          ?? throw new JournalImportException("指定的日记账不存在。");

            if (!journal.IsEnabled)
            {
                throw new JournalImportException($"账户「{journal.Name}」已停用，请先启用后再导入流水。");
            }

            var rows = _reader.Read(stream);
            var categories = await _categoryRepository.GetAllAsync(accountSetId, cancellationToken);

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

                // 文件中未填类别时，按摘要与类别智能匹配关键字自动归类。
                var category = row.Category;
                if (string.IsNullOrWhiteSpace(category))
                {
                    var matched = MatchCategory(categories, row.Summary, row.Income > 0m);
                    if (matched is not null)
                    {
                        category = matched.Name;
                        warnings.Add($"第 {row.RowNumber} 行：摘要命中「{matched.Name}」的智能匹配关键字，已自动归类。");
                    }
                }

                try
                {
                    journal.AddEntry(row.Date, row.Summary, row.Income, row.Expense, category, row.Counterparty);
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

        /// <summary>
        /// 按摘要包含关系在指定方向的类别中查找命中关键字最长的类别。
        /// isIncome 为 true 时只匹配收入类别，否则只匹配支出类别。
        /// </summary>
        private static EntryCategory? MatchCategory(IReadOnlyList<EntryCategory> categories, string summary, bool isIncome)
        {
            var expectedType = isIncome ? EntryCategoryType.Income : EntryCategoryType.Expense;
            EntryCategory? best = null;
            var bestLength = 0;

            foreach (var category in categories)
            {
                if (category.Type != expectedType)
                {
                    continue;
                }

                foreach (var keyword in category.KeywordList())
                {
                    if (keyword.Length > bestLength && summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        best = category;
                        bestLength = keyword.Length;
                    }
                }
            }

            return best;
        }
    }
}
