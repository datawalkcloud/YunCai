using MiniExcelLibs;

namespace KuaiJi.Infrastructure.Journals
{
    /// <summary>
    /// 日记账导入模板生成器：用 MiniExcel 生成一份 .xlsx 模板，含表头、示例数据行与填写说明工作表。
    /// 模板列与 <see cref="XlsJournalImportReader"/> 的解析列一一对应，二者保持同步。
    /// </summary>
    public static class JournalImportTemplateBuilder
    {
        /// <summary>下载时使用的文件名。</summary>
        public const string FileName = "日记账导入模板.xlsx";

        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>生成模板文件的字节内容。</summary>
        public static byte[] Build()
        {
            var rows = new List<Dictionary<string, object?>>
            {
                Row("2024-01-05", "收到货款", "销售收入", "甲公司", 10000m, 0m),
                Row("2024-01-08", "支付办公费", "管理费用", "乙商店", 0m, 350m),
            };

            var instructions = new List<Dictionary<string, object?>>
            {
                Note("日期", "必填，格式如 2024-01-05。"),
                Note("摘要", "必填。"),
                Note("类别", "选填，收支类别，如“销售收入”“管理费用”。"),
                Note("往来", "选填，往来单位或个人。"),
                Note("收入", "收入金额（借方），无则填 0 或留空。"),
                Note("支出", "支出金额（贷方），无则填 0 或留空。"),
                Note("导入说明", "每行「收入」与「支出」应只填其一；两者均为零的行会被跳过。导出文件附带的序号/余额/关联凭证号列在导入时忽略。"),
            };

            var sheets = new Dictionary<string, object>
            {
                ["日记账"] = rows,
                ["填写说明"] = instructions,
            };

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, sheets, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        private static Dictionary<string, object?> Row(string date, string summary, string category, string counterparty, decimal income, decimal expense)
        {
            return new Dictionary<string, object?>
            {
                ["日期"] = date,
                ["摘要"] = summary,
                ["类别"] = category,
                ["往来"] = counterparty,
                ["收入"] = income,
                ["支出"] = expense,
            };
        }

        private static Dictionary<string, object?> Note(string column, string description)
        {
            return new Dictionary<string, object?>
            {
                ["列名"] = column,
                ["填写说明"] = description,
            };
        }
    }
}
