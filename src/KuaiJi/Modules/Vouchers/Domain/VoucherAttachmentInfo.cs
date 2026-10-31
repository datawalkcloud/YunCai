namespace KuaiJi.Modules.Vouchers.Domain
{
    /// <summary>附件元数据投影（不含文件内容），用于列表与详情展示。</summary>
    public sealed record VoucherAttachmentInfo(
        Guid Id,
        Guid VoucherId,
        string FileName,
        string ContentType,
        long SizeBytes,
        DateTimeOffset UploadedAt);
}
