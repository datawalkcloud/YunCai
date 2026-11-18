namespace YunCai.Modules.Reports.Application
{
    /// <summary>现金流量表某一行的取数公式。</summary>
    public sealed class CashFlowFormulaDto
    {
        /// <summary>标准行次。</summary>
        public int Line { get; init; }

        /// <summary>行次名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>当前生效的公式（已保存的覆盖优先，否则为默认公式）。</summary>
        public string Expression { get; init; } = null!;

        /// <summary>系统默认公式，便于前端「恢复默认」。</summary>
        public string DefaultExpression { get; init; } = null!;

        /// <summary>true=该行使用账套自定义公式；false=使用默认公式。</summary>
        public bool IsCustomized { get; init; }
    }

    /// <summary>现金流量表取数公式配置（35 个标准行次）。</summary>
    public sealed class CashFlowFormulaSettingsDto
    {
        public IReadOnlyList<CashFlowFormulaDto> Lines { get; init; } = [];
    }

    /// <summary>保存单行公式；Expression 传空串表示删除该行覆盖、恢复默认公式。</summary>
    public sealed class SaveCashFlowFormulaItem
    {
        public int Line { get; init; }

        public string? Expression { get; init; }
    }

    /// <summary>
    /// 保存取数公式。只处理请求中出现的行次，未出现的行保持不变；
    /// Expression 为空的行会删除自定义公式并回落默认公式。
    /// </summary>
    public sealed class SaveCashFlowFormulasRequest
    {
        public IReadOnlyList<SaveCashFlowFormulaItem> Formulas { get; init; } = [];
    }
}
