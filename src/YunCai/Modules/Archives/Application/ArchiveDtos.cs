using YunCai.Modules.Archives.Domain;

namespace YunCai.Modules.Archives.Application
{
    /// <summary>电子档案列表项 DTO（不含文件内容）。</summary>
    public sealed class ArchiveItemDto
    {
        public Guid Id { get; init; }

        public string FileName { get; init; } = null!;

        public string Category { get; init; } = null!;

        public string DocType { get; init; } = null!;

        public DateTime UploadedAt { get; init; }

        /// <summary>文件大小（字节）。</summary>
        public long FileSize { get; init; }

        public string? RelatedDoc { get; init; }

        public string? RelatedNo { get; init; }

        public string? VoucherText { get; init; }

        public string? Matter { get; init; }

        public string? CheckStatus { get; init; }

        public string StatusText { get; init; } = null!;

        public string Uploader { get; init; } = null!;

        public string Source { get; init; } = null!;

        public bool IsArchived { get; init; }

        /// <summary>是否标记为纸质档案。</summary>
        public bool IsPaper { get; init; }

        /// <summary>归档时间，可为空（未归档）。</summary>
        public DateTime? ArchivedAt { get; init; }

        /// <summary>归档人，可为空（未归档）。</summary>
        public string? ArchivedBy { get; init; }

        public static string StatusTextOf(ArchiveFileStatus status)
            => status == ArchiveFileStatus.Audited ? "已审核" : "未审核";

        public static ArchiveItemDto FromDomain(ArchiveItem item) => new()
        {
            Id = item.Id,
            FileName = item.FileName,
            Category = item.Category,
            DocType = item.DocType,
            UploadedAt = item.UploadedAt,
            FileSize = item.FileSize,
            RelatedDoc = item.RelatedDoc,
            RelatedNo = item.RelatedNo,
            VoucherText = item.VoucherText,
            Matter = item.Matter,
            CheckStatus = item.CheckStatus,
            StatusText = StatusTextOf(item.Status),
            Uploader = item.Uploader,
            Source = item.Source,
            IsArchived = item.IsArchived,
            IsPaper = item.IsPaper,
            ArchivedAt = item.ArchivedAt,
            ArchivedBy = item.ArchivedBy,
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

        /// <summary>是否标记为纸质档案。</summary>
        public bool IsPaper { get; init; }
    }

    /// <summary>批量标记纸质档案请求。</summary>
    public sealed class PaperArchiveRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];

        public bool IsPaper { get; init; } = true;
    }

    /// <summary>批量关联凭证请求（VoucherText 为空表示取消关联）。</summary>
    public sealed class LinkVoucherRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];

        public string? VoucherText { get; init; }
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

    /// <summary>由档案生成凭证的请求：选中档案合并为一张「借 A 科目 / 贷 B 科目」凭证。</summary>
    public sealed class GenerateVoucherFromArchivesRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];

        public DateOnly Date { get; init; }

        /// <summary>凭证字，默认「记」。</summary>
        public string Word { get; init; } = "记";

        /// <summary>分录摘要；为空时取档案事项 / 文件名。</summary>
        public string? Summary { get; init; }

        public decimal Amount { get; init; }

        public Guid DebitAccountId { get; init; }

        public Guid CreditAccountId { get; init; }
    }
}
