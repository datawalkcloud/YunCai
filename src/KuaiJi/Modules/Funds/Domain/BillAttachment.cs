namespace KuaiJi.Modules.Funds.Domain
{
    /// <summary>
    /// 票据附件：票面影像、回单等原始单据。内容以二进制随票据存储，删除票据时一并清理。
    /// </summary>
    public class BillAttachment
    {
        private BillAttachment()
        {
        }

        private BillAttachment(Guid id, Guid billId, string fileName, string contentType, long sizeBytes, byte[] content)
        {
            Id = id;
            BillId = billId;
            FileName = fileName;
            ContentType = contentType;
            SizeBytes = sizeBytes;
            Content = content;
            UploadedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>附件标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属票据标识。</summary>
        public Guid BillId { get; private set; }

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
        public static BillAttachment Create(Guid billId, string fileName, string? contentType, byte[] content)
        {
            if (billId == Guid.Empty)
            {
                throw new ArgumentException("附件必须归属某张票据。", nameof(billId));
            }

            if (content is null || content.Length == 0)
            {
                throw new ArgumentException("附件内容为空。", nameof(content));
            }

            var name = string.IsNullOrWhiteSpace(fileName) ? "未命名附件" : fileName.Trim();
            if (name.Length > 200)
            {
                name = name[^200..];
            }

            return new BillAttachment(
                Guid.NewGuid(),
                billId,
                name,
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                content.LongLength,
                content);
        }
    }
}
