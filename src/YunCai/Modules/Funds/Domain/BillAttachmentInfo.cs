namespace YunCai.Modules.Funds.Domain
{
    /// <summary>票据附件摘要（不含文件内容，供列表展示）。</summary>
    public sealed record BillAttachmentInfo(Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);
}
