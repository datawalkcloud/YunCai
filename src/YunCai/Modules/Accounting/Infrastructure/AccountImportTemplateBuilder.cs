using MiniExcelLibs;

namespace YunCai.Modules.Accounting.Infrastructure
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
            // 示例：用超出标准编码长度的数据展示系统自动拆分与名称提取能力。
            var accounts = new List<Dictionary<string, object?>>
            {
                Row("22020", "应付账款", "资产", "借", "", "", "是"),
                Row("2202003", "应付账款-韩帮贵", "资产", "借", "", "", "是"),
            };

            var instructions = new List<Dictionary<string, object?>>
            {
                Note("科目编码", "必填。账套内唯一；系统按编码方案自动拆分层级（如 4-3-2 方案下 222100101 自动拆为 3 级）。"),
                Note("科目名称", "必填。"),
                Note("类别", "顶级科目必填，取值：资产 / 负债 / 权益 / 成本 / 损益；明细科目留空，自动继承上级。"
                    + "外部软件导出的细分口径也认，如流动资产、非流动负债、所有者权益、营业收入、期间费用、营业成本及税金、所得税。"),
                Note("余额方向", "取值：借 / 贷。留空时顶级科目按类别取默认值，明细科目继承上级。"),
                Note("上级科目编码", "选填。留空时系统按编码方案自动推断层级的上级关系并自动创建缺失上级。"),
                Note("助记码", "选填。拼音首字母等，便于检索。"),
                Note("是否启用", "选填，取值：是 / 否，留空默认为「是」。"),
                Note("兼容表头", "金蝶等软件导出的科目表可直接上传：编码=科目编码、名称=科目名称、科目类别=类别、"
                    + "方向=余额方向、上级编码=上级科目编码、科目简码=助记码、状态=是否启用；其余列（辅助核算类别、是否现金科目、是否平行科目）忽略。"),
                Note("导入说明", "编码已存在的科目会被跳过；缺失的上级科目会自动创建（名称以「科目」为前缀，可导入后修改）。"),
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
