using System.Text;
using TinyPinyin;

namespace YunCai.Modules.Accounting.Application
{
    /// <summary>
    /// 科目助记码生成：取科目名称的拼音首字母小写，与规则一致。
    /// 明细科目在自身首字母前加「上级助记码_」前缀，如「短期投资」dqtz 下的「股票」为 dqtz_gp。
    /// </summary>
    public static class AccountMnemonic
    {
        /// <summary>与 accounts.mnemonic 列宽保持一致。</summary>
        public const int MaxLength = 50;

        /// <summary>
        /// 多音字纠偏：拼音库按首读音取字母，会计科目里的高频词需要按语境改回常用读音。
        /// 银行（háng，不是 xíng）、长期（cháng，不是 zhǎng）。
        /// </summary>
        private static readonly (string Phrase, int Offset, char Initial)[] PolyphonicCorrections =
        [
            ("银行", 1, 'H'),
            ("长期", 0, 'C'),
        ];

        /// <summary>按名称生成助记码；名称中没有可用的字母或数字时返回 null。</summary>
        public static string? FromName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var trimmed = name.Trim();
            var initials = PinyinHelper.GetPinyinInitials(trimmed).ToCharArray();
            foreach (var (phrase, offset, initial) in PolyphonicCorrections)
            {
                for (var i = trimmed.IndexOf(phrase, StringComparison.Ordinal); i >= 0;
                     i = trimmed.IndexOf(phrase, i + 1, StringComparison.Ordinal))
                {
                    if (i + offset < initials.Length)
                        initials[i + offset] = initial;
                }
            }

            var builder = new StringBuilder(initials.Length);
            foreach (var ch in initials)
            {
                if (char.IsAsciiLetterOrDigit(ch))
                    builder.Append(char.ToLowerInvariant(ch));
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        /// <summary>
        /// 解析最终助记码：显式填写优先；否则按名称生成，并在上级已有助记码时加前缀。
        /// 子科目名称若重复了上级名称（如「营业外收入-政府补助」），生成前缀时先剥掉重复部分。
        /// </summary>
        public static string? Resolve(string? explicitMnemonic, string? name, string? parentMnemonic, string? parentName = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitMnemonic))
                return Truncate(explicitMnemonic.Trim());

            if (string.IsNullOrWhiteSpace(name))
                return null;

            var mnemonic = FromName(name);
            if (mnemonic is null)
                return null;

            if (!string.IsNullOrWhiteSpace(parentMnemonic))
            {
                var own = FromName(StripParentName(name, parentName)) ?? mnemonic;
                mnemonic = parentMnemonic.Trim() + "_" + own;
            }

            return Truncate(mnemonic);
        }

        private static string StripParentName(string name, string? parentName)
        {
            if (string.IsNullOrWhiteSpace(parentName))
                return name;

            var trimmedName = name.Trim();
            var trimmedParent = parentName.Trim();
            if (trimmedName.Length <= trimmedParent.Length
                || !trimmedName.StartsWith(trimmedParent, StringComparison.Ordinal))
                return trimmedName;

            var remainder = trimmedName[trimmedParent.Length..].TrimStart(' ', '-', '—', '－', '/', '／', '_');
            return remainder.Length == 0 ? trimmedName : remainder;
        }

        private static string Truncate(string mnemonic)
        {
            if (mnemonic.Length <= MaxLength)
                return mnemonic;

            return mnemonic[..MaxLength].TrimEnd('_');
        }
    }
}
