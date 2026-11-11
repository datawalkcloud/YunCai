namespace KuaiJi.Modules.Reports.Domain.CashFlow
{
    /// <summary>
    /// 取数公式求值器：按行次升序计算，行次引用只能指向更小的行（保存时已校验），
    /// 因此单次遍历即可完成全部 35 行。
    /// </summary>
    public static class CashFlowFormulaEvaluator
    {
        /// <summary>计算全部行次金额（未四舍五入）。</summary>
        public static Dictionary<int, decimal> Evaluate(
            IReadOnlyDictionary<int, CashFlowFormulaNode> formulas,
            ICashFlowFormulaContext context)
        {
            var values = new Dictionary<int, decimal>(formulas.Count);
            foreach (var line in formulas.Keys.OrderBy(x => x))
            {
                values[line] = EvaluateNode(formulas[line], line, context, values);
            }

            return values;
        }

        private static decimal EvaluateNode(
            CashFlowFormulaNode node,
            int currentLine,
            ICashFlowFormulaContext context,
            IReadOnlyDictionary<int, decimal> values)
        {
            switch (node)
            {
                case CashFlowFormulaNumber number:
                    return number.Value;

                case CashFlowFormulaRowReference row:
                    return values.TryGetValue(row.Line, out var value) ? value : 0m;

                case CashFlowFormulaNegate negate:
                    return -EvaluateNode(negate.Operand, currentLine, context, values);

                case CashFlowFormulaBinary binary:
                    var left = EvaluateNode(binary.Left, currentLine, context, values);
                    var right = EvaluateNode(binary.Right, currentLine, context, values);
                    return binary.Operator switch
                    {
                        '+' => left + right,
                        '-' => left - right,
                        '*' => left * right,
                        '/' => right == 0m ? 0m : left / right,
                        _ => throw new InvalidOperationException($"未知运算符 {binary.Operator}。"),
                    };

                case CashFlowFormulaFunction function:
                    return function.Name switch
                    {
                        "JF" => context.Debit(function.Argument),
                        "DF" => context.Credit(function.Argument),
                        "QC" => context.Opening(function.Argument),
                        "QM" => context.Closing(function.Argument),
                        "XJS" => context.CashInflow(function.Argument),
                        "XJF" => context.CashOutflow(function.Argument),
                        "XJ" => context.CashNet(function.Argument),
                        "AUTO" => context.Auto(currentLine),
                        _ => throw new InvalidOperationException($"未知函数 {function.Name}。"),
                    };

                default:
                    throw new InvalidOperationException("未知公式节点。");
            }
        }
    }
}
