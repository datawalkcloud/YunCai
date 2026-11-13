using KuaiJi.Modules.Salary.Domain;
using Xunit;

namespace KuaiJi.Tests.Salary;

/// <summary>
/// 工资领域对象边界测试：锁住 SalaryBill 的口径公式与校验、凭证回填的重复防护与悬空引用清理，
/// 以及 Employee / InsuranceItem 的入参守卫。依据 SalaryBill 类注释的金额口径与 AGENTS.md 期间标识约定。
/// 纯领域，无数据库。
/// </summary>
public sealed class SalaryDomainTests
{
    private static readonly Guid SetId = Guid.NewGuid();
    private static readonly Guid EmployeeId = Guid.NewGuid();

    private static SalaryBill NewBill(
        decimal gross, decimal deductible, decimal personal, decimal tax, decimal adjust, decimal company,
        int period = 202602)
        => SalaryBill.Create(SetId, period, EmployeeId, gross, deductible, personal, 5000m, tax, adjust, company);

    [Fact]
    public void PayableNetCost_MatchDomainFormulas()
    {
        // 应发＝总额−应扣；实发＝应发−代扣个人−个税＋调整；员工成本＝应发＋公司承担。
        var bill = NewBill(gross: 10000m, deductible: 500m, personal: 800m, tax: 200m, adjust: 100m, company: 1500m);

        Assert.Equal(9500m, bill.Payable);
        Assert.Equal(8600m, bill.Net);
        Assert.Equal(11000m, bill.Cost);
    }

    [Fact]
    public void Net_NegativeAdjust_ReducesNetWithoutRejection()
    {
        // 实发调整允许为负（补发/扣回），不触发金额守卫；应发不变，仅实发减少。
        var bill = NewBill(gross: 10000m, deductible: 0m, personal: 0m, tax: 0m, adjust: -2000m, company: 0m);

        Assert.Equal(10000m, bill.Payable);
        Assert.Equal(8000m, bill.Net);
    }

    [Fact]
    public void Create_PeriodOutsideSixDigits_Throws()
    {
        // 期间标识统一 yyyyMM，越界（<100000 或 >999999）判为无效。
        Assert.Throws<ArgumentException>(() => NewBill(100m, 0m, 0m, 0m, 0m, 0m, period: 20262));
        Assert.Throws<ArgumentException>(() => NewBill(100m, 0m, 0m, 0m, 0m, 0m, period: 1000000));
    }

    [Fact]
    public void Create_EmptyEmployeeId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SalaryBill.Create(SetId, 202602, Guid.Empty, 100m, 0m, 0m, 5000m, 0m, 0m, 0m));
        Assert.Contains("必须指定员工", ex.Message);
    }

    [Fact]
    public void Create_NegativeCompany_Throws()
    {
        // GuardAmounts：公司承担款项不能为负（与实发调整不同，不参与放宽）。
        Assert.Throws<ArgumentException>(() => NewBill(10000m, 0m, 0m, 0m, 0m, company: -1m));
    }

    [Fact]
    public void MarkAccrued_Twice_ThrowsInvalidOperationException()
    {
        // 同一工资单只能回填一次计提凭证，防止重复计提。
        var bill = NewBill(10000m, 0m, 0m, 0m, 0m, 0m);
        bill.MarkAccrued(Guid.NewGuid());

        var ex = Assert.Throws<InvalidOperationException>(() => bill.MarkAccrued(Guid.NewGuid()));
        Assert.Contains("已计提过凭证", ex.Message);
    }

    [Fact]
    public void MarkPaid_Twice_ThrowsInvalidOperationException()
    {
        var bill = NewBill(10000m, 0m, 0m, 0m, 0m, 0m);
        bill.MarkPaid(Guid.NewGuid());

        var ex = Assert.Throws<InvalidOperationException>(() => bill.MarkPaid(Guid.NewGuid()));
        Assert.Contains("已发放过凭证", ex.Message);
    }

    [Fact]
    public void ClearStaleVouchers_OnlyDropsReferencesToMissingVouchers()
    {
        // 凭证被删除后应清掉悬空引用；仍存在的凭证保留。返回是否发生清除。
        var accrued = Guid.NewGuid();
        var paid = Guid.NewGuid();
        var bill = NewBill(10000m, 0m, 0m, 0m, 0m, 0m);
        bill.MarkAccrued(accrued);
        bill.MarkPaid(paid);

        // paid 凭证仍在册、accrued 已被删除 → 只清 accrued。
        var changed = bill.ClearStaleVouchers(new HashSet<Guid> { paid });

        Assert.True(changed);
        Assert.Equal(paid, bill.PaidVoucherId);
        // accrued 被清掉后可再次计提。
        bill.MarkAccrued(Guid.NewGuid());
        Assert.NotNull(bill.AccruedVoucherId);
    }

    [Fact]
    public void ClearStaleVouchers_AllVouchersLive_ReturnsFalseAndKeepsBoth()
    {
        var accrued = Guid.NewGuid();
        var paid = Guid.NewGuid();
        var bill = NewBill(10000m, 0m, 0m, 0m, 0m, 0m);
        bill.MarkAccrued(accrued);
        bill.MarkPaid(paid);

        var changed = bill.ClearStaleVouchers(new HashSet<Guid> { accrued, paid });

        Assert.False(changed);
        Assert.Equal(accrued, bill.AccruedVoucherId);
        Assert.Equal(paid, bill.PaidVoucherId);
    }

    [Fact]
    public void Employee_Create_TrimsFieldsAndEnforcesMaxLengths()
    {
        // 编号/姓名去空格入库；超长（>20 / >50）分别被守卫拒绝。
        var employee = Employee.Create(SetId, new EmployeeProfile { Code = " E001 ", Name = " 张三 " });
        Assert.Equal("E001", employee.Code);
        Assert.Equal("张三", employee.Name);

        Assert.Throws<ArgumentException>(() => Employee.Create(
            SetId, new EmployeeProfile { Code = new string('X', Employee.MaxCodeLength + 1), Name = "张三" }));
    }

    [Fact]
    public void InsuranceItem_Update_RateOutsideRange_Throws()
    {
        // 缴纳比例按百分比存储，须在 0-100 之间。
        var item = new InsuranceItem(Guid.NewGuid(), SetId, "养老保险", 20m, 8m, 1);

        Assert.Throws<ArgumentException>(() => item.Update("养老保险", 120m, 8m, 0m));
        Assert.Throws<ArgumentException>(() => item.Update("养老保险", 20m, -1m, 0m));
        Assert.Throws<ArgumentException>(() => item.Update("养老保险", 20m, 8m, -1m));
    }
}
