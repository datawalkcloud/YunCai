namespace KuaiJi.Modules.Vouchers.Application
{
    /// <summary>
    /// 凭证导入文件读取器。负责将上传的文件流解析为原始分录行集合，屏蔽具体文件格式（如 .xls）。
    /// </summary>
    public interface IVoucherImportReader
    {
        /// <summary>
        /// 从文件流解析出全部原始分录行。解析失败（格式不符、缺列等）时抛出 <see cref="VoucherImportException"/>。
        /// </summary>
        IReadOnlyList<ImportedVoucherRow> Read(Stream stream);
    }

    /// <summary>凭证导入过程中的可预期错误（如文件格式不符、内容缺失），用于向前端返回友好提示。</summary>
    public sealed class VoucherImportException : Exception
    {
        public VoucherImportException(string message) : base(message)
        {
        }
    }
}
