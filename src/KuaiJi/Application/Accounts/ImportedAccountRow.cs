namespace KuaiJi.Application.Accounts
{
    /// <summary>
    /// 从科目导入模板解析出的一行原始数据。字段与模板列一一对应，尚未做类别/方向/上级映射，
    /// 类别与方向保留为原始文本，由应用服务负责解析与校验。
    /// </summary>
    public sealed class ImportedAccountRow
    {
        /// <summary>数据所在行号（从 1 开始，供错误提示定位；不含表头）。</summary>
        public int RowNumber { get; init; }

        /// <summary>科目编码。</summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>科目名称。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>科目类别原文（资产/负债/权益/成本/损益）；明细科目可留空，继承上级。</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>余额方向原文（借/贷）；明细科目留空时继承上级。</summary>
        public string Direction { get; init; } = string.Empty;

        /// <summary>上级科目编码；留空表示顶级科目。</summary>
        public string ParentCode { get; init; } = string.Empty;

        /// <summary>助记码（可空）。</summary>
        public string Mnemonic { get; init; } = string.Empty;

        /// <summary>是否启用。模板中「是否启用」列为空或标记为启用时为 true。</summary>
        public bool IsEnabled { get; init; } = true;
    }
}
