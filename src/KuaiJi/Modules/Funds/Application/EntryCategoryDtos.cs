using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Funds.Domain;

namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>收支类别 DTO（含智能匹配关键字与默认对方科目）。</summary>
    public sealed class EntryCategoryDto
    {
        public Guid Id { get; init; }

        /// <summary>类别类型（1=收入，2=支出）。</summary>
        public int Type { get; init; }

        public string TypeText { get; init; } = null!;

        public string Name { get; init; } = null!;

        /// <summary>智能匹配关键字（逗号分隔）；流水导入时按摘要包含关系自动归类。</summary>
        public string? SmartKeywords { get; init; }

        /// <summary>默认对方科目（由流水生成凭证时使用）。</summary>
        public Guid? VoucherSubjectId { get; init; }

        public string? VoucherSubjectCode { get; init; }

        public string? VoucherSubjectName { get; init; }

        public static EntryCategoryDto FromDomain(EntryCategory category, Account? subject = null) => new()
        {
            Id = category.Id,
            Type = (int)category.Type,
            TypeText = TypeTextOf(category.Type),
            Name = category.Name,
            SmartKeywords = category.SmartKeywords,
            VoucherSubjectId = category.VoucherSubjectId,
            VoucherSubjectCode = subject?.Code,
            VoucherSubjectName = subject?.Name,
        };

        internal static string TypeTextOf(EntryCategoryType type) => type switch
        {
            EntryCategoryType.Income => "收入",
            EntryCategoryType.Expense => "支出",
            _ => type.ToString(),
        };
    }

    /// <summary>新建收支类别请求。</summary>
    public sealed class CreateEntryCategoryRequest
    {
        /// <summary>类别类型（1=收入，2=支出）。</summary>
        public int Type { get; init; }

        public string Name { get; init; } = null!;

        /// <summary>智能匹配关键字（逗号分隔，选填）。</summary>
        public string? SmartKeywords { get; init; }

        /// <summary>默认对方科目（选填；须为末级启用科目）。</summary>
        public Guid? VoucherSubjectId { get; init; }
    }

    /// <summary>修改收支类别请求（类型不可变更）。</summary>
    public sealed class UpdateEntryCategoryRequest
    {
        public string Name { get; init; } = null!;

        /// <summary>智能匹配关键字；null = 保持不变，空串 = 清除。</summary>
        public string? SmartKeywords { get; init; }

        /// <summary>默认对方科目；null = 保持不变，Guid.Empty = 清除，其余 = 设置。</summary>
        public Guid? VoucherSubjectId { get; init; }
    }
}
