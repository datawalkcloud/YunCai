namespace KuaiJi.Domain.AccountSets
{
    /// <summary>
    /// 账套聚合根：一家核算单位的账簿集合与数据隔离边界。
    /// 记录单位信息、会计准则、本位币、纳税人性质与启用会计期间；
    /// 科目、日记账、凭证等均归属于某一账套。
    /// </summary>
    public class AccountSet
    {
        /// <summary>科目编码方案分隔符，形如 "4-2-2-2"。</summary>
        public const char CodingSchemeSeparator = '-';

        /// <summary>默认科目编码方案（4 级，每级位数分别为 4-2-2-2）。</summary>
        public const string DefaultCodingScheme = "4-2-2-2";

        /// <summary>科目编码方案最大支持级数。</summary>
        public const int MaxCodingLevel = 10;

        // EF Core 物化实体所需的无参构造函数。
        private AccountSet()
        {
        }

        private AccountSet(
            Guid id,
            string name,
            string companyName,
            AccountingStandard standard,
            TaxpayerType taxpayerType,
            string baseCurrency,
            int startYear,
            int startMonth,
            string accountCodingScheme)
        {
            Id = id;
            Name = name;
            CompanyName = companyName;
            Standard = standard;
            TaxpayerType = taxpayerType;
            BaseCurrency = baseCurrency;
            StartYear = startYear;
            StartMonth = startMonth;
            AccountCodingScheme = accountCodingScheme;
            IsCurrent = false;
        }

        /// <summary>账套标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>账套名称。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>单位（公司）名称。</summary>
        public string CompanyName { get; private set; } = null!;

        /// <summary>会计准则。</summary>
        public AccountingStandard Standard { get; private set; }

        /// <summary>纳税人性质。</summary>
        public TaxpayerType TaxpayerType { get; private set; }

        /// <summary>记账本位币，如“人民币”。</summary>
        public string BaseCurrency { get; private set; } = null!;

        /// <summary>启用会计期间——年份。</summary>
        public int StartYear { get; private set; }

        /// <summary>启用会计期间——月份（1-12）。</summary>
        public int StartMonth { get; private set; }

        /// <summary>是否为当前账套（全局唯一，由应用层保证互斥）。</summary>
        public bool IsCurrent { get; private set; }

        /// <summary>科目编码方案，形如 "4-2-2-2"，表示各级科目的编码位数。默认 "4-2-2-2"。</summary>
        public string AccountCodingScheme { get; private set; } = DefaultCodingScheme;

        /// <summary>启用期间的显示文本，形如 2026-01。</summary>
        public string StartPeriodText() => $"{StartYear:D4}-{StartMonth:D2}";

        /// <summary>新建账套。</summary>
        public static AccountSet Create(
            string name,
            string companyName,
            AccountingStandard standard,
            TaxpayerType taxpayerType,
            string baseCurrency,
            int startYear,
            int startMonth,
            string accountCodingScheme = DefaultCodingScheme)
        {
            Guard(name, companyName, baseCurrency, startYear, startMonth);
            GuardCodingScheme(accountCodingScheme);
            return new AccountSet(
                Guid.NewGuid(),
                name.Trim(),
                companyName.Trim(),
                standard,
                taxpayerType,
                baseCurrency.Trim(),
                startYear,
                startMonth,
                accountCodingScheme);
        }

        /// <summary>重命名账套。</summary>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("账套名称不能为空。", nameof(name));
            }

            Name = name.Trim();
        }

        /// <summary>更新账套基础信息。</summary>
        public void UpdateProfile(
            string companyName,
            AccountingStandard standard,
            TaxpayerType taxpayerType,
            string baseCurrency)
        {
            if (string.IsNullOrWhiteSpace(companyName))
            {
                throw new ArgumentException("单位名称不能为空。", nameof(companyName));
            }

            if (string.IsNullOrWhiteSpace(baseCurrency))
            {
                throw new ArgumentException("记账本位币不能为空。", nameof(baseCurrency));
            }

            CompanyName = companyName.Trim();
            Standard = standard;
            TaxpayerType = taxpayerType;
            BaseCurrency = baseCurrency.Trim();
        }

        /// <summary>设为当前账套。互斥性（同一时刻仅一个当前账套）由应用层保证。</summary>
        public void MarkAsCurrent() => IsCurrent = true;

        /// <summary>取消当前账套标记。</summary>
        public void UnmarkCurrent() => IsCurrent = false;

        /// <summary>设置科目编码方案。格式为以 "-" 分隔的各级位数，如 "4-2-2-2"。</summary>
        public void SetCodingScheme(string scheme)
        {
            GuardCodingScheme(scheme);
            AccountCodingScheme = scheme;
        }

        /// <summary>获取科目编码方案中第 level 级（从 1 开始）的编码总长度。</summary>
        public int CodeLengthForLevel(int level)
        {
            var segments = ParseCodingScheme(AccountCodingScheme);
            if (level < 1 || level > segments.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(level), $"级次必须在 1 到 {segments.Length} 之间。");
            }

            return segments.Take(level).Sum();
        }

        /// <summary>解析编码方案字符串为各级位数的整数数组。</summary>
        public static int[] ParseCodingScheme(string scheme)
        {
            var parts = (scheme ?? DefaultCodingScheme).Split(CodingSchemeSeparator);
            return parts.Select(p => int.Parse(p.Trim())).ToArray();
        }

        /// <summary>校验编码方案字符串的合法性。</summary>
        public static bool IsValidCodingScheme(string scheme, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(scheme))
            {
                error = "科目编码方案不能为空。";
                return false;
            }

            var parts = scheme.Split(CodingSchemeSeparator);
            if (parts.Length == 0 || parts.Length > MaxCodingLevel)
            {
                error = $"科目编码方案级数须在 1 到 {MaxCodingLevel} 之间。";
                return false;
            }

            for (var i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), out var digits) || digits < 1 || digits > 4)
                {
                    error = $"第 {i + 1} 级的位数「{parts[i].Trim()}」不合法（须为 1–4 的整数）。";
                    return false;
                }
            }

            return true;
        }

        private static void GuardCodingScheme(string scheme)
        {
            if (!IsValidCodingScheme(scheme, out var error))
            {
                throw new ArgumentException(error!, nameof(scheme));
            }
        }

        private static void Guard(string name, string companyName, string baseCurrency, int startYear, int startMonth)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("账套名称不能为空。", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(companyName))
            {
                throw new ArgumentException("单位名称不能为空。", nameof(companyName));
            }

            if (string.IsNullOrWhiteSpace(baseCurrency))
            {
                throw new ArgumentException("记账本位币不能为空。", nameof(baseCurrency));
            }

            if (startYear < 1900 || startYear > 9999)
            {
                throw new ArgumentException("启用年份不合法。", nameof(startYear));
            }

            if (startMonth < 1 || startMonth > 12)
            {
                throw new ArgumentException("启用月份必须在 1 到 12 之间。", nameof(startMonth));
            }
        }
    }
}
