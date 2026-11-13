using System.Globalization;
using YunCai.Modules.Funds.Domain;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>资金账户导入的一行解析结果。</summary>
    public sealed class JournalAccountImportRow
    {
        public int Row { get; init; }

        public string Name { get; init; } = string.Empty;

        /// <summary>账户类型文本：现金 / 银行存款。</summary>
        public string TypeText { get; init; } = string.Empty;

        public string? Code { get; init; }

        public string? Currency { get; init; }

        public decimal OpeningBalance { get; init; }

        public string? BankName { get; init; }

        public string? BankAccountNo { get; init; }
    }

    /// <summary>资金账户导入失败行。</summary>
    public sealed class JournalAccountImportFailureDto
    {
        public int Row { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;
    }

    /// <summary>资金账户导入结果。</summary>
    public sealed class JournalAccountImportResultDto
    {
        public int Imported { get; init; }

        public int Updated { get; init; }

        public IReadOnlyList<JournalAccountImportFailureDto> Failures { get; init; } = [];
    }

    /// <summary>
    /// 资金账户批量导入：按 Excel（账簿名称 / 账户类型 / 账户编码 / 币别 / 期初余额 / 开户银行 / 银行账号）
    /// 建「现金 / 银行存款日记账」。同名同类型账户已存在时更新期初余额与开户信息，否则新建。
    /// </summary>
    public sealed class JournalAccountImportService
    {
        private readonly JournalService _journalService;
        private readonly IJournalRepository _journalRepository;

        public JournalAccountImportService(JournalService journalService, IJournalRepository journalRepository)
        {
            _journalService = journalService;
            _journalRepository = journalRepository;
        }

        public async Task<JournalAccountImportResultDto> ImportAsync(Guid accountSetId, Stream stream, CancellationToken cancellationToken = default)
        {
            var rows = ReadRows(stream);
            if (rows.Count == 0)
            {
                throw new JournalException("文件中没有可导入的数据行。");
            }

            var existing = await _journalRepository.GetAllAsync(accountSetId, cancellationToken);
            var failures = new List<JournalAccountImportFailureDto>();
            var imported = 0;
            var updated = 0;
            var seen = new HashSet<string>();

            foreach (var row in rows)
            {
                var type = ParseType(row.TypeText);
                if (type is null)
                {
                    failures.Add(new JournalAccountImportFailureDto { Row = row.Row, Name = row.Name, Reason = "账户类型需为「现金」或「银行存款」" });
                    continue;
                }

                var key = $"{(int)type}|{row.Name.Trim()}";
                if (!seen.Add(key))
                {
                    failures.Add(new JournalAccountImportFailureDto { Row = row.Row, Name = row.Name, Reason = "同一账户在文件中重复出现" });
                    continue;
                }

                try
                {
                    var match = existing.FirstOrDefault(j => j.Type == type && j.Name == row.Name.Trim());
                    if (match is null)
                    {
                        await _journalService.CreateAsync(accountSetId, new CreateJournalRequest
                        {
                            Type = (int)type,
                            Name = row.Name,
                            Code = row.Code,
                            Currency = row.Currency,
                            OpeningBalance = row.OpeningBalance,
                            BankName = row.BankName,
                            BankAccountNo = row.BankAccountNo,
                        }, cancellationToken);
                        imported++;
                    }
                    else
                    {
                        await _journalService.UpdateAsync(accountSetId, match.Id, new UpdateJournalRequest
                        {
                            Name = row.Name,
                            Code = string.IsNullOrWhiteSpace(row.Code) ? null : row.Code,
                            Currency = string.IsNullOrWhiteSpace(row.Currency) ? null : row.Currency,
                            OpeningBalance = row.OpeningBalance,
                            BankName = row.BankName ?? string.Empty,
                            BankAccountNo = row.BankAccountNo ?? string.Empty,
                        }, cancellationToken);
                        updated++;
                    }
                }
                catch (JournalException ex)
                {
                    failures.Add(new JournalAccountImportFailureDto { Row = row.Row, Name = row.Name, Reason = ex.Message });
                }
            }

            return new JournalAccountImportResultDto { Imported = imported, Updated = updated, Failures = failures };
        }

        private static JournalType? ParseType(string text) => text.Trim() switch
        {
            "现金" or "现金日记账" or "1" => JournalType.Cash,
            "银行存款" or "银行存款日记账" or "银行" or "2" => JournalType.BankDeposit,
            _ => null,
        };

        private static List<JournalAccountImportRow> ReadRows(Stream stream)
        {
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0) ?? throw new JournalException("文件中没有工作表。");
            var headerRow = sheet.GetRow(0) ?? throw new JournalException("文件缺少表头行。");

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < headerRow.LastCellNum; i++)
            {
                var text = headerRow.GetCell(i)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text) && !index.ContainsKey(text))
                {
                    index[text] = i;
                }
            }

            if (!index.ContainsKey("账簿名称"))
            {
                throw new JournalException("模板不正确：缺少「账簿名称」列。");
            }

            string? Get(Dictionary<string, int> map, Dictionary<string, int> columns, IRow row)
            {
                foreach (var candidate in map.Keys)
                {
                    if (columns.TryGetValue(candidate, out var column))
                    {
                        var text = row.GetCell(column)?.ToString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }

                return null;
            }

            var typeColumns = new Dictionary<string, int> { ["账户类型"] = 0, ["类型"] = 0 };
            var codeColumns = new Dictionary<string, int> { ["账户编码"] = 0, ["编码"] = 0 };
            var currencyColumns = new Dictionary<string, int> { ["币别"] = 0 };
            var openingColumns = new Dictionary<string, int> { ["期初余额"] = 0 };
            var bankColumns = new Dictionary<string, int> { ["开户银行"] = 0, ["银行"] = 0 };
            var accountNoColumns = new Dictionary<string, int> { ["银行账号"] = 0, ["账号"] = 0 };

            var rows = new List<JournalAccountImportRow>();
            for (var r = 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                {
                    continue;
                }

                var name = Get(new Dictionary<string, int> { ["账簿名称"] = 0 }, index, row);
                if (string.IsNullOrWhiteSpace(name) || name is "合计" or "总计")
                {
                    continue;
                }

                var openingText = Get(openingColumns, index, row);
                rows.Add(new JournalAccountImportRow
                {
                    Row = r + 1,
                    Name = name.Trim(),
                    TypeText = Get(typeColumns, index, row) ?? string.Empty,
                    Code = Get(codeColumns, index, row),
                    Currency = Get(currencyColumns, index, row),
                    OpeningBalance = decimal.TryParse((openingText ?? "0").Replace(",", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ? amount : 0m,
                    BankName = Get(bankColumns, index, row),
                    BankAccountNo = Get(accountNoColumns, index, row),
                });
            }

            return rows;
        }
    }
}
