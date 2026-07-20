using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;

namespace KuaiJi.Application.Accounts
{
    /// <summary>
    /// 科目导入应用服务：解析模板 → 先父后子逐行建立科目 → 编码重复者跳过并告警。
    /// 所有科目均归属于指定账套，无法脱离账套独立导入。顶级科目须给出类别；明细科目类别继承上级，
    /// 方向留空时亦继承上级，编码须以上级编码为前缀（由领域模型强制校验）。
    /// </summary>
    public sealed class AccountImportService
    {
        private readonly IAccountImportReader _reader;
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;

        public AccountImportService(
            IAccountImportReader reader,
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository)
        {
            _reader = reader;
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
        }

        public async Task<AccountImportResult> ImportAsync(Guid accountSetId, Stream stream, CancellationToken cancellationToken = default)
        {
            // 导入必须面向一个已存在的账套；科目不能脱离账套独立存在。
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new AccountImportException("指定的账套不存在，无法导入科目。");
            }

            var rows = _reader.Read(stream);
            if (rows.Count == 0)
            {
                throw new AccountImportException("文件中未找到任何科目数据行。");
            }

            var warnings = new List<string>();

            // 已存在科目编码集合，用于查重（AsNoTracking，仅用于判断存在性）。
            var knownCodes = new HashSet<string>(
                (await _accountRepository.GetAllAsync(accountSetId, cancellationToken)).Select(a => a.Code),
                StringComparer.Ordinal);

            // 可作为上级使用的科目（本次新建的 + 按需从库中加载的，均为受跟踪实体，
            // 以便 AddChild 对上级 IsLeaf 的修改能被持久化）。
            var parentCandidates = new Dictionary<string, Account>(StringComparer.Ordinal);

            var imported = 0;
            var skipped = 0;

            // 先父后子：明细科目编码以上级编码为前缀，必长于上级；按编码长度升序即可保证上级先建。
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

                if (knownCodes.Contains(code))
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行科目「{code} {name}」编码已存在，已跳过。");
                    continue;
                }

                Account account;
                var parentCode = row.ParentCode.Trim();
                if (parentCode.Length > 0)
                {
                    var parent = await ResolveParentAsync(accountSetId, parentCode, parentCandidates, cancellationToken);
                    if (parent is null)
                    {
                        warnings.Add($"第 {row.RowNumber} 行科目「{code} {name}」的上级科目「{parentCode}」不存在，已跳过。");
                        continue;
                    }

                    try
                    {
                        // 明细科目类别继承上级；方向留空亦继承上级（AddChild 内部处理）。
                        account = parent.AddChild(code, name, ParseDirection(row.Direction));
                    }
                    catch (ArgumentException ex)
                    {
                        warnings.Add($"第 {row.RowNumber} 行科目「{code} {name}」创建失败：{ex.Message}");
                        continue;
                    }
                }
                else
                {
                    var category = ParseCategory(row.Category);
                    if (category is null)
                    {
                        warnings.Add(
                            $"第 {row.RowNumber} 行顶级科目「{code} {name}」的类别「{row.Category}」无法识别（应为 资产/负债/权益/成本/损益），已跳过。");
                        continue;
                    }

                    var direction = ParseDirection(row.Direction) ?? DefaultDirection(category.Value, name);
                    account = Account.CreateRoot(accountSetId, code, name, category.Value, direction, isPreset: false);
                }

                account.SetMnemonic(row.Mnemonic);
                if (!row.IsEnabled)
                {
                    account.Disable();
                }

                await _accountRepository.AddAsync(account, cancellationToken);
                knownCodes.Add(code);
                parentCandidates[code] = account;
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

        /// <summary>解析上级科目：优先取本次导入的缓存，否则从库中按编码加载（受跟踪）。</summary>
        private async Task<Account?> ResolveParentAsync(
            Guid accountSetId,
            string parentCode,
            Dictionary<string, Account> parentCandidates,
            CancellationToken cancellationToken)
        {
            if (parentCandidates.TryGetValue(parentCode, out var cached))
            {
                return cached;
            }

            var fromDb = await _accountRepository.GetByCodeAsync(accountSetId, parentCode, cancellationToken);
            if (fromDb is not null)
            {
                parentCandidates[parentCode] = fromDb;
            }

            return fromDb;
        }

        /// <summary>解析科目类别文本；无法识别返回 null。兼容「资产」与「资产类」等写法。</summary>
        private static AccountCategory? ParseCategory(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var t = text.Trim();
            if (t.StartsWith("资产", StringComparison.Ordinal)) return AccountCategory.Asset;
            if (t.StartsWith("负债", StringComparison.Ordinal)) return AccountCategory.Liability;
            if (t.StartsWith("权益", StringComparison.Ordinal) || t.StartsWith("所有者权益", StringComparison.Ordinal)) return AccountCategory.Equity;
            if (t.StartsWith("成本", StringComparison.Ordinal)) return AccountCategory.Cost;
            if (t.StartsWith("损益", StringComparison.Ordinal) || t.StartsWith("损益类", StringComparison.Ordinal)) return AccountCategory.ProfitAndLoss;
            return null;
        }

        /// <summary>解析余额方向文本；留空或无法识别返回 null（交由上级继承或按类别取默认值）。</summary>
        private static BalanceDirection? ParseDirection(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var t = text.Trim();
            if (t.StartsWith("借", StringComparison.Ordinal)) return BalanceDirection.Debit;
            if (t.StartsWith("贷", StringComparison.Ordinal)) return BalanceDirection.Credit;
            return null;
        }

        /// <summary>
        /// 顶级科目未显式给出方向时，按类别取常规默认方向：资产、成本记借方；负债、权益记贷方；
        /// 损益类中含「收入/收益」的记贷方，其余记借方。
        /// </summary>
        private static BalanceDirection DefaultDirection(AccountCategory category, string name)
        {
            return category switch
            {
                AccountCategory.Asset => BalanceDirection.Debit,
                AccountCategory.Cost => BalanceDirection.Debit,
                AccountCategory.Liability => BalanceDirection.Credit,
                AccountCategory.Equity => BalanceDirection.Credit,
                AccountCategory.ProfitAndLoss => (name.Contains("收入") || name.Contains("收益"))
                    ? BalanceDirection.Credit
                    : BalanceDirection.Debit,
                _ => BalanceDirection.Debit,
            };
        }
    }
}
