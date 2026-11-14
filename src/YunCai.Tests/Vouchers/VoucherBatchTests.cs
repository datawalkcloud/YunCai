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
/// 凭证批量能力：合并（同字同日、仅暂存）与批量修改（摘要覆盖、仅暂存、已结期间拦截）。
/// </summary>
public sealed class VoucherBatchTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _cash;
    private readonly Account _counter;
    private readonly VoucherService _service;
    private readonly VoucherRepository _vouchers;

    public VoucherBatchTests()
    {
        _accountSet = _db.AddAccountSet("批量测试账套");
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

    private Task<VoucherDetailDto> CreatePostedAsync(string summary = "测试", DateOnly? date = null)
    {
        var request = new CreateVoucherRequest
        {
            Word = "记",
            Date = date ?? new DateOnly(2026, 2, 15),
            Entries =
            [
                new CreateVoucherEntryRequest { Summary = summary, AccountId = _cash.Id, Debit = 100m },
                new CreateVoucherEntryRequest { Summary = summary, AccountId = _counter.Id, Credit = 100m },
            ],
        };
        return _service.CreateAsync(_accountSet.Id, "制单人", request);
    }

    private async Task<VoucherDetailDto> CreateDraftAsync(string summary = "测试", DateOnly? date = null)
    {
        var posted = await CreatePostedAsync(summary, date);
        return await _service.UnpostAsync(_accountSet.Id, posted.Id) ?? throw new InvalidOperationException("反过账失败");
    }

    [Fact]
    public async Task Merge_TwoDrafts_AppendsEntriesAndRemovesSources()
    {
        var first = await CreateDraftAsync("第一张");
        var second = await CreateDraftAsync("第二张");

        var merged = await _service.MergeAsync(_accountSet.Id, new MergeVouchersRequest
        {
            Ids = [first.Id, second.Id],
        });

        Assert.Equal(first.Id, merged.Id);
        Assert.Equal(4, merged.Entries.Count);
        Assert.Equal($"{first.Word}-{first.Number}", merged.VoucherNo);
        Assert.Equal((int)VoucherStatus.Draft, merged.Status);

        _db.DetachAll();
        var remaining = await _vouchers.GetAllAsync(_accountSet.Id);
        var survivor = Assert.Single(remaining);
        Assert.Equal(first.Id, survivor.Id);
        Assert.Equal(4, survivor.Entries.Count);
        Assert.Equal(2, survivor.Entries.Count(e => e.Summary == "第一张"));
        Assert.Equal(2, survivor.Entries.Count(e => e.Summary == "第二张"));
    }

    [Fact]
    public async Task Merge_RejectsPostedVoucher()
    {
        var draft = await CreateDraftAsync();
        var posted = await CreatePostedAsync();

        var ex = await Assert.ThrowsAsync<VoucherException>(() => _service.MergeAsync(_accountSet.Id,
            new MergeVouchersRequest { Ids = [draft.Id, posted.Id] }));

        Assert.Contains("只能合并暂存凭证", ex.Message);
    }

    [Fact]
    public async Task Merge_RejectsDifferentDates()
    {
        var first = await CreateDraftAsync(date: new DateOnly(2026, 2, 15));
        var second = await CreateDraftAsync(date: new DateOnly(2026, 2, 16));

        var ex = await Assert.ThrowsAsync<VoucherException>(() => _service.MergeAsync(_accountSet.Id,
            new MergeVouchersRequest { Ids = [first.Id, second.Id] }));

        Assert.Contains("同一凭证字、同一日期", ex.Message);
    }

    [Fact]
    public async Task BatchUpdate_ReplacesSummaryOnAllEntries()
    {
        var first = await CreateDraftAsync("旧摘要");
        var second = await CreateDraftAsync("旧摘要");

        var updated = await _service.BatchUpdateAsync(_accountSet.Id, new BatchUpdateVouchersRequest
        {
            Ids = [first.Id, second.Id],
            Summary = "统一摘要",
        });

        Assert.Equal(2, updated);

        _db.DetachAll();
        foreach (var voucher in await _vouchers.GetAllAsync(_accountSet.Id))
        {
            Assert.All(voucher.Entries, entry => Assert.Equal("统一摘要", entry.Summary));
            // 批量改摘要不触发入账，暂存状态保持不变。
            Assert.Equal(VoucherStatus.Draft, voucher.Status);
        }
    }

    [Fact]
    public async Task BatchUpdate_RejectsPostedVoucher()
    {
        var posted = await CreatePostedAsync();

        var ex = await Assert.ThrowsAsync<VoucherException>(() => _service.BatchUpdateAsync(_accountSet.Id,
            new BatchUpdateVouchersRequest { Ids = [posted.Id], Summary = "改摘要" }));

        Assert.Contains("只能批量修改暂存凭证", ex.Message);
    }

    [Fact]
    public async Task BatchUpdate_RequiresAtLeastOneField()
    {
        var draft = await CreateDraftAsync();

        var ex = await Assert.ThrowsAsync<VoucherException>(() => _service.BatchUpdateAsync(_accountSet.Id,
            new BatchUpdateVouchersRequest { Ids = [draft.Id] }));

        Assert.Contains("至少选择一项", ex.Message);
    }
}
