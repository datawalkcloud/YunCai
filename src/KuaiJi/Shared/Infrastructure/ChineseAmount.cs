using System.Globalization;
using System.Text;

namespace KuaiJi.Shared.Infrastructure
{
    /// <summary>
    /// 人民币金额转中文大写，用于凭证、账簿、报表等财务单据的「人民币（大写）」栏。
    /// 规则参照中国人民银行《正确填写票据和结算凭证的基本规定》。
    /// </summary>
    public static class ChineseAmount
    {
        private static readonly string[] Digits = ["零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖"];
        private static readonly string[] Units = ["", "拾", "佰", "仟"];
        private static readonly string[] Sections = ["", "万", "亿", "万亿"];

        /// <summary>把金额转为中文大写，如 1234.50 → 壹仟贰佰叁拾肆元伍角整。</summary>
        public static string ToUppercase(decimal amount)
        {
            var negative = amount < 0;
            var value = Math.Abs(decimal.Round(amount, 2, MidpointRounding.AwayFromZero));

            var yuan = decimal.Truncate(value);
            var fen = (int)Math.Round((value - yuan) * 100m, 0, MidpointRounding.AwayFromZero);
            if (fen >= 100)
            {
                // 形如 1.999 四舍五入后进位到整元
                yuan += 1;
                fen = 0;
            }

            // 零元零分单独成文：零元整
            if (yuan == 0 && fen == 0) return "零元整";

            var sb = new StringBuilder();
            if (negative) sb.Append("负");

            // 元位为零时只写角分（壹角 / 壹分），不额外带出「零元」
            var hasYuan = yuan > 0;
            if (hasYuan) sb.Append(IntegerToChinese(yuan)).Append('元');

            if (fen == 0)
            {
                sb.Append('整');
                return sb.ToString();
            }

            var jiao = fen / 10;
            var cent = fen % 10;

            // 角位为零而分位不为零时补「零」：壹元零伍分
            if (jiao == 0 && cent > 0 && hasYuan) sb.Append('零');
            if (jiao > 0) sb.Append(Digits[jiao]).Append('角');
            if (cent > 0) sb.Append(Digits[cent]).Append('分');

            return sb.ToString();
        }

        /// <summary>整数部分转中文大写（按四位分节）。</summary>
        private static string IntegerToChinese(decimal value)
        {
            if (value <= 0) return string.Empty;

            var text = ((long)value).ToString(CultureInfo.InvariantCulture);
            var length = text.Length;
            var sb = new StringBuilder();
            var pendingZero = false;

            for (var i = 0; i < length; i++)
            {
                var digit = text[i] - '0';
                var pos = length - 1 - i;
                var unitIndex = pos % 4;
                var sectionIndex = pos / 4;

                if (digit != 0)
                {
                    if (pendingZero && sb.Length > 0) sb.Append('零');
                    pendingZero = false;
                    sb.Append(Digits[digit]).Append(Units[unitIndex]);
                }
                else
                {
                    // 只有后面还会出现非零数字时，这个零才有意义
                    pendingZero = sb.Length > 0;
                }

                // 到达某一节的个位时补节名（万 / 亿），但该节全为零时不补
                if (unitIndex == 0 && sectionIndex > 0)
                {
                    var sectionStart = Math.Max(0, i - 3);
                    var hasValue = false;
                    for (var k = sectionStart; k <= i; k++)
                    {
                        if (text[k] != '0')
                        {
                            hasValue = true;
                            break;
                        }
                    }

                    if (hasValue) sb.Append(Sections[sectionIndex]);
                    // 节与节之间若需要补零（如 100000005 的万节为空）交给上一层的 pendingZero 处理
                    if (!hasValue && sb.Length > 0) pendingZero = true;
                }
            }

            return sb.ToString();
        }
    }
}
