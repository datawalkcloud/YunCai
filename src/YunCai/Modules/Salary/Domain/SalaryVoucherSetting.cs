namespace YunCai.Modules.Salary.Domain
{
    /// <summary>
    /// 工资凭证科目映射设置（每账套至多一条）。
    /// 对应界面上的「工资凭证设置」：把计提 / 发放凭证要用的科目与发放账户显式定下来，
    /// 未配置时由服务层按编码前缀自动推断（2211 应付职工薪酬 / 2221012 个税 / 2241 其他应付款 / 首个银行日记账）。
    /// </summary>
    public class SalaryVoucherSetting
    {
        private SalaryVoucherSetting()
        {
        }

        private SalaryVoucherSetting(Guid id, Guid accountSetId)
        {
            Id = id;
            AccountSetId = accountSetId;
        }

        /// <summary>设置标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>应付职工薪酬科目（计提凭证贷方 / 发放凭证借方）。</summary>
        public Guid? PayrollAccountId { get; private set; }

        /// <summary>应交个人所得税科目（发放凭证贷方）。</summary>
        public Guid? TaxAccountId { get; private set; }

        /// <summary>代扣个人社保等款项科目（发放凭证贷方）。</summary>
        public Guid? InsuranceAccountId { get; private set; }

        /// <summary>发放工资的资金账户（发放凭证贷方「银行存款」）。</summary>
        public Guid? BankAccountId { get; private set; }

        /// <summary>为账套创建默认设置（全部留空，走自动推断）。</summary>
        public static SalaryVoucherSetting Create(Guid accountSetId)
            => new(Guid.NewGuid(), accountSetId);

        /// <summary>覆盖科目映射；传 null 表示该科目回落到自动推断。</summary>
        public void Update(Guid? payrollAccountId, Guid? taxAccountId, Guid? insuranceAccountId, Guid? bankAccountId)
        {
            PayrollAccountId = EmptyToNull(payrollAccountId);
            TaxAccountId = EmptyToNull(taxAccountId);
            InsuranceAccountId = EmptyToNull(insuranceAccountId);
            BankAccountId = EmptyToNull(bankAccountId);
        }

        private static Guid? EmptyToNull(Guid? value) => value is null || value == Guid.Empty ? null : value;
    }
}
