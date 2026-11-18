namespace YunCai.Modules.Vouchers.Domain
{
    /// <summary>
    /// 凭证附件：随凭证一起留存的原始单据影像（发票、回单、合同等）。
    /// 内容以二进制形式随凭证存储，删除凭证时级联清理。
    /// </summary>
    public class VoucherAttachment
    {
        private VoucherAttachment()
        {
        }

        private VoucherAttachment(Guid id, Guid voucherId, string fileName, string contentType, long sizeBytes, byte[] content)
        {
            Id = id;
            VoucherId = voucherId;
            FileName = fileName;
            ContentType = contentType;
            SizeBytes = sizeBytes;
            Content = content;
            UploadedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>附件标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属凭证标识。</summary>
        public Guid VoucherId { get; private set; }

        /// <summary>原始文件名（含扩展名）。</summary>
        public string FileName { get; private set; } = null!;

        /// <summary>MIME 类型，下载时回写响应头。</summary>
        public string ContentType { get; private set; } = null!;

        /// <summary>文件字节数。</summary>
        public long SizeBytes { get; private set; }

        /// <summary>上传时间（UTC）。</summary>
        public DateTimeOffset UploadedAt { get; private set; }

        /// <summary>文件内容。</summary>
        public byte[] Content { get; private set; } = [];

        /// <summary>创建附件。文件名做长度截断，空内容直接拒绝。</summary>
        public static VoucherAttachment Create(Guid voucherId, string fileName, string? contentType, byte[] content)
        {
            if (voucherId == Guid.Empty)
            {
                throw new ArgumentException("附件必须归属某张凭证。");
            }

            if (content is null || content.Length == 0)
            {
                throw new ArgumentException("附件内容为空。");
            }

            var name = string.IsNullOrWhiteSpace(fileName) ? "未命名附件" : fileName.Trim();
            if (name.Length > 200)
            {
                name = name[^200..];
            }

            return new VoucherAttachment(
                Guid.NewGuid(),
                voucherId,
                name,
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                content.LongLength,
                content);
        }
    }
}
