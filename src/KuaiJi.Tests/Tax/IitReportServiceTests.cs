using KuaiJi.Modules.Salary.Domain;
using KuaiJi.Modules.Tax.Application;
using Xunit;

namespace KuaiJi.Tests.Tax;

/// <summary>
/// 个税「报表填写」取数测试（<see cref="IitReportService"/>）。
/// 依据 AGENTS.md「领域须知」与税局累计预扣法口径：
/// 本期收入＝应发工资（工资总额 − 应扣工资）；累计列取本年 1 月至所属期；
/// 累计减除费用＝免征额 × 本年截至本月的任职受雇月份数（当年入职从入职月起算）；
/// 累计专项扣除＝「代扣个人款项」的年内累计；已缴税额取 1 月至上月；
/// 应补（退）税额＝累计应纳税额 − 已缴税额，且只申报本期有工资的员工。
/// </summary>
public sealed class IitReportServiceTests
{
    private static readonly Guid SetId = Guid.NewGuid();

    private sealed class FakeSalaryRepository : ISalaryRepository
    {
        internal readonly List<Employee> Employees = [];
        internal readonly List<SalaryBill> Bills = [];

        public Task<Employee?> GetEmployeeByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Employees.FirstOrDefault(e => e.Id == id));

        public Task<IReadOnlyList<Employee>> GetEmployeesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Employee>>(Employees);

        public Task<IReadOnlyList<SalaryBill>> GetBillsAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SalaryBill>>(Bills.Where(b => b.Period == period).ToList());

