using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.AccountSets.Application
{
    /// <summary>凭证字列表 / 详情 DTO。</summary>
    public sealed class VoucherWordDto
    {
        public Guid Id { get; init; }

        public string Word { get; init; } = null!;

        public string PrintTitle { get; init; } = null!;

        public bool IsDefault { get; init; }

        public static VoucherWordDto FromDomain(VoucherWord word) => new()
        {
            Id = word.Id,
            Word = word.Word,
            PrintTitle = word.PrintTitle,
            IsDefault = word.IsDefault,
        };
    }

    /// <summary>新建 / 修改凭证字请求。</summary>
    public sealed class SaveVoucherWordRequest
    {
        public string Word { get; init; } = null!;

        public string PrintTitle { get; init; } = null!;

        public bool IsDefault { get; init; }
    }
}
