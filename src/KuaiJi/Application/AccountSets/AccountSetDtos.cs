using KuaiJi.Domain.AccountSets;

namespace KuaiJi.Application.AccountSets
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

        public static AccountSetDto FromDomain(AccountSet accountSet) => new()
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
        };
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

        /// <summary>科目编码方案，形如 "4-2-2-2"。不传则默认 "4-2-2-2"。</summary>
        public string? AccountCodingScheme { get; init; }
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
    }
}
