namespace KuaiJi.Modules.Salary.Domain
{
    /// <summary>
    /// 员工某险种的缴纳基数与个人比例：一名员工对多个险种各一行。
    /// 比例留空表示沿用账套级 <see cref="InsuranceItem" /> 的默认比例，填写后按个人比例计算。
    /// </summary>
    public class EmployeeInsurance
    {
        private EmployeeInsurance()
        {
        }

        public EmployeeInsurance(
            Guid id,
            Guid accountSetId,
            Guid employeeId,
            Guid insuranceItemId,
            decimal insuranceBase,
            decimal? companyRate = null,
            decimal? personalRate = null)
        {
            Id = id;
            AccountSetId = accountSetId;
            EmployeeId = employeeId;
            InsuranceItemId = insuranceItemId;
            if (insuranceBase < 0m)
            {
                throw new ArgumentException("缴纳基数不能为负数。");
            }

            if (companyRate is { } cr && (cr < 0m || cr > 100m))
            {
                throw new ArgumentException("公司缴纳比例必须在 0-100 之间。");
            }

            if (personalRate is { } pr && (pr < 0m || pr > 100m))
            {
                throw new ArgumentException("个人缴纳比例必须在 0-100 之间。");
            }

            InsuranceBase = insuranceBase;
            CompanyRate = companyRate;
            PersonalRate = personalRate;
        }

        /// <summary>记录标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>员工标识。</summary>
        public Guid EmployeeId { get; private set; }

        /// <summary>险种标识。</summary>
        public Guid InsuranceItemId { get; private set; }

        /// <summary>缴纳基数。</summary>
        public decimal InsuranceBase { get; private set; }

        /// <summary>个人公司缴纳比例（百分比）；为空沿用险种默认比例。</summary>
        public decimal? CompanyRate { get; private set; }

        /// <summary>个人缴纳比例（百分比）；为空沿用险种默认比例。</summary>
        public decimal? PersonalRate { get; private set; }
    }
}
