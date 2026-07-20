using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Vouchers;

namespace KuaiJi.Application.Vouchers
{
    /// <summary>
    /// 凭证导入应用服务：解析文件 → 自动补建缺失科目 → 按凭证号聚合分录 → 原样写入凭证。
    /// 借贷金额原样保留（含负数），借贷不平衡的凭证不阻断导入，仅记录告警。
    /// 所有科目与凭证均归属于指定账套，无法脱离账套独立导入。
    /// </summary>
    public sealed class VoucherImportService
    {
        private readonly IVoucherImportReader _reader;
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public VoucherImportService(
            IVoucherImportReader reader,
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _reader = reader;
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        public async Task<VoucherImportResult> ImportAsync(Guid accountSetId, Stream stream, CancellationToken cancellationToken = default)
        {
            // 导入必须面向一个已存在的账套；科目与凭证不能脱离账套独立存在。
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new VoucherImportException("指定的账套不存在，无法导入凭证。");
            }

            var rows = _reader.Read(stream);
            if (rows.Count == 0)
            {
                throw new VoucherImportException("文件中未找到任何凭证数据行。");
            }

            var warnings = new List<string>();

            // 1) 补建缺失科目：以科目编码为键，先载入该账套已有科目，再按需创建。
            var accountsByCode = (await _accountRepository.GetAllAsync(accountSetId, cancellationToken))
                .ToDictionary(a => a.Code, a => a, StringComparer.Ordinal);
            var createdAccounts = 0;

            foreach (var code in rows
                         .Select(r => r.AccountCode)
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct(StringComparer.Ordinal))
            {
                if (accountsByCode.ContainsKey(code))
                {
                    continue;
                }

                var name = rows.First(r => r.AccountCode == code).AccountName;
                var (category, direction) = InferCategoryAndDirection(code, name);
                var account = Account.CreateRoot(
                    accountSetId,
                    code,
                    string.IsNullOrWhiteSpace(name) ? code : name,
                    category,
                    direction,
                    isPreset: false);

                await _accountRepository.AddAsync(account, cancellationToken);
                accountsByCode[code] = account;
                createdAccounts++;
            }

            // 2) 按凭证聚合：源文件中同一凭证的分录连续排列，且行号自 1 起递增；
            //    以「行号==1 或 凭证号发生变化」作为一张新凭证的起点。
            var importedVouchers = 0;
            var importedEntries = 0;
            var skippedVouchers = 0;

            foreach (var group in GroupIntoVouchers(rows))
            {
                var head = group[0];
                var (word, number) = ParseVoucherNo(head.VoucherNo);

                if (await _voucherRepository.ExistsByWordNumberDateAsync(accountSetId, word, number, head.Date, cancellationToken))
                {
                    skippedVouchers++;
                    warnings.Add($"凭证「{head.VoucherNo}」（{head.Date:yyyy-MM-dd}）已存在，已跳过。");
                    continue;
                }

                var voucher = Voucher.Import(
                    accountSetId,
                    word,
                    number,
                    head.Date,
                    head.Preparer,
                    head.AttachmentCount,
                    head.Reviewer);

                var lineNumber = 0;
                foreach (var row in group)
                {
                    if (string.IsNullOrWhiteSpace(row.AccountCode) || !accountsByCode.TryGetValue(row.AccountCode, out var account))
                    {
                        warnings.Add($"第 {row.RowNumber} 行缺少有效科目编码，该分录已跳过。");
                        continue;
                    }

                    voucher.AddImportedEntry(
                        ++lineNumber,
                        Truncate(row.Summary, 200),
                        account.Id,
                        row.Debit,
                        row.Credit);
                    importedEntries++;
                }

                if (voucher.Entries.Count == 0)
                {
                    warnings.Add($"凭证「{head.VoucherNo}」（{head.Date:yyyy-MM-dd}）无有效分录，已跳过。");
                    continue;
                }

                if (!voucher.IsBalanced)
                {
                    warnings.Add(
                        $"凭证「{head.VoucherNo}」（{head.Date:yyyy-MM-dd}）借方 {voucher.TotalDebit:0.##} 与贷方 {voucher.TotalCredit:0.##} 不平衡，仍按原样导入。");
                }

                await _voucherRepository.AddAsync(voucher, cancellationToken);
                importedVouchers++;
            }

            await _voucherRepository.SaveChangesAsync(cancellationToken);

            return new VoucherImportResult
            {
                TotalRows = rows.Count,
                ImportedVouchers = importedVouchers,
                ImportedEntries = importedEntries,
                SkippedVouchers = skippedVouchers,
                CreatedAccounts = createdAccounts,
                Warnings = warnings,
            };
        }

        /// <summary>按凭证边界（行号回到 1 或凭证号变化）将连续的原始行切分成若干张凭证。</summary>
        private static List<List<ImportedVoucherRow>> GroupIntoVouchers(IReadOnlyList<ImportedVoucherRow> rows)
        {
            var groups = new List<List<ImportedVoucherRow>>();
            List<ImportedVoucherRow>? current = null;
            string? currentNo = null;

            foreach (var row in rows)
            {
                var isNewVoucher = current is null
                                   || row.LineNumber == 1
                                   || !string.Equals(row.VoucherNo, currentNo, StringComparison.Ordinal);

                if (isNewVoucher)
                {
                    current = new List<ImportedVoucherRow>();
                    groups.Add(current);
                    currentNo = row.VoucherNo;
                }

                current!.Add(row);
            }

            return groups;
        }

        /// <summary>解析形如「记-1」的凭证号，拆分为凭证字与凭证号；无法解析时兜底为「记 / 1」。</summary>
        private static (string Word, int Number) ParseVoucherNo(string voucherNo)
        {
            if (string.IsNullOrWhiteSpace(voucherNo))
            {
                return ("记", 1);
            }

            var text = voucherNo.Trim();
            var dash = text.LastIndexOf('-');
            if (dash > 0 && dash < text.Length - 1 && int.TryParse(text[(dash + 1)..], out var n))
            {
                return (text[..dash].Trim(), n <= 0 ? 1 : n);
            }

            // 无分隔符时，剥离末尾连续数字作为凭证号，其余作为凭证字。
            var i = text.Length;
            while (i > 0 && char.IsDigit(text[i - 1]))
            {
                i--;
            }

            var word = i > 0 ? text[..i].Trim() : "记";
            var numberText = text[i..];
            return (string.IsNullOrEmpty(word) ? "记" : word, int.TryParse(numberText, out var num) && num > 0 ? num : 1);
        }

        /// <summary>
        /// 依据科目编码首位与名称推断科目类别与余额方向（中国会计准则通用编码约定）：
        /// 1 资产、2 负债、3 权益、4 成本、5/6 损益；损益类中含“收入”的记贷方，其余记借方。
        /// </summary>
        private static (AccountCategory Category, BalanceDirection Direction) InferCategoryAndDirection(string code, string name)
        {
            var first = code.Length > 0 ? code[0] : '1';
            return first switch
            {
                '1' => (AccountCategory.Asset, BalanceDirection.Debit),
                '2' => (AccountCategory.Liability, BalanceDirection.Credit),
                '3' => (AccountCategory.Equity, BalanceDirection.Credit),
                '4' => (AccountCategory.Cost, BalanceDirection.Debit),
                '5' or '6' => (
                    AccountCategory.ProfitAndLoss,
                    (name?.Contains("收入") ?? false) ? BalanceDirection.Credit : BalanceDirection.Debit),
                _ => (AccountCategory.Asset, BalanceDirection.Debit),
            };
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value[..maxLength];
        }
    }
}
