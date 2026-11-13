using YunCai.Modules.Vouchers.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Salary.Domain;
using YunCai.Modules.Salary.Infrastructure;
using YunCai.Modules.Vouchers.Domain;
using System.Globalization;

namespace YunCai.Modules.Salary.Application
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
        private readonly IVoucherRepository _voucherRepository;
        private readonly VoucherService _voucherService;

        public SalaryService(
            IAccountSetRepository accountSetRepository,
            ISalaryRepository salaryRepository,
            IAccountRepository accountRepository,
            IJournalRepository journalRepository,
            IVoucherRepository voucherRepository,
            VoucherService voucherService)
        {
            _accountSetRepository = accountSetRepository;
            _salaryRepository = salaryRepository;
            _accountRepository = accountRepository;
            _journalRepository = journalRepository;
            _voucherRepository = voucherRepository;
            _voucherService = voucherService;
        }

        // —— 员工 ——

        /// <summary>获取全部员工（按编号排列，附计提工资科目解析与各险种缴纳基数）。</summary>
        public async Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var employees = await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var basesByEmployee = (await _salaryRepository.GetEmployeeInsurancesAsync(accountSetId, cancellationToken))
                .GroupBy(b => b.EmployeeId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<EmployeeInsurance>)g.ToList());
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
                        BirthDate = e.BirthDate,
                        BankName = e.BankName,
                        BankAccountNo = e.BankAccountNo,
                        Post = e.Post,
                        Title = e.Title,
                        Education = e.Education,
                        IsDisabled = e.IsDisabled,
                        IsMartyrFamily = e.IsMartyrFamily,
                        IsSoloElder = e.IsSoloElder,
                        IsSeniorExpert = e.IsSeniorExpert,
                        EmploymentTypeText = EmployeeDto.EmploymentTypeTextOf(e.EmploymentType),
                        DeductBasicExpense = e.DeductBasicExpense,
                        PayStartDate = e.PayStartDate,
                        PaysSocialInsurance = e.PaysSocialInsurance,
                        Insurances = basesByEmployee.GetValueOrDefault(e.Id) is { } bases
                            ? bases.Select(b => new EmployeeInsuranceDto
                            {
                                InsuranceItemId = b.InsuranceItemId,
                                InsuranceBase = b.InsuranceBase,
                                CompanyRate = b.CompanyRate,
                                PersonalRate = b.PersonalRate,
                            }).ToList()
                            : [],
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
            await ReplaceEmployeeInsurancesAsync(accountSetId, employee.Id, request, cancellationToken);
            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return (await GetEmployeesAsync(accountSetId, cancellationToken)).First(e => e.Id == employee.Id);
        }

        /// <summary>修改员工档案；不存在时返回 null。</summary>
        public async Task<EmployeeDto?> UpdateEmployeeAsync(Guid accountSetId, Guid id, SaveEmployeeRequest request, CancellationToken cancellationToken = default)
        {
            // 走跟踪查询：Read 用的 GetEmployeeByIdAsync 是 AsNoTracking，改完不会落库。
            var employee = await _salaryRepository.GetEmployeeForUpdateAsync(accountSetId, id, cancellationToken);
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
                employee.Update(BuildProfile(request));
            }
            catch (ArgumentException ex)
            {
                throw new SalaryException(ex.Message);
            }

            await ReplaceEmployeeInsurancesAsync(accountSetId, employee.Id, request, cancellationToken);
            await _salaryRepository.SaveChangesAsync(cancellationToken);
            return (await GetEmployeesAsync(accountSetId, cancellationToken)).First(e => e.Id == employee.Id);
        }

        /// <summary>删除员工；已生成工资单的员工不允许删除（否则工资表会出现孤儿行）。</summary>
        public async Task<bool> DeleteEmployeeAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var employee = await _salaryRepository.GetEmployeeForUpdateAsync(accountSetId, id, cancellationToken);
            if (employee is null)
            {
                return false;
            }

            var bills = await _salaryRepository.CountBillsOfEmployeeAsync(accountSetId, id, cancellationToken);
            if (bills > 0)
            {
                throw new SalaryException($"员工「{employee.Name}」已生成 {bills} 张工资单，请先删除工资单再删除员工。");
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
            var item = new InsuranceItem(Guid.NewGuid(), accountSetId, name, request.CompanyRate, request.PersonalRate, existing.Count + 1, request.BaseAmount);
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
                item.Update(name, request.CompanyRate, request.PersonalRate, request.BaseAmount);
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
            // 用跟踪实例：若凭证已被删除，需要把工资单上的悬空引用清掉并落库。
            var bills = await _salaryRepository.GetBillsForUpdateAsync(accountSetId, period, cancellationToken);
            var employees = (await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken))
                .ToDictionary(e => e.Id, e => e);
            // 凭证字号：只认仍在册（未删除）的凭证，避免删除凭证后工资单还显示「记-12」。
            var vouchersById = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.DeletedAt is null)
                .ToDictionary(v => v.Id, v => v);
            var existingIds = vouchersById.Keys.ToHashSet();
            if (bills.Any(b => b.ClearStaleVouchers(existingIds)))
            {
                await _salaryRepository.SaveChangesAsync(cancellationToken);
            }

            string? VoucherText(Guid? id)
                => id is { } value && vouchersById.TryGetValue(value, out var voucher) ? $"{voucher.Word}-{voucher.Number}" : null;

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
                        AccruedVoucherText = VoucherText(b.AccruedVoucherId),
                        PaidVoucherText = VoucherText(b.PaidVoucherId),
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
                // 跟踪实例：FindBillAsync 是 AsNoTracking，直接改它不会落库。
                var tracked = await _salaryRepository.GetBillForUpdateAsync(accountSetId, existing.Id, cancellationToken) ?? existing;
                try
                {
                    tracked.Update(
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

                bill = tracked;
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

        // —— 员工导入导出 / 批量 ——

        /// <summary>导出员工名册（列与导入模板一致，可改完原样导回）。</summary>
        public async Task<byte[]> ExportEmployeesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var employees = await GetEmployeesAsync(accountSetId, cancellationToken);
            return SalaryExportBuilder.BuildEmployees(employees);
        }

        /// <summary>员工导入模板（仅表头）。</summary>
        public byte[] BuildEmployeeTemplate() => SalaryExportBuilder.BuildEmployeeTemplate();

        /// <summary>
        /// 导入员工：按「编号」匹配，存在则更新、不存在则新增。
        /// 科目按编码匹配末级科目；编码找不到或非末级时该行失败，不影响其它行。
        /// </summary>
        public async Task<EmployeeImportResultDto> ImportEmployeesAsync(
            Guid accountSetId,
            IReadOnlyList<EmployeeImportRow> rows,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var byCode = accounts
                .Where(a => a.IsLeaf)
                .GroupBy(a => a.Code, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var existing = (await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken))
                .ToDictionary(e => e.Code, e => e, StringComparer.Ordinal);

            var failures = new List<SalaryImportFailureDto>();
            var imported = 0;
            var updated = 0;

            foreach (var row in rows)
            {
                var code = row.Code?.Trim();
                var name = row.Name?.Trim();
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                {
                    failures.Add(new SalaryImportFailureDto { Row = row.Row, Reason = "编号与姓名不能为空。" });
                    continue;
                }

                Guid? subjectId = null;
                var subjectCode = row.SubjectAccountCode?.Trim();
                if (!string.IsNullOrEmpty(subjectCode))
                {
                    if (!byCode.TryGetValue(subjectCode, out var subject))
                    {
                        failures.Add(new SalaryImportFailureDto { Row = row.Row, Reason = $"计提工资科目编码「{subjectCode}」不存在或不是末级科目。" });
                        continue;
                    }

                    subjectId = subject.Id;
                }

                var gender = ParseGender(row.GenderText);
                var idType = ParseIdType(row.IdTypeText);
                var status = ParseStatus(row.StatusText);
                var hireDate = ParseDate(row.HireDate);

                try
                {
                    if (existing.TryGetValue(code, out var current))
                    {
                        var tracked = await _salaryRepository.GetEmployeeForUpdateAsync(accountSetId, current.Id, cancellationToken);
                        if (tracked is not null)
                        {
                            // 模板只含基础列，个税档案列与银行/出生日期等保持档案原值。
                            tracked.Update(ProfileOf(tracked) with
                            {
                                Code = code,
                                Name = name,
                                Gender = gender,
                                IdType = idType,
                                IdNo = row.IdNo,
                                Dept = row.Dept,
                                Phone = row.Phone,
                                Email = row.Email,
                                HireDate = hireDate,
                                Post = row.Post,
                                Title = row.Title,
                                SubjectAccountId = subjectId ?? tracked.SubjectAccountId,
                                Status = status,
                                Remark = row.Remark,
                            });
                        }

                        updated++;
                    }
                    else
                    {
                        var employee = Employee.Create(accountSetId, new EmployeeProfile
                        {
                            Code = code,
                            Name = name,
                            Gender = gender,
                            IdType = idType,
                            IdNo = row.IdNo,
                            Dept = row.Dept,
                            Phone = row.Phone,
                            Email = row.Email,
                            HireDate = hireDate,
                            Post = row.Post,
                            Title = row.Title,
                            SubjectAccountId = subjectId,
                            Status = status,
                            Remark = row.Remark,
                        });
                        await _salaryRepository.AddEmployeeAsync(employee, cancellationToken);
                        existing[code] = employee;
                        imported++;
                    }
                }
                catch (ArgumentException ex)
                {
                    failures.Add(new SalaryImportFailureDto { Row = row.Row, Reason = ex.Message });
                }
            }

            if (imported + updated > 0)
            {
                await _salaryRepository.SaveChangesAsync(cancellationToken);
            }

            return new EmployeeImportResultDto
            {
                Imported = imported,
                Updated = updated,
                Skipped = failures.Count,
                Failures = failures,
            };
        }

        /// <summary>
        /// 同步个税「人员信息采集」名单到员工档案。
        ///
        /// 匹配口径与个税页一致：先按证件号码、取不到再按姓名——税局回的是脱敏号
        /// （3412************97），对不上本地完整号时只能靠姓名兜底。命中只覆盖税局给得到的列，
        /// 脱敏的证件号/手机号不回写，员工编号也不动（工资表按编号匹配员工）；没命中则新建，
        /// 税局没有的字段一律用员工档案的默认值。
        /// </summary>
        public async Task<EmployeeImportResultDto> SyncIitPersonnelAsync(
            Guid accountSetId,
            IReadOnlyList<IitPersonnelSyncRow> rows,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var employees = await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken);
            var byIdNo = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
            var byName = new Dictionary<string, Employee>(StringComparer.Ordinal);
            foreach (var employee in employees)
            {
                if (IdKeyOf(employee.IdNo) is { Length: > 0 } idKey)
                {
                    byIdNo.TryAdd(idKey, employee);
                }

                byName.TryAdd(employee.Name, employee);
            }

            var usedCodes = employees.Select(e => e.Code).ToHashSet(StringComparer.Ordinal);
            var nextCode = NextEmployeeCode(employees);
            var claimed = new HashSet<Guid>();

            // 税局只回脱敏证件号，同名两个人分不出谁是谁：一个本地员工只认一笔姓名匹配，
            // 多出来的同名行按新人登记，免得把两个人并成一个档案。
            Employee? FindByNameMatch(string personName)
            {
                var named = byName.GetValueOrDefault(personName);
                return named is not null && !claimed.Contains(named.Id) ? named : null;
            }

            var failures = new List<SalaryImportFailureDto>();
            var imported = 0;
            var updated = 0;

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var name = row.Name?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    failures.Add(new SalaryImportFailureDto { Row = index + 1, Reason = "姓名不能为空。" });
                    continue;
                }

                var idNo = IdKeyOf(row.IdNo);
                var hireDate = ParseDate(row.HireDate);
                var leaveDate = ParseDate(row.LeaveDate);
                var matched = byIdNo.GetValueOrDefault(idNo) ?? FindByNameMatch(name);
                if (matched is not null)
                {
                    claimed.Add(matched.Id);
                }

                try
                {
                    if (matched is not null)
                    {
                        var tracked = await _salaryRepository.GetEmployeeForUpdateAsync(accountSetId, matched.Id, cancellationToken);
                        if (tracked is not null)
                        {
                            var gender = row.GenderText?.Trim() ?? string.Empty;
                            var idType = row.IdTypeText?.Trim() ?? string.Empty;
                            var employType = row.EmployTypeText?.Trim() ?? string.Empty;
                            tracked.Update(ProfileOf(tracked) with
                            {
                                Name = name,
                                Gender = gender.Length == 0 ? tracked.Gender : ParseGender(gender),
                                IdType = idType.Length == 0 ? tracked.IdType : ParseIdType(idType),
                                EmploymentType = employType.Length == 0 ? tracked.EmploymentType : ParseEmploymentType(employType),
                                IdNo = Writable(row.IdNo) ?? tracked.IdNo,
                                Phone = Writable(row.Phone) ?? tracked.Phone,
                                Dept = Writable(row.Dept) ?? tracked.Dept,
                                HireDate = hireDate ?? tracked.HireDate,
                                Status = leaveDate is null ? tracked.Status : EmployeeStatus.Resigned,
                                LeaveDate = leaveDate ?? tracked.LeaveDate,
                            });
                        }

                        updated++;
                        continue;
                    }

                    var code = (row.Code ?? string.Empty).Trim();
                    if (code.Length is 0 or > Employee.MaxCodeLength || usedCodes.Contains(code))
                    {
                        while (usedCodes.Contains(nextCode))
                        {
                            nextCode = (int.Parse(nextCode) + 1).ToString("D3");
                        }

                        code = nextCode;
                    }

                    var employee = Employee.Create(accountSetId, new EmployeeProfile
                    {
                        Code = code,
                        Name = name,
                        Gender = ParseGender(row.GenderText),
                        IdType = ParseIdType(row.IdTypeText),
                        EmploymentType = ParseEmploymentType(row.EmployTypeText),
                        IdNo = Writable(row.IdNo),
                        Phone = Writable(row.Phone),
                        Dept = Writable(row.Dept),
                        HireDate = hireDate,
                        LeaveDate = leaveDate,
                        Status = leaveDate is null ? EmployeeStatus.Active : EmployeeStatus.Resigned,
                    });
                    await _salaryRepository.AddEmployeeAsync(employee, cancellationToken);
                    usedCodes.Add(code);
                    byName.TryAdd(name, employee);
                    if (IdKeyOf(employee.IdNo) is { Length: > 0 } addedKey)
                    {
                        byIdNo.TryAdd(addedKey, employee);
                    }

                    imported++;
                }
                catch (ArgumentException ex)
                {
                    failures.Add(new SalaryImportFailureDto { Row = index + 1, Reason = ex.Message });
                }
            }

            if (imported + updated > 0)
            {
                await _salaryRepository.SaveChangesAsync(cancellationToken);
            }

            return new EmployeeImportResultDto
            {
                Imported = imported,
                Updated = updated,
                Skipped = failures.Count,
                Failures = failures,
            };
        }

        /// <summary>批量删除员工；有工资单的员工计入跳过并说明原因。</summary>
        public async Task<SalaryBulkResultDto> DeleteEmployeesAsync(
            Guid accountSetId,
            IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            var affected = 0;
            var skipped = 0;
            var messages = new List<string>();

            foreach (var id in ids.Distinct())
            {
                var employee = await _salaryRepository.GetEmployeeForUpdateAsync(accountSetId, id, cancellationToken);
                if (employee is null)
                {
                    skipped++;
                    continue;
                }

                var bills = await _salaryRepository.CountBillsOfEmployeeAsync(accountSetId, id, cancellationToken);
                if (bills > 0)
                {
                    skipped++;
                    messages.Add($"「{employee.Name}」有 {bills} 张工资单，已跳过");
                    continue;
                }

                _salaryRepository.RemoveEmployee(employee);
                affected++;
            }

            if (affected > 0)
            {
                await _salaryRepository.SaveChangesAsync(cancellationToken);
            }

            return new SalaryBulkResultDto { Affected = affected, Skipped = skipped, Messages = messages };
        }

        // —— 工资表导入导出 / 工资条 ——

        /// <summary>导出工资统计表。</summary>
        public async Task<byte[]> ExportSummaryAsync(Guid accountSetId, int startPeriod, int endPeriod, CancellationToken cancellationToken = default)
        {
            var rows = await GetSummaryAsync(accountSetId, startPeriod, endPeriod, cancellationToken);
            return SalaryExportBuilder.BuildSummary(rows);
        }

        /// <summary>导出当期工资表。</summary>
        public async Task<byte[]> ExportBillsAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            var bills = await GetBillsAsync(accountSetId, period, cancellationToken);
            return SalaryExportBuilder.BuildBills(bills, period);
        }

        /// <summary>生成当期工资条 PDF（一页 3 条，带裁切线）。</summary>
        public async Task<byte[]> BuildPayslipsAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            var bills = await GetBillsAsync(accountSetId, period, cancellationToken);
            if (bills.Count == 0)
            {
                throw new SalaryException($"{FormatPeriod(period)} 还没有工资单，无法生成工资条。");
            }

            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            return PayslipPdfBuilder.Build(bills, period, accountSet?.CompanyName ?? accountSet?.Name ?? string.Empty);
        }

        /// <summary>
        /// 导入工资表：按「编号」匹配员工，同期间存在则更新金额。
        /// 已生成计提 / 发放凭证的工资单不允许覆盖（否则凭证金额与工资单不一致），计入失败明细。
        /// </summary>
        public async Task<SalaryBillImportResultDto> ImportBillsAsync(
            Guid accountSetId,
            int period,
            IReadOnlyList<SalaryBillImportRow> rows,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var employees = await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken);
            var byCode = employees.ToDictionary(e => e.Code, e => e, StringComparer.Ordinal);
            var byName = employees
                .GroupBy(e => e.Name, StringComparer.Ordinal)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var failures = new List<SalaryImportFailureDto>();
            var imported = 0;
            var updated = 0;

            foreach (var row in rows)
            {
                var code = row.EmployeeCode?.Trim();
                var name = row.EmployeeName?.Trim();
                Employee? employee = null;
                if (!string.IsNullOrEmpty(code) && byCode.TryGetValue(code, out var byCodeHit))
                {
                    employee = byCodeHit;
                }
                else if (!string.IsNullOrEmpty(name) && byName.TryGetValue(name, out var byNameHit))
                {
                    employee = byNameHit;
                }

                if (employee is null)
                {
                    failures.Add(new SalaryImportFailureDto
                    {
                        Row = row.Row,
                        Reason = string.IsNullOrEmpty(code) ? "未填写员工编号，且姓名无法唯一匹配员工。" : $"员工编号「{code}」不存在。",
                    });
                    continue;
                }

                var existing = await _salaryRepository.FindBillAsync(accountSetId, period, employee.Id, cancellationToken);
                if (existing is { AccruedVoucherId: not null } || existing is { PaidVoucherId: not null })
                {
                    failures.Add(new SalaryImportFailureDto { Row = row.Row, Reason = $"员工「{employee.Name}」本期已生成凭证，请先删除凭证再导入。" });
                    continue;
                }

                try
                {
                    var gross = ParseAmount(row.Gross);
                    var deductible = ParseAmount(row.Deductible);
                    var personal = ParseAmount(row.PersonalDeduction);
                    var threshold = string.IsNullOrWhiteSpace(row.TaxThreshold) ? 5000m : ParseAmount(row.TaxThreshold);
                    var tax = ParseAmount(row.Tax);
                    var adjust = ParseAmount(row.Adjust);
                    var company = ParseAmount(row.CompanyPart);

                    if (existing is null)
                    {
                        await _salaryRepository.AddBillAsync(
                            SalaryBill.Create(accountSetId, period, employee.Id, gross, deductible, personal, threshold, tax, adjust, company),
                            cancellationToken);
                        imported++;
                    }
                    else
                    {
                        var tracked = await _salaryRepository.GetBillForUpdateAsync(accountSetId, existing.Id, cancellationToken);
                        tracked?.Update(gross, deductible, personal, threshold, tax, adjust, company);
                        updated++;
                    }
                }
                catch (ArgumentException ex)
                {
                    failures.Add(new SalaryImportFailureDto { Row = row.Row, Reason = ex.Message });
                }
                catch (FormatException)
                {
                    failures.Add(new SalaryImportFailureDto { Row = row.Row, Reason = "金额格式不正确，应填写数字（如 10000.00）。" });
                }
            }

            if (imported + updated > 0)
            {
                await _salaryRepository.SaveChangesAsync(cancellationToken);
            }

            return new SalaryBillImportResultDto { Imported = imported, Updated = updated, Failures = failures };
        }

        // —— 工资凭证科目设置 ——

        /// <summary>读取工资凭证科目映射；未配置项回落到自动推断，并在 DTO 里标出是否命中。</summary>
        public async Task<SalaryVoucherSettingDto> GetVoucherSettingAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var setting = await _salaryRepository.GetVoucherSettingAsync(accountSetId, cancellationToken);

            var payroll = ResolveAccount(accountsById, setting?.PayrollAccountId, AutoPayroll(accountsById));
            var tax = ResolveAccount(accountsById, setting?.TaxAccountId, AutoByPrefix(accountsById, "2221", "个人所得税"));
            var insurance = ResolveAccount(accountsById, setting?.InsuranceAccountId, AutoByPrefix(accountsById, "2241", "个人"));
            var bankId = setting?.BankAccountId is { } configured && accountsById.ContainsKey(configured)
                ? configured
                : await FindFirstBankAccountIdAsync(accountSetId, cancellationToken);

            return new SalaryVoucherSettingDto
            {
                PayrollAccountId = setting?.PayrollAccountId,
                TaxAccountId = setting?.TaxAccountId,
                InsuranceAccountId = setting?.InsuranceAccountId,
                BankAccountId = setting?.BankAccountId,
                PayrollAccountText = Text(accountsById, payroll),
                TaxAccountText = Text(accountsById, tax),
                InsuranceAccountText = Text(accountsById, insurance),
                BankAccountText = Text(accountsById, bankId),
                PayrollResolved = payroll is not null,
                BankResolved = bankId is not null,
            };
        }

        /// <summary>保存工资凭证科目映射；留空的项回落到自动推断。</summary>
        public async Task<SalaryVoucherSettingDto> SaveVoucherSettingAsync(
            Guid accountSetId,
            SaveSalaryVoucherSettingRequest request,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);

            ValidateAccount(accountsById, request.PayrollAccountId, "应付职工薪酬科目");
            ValidateAccount(accountsById, request.TaxAccountId, "个人所得税科目");
            ValidateAccount(accountsById, request.InsuranceAccountId, "代扣个人款项科目");
            ValidateAccount(accountsById, request.BankAccountId, "发放账户");

            var setting = await _salaryRepository.GetVoucherSettingAsync(accountSetId, cancellationToken);
            if (setting is null)
            {
                setting = SalaryVoucherSetting.Create(accountSetId);
                await _salaryRepository.AddVoucherSettingAsync(setting, cancellationToken);
            }

            setting.Update(
                request.PayrollAccountId,
                request.TaxAccountId,
                request.InsuranceAccountId,
                request.BankAccountId);
            await _salaryRepository.SaveChangesAsync(cancellationToken);

            return await GetVoucherSettingAsync(accountSetId, cancellationToken);
        }

        /// <summary>计提指定期间工资：为每张未计提且员工成本大于零的工资单生成计提凭证。
        /// 借 员工计提工资科目（员工成本）/ 贷 应付职工薪酬（员工成本）。</summary>
        public async Task<int> GenerateAccrueVouchersAsync(Guid accountSetId, int period, string preparer, CancellationToken cancellationToken = default)
        {
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var setting = await ResolveVoucherAccountsAsync(accountSetId, accountsById, cancellationToken);
            // 跟踪查询：计提后要回填 AccruedVoucherId，AsNoTracking 的实例改完不落库。
            var bills = await _salaryRepository.GetBillsForUpdateAsync(accountSetId, period, cancellationToken);
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
                            new CreateVoucherEntryRequest { Summary = $"{FormatPeriod(period)} 计提工资", AccountId = setting.PayrollAccountId, Debit = 0m, Credit = bill.Cost },
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
        /// 借 应付职工薪酬（实发 + 代扣个人款项 + 个税）/ 贷 银行存款（实发）+ 应交个人所得税 + 代扣个人款项科目。
        /// 只做「借应付、贷银行」两行会把代扣的社保与个税永久挂在应付职工薪酬上，故按实际去向拆行。</summary>
        public async Task<int> GeneratePayVouchersAsync(Guid accountSetId, int period, string preparer, CancellationToken cancellationToken = default)
        {
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var setting = await ResolveVoucherAccountsAsync(accountSetId, accountsById, cancellationToken);
            var bills = await _salaryRepository.GetBillsForUpdateAsync(accountSetId, period, cancellationToken);
            var employees = (await _salaryRepository.GetEmployeesAsync(accountSetId, cancellationToken))
                .ToDictionary(e => e.Id, e => e);
            var count = 0;
            foreach (var bill in bills)
            {
                if (bill.PaidVoucherId is not null || bill.Net <= 0m)
                {
                    continue;
                }

                var employee = employees.GetValueOrDefault(bill.EmployeeId);
                var summary = $"{FormatPeriod(period)} 发放工资";
                var entries = new List<CreateVoucherEntryRequest>
                {
                    new() { Summary = summary, AccountId = setting.PayrollAccountId, Debit = bill.Net + bill.PersonalDeduction + bill.Tax, Credit = 0m },
                    new() { Summary = summary, AccountId = setting.BankAccountId, Debit = 0m, Credit = bill.Net },
                };

                if (bill.PersonalDeduction > 0m)
                {
                    entries.Add(new CreateVoucherEntryRequest
                    {
                        Summary = summary,
                        AccountId = setting.InsuranceAccountId
                                   ?? throw new SalaryException($"员工「{employee?.Name ?? string.Empty}」有代扣个人款项，但未配置代扣款项科目，请先在「工资凭证设置」中指定。"),
                        Debit = 0m,
                        Credit = bill.PersonalDeduction,
                    });
                }

                if (bill.Tax > 0m)
                {
                    entries.Add(new CreateVoucherEntryRequest
                    {
                        Summary = summary,
                        AccountId = setting.TaxAccountId
                                   ?? throw new SalaryException($"员工「{employee?.Name ?? string.Empty}」有应缴个税，但未配置个人所得税科目，请先在「工资凭证设置」中指定。"),
                        Debit = 0m,
                        Credit = bill.Tax,
                    });
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
                        Entries = entries,
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

        /// <summary>计提 / 发放凭证要用的科目集合（设置优先，其次自动推断）。</summary>
        private sealed record VoucherAccounts(
            Guid PayrollAccountId,
            Guid? TaxAccountId,
            Guid? InsuranceAccountId,
            Guid BankAccountId);

        /// <summary>解析凭证科目：设置里配置的优先；未配置或配置失效时按编码前缀自动推断。</summary>
        private async Task<VoucherAccounts> ResolveVoucherAccountsAsync(
            Guid accountSetId,
            IReadOnlyDictionary<Guid, Account> accountsById,
            CancellationToken cancellationToken)
        {
            var setting = await _salaryRepository.GetVoucherSettingAsync(accountSetId, cancellationToken);

            var payrollId = (setting?.PayrollAccountId is { } configuredPayroll && accountsById.ContainsKey(configuredPayroll)
                                ? configuredPayroll
                                : AutoPayroll(accountsById)?.Id)
                            ?? throw new SalaryException("未找到「应付职工薪酬」科目（2211 开头的末级科目），请先在科目设置中创建，或在「工资凭证设置」中指定。");

            var bankId = (setting?.BankAccountId is { } configuredBank && accountsById.ContainsKey(configuredBank)
                             ? configuredBank
                             : await FindFirstBankAccountIdAsync(accountSetId, cancellationToken))
                         ?? throw new SalaryException("账套还没有启用中的银行存款日记账，无法生成发放工资凭证。");

            Guid? taxId = setting?.TaxAccountId is { } configuredTax && accountsById.ContainsKey(configuredTax)
                ? configuredTax
                : AutoByPrefix(accountsById, "2221", "个人所得税")?.Id;

            Guid? insuranceId = setting?.InsuranceAccountId is { } configuredInsurance && accountsById.ContainsKey(configuredInsurance)
                ? configuredInsurance
                : AutoByPrefix(accountsById, "2241", "个人")?.Id;

            return new VoucherAccounts(payrollId, taxId, insuranceId, bankId);
        }

        private static Account? AutoPayroll(IReadOnlyDictionary<Guid, Account> accountsById)
            => accountsById.Values
                .Where(a => a.IsLeaf && a.IsEnabled && a.Code.StartsWith(PayrollSubjectPrefix, StringComparison.Ordinal))
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .FirstOrDefault();

        /// <summary>按编码前缀 + 名称关键字自动找科目；名称优先命中，其次取该前缀下第一个末级科目。</summary>
        private static Account? AutoByPrefix(IReadOnlyDictionary<Guid, Account> accountsById, string prefix, string keyword)
        {
            var candidates = accountsById.Values
                .Where(a => a.IsLeaf && a.IsEnabled && a.Code.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .ToList();
            return candidates.FirstOrDefault(a => a.Name.Contains(keyword, StringComparison.Ordinal)) ?? candidates.FirstOrDefault();
        }

        private static Guid? ResolveAccount(IReadOnlyDictionary<Guid, Account> accountsById, Guid? configured, Account? fallback)
            => configured is { } id && accountsById.ContainsKey(id) ? id : fallback?.Id;

        private static string? Text(IReadOnlyDictionary<Guid, Account> accountsById, Guid? id)
            => id is { } value && accountsById.TryGetValue(value, out var account)
                ? $"{account.Code} {account.Name}"
                : null;

        private static void ValidateAccount(IReadOnlyDictionary<Guid, Account> accountsById, Guid? id, string roleName)
        {
            if (id is not { } value || value == Guid.Empty)
            {
                return;
            }

            if (!accountsById.TryGetValue(value, out var account))
            {
                throw new SalaryException($"所选{roleName}不存在。");
            }

            if (!account.IsLeaf)
            {
                throw new SalaryException($"所选{roleName}「{account.Code} {account.Name}」不是末级科目，不能用于记账。");
            }
        }

        private static EmployeeGender ParseGender(string? text)
            => text?.Trim() switch
            {
                "女" => EmployeeGender.Female,
                _ => EmployeeGender.Male,
            };

        private static EmployeeIdType ParseIdType(string? text)
            => text?.Trim() switch
            {
                "护照" => EmployeeIdType.Passport,
                "其他" => EmployeeIdType.Other,
                _ => EmployeeIdType.ResidentIdCard,
            };

        private static EmployeeStatus ParseStatus(string? text)
            => string.Equals(text?.Trim(), "离职", StringComparison.Ordinal) ? EmployeeStatus.Resigned : EmployeeStatus.Active;

        private static EmployeeEmploymentType ParseEmploymentType(string? text)
            => text?.Trim() switch
            {
                "保险营销员" => EmployeeEmploymentType.InsuranceAgent,
                "证券经纪人" => EmployeeEmploymentType.SecuritiesBroker,
                "劳务派遣" => EmployeeEmploymentType.LaborDispatch,
                "其他" or "实习学生（全日制学历教育）" => EmployeeEmploymentType.Other,
                _ => EmployeeEmploymentType.Employee,
            };

        /// <summary>证件号码比对口径：去空格、转大写，两边大小写与空格不一致也能对上。</summary>
        private static string IdKeyOf(string? text) => text?.Trim().ToUpperInvariant() ?? string.Empty;

        /// <summary>脱敏值（含 *）与空白都不落档案，取不到就当作税局没给这一列。</summary>
        private static string? Writable(string? text)
            => string.IsNullOrWhiteSpace(text) || text.Contains('*') ? null : text.Trim();

        /// <summary>新增员工的建议编号：现有编号末尾数字的最大值 +1，三位补零。</summary>
        private static string NextEmployeeCode(IEnumerable<Employee> employees)
        {
            var max = 0;
            foreach (var employee in employees)
            {
                var digits = employee.Code.TrimEnd();
                var tail = 0;
                while (tail < digits.Length && char.IsDigit(digits[digits.Length - 1 - tail]))
                {
                    tail++;
                }

                if (tail > 0 && int.TryParse(digits[(digits.Length - tail)..], out var value) && value > max)
                {
                    max = value;
                }
            }

            return (max + 1).ToString("D3");
        }

        private static DateOnly? ParseDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return DateOnly.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
                ? value
                : null;
        }

        private static decimal ParseAmount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0m;
            }

            var cleaned = text.Trim().Replace(",", string.Empty).Replace("￥", string.Empty).Replace("¥", string.Empty);
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new FormatException();
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
                return Employee.Create(accountSetId, BuildProfile(request));
            }
            catch (ArgumentException ex)
            {
                throw new SalaryException(ex.Message);
            }
        }

        /// <summary>保存请求 → 员工档案字段集合。</summary>
        private static EmployeeProfile BuildProfile(SaveEmployeeRequest request) => new()
        {
            Code = request.Code,
            Name = request.Name,
            Gender = (EmployeeGender)request.Gender,
            IdType = (EmployeeIdType)request.IdType,
            IdNo = request.IdNo,
            Dept = request.Dept,
            Phone = request.Phone,
            Email = request.Email,
            BirthDate = request.BirthDate,
            HireDate = request.HireDate,
            BankName = request.BankName,
            BankAccountNo = request.BankAccountNo,
            Post = request.Post,
            Title = request.Title,
            Education = request.Education,
            IsDisabled = request.IsDisabled,
            IsMartyrFamily = request.IsMartyrFamily,
            IsSoloElder = request.IsSoloElder,
            IsSeniorExpert = request.IsSeniorExpert,
            EmploymentType = (EmployeeEmploymentType)request.EmploymentType,
            DeductBasicExpense = request.DeductBasicExpense,
            PayStartDate = request.PayStartDate,
            SubjectAccountId = request.SubjectAccountId,
            PaysSocialInsurance = request.PaysSocialInsurance,
            Status = (EmployeeStatus)request.Status,
            Remark = request.Remark,
        };

        /// <summary>员工档案 → 可编辑字段集合（导入更新时保留模板未覆盖的列）。</summary>
        private static EmployeeProfile ProfileOf(Employee employee) => new()
        {
            Code = employee.Code,
            Name = employee.Name,
            Gender = employee.Gender,
            IdType = employee.IdType,
            IdNo = employee.IdNo,
            Dept = employee.Dept,
            Phone = employee.Phone,
            Email = employee.Email,
            BirthDate = employee.BirthDate,
            HireDate = employee.HireDate,
            LeaveDate = employee.LeaveDate,
            BankName = employee.BankName,
            BankAccountNo = employee.BankAccountNo,
            Post = employee.Post,
            Title = employee.Title,
            Education = employee.Education,
            IsDisabled = employee.IsDisabled,
            IsMartyrFamily = employee.IsMartyrFamily,
            IsSoloElder = employee.IsSoloElder,
            IsSeniorExpert = employee.IsSeniorExpert,
            EmploymentType = employee.EmploymentType,
            DeductBasicExpense = employee.DeductBasicExpense,
            PayStartDate = employee.PayStartDate,
            SubjectAccountId = employee.SubjectAccountId,
            PaysSocialInsurance = employee.PaysSocialInsurance,
            Status = employee.Status,
            Remark = employee.Remark,
        };

        /// <summary>整批替换员工缴纳基数与个人比例；不缴五险一金或未带行时清空。</summary>
        private async Task ReplaceEmployeeInsurancesAsync(
            Guid accountSetId,
            Guid employeeId,
            SaveEmployeeRequest request,
            CancellationToken cancellationToken)
        {
            foreach (var existing in await _salaryRepository.GetEmployeeInsurancesForUpdateAsync(accountSetId, employeeId, cancellationToken))
            {
                _salaryRepository.RemoveEmployeeInsurance(existing);
            }

            if (!request.PaysSocialInsurance)
            {
                return;
            }

            foreach (var line in request.Insurances ?? [])
            {
                if (line.InsuranceItemId == Guid.Empty
                    || (line.InsuranceBase <= 0m && line.CompanyRate is null && line.PersonalRate is null))
                {
                    continue;
                }

                await _salaryRepository.AddEmployeeInsuranceAsync(
                    new EmployeeInsurance(
                        Guid.NewGuid(),
                        accountSetId,
                        employeeId,
                        line.InsuranceItemId,
                        line.InsuranceBase,
                        line.CompanyRate,
                        line.PersonalRate),
                    cancellationToken);
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
