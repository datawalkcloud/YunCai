using YunCai.Modules.Accounting.Application;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Vouchers.Application
{
    /// <summary>
    /// 凭证导入应用服务：解析文件 → 自动补建缺失科目（按编码方案拆分层级） → 按凭证号聚合分录 → 原样写入凭证。
    /// 借贷平衡的凭证导入后直接过账入账；不平衡的保留为暂存并给出告警，待修正后再保存。
    /// </summary>
    public sealed class VoucherImportService
    {
        private readonly IVoucherImportReader _reader;
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly AccountAutoCreateService _autoCreate;

        public VoucherImportService(
            IVoucherImportReader reader,
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            AccountAutoCreateService autoCreate)
        {
            _reader = reader;
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _autoCreate = autoCreate;
        }

        public async Task<VoucherImportResult> ImportAsync(
            Guid accountSetId, Stream stream, VoucherImportConflict conflict = VoucherImportConflict.Skip, CancellationToken cancellationToken = default)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is not { } accountSet)
                throw new VoucherImportException("指定的账套不存在，无法导入凭证。");

            var rows = _reader.Read(stream);
            if (rows.Count == 0)
                throw new VoucherImportException("文件中未找到任何凭证数据行。");

            var warnings = new List<string>();

            // 解析编码方案。
            var scheme = AccountSet.ParseCodingScheme(accountSet.AccountCodingScheme);
            var maxLevel = scheme.Length;
            var cumLengths = new int[scheme.Length];
            for (int i = 0, sum = 0; i < scheme.Length; i++)
                cumLengths[i] = sum += scheme[i];

            // 载入已有科目。
            var knownAccounts = new Dictionary<string, Account>(StringComparer.Ordinal);
            foreach (var a in await _accountRepository.GetAllAsync(accountSetId, cancellationToken))
                knownAccounts[a.Code] = a;

            var createdAccounts = 0;

            // 补建缺失科目：按编码方案自动拆分、自动创建缺失上级。
            // 先按编码长度排序，确保上级短编码先建，长编码能找到上级。
            foreach (var code in rows
                         .Select(r => r.AccountCode)
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(c => c.Length)
                         .ThenBy(c => c, StringComparer.Ordinal))
            {
                if (knownAccounts.ContainsKey(code))
                    continue;

                var name = rows.First(r => r.AccountCode == code).AccountName;
                if (string.IsNullOrWhiteSpace(name))
                    name = code;

                var account = await _autoCreate.EnsureAccountAsync(
                    accountSetId, scheme, cumLengths, maxLevel,
                    code, name, knownAccounts,
                    category: null,
                    direction: null,
                    warnings: warnings,
                    cancellationToken: cancellationToken);

                if (account is not null)
                    createdAccounts++;
            }

            await _accountRepository.SaveChangesAsync(cancellationToken);

            // 2) 按凭证聚合：源文件中同一凭证的分录连续排列，且行号自 1 起递增；
            //    以「行号==1 或 凭证号发生变化」作为一张新凭证的起点。
            var importedVouchers = 0;
            var importedEntries = 0;
            var skippedVouchers = 0;
            var overwrittenVouchers = 0;

            foreach (var group in GroupIntoVouchers(rows))
            {
                var head = group[0];
                var (word, number) = ParseVoucherNo(head.VoucherNo);

                var existing = await _voucherRepository.GetByWordNumberDateAsync(accountSetId, word, number, head.Date, cancellationToken);
                if (existing is not null)
                {
                    if (conflict == VoucherImportConflict.Skip)
                    {
                        skippedVouchers++;
                        warnings.Add($"凭证「{head.VoucherNo}」（{head.Date:yyyy-MM-dd}）已存在，已跳过。");
                        continue;
                    }

                    // 覆盖：先删旧凭证。
                    _voucherRepository.Remove(existing);
                    overwrittenVouchers++;
                    warnings.Add($"凭证「{head.VoucherNo}」（{head.Date:yyyy-MM-dd}）已存在，已覆盖。");
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
                    if (string.IsNullOrWhiteSpace(row.AccountCode) || !knownAccounts.TryGetValue(row.AccountCode, out var account))
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
                        $"凭证「{head.VoucherNo}」（{head.Date:yyyy-MM-dd}）借方 {voucher.TotalDebit:0.##} 与贷方 {voucher.TotalCredit:0.##} 不平衡，已按暂存导入，修正并保存后才会入账。");
                }
                else
                {
                    voucher.Post();
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
                OverwrittenVouchers = overwrittenVouchers,
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
