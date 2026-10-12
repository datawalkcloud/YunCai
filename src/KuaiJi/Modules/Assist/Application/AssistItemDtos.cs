using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Assist.Application
{
    /// <summary>辅助核算档案 DTO（核算项目）。</summary>
    public sealed class AssistItemDto
    {
        /// <summary>档案标识。</summary>
        public Guid Id { get; init; }

        /// <summary>所属辅助核算类别。</summary>
        public AssistType Type { get; init; }

        /// <summary>类别中文名。</summary>
        public string TypeText { get; init; } = null!;

        /// <summary>档案编码，同类别内唯一；层级型类别以上级编码为前缀。</summary>
        public string Code { get; init; } = null!;

        /// <summary>档案名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>上级档案标识；顶级档案为 null。</summary>
        public Guid? ParentId { get; init; }

        /// <summary>档案级次，从 1 开始。</summary>
        public int Level { get; init; }

        /// <summary>是否末级档案（仅末级档案可用于记账）。</summary>
        public bool IsLeaf { get; init; }

        /// <summary>是否启用（停用后不可在新凭证中选用）。</summary>
        public bool IsEnabled { get; init; }

        /// <summary>联系人/税号等资料，可为空。</summary>
        public string? Contact { get; init; }

        /// <summary>备注，可为空。</summary>
        public string? Remark { get; init; }

        public static AssistItemDto FromDomain(AssistItem item) => new()
        {
            Id = item.Id,
            Type = item.Type,
            TypeText = AssistTypeText.Of(item.Type),
            Code = item.Code,
            Name = item.Name,
            ParentId = item.ParentId,
            Level = item.Level,
            IsLeaf = item.IsLeaf,
            IsEnabled = item.IsEnabled,
            Contact = item.Contact,
            Remark = item.Remark,
        };
    }

    /// <summary>新增辅助核算档案请求。</summary>
    public sealed class CreateAssistItemRequest
    {
        /// <summary>辅助核算类别（须在账套已启用列表内）。</summary>
        public AssistType Type { get; init; }

        /// <summary>档案编码；层级型类别须以上级编码为前缀。</summary>
        public string Code { get; init; } = null!;

        /// <summary>档案名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>上级档案标识；为空表示新建顶级档案。仅层级型类别允许指定。</summary>
        public Guid? ParentId { get; init; }

        /// <summary>联系人/税号等资料，可为空。</summary>
        public string? Contact { get; init; }

        /// <summary>备注，可为空。</summary>
        public string? Remark { get; init; }
    }

    /// <summary>
    /// 修改辅助核算档案请求。编码与所属类别一经创建不可变更（变更会破坏历史分录的维度归属），
    /// 故仅支持改名、联系方式与备注。
    /// </summary>
    public sealed class UpdateAssistItemRequest
    {
        /// <summary>档案名称；传 null 表示不修改。</summary>
        public string? Name { get; init; }

        /// <summary>联系人/税号等资料；传 null 表示不修改，传空串表示清除。</summary>
        public string? Contact { get; init; }

        /// <summary>备注；传 null 表示不修改，传空串表示清除。</summary>
        public string? Remark { get; init; }
    }

    /// <summary>启用/停用档案请求。</summary>
    public sealed class SetAssistItemEnabledRequest
    {
        /// <summary>是否启用。</summary>
        public bool Enabled { get; init; }
    }

    /// <summary>辅助核算类别概览：类别、中文名、账套是否启用、该类别下的档案数。</summary>
    public sealed class AssistCategoryStatDto
    {
        /// <summary>辅助核算类别。</summary>
        public AssistType Type { get; init; }

        /// <summary>类别中文名。</summary>
        public string TypeText { get; init; } = null!;

        /// <summary>该类别在本账套是否已启用。</summary>
        public bool IsEnabled { get; init; }

        /// <summary>该类别下的档案数量。</summary>
        public int ItemCount { get; init; }
    }

    /// <summary>科目的一条辅助核算维度设置（响应）。</summary>
    public sealed class AccountAssistTypeDto
    {
        /// <summary>辅助核算类别。</summary>
        public AssistType Type { get; init; }

        /// <summary>类别中文名。</summary>
        public string TypeText { get; init; } = null!;

        /// <summary>制单时该维度是否必录。</summary>
        public bool IsRequired { get; init; }

        public static AccountAssistTypeDto FromDomain(AccountAssistType setting) => new()
        {
            Type = setting.Type,
            TypeText = AssistTypeText.Of(setting.Type),
            IsRequired = setting.IsRequired,
        };
    }

    /// <summary>科目辅助核算维度设置项（请求）：类别 + 是否必录。</summary>
    public sealed class AccountAssistTypeRequest
    {
        /// <summary>辅助核算类别（须在账套已启用列表内）。</summary>
        public AssistType Type { get; init; }

        /// <summary>制单时是否必录；默认必录。</summary>
        public bool IsRequired { get; init; } = true;
    }
}
