using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Assist.Infrastructure;
using YunCai.Modules.Vouchers.Application;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Vouchers;

/// <summary>
/// 凭证生命周期测试（编号规则之外的行为，编号相关见 VoucherNumberingTests）：
/// - 制单校验：无分录、借贷不平衡、凭证字不在账套列表、科目无效/非末级/停用、账套不存在、已结账期间；
/// - 状态流转：保存即过账、反过账退回草稿，以及非法流转与已结期间拦截；
/// - 编辑与删除：正常须先反过账才可改、正常/已结期间不可删、回收站软删除与还原/彻底删除；
/// - 复制与红冲：新号凭证直接入账、金额取反与「冲销yyyymm字-号_」摘要前缀；
/// - 凭证号重排：断号续接、范围内存在正常凭证时拒绝。
/// </summary>
public sealed class VoucherLifecycleTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _cash;
    private readonly Account _counter;
    private readonly VoucherRepository _vouchers;
    private readonly VoucherService _service;

    public VoucherLifecycleTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _cash = _db.AddAccount(_accountSet, "1001", "库存现金");
        _counter = _db.AddAccount(_accountSet, "6602", "管理费用");

        _service = new VoucherService(
            new AccountSetRepository(_db.Context),
            new AccountRepository(_db.Context),
            new AssistItemRepository(_db.Context),
            new VoucherRepository(_db.Context),
            new AccountAssistGuard());
        _vouchers = new VoucherRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// 把指定账套结账至某期间。结转与结账都必须「逐月按顺序推进」（见 AGENTS.md 期末结转条目：
    /// 结账要求该期间已完成结转损益，且 MarkCarried / ClosePeriod 都只认下一个待处理期间）。
    /// 因此这里从启用期间起先把损益逐月结转至目标期间，再逐月结账至目标期间——完整复刻生产推进路径，
    /// 而不是跳月直改指针（账套启用期间为播种时的当前年 1 月，直接结 2026-02 会撞「只能按顺序结转」）。
    /// </summary>
    private void ClosePeriod(int year, int month)
    {
        var target = year * 100 + month;

        // 第一段：从启用期间逐月标记结转损益，直到目标期间。
        for (var (y, m) = (_accountSet.StartYear, _accountSet.StartMonth); y * 100 + m <= target; (y, m) = NextMonth(y, m))
        {
            _accountSet.MarkCarried(y, m);
        }

        // 第二段：结转已覆盖目标期间后，再从启用期间逐月结账至目标期间。
        for (var (y, m) = (_accountSet.StartYear, _accountSet.StartMonth); y * 100 + m <= target; (y, m) = NextMonth(y, m))
        {
            _accountSet.ClosePeriod(y, m);
        }

        _db.Context.SaveChanges();
    }

    /// <summary>次月：12 月翻到次年 1 月，用于期间逐月推进。</summary>
    private static (int Year, int Month) NextMonth(int year, int month) => month == 12 ? (year + 1, 1) : (year, month + 1);

    /// <summary>绕过应用服务直接落一张凭证，用于构造服务层造不出来的形态（不平衡、无分录等）。</summary>
    private Voucher SeedRaw(string word, int number, params (decimal Debit, decimal Credit)[] amounts)
        => SeedRaw(word, number, new DateOnly(2026, 2, 15), amounts);

    private Voucher SeedRaw(string word, int number, DateOnly date, params (decimal Debit, decimal Credit)[] amounts)
    {
        var voucher = Voucher.Create(_accountSet.Id, word, number, date, "制单人");
        foreach (var (debit, credit) in amounts)
        {
            voucher.AddEntry("种子分录", debit != 0m ? _cash.Id : _counter.Id, debit, credit);
        }

        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();
        return voucher;
    }

    private CreateVoucherRequest NewRequest(
        string word = "记",
        int number = 0,
        int year = 2026,
        int month = 2,
        Guid? debitAccount = null,
        decimal debit = 100m,
        decimal credit = 100m,
        IReadOnlyList<CreateVoucherEntryRequest>? entries = null) => new()
    {
        Word = word,
        Number = number,
        Date = new DateOnly(year, month, 15),
        Entries = entries ??
        [
            new CreateVoucherEntryRequest { Summary = "测试", AccountId = debitAccount ?? _cash.Id, Debit = debit },
            new CreateVoucherEntryRequest { Summary = "测试", AccountId = _counter.Id, Credit = credit },
        ],
    };

    /// <summary>经服务创建一张平衡凭证（保存即过账，记-1）。</summary>
    private async Task<VoucherDetailDto> CreatePostedAsync()
    {
        return await _service.CreateAsync(_accountSet.Id, "制单人", NewRequest());
    }

    /// <summary>经服务创建后反过账退回草稿（记-1），用于验证编辑/删除等仅草稿可用的路径。</summary>
    private async Task<VoucherDetailDto> CreateDraftAsync()
    {
        var created = await CreatePostedAsync();
        return await _service.UnpostAsync(_accountSet.Id, created.Id) ?? throw new InvalidOperationException("反过账失败");
    }

    // —— 制单校验 ——

    [Fact]
    public async Task Create_WithoutEntries_Throws()
    {
        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(entries: [])));

        Assert.Equal("凭证至少需要一条分录。", ex.Message);
    }

    [Fact]
    public async Task Create_BalancedVoucher_IsPostedImmediately()
    {
        var created = await _service.CreateAsync(_accountSet.Id, "制单人", NewRequest());

        // 保存即入账：制单不再经过审核，落库即为正常状态。
        Assert.Equal((int)VoucherStatus.Posted, created.Status);
        Assert.Equal("正常", created.StatusText);
    }

    [Fact]
    public async Task Create_WithUnbalancedEntries_Throws()
    {
        // 保存即入账，借贷平衡成为保存的硬性条件（Voucher.EnsurePostable 由 Post() 调用）。
        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(debit: 100m, credit: 60m)));

        Assert.Contains("借贷不平衡", ex.Message);
    }

    [Fact]
    public async Task Create_WithUnknownWord_Throws()
    {
        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(word: "银")));

        Assert.Contains("凭证字「银」", ex.Message);
        Assert.Contains("不在账套凭证字列表", ex.Message);
    }

    [Fact]
    public async Task Create_WithUnknownAccount_Throws()
    {
        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(debitAccount: Guid.NewGuid())));

        Assert.Equal("存在无效的会计科目，请重新选择。", ex.Message);
    }

    [Fact]
    public async Task Create_WithNonLeafAccount_Throws()
    {
        var parent = _db.AddAccount(_accountSet, "1403", "原材料");
        parent.AddChild("140301", "甲材料");
        _db.Context.SaveChanges();

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(debitAccount: parent.Id)));

        Assert.Contains("1403 原材料", ex.Message);
        Assert.Contains("不是末级科目", ex.Message);
    }

    [Fact]
    public async Task Create_WithDisabledAccount_Throws()
    {
        var disabled = _db.AddAccount(_accountSet, "6603", "销售费用");
        disabled.Disable();
        _db.Context.SaveChanges();

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(debitAccount: disabled.Id)));

        Assert.Contains("6603 销售费用", ex.Message);
        Assert.Contains("已停用", ex.Message);
    }

    [Fact]
    public async Task Create_WithUnknownAccountSet_Throws()
    {
        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CreateAsync(Guid.NewGuid(), "制单人", NewRequest()));

        Assert.Equal("指定的账套不存在。", ex.Message);
    }

    [Fact]
    public async Task Create_InClosedPeriod_Throws()
    {
        ClosePeriod(2026, 1);

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(year: 2026, month: 1)));

        Assert.Contains("2026-01 期间已结账", ex.Message);
    }

    [Fact]
    public async Task Create_WithBlankWord_FallsBackToDefaultWord()
    {
        var created = await _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(word: "  "));

        Assert.Equal("记", created.Word);
    }

    // —— 编辑 ——

    [Fact]
    public async Task Update_DraftVoucher_ReplacesHeaderAndEntries()
    {
        var created = await CreateDraftAsync();

        var updated = await _service.UpdateAsync(_accountSet.Id, created.Id, new UpdateVoucherRequest
        {
            Date = new DateOnly(2026, 3, 1),
            AttachmentCount = 2,
            Remark = "改过",
            Entries =
            [
                new CreateVoucherEntryRequest { Summary = "改后分录", AccountId = _cash.Id, Debit = 200m },
                new CreateVoucherEntryRequest { Summary = "改后分录", AccountId = _counter.Id, Credit = 200m },
            ],
        });

        Assert.NotNull(updated);
        Assert.Equal(new DateOnly(2026, 3, 1), updated.Date);
        Assert.Equal(2, updated.AttachmentCount);
        Assert.Equal("改过", updated.Remark);
        Assert.Equal(200m, updated.TotalDebit);
        Assert.Equal(2, updated.Entries.Count);
        Assert.Equal("改后分录", updated.Entries[0].Summary);
        // 凭证字与号保持不变
        Assert.Equal("记", updated.Word);
        Assert.Equal(1, updated.Number);
    }

    [Fact]
    public async Task Update_CanShrinkEntries()
    {
        var created = await CreateDraftAsync();

        var updated = await _service.UpdateAsync(_accountSet.Id, created.Id, new UpdateVoucherRequest
        {
            Date = new DateOnly(2026, 2, 15),
            Entries =
            [
                new CreateVoucherEntryRequest { Summary = "单分录", AccountId = _cash.Id, Debit = 100m },
                new CreateVoucherEntryRequest { Summary = "单分录", AccountId = _counter.Id, Credit = 100m },
            ],
        });

        // 保存一次即重新过账，二次编辑前必须先反过账退回草稿。
        await _service.UnpostAsync(_accountSet.Id, created.Id);

        // 请求两条与原有两条一致即可整体替换；收缩场景再单独验证
        var shrunk = await _service.UpdateAsync(_accountSet.Id, created.Id, new UpdateVoucherRequest
        {
            Date = new DateOnly(2026, 2, 15),
            Entries =
            [
                new CreateVoucherEntryRequest { Summary = "唯一分录", AccountId = _cash.Id, Debit = 50m },
                new CreateVoucherEntryRequest { Summary = "唯一分录", AccountId = _counter.Id, Credit = 50m },
            ],
        });

        Assert.NotNull(shrunk);
        Assert.Equal(2, shrunk.Entries.Count);
        Assert.Equal(50m, shrunk.TotalDebit);
    }

    [Fact]
    public async Task Update_MissingVoucher_ReturnsNull()
    {
        var updated = await _service.UpdateAsync(_accountSet.Id, Guid.NewGuid(), new UpdateVoucherRequest
        {
            Date = new DateOnly(2026, 2, 15),
            Entries = NewRequest().Entries,
        });

        Assert.Null(updated);
    }

    [Fact]
    public async Task Update_WithoutEntries_Throws()
    {
        var created = await CreateDraftAsync();

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.UpdateAsync(_accountSet.Id, created.Id, new UpdateVoucherRequest
            {
                Date = new DateOnly(2026, 2, 15),
                Entries = [],
            }));

        Assert.Equal("凭证至少需要一条分录。", ex.Message);
    }

    [Fact]
    public async Task Update_PostedVoucher_Throws()
    {
        var posted = await CreatePostedAsync();

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.UpdateAsync(_accountSet.Id, posted.Id, new UpdateVoucherRequest
            {
                Date = new DateOnly(2026, 2, 15),
                Entries = NewRequest().Entries,
            }));

        Assert.Contains("请先反过账", ex.Message);
    }

    // —— 删除与回收站 ——

    [Fact]
    public async Task Delete_PostedVoucher_Throws()
    {
        var posted = await CreatePostedAsync();

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.DeleteAsync(_accountSet.Id, posted.Id));

        Assert.Contains("记-1", ex.Message);
        Assert.Contains("已是正常状态，不能删除", ex.Message);
    }

    [Fact]
    public async Task Delete_VoucherInClosedPeriod_Throws()
    {
        ClosePeriod(2026, 1);
        var seeded = _db.AddVoucher(_accountSet.Id, "记", 1, new DateOnly(2026, 1, 28), _cash.Id, _counter.Id);

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.DeleteAsync(_accountSet.Id, seeded.Id));

        Assert.Contains("所属期间已结账，不能删除", ex.Message);
    }

    [Fact]
    public async Task Delete_DraftVoucher_SoftDeletesIntoRecycleBin()
    {
        var created = await CreateDraftAsync();

        var deleted = await _service.DeleteAsync(_accountSet.Id, created.Id);

        Assert.True(deleted);
        var entity = await _vouchers.GetByIdAsync(_accountSet.Id, created.Id);
        Assert.NotNull(entity);
        Assert.True(entity.IsDeleted);
    }

    [Fact]
    public async Task Delete_MissingVoucher_ReturnsFalse()
    {
        Assert.False(await _service.DeleteAsync(_accountSet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Purge_NotDeletedVoucher_Throws()
    {
        var created = await CreateDraftAsync();

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.PurgeAsync(_accountSet.Id, created.Id));

        Assert.Contains("只有回收站中的凭证才能彻底删除", ex.Message);
    }

    [Fact]
    public async Task Purge_DeletedVoucher_RemovesPermanently()
    {
        var created = await CreateDraftAsync();
        await _service.DeleteAsync(_accountSet.Id, created.Id);

        Assert.True(await _service.PurgeAsync(_accountSet.Id, created.Id));
        Assert.Null(await _vouchers.GetByIdAsync(_accountSet.Id, created.Id));
    }

    [Fact]
    public async Task Restore_RestoresSoftDeletedVoucher()
    {
        var created = await CreateDraftAsync();
        await _service.DeleteAsync(_accountSet.Id, created.Id);

        var restored = await _service.RestoreAsync(_accountSet.Id, created.Id);

        Assert.NotNull(restored);
        Assert.False((await _vouchers.GetByIdAsync(_accountSet.Id, created.Id))!.IsDeleted);
    }

    [Fact]
    public async Task Restore_MissingVoucher_ReturnsNull()
    {
        Assert.Null(await _service.RestoreAsync(_accountSet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task ClearRecycleBin_RemovesOnlyDeletedVouchers()
    {
        // 只有暂存能进回收站（正常须先反过账），所以这里直接落三张暂存。
        var deleted1 = SeedRaw("记", 1, (100m, 0m), (0m, 100m));
        var deleted2 = SeedRaw("记", 2, (100m, 0m), (0m, 100m));
        var kept = SeedRaw("记", 3, (100m, 0m), (0m, 100m));

        // 生产每个 HTTP 请求各用一个新 DbContext；这里用 DetachAll 还原请求边界，
        // 否则制单时 Add 进来的实例会与清回收站时 Remove 的同主键实例撞车
        // （ClearRecycleBinAsync 走 GetAllAsync 的 AsNoTracking，Remove 会试图 attach 已在跟踪器中的实例）。
        _db.DetachAll();
        await _service.DeleteAsync(_accountSet.Id, deleted1.Id);
        await _service.DeleteAsync(_accountSet.Id, deleted2.Id);

        _db.DetachAll();
        var count = await _service.ClearRecycleBinAsync(_accountSet.Id);

        Assert.Equal(2, count);
        Assert.Null(await _vouchers.GetByIdAsync(_accountSet.Id, deleted1.Id));
        Assert.Null(await _vouchers.GetByIdAsync(_accountSet.Id, deleted2.Id));
        Assert.NotNull(await _vouchers.GetByIdAsync(_accountSet.Id, kept.Id));
    }

    // —— 反过账 ——

    [Fact]
    public async Task Unpost_PostedVoucher_ReturnsToDraft()
    {
        var posted = await CreatePostedAsync();

        var unposted = await _service.UnpostAsync(_accountSet.Id, posted.Id);

        Assert.NotNull(unposted);
        Assert.Equal((int)VoucherStatus.Draft, unposted.Status);
        Assert.Equal("暂存", unposted.StatusText);
    }

    [Fact]
    public async Task Unpost_MissingVoucher_ReturnsNull()
    {
        Assert.Null(await _service.UnpostAsync(_accountSet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Unpost_DraftVoucher_Throws()
    {
        var created = await CreateDraftAsync();

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.UnpostAsync(_accountSet.Id, created.Id));

        Assert.Equal("只有正常凭证才能反过账。", ex.Message);
    }

    [Fact]
    public async Task Unpost_VoucherInClosedPeriod_Throws()
    {
        var posted = await CreatePostedAsync();
        ClosePeriod(2026, 2);

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.UnpostAsync(_accountSet.Id, posted.Id));

        Assert.Contains("所属期间已结账", ex.Message);
    }

    // —— 复制与红冲 ——

    [Fact]
    public async Task Copy_CreatesPostedCopyWithNextNumberAndSameEntries()
    {
        var created = await CreateDraftAsync();

        var copy = await _service.CopyAsync(_accountSet.Id, created.Id, "复制人");

        Assert.NotEqual(created.Id, copy.Id);
        Assert.Equal("记", copy.Word);
        Assert.Equal(2, copy.Number);
        // 平衡的复制件直接入账，不再留下一张没人处理的草稿。
        Assert.Equal((int)VoucherStatus.Posted, copy.Status);
        Assert.Equal("复制人", copy.Preparer);
        Assert.Equal(2, copy.Entries.Count);
        Assert.Equal(100m, copy.Entries[0].Debit);
        // 原凭证不受影响
        var source = await _vouchers.GetByIdAsync(_accountSet.Id, created.Id);
        Assert.Equal(1, source!.Number);
    }

    [Fact]
    public async Task Copy_MissingVoucher_Throws()
    {
        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CopyAsync(_accountSet.Id, Guid.NewGuid(), "复制人"));

        Assert.Equal("凭证不存在。", ex.Message);
    }

    [Fact]
    public async Task Copy_DeletedVoucher_Throws()
    {
        var created = await CreateDraftAsync();
        await _service.DeleteAsync(_accountSet.Id, created.Id);

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CopyAsync(_accountSet.Id, created.Id, "复制人"));

        Assert.Contains("回收站中的凭证不能复制", ex.Message);
    }

    [Fact]
    public async Task Reverse_NegatesAmountsWithRedLetterPrefix()
    {
        var created = await CreateDraftAsync();

        var reversal = await _service.ReverseAsync(_accountSet.Id, created.Id, "制单人");

        Assert.Equal(2, reversal.Number);
        Assert.Equal((int)VoucherStatus.Posted, reversal.Status);
        Assert.Equal(-100m, reversal.Entries[0].Debit);
        Assert.Equal(-100m, reversal.Entries[1].Credit);
        Assert.StartsWith("冲销202602记-1_", reversal.Entries[0].Summary);
        Assert.StartsWith("冲销202602记-1_", reversal.Entries[1].Summary);
    }

    [Fact]
    public async Task Reverse_DeletedVoucher_Throws()
    {
        var created = await CreateDraftAsync();
        await _service.DeleteAsync(_accountSet.Id, created.Id);

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.ReverseAsync(_accountSet.Id, created.Id, "制单人"));

        Assert.Contains("回收站中的凭证不能红冲", ex.Message);
    }

    // —— 凭证号重排 ——

    [Fact]
    public async Task Renumber_ClosesGapsAndReturnsChangedCount()
    {
        SeedRaw("记", 1, (100m, 0m), (0m, 100m));
        SeedRaw("记", 3, (100m, 0m), (0m, 100m));
        SeedRaw("转", 7, (100m, 0m), (0m, 100m));

        var changed = await _service.RenumberAsync(_accountSet.Id, "记", null, null, byDate: false);

        Assert.Equal(1, changed);
        Assert.Equal(2, await _vouchers.GetMaxNumberAsync(_accountSet.Id, "记"));
        // 其他凭证字不受影响
        Assert.Equal(7, await _vouchers.GetMaxNumberAsync(_accountSet.Id, "转"));
    }

    [Fact]
    public async Task Renumber_WithPostedVoucherInScope_Throws()
    {
        await CreatePostedAsync();

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.RenumberAsync(_accountSet.Id, null, null, null, byDate: false));

        Assert.Contains("存在正常凭证", ex.Message);
        Assert.Contains("凭证号不能重排", ex.Message);
    }

    [Fact]
    public async Task Renumber_IgnoresRecycledVouchers()
    {
        var recycled = SeedRaw("记", 5, (100m, 0m), (0m, 100m));
        await _service.DeleteAsync(_accountSet.Id, recycled.Id);
        SeedRaw("记", 1, (100m, 0m), (0m, 100m));

        var changed = await _service.RenumberAsync(_accountSet.Id, "记", null, null, byDate: false);

        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task Renumber_WithPeriodRange_ContinuesAfterEarlierNumbers()
    {
        // 1 月 记-1；3 月 记-7、记-9（跨期整理时只选定 3 月）。
        SeedRaw("记", 1, new DateOnly(2026, 1, 10), (100m, 0m), (0m, 100m));
        SeedRaw("记", 7, new DateOnly(2026, 3, 10), (100m, 0m), (0m, 100m));
        SeedRaw("记", 9, new DateOnly(2026, 3, 20), (100m, 0m), (0m, 100m));

        var changed = await _service.RenumberAsync(
            _accountSet.Id, "记", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), byDate: false);

        // 区间内从区间前的最大号续编（1 → 2、3），不能从 1 重启与 1 月撞号。
        Assert.Equal(2, changed);
        var all = await _vouchers.GetAllAsync(_accountSet.Id);
        Assert.Equal(1, all.Single(v => v.Date == new DateOnly(2026, 1, 10)).Number);
        Assert.Equal(2, all.Single(v => v.Date == new DateOnly(2026, 3, 10)).Number);
        Assert.Equal(3, all.Single(v => v.Date == new DateOnly(2026, 3, 20)).Number);
        Assert.Equal(all.Select(v => (v.Word, v.Number)).Distinct().Count(), all.Count);
    }
}

/// <summary>测试辅助：替换请求中指定位置分录的科目。</summary>
internal static class VoucherRequestTestExtensions
{
    public static CreateVoucherRequest WithEntryAccount(this CreateVoucherRequest request, int index, Guid accountId)
    {
        var entries = request.Entries.ToList();
        entries[index] = new CreateVoucherEntryRequest
        {
            Summary = entries[index].Summary,
            AccountId = accountId,
            Debit = entries[index].Debit,
            Credit = entries[index].Credit,
        };
        return new CreateVoucherRequest
        {
            Word = request.Word,
            Number = request.Number,
            Date = request.Date,
            AttachmentCount = request.AttachmentCount,
            Remark = request.Remark,
            Entries = entries,
        };
    }
}
