namespace KuaiJi.SharedKernel.Infrastructure.Pdf
{
    /// <summary>
    /// 一张待渲染成 PDF 的账页/报表：大标题 + 页头信息 + 列（可带上级分组）+ 表体行 + 表尾说明。
    /// 各模块的打印 PDF 统一落到此结构，版式由 <see cref="PrintSheetPdfBuilder"/> 一处控制。
    /// </summary>
    public sealed class PrintSheet
    {
        /// <summary>页面大标题，如「总账」「明细账」「科目余额表」。</summary>
        public required string Title { get; init; }

        /// <summary>页头信息项，按对齐方式分左/中/右三段排在一行内。</summary>
        public IReadOnlyList<PrintMeta> Meta { get; init; } = [];

        /// <summary>表头列定义，顺序即表体每行单元格的顺序。</summary>
        public required IReadOnlyList<PrintColumn> Columns { get; init; }

        /// <summary>表体行，含各段小计与最后的总计行。</summary>
        public required IReadOnlyList<PrintRow> Rows { get; init; }

        /// <summary>表尾说明（口径提示、币种说明等），逐行小字输出。</summary>
        public IReadOnlyList<string> Notes { get; init; } = [];

        /// <summary>横向打印（宽表用），默认纵向 A4。</summary>
        public bool Landscape { get; init; }

        /// <summary>打印时间，输出在页脚左侧；省略则页脚只有页码。</summary>
        public DateTimeOffset? PrintedAt { get; init; }

        /// <summary>表体为空时的一行提示；省略则输出空表体。</summary>
        public string? EmptyText { get; init; }
    }

    /// <summary>页头信息项。</summary>
    /// <param name="Text">显示文本。</param>
    /// <param name="Align">left / center / right，决定落在页头行的哪一段。</param>
    public sealed record PrintMeta(string Text, string Align = PdfAlign.Left);

    /// <summary>表头列。</summary>
    /// <param name="Text">列名。</param>
    /// <param name="Group">上级分组表头（如「期初余额」）；相邻同组列在表头第一行合并为一格。为 null 时该列跨两行。</param>
    /// <param name="Width">列宽，如「12%」；省略则由内容自适应。</param>
    /// <param name="Align">本列单元格的默认对齐。</param>
    public sealed record PrintColumn(
        string Text,
        string? Group = null,
        string? Width = null,
        string Align = PdfAlign.Left);

    /// <summary>表体单元格。传入 decimal / decimal? 时按金额格式输出，其余按文本输出。</summary>
    /// <param name="Value">单元格内容：null 留白，decimal 走金额格式，其它调用 ToString()。</param>
    /// <param name="ColSpan">跨列数（小计、总计行用）。</param>
    /// <param name="Align">覆盖所属列的对齐；省略则跟随列定义。</param>
    /// <param name="Css">附加样式类，见 <see cref="PdfCss"/>：缩进、加粗。</param>
    public sealed record PrintCell(
        object? Value,
        int ColSpan = 1,
        string? Align = null,
        string Css = "")
    {
        /// <summary>空单元格，用于占位补齐列数。必须写实参名：`new(null)` 会绑到记录的拷贝构造。</summary>
        public static readonly PrintCell Blank = new(Value: null);
    }

    /// <summary>表体一行。</summary>
    /// <param name="Cells">单元格，按列顺序。</param>
    /// <param name="Css">行级样式类，见 <see cref="PdfCss"/>。</param>
    public sealed record PrintRow(IReadOnlyList<PrintCell> Cells, string Css = "");

    /// <summary>对齐方式常量。</summary>
    public static class PdfAlign
    {
        public const string Left = "left";
        public const string Center = "center";
        public const string Right = "right";
    }

    /// <summary>可用的行/单元格样式类名。</summary>
    public static class PdfCss
    {
        /// <summary>一级缩进（科目名称等）。</summary>
        public const string Indent1 = "i1";

        /// <summary>二级缩进（明细科目、末级科目）。</summary>
        public const string Indent2 = "i2";

        /// <summary>小计、合计等加重行。</summary>
        public const string Strong = "strong";
    }
}
