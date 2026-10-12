using System.Globalization;
using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Assist.Application
{
    /// <summary>
    /// 辅助核算档案的编码方案。层级型类别（部门/项目/存货/现金流）按「上级编码 + 每级 2 位」逐级延伸，
    /// 顶级 2 位、二级 4 位；无层级类别（客户/供应商/职员）用 4 位流水号（0001、0002…）。
    /// 「哪些类别有层级」一律取自 <see cref="AssistTypeText.SupportsHierarchy"/>，
    /// 位数只在本类里定义，避免散落到各服务硬编码。
    /// </summary>
    public static class AssistCodingScheme
    {
        /// <summary>层级型类别每级编码位数。</summary>
        public const int HierarchyLevelLength = 2;

        /// <summary>无层级类别的流水号位数。</summary>
        public const int SequenceCodeLength = 4;

        /// <summary>该类别的档案是否按层级编码（可建下级）。</summary>
        public static bool IsHierarchical(AssistType type) => AssistTypeText.SupportsHierarchy(type);

        /// <summary>
        /// 第 level 级（从 1 开始）档案编码应有的总长度。无层级类别恒为 4 位流水号。
        /// </summary>
        public static int CodeLengthFor(AssistType type, int level)
        {
            if (!IsHierarchical(type))
            {
                return SequenceCodeLength;
            }

            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "档案级次必须从 1 开始。");
            }

            return level * HierarchyLevelLength;
        }

        /// <summary>本级新增档案时编码片段的长度（层级型 2 位，无层级型 4 位）。</summary>
        public static int SegmentLengthFor(AssistType type) =>
            IsHierarchical(type) ? HierarchyLevelLength : SequenceCodeLength;

        /// <summary>
        /// 给出下一个建议编码：层级型在上级编码后追加一段流水（不传上级则从顶级开始），
        /// 无层级型直接返回 4 位流水号。takenCodes 为该类别已有的全部档案编码，
        /// 用于取同级的最大流水号；非纯数字的自定义编码会被跳过。
        /// </summary>
        public static string NextCode(AssistType type, string? parentCode, IEnumerable<string> takenCodes)
        {
            ArgumentNullException.ThrowIfNull(takenCodes);

            var prefix = IsHierarchical(type) ? parentCode ?? string.Empty : string.Empty;
            var length = SegmentLengthFor(type);

            var maxNumber = 0;
            foreach (var code in takenCodes)
            {
                if (code.Length != prefix.Length + length
                    || !code.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var segment = code.Substring(prefix.Length, length);
                if (int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                    && number > maxNumber)
                {
                    maxNumber = number;
                }
            }

            var next = (maxNumber + 1).ToString(CultureInfo.InvariantCulture);
            return prefix + next.PadLeft(length, '0');
        }
    }
}
