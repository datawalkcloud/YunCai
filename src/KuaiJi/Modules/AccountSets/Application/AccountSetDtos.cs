using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.AccountSets.Application
{
    /// <summary>账套响应 DTO。</summary>
    public sealed class AccountSetDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = null!;

        public string CompanyName { get; init; } = null!;

        public AccountingStandard Standard { get; init; }

        public TaxpayerType TaxpayerType { get; init; }

        public string BaseCurrency { get; init; } = null!;

        public int StartYear { get; init; }

        public int StartMonth { get; init; }

        public string StartPeriod { get; init; } = null!;

        public bool IsCurrent { get; init; }

        /// <summary>科目编码方案，形如 "4-2-2-2"。</summary>
        public string AccountCodingScheme { get; init; } = AccountSet.DefaultCodingScheme;

        /// <summary>纳税人识别号，可为空。</summary>
        public string? TaxpayerNumber { get; init; }

        /// <summary>增值税税率（百分比数值）。</summary>
        public decimal VatRate { get; init; }

        /// <summary>企业所得税税率（百分比数值）。</summary>
        public decimal IncomeTaxRate { get; init; }

        /// <summary>报税地区（省级税务局名称，如“浙江省”），未填写为 null。</summary>
        public string? TaxRegion { get; init; }

        /// <summary>税局登录方式（如“企业业务登录”）。</summary>
        public string? TaxLoginType { get; init; }

        /// <summary>税局身份类型（如“办税员”）。</summary>
        public string? TaxIdentityType { get; init; }

        /// <summary>税局实名账号。</summary>
        public string? TaxLoginAccount { get; init; }

        /// <summary>
        /// 是否已保存税局实名密码。密码本身（含密文）永不出现在响应里，
        /// 助手取密码走独立的凭据接口，不搭账套查询的便车。
        /// </summary>
        public bool TaxLoginPasswordSet { get; init; }

        /// <summary>所属行业，未选择为 null。</summary>
        public string? Industry { get; init; }

        /// <summary>是否启用固定资产模块。</summary>
        public bool AssetsModuleEnabled { get; init; }

        /// <summary>是否启用资金出纳模块。</summary>
        public bool FundsModuleEnabled { get; init; }

        /// <summary>凭证是否需要审核后过账。</summary>
        public bool VoucherAuditRequired { get; init; }

        /// <summary>资产负债表辅助核算余额取值方式。</summary>
        public BalanceAssistScope BalanceAssistScope { get; init; }

        /// <summary>单价 / 数量 / 汇率的小数位数。</summary>
        public int UnitDecimalPlaces { get; init; }

        /// <summary>是否出口企业。</summary>
        public bool IsExportEnterprise { get; init; }

        /// <summary>个税申报渠道（如「个税网页端」）。</summary>
        public string? IitChannel { get; init; }

        /// <summary>个税登录方式（如「实名账号登录」）。</summary>
        public string? IitLoginType { get; init; }

        /// <summary>个税实名登录账号。</summary>
        public string? IitLoginAccount { get; init; }

        /// <summary>是否已保存个税实名密码；密码本身不下发，语义同 <see cref="TaxLoginPasswordSet"/>。</summary>
        public bool IitLoginPasswordSet { get; init; }

        /// <summary>凭证字列表（逗号分隔，如 "记,收,付,转"）。</summary>
        public string VoucherWords { get; init; } = AccountSet.DefaultVoucherWords;

        /// <summary>已结账至期间的显示文本（如 "2026-06"）；尚未结账时为 null。</summary>
        public string? ClosedPeriod { get; init; }

        /// <summary>已结转损益至期间的显示文本（如 "2026-06"）；尚未结转时为 null。</summary>
        public string? CarriedPeriod { get; init; }

        /// <summary>下一个待结账期间——年份。</summary>
        public int NextCloseYear { get; init; }

        /// <summary>下一个待结账期间——月份。</summary>
        public int NextCloseMonth { get; init; }

        public static AccountSetDto FromDomain(AccountSet accountSet)
        {
            var (nextYear, nextMonth) = accountSet.NextPeriodToClose();
            return new AccountSetDto
            {
                Id = accountSet.Id,
                Name = accountSet.Name,
                CompanyName = accountSet.CompanyName,
                Standard = accountSet.Standard,
                TaxpayerType = accountSet.TaxpayerType,
                BaseCurrency = accountSet.BaseCurrency,
                StartYear = accountSet.StartYear,
                StartMonth = accountSet.StartMonth,
                StartPeriod = accountSet.StartPeriodText(),
                IsCurrent = accountSet.IsCurrent,
                AccountCodingScheme = accountSet.AccountCodingScheme,
                TaxpayerNumber = accountSet.TaxpayerNumber,
                VatRate = accountSet.VatRate,
                IncomeTaxRate = accountSet.IncomeTaxRate,
                TaxRegion = accountSet.TaxRegion,
                TaxLoginType = accountSet.TaxLoginType,
                TaxIdentityType = accountSet.TaxIdentityType,
                TaxLoginAccount = accountSet.TaxLoginAccount,
                TaxLoginPasswordSet = accountSet.TaxLoginPasswordCipher is not null,
                Industry = accountSet.Industry,
                AssetsModuleEnabled = accountSet.AssetsModuleEnabled,
                FundsModuleEnabled = accountSet.FundsModuleEnabled,
                VoucherAuditRequired = accountSet.VoucherAuditRequired,
                BalanceAssistScope = accountSet.BalanceAssistScope,
                UnitDecimalPlaces = accountSet.UnitDecimalPlaces,
                IsExportEnterprise = accountSet.IsExportEnterprise,
                IitChannel = accountSet.IitChannel,
                IitLoginType = accountSet.IitLoginType,
                IitLoginAccount = accountSet.IitLoginAccount,
                IitLoginPasswordSet = accountSet.IitLoginPasswordCipher is not null,

                VoucherWords = accountSet.VoucherWords,
                ClosedPeriod = accountSet.ClosedPeriodText(),
                CarriedPeriod = accountSet.CarriedPeriodText(),
                NextCloseYear = nextYear,
                NextCloseMonth = nextMonth,
            };
        }
    }

    /// <summary>新建账套请求。</summary>
    public sealed class CreateAccountSetRequest
    {
        public string Name { get; init; } = null!;

        public string CompanyName { get; init; } = null!;

        public AccountingStandard Standard { get; init; } = AccountingStandard.Enterprise;

        public TaxpayerType TaxpayerType { get; init; } = TaxpayerType.General;

        public string BaseCurrency { get; init; } = "人民币";

        public int StartYear { get; init; }

        public int StartMonth { get; init; }

        /// <summary>是否按准则预置科目、收支类别与现金账户；默认 true（与柠檬云一致）。</summary>
        public bool Preset { get; init; } = true;

        /// <summary>科目编码方案，形如 "4-2-2-2"。不传则默认 "4-2-2-2"。</summary>
        public string? AccountCodingScheme { get; init; }

        /// <summary>所属行业，可选。</summary>
        public string? Industry { get; init; }
    }

    /// <summary>更新账套基础信息请求。</summary>
    public sealed class UpdateAccountSetRequest
    {
        public string Name { get; init; } = null!;

        public string CompanyName { get; init; } = null!;

        public AccountingStandard Standard { get; init; }

        public TaxpayerType TaxpayerType { get; init; }

        public string BaseCurrency { get; init; } = null!;

        /// <summary>科目编码方案，形如 "4-2-2-2"。</summary>
        public string? AccountCodingScheme { get; init; }

        /// <summary>纳税人识别号；传 null 表示不修改，传空串表示清除。</summary>
        public string? TaxpayerNumber { get; init; }

        /// <summary>增值税税率（百分比数值）；不传则不修改。</summary>
        public decimal? VatRate { get; init; }

        /// <summary>企业所得税税率（百分比数值）；不传则不修改。</summary>
        public decimal? IncomeTaxRate { get; init; }

        /// <summary>凭证字列表（逗号分隔）；不传则不修改。</summary>
        public string? VoucherWords { get; init; }

        /// <summary>报税地区（省级税务局名称）；不传则不修改，传空串表示清除。</summary>
        public string? TaxRegion { get; init; }

        /// <summary>税局登录方式（如“企业业务登录”）；不传则不修改。</summary>
        public string? TaxLoginType { get; init; }

        /// <summary>税局身份类型（如“办税员”）；不传则不修改。</summary>
        public string? TaxIdentityType { get; init; }

        /// <summary>税局实名账号；不传则不修改。</summary>
        public string? TaxLoginAccount { get; init; }

        /// <summary>
        /// 税局实名密码明文，仅在请求体内出现、加密后才入库；
        /// 不传表示保持原密码，传空串表示清除。响应里只回「是否已填写」。
        /// </summary>
        public string? TaxLoginPassword { get; init; }

        /// <summary>所属行业；不传则不修改，传空串表示清除。</summary>
        public string? Industry { get; init; }

        /// <summary>是否启用固定资产模块；不传则不修改。</summary>
        public bool? AssetsModuleEnabled { get; init; }

        /// <summary>是否启用资金出纳模块；不传则不修改。</summary>
        public bool? FundsModuleEnabled { get; init; }

        /// <summary>凭证是否需要审核后过账；不传则不修改。</summary>
        public bool? VoucherAuditRequired { get; init; }

        /// <summary>资产负债表辅助核算余额取值方式；不传则不修改。</summary>
        public BalanceAssistScope? BalanceAssistScope { get; init; }

        /// <summary>单价 / 数量 / 汇率的小数位数；不传则不修改。</summary>
        public int? UnitDecimalPlaces { get; init; }

        /// <summary>是否出口企业；不传则不修改。</summary>
        public bool? IsExportEnterprise { get; init; }

        /// <summary>个税申报渠道（如「个税网页端」）；不传则不修改，传空串表示清除。</summary>
        public string? IitChannel { get; init; }

        /// <summary>个税登录方式（如「实名账号登录」）；不传则不修改。</summary>
        public string? IitLoginType { get; init; }

        /// <summary>个税实名登录账号；不传则不修改。</summary>
        public string? IitLoginAccount { get; init; }

        /// <summary>个税实名密码明文；不传保持原密码，传空串清除。</summary>
        public string? IitLoginPassword { get; init; }
    }

    /// <summary>一次性税局密码票据：由登录态接口签发，本机助手凭票据匿名兑换密码。</summary>
    public sealed class TaxLoginTicketDto
    {
        public string Ticket { get; init; } = string.Empty;

        public int ExpiresInSeconds { get; init; }
    }

    /// <summary>本机助手兑换票据的请求。</summary>
    public sealed class TaxLoginTicketRedeemRequest
    {
        public string? Ticket { get; init; }
    }

    /// <summary>兑换结果：仅在这次响应里出现明文密码。</summary>
    public sealed class TaxLoginPasswordDto
    {
        public string Password { get; init; } = string.Empty;
    }

    /// <summary>月末结账请求：指定待结账的会计期间（须为下一个待结期间）。</summary>
    public sealed class ClosePeriodRequest
    {
        public int Year { get; init; }

        public int Month { get; init; }
    }
}
