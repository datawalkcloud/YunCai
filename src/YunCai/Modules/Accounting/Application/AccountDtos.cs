using YunCai.Modules.Accounting.Domain;

namespace YunCai.Modules.Accounting.Application
{
    /// <summary>会计科目响应 DTO（供凭证录入选择科目等场景）。</summary>
    public sealed class AccountDto
    {
        public Guid Id { get; init; }

        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        public AccountCategory Category { get; init; }

        /// <summary>科目二级类别（流动资产/非流动资产…），未指定时为 null。</summary>
        public AccountSubCategory? SubCategory { get; init; }

        /// <summary>是否为现金及现金等价物科目（现金流量表口径）。</summary>
        public bool IsCashAccount { get; init; }

        /// <summary>是否数量核算。</summary>
        public bool IsQuantityAccounted { get; init; }

        /// <summary>数量核算的计量单位。</summary>
        public string? Unit { get; init; }

        /// <summary>是否外币核算。</summary>
        public bool IsForeignCurrencyAccounted { get; init; }

        /// <summary>外币核算可选的币别标识（未开启时为空集合）。</summary>
        public IReadOnlyList<Guid> CurrencyIds { get; init; } = [];

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
            SubCategory = account.SubCategory,
            IsCashAccount = account.IsCashAccount,
            IsQuantityAccounted = account.IsQuantityAccounted,
            Unit = account.Unit,
            IsForeignCurrencyAccounted = account.IsForeignCurrencyAccounted,
            CurrencyIds = account.Currencies.Select(c => c.CurrencyId).ToList(),
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

        /// <summary>科目二级类别；明细科目忽略此值（继承上级）。</summary>
        public AccountSubCategory? SubCategory { get; init; }

        /// <summary>是否为现金及现金等价物科目；明细科目忽略此值（继承上级）。</summary>
        public bool IsCashAccount { get; init; }

        /// <summary>是否数量核算，开启时 <see cref="Unit"/> 必填；明细科目忽略此值（继承上级）。</summary>
        public bool IsQuantityAccounted { get; init; }

        /// <summary>计量单位。</summary>
        public string? Unit { get; init; }

        /// <summary>是否外币核算，开启时 <see cref="CurrencyIds"/> 至少要有一个；明细科目忽略此值（继承上级）。</summary>
        public bool IsForeignCurrencyAccounted { get; init; }

        /// <summary>外币核算可选币别标识。</summary>
        public IReadOnlyList<Guid> CurrencyIds { get; init; } = [];
    }

    /// <summary>
    /// 修改科目请求。字段为 null 表示本次不改动该属性；
    /// 编码仅末级科目可改，余额方向同样仅末级科目可改。
    /// </summary>
    public sealed class UpdateAccountRequest
    {
        /// <summary>科目名称。</summary>
        public string? Name { get; init; }

        /// <summary>科目编码（仅末级科目可修改，长度须符合账套编码方案）。</summary>
        public string? Code { get; init; }

        /// <summary>余额方向（仅末级科目可修改）。</summary>
        public BalanceDirection? Direction { get; init; }

        /// <summary>助记码。</summary>
        public string? Mnemonic { get; init; }

        /// <summary>是否启用。</summary>
        public bool? IsEnabled { get; init; }

        /// <summary>科目二级类别；与其余「null 即不改动」的字段不同，本字段按整体覆盖处理，null 表示置为未指定。</summary>
        public AccountSubCategory? SubCategory { get; init; }

        /// <summary>是否为现金及现金等价物科目。</summary>
        public bool? IsCashAccount { get; init; }

        /// <summary>是否数量核算；为 false 时清空计量单位。</summary>
        public bool? IsQuantityAccounted { get; init; }

        /// <summary>计量单位（与 <see cref="IsQuantityAccounted"/> 一起提交）。</summary>
        public string? Unit { get; init; }

        /// <summary>是否外币核算；为 false 时清空币别列表。</summary>
        public bool? IsForeignCurrencyAccounted { get; init; }

        /// <summary>外币核算可选币别标识（与 <see cref="IsForeignCurrencyAccounted"/> 一起提交）。</summary>
        public IReadOnlyList<Guid>? CurrencyIds { get; init; }
    }
}
