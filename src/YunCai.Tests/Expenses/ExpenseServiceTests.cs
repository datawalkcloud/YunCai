using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Assist.Infrastructure;
using YunCai.Modules.Expenses.Application;
using YunCai.Modules.Expenses.Domain;
using YunCai.Modules.Expenses.Infrastructure;
using YunCai.Modules.Vouchers.Application;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace YunCai.Tests.Expenses;

/// <summary>
/// 费用单据服务测试：编号自动生成（FY-0001 序列，跳过占用号）、单据号判重、
/// 金额与可抵扣税额校验、已入账（Posted）单据的修改 / 删除 / 重复出票拦截、
/// 单张与批量生成凭证的分录正确性（借 费用科目 不含税额 + 借 进项税额 / 贷 对方科目 含税总额）、
/// 进项税额科目的三级查找（显式指定 → 2221 开头且名称含「进项税额」的启用末级 → 任意 2221 启用末级，
/// 全都没有则报可操作提示）、批量出票逐条报错不整批失败、
/// 以及凭证删除后 GetBillsAsync 的 ClearStaleVoucher / SyncStatus 自愈。
/// </summary>
public sealed class ExpenseServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _expenseAccount;
    private readonly Account _counterAccount;
    private readonly Account _taxAccount;
    private readonly ExpenseRepository _expenses;
    private readonly VoucherRepository _vouchers;
    private readonly ExpenseService _service;

    public ExpenseServiceTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _expenseAccount = _db.AddAccount(_accountSet, "560201", "管理费用-办公费");
        _counterAccount = _db.AddAccount(_accountSet, "100201", "银行存款");
        _taxAccount = _db.AddAccount(_accountSet, "222100101", "应交税费-进项税额");

        _expenses = new ExpenseRepository(_db.Context);
        _vouchers = new VoucherRepository(_db.Context);
        var accountRepository = new AccountRepository(_db.Context);
        var voucherService = new VoucherService(
            new AccountSetRepository(_db.Context),
            accountRepository,
            new AssistItemRepository(_db.Context),
            _vouchers,
            new AccountAssistGuard());
        _service = new ExpenseService(
            new AccountSetRepository(_db.Context),
            _expenses,
            accountRepository,
            voucherService);
    }

    public void Dispose() => _db.Dispose();

    // —— 种子助手 ——

    private static SaveExpenseBillRequest NewRequest(
        string? billNo = null,
        decimal amount = 113m,
        decimal deductibleTax = 13m) => new()
    {
        BillNo = billNo,
        BillDate = new DateOnly(2026, 2, 10),
        Reason = "办公费报销",
        ExpenseType = "办公费",
        Payee = "张三",
        SettleMethod = "银行转账",
        Amount = amount,
        DeductibleTax = deductibleTax,
    };

    private async Task<ExpenseBill> SeedBill(
        string? billNo = null,
        decimal amount = 113m,
        decimal deductibleTax = 13m,
        Guid? voucherId = null)
    {
        var bill = ExpenseBill.Create(
            _accountSet.Id, billNo, new DateOnly(2026, 2, 10), "办公费报销", "办公费", null, null, amount, deductibleTax);
        if (voucherId is { } id)
        {
            bill.MarkPosted(id);
        }

        await _expenses.AddBillAsync(bill);
        await _expenses.SaveChangesAsync();
        return bill;
    }

    /// <summary>直接种一张凭证（不经过凭证服务，供回填关联 / 自愈测试使用）。</summary>
    private Voucher SeedVoucher(int number)
    {
        var voucher = Voucher.Create(_accountSet.Id, "记", number, new DateOnly(2026, 2, 10), "测试制单人");
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();
        return voucher;
    }

    private static GenerateExpenseVoucherRequest NewVoucherRequest(
        Guid? expenseAccountId = null,
        Guid? counterAccountId = null,
        Guid? taxAccountId = null) => new()
    {
        ExpenseAccountId = expenseAccountId ?? Guid.Empty,
        CounterAccountId = counterAccountId ?? Guid.Empty,
        TaxAccountId = taxAccountId,
    };

    private async Task<Voucher> LoadVoucherAsync(Guid voucherId)
        => await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == voucherId);

    // —— 新建费用单据 ——

    [Fact]
    public async Task CreateBill_WithoutBillNo_AutoGeneratesSequentialNumbers()
    {
        var first = await _service.CreateBillAsync(_accountSet.Id, NewRequest());
        var second = await _service.CreateBillAsync(_accountSet.Id, NewRequest());

        Assert.Equal("FY-0001", first.BillNo);
        Assert.Equal("FY-0002", second.BillNo);
        Assert.Equal(ExpenseBillStatus.Registered, first.Status);
    }

    [Fact]
    public async Task CreateBill_AutoNumber_FillsSmallestFreeSlot()
    {
        // 自动编号取最小空缺，而不是简单累加：FY-0001、FY-0003 已占用时补 FY-0002。
        await SeedBill("FY-0001");
        await SeedBill("FY-0003");

        var created = await _service.CreateBillAsync(_accountSet.Id, NewRequest());

        Assert.Equal("FY-0002", created.BillNo);
    }

    [Fact]
    public async Task CreateBill_DuplicateBillNo_ThrowsExpenseException()
    {
        await SeedBill("FY-0001");

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.CreateBillAsync(_accountSet.Id, NewRequest(billNo: "FY-0001")));

        Assert.Contains("FY-0001", ex.Message);
        Assert.Contains("已存在", ex.Message);
    }

    [Fact]
    public async Task CreateBill_ZeroAmount_ThrowsExpenseException()
    {
        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.CreateBillAsync(_accountSet.Id, NewRequest(amount: 0m)));

        Assert.Contains("单据金额必须大于零", ex.Message);
    }

    [Fact]
    public async Task CreateBill_DeductibleTaxNotLessThanAmount_ThrowsExpenseException()
    {
        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.CreateBillAsync(_accountSet.Id, NewRequest(amount: 100m, deductibleTax: 100m)));

        Assert.Contains("可抵扣税额必须小于单据金额", ex.Message);
    }

    [Fact]
    public async Task CreateBill_UnknownAccountSet_ThrowsExpenseException()
    {
        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.CreateBillAsync(Guid.NewGuid(), NewRequest()));

        Assert.Contains("账套不存在", ex.Message);
    }

    // —— 修改费用单据 ——

    [Fact]
    public async Task UpdateBill_RegisteredBill_AppliesChanges()
    {
        var bill = await SeedBill("FY-0001");

        var dto = await _service.UpdateBillAsync(_accountSet.Id, bill.Id, NewRequest(amount: 226m, deductibleTax: 26m));

        Assert.NotNull(dto);
        Assert.Equal(226m, dto!.Amount);
        Assert.Equal(26m, dto.DeductibleTax);
        Assert.Equal(200m, dto.NetAmount);
    }

    [Fact]
    public async Task UpdateBill_PostedBill_ThrowsExpenseException()
    {
        var voucher = SeedVoucher(1);
        var bill = await SeedBill("FY-0001", voucherId: voucher.Id);

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.UpdateBillAsync(_accountSet.Id, bill.Id, NewRequest()));

        Assert.Contains("请先删除凭证再修改", ex.Message);
    }

    [Fact]
    public async Task UpdateBill_DuplicateBillNo_ThrowsExpenseException()
    {
        var bill = await SeedBill("FY-0001");
        await SeedBill("FY-0002");

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.UpdateBillAsync(_accountSet.Id, bill.Id, NewRequest(billNo: "FY-0002")));

        Assert.Contains("已存在", ex.Message);
    }

    [Fact]
    public async Task UpdateBill_KeepingOwnBillNo_Succeeds()
    {
        var bill = await SeedBill("FY-0001");

        var dto = await _service.UpdateBillAsync(_accountSet.Id, bill.Id, NewRequest(billNo: "FY-0001", amount: 500m));

        Assert.NotNull(dto);
        Assert.Equal(500m, dto!.Amount);
    }

    [Fact]
    public async Task UpdateBill_NonExistent_ReturnsNull()
    {
        Assert.Null(await _service.UpdateBillAsync(_accountSet.Id, Guid.NewGuid(), NewRequest()));
    }

    // —— 删除费用单据 ——

    [Fact]
    public async Task DeleteBill_RegisteredBill_Succeeds()
    {
        var bill = await SeedBill("FY-0001");

        Assert.True(await _service.DeleteBillAsync(_accountSet.Id, bill.Id));
        Assert.Null(await _expenses.GetBillByIdAsync(_accountSet.Id, bill.Id));
    }

    [Fact]
    public async Task DeleteBill_PostedBill_ThrowsExpenseException()
    {
        var voucher = SeedVoucher(1);
        var bill = await SeedBill("FY-0001", voucherId: voucher.Id);

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.DeleteBillAsync(_accountSet.Id, bill.Id));

        Assert.Contains(bill.BillNo, ex.Message);
        Assert.Contains("请先删除凭证再删除单据", ex.Message);
    }

    [Fact]
    public async Task DeleteBill_NonExistent_ReturnsFalse()
    {
        Assert.False(await _service.DeleteBillAsync(_accountSet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteBillsAsync_MixedIds_SkipsPostedAndMissingWithMessages()
    {
        var registeredA = await SeedBill("FY-0001");
        var registeredB = await SeedBill("FY-0002");
        var voucher = SeedVoucher(1);
        var posted = await SeedBill("FY-0003", voucherId: voucher.Id);
        var missingId = Guid.NewGuid();

        var (affected, skipped, messages) = await _service.DeleteBillsAsync(
            _accountSet.Id, [registeredA.Id, registeredB.Id, posted.Id, missingId]);

        Assert.Equal(2, affected);
        Assert.Equal(2, skipped);
        var message = Assert.Single(messages);
        Assert.Contains("请先删除凭证再删除单据", message);
    }

    // —— 生成凭证 ——

    [Fact]
    public async Task GenerateVoucher_Single_BooksNetExpenseTaxDebitAndFullCounterCredit()
    {
        // 凭证口径：借 费用科目（不含税 100）+ 借 进项税额（13）/ 贷 对方科目（含税 113）。
        var bill = await SeedBill("FY-0001", amount: 113m, deductibleTax: 13m);

        var voucherText = await _service.GenerateVoucherAsync(
            _accountSet.Id, bill.Id, NewVoucherRequest(_expenseAccount.Id, _counterAccount.Id, _taxAccount.Id));

        Assert.Equal("记-1", voucherText);
        var voucher = await LoadVoucherAsync(bill.VoucherId!.Value);
        Assert.Equal("记", voucher.Word);
        Assert.Equal(new DateOnly(2026, 2, 10), voucher.Date);
        Assert.Equal(3, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == _expenseAccount.Id && e.Debit == 100m && e.Credit == 0m);
        Assert.Contains(voucher.Entries, e => e.AccountId == _taxAccount.Id && e.Debit == 13m && e.Credit == 0m);
        Assert.Contains(voucher.Entries, e => e.AccountId == _counterAccount.Id && e.Debit == 0m && e.Credit == 113m);

        var reloaded = await _expenses.GetBillByIdAsync(_accountSet.Id, bill.Id);
        Assert.Equal(ExpenseBillStatus.Posted, reloaded!.Status);
        Assert.Equal(voucher.Id, reloaded.VoucherId);
    }

    [Fact]
    public async Task GenerateVoucher_ZeroDeductibleTax_OmitsTaxEntryAndDebitsFullAmount()
    {
        var bill = await SeedBill("FY-0001", amount: 113m, deductibleTax: 0m);

        await _service.GenerateVoucherAsync(
            _accountSet.Id, bill.Id, NewVoucherRequest(_expenseAccount.Id, _counterAccount.Id));

        var voucher = await LoadVoucherAsync(bill.VoucherId!.Value);
        Assert.Equal(2, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == _expenseAccount.Id && e.Debit == 113m);
        Assert.Contains(voucher.Entries, e => e.AccountId == _counterAccount.Id && e.Credit == 113m);
    }

    [Fact]
    public async Task GenerateVoucher_WithoutExplicitTaxAccount_InfersByNameUnder2221()
    {
        // 未显式指定进项税额科目时，取 2221 开头、名称含「进项税额」的末级科目。
        var bill = await SeedBill("FY-0001", amount: 113m, deductibleTax: 13m);

        await _service.GenerateVoucherAsync(
            _accountSet.Id, bill.Id, NewVoucherRequest(_expenseAccount.Id, _counterAccount.Id, taxAccountId: null));

        var voucher = await LoadVoucherAsync(bill.VoucherId!.Value);
        Assert.Contains(voucher.Entries, e => e.AccountId == _taxAccount.Id && e.Debit == 13m);
    }

    [Fact]
    public async Task GenerateVoucher_TaxNameMissing_FallsBackToAny2221Leaf()
    {
        // 税额科目查找顺序（ExpenseService.ResolveTaxAccountAsync）：显式指定 →
        // 2221 开头且名称含「进项税额」的启用末级 → 退而取 2221 开头的任意启用末级（按编码升序取第一个）。
        // 装置里预置的 222100101「应交税费-进项税额」会命中第二级名称匹配，
        // 这里先把它停用，让「没有可用的进项税额末级科目」这一前提成立，才走得到任意 2221 回落分支。
        _taxAccount.Disable();
        await _db.Context.SaveChangesAsync();
        var fallbackTax = _db.AddAccount(_accountSet, "222100199", "其他税费");
        var bill = await SeedBill("FY-0001", amount: 113m, deductibleTax: 13m);

        await _service.GenerateVoucherAsync(
            _accountSet.Id, bill.Id, NewVoucherRequest(_expenseAccount.Id, _counterAccount.Id, taxAccountId: null));

        var voucher = await LoadVoucherAsync(bill.VoucherId!.Value);
        Assert.Equal(3, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == fallbackTax.Id && e.Debit == 13m && e.Credit == 0m);
        Assert.DoesNotContain(voucher.Entries, e => e.AccountId == _taxAccount.Id);
        // 回落只替换税额行的科目，费用科目记不含税额、对方科目记含税总额的口径不变。
        Assert.Contains(voucher.Entries, e => e.AccountId == _expenseAccount.Id && e.Debit == 100m);
        Assert.Contains(voucher.Entries, e => e.AccountId == _counterAccount.Id && e.Credit == 113m);
    }

    [Fact]
    public async Task GenerateVoucher_NoUsable2221Leaf_ThrowsOperativeHint()
    {
        // 2221 下一个可用末级都没有时不猜科目，给出可操作中文提示，且不留下半成品凭证。
        _taxAccount.Disable();
        await _db.Context.SaveChangesAsync();
        var bill = await SeedBill("FY-0001", amount: 113m, deductibleTax: 13m);

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.GenerateVoucherAsync(
                _accountSet.Id, bill.Id, NewVoucherRequest(_expenseAccount.Id, _counterAccount.Id, taxAccountId: null)));

        Assert.Contains("2221 开头", ex.Message);
        Assert.Contains("手动指定", ex.Message);
        Assert.Empty(await _db.Context.Vouchers.Where(v => v.AccountSetId == _accountSet.Id).ToListAsync());
    }

    [Fact]
    public async Task GenerateVoucher_PostedBill_ThrowsExpenseException()
    {
        var voucher = SeedVoucher(1);
        var bill = await SeedBill("FY-0001", voucherId: voucher.Id);

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.GenerateVoucherAsync(
                _accountSet.Id, bill.Id, NewVoucherRequest(_expenseAccount.Id, _counterAccount.Id)));

        Assert.Contains("已生成凭证", ex.Message);
        Assert.Contains("请勿重复生成", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucher_NonExistentBill_ThrowsExpenseException()
    {
        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.GenerateVoucherAsync(
                _accountSet.Id, Guid.NewGuid(), NewVoucherRequest(_expenseAccount.Id, _counterAccount.Id)));

        Assert.Contains("费用单据不存在", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucher_SameExpenseAndCounterAccount_ThrowsExpenseException()
    {
        var bill = await SeedBill("FY-0001");

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.GenerateVoucherAsync(
                _accountSet.Id, bill.Id, NewVoucherRequest(_expenseAccount.Id, _expenseAccount.Id)));

        Assert.Contains("费用科目与对方科目不能相同", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucher_MissingAccount_ThrowsExpenseException()
    {
        var bill = await SeedBill("FY-0001");

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.GenerateVoucherAsync(
                _accountSet.Id, bill.Id, NewVoucherRequest(expenseAccountId: Guid.Empty, counterAccountId: _counterAccount.Id)));

        Assert.Contains("所选费用科目不存在", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucher_NonLeafExpenseAccount_ThrowsExpenseException()
    {
        var parent = Account.CreateRoot(
            _accountSet.Id, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var child = parent.AddChild("660201", "办公费");
        _db.Context.Accounts.Add(parent);
        _db.Context.Accounts.Add(child);
        await _db.Context.SaveChangesAsync();

        var bill = await SeedBill("FY-0001");

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.GenerateVoucherAsync(
                _accountSet.Id, bill.Id, NewVoucherRequest(parent.Id, _counterAccount.Id)));

        Assert.Contains("不是末级科目", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucher_DisabledAccount_ThrowsExpenseException()
    {
        _expenseAccount.Disable();
        await _db.Context.SaveChangesAsync();
        var bill = await SeedBill("FY-0001");

        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.GenerateVoucherAsync(
                _accountSet.Id, bill.Id, NewVoucherRequest(_expenseAccount.Id, _counterAccount.Id)));

        Assert.Contains("已停用", ex.Message);
    }

    [Fact]
    public async Task GenerateVouchersAsync_MixedBills_GeneratesFailsPostedSkipsMissing()
    {
        // 批量出票：未入账的正常生成；已入账计入失败明细（不整批失败）；不存在的计入跳过。
        var billA = await SeedBill("FY-0001");
        var billB = await SeedBill("FY-0002");
        var voucher = SeedVoucher(1);
        var posted = await SeedBill("FY-0003", voucherId: voucher.Id);
        var missingId = Guid.NewGuid();
        var request = new GenerateExpenseVouchersRequest
        {
            BillIds = [billA.Id, billB.Id, posted.Id, missingId],
            ExpenseAccountId = _expenseAccount.Id,
            CounterAccountId = _counterAccount.Id,
            TaxAccountId = _taxAccount.Id,
        };

        var result = await _service.GenerateVouchersAsync(_accountSet.Id, request);

        Assert.Equal(2, result.Generated);
        Assert.Equal(1, result.Skipped);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(posted.Id, failure.BillId);
        Assert.Equal("FY-0003", failure.BillNo);
        Assert.Contains("请勿重复生成", failure.Reason);
    }

    // —— 列表查询与自愈 ——

    [Fact]
    public async Task GetBillsAsync_UnknownAccountSet_ThrowsExpenseException()
    {
        var ex = await Assert.ThrowsAsync<ExpenseException>(
            () => _service.GetBillsAsync(Guid.NewGuid()));

        Assert.Contains("账套不存在", ex.Message);
    }

    [Fact]
    public async Task GetBillsAsync_HealsStaleVoucherLink_BackToRegistered()
    {
        // 凭证被删除后单据仍持有 VoucherId：列表查询应清引用并回到已登记状态。
        var voucher = SeedVoucher(1);
        var bill = await SeedBill("FY-0001", voucherId: voucher.Id);
        _db.Context.Vouchers.Remove(voucher);
        await _db.Context.SaveChangesAsync();

        var bills = await _service.GetBillsAsync(_accountSet.Id);

        var dto = Assert.Single(bills);
        Assert.Null(dto.VoucherId);
        Assert.Null(dto.VoucherText);
        Assert.Equal(ExpenseBillStatus.Registered, dto.Status);
    }

    [Fact]
    public async Task GetBillsAsync_SyncStatusHealsDriftedStatusToPosted()
    {
        // 历史数据兜底：voucher_id 在而 status 落后（模拟为已登记）时，列表查询修正为已入账。
        var voucher = SeedVoucher(1);
        var bill = await SeedBill("FY-0001", voucherId: voucher.Id);
        await _db.Context.Database.ExecuteSqlRawAsync(
            "UPDATE expense_bills SET status = 1 WHERE id = {0}", bill.Id);

        var bills = await _service.GetBillsAsync(_accountSet.Id);

        var dto = Assert.Single(bills);
        Assert.Equal(ExpenseBillStatus.Posted, dto.Status);
        Assert.Equal("记-1", dto.VoucherText);
    }

    [Fact]
    public async Task GetBillsAsync_KeepsVoucherTextForLiveVoucher()
    {
        var voucher = SeedVoucher(7);
        await SeedBill("FY-0001", voucherId: voucher.Id);

        var bills = await _service.GetBillsAsync(_accountSet.Id);

        var dto = Assert.Single(bills);
        Assert.Equal("记-7", dto.VoucherText);
        Assert.Equal("已入账", dto.StatusText);
    }

    [Fact]
    public async Task GetSummaryAsync_SumsAmountAndDeductibleTax()
    {
        await SeedBill("FY-0001", amount: 113m, deductibleTax: 13m);
        await SeedBill("FY-0002", amount: 226m, deductibleTax: 26m);

        var summary = await _service.GetSummaryAsync(_accountSet.Id);

        Assert.Equal(2, summary.Count);
        Assert.Equal(339m, summary.TotalAmount);
        Assert.Equal(39m, summary.TotalDeductibleTax);
    }
}
