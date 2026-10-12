using KuaiJi.Modules.Vouchers.Application;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Funds.Domain;
using KuaiJi.Modules.Salary.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Salary.Application
{
    /// <summary>工资相关业务错误。</summary>
    public sealed class SalaryException : Exception
    {
        public SalaryException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 工资应用服务：员工档案 / 五险一金配置 / 月度工资单 / 统计表 / 计提与发放生成凭证。
    /// 凭证口径（当前简化版，待科目映射配置化）：
    /// 计提：借 员工计提工资科目（员工成本）/ 贷 应付职工薪酬（员工成本）；
    /// 发放：借 应付职工薪酬（实发）/ 贷 银行存款（实发）。
    /// </summary>
    public sealed class SalaryService
    {
        /// <summary>应付职工薪酬科目编码前缀。</summary>
        private const string PayrollSubjectPrefix = "2211";

        private readonly IAccountSetRepository _accountSetRepository;
        private readonly ISalaryRepository _salaryRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IJournalRepository _journalRepository;
        private readonly VoucherService _voucherService;

        public SalaryService(
            IAccountSetRepository accountSetRepository,
            ISalaryRepository salaryRepository,
            IAccountRepository accountRepository,
            IJournalRepository journalRepository,
            VoucherService voucherService)
        {
            _accountSetRepository = accountSetRepository;
            _salaryRepository = salaryRepository;
            _accountRepository = accountRepository;
            _journalRepository = journalRepository;
            _voucherService = voucherService;
        }

        // —— 员工 ——

        /// <summary>获取全部员工（按编号排列，附计提工资科目解析）。</summary>
        public async Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var employees = await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            return employees
                .OrderBy(e => e.Code)
                .Select(e =>
                {
                    var subject = e.SubjectAccountId is { } sid ? accountsById.GetValueOrDefault(sid) : null;
                    return new EmployeeDto
                    {
                        Id = e.Id,
                        Code = e.Code,
                        Name = e.Name,
                        GenderText = EmployeeDto.GenderTextOf(e.Gender),
                        Dept = e.Dept ?? string.Empty,
                        Phone = e.Phone,
                        Email = e.Email,
                        IdTypeText = EmployeeDto.IdTypeTextOf(e.IdType),
                        IdNo = e.IdNo,
                        SubjectAccountId = e.SubjectAccountId,
                        SubjectText = subject is null ? string.Empty : $"{subject.Code} {subject.Name}",
                        StatusText = EmployeeDto.StatusTextOf(e.Status),
                        HireDate = e.HireDate,
                        Remark = e.Remark,
                    };
                })
                .ToList();
        }

        /// <summary>新建员工（编号唯一）。</summary>
        public async Task<EmployeeDto> CreateEmployeeAsync(Guid accountSetId, SaveEmployeeRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var code = request.Code?.Trim() ?? string.Empty;
            if (await _salaryRepository.EmployeeCodeExistsAsync(accountSetId, code, cancellationToken: cancellationToken))
            {
                throw new SalaryException($"员工编号「{code}」已存在。");
            }

            var employee = BuildEmployee(accountSetId, request);
            await _salaryRepository.AddEmployeeAsync(employee, cancellationToken);
            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return (await GetEmployeesAsync(accountSetId, cancellationToken)).First(e => e.Id == employee.Id);
        }

        /// <summary>修改员工档案；不存在时返回 null。</summary>
        public async Task<EmployeeDto?> UpdateEmployeeAsync(Guid accountSetId, Guid id, SaveEmployeeRequest request, CancellationToken cancellationToken = default)
        {
            var employee = await _salaryRepository.GetEmployeeByIdAsync(accountSetId, id, cancellationToken);
            if (employee is null)
            {
                return null;
            }

            var code = request.Code?.Trim() ?? string.Empty;
            if (await _salaryRepository.EmployeeCodeExistsAsync(accountSetId, code, employee.Id, cancellationToken))
            {
                throw new SalaryException($"员工编号「{code}」已存在。");
            }

            try
            {
                employee.Update(
                    code,
                    request.Name,
                    (EmployeeGender)request.Gender,
                    (EmployeeIdType)request.IdType,
                    request.IdNo,
                    request.Dept,
                    request.Phone,
                    request.Email,
                    request.BirthDate,
                    request.HireDate,
                    request.BankName,
                    request.BankAccountNo,
                    request.Post,
                    request.Title,
                    request.SubjectAccountId,
                    (EmployeeStatus)request.Status,
                    request.Remark);
            }
            catch (ArgumentException ex)
            {
                throw new SalaryException(ex.Message);
            }

            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return (await GetEmployeesAsync(accountSetId, cancellationToken)).First(e => e.Id == employee.Id);
        }

        /// <summary>删除员工；不存在时返回 false。</summary>
        public async Task<bool> DeleteEmployeeAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var employee = await _salaryRepository.GetEmployeeByIdAsync(accountSetId, id, cancellationToken);
            if (employee is null)
            {
                return false;
            }

            _salaryRepository.RemoveEmployee(employee);
            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        // —— 五险一金 ——

        /// <summary>获取五险一金配置；为空时补种柠檬云口径的六个默认险种。</summary>
        public async Task<IReadOnlyList<InsuranceItemDto>> GetInsuranceAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var items = await _salaryRepository.GetInsuranceAsync(accountSetId, cancellationToken);
            if (items.Count == 0)
            {
                var seeds = new List<InsuranceItem>
                {
                    new(Guid.NewGuid(), accountSetId, "养老保险", 20m, 8m, 1),
                    new(Guid.NewGuid(), accountSetId, "医疗保险", 10m, 2m, 2),
                    new(Guid.NewGuid(), accountSetId, "失业保险", 1m, 0.2m, 3),
                    new(Guid.NewGuid(), accountSetId, "工伤保险", 0.5m, null, 4),
                    new(Guid.NewGuid(), accountSetId, "生育保险", 0.8m, null, 5),
                    new(Guid.NewGuid(), accountSetId, "住房公积金", 12m, 12m, 6),
                };
                foreach (var seed in seeds)
                {
                    await _salaryRepository.AddInsuranceAsync(seed, cancellationToken);
                }

                await _salaryRepository.SaveChangesAsync(cancellationToken);
                items = seeds;
            }

            return items
                .OrderBy(i => i.SortOrder)
                .Select(InsuranceItemDto.FromDomain)
                .ToList();
        }

        /// <summary>新建五险一金项（险种名唯一）。</summary>
        public async Task<InsuranceItemDto> CreateInsuranceAsync(Guid accountSetId, SaveInsuranceRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var name = request.Name?.Trim() ?? string.Empty;
            if (await _salaryRepository.InsuranceNameExistsAsync(accountSetId, name, cancellationToken: cancellationToken))
            {
                throw new SalaryException($"险种「{name}」已存在。");
            }

            var existing = await _salaryRepository.GetInsuranceAsync(accountSetId, cancellationToken);
            var item = new InsuranceItem(Guid.NewGuid(), accountSetId, name, request.CompanyRate, request.PersonalRate, existing.Count + 1);
            await _salaryRepository.AddInsuranceAsync(item, cancellationToken);
            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return InsuranceItemDto.FromDomain(item);
        }

        /// <summary>修改五险一金项；不存在时返回 null。</summary>
        public async Task<InsuranceItemDto?> UpdateInsuranceAsync(Guid accountSetId, Guid id, SaveInsuranceRequest request, CancellationToken cancellationToken = default)
        {
            var item = await _salaryRepository.GetInsuranceByIdAsync(accountSetId, id, cancellationToken);
            if (item is null)
            {
                return null;
            }

            var name = request.Name?.Trim() ?? string.Empty;
            if (await _salaryRepository.InsuranceNameExistsAsync(accountSetId, name, item.Id, cancellationToken))
            {
                throw new SalaryException($"险种「{name}」已存在。");
            }

            try
            {
                item.Update(name, request.CompanyRate, request.PersonalRate);
            }
            catch (ArgumentException ex)
            {
                throw new SalaryException(ex.Message);
            }

            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return InsuranceItemDto.FromDomain(item);
        }

        /// <summary>删除五险一金项；不存在时返回 false。</summary>
        public async Task<bool> DeleteInsuranceAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var item = await _salaryRepository.GetInsuranceByIdAsync(accountSetId, id, cancellationToken);
            if (item is null)
            {
                return false;
            }

            _salaryRepository.RemoveInsurance(item);
            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        // —— 月度工资单 ——

        /// <summary>获取某期间工资单（含员工信息与凭证回填文本）。</summary>
        public async Task<IReadOnlyList<SalaryBillDto>> GetBillsAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var bills = await _salaryRepository.GetBillsAsync(accountSetId, period, cancellationToken);
            var employees = (await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken))
                .ToDictionary(e => e.Id, e => e);
            return bills
                .OrderBy(b => employees.GetValueOrDefault(b.EmployeeId)?.Code ?? string.Empty)
                .Select(b =>
                {
                    var employee = employees.GetValueOrDefault(b.EmployeeId);
                    return new SalaryBillDto
                    {
                        Id = b.Id,
                        EmployeeId = b.EmployeeId,
                        EmployeeCode = employee?.Code ?? string.Empty,
                        EmployeeName = employee?.Name ?? string.Empty,
                        Dept = employee?.Dept ?? string.Empty,
                        Gross = b.Gross,
                        Deductible = b.Deductible,
                        Payable = b.Payable,
                        PersonalDeduction = b.PersonalDeduction,
                        TaxThreshold = b.TaxThreshold,
                        Tax = b.Tax,
                        Adjust = b.Adjust,
                        Net = b.Net,
                        CompanyPart = b.CompanyPart,
                        Cost = b.Cost,
                        AccruedVoucherText = null,
                        PaidVoucherText = null,
                    };
                })
                .ToList();
        }

        /// <summary>保存工资单（同期间同员工存在则更新，否则新建）。</summary>
        public async Task<SalaryBillDto> SaveBillAsync(Guid accountSetId, SaveSalaryBillRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var employee = await _salaryRepository.GetEmployeeByIdAsync(accountSetId, request.EmployeeId, cancellationToken)
                           ?? throw new SalaryException("员工不存在。");

            var existing = await _salaryRepository.FindBillAsync(accountSetId, request.Period, request.EmployeeId, cancellationToken);
            SalaryBill bill;
            if (existing is null)
            {
                try
                {
                    bill = SalaryBill.Create(
                        accountSetId,
                        request.Period,
                        request.EmployeeId,
                        request.Gross,
                        request.Deductible,
                        request.PersonalDeduction,
                        request.TaxThreshold,
                        request.Tax,
                        request.Adjust,
                        request.CompanyPart);
                }
                catch (ArgumentException ex)
                {
                    throw new SalaryException(ex.Message);
                }

                await _salaryRepository.AddBillAsync(bill, cancellationToken);
            }
            else
            {
                try
                {
                    existing.Update(
                        request.Gross,
                        request.Deductible,
                        request.PersonalDeduction,
                        request.TaxThreshold,
                        request.Tax,
                        request.Adjust,
                        request.CompanyPart);
                }
                catch (ArgumentException ex)
                {
                    throw new SalaryException(ex.Message);
                }

                bill = existing;
            }

            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return (await GetBillsAsync(accountSetId, request.Period, cancellationToken)).First(b => b.Id == bill.Id);
        }

        /// <summary>删除工资单；不存在时返回 false。</summary>
        public async Task<bool> DeleteBillAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var bill = await _salaryRepository.GetBillByIdAsync(accountSetId, id, cancellationToken);
            if (bill is null)
            {
                return false;
            }

            _salaryRepository.RemoveBill(bill);
            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>工资统计表：按部门汇总指定期间工资单。</summary>
        public async Task<IReadOnlyList<SalarySummaryRowDto>> GetSummaryAsync(Guid accountSetId, int startPeriod, int endPeriod, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var employees = (await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken))
                .ToDictionary(e => e.Id, e => e);
            var bills = new List<SalaryBill>();
            for (var period = startPeriod; period <= endPeriod; period = NextPeriod(period))
            {
                bills.AddRange(await _salaryRepository.GetBillsAsync(accountSetId, period, cancellationToken));
            }

            return bills
                .GroupBy(b => employees.GetValueOrDefault(b.EmployeeId)?.Dept ?? "未分配")
                .OrderBy(g => g.Key)
                .Select(g => new SalarySummaryRowDto
                {
                    Dept = g.Key,
                    EmployeeCount = g.Select(b => b.EmployeeId).Distinct().Count(),
                    Payable = g.Sum(b => b.Payable),
                    PersonalDeduction = g.Sum(b => b.PersonalDeduction),
                    CompanyPart = g.Sum(b => b.CompanyPart),
                    Net = g.Sum(b => b.Net),
                    Cost = g.Sum(b => b.Cost),
                })
                .ToList();
        }

        // —— 计提 / 发放 ——

        /// <summary>计提指定期间工资：为每张未计提且员工成本大于零的工资单生成计提凭证。
        /// 借 员工计提工资科目（员工成本）/ 贷 应付职工薪酬（员工成本）。</summary>
        public async Task<int> GenerateAccrueVouchersAsync(Guid accountSetId, int period, string preparer, CancellationToken cancellationToken = default)
        {
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var payrollAccountId = FindPayrollSubject(accountsById);
            var bills = await _salaryRepository.GetBillsAsync(accountSetId, period, cancellationToken);
            var employees = (await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken))
                .ToDictionary(e => e.Id, e => e);
            var count = 0;
            foreach (var bill in bills)
            {
                if (bill.AccruedVoucherId is not null || bill.Cost <= 0m)
                {
                    continue;
                }

                var employee = employees.GetValueOrDefault(bill.EmployeeId);
                var subjectAccountId = employee?.SubjectAccountId
                                       ?? throw new SalaryException($"员工「{employee?.Name ?? bill.EmployeeId.ToString()}」未设置计提工资科目，无法计提。");
                if (!accountsById.ContainsKey(subjectAccountId))
                {
                    throw new SalaryException("员工计提工资科目不存在。");
                }

                var voucher = await _voucherService.CreateAsync(
                    accountSetId,
                    preparer,
                    new CreateVoucherRequest
                    {
                        Word = "记",
                        Number = 0,
                        Date = PeriodEndDate(period),
                        AttachmentCount = 0,
                        Entries =
                        [
                            new CreateVoucherEntryRequest { Summary = $"{FormatPeriod(period)} 计提工资", AccountId = subjectAccountId, Debit = bill.Cost, Credit = 0m },
                            new CreateVoucherEntryRequest { Summary = $"{FormatPeriod(period)} 计提工资", AccountId = payrollAccountId, Debit = 0m, Credit = bill.Cost },
                        ],
                    },
                    cancellationToken);
                bill.MarkAccrued(voucher.Id);
                count++;
            }

            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return count;
        }

        /// <summary>发放指定期间工资：为每张未发放且实发金额大于零的工资单生成发放凭证。
        /// 借 应付职工薪酬（实发）/ 贷 银行存款（实发）。</summary>
        public async Task<int> GeneratePayVouchersAsync(Guid accountSetId, int period, string preparer, CancellationToken cancellationToken = default)
        {
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var payrollAccountId = FindPayrollSubject(accountsById);
            var bankAccountId = await FindFirstBankAccountIdAsync(accountSetId, cancellationToken)
                                ?? throw new SalaryException("账套还没有启用中的银行存款日记账，无法生成发放工资凭证。");
            var bills = await _salaryRepository.GetBillsAsync(accountSetId, period, cancellationToken);
            var count = 0;
            foreach (var bill in bills)
            {
                if (bill.PaidVoucherId is not null || bill.Net <= 0m)
                {
                    continue;
                }

                var voucher = await _voucherService.CreateAsync(
                    accountSetId,
                    preparer,
                    new CreateVoucherRequest
                    {
                        Word = "记",
                        Number = 0,
                        Date = PeriodEndDate(period),
                        AttachmentCount = 0,
                        Entries =
                        [
                            new CreateVoucherEntryRequest { Summary = $"{FormatPeriod(period)} 发放工资", AccountId = payrollAccountId, Debit = bill.Net, Credit = 0m },
                            new CreateVoucherEntryRequest { Summary = $"{FormatPeriod(period)} 发放工资", AccountId = bankAccountId, Debit = 0m, Credit = bill.Net },
                        ],
                    },
                    cancellationToken);
                bill.MarkPaid(voucher.Id);
                count++;
            }

            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return count;
        }

        /// <summary>计提 / 发放卡片金额（应发合计 / 实发合计）。</summary>
        public async Task<(decimal Payable, decimal Net)> GetAccrueViewAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            var bills = await _salaryRepository.GetBillsAsync(accountSetId, period, cancellationToken);
            return (bills.Sum(b => b.Payable), bills.Sum(b => b.Net));
        }

        private Guid FindPayrollSubject(IReadOnlyDictionary<Guid, Account> accountsById)
        {
            var subject = accountsById.Values.FirstOrDefault(a =>
                a.IsLeaf && a.IsEnabled && a.Code.StartsWith(PayrollSubjectPrefix, StringComparison.Ordinal));
            if (subject is null)
            {
                throw new SalaryException("未找到「应付职工薪酬」科目（编码 2211 开头的末级科目），请先在科目设置中创建。");
            }

            return subject.Id;
        }

        private async Task<Guid?> FindFirstBankAccountIdAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var journals = await _journalRepository.GetAllAsync(accountSetId, cancellationToken);
            return journals.FirstOrDefault(j => j.Type == JournalType.BankDeposit && j.IsEnabled)?.AccountId;
        }

        private static DateOnly PeriodEndDate(int period)
        {
            var year = period / 100;
            var month = period % 100;
            return new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        }

        private static int NextPeriod(int period)
        {
            var year = period / 100;
            var month = period % 100;
            return month == 12 ? (year + 1) * 100 + 1 : period + 1;
        }

        private static string FormatPeriod(int period) => $"{period / 100:D4}-{period % 100:D2}";

        private Employee BuildEmployee(Guid accountSetId, SaveEmployeeRequest request)
        {
            try
            {
                return Employee.Create(
                    accountSetId,
                    request.Code,
                    request.Name,
                    (EmployeeGender)request.Gender,
                    (EmployeeIdType)request.IdType,
                    request.IdNo,
                    request.Dept,
                    request.Phone,
                    request.Email,
                    request.BirthDate,
                    request.HireDate,
                    request.BankName,
                    request.BankAccountNo,
                    request.Post,
                    request.Title,
                    request.SubjectAccountId,
                    (EmployeeStatus)request.Status,
                    request.Remark);
            }
            catch (ArgumentException ex)
            {
                throw new SalaryException(ex.Message);
            }
        }

        private async Task EnsureAccountSetExistsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new SalaryException("指定的账套不存在。");
            }
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsByIdAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }
    }
}
