using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.AccountSets.Application
{
    /// <summary>币别列表 / 详情 DTO。</summary>
    public sealed class CurrencyDto
    {
        public Guid Id { get; init; }

        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        /// <summary>记账汇率。</summary>
        public decimal Rate { get; init; }

        /// <summary>折算方式：1 = 原币*汇率=本位币，2 = 原币/汇率=本位币。</summary>
        public CurrencyConvertType ConvertType { get; init; }

        /// <summary>折算方式展示文本。</summary>
        public string ConvertText => ConvertType == CurrencyConvertType.Divide ? "原币/汇率=本位币" : "原币*汇率=本位币";

        public bool IsBase { get; init; }

        public static CurrencyDto FromDomain(Currency currency) => new()
        {
            Id = currency.Id,
            Code = currency.Code,
            Name = currency.Name,
            Rate = currency.Rate,
            ConvertType = currency.ConvertType,
            IsBase = currency.IsBase,
        };
    }

    /// <summary>新建 / 修改币别请求。</summary>
    public sealed class SaveCurrencyRequest
    {
        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        public decimal Rate { get; init; }

        /// <summary>折算方式；不传按「原币*汇率=本位币」。</summary>
        public CurrencyConvertType ConvertType { get; init; } = CurrencyConvertType.Multiply;

        /// <summary>设为本位币；保存时服务层会取消其他币别的本位币标记。</summary>
        public bool IsBase { get; init; }
    }

    /// <summary>折算方式文案（列表与详情共用）。</summary>
    internal static class CurrencyDtoExtensions
    {
        public static string ConvertTextOf(CurrencyConvertType convertType)
            => convertType == CurrencyConvertType.Divide ? "原币/汇率=本位币" : "原币*汇率=本位币";
    }
}
