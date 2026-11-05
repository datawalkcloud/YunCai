namespace KuaiJi.Modules.Salary.Domain
{
    /// <summary>
    /// 月度工资单聚合根：某账套某期间某员工的一行工资数据。
    /// 金额口径：应发 = 工资总额 − 应扣工资；实发 = 应发 − 代扣个人款项 − 本月应缴个税 + 实发工资调整；
    /// 员工成本 = 应发 + 公司承担款项。个税免征额默认 5000。
    /// </summary>
    public class SalaryBill
    {
        private SalaryBill()
        {
        }

        private SalaryBill(
            Guid id,
            Guid accountSetId,
            int period,
            Guid employeeId,
            decimal gross,
            decimal deductible,
            decimal personalDeduction,
            decimal taxThreshold,
            decimal tax,
            decimal adjust,
            decimal companyPart)
        {
            Id = id;
            AccountSetId = accountSetId;
            Period = period;
            EmployeeId = employeeId;
            Gross = gross;
            Deductible = deductible;
            PersonalDeduction = personalDeduction;
            TaxThreshold = taxThreshold;
            Tax = tax;
            Adjust = adjust;
            CompanyPart = companyPart;
        }

        /// <summary>工资单标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>工资期间（yyyyMM）。</summary>
        public int Period { get; private set; }

        /// <summary>员工标识。</summary>
        public Guid EmployeeId { get; private set; }

        /// <summary>工资总额。</summary>
        public decimal Gross { get; private set; }

        /// <summary>应扣工资。</summary>
        public decimal Deductible { get; private set; }

        /// <summary>代扣个人款项（个人承担社保等）。</summary>
        public decimal PersonalDeduction { get; private set; }

        /// <summary>个税免征额。</summary>
        public decimal TaxThreshold { get; private set; }

        /// <summary>本月应缴个税。</summary>
        public decimal Tax { get; private set; }

        /// <summary>实发工资调整。</summary>
        public decimal Adjust { get; private set; }

        /// <summary>公司承担款项。</summary>
        public decimal CompanyPart { get; private set; }

        /// <summary>应发工资 = 工资总额 − 应扣工资。</summary>
        public decimal Payable => Gross - Deductible;

        /// <summary>实发工资 = 应发 − 代扣个人款项 − 个税 + 实发调整。</summary>
        public decimal Net => Payable - PersonalDeduction - Tax + Adjust;

        /// <summary>员工成本 = 应发 + 公司承担款项。</summary>
        public decimal Cost => Payable + CompanyPart;

        /// <summary>计提工资凭证标识（未计提为空）。</summary>
        public Guid? AccruedVoucherId { get; private set; }

        /// <summary>发放工资凭证标识（未发放为空）。</summary>
        public Guid? PaidVoucherId { get; private set; }

        /// <summary>回填计提工资凭证标识。</summary>
        public void MarkAccrued(Guid voucherId)
        {
            if (AccruedVoucherId is not null)
            {
                throw new InvalidOperationException("本期间该员工工资已计提过凭证。");
            }

            AccruedVoucherId = voucherId;
        }

        /// <summary>回填发放工资凭证标识。</summary>
        public void MarkPaid(Guid voucherId)
        {
            if (PaidVoucherId is not null)
            {
                throw new InvalidOperationException("本期间该员工工资已发放过凭证。");
            }

            PaidVoucherId = voucherId;
        }

        /// <summary>
        /// 清理悬空的凭证引用：凭证被删除后，工资单仍留着它的 Id，
        /// 界面会显示不存在的字号，且因「已计提 / 已发放」被永久跳过，再也没法重新生成。
        /// </summary>
        internal bool ClearStaleVouchers(IReadOnlySet<Guid> existingVoucherIds)
        {
            var changed = false;
            if (AccruedVoucherId is { } accrued && !existingVoucherIds.Contains(accrued))
            {
                AccruedVoucherId = null;
                changed = true;
            }

            if (PaidVoucherId is { } paid && !existingVoucherIds.Contains(paid))
            {
                PaidVoucherId = null;
                changed = true;
            }

            return changed;
        }

        /// <summary>新建工资单（同一账套同一期间同一员工唯一，由服务层校验）。</summary>
        public static SalaryBill Create(
            Guid accountSetId,
            int period,
            Guid employeeId,
            decimal gross,
            decimal deductible,
            decimal personalDeduction,
            decimal taxThreshold,
            decimal tax,
            decimal adjust,
            decimal companyPart)
        {
            GuardAmounts(gross, deductible, personalDeduction, taxThreshold, tax, companyPart);
            if (period is < 100000 or > 999999)
            {
                throw new ArgumentException("工资期间无效。");
            }

            if (employeeId == Guid.Empty)
            {
                throw new ArgumentException("必须指定员工。");
            }

            return new SalaryBill(
                Guid.NewGuid(),
                accountSetId,
                period,
                employeeId,
                gross,
                deductible,
                personalDeduction,
                taxThreshold,
                tax,
                adjust,
                companyPart);
        }

        /// <summary>修改工资单金额要素。</summary>
        public void Update(
            decimal gross,
            decimal deductible,
            decimal personalDeduction,
            decimal taxThreshold,
            decimal tax,
            decimal adjust,
            decimal companyPart)
        {
            GuardAmounts(gross, deductible, personalDeduction, taxThreshold, tax, companyPart);
            Gross = gross;
            Deductible = deductible;
            PersonalDeduction = personalDeduction;
            TaxThreshold = taxThreshold;
            Tax = tax;
            Adjust = adjust;
            CompanyPart = companyPart;
        }

        private static void GuardAmounts(
            decimal gross,
            decimal deductible,
            decimal personalDeduction,
            decimal taxThreshold,
            decimal tax,
            decimal companyPart)
        {
            if (gross < 0m || deductible < 0m || personalDeduction < 0m || tax < 0m || companyPart < 0m)
            {
                throw new ArgumentException("工资各项金额不能为负数。");
            }

            if (taxThreshold < 0m)
            {
                throw new ArgumentException("个税免征额不能为负数。");
            }
        }
    }
}
