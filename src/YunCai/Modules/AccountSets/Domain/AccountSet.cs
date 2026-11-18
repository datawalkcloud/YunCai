using YunCai.Modules.Assist.Domain;

namespace YunCai.Modules.AccountSets.Domain
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

        /// <summary>建账入口的默认编码方案：（一级 4 位、二级 3 位）。</summary>
        public const string NewAccountSetCodingScheme = "4-3-2-2";

        /// <summary>科目编码方案最大支持级数。</summary>
        public const int MaxCodingLevel = 10;

        /// <summary>默认凭证字列表（逗号分隔）。</summary>
        public const string DefaultVoucherWords = "记,收,付,转";

        /// <summary>单价 / 数量 / 汇率的默认小数位数。</summary>
        public const int DefaultUnitDecimalPlaces = 8;

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

        /// <summary>城市维护建设税税率（百分比数值）：市区 7%、县城与镇 5%、其他 1%。</summary>
        public decimal CityBuildTaxRate { get; private set; } = 7m;

        /// <summary>教育费附加率（百分比数值），法定 3%。</summary>
        public decimal EducationSurchargeRate { get; private set; } = 3m;

        /// <summary>地方教育附加率（百分比数值），法定 2%。</summary>
        public decimal LocalEducationSurchargeRate { get; private set; } = 2m;

        /// <summary>报税地区（省级税务局名称，如“浙江省”）。电子税务局按省分域，助手据此决定打开哪个入口。</summary>
        public string? TaxRegion { get; private set; }

        /// <summary>税局登录方式（如“企业业务登录”）。与税局页面的下拉一一对应，按文案存储不做本地枚举收敛。</summary>
        public string? TaxLoginType { get; private set; }

        /// <summary>税局身份类型（如“办税员”）。</summary>
        public string? TaxIdentityType { get; private set; }

        /// <summary>税局实名账号（手机号或身份证号）。</summary>
        public string? TaxLoginAccount { get; private set; }

        /// <summary>
        /// 税局实名密码的密文，入库前由 Application.ITaxCredentialProtector 加密；
        /// 任何响应 DTO 都不带这个字段，对外只暴露「是否已填写」。
        /// </summary>
        public string? TaxLoginPasswordCipher { get; private set; }

        /// <summary>凭证字列表，逗号分隔（如 "记,收,付,转"）。制单时凭证字须取自此列表。</summary>
        public string VoucherWords { get; private set; } = DefaultVoucherWords;

        /// <summary>
        /// 已启用的辅助核算类别，逗号分隔的枚举值（如 "1,2,4"）；空串表示账套未启用辅助核算。
        /// 与凭证字同样用逗号串存储，避免为一组固定枚举另建表。
        /// </summary>
        public string EnabledAssistTypes { get; private set; } = string.Empty;

        /// <summary>所属行业（如「商业」「工业」），仅作为账套属性记录，不影响取数。</summary>
        public string? Industry { get; private set; }

        /// <summary>是否启用固定资产模块。</summary>
        public bool AssetsModuleEnabled { get; private set; } = true;

        /// <summary>是否启用资金出纳模块。</summary>
        public bool FundsModuleEnabled { get; private set; } = true;

        /// <summary>凭证是否需要审核后才能过账；关闭后制单即可直接过账。</summary>
        public bool VoucherAuditRequired { get; private set; } = true;

        /// <summary>资产负债表辅助核算余额的取值范围。</summary>
        public BalanceAssistScope BalanceAssistScope { get; private set; } = BalanceAssistScope.All;

        /// <summary>单价 / 数量 / 汇率的小数位数。</summary>
        public int UnitDecimalPlaces { get; private set; } = DefaultUnitDecimalPlaces;

        /// <summary>是否出口企业（影响免抵退等税务处理，作为账套属性记录）。</summary>
        public bool IsExportEnterprise { get; private set; }

        /// <summary>个税申报渠道（如「个税网页端」）。与税局页面文案一致，不做本地枚举收敛。</summary>
        public string? IitChannel { get; private set; }

        /// <summary>个税登录方式（如「实名账号登录」「扫码登录」）。</summary>
        public string? IitLoginType { get; private set; }

        /// <summary>个税实名登录账号（手机号或身份证号）。</summary>
        public string? IitLoginAccount { get; private set; }

        /// <summary>
        /// 个税实名登录密码的密文，加密方式与 <see cref="TaxLoginPasswordCipher"/> 相同；
        /// 不下发也不进日志，响应只暴露「是否已填写」。
        /// </summary>
        public string? IitLoginPasswordCipher { get; private set; }

        /// <summary>已结账至期间（yyyyMM 格式整数）；0 表示尚未结账。结账按月逐期顺序推进。</summary>
        public int ClosedPeriod { get; private set; }

        /// <summary>
        /// 已结转损益至期间（yyyyMM 格式整数）；0 表示尚未结转。按月逐期顺序推进；
        /// 与 <see cref="ClosedPeriod"/> 构成「未结转 / 已结转未结账 / 已结账」三段状态，结账前必须先完成结转。
        /// </summary>
        public int CarriedPeriod { get; private set; }

        /// <summary>启用期间的显示文本，形如 2026-01。</summary>
        public string StartPeriodText() => $"{StartYear:D4}-{StartMonth:D2}";

        /// <summary>已结账期间的显示文本（形如 2026-06）；尚未结账时为 null。</summary>
        public string? ClosedPeriodText() => ClosedPeriod == 0 ? null : $"{ClosedPeriod / 100:D4}-{ClosedPeriod % 100:D2}";

        /// <summary>已结转损益期间的显示文本（形如 2026-06）；尚未结转时为 null。</summary>
        public string? CarriedPeriodText() => CarriedPeriod == 0 ? null : $"{CarriedPeriod / 100:D4}-{CarriedPeriod % 100:D2}";

        /// <summary>新建账套。</summary>
        public static AccountSet Create(
            string name,
            string companyName,
            AccountingStandard standard,
            TaxpayerType taxpayerType,
            string baseCurrency,
            int startYear,
            int startMonth,
            string accountCodingScheme = DefaultCodingScheme,
            string? industry = null)
        {
            Guard(name, companyName, baseCurrency, startYear, startMonth);
            // 四种准则都可建账，各自的默认科目表在 preset_accounts 里按准则预置。
            if (standard is not (AccountingStandard.Enterprise or AccountingStandard.SmallBusiness
                    or AccountingStandard.NonProfit or AccountingStandard.NewEnterprise2006))
            {
                throw new ArgumentException("会计准则取值无效，请重新选择。");
            }

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
            accountSet.Industry = NullIfBlank(industry);

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
                throw new ArgumentException("账套名称不能为空。");
            }

            Name = name.Trim();
        }

        /// <summary>更新账套基础信息。</summary>
        public void UpdateProfile(
            string companyName,
            AccountingStandard standard,
            TaxpayerType taxpayerType,
            string baseCurrency,
            string? industry = null)
        {
            if (string.IsNullOrWhiteSpace(companyName))
            {
                throw new ArgumentException("单位名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(baseCurrency))
            {
                throw new ArgumentException("记账本位币不能为空。");
            }

            ValidateStandardChange(standard);

            CompanyName = companyName.Trim();
            Standard = standard;
            TaxpayerType = taxpayerType;
            BaseCurrency = baseCurrency.Trim();
            Industry = NullIfBlank(industry);
        }

        /// <summary>
        /// 已建账套只允许从小企业会计准则升级为企业会计准则；换准则等于换整套科目编码与报表版式，
        /// 其余切换（含新会计准则 2006、民非制度）需要重新建账，不做原地改写。
        /// </summary>
        private void ValidateStandardChange(AccountingStandard standard)
        {
            if (standard == Standard)
            {
                return;
            }

            if (Standard == AccountingStandard.Enterprise && standard == AccountingStandard.SmallBusiness)
            {
                throw new ArgumentException("企业会计准则账套不能切换为小企业会计准则。");
            }

            if (Standard != AccountingStandard.SmallBusiness || standard != AccountingStandard.Enterprise)
            {
                throw new ArgumentException("更换会计准则请重新建账，目前只支持从小企业会计准则升级为企业会计准则。");
            }
        }

        /// <summary>
        /// 更新功能参数：模块开关、凭证审核、报表口径与小数位。
        /// 开关只影响后续录入与展示口径，已登记的资产、流水与凭证照常保留。
        /// </summary>
        public void UpdateFunctionParams(
            bool assetsModuleEnabled,
            bool fundsModuleEnabled,
            bool voucherAuditRequired,
            BalanceAssistScope balanceAssistScope,
            int unitDecimalPlaces)
        {
            if (unitDecimalPlaces is < 0 or > 10)
            {
                throw new ArgumentException("单价 / 数量 / 汇率小数位数必须在 0 到 10 之间。");
            }

            AssetsModuleEnabled = assetsModuleEnabled;
            FundsModuleEnabled = fundsModuleEnabled;
            VoucherAuditRequired = voucherAuditRequired;
            BalanceAssistScope = balanceAssistScope;
            UnitDecimalPlaces = unitDecimalPlaces;
        }

        /// <summary>更新涉税资料：纳税人识别号与税率。税率为百分比数值（0–100）。</summary>
        public void UpdateTaxInfo(
            string? taxpayerNumber,
            decimal vatRate,
            decimal incomeTaxRate,
            bool? isExportEnterprise = null,
            decimal? cityBuildTaxRate = null,
            decimal? educationSurchargeRate = null,
            decimal? localEducationSurchargeRate = null)
        {
            if (vatRate < 0m || vatRate > 100m)
            {
                throw new ArgumentException("增值税税率必须在 0 到 100 之间。");
            }

            if (incomeTaxRate < 0m || incomeTaxRate > 100m)
            {
                throw new ArgumentException("所得税税率必须在 0 到 100 之间。");
            }

            TaxpayerNumber = string.IsNullOrWhiteSpace(taxpayerNumber) ? null : taxpayerNumber.Trim();
            VatRate = vatRate;
            IncomeTaxRate = incomeTaxRate;
            if (isExportEnterprise.HasValue)
            {
                IsExportEnterprise = isExportEnterprise.Value;
            }

            CityBuildTaxRate = CheckSurtaxRate(cityBuildTaxRate, "城市维护建设税税率") ?? CityBuildTaxRate;
            EducationSurchargeRate = CheckSurtaxRate(educationSurchargeRate, "教育费附加率") ?? EducationSurchargeRate;
            LocalEducationSurchargeRate = CheckSurtaxRate(localEducationSurchargeRate, "地方教育附加率") ?? LocalEducationSurchargeRate;
        }

        private static decimal? CheckSurtaxRate(decimal? rate, string label)
        {
            if (rate is null)
            {
                return null;
            }

            if (rate < 0m || rate > 100m)
            {
                throw new ArgumentException($"{label}必须在 0 到 100 之间。");
            }

            return rate;
        }

        /// <summary>
        /// 更新税局登录资料（一键登录税局用）。约定与 <see cref="UpdateTaxInfo"/> 的调用方一致：
        /// 传 null 表示保持原值，传空串表示清除。
        /// passwordCipher 必须是已加密的密文——明文只在请求处理过程中存在，不落库也不进日志。
        /// </summary>
        public void UpdateTaxLoginInfo(string? taxRegion, string? loginType, string? identityType, string? account, string? passwordCipher)
        {
            TaxRegion = NullIfBlank(taxRegion);
            TaxLoginType = NullIfBlank(loginType);
            TaxIdentityType = NullIfBlank(identityType);
            TaxLoginAccount = NullIfBlank(account);
            TaxLoginPasswordCipher = NullIfBlank(passwordCipher);
        }

        /// <summary>
        /// 更新个税（自然人电子税务局）登录资料。空值语义与 <see cref="UpdateTaxLoginInfo"/> 一致：
        /// 传 null 保持原值，传空串清除；passwordCipher 必须是已加密的密文。
        /// </summary>
        public void UpdateIitLoginInfo(string? channel, string? loginType, string? account, string? passwordCipher)
        {
            IitChannel = NullIfBlank(channel);
            IitLoginType = NullIfBlank(loginType);
            IitLoginAccount = NullIfBlank(account);
            IitLoginPasswordCipher = NullIfBlank(passwordCipher);
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>设置凭证字列表（逗号分隔）。每个凭证字 1–4 个字符，去重后至少保留一个。</summary>
        public void SetVoucherWords(string words)
        {
            var list = ParseVoucherWords(words);
            if (list.Count == 0)
            {
                throw new ArgumentException("凭证字列表不能为空。");
            }

            if (list.Any(w => w.Length > VoucherWord.MaxWordLength))
            {
                throw new ArgumentException($"单个凭证字不能超过 {VoucherWord.MaxWordLength} 个字符。");
            }

            VoucherWords = string.Join(',', list);
        }

        /// <summary>解析后的凭证字列表。</summary>
        public IReadOnlyList<string> VoucherWordList() => ParseVoucherWords(VoucherWords);

        /// <summary>解析后的已启用辅助核算类别列表（按 <see cref="AssistTypeText.All"/> 顺序去重）。</summary>
        public IReadOnlyList<AssistType> EnabledAssistTypeList() => ParseAssistTypes(EnabledAssistTypes);

        /// <summary>设置已启用的辅助核算类别。传入空集合表示整个账套关闭辅助核算。</summary>
        public void SetEnabledAssistTypes(IEnumerable<AssistType> types)
        {
            ArgumentNullException.ThrowIfNull(types);

            var distinct = types.Where(t => Enum.IsDefined(t)).Distinct()
                .OrderBy(t => (int)t)
                .ToList();
            EnabledAssistTypes = string.Join(',', distinct.Select(t => (int)t));
        }

        /// <summary>解析逗号串形式的辅助核算类别，忽略无法识别的值并保持声明顺序。</summary>
        public static List<AssistType> ParseAssistTypes(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return [];
            }

            return value
                .Split(new[] { ',', '，', '、', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => Enum.TryParse<AssistType>(part, out var parsed) ? parsed : (AssistType?)null)
                .Where(t => t is not null && Enum.IsDefined(t.Value))
                .Select(t => t!.Value)
                .Distinct()
                .OrderBy(t => (int)t)
                .ToList();
        }

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

        /// <summary>
        /// 月末结账：只能按顺序结转下一个待结期间；期间内凭证须已全部过账（由应用层校验），
        /// 且该期间必须已完成结转损益（先结转、后结账）。
        /// </summary>
        /// <summary>重新初始化：将结转 / 结账期间退回启用期间（0）。</summary>
        public void ResetPeriods()
        {
            CarriedPeriod = 0;
            ClosedPeriod = 0;
        }

        public void ClosePeriod(int year, int month)
        {
            var (nextYear, nextMonth) = NextPeriodToClose();
            if (year != nextYear || month != nextMonth)
            {
                throw new InvalidOperationException($"只能按顺序结账，下一个待结期间为 {nextYear:D4}-{nextMonth:D2}。");
            }

            // 先结转损益、再结账：未完成结转的期间不允许结账。
            if (CarriedPeriod < year * 100 + month)
            {
                throw new InvalidOperationException($"{year:D4}-{month:D2} 尚未完成结转损益，请先结转后再结账。");
            }

            ClosedPeriod = year * 100 + month;
        }

        /// <summary>
        /// 反结账：从指定已结期间起撤销结账——该期间及其后的已结期间全部退回，
        /// 已结账期间退到目标期间的上一个月；退到启用期间之前则清零。
        /// </summary>
        public void ReopenFrom(int year, int month)
        {
            if (ClosedPeriod == 0)
            {
                throw new InvalidOperationException("尚未结账，无需反结账。");
            }

            if (year * 100 + month > ClosedPeriod)
            {
                throw new InvalidOperationException($"该期间尚未结账（当前已结账至 {ClosedPeriod / 100:D4}-{ClosedPeriod % 100:D2}）。");
            }

            var previous = month == 1 ? (year - 1) * 100 + 12 : year * 100 + (month - 1);
            ClosedPeriod = previous < StartYear * 100 + StartMonth ? 0 : previous;
        }

        /// <summary>反结账：撤销最近一次结账，已结期间退回一个月。</summary>
        public void ReopenPeriod()
        {
            if (ClosedPeriod == 0)
            {
                throw new InvalidOperationException("尚未结账，无需反结账。");
            }

            ReopenFrom(ClosedPeriod / 100, ClosedPeriod % 100);
        }

        /// <summary>
        /// 下一个待结转损益期间：已结转时为已结转期间的次月；尚未结转时从启用期间开始；
        /// 若存在历史已结账月份（旧数据未结转），则从最后一个已结期间的次月开始。
        /// </summary>
        public (int Year, int Month) NextPeriodToCarry()
        {
            if (CarriedPeriod != 0)
            {
                var year = CarriedPeriod / 100;
                var month = CarriedPeriod % 100;
                return month == 12 ? (year + 1, 1) : (year, month + 1);
            }

            if (ClosedPeriod != 0 && ClosedPeriod > StartYear * 100 + StartMonth)
            {
                var year = ClosedPeriod / 100;
                var month = ClosedPeriod % 100;
                return month == 12 ? (year + 1, 1) : (year, month + 1);
            }

            return (StartYear, StartMonth);
        }

        /// <summary>标记指定期间已完成结转损益：只能按顺序结转下一个待结转期间。</summary>
        public void MarkCarried(int year, int month)
        {
            var (nextYear, nextMonth) = NextPeriodToCarry();
            if (year != nextYear || month != nextMonth)
            {
                throw new InvalidOperationException($"只能按顺序结转，下一个待结转期间为 {nextYear:D4}-{nextMonth:D2}。");
            }

            CarriedPeriod = year * 100 + month;
        }

        /// <summary>撤销最近一次结转损益：结转进度退回一个月；退到启用期间之前则清零。调用方负责校验期间未结账。</summary>
        public void ClearCarried()
        {
            if (CarriedPeriod == 0)
            {
                throw new InvalidOperationException("尚未结转损益，无需撤销。");
            }

            ClearCarriedTo(CarriedPeriod / 100, CarriedPeriod % 100);
        }

        /// <summary>
        /// 撤销至指定期间之前：结转进度退到目标期间的上一个月；退到启用期间之前则清零。
        /// 目标期间本身及其后的结转由调用方一并撤销。
        /// </summary>
        public void ClearCarriedTo(int year, int month)
        {
            if (CarriedPeriod == 0 || year * 100 + month > CarriedPeriod)
            {
                throw new InvalidOperationException("只能撤销已结转的期间。");
            }

            var previous = month == 1 ? (year - 1) * 100 + 12 : year * 100 + (month - 1);
            CarriedPeriod = previous < StartYear * 100 + StartMonth ? 0 : previous;
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
                throw new ArgumentException("账套名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(companyName))
            {
                throw new ArgumentException("单位名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(baseCurrency))
            {
                throw new ArgumentException("记账本位币不能为空。");
            }

            if (startYear < 1900 || startYear > 9999)
            {
                throw new ArgumentException("启用年份不合法。");
            }

            if (startMonth < 1 || startMonth > 12)
            {
                throw new ArgumentException("启用月份必须在 1 到 12 之间。");
            }
        }
    }
}
