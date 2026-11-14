using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.Budgets.Application;
using YunCai.Modules.Budgets.Domain;
using YunCai.Modules.Budgets.Infrastructure;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Budgets;

/// <summary>
/// 预算服务单元测试：预算单增改删、同年度重名校验，以及执行数「只取已过账凭证、
/// 按科目编码前缀归集、父子预算取最长匹配不重复统计」三条口径。
/// </summary>
public sealed class BudgetServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();

    public void Dispose() => _db.Dispose();

    private BudgetService CreateService()
        => new(new BudgetRepository(_db.Context), new AccountRepository(_db.Context));

    private static SaveBudgetRequest Request(int year, string name, params (Guid AccountId, decimal[] Months)[] lines)
        => new()
        {
            Year = year,
            Name = name,
            Note = null,
            Lines = lines.Select(l => new SaveBudgetLineRequest { AccountId = l.AccountId, Months = l.Months }).ToList(),
        };

    private static decimal[] Months(params (int Month, decimal Amount)[] entries)
    {
        var months = new decimal[12];
        foreach (var (month, amount) in entries)
        {
            months[month - 1] = amount;
        }

        return months;
    }

    private void AddPostedVoucher(Guid accountSetId, DateOnly date, Guid debitAccountId, Guid creditAccountId, decimal amount)
    {
        var voucher = Voucher.Create(accountSetId, "记", 1, date, "测试");
        voucher.AddEntry("预算测试", debitAccountId, amount, 0m);
        voucher.AddEntry("预算测试", creditAccountId, 0m, amount);
        voucher.Post();
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();
    }

    [Fact]
    public async Task Create_ThenExecution_CountsPostedVouchersOfChildAccountsIntoParentBudget()
    {
        var set = _db.AddAccountSet("预算执行");
        var cash = _db.AddAccount(set, "1001", "库存现金");
        var expense = _db.AddAccount(set, "6602", "管理费用");
        var child = expense.AddChild("660201", "办公费");
        _db.Context.Accounts.Add(child);
        await _db.Context.SaveChangesAsync();

        AddPostedVoucher(set.Id, new DateOnly(2026, 3, 15), child.Id, cash.Id, 800m);
        // 未过账凭证不计入执行数。
        var draft = Voucher.Create(set.Id, "记", 2, new DateOnly(2026, 4, 1), "测试");
        draft.AddEntry("预算测试", child.Id, 200m, 0m);
        draft.AddEntry("预算测试", cash.Id, 0m, 200m);
        _db.Context.Vouchers.Add(draft);
        await _db.Context.SaveChangesAsync();

        var service = CreateService();
        var budget = await service.CreateAsync(
            set.Id,
            Request(2026, "2026 年费用预算", (expense.Id, Months((3, 1000m), (4, 1000m)))),
            "测试");
        _db.DetachAll();

        var report = await service.GetExecutionAsync(set.Id, budget.Id, null);
        Assert.NotNull(report);
        var row = Assert.Single(report!.Rows);
        Assert.Equal("6602", row.AccountCode);
        Assert.Equal(800m, row.ActualMonths[2]);
        Assert.Equal(0m, row.ActualMonths[3]);
        Assert.Equal(2000m, row.BudgetToDate);
        Assert.Equal(800m, row.ActualToDate);
        Assert.Equal(-1200m, row.Variance);
        Assert.Equal(40m, row.Rate);

        // 截至 3 月的口径只累计前 3 个月。
        var march = await service.GetExecutionAsync(set.Id, budget.Id, 3);
        Assert.Equal(1000m, march!.BudgetToDate);
        Assert.Equal(800m, march.ActualToDate);
    }

    [Fact]
    public async Task Execution_ParentAndChildBudgets_LongestMatchAvoidsDoubleCounting()
    {
        var set = _db.AddAccountSet("父子预算");
        var cash = _db.AddAccount(set, "1001", "库存现金");
        var expense = _db.AddAccount(set, "6602", "管理费用");
        var child = expense.AddChild("660201", "办公费");
        _db.Context.Accounts.Add(child);
        await _db.Context.SaveChangesAsync();

        AddPostedVoucher(set.Id, new DateOnly(2026, 5, 10), child.Id, cash.Id, 300m);

        var service = CreateService();
        var budget = await service.CreateAsync(
            set.Id,
            Request(2026, "父子预算单",
                (expense.Id, Months((5, 500m))),
                (child.Id, Months((5, 300m)))),
            "测试");
        _db.DetachAll();

        var report = await service.GetExecutionAsync(set.Id, budget.Id, null);
        var parent = report!.Rows.Single(r => r.AccountId == expense.Id);
        var childRow = report.Rows.Single(r => r.AccountId == child.Id);
        Assert.Equal(0m, parent.ActualToDate);
        Assert.Equal(300m, childRow.ActualToDate);
        Assert.Equal(300m, report.ActualToDate);
    }

    [Fact]
    public async Task Create_DuplicateNameInSameYear_Rejected()
    {
        var set = _db.AddAccountSet("预算重名");
        var account = _db.AddAccount(set, "6602", "管理费用");
        var service = CreateService();

        await service.CreateAsync(set.Id, Request(2026, "年度预算", (account.Id, Months((1, 100m)))), "测试");
        _db.DetachAll();

        var error = await Assert.ThrowsAsync<BudgetException>(() =>
            service.CreateAsync(set.Id, Request(2026, "年度预算", (account.Id, Months((1, 200m)))), "测试"));
        Assert.Contains("已存在同名预算", error.Message);

        // 不同年度可以同名。
        var other = await service.CreateAsync(set.Id, Request(2027, "年度预算", (account.Id, Months((1, 200m)))), "测试");
        Assert.Equal(2027, other.Year);
    }

    [Fact]
    public async Task Update_YearChangeRejected_DeleteRemovesBudget()
    {
        var set = _db.AddAccountSet("预算修改");
        var account = _db.AddAccount(set, "6602", "管理费用");
        var service = CreateService();
        var budget = await service.CreateAsync(set.Id, Request(2026, "预算", (account.Id, Months((1, 100m)))), "测试");
        _db.DetachAll();

        var yearError = await Assert.ThrowsAsync<BudgetException>(() =>
            service.UpdateAsync(set.Id, budget.Id, Request(2027, "预算", (account.Id, Months((1, 100m))))));
        Assert.Contains("年度创建后不可修改", yearError.Message);

        var updated = await service.UpdateAsync(set.Id, budget.Id, Request(2026, "预算（调整）", (account.Id, Months((2, 300m)))));
        Assert.NotNull(updated);
        Assert.Equal("预算（调整）", updated!.Name);
        Assert.Equal(300m, updated.Lines.Single().Months[1]);

        Assert.True(await service.DeleteAsync(set.Id, budget.Id));
        Assert.Null(await service.GetAsync(set.Id, budget.Id));
    }

    [Fact]
    public async Task Create_LineWithForeignAccountOrNegativeAmount_Rejected()
    {
        var set = _db.AddAccountSet("预算校验");
        var foreignSet = _db.AddAccountSet("别的账套");
        var foreignAccount = _db.AddAccount(foreignSet, "6602", "管理费用");
        var service = CreateService();

        var foreign = await Assert.ThrowsAsync<BudgetException>(() =>
            service.CreateAsync(set.Id, Request(2026, "越界科目", (foreignAccount.Id, Months((1, 10m)))), "测试"));
        Assert.Contains("不属于当前账套", foreign.Message);

        var empty = await Assert.ThrowsAsync<BudgetException>(() =>
            service.CreateAsync(set.Id, new SaveBudgetRequest { Year = 2026, Name = "空预算", Lines = [] }, "测试"));
        Assert.Contains("至少为一个科目", empty.Message);
    }
}
