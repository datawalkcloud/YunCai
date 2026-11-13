using YunCai.Modules.Accounting.Domain;

namespace YunCai.Modules.Accounting.Application
{
    /// <summary>
    /// 科目自动创建服务：确保指定编码的科目以正确层级存在于系统中。
    /// 按编码方案自动拆分编码层级、容错末尾零、自动创建缺失上级。
    /// 供科目导入和凭证导入复用。
    /// </summary>
    public sealed class AccountAutoCreateService
    {
        private readonly IAccountRepository _accountRepository;

        public AccountAutoCreateService(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        /// <summary>
        /// 确保科目存在：已在 knownAccounts 中则直接返回；否则按编码方案自动创建（含缺失上级）。
        /// </summary>
        public async Task<Account?> EnsureAccountAsync(
            Guid accountSetId,
            int[] scheme,
            int[] cumLengths,
            int maxLevel,
            string code,
            string name,
            Dictionary<string, Account> knownAccounts,
            AccountCategory? category = null,
            BalanceDirection? direction = null,
            string? mnemonic = null,
            bool isEnabled = true,
            List<string>? warnings = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            if (knownAccounts.TryGetValue(code, out var existing))
                return existing;

            // ── 精确匹配编码方案 ──
            var level = DetermineLevel(code, cumLengths);

            // ── 末尾零容错 ──
            if (level < 1)
            {
                var trimmed = TryTrimTrailingZero(code, cumLengths);
                if (trimmed is not null)
                {
                    code = trimmed;
                    // 截短后可能与已有科目撞码，需重新检查。
                    if (knownAccounts.TryGetValue(code, out var trimmedExisting))
                        return trimmedExisting;
                    level = DetermineLevel(code, cumLengths);
                }
            }

            // ── 前缀推断上级 ──
            if (level < 1)
            {
                var parent = FindParentByCodePrefix(code, knownAccounts);
                if (parent is not null)
                {
                    level = parent.Level + 1;
                    if (level > maxLevel)
                    {
                        warnings?.Add($"科目「{code} {name}」超出编码方案最大级次 {maxLevel}，已跳过。");
                        return null;
                    }

                    try
                    {
                        var child = parent.AddChild(code, name, direction);
                        FinalizeAccount(child, mnemonic, isEnabled, parent);
                        await _accountRepository.AddAsync(child, cancellationToken);
                        knownAccounts[code] = child;
                        return child;
                    }
                    catch (ArgumentException ex)
                    {
                        warnings?.Add($"科目「{code} {name}」创建失败：{ex.Message}");
                        return null;
                    }
                }

                // 无上级可参照，当作一级科目创建。
                category ??= InferCategoryByCodePrefix(code, name);
                if (category is null)
                {
                    warnings?.Add($"科目「{code} {name}」的类别无法识别，已跳过。");
                    return null;
                }

                direction ??= DefaultDirection(category.Value, name);
                try
                {
                    var root = Account.CreateRoot(accountSetId, code, name, category.Value, direction.Value, isPreset: false);
                    FinalizeAccount(root, mnemonic, isEnabled);
                    await _accountRepository.AddAsync(root, cancellationToken);
                    knownAccounts[code] = root;
                    return root;
                }
                catch (ArgumentException ex)
                {
                    warnings?.Add($"科目「{code} {name}」创建失败：{ex.Message}");
                    return null;
                }
            }

            // ── 精确命中：按方案创建（含自动建上级） ──
            var bestParent = FindParentByCodePrefix(code, knownAccounts);
            Account? immediateParent = null;

            if (bestParent is not null && bestParent.Level < level)
            {
                immediateParent = bestParent;
            }
            else
            {
                for (int lv = 1; lv < level; lv++)
                {
                    var ancestorCode = code.Substring(0, cumLengths[lv - 1]);
                    var ancestor = await ResolveOrCreateParentAsync(
                        accountSetId, scheme, cumLengths, maxLevel,
                        ancestorCode, name, category, direction, knownAccounts, warnings, cancellationToken);
                    if (ancestor is null)
                    {
                        immediateParent = null;
                        break;
                    }

                    if (lv == level - 1)
                        immediateParent = ancestor;
                }
            }

            if (immediateParent is null && level > 1)
            {
                warnings?.Add($"科目「{code} {name}」无法确定上级科目，已跳过。");
                return null;
            }

            try
            {
                Account account;
                if (level == 1)
                {
                    category ??= InferCategoryByCodePrefix(code, name);
                    if (category is null)
                    {
                        warnings?.Add($"顶级科目「{code} {name}」无法推断类别，已跳过。");
                        return null;
                    }

                    direction ??= DefaultDirection(category.Value, name);
                    account = Account.CreateRoot(accountSetId, code, name, category.Value, direction.Value, isPreset: false);
                }
                else
                {
                    account = immediateParent!.AddChild(code, name, direction);
                }

                FinalizeAccount(account, mnemonic, isEnabled, immediateParent);
                await _accountRepository.AddAsync(account, cancellationToken);
                knownAccounts[code] = account;
                return account;
            }
            catch (ArgumentException ex)
            {
                warnings?.Add($"科目「{code} {name}」创建失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>确定编码在方案中的层级；不匹配返回 -1。</summary>
        public static int DetermineLevel(string code, int[] cumLengths)
        {
            for (int i = 0; i < cumLengths.Length; i++)
                if (code.Length == cumLengths[i])
                    return i + 1;
            return -1;
        }

        /// <summary>末尾多一位且是 0 时，去掉后返回；否则 null。</summary>
        public static string? TryTrimTrailingZero(string code, int[] cumLengths)
        {
            if (code.Length < 2 || code[^1] != '0')
                return null;
            var shortened = code.Length - 1;
            foreach (var cl in cumLengths)
                if (shortened == cl)
                    return code.Substring(0, shortened);
            return null;
        }

        /// <summary>按编码前缀在已知科目中查找最长匹配的上级。</summary>
        public static Account? FindParentByCodePrefix(string code, Dictionary<string, Account> knownAccounts)
        {
            return knownAccounts.Values
                .Where(a => code.StartsWith(a.Code, StringComparison.Ordinal) && a.Code.Length < code.Length)
                .MaxBy(a => a.Code.Length);
        }

        /// <summary>
        /// 根据编码首位推断科目类别。4xxx 的归属依赖科目表体系：2007 企业准则为权益
        /// （4001 实收资本、4103 本年利润），旧准则/小企业准则为成本（4001 生产成本、4101 制造费用），
        /// 因此按名称中的权益关键字判定，避免权益类被计成存货。
        /// </summary>
        public static AccountCategory? InferCategoryByCodePrefix(string code, string? name = null)
        {
            if (code.Length == 0) return null;
            return code[0] switch
            {
                '1' => AccountCategory.Asset,
                '2' => AccountCategory.Liability,
                '3' => AccountCategory.Equity,
                '4' => LooksLikeEquity(name) ? AccountCategory.Equity : AccountCategory.Cost,
                '5' => AccountCategory.Cost,
                '6' => AccountCategory.ProfitAndLoss,
                _ => null,
            };
        }

        private static readonly string[] EquityNameKeywords =
        [
            "实收资本", "股本", "资本公积", "其他权益工具", "其他综合收益", "专项储备",
            "盈余公积", "一般风险准备", "本年利润", "利润分配", "库存股",
        ];

        private static bool LooksLikeEquity(string? name)
        {
            return !string.IsNullOrWhiteSpace(name)
                   && EquityNameKeywords.Any(keyword => name.Contains(keyword, StringComparison.Ordinal));
        }

        /// <summary>解析或自动创建上级科目。尝试从子科目名称中提取上级名称。</summary>
        private async Task<Account?> ResolveOrCreateParentAsync(
            Guid accountSetId,
            int[] scheme,
            int[] cumLengths,
            int maxLevel,
            string parentCode,
            string? childName,
            AccountCategory? fallbackCategory,
            BalanceDirection? fallbackDirection,
            Dictionary<string, Account> knownAccounts,
            List<string>? warnings,
            CancellationToken cancellationToken)
        {
            if (knownAccounts.TryGetValue(parentCode, out var cached))
                return cached;

            var parentLevel = DetermineLevel(parentCode, cumLengths);
            if (parentLevel < 1 || parentLevel > maxLevel)
                return null;

            // 递归创建祖辈（祖辈名称无法从子科目名推断，传 null 走兜底）。
            Account? grandParent = null;
            if (parentLevel > 1)
            {
                var grandCode = parentCode.Substring(0, cumLengths[parentLevel - 2]);
                grandParent = await ResolveOrCreateParentAsync(
                    accountSetId, scheme, cumLengths, maxLevel,
                    grandCode, null, fallbackCategory, fallbackDirection, knownAccounts, warnings, cancellationToken);
                if (grandParent is null) return null;
            }

            // 尝试从子科目名称中提取上级名称（如 "营业外收入-政府补助" → "营业外收入"）。
            var parentName = TryExtractParentName(childName) ?? "科目" + parentCode;

            try
            {
                Account parent;
                if (parentLevel == 1)
                {
                    var cat = fallbackCategory ?? InferCategoryByCodePrefix(parentCode, parentName);
                    if (cat is null) return null;
                    var dir = fallbackDirection ?? DefaultDirection(cat.Value, string.Empty);
                    parent = Account.CreateRoot(accountSetId, parentCode, parentName, cat.Value, dir, isPreset: false);
                    warnings?.Add($"上级科目「{parentCode}」不存在，已自动创建为顶级科目。");
                }
                else
                {
                    parent = grandParent!.AddChild(parentCode, parentName);
                    warnings?.Add($"上级科目「{parentCode}」不存在，已自动创建为 {parentLevel} 级科目。");
                }

                FinalizeAccount(parent, null, true, grandParent);
                await _accountRepository.AddAsync(parent, cancellationToken);
                knownAccounts[parentCode] = parent;
                return parent;
            }
            catch (ArgumentException ex)
            {
                warnings?.Add($"自动创建上级科目「{parentCode}」失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从子科目名称中尝试提取上级名称。如 "营业外收入-政府补助" → "营业外收入"。
        /// 支持的分隔符：-  —  /  ／  _
        /// </summary>
        private static string? TryExtractParentName(string? childName)
        {
            if (string.IsNullOrWhiteSpace(childName))
                return null;

            var name = childName.Trim();
            char[] seps = ['-', '—', '－', '/', '／', '_'];

            var idx = -1;
            foreach (var sep in seps)
            {
                var i = name.LastIndexOf(sep);
                if (i > 0 && i > idx)
                    idx = i;
            }

            if (idx <= 0)
                return null;

            var extracted = name.Substring(0, idx).Trim();
            return extracted.Length > 0 ? extracted : null;
        }

        /// <summary>落库前收尾：未填助记码时按名称自动生成（沿用柠檬云「父缩写_子缩写」规则），再处理停用。</summary>
        internal static void FinalizeAccount(Account account, string? mnemonic, bool isEnabled, Account? parent = null)
        {
            var resolved = AccountMnemonic.Resolve(mnemonic, account.Name, parent?.Mnemonic, parent?.Name);
            if (resolved is not null)
                account.SetMnemonic(resolved);
            if (!isEnabled)
                account.Disable();
        }

        /// <summary>顶级科目默认方向。</summary>
        public static BalanceDirection DefaultDirection(AccountCategory category, string name)
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
