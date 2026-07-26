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

        /// <summary>默认凭证字列表（逗号分隔）。</summary>
        public const string DefaultVoucherWords = "记,收,付,转";

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

        /// <summary>纳税人识别号（统一社会信用代码），可为空。</summary>
        public string? TaxpayerNumber { get; private set; }

        /// <summary>增值税税率（百分比数值，如 13 表示 13%）。</summary>
        public decimal VatRate { get; private set; }

        /// <summary>企业所得税税率（百分比数值，如 25 表示 25%）。</summary>
        public decimal IncomeTaxRate { get; private set; }

        /// <summary>凭证字列表，逗号分隔（如 "记,收,付,转"）。制单时凭证字须取自此列表。</summary>
        public string VoucherWords { get; private set; } = DefaultVoucherWords;

        /// <summary>已结账至期间（yyyyMM 格式整数）；0 表示尚未结账。结账按月逐期顺序推进。</summary>
        public int ClosedPeriod { get; private set; }

        /// <summary>启用期间的显示文本，形如 2026-01。</summary>
        public string StartPeriodText() => $"{StartYear:D4}-{StartMonth:D2}";

        /// <summary>已结账期间的显示文本（形如 2026-06）；尚未结账时为 null。</summary>
        public string? ClosedPeriodText() => ClosedPeriod == 0 ? null : $"{ClosedPeriod / 100:D4}-{ClosedPeriod % 100:D2}";

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
            var accountSet = new AccountSet(
                Guid.NewGuid(),
                name.Trim(),
                companyName.Trim(),
                standard,
                taxpayerType,
                baseCurrency.Trim(),
                startYear,
                startMonth,
                accountCodingScheme);

            // 默认税率按纳税人性质预置：一般纳税人 13%，小规模纳税人 3%；所得税默认 25%。
            accountSet.VatRate = taxpayerType == TaxpayerType.SmallScale ? 3m : 13m;
            accountSet.IncomeTaxRate = 25m;
            return accountSet;
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

        /// <summary>更新涉税资料：纳税人识别号与税率。税率为百分比数值（0–100）。</summary>
        public void UpdateTaxInfo(string? taxpayerNumber, decimal vatRate, decimal incomeTaxRate)
        {
            if (vatRate < 0m || vatRate > 100m)
            {
                throw new ArgumentException("增值税税率必须在 0 到 100 之间。", nameof(vatRate));
            }

            if (incomeTaxRate < 0m || incomeTaxRate > 100m)
            {
                throw new ArgumentException("所得税税率必须在 0 到 100 之间。", nameof(incomeTaxRate));
            }

            TaxpayerNumber = string.IsNullOrWhiteSpace(taxpayerNumber) ? null : taxpayerNumber.Trim();
            VatRate = vatRate;
            IncomeTaxRate = incomeTaxRate;
        }

        /// <summary>设置凭证字列表（逗号分隔）。每个凭证字 1–4 个字符，去重后至少保留一个。</summary>
        public void SetVoucherWords(string words)
        {
            var list = ParseVoucherWords(words);
            if (list.Count == 0)
            {
                throw new ArgumentException("凭证字列表不能为空。", nameof(words));
            }

            if (list.Any(w => w.Length > 4))
            {
                throw new ArgumentException("单个凭证字不能超过 4 个字符。", nameof(words));
            }

            VoucherWords = string.Join(',', list);
        }

        /// <summary>解析后的凭证字列表。</summary>
        public IReadOnlyList<string> VoucherWordList() => ParseVoucherWords(VoucherWords);

        /// <summary>解析凭证字字符串（兼容中英文逗号、顿号与空格分隔），去空去重保序。</summary>
        public static List<string> ParseVoucherWords(string? words)
        {
            return (string.IsNullOrWhiteSpace(words) ? DefaultVoucherWords : words)
                .Split(new[] { ',', '，', '、', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
        }

        /// <summary>下一个待结账期间：未结账时为启用期间，否则为已结期间的次月。</summary>
        public (int Year, int Month) NextPeriodToClose()
        {
            if (ClosedPeriod == 0)
            {
                return (StartYear, StartMonth);
            }

            var year = ClosedPeriod / 100;
            var month = ClosedPeriod % 100;
            return month == 12 ? (year + 1, 1) : (year, month + 1);
        }

        /// <summary>判断指定日期是否落在已结账期间内。已结期间禁止增删改凭证。</summary>
        public bool IsDateClosed(DateOnly date)
        {
            return ClosedPeriod != 0 && date.Year * 100 + date.Month <= ClosedPeriod;
        }

        /// <summary>月末结账：只能按顺序结转下一个待结期间。期间内凭证须已全部过账（由应用层校验）。</summary>
        public void ClosePeriod(int year, int month)
        {
            var (nextYear, nextMonth) = NextPeriodToClose();
            if (year != nextYear || month != nextMonth)
            {
                throw new InvalidOperationException($"只能按顺序结账，下一个待结期间为 {nextYear:D4}-{nextMonth:D2}。");
            }

            ClosedPeriod = year * 100 + month;
        }

        /// <summary>反结账：撤销最近一次结账，已结期间退回一个月；退到启用期间之前则清零。</summary>
        public void ReopenPeriod()
        {
            if (ClosedPeriod == 0)
            {
                throw new InvalidOperationException("尚未结账，无需反结账。");
            }

            var year = ClosedPeriod / 100;
            var month = ClosedPeriod % 100;
            var previous = month == 1 ? (year - 1) * 100 + 12 : year * 100 + (month - 1);
            ClosedPeriod = previous < StartYear * 100 + StartMonth ? 0 : previous;
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
