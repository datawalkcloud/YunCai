using System.Globalization;
using KuaiJi.Modules.Accounting.Domain;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace KuaiJi.Modules.Accounting.Application
{
    /// <summary>旧账导入的一行解析结果（科目编码 + 借贷金额）。</summary>
    public sealed class OpeningBalanceImportRow
    {
        /// <summary>Excel 行号（从 1 开始，含表头）。</summary>
        public int Row { get; init; }

        public string Code { get; init; } = string.Empty;

        /// <summary>借方金额（没有该列时为 0）。</summary>
        public decimal Debit { get; init; }

        /// <summary>贷方金额（没有该列时为 0）。</summary>
        public decimal Credit { get; init; }

        /// <summary>单列余额（借方/贷方列都缺省时使用）。</summary>
        public decimal? Balance { get; init; }

        /// <summary>余额方向文本（借 / 贷），可空。</summary>
        public string? Direction { get; init; }
    }

    /// <summary>旧账导入失败行。</summary>
    public sealed class OpeningBalanceImportFailureDto
    {
        public int Row { get; init; }

        public string Code { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;
    }

    /// <summary>旧账导入结果：写入科目数、试算平衡与失败明细。</summary>
    public sealed class OpeningBalanceImportResultDto
    {
        public int Imported { get; init; }

        public int Updated { get; init; }

        public decimal TotalDebit { get; init; }

        public decimal TotalCredit { get; init; }

        public bool Balanced { get; init; }

        public IReadOnlyList<OpeningBalanceImportFailureDto> Failures { get; init; } = [];
    }

    /// <summary>
    /// 旧账导入：把旧系统「科目余额表」Excel 写入本账套期初余额。
    /// 支持三种表头形态——借贷两列（借方余额 / 贷方余额）、单列余额 + 方向列、单列余额（按科目余额方向）。
    /// </summary>
    public sealed class OpeningBalanceImportService
    {
        private static readonly string[] CodeHeaders = ["科目编码", "科目代码", "科目编号", "编码", "代码"];
        private static readonly string[] DebitHeaders = ["借方余额", "期初借方", "借方金额", "借方发生额", "借方"];
        private static readonly string[] CreditHeaders = ["贷方余额", "期初贷方", "贷方金额", "贷方发生额", "贷方"];
        private static readonly string[] BalanceHeaders = ["期初余额", "期末余额", "余额"];
        private static readonly string[] DirectionHeaders = ["余额方向", "方向"];

        private readonly OpeningBalanceService _openingBalanceService;
        private readonly IAccountRepository _accountRepository;

        public OpeningBalanceImportService(OpeningBalanceService openingBalanceService, IAccountRepository accountRepository)
        {
            _openingBalanceService = openingBalanceService;
            _accountRepository = accountRepository;
        }

        /// <summary>导入旧账科目余额表。</summary>
        public async Task<OpeningBalanceImportResultDto> ImportAsync(Guid accountSetId, Stream stream, CancellationToken cancellationToken = default)
        {
            var rows = ReadRows(stream);
            if (rows.Count == 0)
            {
                throw new OpeningBalanceException("文件中没有可导入的数据行。");
            }

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var byCode = accounts
                .GroupBy(a => a.Code, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var current = await _openingBalanceService.GetAsync(accountSetId, cancellationToken);
            var existing = current.Rows.Where(r => r.IsLeaf).ToDictionary(r => r.AccountId, r => r.OpeningBalance);

            var items = new List<OpeningBalanceItem>();
            var failures = new List<OpeningBalanceImportFailureDto>();
            var seen = new HashSet<Guid>();
            var imported = 0;
            var updated = 0;

            foreach (var row in rows)
            {
                if (!byCode.TryGetValue(row.Code, out var account))
                {
                    failures.Add(new OpeningBalanceImportFailureDto { Row = row.Row, Code = row.Code, Reason = "科目编码不存在" });
                    continue;
                }

                if (!account.IsLeaf)
                {
                    failures.Add(new OpeningBalanceImportFailureDto { Row = row.Row, Code = row.Code, Reason = "非末级科目不能录入期初余额" });
                    continue;
                }

                if (!seen.Add(account.Id))
                {
                    failures.Add(new OpeningBalanceImportFailureDto { Row = row.Row, Code = row.Code, Reason = "同一科目在文件中重复出现" });
                    continue;
                }

                var amount = ToBalance(row, account);
                items.Add(new OpeningBalanceItem { AccountId = account.Id, Amount = amount });

                if (existing.TryGetValue(account.Id, out var previous) && previous != 0m)
                {
                    updated++;
                }
                else
                {
                    imported++;
                }
            }

            if (items.Count > 0)
            {
                var balances = await _openingBalanceService.SaveAsync(accountSetId, new SaveOpeningBalancesRequest { Items = items }, cancellationToken);
                return new OpeningBalanceImportResultDto
                {
                    Imported = imported,
                    Updated = updated,
                    TotalDebit = balances.TotalDebit,
                    TotalCredit = balances.TotalCredit,
                    Balanced = balances.Balanced,
                    Failures = failures,
                };
            }

            var currentTotals = await _openingBalanceService.GetAsync(accountSetId, cancellationToken);
            return new OpeningBalanceImportResultDto
            {
                Imported = 0,
                Updated = 0,
                TotalDebit = currentTotals.TotalDebit,
                TotalCredit = currentTotals.TotalCredit,
                Balanced = currentTotals.Balanced,
                Failures = failures,
            };
        }

        /// <summary>把一行换算为「按科目余额方向为正」的期初金额。</summary>
        private static decimal ToBalance(OpeningBalanceImportRow row, Account account)
        {
            // 优先用借贷两列：净借方 = 借 - 贷，再按科目方向换算。
            if (row.Debit != 0m || row.Credit != 0m)
            {
                var netDebit = row.Debit - row.Credit;
                return account.Direction == BalanceDirection.Debit ? netDebit : -netDebit;
            }

            if (row.Balance is not { } balance)
            {
                return 0m;
            }

            var magnitude = Math.Abs(balance);
            var text = row.Direction?.Trim() ?? string.Empty;

            if (text.StartsWith('借'))
            {
                return account.Direction == BalanceDirection.Debit ? magnitude : -magnitude;
            }

            if (text.StartsWith('贷'))
            {
                return account.Direction == BalanceDirection.Credit ? magnitude : -magnitude;
            }

            // 单列余额且无方向列：视为科目正常方向余额。
            return balance >= 0m ? magnitude : -magnitude;
        }

        /// <summary>读取第一个工作表，定位「科目编码 / 借 / 贷 / 余额 / 方向」列。</summary>
        private static List<OpeningBalanceImportRow> ReadRows(Stream stream)
        {
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0) ?? throw new OpeningBalanceException("文件中没有工作表。");

            var headerRowIndex = FindHeaderRow(sheet);
            var header = sheet.GetRow(headerRowIndex) ?? throw new OpeningBalanceException("文件缺少表头行。");

            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < header.LastCellNum; i++)
            {
                var text = Normalize(header.GetCell(i)?.ToString());
                if (text.Length > 0 && !columns.ContainsKey(text))
                {
                    columns[text] = i;
                }
            }

            var codeColumn = FindColumn(columns, CodeHeaders);
            if (codeColumn is null)
            {
                throw new OpeningBalanceException("模板不正确：缺少「科目编码」列。");
            }

            var debitColumn = FindColumn(columns, DebitHeaders);
            var creditColumn = FindColumn(columns, CreditHeaders);
            var balanceColumn = FindColumn(columns, BalanceHeaders);
            var directionColumn = FindColumn(columns, DirectionHeaders);

            if (debitColumn is null && creditColumn is null && balanceColumn is null)
            {
                throw new OpeningBalanceException("模板不正确：至少需要「借方余额 / 贷方余额」或「期初余额」列。");
            }

            var rows = new List<OpeningBalanceImportRow>();
            for (var r = headerRowIndex + 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                {
                    continue;
                }

                var code = Normalize(Text(row.GetCell(codeColumn.Value)));
                if (code.Length == 0 || code is "合计" or "总计")
                {
                    continue;
                }

                rows.Add(new OpeningBalanceImportRow
                {
                    Row = r + 1,
                    Code = code,
                    Debit = debitColumn is null ? 0m : Number(row.GetCell(debitColumn.Value)),
                    Credit = creditColumn is null ? 0m : Number(row.GetCell(creditColumn.Value)),
                    Balance = balanceColumn is null ? null : Number(row.GetCell(balanceColumn.Value)),
                    Direction = directionColumn is null ? null : Text(row.GetCell(directionColumn.Value)),
                });
            }

            return rows;
        }

        /// <summary>表头可能不在第一行（旧系统导出常有标题行），向下最多找 5 行含「科目编码」的行。</summary>
        private static int FindHeaderRow(ISheet sheet)
        {
            for (var r = 0; r <= Math.Min(sheet.LastRowNum, 5); r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                {
                    continue;
                }

                for (var c = 0; c < row.LastCellNum; c++)
                {
                    if (Normalize(row.GetCell(c)?.ToString()) is "科目编码" or "科目代码" or "科目编号")
                    {
                        return r;
                    }
                }
            }

            throw new OpeningBalanceException("模板不正确：未找到包含「科目编码」的表头行。");
        }

        private static int? FindColumn(Dictionary<string, int> columns, string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (columns.TryGetValue(candidate, out var index))
                {
                    return index;
                }
            }

            // 兼容「借方余额(元)」这类带后缀的表头。
            foreach (var (name, index) in columns)
            {
                if (candidates.Any(candidate => name.StartsWith(candidate, StringComparison.Ordinal)))
                {
                    return index;
                }
            }

            return null;
        }

        private static string Normalize(string? text)
            => (text ?? string.Empty).Replace(" ", string.Empty).Replace("\u3000", string.Empty).Trim();

        private static string Text(ICell? cell) => cell switch
        {
            null => string.Empty,
            { CellType: CellType.Numeric } => cell.NumericCellValue.ToString("0", CultureInfo.InvariantCulture),
            _ => cell.ToString()?.Trim() ?? string.Empty,
        };

        private static decimal Number(ICell? cell)
        {
            if (cell is null)
            {
                return 0m;
            }

            if (cell.CellType is CellType.Numeric or CellType.Formula)
            {
                return (decimal)cell.NumericCellValue;
            }

            var text = (cell.ToString() ?? string.Empty).Replace(",", string.Empty).Replace("，", string.Empty).Trim();
            if (text.Length == 0)
            {
                return 0m;
            }

            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
        }
    }
}
