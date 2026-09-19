using KuaiJi.Domain.Journals;

namespace KuaiJi.Application.Journals
{
    /// <summary>收支类别 DTO。</summary>
    public sealed class EntryCategoryDto
    {
        public Guid Id { get; init; }

        /// <summary>类别类型（1=收入，2=支出）。</summary>
        public int Type { get; init; }

        public string TypeText { get; init; } = null!;

        public string Name { get; init; } = null!;

        public static EntryCategoryDto FromDomain(EntryCategory category) => new()
        {
            Id = category.Id,
            Type = (int)category.Type,
            TypeText = TypeTextOf(category.Type),
            Name = category.Name,
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
    }

    /// <summary>修改收支类别请求（仅支持改名，类型不可变更）。</summary>
    public sealed class UpdateEntryCategoryRequest
    {
        public string Name { get; init; } = null!;
    }
}