        public Task<bool> EmployeeCodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddEmployeeAsync(Employee employee, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void RemoveEmployee(Employee employee) => throw new NotSupportedException();
        public Task<IReadOnlyList<InsuranceItem>> GetInsuranceAsync(Guid accountSetId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InsuranceItem?> GetInsuranceByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> InsuranceNameExistsAsync(Guid accountSetId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddInsuranceAsync(InsuranceItem item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void RemoveInsurance(InsuranceItem item) => throw new NotSupportedException();
        public Task<IReadOnlyList<EmployeeInsurance>> GetEmployeeInsurancesAsync(Guid accountSetId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EmployeeInsurance>> GetEmployeeInsurancesForUpdateAsync(Guid accountSetId, Guid employeeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddEmployeeInsuranceAsync(EmployeeInsurance item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void RemoveEmployeeInsurance(EmployeeInsurance item) => throw new NotSupportedException();
        public Task<Employee?> GetEmployeeForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountBillsOfEmployeeAsync(Guid accountSetId, Guid employeeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SalaryBill?> GetBillByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SalaryBill?> FindBillAsync(Guid accountSetId, int period, Guid employeeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddBillAsync(SalaryBill bill, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void RemoveBill(SalaryBill bill) => throw new NotSupportedException();
        public Task<IReadOnlyList<SalaryBill>> GetBillsForUpdateAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SalaryBill?> GetBillForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SalaryVoucherSetting?> GetVoucherSettingAsync(Guid accountSetId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddVoucherSettingAsync(SalaryVoucherSetting setting, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private static Employee NewEmployee(string code, string name, DateOnly? hireDate)
        => Employee.Create(SetId, new EmployeeProfile { Code = code, Name = name, HireDate = hireDate });

    private static SalaryBill NewBill(Guid employeeId, int period, decimal payable, decimal personal, decimal tax)
        => SalaryBill.Create(SetId, period, employeeId, payable, 0m, personal, 5000m, tax, 0m, 0m);

    private static async Task<TaxIitReportRowDto> RowAsync(FakeSalaryRepository repo, int year, int month, string code)
    {
        var result = await new IitReportService(repo).BuildAsync(SetId, year, month, CancellationToken.None);
        return Assert.Single(result.Rows, row => row.Code == code);
    }

    [Fact]
    public async Task FullYearEmployee_AccumulatesAcrossMonthsAndNetsPriorTax()
    {
        var repo = new FakeSalaryRepository();
        var employee = NewEmployee("E001", "张三", new DateOnly(2025, 5, 1));
        repo.Employees.Add(employee);
        // 每月应发 30000、代扣个人 6000；1、2 月各已预扣 1500。
        for (var m = 1; m <= 3; m++)
        {
            repo.Bills.Add(NewBill(employee.Id, 202600 + m, 30000m, 6000m, m == 3 ? 0m : 1500m));
        }

        var row = await RowAsync(repo, 2026, 3, "E001");

        Assert.Equal(30000m, row.CurrentIncome);
        Assert.Equal(5000m, row.StandardDeduction);
        Assert.Equal(90000m, row.CumulativeIncome);
        Assert.Equal(15000m, row.CumulativeStandardDeduction);
        Assert.Equal(18000m, row.CumulativeSpecialDeduction);
        // 90000 − 15000 − 18000 = 57000 → 落在 10% 档，速算扣除数 2520。
        Assert.Equal(57000m, row.TaxableIncome);
        Assert.Equal(0.10m, row.Rate);
        Assert.Equal(2520m, row.QuickDeduction);
        Assert.Equal(3180m, row.CumulativeTax);
        Assert.Equal(3000m, row.PaidTax);
        Assert.Equal(180m, row.Refundable);
    }

    [Fact]
    public async Task MidYearHire_CountsDeductionMonthsFromHireMonth()
    {
        var repo = new FakeSalaryRepository();
        var employee = NewEmployee("E002", "李四", new DateOnly(2026, 2, 10));
        repo.Employees.Add(employee);
        repo.Bills.Add(NewBill(employee.Id, 202602, 20000m, 3000m, 360m));
        repo.Bills.Add(NewBill(employee.Id, 202603, 20000m, 3000m, 0m));

        var row = await RowAsync(repo, 2026, 3, "E002");

        // 2、3 月两个月任职受雇：累计减除费用 10000，而不是 15000。
        Assert.Equal(10000m, row.CumulativeStandardDeduction);
        Assert.Equal(24000m, row.TaxableIncome);
        Assert.Equal(0.03m, row.Rate);
        Assert.Equal(720m, row.CumulativeTax);
        Assert.Equal(360m, row.PaidTax);
        Assert.Equal(360m, row.Refundable);
    }

    [Fact]
    public async Task BelowThreshold_YieldsNoTax()
    {
        var repo = new FakeSalaryRepository();
        var employee = NewEmployee("E003", "王五", new DateOnly(2026, 1, 1));
        repo.Employees.Add(employee);
        repo.Bills.Add(NewBill(employee.Id, 202603, 4000m, 0m, 0m));

        var row = await RowAsync(repo, 2026, 3, "E003");

        // 4000 − 15000 < 0：应纳税所得额与税额都归零，不出现负数。
        Assert.Equal(0m, row.TaxableIncome);
        Assert.Equal(0m, row.CumulativeTax);
        Assert.Equal(0m, row.Refundable);
    }

    [Fact]
    public async Task OnlyCurrentMonthPayrollIsDeclared_AndSummaryTotalsMatch()
    {
        var repo = new FakeSalaryRepository();
        var active = NewEmployee("E010", "赵六", new DateOnly(2026, 1, 1));
        var left = NewEmployee("E011", "孙七", new DateOnly(2026, 1, 1));
        repo.Employees.AddRange([active, left]);
        repo.Bills.Add(NewBill(active.Id, 202602, 30000m, 6000m, 240m));
        repo.Bills.Add(NewBill(active.Id, 202603, 30000m, 6000m, 0m));
        // 2 月离职：3 月没有工资，不进本期申报表，但 1-2 月已缴的税仍算在内。
        repo.Bills.Add(NewBill(left.Id, 202601, 20000m, 0m, 450m));
        repo.Bills.Add(NewBill(left.Id, 202602, 20000m, 0m, 450m));

        var result = await new IitReportService(repo).BuildAsync(SetId, 2026, 3, CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("E010", row.Code);
        Assert.Equal(1, result.PersonCount);
        Assert.Equal(30000m, result.TotalIncome);
        Assert.Equal(result.TotalRefundable, row.Refundable);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task EmptyPeriod_ReportsMessageWithoutRows()
    {
        var result = await new IitReportService(new FakeSalaryRepository()).BuildAsync(
            SetId, 2026, 10, CancellationToken.None);

        Assert.Empty(result.Rows);
        Assert.Equal(0, result.PersonCount);
        Assert.Contains("还没有工资表数据", result.Message);
    }

    [Fact]
    public void WithholdingTable_CoversEveryBracketBoundary()
    {
        // 预扣率表一（居民工资薪金）：36000/144000/300000/420000/660000/960000 为各档上限。
        Assert.Equal((0.03m, 0m), KuaiJi.Modules.Tax.Domain.IitWithholdingTable.Lookup(0m));
        Assert.Equal((0.03m, 0m), KuaiJi.Modules.Tax.Domain.IitWithholdingTable.Lookup(36000m));
        Assert.Equal((0.10m, 2520m), KuaiJi.Modules.Tax.Domain.IitWithholdingTable.Lookup(36000.01m));
        Assert.Equal((0.10m, 2520m), KuaiJi.Modules.Tax.Domain.IitWithholdingTable.Lookup(144000m));
        Assert.Equal((0.20m, 16920m), KuaiJi.Modules.Tax.Domain.IitWithholdingTable.Lookup(144001m));
        Assert.Equal((0.25m, 31920m), KuaiJi.Modules.Tax.Domain.IitWithholdingTable.Lookup(300001m));
        Assert.Equal((0.30m, 52920m), KuaiJi.Modules.Tax.Domain.IitWithholdingTable.Lookup(420001m));
        Assert.Equal((0.35m, 85920m), KuaiJi.Modules.Tax.Domain.IitWithholdingTable.Lookup(660001m));
        Assert.Equal((0.45m, 181920m), KuaiJi.Modules.Tax.Domain.IitWithholdingTable.Lookup(960001m));
    }
}
