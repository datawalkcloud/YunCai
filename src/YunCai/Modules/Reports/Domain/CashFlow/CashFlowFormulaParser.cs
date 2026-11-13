using System.Globalization;
using YunCai.Modules.Reports.Domain;

namespace YunCai.Modules.Reports.Domain.CashFlow
{
    /// <summary>取数公式语法树节点。</summary>
    public abstract record CashFlowFormulaNode;

    public sealed record CashFlowFormulaNumber(decimal Value) : CashFlowFormulaNode;

    /// <summary>引用本表更小行次的计算结果（R1 → 第 1 行）。</summary>
    public sealed record CashFlowFormulaRowReference(int Line) : CashFlowFormulaNode;

    /// <summary>取数函数。Argument 为去掉引号后的参数；AUTO 无参数时为空串。</summary>
    public sealed record CashFlowFormulaFunction(string Name, string Argument) : CashFlowFormulaNode;

    public sealed record CashFlowFormulaBinary(char Operator, CashFlowFormulaNode Left, CashFlowFormulaNode Right) : CashFlowFormulaNode;

    public sealed record CashFlowFormulaNegate(CashFlowFormulaNode Operand) : CashFlowFormulaNode;

    /// <summary>
    /// 现金流量表行取数公式解析器。支持四则运算、括号、行次引用（R1..R35）与下列取数函数：
    /// <list type="bullet">
    /// <item>JF("科目编码") / DF("科目编码")：区间内借方/贷方发生额（编码前缀匹配，含下级科目）。</item>
    /// <item>QC("科目编码") / QM("科目编码")：区间起点前/截止日的借-贷净额（含科目启用期初）。</item>
    /// <item>XJS("现金流编码") / XJF("现金流编码") / XJ("现金流编码")：现金流辅助项目的流入/流出/净额。</item>
    /// <item>AUTO()：按系统凭证归类口径取本行金额（与直接法主表同源）。</item>
    /// </list>
    /// </summary>
    public static class CashFlowFormulaParser
    {
        /// <summary>取数函数名（大写）。</summary>
        public static readonly IReadOnlySet<string> FunctionNames = new HashSet<string>
        {
            "JF", "DF", "QC", "QM", "XJS", "XJF", "XJ", "AUTO",
        };

        /// <summary>解析公式；语法错误抛出 <see cref="ReportException"/>。</summary>
        public static CashFlowFormulaNode Parse(string expression)
        {
            var text = (expression ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                throw new ReportException("取数公式不能为空。");
            }

            var parser = new Parser(text);
            var node = parser.ParseExpression();
            parser.EnsureEnd();
            return node;
        }

        /// <summary>收集公式引用的全部行次。</summary>
        public static IReadOnlyList<int> RowReferences(CashFlowFormulaNode node)
        {
            var lines = new List<int>();
            Walk(node, lines.Add);
            return lines;
        }

        /// <summary>
        /// 校验行次引用只指向更小的行（计算按行次升序进行，因此不会出现循环依赖）。
        /// </summary>
        public static void EnsureRowOrder(CashFlowFormulaNode node, int line)
        {
            foreach (var referenced in RowReferences(node))
            {
                if (referenced >= line)
                {
                    throw new ReportException($"第 {line} 行公式只能引用行次更小的行（当前引用了 R{referenced}）。");
                }
            }
        }

