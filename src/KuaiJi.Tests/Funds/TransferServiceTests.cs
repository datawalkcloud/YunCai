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
/// 内部转账应用服务测试。依据 AGENTS.md「出纳闭环（Modules/Funds）」：
/// 内部转账自动在两端各登记一笔流水（类别「内部转账」、往来记为对方户名）；
/// 停用账户只拦新流水（转账同样被拦截）；已生成凭证的转账不可改删；
/// 转账凭证口径为「借 转入科目 / 贷 转出科目」，生成后回写转账与两端流水的关联凭证；
/// 批量出票每笔转账独立一张凭证（转账链路无合并方式，流水合并出票属 JournalService），
/// 已生成凭证与单条失败（科目缺失）不影响其余；凭证被删后引用残留可自愈。
/// </summary>
public sealed class TransferServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _cashAccount;
    private readonly Account _bankAccount;
    private readonly Journal _cashJournal;
    private readonly Journal _bankJournal;
    private readonly TransferService _service;

    public TransferServiceTests()
    {
        _accountSet = _db.AddAccountSet("出纳账套");
        _cashAccount = _db.AddAccount(_accountSet, "1001", "库存现金");
        _bankAccount = _db.AddAccount(_accountSet, "1002", "银行存款");
        _cashJournal = AddJournal(JournalType.Cash, "现金日记账", _cashAccount.Id);
        _bankJournal = AddJournal(JournalType.BankDeposit, "工行存款日记账", _bankAccount.Id);
        _service = new TransferService(
            new AccountSetRepository(_db.Context),
            new JournalRepository(_db.Context),
            new TransferRepository(_db.Context),
            new AccountRepository(_db.Context),
            new VoucherRepository(_db.Context));
    }

    public void Dispose() => _db.Dispose();

    // —— 装置助手 ——

    private Journal AddJournal(JournalType type, string name, Guid accountId)
    {
        var journal = Journal.Create(_accountSet.Id, type, name, accountId);
        _db.Context.Journals.Add(journal);
        _db.Context.SaveChanges();
        return journal;
    }

    private static DateOnly TransferDate => new(2026, 3, 10);

    private SaveTransferRequest Req(
        decimal amount = 1000m,
        Guid? from = null,
        Guid? to = null,
        string? summary = null) => new()
    {
        Date = TransferDate,
        Summary = summary,
        FromJournalId = from ?? _cashJournal.Id,
        ToJournalId = to ?? _bankJournal.Id,
        Amount = amount,
        Remark = "出纳调拨",
    };

    private Task<Journal> ReloadJournalAsync(Guid journalId)
        => _db.Context.Journals.Include(j => j.Entries).SingleAsync(j => j.Id == journalId);

    private Task<Transfer?> ReloadTransferAsync(Guid id)
        => _db.Context.Transfers.SingleOrDefaultAsync(t => t.Id == id);

    private Task<Voucher> ReloadVoucherAsync(Guid id)
        => _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == id);

    /// <summary>按库中当前状态停用某本日记账（服务调用后跟踪器已清空，必须重新加载再改）。</summary>
    private async Task DisableJournalAsync(Guid journalId)
    {
        var journal = await ReloadJournalAsync(journalId);
        journal.SetEnabled(false);
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();
    }

    private async Task<Transfer> SeedTransferAsync(decimal amount = 1000m)
    {
        var dto = await _service.CreateAsync(_accountSet.Id, Req(amount));
        _db.DetachAll();
        var transfer = await ReloadTransferAsync(dto.Id);
        Assert.NotNull(transfer);
        return transfer!;
    }

    // —— 两端流水登记（规则：内部转账自动在两端各登记一笔流水，类别「内部转账」、往来记为对方户名）——

    [Fact]
    public async Task Create_RegistersSymmetricalEntriesOnBothJournals()
    {
        // 转出端记支出、转入端记收入，金额对称；摘要默认「资金内部调拨」。
        var dto = await _service.CreateAsync(_accountSet.Id, Req(1000m));
        _db.DetachAll();

        Assert.Equal(1000m, dto.Amount);
        Assert.Equal("现金日记账", dto.FromJournalName);
        Assert.Equal("工行存款日记账", dto.ToJournalName);
        Assert.Null(dto.VoucherId);
        Assert.Null(dto.VoucherNo);

        var fromJournal = await ReloadJournalAsync(_cashJournal.Id);
        var toJournal = await ReloadJournalAsync(_bankJournal.Id);

        var outEntry = Assert.Single(fromJournal.Entries);
        Assert.Equal(0m, outEntry.Income);
        Assert.Equal(1000m, outEntry.Expense);
        Assert.Equal("内部转账", outEntry.Category);
        Assert.Equal("资金内部调拨", outEntry.Summary);

        var inEntry = Assert.Single(toJournal.Entries);
        Assert.Equal(1000m, inEntry.Income);
        Assert.Equal(0m, inEntry.Expense);
        Assert.Equal("内部转账", inEntry.Category);
    }

    [Fact]
    public async Task Create_CounterpartyTakesOppositeJournalName()
    {
        // 规则：往来记为对方户名——转出端流水的往来是转入账户名，转入端反之。
        await _service.CreateAsync(_accountSet.Id, Req());
        _db.DetachAll();

        var fromJournal = await ReloadJournalAsync(_cashJournal.Id);
        var toJournal = await ReloadJournalAsync(_bankJournal.Id);

        Assert.Equal("工行存款日记账", Assert.Single(fromJournal.Entries).Counterparty);
        Assert.Equal("现金日记账", Assert.Single(toJournal.Entries).Counterparty);
    }

    [Fact]
    public async Task Create_WithExplicitSummary_TrimsAndKeepsCustomText()
    {
        await _service.CreateAsync(_accountSet.Id, Req(summary: "  提现  "));
        _db.DetachAll();

        var fromJournal = await ReloadJournalAsync(_cashJournal.Id);
        var entry = Assert.Single(fromJournal.Entries);
        Assert.Equal("提现", entry.Summary);
    }

    // —— 新建校验 ——

    [Fact]
    public async Task Create_SameJournalForBothSides_ThrowsJournalException()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(from: _cashJournal.Id, to: _cashJournal.Id)));

        Assert.Contains("转出与转入账户不能相同", ex.Message);
    }

    [Fact]
    public async Task Create_ZeroAmount_ThrowsJournalException()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(amount: 0m)));

        Assert.Contains("转账金额必须大于 0", ex.Message);
    }

    [Fact]
    public async Task Create_SummaryOver200Chars_ThrowsJournalException()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(summary: new string('摘', 201))));

        Assert.Contains("摘要不能超过 200 个字符", ex.Message);
    }

    [Fact]
    public async Task Create_UnknownAccountSet_ThrowsJournalException()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(Guid.NewGuid(), Req()));

        Assert.Contains("指定的账套不存在", ex.Message);
    }

    [Fact]
    public async Task Create_MissingJournals_ThrowsJournalException()
    {
        var exFrom = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(to: Guid.NewGuid())));
        Assert.Contains("转入账户不存在", exFrom.Message);

        var exTo = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(from: Guid.NewGuid())));
        Assert.Contains("转出账户不存在", exTo.Message);
    }

    [Fact]
    public async Task Create_FromJournalDisabled_ThrowsAndRegistersNoEntries()
    {
        // 规则：停用只拦新流水——转账的两端任一处停用都不允许登记。
        _cashJournal.SetEnabled(false);
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req()));

        Assert.Equal("账户「现金日记账」已停用，不能用于转账。", ex.Message);
        _db.DetachAll();
        Assert.Empty(await _db.Context.Transfers.Where(t => t.AccountSetId == _accountSet.Id).ToListAsync());
        Assert.Empty((await ReloadJournalAsync(_cashJournal.Id)).Entries);
        Assert.Empty((await ReloadJournalAsync(_bankJournal.Id)).Entries);
    }

    // —— 修改（重建两端流水；已生成凭证不可改）——

    [Fact]
    public async Task Update_WithoutVoucher_RebuildsBothEntriesWithNewAmount()
    {
        var transfer = await SeedTransferAsync(1000m);
        var oldFromEntryId = transfer.FromEntryId;

        var dto = await _service.UpdateAsync(_accountSet.Id, transfer.Id, Req(2500m, summary: "存现"));
        _db.DetachAll();

        Assert.Equal(2500m, dto!.Amount);
        Assert.Equal("存现", dto.Summary);

        var fromJournal = await ReloadJournalAsync(_cashJournal.Id);
        var toJournal = await ReloadJournalAsync(_bankJournal.Id);

        // 旧流水被删除、新流水重建：每端仍只有一笔，且金额与往来口径不变。
        var outEntry = Assert.Single(fromJournal.Entries);
        Assert.NotEqual(oldFromEntryId, outEntry.Id);
        Assert.Equal(2500m, outEntry.Expense);
        Assert.Equal("存现", outEntry.Summary);
        Assert.Equal("内部转账", outEntry.Category);

        var inEntry = Assert.Single(toJournal.Entries);
        Assert.Equal(2500m, inEntry.Income);
        Assert.Equal("现金日记账", inEntry.Counterparty);

        var reloaded = await ReloadTransferAsync(transfer.Id);
        Assert.Equal(outEntry.Id, reloaded!.FromEntryId);
        Assert.Equal(inEntry.Id, reloaded.ToEntryId);
    }

    [Fact]
    public async Task Update_SwitchesToJournal_OldJournalLosesEntry()
    {
        var transfer = await SeedTransferAsync();
        var newTo = AddJournal(JournalType.BankDeposit, "建行存款日记账", _bankAccount.Id);

        await _service.UpdateAsync(_accountSet.Id, transfer.Id, Req(to: newTo.Id));
        _db.DetachAll();

        Assert.Empty((await ReloadJournalAsync(_bankJournal.Id)).Entries);
        var movedEntry = Assert.Single((await ReloadJournalAsync(newTo.Id)).Entries);
        Assert.Equal(1000m, movedEntry.Income);
        Assert.Equal("现金日记账", movedEntry.Counterparty);
        // 转出端流水不受影响。
        Assert.Single((await ReloadJournalAsync(_cashJournal.Id)).Entries);
    }

    [Fact]
    public async Task Update_ToDisabledJournal_ThrowsAndKeepsOriginalEntries()
    {
        // 停用的新目标账户应在改动旧流水之前被拦截，不能留下删旧未建的半成品。
        var transfer = await SeedTransferAsync();
        await DisableJournalAsync(_bankJournal.Id);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.UpdateAsync(_accountSet.Id, transfer.Id, Req(2000m)));

        Assert.Contains("已停用，不能用于转账", ex.Message);
        _db.DetachAll();
        Assert.Equal(1000m, Assert.Single((await ReloadJournalAsync(_cashJournal.Id)).Entries).Expense);
        Assert.Equal(1000m, Assert.Single((await ReloadJournalAsync(_bankJournal.Id)).Entries).Income);
    }

    [Fact]
    public async Task Update_WithVoucher_ThrowsJournalException()
    {
        // 规则：已生成凭证的转账不可改。
        var transfer = await SeedTransferAsync();
        await _service.GenerateVoucherAsync(_accountSet.Id, transfer.Id, "张三", null);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.UpdateAsync(_accountSet.Id, transfer.Id, Req(2000m)));

        Assert.Equal("该转账已生成凭证，不能修改；如需调整请删除凭证后重建转账。", ex.Message);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNull()
    {
        Assert.Null(await _service.UpdateAsync(_accountSet.Id, Guid.NewGuid(), Req()));
    }

    [Fact]
    public async Task Update_ZeroAmount_ThrowsAndEntriesUntouched()
    {
        var transfer = await SeedTransferAsync();

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.UpdateAsync(_accountSet.Id, transfer.Id, Req(0m)));

        Assert.Contains("转账金额必须大于 0", ex.Message);
        _db.DetachAll();
        Assert.Equal(1000m, Assert.Single((await ReloadJournalAsync(_cashJournal.Id)).Entries).Expense);
    }

    // —— 删除（含两端流水；已生成凭证不可删）——

    [Fact]
    public async Task Delete_RemovesTransferAndBothJournalEntries()
    {
        var transfer = await SeedTransferAsync();

        Assert.True(await _service.DeleteAsync(_accountSet.Id, transfer.Id));
        _db.DetachAll();

        Assert.Null(await ReloadTransferAsync(transfer.Id));
        Assert.Empty((await ReloadJournalAsync(_cashJournal.Id)).Entries);
        Assert.Empty((await ReloadJournalAsync(_bankJournal.Id)).Entries);
    }

    [Fact]
    public async Task Delete_WithVoucher_ThrowsJournalException()
    {
        // 规则：已生成凭证的转账不可删。
        var transfer = await SeedTransferAsync();
        await _service.GenerateVoucherAsync(_accountSet.Id, transfer.Id, "张三", null);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.DeleteAsync(_accountSet.Id, transfer.Id));

        Assert.Equal("该转账已生成凭证，不能删除；如需删除请先在凭证管理中处理对应凭证。", ex.Message);
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsFalse()
    {
        Assert.False(await _service.DeleteAsync(_accountSet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteMany_SkipsTransfersWithVoucher()
    {
        var free = await SeedTransferAsync();
        var posted = await SeedTransferAsync(500m);
        await _service.GenerateVoucherAsync(_accountSet.Id, posted.Id, "张三", null);
        _db.DetachAll();

        var removed = await _service.DeleteManyAsync(
            _accountSet.Id, [free.Id, posted.Id, Guid.NewGuid()]);

        Assert.Equal(1, removed);
        _db.DetachAll();
        Assert.Null(await ReloadTransferAsync(free.Id));
        Assert.NotNull(await ReloadTransferAsync(posted.Id));
        // 两笔转账共用现金日记账：free 的流水被清掉，posted 的流水保留。
        var surviving = Assert.Single((await ReloadJournalAsync(_cashJournal.Id)).Entries);
        Assert.Equal(500m, surviving.Expense);
        Assert.Equal(posted.FromEntryId, surviving.Id);
    }

    // —— 生成凭证（借 转入科目 / 贷 转出科目，回写转账与两端流水）——

    [Fact]
    public async Task GenerateVoucher_DebitsToAccountCreditsFromAccountAndBackfillsEntries()
    {
        var transfer = await SeedTransferAsync(1000m);

        var voucherId = await _service.GenerateVoucherAsync(_accountSet.Id, transfer.Id, "张三", null);
        _db.DetachAll();

        var voucher = await ReloadVoucherAsync(voucherId);
        Assert.Equal("记", voucher.Word);
        Assert.Equal(1, voucher.Number);
        Assert.Equal(TransferDate, voucher.Date);
        Assert.Equal("张三", voucher.Preparer);
        Assert.Equal(2, voucher.Entries.Count);
        // 借 转入科目 / 贷 转出科目。
        Assert.Contains(voucher.Entries, e => e.AccountId == _bankAccount.Id && e.Debit == 1000m && e.Credit == 0m);
        Assert.Contains(voucher.Entries, e => e.AccountId == _cashAccount.Id && e.Debit == 0m && e.Credit == 1000m);

        var reloaded = await ReloadTransferAsync(transfer.Id);
        Assert.Equal(voucherId, reloaded!.VoucherId);

        var fromEntry = Assert.Single((await ReloadJournalAsync(_cashJournal.Id)).Entries);
        var toEntry = Assert.Single((await ReloadJournalAsync(_bankJournal.Id)).Entries);
        Assert.Equal(voucherId, fromEntry.VoucherId);
        Assert.Equal(voucherId, toEntry.VoucherId);

        var dto = Assert.Single(await _service.ListAsync(_accountSet.Id));
        Assert.Equal("记-1", dto.VoucherNo);
    }

    [Fact]
    public async Task GenerateVoucher_WithCustomWord_UsesProvidedWord()
    {
        var transfer = await SeedTransferAsync();

        await _service.GenerateVoucherAsync(_accountSet.Id, transfer.Id, "张三", " 银付 ");
        _db.DetachAll();

        var vouchers = await _db.Context.Vouchers
            .Where(v => v.AccountSetId == _accountSet.Id)
            .ToListAsync();
        var voucher = Assert.Single(vouchers);
        Assert.Equal("银付", voucher.Word);
        Assert.Equal(1, voucher.Number);
    }

    [Fact]
    public async Task GenerateVoucher_NonExistentTransfer_ThrowsJournalException()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, Guid.NewGuid(), "张三", null));

        Assert.Contains("指定的内部转账不存在", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucher_Twice_ThrowsAndKeepsOriginalVoucher()
    {
        var transfer = await SeedTransferAsync();
        var first = await _service.GenerateVoucherAsync(_accountSet.Id, transfer.Id, "张三", null);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, transfer.Id, "张三", null));

        Assert.Contains("已生成凭证，请勿重复生成", ex.Message);
        Assert.Equal(1, await _db.Context.Vouchers.CountAsync(v => v.AccountSetId == _accountSet.Id));
        Assert.Equal(first, (await ReloadTransferAsync(transfer.Id))!.VoucherId);
    }

    [Fact]
    public async Task GenerateVoucher_MissingToAccount_ThrowsAndLeavesNoHalfVoucher()
    {
        // 日记账对应科目被删（引用悬空）时整单拒绝，不落半张凭证、不回填关联。
        var ghostJournal = AddJournal(JournalType.BankDeposit, "幽灵账户", Guid.NewGuid());
        var dto = await _service.CreateAsync(_accountSet.Id, Req(to: ghostJournal.Id));
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, dto.Id, "张三", null));

        Assert.Equal("转入账户对应的科目不存在。", ex.Message);
        Assert.Equal(0, await _db.Context.Vouchers.CountAsync(v => v.AccountSetId == _accountSet.Id));
        Assert.Null((await ReloadTransferAsync(dto.Id))!.VoucherId);
    }

    // —— 批量出票（每笔转账独立一张；已生成凭证跳过；单条失败不阻断整批）——

    [Fact]
    public async Task GenerateVouchers_Batch_OneVoucherPerTransfer_SkipsPostedAndFailing()
    {
        var t1 = await SeedTransferAsync(1000m);
        var t2 = await SeedTransferAsync(2000m);
        var ghostJournal = AddJournal(JournalType.BankDeposit, "幽灵账户", Guid.NewGuid());
        var ghost = await _service.CreateAsync(_accountSet.Id, Req(to: ghostJournal.Id));
        _db.DetachAll();
        // t1 先单独出票，模拟「已生成凭证的跳过」。
        await _service.GenerateVoucherAsync(_accountSet.Id, t1.Id, "张三", null);
        _db.DetachAll();

        // ids 含重复项：Distinct 后 t2 只出一张。
        var created = await _service.GenerateVouchersAsync(
            _accountSet.Id, [t1.Id, t2.Id, t2.Id, ghost.Id], "李四", null);

        Assert.Equal(1, created);
        _db.DetachAll();
        Assert.Equal(2, await _db.Context.Vouchers.CountAsync(v => v.AccountSetId == _accountSet.Id));
        Assert.Null((await ReloadTransferAsync(ghost.Id))!.VoucherId);
        Assert.NotNull((await ReloadTransferAsync(t2.Id))!.VoucherId);
    }

    // —— 凭证删除后的引用自愈 ——

    [Fact]
    public async Task Update_AfterVoucherHardDeleted_HealsStaleReferenceAndSucceeds()
    {
        // 凭证被删除后转账残留 VoucherId：修改前自愈清除引用，转账恢复可改。
        var transfer = await SeedTransferAsync();
        var voucherId = await _service.GenerateVoucherAsync(_accountSet.Id, transfer.Id, "张三", null);
        _db.DetachAll();
        _db.Context.Vouchers.Remove(await _db.Context.Vouchers.SingleAsync(v => v.Id == voucherId));
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var dto = await _service.UpdateAsync(_accountSet.Id, transfer.Id, Req(2000m));

        Assert.NotNull(dto);
        Assert.Null(dto!.VoucherId);
        Assert.Null(dto.VoucherNo);
    }

    [Fact]
    public async Task ListAsync_HidesVoucherNoOfSoftDeletedVoucher()
    {
        // 规则：只认仍在册的凭证——回收站中的凭证不算有效关联，列表不显示其凭证号。
        var transfer = await SeedTransferAsync();
        var voucherId = await _service.GenerateVoucherAsync(_accountSet.Id, transfer.Id, "张三", null);
        _db.DetachAll();

        var voucher = await _db.Context.Vouchers.SingleAsync(v => v.Id == voucherId);
        voucher.SoftDelete(DateTimeOffset.UtcNow);
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var dto = Assert.Single(await _service.ListAsync(_accountSet.Id));
        Assert.Null(dto.VoucherNo);
    }

    [Fact]
    public async Task ListAsync_OrdersByDate()
    {
        await _service.CreateAsync(_accountSet.Id, new SaveTransferRequest
        {
            Date = new DateOnly(2026, 3, 20),
            FromJournalId = _cashJournal.Id,
            ToJournalId = _bankJournal.Id,
            Amount = 200m,
            Summary = "晚一笔",
        });
        await _service.CreateAsync(_accountSet.Id, Req(100m, summary: "早一笔"));
        _db.DetachAll();

        var list = await _service.ListAsync(_accountSet.Id);

        Assert.Equal(2, list.Count);
        Assert.Equal("早一笔", list[0].Summary);
        Assert.Equal("晚一笔", list[1].Summary);
        Assert.Equal(new DateOnly(2026, 3, 10), list[0].Date);
    }
}
