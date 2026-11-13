using YunCai.Modules.Accounting.Domain;

namespace YunCai.Modules.Ledgers.Application
{
    /// <summary>
    /// 科目编码条件解析器：把「1001,1122,1501-1503」这类串解析成可判定的选择器。
    /// <para>
    /// 区间端点必须等长且全为数字，且长度差不超过 <see cref="MaxRangeSpan"/>，
    /// 否则视为非法（回退成「按字面前缀匹配」）——避免 1501-1503 这类写法意外吞掉
    /// 更深层级或与区间无关的科目。逗号分隔的每一段相互独立。
    /// </para>
    /// </summary>
    public sealed class AccountCodeSelector
    {
        /// <summary>单个区间允许展开的最大科目个数，超出则视为写法过宽并拒绝。</summary>
        public const int MaxRangeSpan = 5000;

        private readonly List<string> _prefixes = [];
        private readonly List<string> _codes = [];

        private AccountCodeSelector(bool matchAll)
        {
            MatchAll = matchAll;
        }

        /// <summary>是否匹配全部科目（条件串为空时）。</summary>
        public bool MatchAll { get; }

        /// <summary>解析条件串。空串或 null 表示不裁剪。</summary>
        /// <exception cref="LedgerException">区间不合法（长度不等、含非数字、跨度超限）。</exception>
        public static AccountCodeSelector Parse(string? expression)
        {
            var selector = new AccountCodeSelector(string.IsNullOrWhiteSpace(expression));
            if (selector.MatchAll)
            {
                return selector;
            }

            foreach (var raw in expression!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var dash = raw.IndexOf('-');
                if (dash < 0)
                {
                    selector._prefixes.Add(raw);
                    continue;
                }

                var from = raw[..dash].Trim();
                var to = raw[(dash + 1)..].Trim();
                selector._codes.AddRange(ExpandRange(from, to));
            }

            return selector;
        }

        /// <summary>
        /// 仅校验条件串可否解析（区间是否等长纯数字、起止顺序、跨度上限），合法返回 null。
        /// 供查询条件模型在校验阶段复用，避免把解析异常抛到控制器。
        /// </summary>
        public static string? Validate(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            try
            {
                _ = Parse(expression);
                return null;
            }
            catch (LedgerException ex)
            {
                return ex.Message;
            }
        }

        /// <summary>判断科目是否命中条件。命中语义：等于选中编码，或以其为前缀（带出下级）。</summary>
        public bool Matches(Account account)
        {
            if (MatchAll)
            {
                return true;
            }

            var code = account.Code;
            if (_codes.Contains(code) || _codes.Any(c => code.StartsWith(c, StringComparison.Ordinal)))
            {
                return true;
            }

            return _prefixes.Any(p => code.StartsWith(p, StringComparison.Ordinal));
        }

        /// <summary>
        /// 解析两个等长数字编码之间的所有同长度编码。长度差 1 以内也允许（如 11--12 形式已不支持，
        /// 这里严格等长），跨度超过 <see cref="MaxRangeSpan"/> 时报错提示改用逗号列举。
        /// </summary>
        private static IEnumerable<string> ExpandRange(string from, string to)
        {
            if (from.Length == 0 || to.Length == 0 || from.Length != to.Length
                || !IsDigits(from) || !IsDigits(to))
            {
                throw new LedgerException($"科目编码区间「{from}-{to}」不合法：两端必须为等长的纯数字编码。");
            }

            var start = int.Parse(from);
            var end = int.Parse(to);
            if (start > end)
            {
                throw new LedgerException($"科目编码区间「{from}-{to}」起始值不能大于终止值。");
            }

            var span = end - start + 1;
            if (span > MaxRangeSpan)
            {
                throw new LedgerException(
                    $"科目编码区间「{from}-{to}」跨度超过 {MaxRangeSpan} 个编码，请改用逗号分隔或更宽的级次条件。");
            }

            for (var value = start; value <= end; value++)
            {
                yield return value.ToString($"D{from.Length}");
            }
        }

        private static bool IsDigits(string value) => value.All(char.IsAsciiDigit);
    }
}
