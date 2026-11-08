namespace KuaiJi.Modules.Archives.Application
{
    /// <summary>分类树节点 DTO（扁平列表，前端按 ParentId 组树）。</summary>
    public sealed class ArchiveCategoryDto
    {
        public Guid Id { get; init; }

        public Guid? ParentId { get; init; }

        public string Name { get; init; } = null!;

        public int SortOrder { get; init; }

        /// <summary>系统内置节点：不可重命名 / 删除，但可以在其下新增子节点。</summary>
        public bool IsSystem { get; init; }

        public static ArchiveCategoryDto FromDomain(Domain.ArchiveCategory category) => new()
        {
            Id = category.Id,
            ParentId = category.ParentId,
            Name = category.Name,
            SortOrder = category.SortOrder,
            IsSystem = category.IsSystem,
        };
    }

    /// <summary>新增 / 重命名分类请求（新增时 ParentId 为上级节点，空表示根节点）。</summary>
    public sealed class SaveArchiveCategoryRequest
    {
        public Guid? ParentId { get; init; }

        public string Name { get; init; } = null!;
    }
}
