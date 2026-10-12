namespace KuaiJi.Modules.Reports.Domain
{
    /// <summary>报表与自定义报表定义的可预期错误（账套不存在、期间/定义不合法等），用于向前端返回友好提示。</summary>
    public sealed class ReportException : Exception
    {
        public ReportException(string message) : base(message)
        {
        }
    }
}
