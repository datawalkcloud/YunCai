using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;

namespace YunCai.Modules.Accounting.Application
{
    /// <summary>
    /// 科目导入应用服务：解析模板 → 先父后子逐行建立科目 → 编码重复者跳过并告警。
    /// 所有科目均归属于指定账套，无法脱离账套独立导入。顶级科目须给出类别；明细科目类别继承上级，
    /// 方向留空时亦继承上级，编码须以上级编码为前缀（由领域模型强制校验）。
    /// 当行未显式指定上级编码时，依据当前账套编码方案自动拆分编码层级，并自动创建缺失的上级科目。
    /// 助记码留空时按科目名称的拼音首字母生成，明细科目再加「上级助记码_」前缀。
    /// </summary>
    public sealed class AccountImportService
    {
        private readonly IAccountImportReader _reader;
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly AccountAutoCreateService _autoCreate;

        public AccountImportService(
            IAccountImportReader reader,
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            AccountAutoCreateService autoCreate)
        {
            _reader = reader;
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _autoCreate = autoCreate;
        }

        public async Task<AccountImportResult> ImportAsync(Guid accountSetId, Stream stream, CancellationToken cancellationToken = default)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is not { } accountSet)
                throw new AccountImportException("指定的账套不存在，无法导入科目。");

            var scheme = AccountSet.ParseCodingScheme(accountSet.AccountCodingScheme);
            var maxLevel = scheme.Length;
            var cumLengths = BuildCumLengths(scheme);

            var rows = _reader.Read(stream);
            if (rows.Count == 0)
                throw new AccountImportException("文件中未找到任何科目数据行。");

            var warnings = new List<string>();

            // 已存在科目：code → Account
            var knownAccounts = new Dictionary<string, Account>(StringComparer.Ordinal);
            foreach (var a in await _accountRepository.GetAllAsync(accountSetId, cancellationToken))
                knownAccounts[a.Code] = a;

            var imported = 0;
            var skipped = 0;

            foreach (var row in rows
                         .OrderBy(r => (r.Code ?? string.Empty).Trim().Length)
                         .ThenBy(r => r.Code, StringComparer.Ordinal))
            {
                var code = row.Code.Trim();
                var name = row.Name.Trim();
                if (code.Length == 0 || name.Length == 0)
                {
                    warnings.Add($"第 {row.RowNumber} 行缺少科目编码或名称，已跳过。");
                    continue;
                }

                if (knownAccounts.TryGetValue(code, out var existing))
                {
                    // 已存在的科目不覆盖任何字段，只给缺少助记码的存量科目补上，重导一次即可修复历史数据。
                    BackfillMnemonic(existing, knownAccounts);
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行科目「{code} {name}」编码已存在，已跳过。");
                    continue;
                }

                var explicitParentCode = row.ParentCode.Trim();

                // ── 方案一：显式指定上级编码 ──
                if (explicitParentCode.Length > 0)
                {
                    // 先确保上级存在。
                    var parent = await _autoCreate.EnsureAccountAsync(
                        accountSetId, scheme, cumLengths, maxLevel,
                        explicitParentCode, "科目" + explicitParentCode, knownAccounts,
                        category: null, direction: null, warnings: warnings, cancellationToken: cancellationToken);
                    if (parent is null) continue;

                    var newLevel = parent.Level + 1;
                    if (newLevel > maxLevel)
                    {
                        warnings.Add($"第 {row.RowNumber} 行科目「{code} {name}」超出编码方案最大级次 {maxLevel}，已跳过。");
                        continue;
                    }

                    var expectedLength = cumLengths[newLevel - 1];
                    if (code.Length != expectedLength)
                    {
                        warnings.Add($"第 {row.RowNumber} 行科目「{code} {name}」编码长度应为 {expectedLength} 位，实际 {code.Length} 位，已跳过。");
                        continue;
                    }

                    try
                    {
                        var child = parent.AddChild(code, name, ParseDirection(row.Direction));
                        AccountAutoCreateService.FinalizeAccount(child, row.Mnemonic, row.IsEnabled, parent);
                        await _accountRepository.AddAsync(child, cancellationToken);
                        knownAccounts[code] = child;
                        imported++;
                    }
                    catch (ArgumentException ex)
                    {
                        warnings.Add($"第 {row.RowNumber} 行科目「{code} {name}」创建失败：{ex.Message}");
                    }

                    continue;
                }

                // ── 方案二：按编码方案自动拆分 ──
                var account = await _autoCreate.EnsureAccountAsync(
                    accountSetId, scheme, cumLengths, maxLevel,
                    code, name, knownAccounts,
                    category: ParseCategory(row.Category),
                    direction: ParseDirection(row.Direction),
                    mnemonic: row.Mnemonic,
                    isEnabled: row.IsEnabled,
                    warnings: warnings,
                    cancellationToken: cancellationToken);

                if (account is not null)
                    imported++;
            }

            await _accountRepository.SaveChangesAsync(cancellationToken);

            return new AccountImportResult
            {
                TotalRows = rows.Count,
                ImportedAccounts = imported,
                SkippedAccounts = skipped,
                Warnings = warnings,
            };
        }

        /// <summary>给缺少助记码的存量科目补生成，已有助记码（含用户手工填写）保持不动。</summary>
        private static void BackfillMnemonic(Account account, IReadOnlyDictionary<string, Account> knownAccounts)
        {
            if (!string.IsNullOrWhiteSpace(account.Mnemonic))
                return;

            var parent = account.ParentId is { } parentId
                ? knownAccounts.Values.FirstOrDefault(a => a.Id == parentId)
                : null;
            account.SetMnemonic(AccountMnemonic.Resolve(null, account.Name, parent?.Mnemonic, parent?.Name));
        }

        private static int[] BuildCumLengths(int[] scheme)
        {
            var cum = new int[scheme.Length];
            for (int i = 0, sum = 0; i < scheme.Length; i++)
                cum[i] = sum += scheme[i];
            return cum;
        }

        private static AccountCategory? ParseCategory(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Trim();
            if (t.StartsWith("资产", StringComparison.Ordinal)) return AccountCategory.Asset;
            if (t.StartsWith("负债", StringComparison.Ordinal)) return AccountCategory.Liability;
            if (t.StartsWith("权益", StringComparison.Ordinal) || t.StartsWith("所有者权益", StringComparison.Ordinal)) return AccountCategory.Equity;
            if (t.StartsWith("成本", StringComparison.Ordinal)) return AccountCategory.Cost;
            if (t.StartsWith("损益", StringComparison.Ordinal) || t.StartsWith("损益类", StringComparison.Ordinal)) return AccountCategory.ProfitAndLoss;
            return null;
        }

        private static BalanceDirection? ParseDirection(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Trim();
            if (t.StartsWith("借", StringComparison.Ordinal)) return BalanceDirection.Debit;
            if (t.StartsWith("贷", StringComparison.Ordinal)) return BalanceDirection.Credit;
            return null;
        }
    }
}
