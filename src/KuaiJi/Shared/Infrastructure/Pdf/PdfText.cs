using System.Globalization;
using System.Text;

namespace KuaiJi.SharedKernel.Infrastructure.Pdf
{
    /// <summary>PDF 输出的文本与数值格式工具，保证各账页/报表打印稿口径一致。</summary>
    public static class PdfText
    {
        /// <summary>HTML 转义；null 与空串都返回 <see cref="Nbsp"/>。</summary>
        public static string Esc(object? value)
        {
            var text = value switch
            {
                null => string.Empty,
                string s => s,
                DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };

            return text.Length == 0
                ? Nbsp
                : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        /// <summary>空单元格占位：PaperSharp 不给无内容的单元格画边框，必须填 &amp;nbsp;。</summary>
        public const string Nbsp = "&nbsp;";

        /// <summary>金额：0 与 null 留白（账页惯例），否则千分位两位小数。</summary>
        public static string Amount(decimal? value)
            => value is { } amount && amount != 0m
                ? amount.ToString("#,##0.00", CultureInfo.InvariantCulture)
                : Nbsp;

        /// <summary>金额：始终显示数值，0 也显示 0.00（报表合计、资金口径用）。</summary>
        public static string AmountAlways(decimal? value)
            => (value ?? 0m).ToString("#,##0.00", CultureInfo.InvariantCulture);
    }
}
