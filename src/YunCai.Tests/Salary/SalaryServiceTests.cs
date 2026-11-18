using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Assist.Infrastructure;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Funds.Infrastructure;
using YunCai.Modules.Salary.Application;
using YunCai.Modules.Salary.Domain;
using YunCai.Modules.Salary.Infrastructure;
using YunCai.Modules.Vouchers.Application;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace YunCai.Tests.Salary;

/// <summary>
/// 工资应用服务测试：员工档案与五险一金的判重/校验、月度工资单的领域口径公式
/// （应发＝总额−应扣、实发＝应发−代扣个人−个税＋调整、员工成本＝应发＋公司承担）、
/// 按部门/员工与跨期间汇总、计提与发放生成凭证的科目取数与拆行去向、重复生成/已结账拒绝、
/// 员工与工资表导入的匹配/金额解析/已生成凭证拦截、批量操作计数、以及工资凭证科目设置的校验。
/// 领域依据见 AGENTS.md「领域须知」：凭证字须取自账套列表、借贷单边校验推迟到审核过账、
/// 期间标识统一 yyyyMM、结账按顺序推进。
/// </summary>
public sealed class SalaryServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly SalaryRepository _salary;
    private readonly SalaryService _service;

    public SalaryServiceTests()
    {
        _accountSet = _db.AddAccountSet("工资测试账套");
        _salary = new SalaryRepository(_db.Context);

        var accountSetRepository = new AccountSetRepository(_db.Context);
        var accountRepository = new AccountRepository(_db.Context);
        var vouchers = new VoucherRepository(_db.Context);
        var voucherService = new VoucherService(
            accountSetRepository,
            accountRepository,
            new AssistItemRepository(_db.Context),
            vouchers,
            new AccountAssistGuard());
        _service = new SalaryService(
            accountSetRepository,
            _salary,
            accountRepository,
            new JournalRepository(_db.Context),
            vouchers,
            voucherService);
    }

    public void Dispose() => _db.Dispose();

    // —— 种子助手 ——

    /// <summary>建一个末级科目（装置里 AddAccount 用 Asset/借方，凭证校验只认末级+启用）。</summary>
    private Account AddAccount(string code, string name) => _db.AddAccount(_accountSet, code, name);

    /// <summary>新建员工（走服务，顺带覆盖 CreateEmployee 路径）；可指定计提工资科目。</summary>
    private async Task<EmployeeDto> AddEmployee(
        string code, string name, string? dept = null, Guid? subjectAccountId = null)
        => await _service.CreateEmployeeAsync(_accountSet.Id, new SaveEmployeeRequest
        {
            Code = code,
            Name = name,
            Gender = 1,
            IdType = 1,
            Dept = dept,
            SubjectAccountId = subjectAccountId,
            Status = 1,
        });

    /// <summary>保存某期间工资单（走服务，覆盖 SaveBill 的数值口径计算）。</summary>
    private async Task<SalaryBillDto> AddBill(
        int period, Guid employeeId,
        decimal gross, decimal deductible = 0m, decimal personal = 0m,
        decimal threshold = 5000m, decimal tax = 0m, decimal adjust = 0m, decimal company = 0m)
        => await _service.SaveBillAsync(_accountSet.Id, new SaveSalaryBillRequest
        {
            Period = period,
            EmployeeId = employeeId,
            Gross = gross,
            Deductible = deductible,
            PersonalDeduction = personal,
            TaxThreshold = threshold,
            Tax = tax,
            Adjust = adjust,
            CompanyPart = company,
        });

    /// <summary>直接经仓储种一张工资单（便于回填悬空凭证引用等 SaveBill 覆盖不到的状态）。</summary>
    private async Task<SalaryBill> SeedBillRaw(
        int period, Guid employeeId, decimal gross,
        decimal deductible = 0m, decimal personal = 0m, decimal tax = 0m, decimal company = 0m)
    {
        var bill = SalaryBill.Create(_accountSet.Id, period, employeeId, gross, deductible, personal, 5000m, tax, 0m, company);
        await _salary.AddBillAsync(bill);
        await _salary.SaveChangesAsync();
        return bill;
    }

    private sealed record VoucherAccounts(Guid Payroll, Guid Bank, Guid Tax, Guid Insurance, Guid Subject);

    /// <summary>
    /// 播种工资凭证所需科目与一本启用的银行存款日记账：
    /// 应付职工薪酬 2211、个税 2221、代扣款项 2241、银行存款 1002，员工成本科目 560201。
    /// 依据 SalaryService 的自动推断前缀（2211/2221 关键字「个人所得税」/2241 关键字「个人」/首个银行日记账）。
    /// </summary>
    private VoucherAccounts SeedVoucherAccounts()
    {
        var payroll = AddAccount("2211", "应付职工薪酬");
        var tax = AddAccount("2221012", "应交税费-应交个人所得税");
        var insurance = AddAccount("2241", "其他应付款-代扣个人社保");
        var bank = AddAccount("1002", "银行存款");
        var subject = AddAccount("560201", "管理费用-工资");
        _db.Context.Journals.Add(Journal.Create(_accountSet.Id, JournalType.BankDeposit, "工行存款日记账", bank.Id));
        _db.Context.SaveChanges();
        _db.DetachAll();
        return new VoucherAccounts(payroll.Id, bank.Id, tax.Id, insurance.Id, subject.Id);
    }

    private async Task<Voucher> LoadSingleVoucherAsync()
        => await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.AccountSetId == _accountSet.Id);

    // ===================== 工资单领域口径 =====================

    [Fact]
    public async Task SaveBill_FullAmounts_ComputesPayableNetAndCostPerItem()
    {
        // 领域公式：应发＝总额−应扣；实发＝应发−代扣个人−个税＋实发调整；员工成本＝应发＋公司承担。
        var emp = await AddEmployee("E001", "张三", "销售部");

        var dto = await AddBill(202602, emp.Id, gross: 10000m, deductible: 500m, personal: 800m, tax: 200m, adjust: 100m, company: 1500m);

        Assert.Equal(9500m, dto.Payable);   // 10000 − 500
        Assert.Equal(8600m, dto.Net);       // 9500 − 800 − 200 + 100
        Assert.Equal(11000m, dto.Cost);     // 9500 + 1500
        // 原始要素逐项回显，证明不是只算总数而是逐字段入库。
        Assert.Equal(10000m, dto.Gross);
        Assert.Equal(500m, dto.Deductible);
        Assert.Equal(800m, dto.PersonalDeduction);
        Assert.Equal(200m, dto.Tax);
        Assert.Equal(100m, dto.Adjust);
        Assert.Equal(1500m, dto.CompanyPart);
    }

    [Fact]
    public async Task SaveBill_NegativeAmount_ThrowsSalaryException()
    {
        // SalaryBill.GuardAmounts：工资各项金额不能为负数。
        var emp = await AddEmployee("E001", "张三");

        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => AddBill(202602, emp.Id, gross: -1m));

        Assert.Contains("工资各项金额不能为负数", ex.Message);
    }

    [Fact]
    public async Task SaveBill_NegativeTaxThreshold_ThrowsSalaryException()
    {
        // SalaryBill.GuardAmounts：个税免征额单独校验，不能为负数。
        var emp = await AddEmployee("E001", "张三");

        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => AddBill(202602, emp.Id, gross: 10000m, threshold: -1m));

        Assert.Contains("个税免征额不能为负数", ex.Message);
    }

    [Fact]
    public async Task SaveBill_UnknownEmployee_ThrowsSalaryException()
    {
        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => _service.SaveBillAsync(_accountSet.Id, new SaveSalaryBillRequest
            {
                Period = 202602,
                EmployeeId = Guid.NewGuid(),
                Gross = 100m,
            }));

        Assert.Contains("员工不存在", ex.Message);
    }

    [Fact]
    public async Task SaveBill_SamePeriodSameEmployee_UpdatesInsteadOfInsertingSecondRow()
    {
        // SaveBillAsync 命中同期间同员工走更新分支，工资表每期每人唯一。
        var emp = await AddEmployee("E001", "张三");
        await AddBill(202602, emp.Id, gross: 10000m);

        var updated = await AddBill(202602, emp.Id, gross: 12000m, company: 2000m);

        Assert.Equal(12000m, updated.Gross);
        Assert.Equal(14000m, updated.Cost); // 12000 + 2000
        var bills = await _service.GetBillsAsync(_accountSet.Id, 202602);
        Assert.Single(bills);
    }

    // ===================== 员工档案校验 =====================

    [Fact]
    public async Task CreateEmployee_DuplicateCode_ThrowsSalaryException()
    {
        await AddEmployee("E001", "张三");

        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => AddEmployee("E001", "李四"));

        Assert.Contains("员工编号「E001」已存在", ex.Message);
    }

    [Fact]
    public async Task CreateEmployee_EmptyCode_ThrowsSalaryException()
    {
        // Employee.GuardCode：编号为空 → ArgumentException 被服务层转 SalaryException。
        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => _service.CreateEmployeeAsync(_accountSet.Id, new SaveEmployeeRequest
            {
                Code = "   ",
                Name = "张三",
                Gender = 1,
                IdType = 1,
                Status = 1,
            }));

        Assert.Contains("员工编号不能为空", ex.Message);
    }

    // ===================== 税局人员同步 =====================

    [Fact]
    public async Task SyncIit_NewPerson_UsesProfileDefaultsAndGeneratesCode()
    {
        await AddEmployee("001", "老员工");

        var result = await _service.SyncIitPersonnelAsync(_accountSet.Id, [
            new IitPersonnelSyncRow { Name = "张三", IdNo = "3412************97", Dept = "销售部" },
        ]);

        Assert.Equal(1, result.Imported);
        var created = (await _service.GetEmployeesAsync(_accountSet.Id)).Single(e => e.Name == "张三");
        // 税局只给了姓名与部门：编号取现有末尾数字 +1，其余一律用员工档案默认值。
        Assert.Equal("002", created.Code);
        Assert.Equal("男", created.GenderText);
        Assert.Equal("居民身份证", created.IdTypeText);
        Assert.Equal("雇员", created.EmploymentTypeText);
        Assert.Equal("在职", created.StatusText);
        Assert.True(created.DeductBasicExpense);
        Assert.True(created.PaysSocialInsurance);
        Assert.Equal("销售部", created.Dept);
        // 脱敏证件号不能当真实号码存进档案。
        Assert.Null(created.IdNo);
    }

    [Fact]
    public async Task SyncIit_MatchedEmployee_OverwritesOnlyWhatTaxProvided()
    {
        var employee = await _service.CreateEmployeeAsync(_accountSet.Id, new SaveEmployeeRequest
        {
            Code = "001",
            Name = "张三",
            Gender = 2,
            IdType = 1,
            IdNo = "341201199001010097",
            Email = "z@example.com",
            Status = 1,
        });

        var result = await _service.SyncIitPersonnelAsync(_accountSet.Id, [
            new IitPersonnelSyncRow
            {
                Name = "张三",
                IdNo = "3412************97",
                Phone = "138****0000",
                Dept = "销售部",
                HireDate = "2026-01-05",
            },
        ]);

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Imported);
        var synced = (await _service.GetEmployeesAsync(_accountSet.Id)).Single(e => e.Id == employee.Id);
        // 编号不动（工资表按编号匹配员工）；税局没给或给的是脱敏值的列保留档案原值。
        Assert.Equal("001", synced.Code);
        Assert.Equal("女", synced.GenderText);
        Assert.Equal("341201199001010097", synced.IdNo);
        Assert.Null(synced.Phone);
        Assert.Equal("z@example.com", synced.Email);
        Assert.Equal("销售部", synced.Dept);
        Assert.Equal(new DateOnly(2026, 1, 5), synced.HireDate);
    }

    [Fact]
    public async Task SyncIit_TwoRowsSameName_ResignsFirstAndRegistersSecondSeparately()
    {
        await AddEmployee("001", "张三", dept: "销售部");

        var result = await _service.SyncIitPersonnelAsync(_accountSet.Id, [
            new IitPersonnelSyncRow { Name = "张三", LeaveDate = "2026-03-31" },
            new IitPersonnelSyncRow { Name = "张三" },
        ]);

        Assert.Equal(1, result.Updated);
        // 税局只回脱敏号时同名两笔分不出谁是谁：第二笔另开一档，不把两个人并成一个。
        Assert.Equal(1, result.Imported);
        var original = await _db.Context.Employees.SingleAsync(e => e.AccountSetId == _accountSet.Id && e.Code == "001");
        Assert.Equal(EmployeeStatus.Resigned, original.Status);
        Assert.Equal(new DateOnly(2026, 3, 31), original.LeaveDate);
    }

    [Fact]
    public async Task DeleteEmployee_HasSalaryBills_ThrowsAndKeepsEmployee()
    {
        var emp = await AddEmployee("E001", "张三");
        await AddBill(202602, emp.Id, gross: 10000m);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => _service.DeleteEmployeeAsync(_accountSet.Id, emp.Id));

        Assert.Contains("已生成 1 张工资单", ex.Message);
        Assert.Contains("请先删除工资单再删除员工", ex.Message);
        Assert.Single(await _service.GetEmployeesAsync(_accountSet.Id));
    }

    // ===================== 五险一金 =====================

    [Fact]
    public async Task GetInsurance_FirstAccess_SeedsSixDefaultItemsInOrder()
    {
        // 首次访问补种口径六险种，按 SortOrder 升序；比例按百分比存储。
        var items = await _service.GetInsuranceAsync(_accountSet.Id);

        Assert.Equal(6, items.Count);
        Assert.Equal("养老保险", items[0].Name);
        Assert.Equal(20m, items[0].CompanyRate);
        Assert.Equal(8m, items[0].PersonalRate);
        Assert.Equal("住房公积金", items[5].Name);
        // 工伤保险个人不缴 → PersonalRate 为空。
        Assert.Null(items.Single(i => i.Name == "工伤保险").PersonalRate);
    }

    [Fact]
    public async Task CreateInsurance_DuplicateName_ThrowsSalaryException()
    {
        await _service.GetInsuranceAsync(_accountSet.Id); // 补种默认险种（含养老保险）

        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => _service.CreateInsuranceAsync(_accountSet.Id, new SaveInsuranceRequest
            {
                Name = "养老保险",
                CompanyRate = 16m,
                PersonalRate = 8m,
            }));

        Assert.Contains("险种「养老保险」已存在", ex.Message);
    }

    [Fact]
    public async Task UpdateInsurance_BaseAmount_PersistsAcrossReload()
    {
        var pension = (await _service.GetInsuranceAsync(_accountSet.Id)).Single(i => i.Name == "养老保险");

        var updated = await _service.UpdateInsuranceAsync(_accountSet.Id, pension.Id, new SaveInsuranceRequest
        {
            Name = pension.Name,
            CompanyRate = pension.CompanyRate,
            PersonalRate = pension.PersonalRate,
            BaseAmount = 8000m,
        });

        Assert.NotNull(updated);
        Assert.Equal(8000m, updated!.BaseAmount);
        var reloaded = await _service.GetInsuranceAsync(_accountSet.Id);
        Assert.Equal(8000m, reloaded.Single(i => i.Id == pension.Id).BaseAmount);
    }

    [Fact]
    public async Task CreateEmployee_IndividualInsuranceRates_PersistWithoutBase()
    {
        var pension = (await _service.GetInsuranceAsync(_accountSet.Id)).Single(i => i.Name == "养老保险");

        var created = await _service.CreateEmployeeAsync(_accountSet.Id, new SaveEmployeeRequest
        {
            Code = "E001",
            Name = "张三",
            Gender = 1,
            IdType = 1,
            Status = 1,
            PaysSocialInsurance = true,
            Insurances =
            [
                new SaveEmployeeInsuranceRequest
                {
                    InsuranceItemId = pension.Id,
                    InsuranceBase = 0m,
                    CompanyRate = 21m,
                    PersonalRate = 9m,
                },
            ],
        });

        var reloaded = (await _service.GetEmployeesAsync(_accountSet.Id)).Single(e => e.Id == created.Id);
        var row = Assert.Single(reloaded.Insurances);
        Assert.Equal(21m, row.CompanyRate);
        Assert.Equal(9m, row.PersonalRate);
        Assert.Equal(0m, row.InsuranceBase);
    }

    // ===================== 按部门/员工汇总 =====================

    [Fact]
    public async Task GetSummary_TwoDeptsAcrossPeriods_AggregatesPerDept()
    {
        // 跨 202602~202603 两个期间迭代；按部门分组，员工数取去重、金额逐项求和；
        // 部门为空的员工归入「未分配」。
        var zhang = await AddEmployee("E001", "张三", "销售部", AddAccount("560201", "管理费用-工资").Id);
        var li = await AddEmployee("E002", "李四", "销售部");
        var wang = await AddEmployee("E003", "王五"); // 无部门 → 未分配

        // 销售部：E001 两月 + E002 一月。
        await AddBill(202602, zhang.Id, gross: 10000m, company: 1000m);
        await AddBill(202603, zhang.Id, gross: 8000m);
        await AddBill(202602, li.Id, gross: 6000m, deductible: 1000m, company: 500m);
        await AddBill(202603, wang.Id, gross: 7000m);

        var rows = await _service.GetSummaryAsync(_accountSet.Id, 202602, 202603);

        var sales = Assert.Single(rows, r => r.Dept == "销售部");
        Assert.Equal(2, sales.EmployeeCount);          // 去重员工数
        Assert.Equal(23000m, sales.Payable);           // 10000 + 8000 + (6000-1000)
        Assert.Equal(1500m, sales.CompanyPart);        // 1000 + 0 + 500
        Assert.Equal(24500m, sales.Cost);              // 11000 + 8000 + 5500

        var unassigned = Assert.Single(rows, r => r.Dept == "未分配");
        Assert.Equal(1, unassigned.EmployeeCount);
        Assert.Equal(7000m, unassigned.Payable);
    }

    [Fact]
    public async Task GetAccrueView_Period_SumsPayableAndNetAcrossBills()
    {
        // 卡片口径：应发合计 / 实发合计（不含公司承担）。
        var a = await AddEmployee("E001", "张三");
        var b = await AddEmployee("E002", "李四");
        await AddBill(202602, a.Id, gross: 10000m, personal: 800m, tax: 200m);
        await AddBill(202602, b.Id, gross: 6000m, deductible: 1000m);
        await AddBill(202603, a.Id, gross: 9999m); // 其它期间，不应计入

        var (payable, net) = await _service.GetAccrueViewAsync(_accountSet.Id, 202602);

        Assert.Equal(15000m, payable); // 10000 + (6000-1000)
        Assert.Equal(14000m, net);     // (10000-800-200) + 5000
    }

    // ===================== 生成计提凭证 =====================

    [Fact]
    public async Task GenerateAccrue_BooksEmployeeSubjectDebitAndPayrollCreditAtCost()
    {
        // 计提口径：借 员工计提工资科目（员工成本）/ 贷 应付职工薪酬（员工成本）。
        var acc = SeedVoucherAccounts();
        var emp = await AddEmployee("E001", "张三", "销售部", acc.Subject);
        await AddBill(202602, emp.Id, gross: 10000m, company: 1500m); // Cost = 11500
        _db.DetachAll();

        var count = await _service.GenerateAccrueVouchersAsync(_accountSet.Id, 202602, "财务");

        Assert.Equal(1, count);
        var voucher = await LoadSingleVoucherAsync();
        Assert.Equal(new DateOnly(2026, 2, 28), voucher.Date); // PeriodEndDate：区间归到月末
        Assert.Equal(2, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == acc.Subject && e.Debit == 11500m && e.Credit == 0m);
        Assert.Contains(voucher.Entries, e => e.AccountId == acc.Payroll && e.Credit == 11500m && e.Debit == 0m);
    }

    [Fact]
    public async Task GenerateAccrue_SkipsZeroCostAndAlreadyAccrued()
    {
        // 员工成本 ≤ 0 不计提；已回填 AccruedVoucherId 的跳过 → 天然防重复生成。
        var acc = SeedVoucherAccounts();
        var withCost = await AddEmployee("E001", "张三", subjectAccountId: acc.Subject);
        var zeroCost = await AddEmployee("E002", "李四", subjectAccountId: acc.Subject);
        await AddBill(202602, withCost.Id, gross: 5000m);
        await AddBill(202602, zeroCost.Id, gross: 0m); // Cost = 0
        _db.DetachAll();

        var first = await _service.GenerateAccrueVouchersAsync(_accountSet.Id, 202602, "财务");
        var second = await _service.GenerateAccrueVouchersAsync(_accountSet.Id, 202602, "财务");

        Assert.Equal(1, first);   // 只计提张三一张
        Assert.Equal(0, second);  // 重复执行：张三已计提、李四成本为 0，都不再生成
        // 成本为零的工资单不应被回填凭证引用。
        var bills = await _service.GetBillsAsync(_accountSet.Id, 202602);
        Assert.Null(bills.Single(b => b.EmployeeName == "李四").AccruedVoucherText);
        Assert.Equal("记-1", bills.Single(b => b.EmployeeName == "张三").AccruedVoucherText);
    }

    [Fact]
    public async Task GenerateAccrue_EmployeeWithoutSubject_ThrowsSalaryException()
    {
        // 员工未设置计提工资科目 → 无法确定借方科目，报错并中止整批。
        var acc = SeedVoucherAccounts();
        var emp = await AddEmployee("E001", "张三"); // 无 subject
        await AddBill(202602, emp.Id, gross: 5000m);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => _service.GenerateAccrueVouchersAsync(_accountSet.Id, 202602, "财务"));

        Assert.Contains("未设置计提工资科目", ex.Message);
    }

    // ===================== 生成发放凭证 =====================

    [Fact]
    public async Task GeneratePay_DebitsPayrollGrossAndSplitsBankInsuranceTaxCredits()
    {
        // 发放按实际去向拆行：借 应付职工薪酬（实发+代扣个人+个税）/
        // 贷 银行存款（实发）+ 代扣款项（个人部分）+ 个税。
        var acc = SeedVoucherAccounts();
        var emp = await AddEmployee("E001", "张三", subjectAccountId: acc.Subject);
        // 实发 Net = 10000-800-200 = 9000；代扣个人 800；个税 200。
        await AddBill(202602, emp.Id, gross: 10000m, personal: 800m, tax: 200m);
        _db.DetachAll();

        var count = await _service.GeneratePayVouchersAsync(_accountSet.Id, 202602, "财务");

        Assert.Equal(1, count);
        var voucher = await LoadSingleVoucherAsync();
        Assert.Equal(4, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == acc.Payroll && e.Debit == 10000m);   // 9000+800+200
        Assert.Contains(voucher.Entries, e => e.AccountId == acc.Bank && e.Credit == 9000m);
        Assert.Contains(voucher.Entries, e => e.AccountId == acc.Insurance && e.Credit == 800m);
        Assert.Contains(voucher.Entries, e => e.AccountId == acc.Tax && e.Credit == 200m);
    }

    [Fact]
    public async Task GeneratePay_NoDeductions_BooksOnlyPayrollDebitAndBankCredit()
    {
        // 无代扣、无个税时只出两行：借应付职工薪酬（实发）/ 贷银行存款（实发）。
        var acc = SeedVoucherAccounts();
        var emp = await AddEmployee("E001", "张三", subjectAccountId: acc.Subject);
        await AddBill(202602, emp.Id, gross: 8000m); // Net = 8000
        _db.DetachAll();

        var count = await _service.GeneratePayVouchersAsync(_accountSet.Id, 202602, "财务");

        Assert.Equal(1, count);
        var voucher = await LoadSingleVoucherAsync();
        Assert.Equal(2, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == acc.Payroll && e.Debit == 8000m);
        Assert.Contains(voucher.Entries, e => e.AccountId == acc.Bank && e.Credit == 8000m);
    }

    [Fact]
    public async Task GeneratePay_PersonalDeductionWithoutInsuranceAccount_ThrowsAndCreatesNoVoucher()
    {
        // 代扣个人款项 > 0 但 2241 未配置/不存在 → 拒绝并给出可操作提示，且不留半成品凭证。
        var acc = SeedVoucherAccounts();
        // 删除自动推断可命中的 2241 科目，制造「未配置代扣款项科目」前提。
        var insurance = await _db.Context.Accounts.SingleAsync(a => a.Id == acc.Insurance);
        _db.Context.Accounts.Remove(insurance);
        await _db.Context.SaveChangesAsync();
        var emp = await AddEmployee("E001", "张三", subjectAccountId: acc.Subject);
        await AddBill(202602, emp.Id, gross: 10000m, personal: 800m);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => _service.GeneratePayVouchersAsync(_accountSet.Id, 202602, "财务"));

        Assert.Contains("未配置代扣款项科目", ex.Message);
        Assert.Empty(await _db.Context.Vouchers.Where(v => v.AccountSetId == _accountSet.Id).ToListAsync());
    }

    [Fact]
    public async Task GeneratePay_SkipsZeroNetAndAlreadyPaid()
    {
        // 实发 ≤ 0 与已回填 PaidVoucherId 的都不再生成，防止重复发放。
        var acc = SeedVoucherAccounts();
        var paid = await AddEmployee("E001", "张三", subjectAccountId: acc.Subject);
        var zeroNet = await AddEmployee("E002", "李四", subjectAccountId: acc.Subject);
        await AddBill(202602, paid.Id, gross: 8000m);
        await AddBill(202602, zeroNet.Id, gross: 5000m, personal: 5000m); // Net = 0
        _db.DetachAll();

        var first = await _service.GeneratePayVouchersAsync(_accountSet.Id, 202602, "财务");
        var second = await _service.GeneratePayVouchersAsync(_accountSet.Id, 202602, "财务");

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    // ===================== 已结账期间拒绝 =====================

    [Fact]
    public async Task GenerateAccrue_ClosedPeriod_IsRejectedByVoucherService()
    {
        // 结账按顺序推进（先结转后结账）；已结期间的凭证由 VoucherService 拦截，工资凭证同样不能入账。
        var acc = SeedVoucherAccounts();
        var emp = await AddEmployee("E001", "张三", subjectAccountId: acc.Subject);
        await AddBill(202601, emp.Id, gross: 10000m);

        // SeedVoucherAccounts 已 DetachAll，须重新取跟踪实例结账，否则改动不落库。
        var set = await _db.Context.AccountSets.SingleAsync(a => a.Id == _accountSet.Id);
        set.MarkCarried(2026, 1);
        set.ClosePeriod(2026, 1);
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.GenerateAccrueVouchersAsync(_accountSet.Id, 202601, "财务"));

        Assert.Contains("期间已结账", ex.Message);
        Assert.Contains("2026-01", ex.Message);
    }

    // ===================== 工资凭证科目设置 =====================

    [Fact]
    public async Task SaveVoucherSetting_NonLeafAccount_ThrowsSalaryException()
    {
        // ValidateAccount：所选科目必须是末级；父级科目不能用于记账。
        var parent = Account.CreateRoot(_accountSet.Id, "2211", "应付职工薪酬", AccountCategory.Liability, BalanceDirection.Credit);
        parent.AddChild("221101", "工资");
        _db.Context.Accounts.Add(parent);
        _db.Context.Accounts.Add(parent.Children.First());
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => _service.SaveVoucherSettingAsync(_accountSet.Id, new SaveSalaryVoucherSettingRequest
            {
                PayrollAccountId = parent.Id,
            }));

        Assert.Contains("所选应付职工薪酬科目", ex.Message);
        Assert.Contains("不是末级科目", ex.Message);
    }

    [Fact]
    public async Task SaveVoucherSetting_UnknownAccount_ThrowsSalaryException()
    {
        // ValidateAccount：传入非空但不存在的科目 Id → 报「不存在」。
        var ex = await Assert.ThrowsAsync<SalaryException>(
            () => _service.SaveVoucherSettingAsync(_accountSet.Id, new SaveSalaryVoucherSettingRequest
            {
                BankAccountId = Guid.NewGuid(),
            }));

        Assert.Contains("所选发放账户不存在", ex.Message);
    }

    [Fact]
    public async Task GetVoucherSetting_NoAccounts_ReportsUnresolved()
    {
        // 无 2211 科目、无启用银行日记账时，自动推断落空，DTO 标记未命中供前端提示手工指定。
        var setting = await _service.GetVoucherSettingAsync(_accountSet.Id);

        Assert.False(setting.PayrollResolved);
        Assert.False(setting.BankResolved);
        Assert.Null(setting.PayrollAccountText);
    }

    [Fact]
    public async Task SaveVoucherSetting_ThenGet_ReflectsConfiguredPayroll()
    {
        // 显式配置的科目优先于自动推断，读取时回填显示文本。
        var payroll = AddAccount("2211", "应付职工薪酬-自定");
        _db.DetachAll();

        var setting = await _service.SaveVoucherSettingAsync(_accountSet.Id, new SaveSalaryVoucherSettingRequest
        {
            PayrollAccountId = payroll.Id,
        });

        Assert.Equal(payroll.Id, setting.PayrollAccountId);
        Assert.Equal("2211 应付职工薪酬-自定", setting.PayrollAccountText);
        Assert.True(setting.PayrollResolved);
    }

    // ===================== 工资表导入 =====================

    [Fact]
    public async Task ImportBills_MatchingCode_ParsesAmountsAndAppliesFormula()
    {
        // 金额解析剥离千分位逗号与 ￥ 符号；导入后按领域公式重算实发。
        var emp = await AddEmployee("E001", "张三");

        var result = await _service.ImportBillsAsync(_accountSet.Id, 202602, [
            new SalaryBillImportRow
            {
                Row = 2,
                EmployeeCode = "E001",
                Gross = "￥10,000.00",
                Deductible = "500",
                PersonalDeduction = "800",
                Tax = "200",
            },
        ]);

        Assert.Equal(1, result.Imported);
        Assert.Empty(result.Failures);
        var bill = Assert.Single(await _service.GetBillsAsync(_accountSet.Id, 202602));
        Assert.Equal(10000m, bill.Gross);
        Assert.Equal(8500m, bill.Net); // 9500 - 800 - 200
    }

    [Fact]
    public async Task ImportBills_UnknownCode_RecordsRowFailure()
    {
        var result = await _service.ImportBillsAsync(_accountSet.Id, 202602, [
            new SalaryBillImportRow { Row = 3, EmployeeCode = "E999", Gross = "1000" },
        ]);

        Assert.Equal(0, result.Imported);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(3, failure.Row);
        Assert.Contains("员工编号「E999」不存在", failure.Reason);
    }

    [Fact]
    public async Task ImportBills_BadAmountFormat_RecordsRowFailure()
    {
        // ParseAmount 解析失败抛 FormatException → 转为「金额格式不正确」的行失败。
        var emp = await AddEmployee("E001", "张三");

        var result = await _service.ImportBillsAsync(_accountSet.Id, 202602, [
            new SalaryBillImportRow { Row = 2, EmployeeCode = "E001", Gross = "一千" },
        ]);

        Assert.Equal(0, result.Imported);
        Assert.Contains("金额格式不正确", Assert.Single(result.Failures).Reason);
    }

    [Fact]
    public async Task ImportBills_BillAlreadyAccrued_RejectsOverwrite()
    {
        // 已生成计提凭证的工资单不允许导入覆盖（否则凭证金额与工资单不一致）。
        var emp = await AddEmployee("E001", "张三");
        var bill = await SeedBillRaw(202602, emp.Id, gross: 10000m);
        bill.MarkAccrued(Guid.NewGuid());
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var result = await _service.ImportBillsAsync(_accountSet.Id, 202602, [
            new SalaryBillImportRow { Row = 2, EmployeeCode = "E001", Gross = "99999" },
        ]);

        Assert.Equal(0, result.Imported);
        Assert.Equal(0, result.Updated);
        Assert.Contains("本期已生成凭证", Assert.Single(result.Failures).Reason);
    }

    // ===================== 员工导入 =====================

    [Fact]
    public async Task ImportEmployees_UnknownSubjectCode_FailsThatRowButImportsOthers()
    {
        // 科目编码非末级/不存在时仅该行失败，不影响其它行；空编号或空姓名也计入失败。
        var leafSubject = AddAccount("560201", "管理费用-工资");
        _db.DetachAll();

        var result = await _service.ImportEmployeesAsync(_accountSet.Id, [
            new EmployeeImportRow { Row = 2, Code = "E001", Name = "张三", SubjectAccountCode = leafSubject.Code },
            new EmployeeImportRow { Row = 3, Code = "E002", Name = "李四", SubjectAccountCode = "999999" },
            new EmployeeImportRow { Row = 4, Code = "", Name = "无编号" },
        ]);

        Assert.Equal(1, result.Imported);
        Assert.Equal(2, result.Skipped);
        Assert.Contains(result.Failures, f => f.Row == 3 && f.Reason.Contains("不存在或不是末级科目"));
        Assert.Contains(result.Failures, f => f.Row == 4 && f.Reason.Contains("编号与姓名不能为空"));
    }

    [Fact]
    public async Task ImportEmployees_ExistingCode_UpdatesNotInserts()
    {
        await AddEmployee("E001", "张三", "旧部门");

        var result = await _service.ImportEmployeesAsync(_accountSet.Id, [
            new EmployeeImportRow { Row = 2, Code = "E001", Name = "张三", Dept = "新部门" },
        ]);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Updated);
        var emp = Assert.Single(await _service.GetEmployeesAsync(_accountSet.Id));
        Assert.Equal("新部门", emp.Dept);
    }

    // ===================== 批量删除员工计数 =====================

    [Fact]
    public async Task DeleteEmployees_MixedIds_CountsAffectedSkippedAndMessage()
    {
        // 有工资单的员工计入跳过并给出原因；不存在的 Id 计入跳过；仅无工资单的员工被删除。
        var withBills = await AddEmployee("E001", "张三");
        var clean = await AddEmployee("E002", "李四");
        await AddBill(202602, withBills.Id, gross: 10000m);
        _db.DetachAll();

        var result = await _service.DeleteEmployeesAsync(_accountSet.Id, [withBills.Id, clean.Id, Guid.NewGuid()]);

        Assert.Equal(1, result.Affected);   // 李四
        Assert.Equal(2, result.Skipped);    // 张三（有工资单）+ 随机 Id（不存在）
        var message = Assert.Single(result.Messages);
        Assert.Contains("张三", message);
        Assert.Contains("有 1 张工资单", message);
        var remaining = await _service.GetEmployeesAsync(_accountSet.Id);
        Assert.Equal("张三", Assert.Single(remaining).Name);
    }
}
