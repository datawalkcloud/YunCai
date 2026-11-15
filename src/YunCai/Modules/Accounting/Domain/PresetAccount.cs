using YunCai.Modules.AccountSets.Domain;

namespace YunCai.Modules.Accounting.Domain
{
    /// <summary>
    /// 某个会计准则的建账预置科目（<c>preset_accounts</c> 表）。
    /// 它是参考数据，不属于任何账套：新建账套时按账套准则把清单复制成该账套的 <see cref="Account"/>。
    /// 层级用上级科目编码表达（顶级为 null），不依赖账套的编码方案。
    /// </summary>
    public class PresetAccount
    {
        // EF Core 物化实体所需的无参构造函数。
        private PresetAccount()
        {
        }

        public PresetAccount(
            int id,
            AccountingStandard standard,
            string code,
            string name,
            AccountCategory category,
            BalanceDirection direction,
            string? parentCode,
            int orderNo)
        {
            Id = id;
            Standard = standard;
            Code = code;
            Name = name;
            Category = category;
            Direction = direction;
            ParentCode = parentCode;
            OrderNo = orderNo;
        }

        /// <summary>清单行主键，由预置清单生成时按顺序编号，迁移灌数与 HasData 共用。</summary>
        public int Id { get; private set; }

        /// <summary>所属会计准则。同一准则内科目编码唯一。</summary>
        public AccountingStandard Standard { get; private set; }

        /// <summary>科目编码。</summary>
        public string Code { get; private set; } = null!;

        /// <summary>科目名称。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>科目类别。清单里每行都带，建账时明细科目仍以上级为准。</summary>
        public AccountCategory Category { get; private set; }

        /// <summary>余额方向。</summary>
        public BalanceDirection Direction { get; private set; }

        /// <summary>上级科目编码；一级科目为 null。</summary>
        public string? ParentCode { get; private set; }

        /// <summary>清单内顺序号，用于按准则原序展示与建树时父先子后。</summary>
        public int OrderNo { get; private set; }
    }
}
