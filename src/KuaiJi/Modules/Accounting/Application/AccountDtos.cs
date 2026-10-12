using KuaiJi.Modules.Accounting.Domain;

namespace KuaiJi.Modules.Accounting.Application
{
    /// <summary>会计科目响应 DTO（供凭证录入选择科目等场景）。</summary>
    public sealed class AccountDto
    {
        public Guid Id { get; init; }

        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        public AccountCategory Category { get; init; }

        public BalanceDirection Direction { get; init; }

        public Guid? ParentId { get; init; }

        public int Level { get; init; }

        public bool IsLeaf { get; init; }

        public bool IsEnabled { get; init; }

        public string? Mnemonic { get; init; }

        /// <summary>期初余额（按科目余额方向为正）。</summary>
        public decimal OpeningBalance { get; init; }

        public static AccountDto FromDomain(Account account) => new()
        {
            Id = account.Id,
            Code = account.Code,
            Name = account.Name,
            Category = account.Category,
            Direction = account.Direction,
            ParentId = account.ParentId,
            Level = account.Level,
            IsLeaf = account.IsLeaf,
            IsEnabled = account.IsEnabled,
            Mnemonic = account.Mnemonic,
            OpeningBalance = account.OpeningBalance,
        };
    }

    /// <summary>新增科目请求。指定上级科目时创建明细科目，其类别与方向继承上级（Category/Direction 被忽略）。</summary>
    public sealed class CreateAccountRequest
    {
        /// <summary>上级科目标识；为空表示创建顶级科目。</summary>
        public Guid? ParentId { get; init; }

        /// <summary>科目编码，账套内唯一；明细科目须以上级编码为前缀。</summary>
        public string Code { get; init; } = null!;

        /// <summary>科目名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>科目类别（仅顶级科目使用；明细科目继承上级）。</summary>
        public AccountCategory Category { get; init; }

        /// <summary>余额方向（仅顶级科目使用；明细科目继承上级）。</summary>
        public BalanceDirection Direction { get; init; }

        /// <summary>助记码，可为空。</summary>
        public string? Mnemonic { get; init; }

        /// <summary>是否启用，默认为 true。</summary>
        public bool IsEnabled { get; init; } = true;
    }

    /// <summary>修改科目请求。仅允许修改名称、余额方向（末级科目）、助记码和启用状态。</summary>
    public sealed class UpdateAccountRequest
    {
        /// <summary>科目名称。</summary>
        public string? Name { get; init; }

        /// <summary>余额方向（仅末级科目可修改）。</summary>
        public BalanceDirection? Direction { get; init; }

        /// <summary>助记码。</summary>
        public string? Mnemonic { get; init; }

        /// <summary>是否启用。</summary>
        public bool? IsEnabled { get; init; }
    }
}
