namespace YunCai.Modules.Reports.Domain.CashFlow
{
    /// <summary>
    /// 现金流量表行取数公式（按账套 + 标准行次保存）。未保存的行回落到
    /// <see cref="CashFlowFormulaDefaults"/> 的系统默认公式，因此「公式法」开箱即有数，
    /// 用户只需覆盖需要调整的行。
    /// </summary>
    public sealed class CashFlowFormula
    {
        /// <summary>单行公式的最大长度。</summary>
        public const int MaxExpressionLength = 500;

        public Guid Id { get; private set; }

        public Guid AccountSetId { get; private set; }

        /// <summary>标准行次（1-35）。</summary>
        public int Line { get; private set; }

        /// <summary>取数公式。</summary>
        public string Expression { get; private set; } = null!;

        public DateTime UpdatedAt { get; private set; }

        private CashFlowFormula()
        {
        }

        /// <summary>创建一行自定义公式。</summary>
        public static CashFlowFormula Create(Guid accountSetId, int line, string expression, DateTime updatedAt)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ReportException("必须指定所属账套。");
            }

            if (!CashFlowStandardRows.ByLine.ContainsKey(line))
            {
                throw new ReportException($"现金流量表不存在行次 {line}。");
            }

            return new CashFlowFormula
            {
                Id = Guid.NewGuid(),
                AccountSetId = accountSetId,
                Line = line,
                Expression = Normalize(expression, line),
                UpdatedAt = updatedAt,
            };
        }

        /// <summary>覆盖该行公式。</summary>
        public void Update(string expression, DateTime updatedAt)
        {
            Expression = Normalize(expression, Line);
            UpdatedAt = updatedAt;
        }

        private static string Normalize(string expression, int line)
        {
            var value = (expression ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                throw new ReportException($"第 {line} 行取数公式不能为空；如需恢复默认公式请删除该行配置。");
            }

            if (value.Length > MaxExpressionLength)
            {
                throw new ReportException($"第 {line} 行取数公式不能超过 {MaxExpressionLength} 个字符。");
            }

            return value;
        }
    }
}
