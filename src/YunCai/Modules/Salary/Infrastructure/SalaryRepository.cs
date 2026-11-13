using YunCai.Modules.Salary.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Salary.Infrastructure
{
    /// <summary>工资模块仓储实现（员工 / 五险一金 / 月度工资单）。</summary>
    public class SalaryRepository : ISalaryRepository
    {
        private readonly AppDbContext _context;

        public SalaryRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Employee?> GetEmployeeByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.AccountSetId == accountSetId && e.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Employee>> GetEmployeesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.Employees.AsNoTracking()
                .Where(e => e.AccountSetId == accountSetId)
                .OrderBy(e => e.Code)
                .ToListAsync(cancellationToken);
            return list;
        }

        public Task<bool> EmployeeCodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.Employees
                .AnyAsync(e => e.AccountSetId == accountSetId && e.Code == code && (excludeId == null || e.Id != excludeId), cancellationToken);
        }

        public async Task AddEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
        {
            await _context.Employees.AddAsync(employee, cancellationToken);
        }

        /// <summary>取可修改的员工（跟踪实例）。</summary>
        public Task<Employee?> GetEmployeeForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Employees
                .FirstOrDefaultAsync(e => e.AccountSetId == accountSetId && e.Id == id, cancellationToken);
        }

        public Task<int> CountBillsOfEmployeeAsync(Guid accountSetId, Guid employeeId, CancellationToken cancellationToken = default)
        {
            return _context.SalaryBills
                .CountAsync(b => b.AccountSetId == accountSetId && b.EmployeeId == employeeId, cancellationToken);
        }

        public void RemoveEmployee(Employee employee)
        {
            _context.Employees.Remove(employee);
        }

        public async Task<IReadOnlyList<InsuranceItem>> GetInsuranceAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.InsuranceItems.AsNoTracking()
                .Where(i => i.AccountSetId == accountSetId)
                .OrderBy(i => i.SortOrder)
                .ToListAsync(cancellationToken);
            return list;
        }

        public Task<InsuranceItem?> GetInsuranceByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.InsuranceItems
                .FirstOrDefaultAsync(i => i.AccountSetId == accountSetId && i.Id == id, cancellationToken);
        }

        public Task<bool> InsuranceNameExistsAsync(Guid accountSetId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.InsuranceItems
                .AnyAsync(i => i.AccountSetId == accountSetId && i.Name == name && (excludeId == null || i.Id != excludeId), cancellationToken);
        }

        public async Task AddInsuranceAsync(InsuranceItem item, CancellationToken cancellationToken = default)
        {
            await _context.InsuranceItems.AddAsync(item, cancellationToken);
        }

        public void RemoveInsurance(InsuranceItem item)
        {
            _context.InsuranceItems.Remove(item);
        }

        public async Task<IReadOnlyList<EmployeeInsurance>> GetEmployeeInsurancesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.EmployeeInsurances.AsNoTracking()
                .Where(i => i.AccountSetId == accountSetId)
                .ToListAsync(cancellationToken);
            return list;
        }

        public async Task<IReadOnlyList<EmployeeInsurance>> GetEmployeeInsurancesForUpdateAsync(Guid accountSetId, Guid employeeId, CancellationToken cancellationToken = default)
        {
            var list = await _context.EmployeeInsurances
                .Where(i => i.AccountSetId == accountSetId && i.EmployeeId == employeeId)
                .ToListAsync(cancellationToken);
            return list;
        }

        public async Task AddEmployeeInsuranceAsync(EmployeeInsurance item, CancellationToken cancellationToken = default)
        {
            await _context.EmployeeInsurances.AddAsync(item, cancellationToken);
        }

        public void RemoveEmployeeInsurance(EmployeeInsurance item)
        {
            _context.EmployeeInsurances.Remove(item);
        }

        public Task<SalaryBill?> GetBillByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.SalaryBills.AsNoTracking()
                .FirstOrDefaultAsync(b => b.AccountSetId == accountSetId && b.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<SalaryBill>> GetBillsAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            var list = await _context.SalaryBills.AsNoTracking()
                .Where(b => b.AccountSetId == accountSetId && b.Period == period)
                .ToListAsync(cancellationToken);
            return list;
        }

        public Task<SalaryBill?> FindBillAsync(Guid accountSetId, int period, Guid employeeId, CancellationToken cancellationToken = default)
        {
            return _context.SalaryBills.AsNoTracking()
                .FirstOrDefaultAsync(b => b.AccountSetId == accountSetId && b.Period == period && b.EmployeeId == employeeId, cancellationToken);
        }

        public async Task AddBillAsync(SalaryBill bill, CancellationToken cancellationToken = default)
        {
            await _context.SalaryBills.AddAsync(bill, cancellationToken);
        }

        public void RemoveBill(SalaryBill bill)
        {
            _context.SalaryBills.Remove(bill);
        }

        /// <summary>取某期间可修改的工资单（跟踪实例）。</summary>
        public async Task<IReadOnlyList<SalaryBill>> GetBillsForUpdateAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            var list = await _context.SalaryBills
                .Where(b => b.AccountSetId == accountSetId && b.Period == period)
                .ToListAsync(cancellationToken);
            return list;
        }

        /// <summary>取单张可修改的工资单（跟踪实例）。</summary>
        public Task<SalaryBill?> GetBillForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.SalaryBills
                .FirstOrDefaultAsync(b => b.AccountSetId == accountSetId && b.Id == id, cancellationToken);
        }

        public Task<SalaryVoucherSetting?> GetVoucherSettingAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return _context.SalaryVoucherSettings
                .FirstOrDefaultAsync(s => s.AccountSetId == accountSetId, cancellationToken);
        }

        public async Task AddVoucherSettingAsync(SalaryVoucherSetting setting, CancellationToken cancellationToken = default)
        {
            await _context.SalaryVoucherSettings.AddAsync(setting, cancellationToken);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
