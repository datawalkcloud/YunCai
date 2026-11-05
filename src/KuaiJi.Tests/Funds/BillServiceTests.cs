using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.AccountSets.Infrastructure;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Accounting.Infrastructure;
using KuaiJi.Modules.Funds.Application;
using KuaiJi.Modules.Funds.Domain;
using KuaiJi.Modules.Funds.Infrastructure;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.Modules.Vouchers.Infrastructure;
using KuaiJi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KuaiJi.Tests.Funds;

/// <summary>
/// 票据管理应用服务测试。覆盖：票据状态流转（作废为终态、非法状态拒绝）、
/// 收款 / 背书的业务前置校验（已结清、不可转让、已作废各自的中文文案）、
/// 按出票日期间区间的台账筛选计数与倒序、附件计数与有效凭证号回显、
/// 批量生成凭证（借 银行存款 1002 / 贷 应收票据 1121；收款与贴现两种摘要前缀；
/// 已生成凭证 / 已作废 / 未设收支类别 / 不存在的跳过；科目缺失整批拒绝且不留半成品；
/// 1002 非末级时取编码最小的启用末级）。
/// </summary>
public sealed class BillServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _bankAccount;
    private readonly Account _billAccount;
    private readonly BillService _service;

    public BillServiceTests()
    {
        _accountSet = _db.AddAccountSet("票据账套");
        _bankAccount = _db.AddAccount(_accountSet, "1002", "银行存款");
        _billAccount = _db.AddAccount(_accountSet, "1121", "应收票据");
        _service = new BillService(
            new AccountSetRepository(_db.Context),
            new BillRepository(_db.Context),
            new AccountRepository(_db.Context),
            new VoucherRepository(_db.Context));
    }

    public void Dispose() => _db.Dispose();

    // —— 装置助手 ——

    private static SaveBillRequest Req(
        string billNo = "P-0001",
        decimal amount = 5000m,
        DateOnly? issueDate = null,
        string? category = "票据收款",
        bool transferable = true,
        int? status = null) => new()
    {
        BillNo = billNo,
        Category = category,
        IssueDate = issueDate ?? new DateOnly(2026, 2, 10),
        Amount = amount,
        Kind = (int)BillKind.BankAcceptance,
        Drawer = "甲公司",
        Acceptor = "乙银行",
        Transferable = transferable,
        Status = status,
        Remark = "银行承兑汇票一张",
    };

    private async Task<BillDto> SeedBillAsync(SaveBillRequest? request = null)
    {
        var dto = await _service.CreateAsync(_accountSet.Id, request ?? Req());
        _db.DetachAll();
        return dto;
    }

    private Task<Bill> ReloadBillAsync(Guid id)
        => _db.Context.Bills.SingleAsync(b => b.Id == id);

    private Task<Voucher> ReloadVoucherAsync(Guid id)
        => _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == id);

    // —— 新建校验 ——

    [Fact]
    public async Task Create_ValidBill_DefaultsToHoldingStatus()
    {
        var dto = await SeedBillAsync();

        Assert.Equal("P-0001", dto.BillNo);
        Assert.Equal(5000m, dto.Amount);
        Assert.Equal((int)BillStatus.Holding, dto.Status);
        Assert.Equal("提示收票已签收", dto.StatusText);
        Assert.Equal("银行汇票", dto.KindText);
        Assert.Null(dto.VoucherId);
    }

    [Fact]
    public async Task Create_DuplicateBillNo_ThrowsBillException()
    {
        await SeedBillAsync();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.CreateAsync(_accountSet.Id, Req()));

        Assert.Equal("票据号码「P-0001」已存在。", ex.Message);
    }

    [Fact]
    public async Task Create_BlankBillNo_ThrowsBillException()
    {
        // ArgumentException 文案会带 " (Parameter 'billNo')" 后缀，透出到 400 响应，断言取核心句。
        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.CreateAsync(_accountSet.Id, Req(billNo: "  ")));

        Assert.StartsWith("票据号码不能为空。", ex.Message);
    }

    [Fact]
    public async Task Create_NonPositiveAmount_ThrowsBillException()
    {
        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.CreateAsync(_accountSet.Id, Req(amount: 0m)));

        Assert.StartsWith("票据金额必须大于零。", ex.Message);
    }

    [Fact]
    public async Task Create_DueDateBeforeIssueDate_ThrowsBillException()
    {
        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.CreateAsync(_accountSet.Id, new SaveBillRequest
            {
                BillNo = "P-0009",
                IssueDate = new DateOnly(2026, 2, 10),
                DueDate = new DateOnly(2026, 2, 1),
                Amount = 100m,
            }));

        Assert.StartsWith("汇票到期日不得早于出票日。", ex.Message);
    }

    [Fact]
    public async Task Create_WithExplicitStatus_AppliesGivenStatus()
    {
        var dto = await SeedBillAsync(Req(status: (int)BillStatus.Endorsing));

        Assert.Equal((int)BillStatus.Endorsing, dto.Status);
        Assert.Equal("背书待签收", dto.StatusText);
    }

    [Fact]
    public async Task Create_WithUndefinedStatus_ThrowsBillException()
    {
        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.CreateAsync(_accountSet.Id, Req(status: 99)));

        Assert.StartsWith("票据状态无效。", ex.Message);
    }

    // —— 状态流转（作废为终态）——

    [Fact]
    public async Task ChangeStatus_ToVoided_SucceedsAndBlocksFurtherChanges()
    {
        var bill = await SeedBillAsync();

        var voided = await _service.ChangeStatusAsync(_accountSet.Id, bill.Id,
            new ChangeBillStatusRequest { Status = (int)BillStatus.Voided });
        Assert.Equal("已作废", voided!.StatusText);
        _db.DetachAll();

        // 已作废为终态，任何再次流转都被拒绝。
        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.ChangeStatusAsync(_accountSet.Id, bill.Id,
                new ChangeBillStatusRequest { Status = (int)BillStatus.Holding }));
        Assert.Equal("票据已作废，不能再变更状态。", ex.Message);
    }

    [Fact]
    public async Task ChangeStatus_UndefinedValue_ThrowsBillException()
    {
        var bill = await SeedBillAsync();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.ChangeStatusAsync(_accountSet.Id, bill.Id,
                new ChangeBillStatusRequest { Status = 42 }));

        Assert.StartsWith("票据状态无效。", ex.Message);
    }

    [Fact]
    public async Task ChangeStatus_NonExistentBill_ReturnsNull()
    {
        Assert.Null(await _service.ChangeStatusAsync(_accountSet.Id, Guid.NewGuid(),
            new ChangeBillStatusRequest { Status = (int)BillStatus.Cashed }));
    }

    [Fact]
    public async Task Collect_HoldingBill_SetsCashedAndRejectsRepeat()
    {
        var bill = await SeedBillAsync();

        var collected = await _service.CollectAsync(_accountSet.Id, bill.Id);
        Assert.Equal("票据已结清", collected!.StatusText);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.CollectAsync(_accountSet.Id, bill.Id));
        Assert.Equal("亲，票据已收款，无需重复收款。", ex.Message);
    }

    [Fact]
    public async Task Collect_VoidedBill_ThrowsBillException()
    {
        var bill = await SeedBillAsync();
        await _service.ChangeStatusAsync(_accountSet.Id, bill.Id,
            new ChangeBillStatusRequest { Status = (int)BillStatus.Voided });
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.CollectAsync(_accountSet.Id, bill.Id));

        Assert.Equal("票据已作废，不能收款。", ex.Message);
    }

    [Fact]
    public async Task Endorse_HoldingTransferableBill_SetsEndorsing()
    {
        var bill = await SeedBillAsync();

        var endorsed = await _service.EndorseAsync(_accountSet.Id, bill.Id);

        Assert.Equal((int)BillStatus.Endorsing, endorsed!.Status);
        Assert.Equal("背书待签收", endorsed.StatusText);
    }

    [Fact]
    public async Task Endorse_NotTransferableBill_ThrowsBillException()
    {
        var bill = await SeedBillAsync(Req(transferable: false));

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.EndorseAsync(_accountSet.Id, bill.Id));

        Assert.Equal("该票据不可转让。", ex.Message);
    }

    [Fact]
    public async Task Endorse_CashedBill_ThrowsBillException()
    {
        var bill = await SeedBillAsync();
        await _service.CollectAsync(_accountSet.Id, bill.Id);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.EndorseAsync(_accountSet.Id, bill.Id));

        Assert.Equal("亲，票据已收款，无法转让。", ex.Message);
    }

    [Fact]
    public async Task Endorse_VoidedBill_ThrowsBillException()
    {
        var bill = await SeedBillAsync();
        await _service.ChangeStatusAsync(_accountSet.Id, bill.Id,
            new ChangeBillStatusRequest { Status = (int)BillStatus.Voided });
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.EndorseAsync(_accountSet.Id, bill.Id));

        Assert.Equal("票据已作废，不能转让。", ex.Message);
    }

    // —— 修改 ——

    [Fact]
    public async Task Update_BillNoTakenByAnother_ThrowsBillException()
    {
        var first = await SeedBillAsync(Req(billNo: "P-0001"));
        var second = await SeedBillAsync(Req(billNo: "P-0002"));

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.UpdateAsync(_accountSet.Id, second.Id, Req(billNo: "P-0001", amount: 7000m)));

        Assert.Equal("票据号码「P-0001」已存在。", ex.Message);
    }

    [Fact]
    public async Task Update_KeepingOwnBillNo_AppliesAmount()
    {
        var bill = await SeedBillAsync();

        var dto = await _service.UpdateAsync(_accountSet.Id, bill.Id, Req(amount: 8000m));

        Assert.NotNull(dto);
        Assert.Equal(8000m, dto!.Amount);
    }

    [Fact]
    public async Task Update_VoidedBillToOtherStatus_ThrowsBillException()
    {
        var bill = await SeedBillAsync();
        await _service.ChangeStatusAsync(_accountSet.Id, bill.Id,
            new ChangeBillStatusRequest { Status = (int)BillStatus.Voided });
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.UpdateAsync(_accountSet.Id, bill.Id, Req(status: (int)BillStatus.Holding)));

        Assert.Equal("票据已作废，不能再变更状态。", ex.Message);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNull()
    {
        Assert.Null(await _service.UpdateAsync(_accountSet.Id, Guid.NewGuid(), Req()));
    }

    // —— 台账查询：期间筛选与计数 ——

    [Fact]
    public async Task GetRange_ByIssueDateWindow_FiltersCountAndOrdersByDateDesc()
    {
        // 2 月两张（收款/付款各一）、1 月与 3 月各一张。
        await SeedBillAsync(Req(billNo: "P-0101", issueDate: new DateOnly(2026, 1, 15)));
        await SeedBillAsync(Req(billNo: "P-0201", issueDate: new DateOnly(2026, 2, 20), category: "票据收款"));
        await SeedBillAsync(Req(billNo: "P-0202", issueDate: new DateOnly(2026, 2, 10), category: "票据付款"));
        await SeedBillAsync(Req(billNo: "P-0301", issueDate: new DateOnly(2026, 3, 5)));
        _db.DetachAll();

        var february = await _service.GetRangeAsync(
            _accountSet.Id, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        // 区间内共 2 张，按出票日倒序；方向（收支类别）计数一收一付。
        Assert.Equal(2, february.Count);
        Assert.Equal("P-0201", february[0].BillNo);
        Assert.Equal("P-0202", february[1].BillNo);
        Assert.Equal(1, february.Count(b => b.Category == "票据收款"));
        Assert.Equal(1, february.Count(b => b.Category == "票据付款"));

        var all = await _service.GetRangeAsync(_accountSet.Id);
        Assert.Equal(4, all.Count);
        Assert.Equal("P-0301", all[0].BillNo);
        Assert.Equal("P-0101", all[3].BillNo);
    }

    [Fact]
    public async Task GetRange_UnknownAccountSet_ThrowsBillException()
    {
        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.GetRangeAsync(Guid.NewGuid()));

        Assert.Equal("指定的账套不存在。", ex.Message);
    }

    [Fact]
    public async Task GetRange_ReportsAttachmentCountAndHidesVoucherNoOfDeletedVoucher()
    {
        // 生成凭证后回显「记-1」；凭证进回收站后凭证号不再显示，但 VoucherId 保留。
        var bill = await SeedBillAsync();
        await _service.AddAttachmentAsync(_accountSet.Id, bill.Id, "票面.jpg", "image/jpeg", [1, 2, 3]);
        var result = await _service.GenerateVouchersAsync(_accountSet.Id, [bill.Id], "collect", "张三", null);
        Assert.Equal(1, result.Created);
        _db.DetachAll();

        var withVoucher = Assert.Single(await _service.GetRangeAsync(_accountSet.Id));
        Assert.Equal("记-1", withVoucher.VoucherNo);
        Assert.Equal(1, withVoucher.AttachmentCount);

        var voucher = await ReloadVoucherAsync((await ReloadBillAsync(bill.Id)).VoucherId!.Value);
        voucher.SoftDelete(DateTimeOffset.UtcNow);
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var afterDelete = Assert.Single(await _service.GetRangeAsync(_accountSet.Id));
        Assert.Null(afterDelete.VoucherNo);
        Assert.NotNull(afterDelete.VoucherId);
    }

    // —— 删除 ——

    [Fact]
    public async Task Delete_WithLiveVoucher_ThrowsBillException()
    {
        var bill = await SeedBillAsync();
        var generated = await _service.GenerateVouchersAsync(_accountSet.Id, [bill.Id], "collect", "张三", null);
        Assert.Equal(1, generated.Created);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.DeleteAsync(_accountSet.Id, bill.Id));

        Assert.Equal("该票据已生成凭证，不能删除；如需删除请先删除对应凭证。", ex.Message);
    }

    [Fact]
    public async Task Delete_AfterVoucherSoftDeleted_Succeeds()
    {
        var bill = await SeedBillAsync();
        var generated = await _service.GenerateVouchersAsync(_accountSet.Id, [bill.Id], "collect", "张三", null);
        Assert.Equal(1, generated.Created);
        var voucher = await ReloadVoucherAsync((await ReloadBillAsync(bill.Id)).VoucherId!.Value);
        voucher.SoftDelete(DateTimeOffset.UtcNow);
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        Assert.True(await _service.DeleteAsync(_accountSet.Id, bill.Id));
        _db.DetachAll();
        Assert.False(await _db.Context.Bills.AnyAsync(b => b.Id == bill.Id));
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsFalse()
    {
        Assert.False(await _service.DeleteAsync(_accountSet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteMany_EmptySelection_ThrowsBillException()
    {
        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.DeleteManyAsync(_accountSet.Id, []));

        Assert.Equal("请先勾选要操作的票据。", ex.Message);
    }

    [Fact]
    public async Task DeleteMany_AllPosted_ThrowsBillException()
    {
        var a = await SeedBillAsync(Req(billNo: "P-0001"));
        var b = await SeedBillAsync(Req(billNo: "P-0002"));
        await _service.GenerateVouchersAsync(_accountSet.Id, [a.Id, b.Id], "collect", "张三", null);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.DeleteManyAsync(_accountSet.Id, [a.Id, b.Id]));

        Assert.Equal("勾选的票据均已生成凭证，不能删除；如需删除请先删除对应凭证。", ex.Message);
    }

    [Fact]
    public async Task DeleteMany_MixedSelection_RemovesOnlyDeletable()
    {
        var free = await SeedBillAsync(Req(billNo: "P-0001"));
        var posted = await SeedBillAsync(Req(billNo: "P-0002"));
        await _service.GenerateVouchersAsync(_accountSet.Id, [posted.Id], "collect", "张三", null);
        _db.DetachAll();

        var removed = await _service.DeleteManyAsync(_accountSet.Id, [free.Id, posted.Id, Guid.NewGuid()]);

        Assert.Equal(1, removed);
        _db.DetachAll();
        Assert.False(await _db.Context.Bills.AnyAsync(b => b.Id == free.Id));
        Assert.True(await _db.Context.Bills.AnyAsync(b => b.Id == posted.Id));
    }

    // —— 批量生成凭证 ——

    [Fact]
    public async Task GenerateVouchers_Collect_DebitsBankCreditsReceivablePerBill()
    {
        // 凭证口径：借 银行存款（1002）/ 贷 应收票据（1121），摘要「票据收款-票据号」，逐张连号。
        var a = await SeedBillAsync(Req(billNo: "P-0001", amount: 5000m));
        var b = await SeedBillAsync(Req(billNo: "P-0002", amount: 3000m));

        var result = await _service.GenerateVouchersAsync(_accountSet.Id, [a.Id, b.Id], "collect", "张三", null);
        _db.DetachAll();

        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(2, await _db.Context.Vouchers.CountAsync(v => v.AccountSetId == _accountSet.Id));

        var voucherA = await ReloadVoucherAsync((await ReloadBillAsync(a.Id)).VoucherId!.Value);
        Assert.Equal("记-1", $"{voucherA.Word}-{voucherA.Number}");
        Assert.Equal(2, voucherA.Entries.Count);
        Assert.Contains(voucherA.Entries, e => e.Summary == "票据收款-P-0001" && e.AccountId == _bankAccount.Id && e.Debit == 5000m);
        Assert.Contains(voucherA.Entries, e => e.Summary == "票据收款-P-0001" && e.AccountId == _billAccount.Id && e.Credit == 5000m);

        var voucherB = await ReloadVoucherAsync((await ReloadBillAsync(b.Id)).VoucherId!.Value);
        Assert.Equal(2, voucherB.Number);
        Assert.Contains(voucherB.Entries, e => e.AccountId == _bankAccount.Id && e.Debit == 3000m);
    }

    [Fact]
    public async Task GenerateVouchers_DiscountKind_UsesDiscountSummaryPrefix()
    {
        var bill = await SeedBillAsync();

        await _service.GenerateVouchersAsync(_accountSet.Id, [bill.Id], "discount", "张三", null);
        _db.DetachAll();

        var voucher = await ReloadVoucherAsync((await ReloadBillAsync(bill.Id)).VoucherId!.Value);
        Assert.All(voucher.Entries, e => Assert.Equal("票据贴现-P-0001", e.Summary));
    }

    [Fact]
    public async Task GenerateVouchers_SkipsVoidedNoCategoryPostedAndMissing()
    {
        var valid = await SeedBillAsync(Req(billNo: "P-0001"));
        var voided = await SeedBillAsync(Req(billNo: "P-0002"));
        await _service.ChangeStatusAsync(_accountSet.Id, voided.Id,
            new ChangeBillStatusRequest { Status = (int)BillStatus.Voided });
        var noCategory = await SeedBillAsync(Req(billNo: "P-0003", category: null));
        var posted = await SeedBillAsync(Req(billNo: "P-0004"));
        await _service.GenerateVouchersAsync(_accountSet.Id, [posted.Id], "collect", "张三", null);
        _db.DetachAll();

        var result = await _service.GenerateVouchersAsync(
            _accountSet.Id,
            [valid.Id, voided.Id, noCategory.Id, posted.Id, Guid.NewGuid(), valid.Id],
            "collect", "张三", null);
        _db.DetachAll();

        // 有效 1 张出票；作废 / 无类别 / 已出票 / 不存在各跳过 1（重复 id 被 Distinct 去重）。
        Assert.Equal(1, result.Created);
        Assert.Equal(4, result.Skipped);
        Assert.Equal(2, await _db.Context.Vouchers.CountAsync(v => v.AccountSetId == _accountSet.Id));
        Assert.Null((await ReloadBillAsync(voided.Id)).VoucherId);
        Assert.Null((await ReloadBillAsync(noCategory.Id)).VoucherId);
    }

    [Fact]
    public async Task GenerateVouchers_EmptySelection_ThrowsBillException()
    {
        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.GenerateVouchersAsync(_accountSet.Id, [], "collect", "张三", null));

        Assert.Equal("请选择票据后生成凭证。", ex.Message);
    }

    [Fact]
    public async Task GenerateVouchers_UnknownAccountSet_ThrowsBillException()
    {
        var bill = await SeedBillAsync();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.GenerateVouchersAsync(Guid.NewGuid(), [bill.Id], "collect", "张三", null));

        Assert.Equal("指定的账套不存在。", ex.Message);
    }

    [Fact]
    public async Task GenerateVouchers_MissingBankAccount_ThrowsAndCreatesNoVoucher()
    {
        // 科目缺失整批拒绝：既不出一张也没有半成品。
        var other = _db.AddAccountSet("无银行科目账套");
        _db.AddAccount(other, "1121", "应收票据");
        var bill = await _service.CreateAsync(other.Id, Req());

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.GenerateVouchersAsync(other.Id, [bill.Id], "collect", "张三", null));

        Assert.Equal("未找到银行存款科目（1002），请先在会计科目中维护。", ex.Message);
        Assert.Equal(0, await _db.Context.Vouchers.CountAsync(v => v.AccountSetId == other.Id));
    }

    [Fact]
    public async Task GenerateVouchers_MissingReceivableAccount_ThrowsWithCodeInMessage()
    {
        var other = _db.AddAccountSet("无应收票据账套");
        _db.AddAccount(other, "1002", "银行存款");
        var bill = await _service.CreateAsync(other.Id, Req());

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.GenerateVouchersAsync(other.Id, [bill.Id], "collect", "张三", null));

        Assert.Equal("未找到应收票据科目（1121），请先在会计科目中维护。", ex.Message);
        Assert.Equal(0, await _db.Context.Vouchers.CountAsync(v => v.AccountSetId == other.Id));
    }

    [Fact]
    public async Task GenerateVouchers_NoBankButCashAccount_FallsBackTo1001()
    {
        // 无 1002 时回退库存现金 1001 作为借方科目。
        var other = _db.AddAccountSet("只有现金账套");
        var cash = _db.AddAccount(other, "1001", "库存现金");
        _db.AddAccount(other, "1121", "应收票据");
        var bill = await _service.CreateAsync(other.Id, Req(amount: 1200m));
        _db.DetachAll();

        var result = await _service.GenerateVouchersAsync(other.Id, [bill.Id], "collect", "张三", null);
        _db.DetachAll();

        Assert.Equal(1, result.Created);
        var voucher = await ReloadVoucherAsync((await ReloadBillAsync(bill.Id)).VoucherId!.Value);
        Assert.Contains(voucher.Entries, e => e.AccountId == cash.Id && e.Debit == 1200m);
    }

    [Fact]
    public async Task GenerateVouchers_BankCodeNotLeaf_PicksFirstEnabledLeafByCode()
    {
        // 1002 本身非末级时，取其启用末级下级中编码最小者；停用末级被跳过。
        var other = _db.AddAccountSet("多级银行账套");
        var bankParent = Account.CreateRoot(other.Id, "1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var disabledLeaf = bankParent.AddChild("1002001", "工行");
        var enabledLeaf = bankParent.AddChild("1002002", "建行");
        var receivable = Account.CreateRoot(other.Id, "1121", "应收票据", AccountCategory.Asset, BalanceDirection.Debit);
        _db.Context.Accounts.Add(bankParent);
        _db.Context.Accounts.Add(disabledLeaf);
        _db.Context.Accounts.Add(enabledLeaf);
        _db.Context.Accounts.Add(receivable);
        await _db.Context.SaveChangesAsync();
        disabledLeaf.Disable();
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var bill = await _service.CreateAsync(other.Id, Req(amount: 900m));
        _db.DetachAll();
        var result = await _service.GenerateVouchersAsync(other.Id, [bill.Id], "collect", "张三", null);
        _db.DetachAll();

        Assert.Equal(1, result.Created);
        var voucher = await ReloadVoucherAsync((await ReloadBillAsync(bill.Id)).VoucherId!.Value);
        Assert.Contains(voucher.Entries, e => e.AccountId == enabledLeaf.Id && e.Debit == 900m);
        Assert.DoesNotContain(voucher.Entries, e => e.AccountId == disabledLeaf.Id);
    }

    // —— 附件 ——

    [Fact]
    public async Task AddAttachment_EmptyContent_ThrowsBillException()
    {
        var bill = await SeedBillAsync();

        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.AddAttachmentAsync(_accountSet.Id, bill.Id, "空文件.bin", null, []));

        Assert.StartsWith("附件内容为空。", ex.Message);
    }

    [Fact]
    public async Task AddAttachment_NonExistentBill_ThrowsBillException()
    {
        var ex = await Assert.ThrowsAsync<BillException>(
            () => _service.AddAttachmentAsync(_accountSet.Id, Guid.NewGuid(), "票面.jpg", "image/jpeg", [1]));

        Assert.Equal("指定的票据不存在。", ex.Message);
    }

    [Fact]
    public async Task AttachmentLifecycle_UploadDownloadCountAndDelete()
    {
        // 注：BillRepository.ListAttachmentsAsync 按 UploadedAt（DateTimeOffset）排序，
        // SQLite 无法翻译该 OrderBy（生产 PostgreSQL 正常），故本用例走台账计数的等价路径。
        var bill = await SeedBillAsync();

        var info = await _service.AddAttachmentAsync(_accountSet.Id, bill.Id, " 票面.jpg ", "image/jpeg", [1, 2, 3]);
        Assert.Equal("票面.jpg", info.FileName);
        Assert.Equal(3L, info.SizeBytes);
        Assert.Equal("image/jpeg", info.ContentType);

        _db.DetachAll();
        var listed = Assert.Single(await _service.GetRangeAsync(_accountSet.Id));
        Assert.Equal(1, listed.AttachmentCount);

        var downloaded = await _service.GetAttachmentAsync(_accountSet.Id, bill.Id, info.Id);
        Assert.Equal(new byte[] { 1, 2, 3 }, downloaded!.Content);
        _db.DetachAll();

        Assert.True(await _service.DeleteAttachmentAsync(_accountSet.Id, bill.Id, info.Id));
        Assert.False(await _service.DeleteAttachmentAsync(_accountSet.Id, bill.Id, info.Id));
        _db.DetachAll();
        Assert.Equal(0, Assert.Single(await _service.GetRangeAsync(_accountSet.Id)).AttachmentCount);
    }
}
