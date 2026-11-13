using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Vouchers.Application
{
    /// <summary>凭证附件的展示信息。</summary>
    public sealed class VoucherAttachmentDto
    {
        public Guid Id { get; init; }

        public string FileName { get; init; } = null!;

        public string ContentType { get; init; } = null!;

        public long SizeBytes { get; init; }

        public DateTimeOffset UploadedAt { get; init; }

        public static VoucherAttachmentDto FromDomain(VoucherAttachmentInfo info) => new()
        {
            Id = info.Id,
            FileName = info.FileName,
            ContentType = info.ContentType,
            SizeBytes = info.SizeBytes,
            UploadedAt = info.UploadedAt,
        };
    }
}
