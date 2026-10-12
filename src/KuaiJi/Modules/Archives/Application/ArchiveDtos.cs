using KuaiJi.Modules.Archives.Domain;

namespace KuaiJi.Modules.Archives.Application
{
    /// <summary>电子档案列表项 DTO（不含文件内容）。</summary>
    public sealed class ArchiveItemDto
    {
        public Guid Id { get; init; }

        public string FileName { get; init; } = null!;

        public string Category { get; init; } = null!;

        public string DocType { get; init; } = null!;

        public DateTime UploadedAt { get; init; }

        public string? RelatedDoc { get; init; }

        public string? RelatedNo { get; init; }

        public string? VoucherText { get; init; }

        public string? Matter { get; init; }

        public string? CheckStatus { get; init; }

        public string StatusText { get; init; } = null!;

        public string Uploader { get; init; } = null!;

        public string Source { get; init; } = null!;

        public bool IsArchived { get; init; }

        public static string StatusTextOf(ArchiveFileStatus status)
            => status == ArchiveFileStatus.Audited ? "已审核" : "未审核";

        public static ArchiveItemDto FromDomain(ArchiveItem item) => new()
        {
            Id = item.Id,
            FileName = item.FileName,
            Category = item.Category,
            DocType = item.DocType,
            UploadedAt = item.UploadedAt,
            RelatedDoc = item.RelatedDoc,
            RelatedNo = item.RelatedNo,
            VoucherText = item.VoucherText,
            Matter = item.Matter,
            CheckStatus = item.CheckStatus,
            StatusText = StatusTextOf(item.Status),
            Uploader = item.Uploader,
            Source = item.Source,
            IsArchived = item.IsArchived,
        };
    }

    /// <summary>电子档案已用空间 DTO。</summary>
    public sealed class ArchiveUsageDto
    {
        /// <summary>已用空间（字节）。</summary>
        public long UsedBytes { get; init; }

        /// <summary>空间总量（字节，免费版 10 MB）。</summary>
        public long TotalBytes { get; init; } = ArchiveItem.MaxFileBytes;
    }

    /// <summary>新建 / 修改电子档案请求。</summary>
    public sealed class SaveArchiveRequest
    {
        public string FileName { get; init; } = null!;

        public string Category { get; init; } = null!;

        public string DocType { get; init; } = null!;

        /// <summary>文件内容（Base64），新建时可空表示仅登记元数据。</summary>
        public string? FileBase64 { get; init; }

        public string? RelatedDoc { get; init; }

        public string? RelatedNo { get; init; }

        public string? VoucherText { get; init; }

        public string? Matter { get; init; }

        public string? CheckStatus { get; init; }

        public string Uploader { get; init; } = null!;

        public string Source { get; init; } = "财务系统";
    }

    /// <summary>批量归档请求。</summary>
    public sealed class ArchiveIdsRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];
    }

    /// <summary>批量审核请求。</summary>
    public sealed class AuditArchiveRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];

        public bool Audited { get; init; }
    }
}
