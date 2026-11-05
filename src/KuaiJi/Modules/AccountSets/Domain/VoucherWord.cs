namespace KuaiJi.Modules.AccountSets.Domain
{
    /// <summary>
    /// 凭证字：凭证的字（记 / 收 / 付 / 转）与打印标题（记账凭证等）。
    /// 每个账套必须有且仅有一个默认凭证字，新增凭证时默认取用。
    /// </summary>
    public class VoucherWord
    {
        /// <summary>凭证字最大长度（与凭证 word 列一致）。</summary>
        public const int MaxWordLength = 8;

        /// <summary>打印标题最大长度。</summary>
        public const int MaxPrintTitleLength = 50;

        private VoucherWord()
        {
        }

        private VoucherWord(Guid id, Guid accountSetId, string word, string printTitle, bool isDefault)
        {
            Id = id;
            AccountSetId = accountSetId;
            Word = word;
            PrintTitle = printTitle;
            IsDefault = isDefault;
        }

        /// <summary>凭证字标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>凭证字，同一账套内唯一（如 记）。</summary>
        public string Word { get; private set; } = null!;

        /// <summary>打印标题（如 记账凭证）。</summary>
        public string PrintTitle { get; private set; } = null!;

        /// <summary>是否默认凭证字。每个账套有且仅有一个默认。</summary>
        public bool IsDefault { get; private set; }

        /// <summary>新建凭证字，须归属于某一账套。</summary>
        public static VoucherWord Create(Guid accountSetId, string word, string printTitle, bool isDefault)
        {
            GuardWord(word);
            GuardPrintTitle(printTitle);
            return new VoucherWord(Guid.NewGuid(), accountSetId, word.Trim(), printTitle.Trim(), isDefault);
        }

        /// <summary>修改凭证字信息。</summary>
        public void Update(string word, string printTitle, bool isDefault)
        {
            GuardWord(word);
            GuardPrintTitle(printTitle);
            Word = word.Trim();
            PrintTitle = printTitle.Trim();
            IsDefault = isDefault;
        }

        /// <summary>取消默认标记（设置新的默认凭证字时由服务层调用）。</summary>
        public void MarkNotDefault() => IsDefault = false;

        private static void GuardWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                throw new ArgumentException("凭证字不能为空。");
            }

            if (word.Trim().Length > MaxWordLength)
            {
                throw new ArgumentException($"凭证字不能超过 {MaxWordLength} 个字符。", nameof(word));
            }
        }

        private static void GuardPrintTitle(string printTitle)
        {
            if (string.IsNullOrWhiteSpace(printTitle))
            {
                throw new ArgumentException("打印标题不能为空。");
            }

            if (printTitle.Trim().Length > MaxPrintTitleLength)
            {
                throw new ArgumentException($"打印标题不能超过 {MaxPrintTitleLength} 个字符。", nameof(printTitle));
            }
        }
    }
}
