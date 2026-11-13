namespace YunCai.Modules.Accounting.Application
{
    /// <summary>
    /// 科目导入文件读取器。负责将上传的模板文件流解析为原始科目行集合，屏蔽具体文件格式（如 .xlsx）。
    /// </summary>
    public interface IAccountImportReader
    {
        /// <summary>
        /// 从文件流解析出全部原始科目行。解析失败（格式不符、缺列等）时抛出 <see cref="AccountImportException"/>。
        /// </summary>
        IReadOnlyList<ImportedAccountRow> Read(Stream stream);
    }

    /// <summary>科目导入过程中的可预期错误（如文件格式不符、内容缺失），用于向前端返回友好提示。</summary>
    public sealed class AccountImportException : Exception
    {
        public AccountImportException(string message) : base(message)
        {
        }
    }
}
