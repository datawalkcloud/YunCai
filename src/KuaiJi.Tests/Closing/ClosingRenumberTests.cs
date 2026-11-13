using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Accounting.Infrastructure;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.AccountSets.Infrastructure;
using KuaiJi.Modules.Closing.Application;
using KuaiJi.Modules.Closing.Infrastructure;
using KuaiJi.Modules.FixedAssets.Infrastructure;
using KuaiJi.Modules.Funds.Infrastructure;
using KuaiJi.Modules.Salary.Infrastructure;
using KuaiJi.Modules.Vouchers.Infrastructure;
using KuaiJi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KuaiJi.Tests.Closing;

/// <summary>
/// 期末「一键整理断号」：凭证号在账套内按凭证字连续分配，整理本期断号时
/// 从本期之前的最后一个号码起把本期及其后各期一并顺延，已结账期间不可整理。
/// </summary>
public sealed class ClosingRenumberTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _cash;
    private readonly Account _counter;
    private readonly ClosingService _service;

    public ClosingRenumberTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _cash = _db.AddAccount(_accountSet, "1001", "库存现金");
        _counter = _db.AddAccount(_accountSet, "6602", "管理费用");

        _service = new ClosingService(
            new AccountSetRepository(_db.Context),
            new AccountRepository(_db.Context),
            new VoucherRepository(_db.Context),
            new ClosingTemplateRepository(_db.Context),
            new SalaryRepository(_db.Context),
            new JournalRepository(_db.Context),
            new FixedAssetRepository(_db.Context));
    }

    public void Dispose() => _db.Dispose();

    private void SeedVoucher(int number, int year, int month) =>
        _db.AddVoucher(_accountSet.Id, "记", number, new DateOnly(year, month, 28), _cash.Id, _counter.Id);

    private async Task<List<int>> NumbersOf(int year, int month) =>
        await _db.Context.Vouchers
            .Where(v => v.AccountSetId == _accountSet.Id && v.Date.Year == year && v.Date.Month == month)
            .OrderBy(v => v.Number)
            .Select(v => v.Number)
            .ToListAsync();

    [Fact]
    public async Task RenumberGaps_FillsGapAndKeepsEarlierNumbers()
    {
        for (var n = 1; n <= 8; n++)
        {
            SeedVoucher(n, 2026, 10);
        }

        // 本期 10 号被删除，留下断号。
        SeedVoucher(9, 2026, 11);
        SeedVoucher(11, 2026, 11);
        SeedVoucher(12, 2026, 11);

        var result = await _service.RenumberGapsAsync(_accountSet.Id, 2026, 11);

        Assert.Equal(2, result.Changed);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], await NumbersOf(2026, 10));
        Assert.Equal([9, 10, 11], await NumbersOf(2026, 11));
    }

    [Fact]
    public async Task RenumberGaps_ShiftsFollowingMonthsToAvoidDuplicates()
    {
        for (var n = 1; n <= 8; n++)
        {
            SeedVoucher(n, 2026, 10);
        }

        SeedVoucher(9, 2026, 11);
        SeedVoucher(11, 2026, 11);
        SeedVoucher(12, 2026, 11);
        SeedVoucher(13, 2026, 12);
        SeedVoucher(14, 2026, 12);

        var result = await _service.RenumberGapsAsync(_accountSet.Id, 2026, 11);

        Assert.Equal(4, result.Changed);
        Assert.Equal([9, 10, 11], await NumbersOf(2026, 11));
        Assert.Equal([12, 13], await NumbersOf(2026, 12));
    }

    [Fact]
    public async Task RenumberGaps_NoGap_ChangesNothing()
    {
        SeedVoucher(1, 2026, 11);
        SeedVoucher(2, 2026, 11);

        var result = await _service.RenumberGapsAsync(_accountSet.Id, 2026, 11);

        Assert.Equal(0, result.Changed);
        Assert.Contains("无需整理", result.Message);
        Assert.Equal([1, 2], await NumbersOf(2026, 11));
    }

    [Fact]
    public async Task RenumberGaps_ClosedPeriod_Throws()
    {
        SeedVoucher(1, 2026, 11);
        SeedVoucher(3, 2026, 11);
        for (var m = 1; m <= 11; m++)
        {
            _accountSet.MarkCarried(2026, m);
            _accountSet.ClosePeriod(2026, m);
        }

        await _db.Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ClosingException>(
            () => _service.RenumberGapsAsync(_accountSet.Id, 2026, 11));

        Assert.Contains("已结账", ex.Message);
        Assert.Equal([1, 3], await NumbersOf(2026, 11));
    }
}