        private static void Walk(CashFlowFormulaNode node, Action<int> onRow)
        {
            switch (node)
            {
                case CashFlowFormulaRowReference row:
                    onRow(row.Line);
                    break;
                case CashFlowFormulaNegate negate:
                    Walk(negate.Operand, onRow);
                    break;
                case CashFlowFormulaBinary binary:
                    Walk(binary.Left, onRow);
                    Walk(binary.Right, onRow);
                    break;
            }
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _pos;

            public Parser(string text)
            {
                _text = text;
            }

            public CashFlowFormulaNode ParseExpression()
            {
                var left = ParseTerm();
                while (true)
                {
                    SkipWhitespace();
                    if (!TryConsume('+') && !TryConsume('-'))
                    {
                        return left;
                    }

                    var op = _text[_pos - 1];
                    var right = ParseTerm();
                    left = new CashFlowFormulaBinary(op, left, right);
                }
            }

            public void EnsureEnd()
            {
                SkipWhitespace();
                if (_pos < _text.Length)
                {
                    throw Error($"公式在「{_text[_pos..]}」处有多余内容。");
                }
            }

            private CashFlowFormulaNode ParseTerm()
            {
                var left = ParseUnary();
                while (true)
                {
                    SkipWhitespace();
                    if (!TryConsume('*') && !TryConsume('/'))
                    {
                        return left;
                    }

                    var op = _text[_pos - 1];
                    var right = ParseUnary();
                    left = new CashFlowFormulaBinary(op, left, right);
                }
            }

            private CashFlowFormulaNode ParseUnary()
            {
                SkipWhitespace();
                if (TryConsume('-'))
                {
                    return new CashFlowFormulaNegate(ParseUnary());
                }

                if (TryConsume('+'))
                {
                    return ParseUnary();
                }

                return ParsePrimary();
            }

            private CashFlowFormulaNode ParsePrimary()
            {
                SkipWhitespace();
                if (_pos >= _text.Length)
                {
                    throw Error("公式不完整。");
                }

                if (TryConsume('('))
                {
                    var inner = ParseExpression();
                    if (!TryConsume(')'))
                    {
                        throw Error("缺少右括号。");
                    }

                    return inner;
                }

                var ch = _text[_pos];
                if (char.IsDigit(ch) || ch == '.')
                {
                    return ParseNumber();
                }

                if (char.IsLetter(ch) || ch == '_')
                {
                    return ParseIdentifier();
                }

                throw Error($"无法识别字符「{ch}」。");
            }

            private CashFlowFormulaNode ParseNumber()
            {
                var start = _pos;
                while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
                {
                    _pos++;
                }

                var text = _text[start.._pos];
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                {
                    throw Error($"数字「{text}」不合法。");
                }

                return new CashFlowFormulaNumber(value);
            }

            private CashFlowFormulaNode ParseIdentifier()
            {
                var start = _pos;
                while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
                {
                    _pos++;
                }

                var name = _text[start.._pos];

                // R1 / r12：引用本表行次。
                if ((name[0] is 'R' or 'r') && name.Length > 1 && name[1..].All(char.IsDigit))
                {
                    if (!int.TryParse(name[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var line)
                        || !CashFlowStandardRows.ByLine.ContainsKey(line))
                    {
                        throw Error($"行次引用「{name}」不合法（应为 R1-R35）。");
                    }

                    return new CashFlowFormulaRowReference(line);
                }

                var upper = name.ToUpperInvariant();
                if (!FunctionNames.Contains(upper))
                {
                    throw Error($"不支持的函数「{name}」。");
                }

                SkipWhitespace();
                if (!TryConsume('('))
                {
                    throw Error($"函数「{name}」缺少左括号。");
                }

                SkipWhitespace();
                if (TryConsume(')'))
                {
                    if (upper != "AUTO")
                    {
                        throw Error($"函数「{upper}」需要 1 个字符串参数。");
                    }

                    return new CashFlowFormulaFunction(upper, string.Empty);
                }

                var argument = ParseStringArgument();
                SkipWhitespace();
                if (!TryConsume(')'))
                {
                    throw Error($"函数「{upper}」只接受 1 个参数。");
                }

                if (upper == "AUTO")
                {
                    throw Error("函数「AUTO」不接受参数，请写成 AUTO()。");
                }

                return new CashFlowFormulaFunction(upper, argument);
            }

            private string ParseStringArgument()
            {
                SkipWhitespace();
                if (_pos >= _text.Length || (_text[_pos] != '"' && _text[_pos] != '\''))
                {
                    throw Error("函数参数必须是带引号的编码，例如 JF(\"1001\")。");
                }

                var quote = _text[_pos++];
                var start = _pos;
                while (_pos < _text.Length && _text[_pos] != quote)
                {
                    _pos++;
                }

                if (_pos >= _text.Length)
                {
                    throw Error("字符串参数缺少右引号。");
                }

                var value = _text[start.._pos].Trim();
                _pos++;
                if (value.Length == 0)
                {
                    throw Error("函数参数不能为空字符串。");
                }

                return value;
            }

            private void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
                {
                    _pos++;
                }
            }

            private bool TryConsume(char expected)
            {
                SkipWhitespace();
                if (_pos < _text.Length && _text[_pos] == expected)
                {
                    _pos++;
                    return true;
                }

                return false;
            }

            private ReportException Error(string message)
            {
                return new ReportException($"取数公式第 {_pos + 1} 个字符处错误：{message}");
            }
        }
    }
}
