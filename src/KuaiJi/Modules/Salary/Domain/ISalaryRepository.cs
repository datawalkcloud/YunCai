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

        // —— 员工缴纳基数 ——

        /// <summary>取账套内全部员工缴纳基数（列表回显用，不跟踪）。</summary>
        Task<IReadOnlyList<EmployeeInsurance>> GetEmployeeInsurancesAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>取某员工的全部缴纳基数（跟踪实例，保存时整批替换）。</summary>
        Task<IReadOnlyList<EmployeeInsurance>> GetEmployeeInsurancesForUpdateAsync(Guid accountSetId, Guid employeeId, CancellationToken cancellationToken = default);

        Task AddEmployeeInsuranceAsync(EmployeeInsurance item, CancellationToken cancellationToken = default);
        void RemoveEmployeeInsurance(EmployeeInsurance item);

        /// <summary>取可修改的员工（跟踪实例）；AsNoTracking 的读取实例改后不落库，写路径必须走这里。</summary>
        Task<Employee?> GetEmployeeForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>统计员工已生成的工资单条数（删除员工前的占用检查）。</summary>
        Task<int> CountBillsOfEmployeeAsync(Guid accountSetId, Guid employeeId, CancellationToken cancellationToken = default);

        // —— 月度工资单 ——
        Task<SalaryBill?> GetBillByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SalaryBill>> GetBillsAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default);
        Task<SalaryBill?> FindBillAsync(Guid accountSetId, int period, Guid employeeId, CancellationToken cancellationToken = default);
        Task AddBillAsync(SalaryBill bill, CancellationToken cancellationToken = default);
        void RemoveBill(SalaryBill bill);

        /// <summary>取某期间可修改的工资单（跟踪实例，用于回填凭证标识）。</summary>
        Task<IReadOnlyList<SalaryBill>> GetBillsForUpdateAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default);

        /// <summary>取单张可修改的工资单（跟踪实例）。</summary>
        Task<SalaryBill?> GetBillForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        // —— 工资凭证科目设置 ——
        Task<SalaryVoucherSetting?> GetVoucherSettingAsync(Guid accountSetId, CancellationToken cancellationToken = default);
        Task AddVoucherSettingAsync(SalaryVoucherSetting setting, CancellationToken cancellationToken = default);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
