namespace KuaiJi.Modules.Salary.Domain
{
    /// <summary>工资模块仓储：员工 / 五险一金配置 / 月度工资单统一出入。</summary>
    public interface ISalaryRepository
    {
        // —— 员工 ——
        Task<Employee?> GetEmployeeByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Employee>> GetEmployeesAsync(Guid accountSetId, CancellationToken cancellationToken = default);
        Task<bool> EmployeeCodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
        Task AddEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);
        void RemoveEmployee(Employee employee);

        // —— 五险一金配置 ——
        Task<IReadOnlyList<InsuranceItem>> GetInsuranceAsync(Guid accountSetId, CancellationToken cancellationToken = default);
        Task<InsuranceItem?> GetInsuranceByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);
        Task<bool> InsuranceNameExistsAsync(Guid accountSetId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
        Task AddInsuranceAsync(InsuranceItem item, CancellationToken cancellationToken = default);
        void RemoveInsurance(InsuranceItem item);

        // —— 月度工资单 ——
        Task<SalaryBill?> GetBillByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SalaryBill>> GetBillsAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default);
        Task<SalaryBill?> FindBillAsync(Guid accountSetId, int period, Guid employeeId, CancellationToken cancellationToken = default);
        Task AddBillAsync(SalaryBill bill, CancellationToken cancellationToken = default);
        void RemoveBill(SalaryBill bill);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
