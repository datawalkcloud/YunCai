namespace KuaiJi.Modules.AccountSets.Domain
{
    /// <summary>
    /// 币别：账套可用的货币字典（如人民币）。每个账套必须有且仅有一个本位币，
    /// 折算方式固定为「原币 * 汇率 = 本位币」。
    /// </summary>
    public class Currency
    {
        /// <summary>币别代码最大长度（如 RMB、USD）。</summary>
        public const int MaxCodeLength = 10;

        /// <summary>币别名称最大长度。</summary>
        public const int MaxNameLength = 50;

        private Currency()
        {
        }

        private Currency(Guid id, Guid accountSetId, string code, string name, decimal rate, bool isBase)
        {
            Id = id;
            AccountSetId = accountSetId;
            Code = code;
            Name = name;
            Rate = rate;
            IsBase = isBase;
        }

        /// <summary>币别标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>币别代码，同一账套内唯一（如 RMB）。</summary>
        public string Code { get; private set; } = null!;

        /// <summary>币别名称（如 人民币）。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>记账汇率（原币 * 汇率 = 本位币）。</summary>
        public decimal Rate { get; private set; }

        /// <summary>是否本位币。每个账套有且仅有一个本位币。</summary>
        public bool IsBase { get; private set; }

        /// <summary>新建币别，须归属于某一账套。</summary>
        public static Currency Create(Guid accountSetId, string code, string name, decimal rate, bool isBase)
        {
            GuardCode(code);
            GuardName(name);
            if (rate <= 0m)
            {
                throw new ArgumentException("汇率必须大于零。", nameof(rate));
            }

            return new Currency(Guid.NewGuid(), accountSetId, code.Trim().ToUpperInvariant(), name.Trim(), rate, isBase);
        }

        /// <summary>修改币别信息。</summary>
        public void Update(string code, string name, decimal rate, bool isBase)
        {
            GuardCode(code);
            GuardName(name);
            if (rate <= 0m)
            {
                throw new ArgumentException("汇率必须大于零。", nameof(rate));
            }

            Code = code.Trim().ToUpperInvariant();
            Name = name.Trim();
            Rate = rate;
            IsBase = isBase;
        }

        private static void GuardCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("币别代码不能为空。", nameof(code));
            }

            if (code.Trim().Length > MaxCodeLength)
            {
                throw new ArgumentException($"币别代码不能超过 {MaxCodeLength} 个字符。", nameof(code));
            }
        }

        private static void GuardName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("币别名称不能为空。", nameof(name));
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"币别名称不能超过 {MaxNameLength} 个字符。", nameof(name));
            }
        }
    }
}
