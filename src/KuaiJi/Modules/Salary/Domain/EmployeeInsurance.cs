namespace KuaiJi.Modules.Salary.Domain
{
    /// <summary>
    /// 员工某险种的缴纳基数：一名员工对多个险种各一行。
    /// 缴纳比例由账套级 <see cref="InsuranceItem" /> 统一维护，此处只登记基数，
    /// 避免同一比例在两处漂移。
    /// </summary>
    public class EmployeeInsurance
    {
        private EmployeeInsurance()
        {
        }

        public EmployeeInsurance(Guid id, Guid accountSetId, Guid employeeId, Guid insuranceItemId, decimal insuranceBase)
        {
            Id = id;
            AccountSetId = accountSetId;
            EmployeeId = employeeId;
            InsuranceItemId = insuranceItemId;
            InsuranceBase = insuranceBase < 0m
                ? throw new ArgumentException("缴纳基数不能为负数。")
                : insuranceBase;
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
    }
}
