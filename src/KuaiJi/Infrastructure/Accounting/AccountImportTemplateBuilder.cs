using MiniExcelLibs;

namespace KuaiJi.Infrastructure.Accounting
{
    /// <summary>
    /// 科目导入模板生成器：用 MiniExcel 生成一份 .xlsx 模板，含表头、示例数据行与填写说明工作表。
    /// 模板列与 <see cref="XlsxAccountImportReader"/> 的解析列一一对应，二者保持同步。
    /// </summary>
    public static class AccountImportTemplateBuilder
    {
        /// <summary>下载时使用的文件名。</summary>
        public const string FileName = "科目导入模板.xlsx";

        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>生成模板文件的字节内容。</summary>
        public static byte[] Build()
        {
            // 示例数据：演示顶级科目、明细科目（类别/方向留空继承上级）、损益类科目的填法。
            var accounts = new List<Dictionary<string, object?>>
            {
                Row("1001", "库存现金", "资产", "借", "", "KCXJ", "是"),
                Row("1002", "银行存款", "资产", "借", "", "YHCK", "是"),
                Row("100201", "工商银行", "", "", "1002", "", "是"),
                Row("2202", "应付账款", "负债", "贷", "", "YFZK", "是"),
                Row("6001", "主营业务收入", "损益", "贷", "", "ZYYWSR", "是"),
            };

            var instructions = new List<Dictionary<string, object?>>
            {
                Note("科目编码", "必填。账套内唯一；明细科目编码须以其上级科目编码为前缀，如 1002 → 100201。"),
                Note("科目名称", "必填。"),
                Note("类别", "顶级科目必填，取值：资产 / 负债 / 权益 / 成本 / 损益；明细科目留空，自动继承上级。"),
                Note("余额方向", "取值：借 / 贷。留空时顶级科目按类别取默认值，明细科目继承上级。"),
                Note("上级科目编码", "选填。留空表示顶级科目；填写后作为对应科目的明细科目导入。"),
                Note("助记码", "选填。拼音首字母等，便于检索。"),
                Note("是否启用", "选填，取值：是 / 否，留空默认为「是」。"),
                Note("导入说明", "编码已存在的科目会被跳过；请确保上级科目在本表中位置靠前或已存在于账套。"),
            };

            var sheets = new Dictionary<string, object>
            {
                ["科目"] = accounts,
                ["填写说明"] = instructions,
            };

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, sheets, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        private static Dictionary<string, object?> Row(
            string code, string name, string category, string direction, string parentCode, string mnemonic, string enabled)
        {
            return new Dictionary<string, object?>
            {
                ["科目编码"] = code,
                ["科目名称"] = name,
                ["类别"] = category,
                ["余额方向"] = direction,
                ["上级科目编码"] = parentCode,
                ["助记码"] = mnemonic,
                ["是否启用"] = enabled,
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
