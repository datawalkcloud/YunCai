using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Assist.Infrastructure;
using YunCai.Modules.Vouchers.Application;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Vouchers;

/// <summary>
/// 凭证号规则：同一账套内按「凭证字」各自连续编号，判重与续号都以全账套为范围，
/// 不按会计期间分段（前端曾按当期取最大号，导致跨年撞号保存失败）。
/// </summary>
public sealed class VoucherNumberingTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _cash;
    private readonly Account _counter;
    private readonly VoucherRepository _vouchers;
    private readonly VoucherService _service;

    public VoucherNumberingTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _cash = _db.AddAccount(_accountSet, "1001", "库存现金");
        _counter = _db.AddAccount(_accountSet, "6602", "管理费用");

        _vouchers = new VoucherRepository(_db.Context);
        _service = new VoucherService(
            new AccountSetRepository(_db.Context),
            new AccountRepository(_db.Context),
            new AssistItemRepository(_db.Context),
            _vouchers,
            new AccountAssistGuard());
    }

    public void Dispose() => _db.Dispose();

    private void SeedVoucher(string word, int number, int year, int month, Guid? accountSetId = null)
    {
        var set = accountSetId ?? _accountSet.Id;
        _db.AddVoucher(set, word, number, new DateOnly(year, month, 28), _cash.Id, _counter.Id);
    }

    private CreateVoucherRequest NewRequest(string word = "记", int number = 0, int year = 2026, int month = 2) => new()
    {
        Word = word,
        Number = number,
        Date = new DateOnly(year, month, 28),
        Entries =
        [
            new CreateVoucherEntryRequest { Summary = "测试", AccountId = _cash.Id, Debit = 100m },
            new CreateVoucherEntryRequest { Summary = "测试", AccountId = _counter.Id, Credit = 100m },
        ],
    };

    [Fact]
    public async Task GetMaxNumber_EmptyAccountSet_ReturnsZero()
    {
        Assert.Equal(0, await _vouchers.GetMaxNumberAsync(_accountSet.Id, "记"));
    }

    [Fact]
    public async Task GetMaxNumber_SpansPeriods_TakesAccountSetWideMaximum()
    {
        SeedVoucher("记", 1, 2025, 12);
        SeedVoucher("记", 7, 2026, 1);
        SeedVoucher("记", 3, 2026, 2);

        Assert.Equal(7, await _vouchers.GetMaxNumberAsync(_accountSet.Id, "记"));
    }

    [Fact]
    public async Task GetMaxNumber_IsPerVoucherWord()
    {
        SeedVoucher("记", 4, 2026, 1);
        SeedVoucher("转", 9, 2026, 1);

        Assert.Equal(4, await _vouchers.GetMaxNumberAsync(_accountSet.Id, "记"));
        Assert.Equal(9, await _vouchers.GetMaxNumberAsync(_accountSet.Id, "转"));
        Assert.Equal(0, await _vouchers.GetMaxNumberAsync(_accountSet.Id, "收"));
    }

    [Fact]
    public async Task GetMaxNumber_IsPerAccountSet()
    {
        var other = _db.AddAccountSet("另一账套");
        SeedVoucher("记", 42, 2026, 1, other.Id);

        Assert.Equal(0, await _vouchers.GetMaxNumberAsync(_accountSet.Id, "记"));
        Assert.Equal(42, await _vouchers.GetMaxNumberAsync(other.Id, "记"));
    }

    [Fact]
    public async Task ExistsByWordAndNumber_AcrossPeriods_StillCountsAsDuplicate()
    {
        SeedVoucher("记", 3, 2025, 12);

        // 2026 年再出现「记-3」也必须判重：后端唯一性范围是全账套，不区分期间。
        Assert.True(await _vouchers.ExistsByWordAndNumberAsync(_accountSet.Id, "记", 3));
        Assert.False(await _vouchers.ExistsByWordAndNumberAsync(_accountSet.Id, "记", 4));
        Assert.False(await _vouchers.ExistsByWordAndNumberAsync(_accountSet.Id, "转", 3));
    }

    [Fact]
    public async Task ExistsByWordAndNumber_DoesNotLeakAcrossAccountSets()
    {
        var other = _db.AddAccountSet("另一账套");
        SeedVoucher("记", 3, 2026, 1, other.Id);

        Assert.True(await _vouchers.ExistsByWordAndNumberAsync(other.Id, "记", 3));
        Assert.False(await _vouchers.ExistsByWordAndNumberAsync(_accountSet.Id, "记", 3));
    }

    [Fact]
    public async Task ExistsByWordNumberDate_ImportDedupe_IsDateScoped()
    {
        // 导入去重按「字+号+日期」精确匹配，同号不同日期不算重复（与制单判重口径不同）。
        SeedVoucher("记", 3, 2026, 1);

        Assert.True(await _vouchers.ExistsByWordNumberDateAsync(_accountSet.Id, "记", 3, new DateOnly(2026, 1, 28)));
        Assert.False(await _vouchers.ExistsByWordNumberDateAsync(_accountSet.Id, "记", 3, new DateOnly(2026, 2, 28)));
    }

    [Fact]
    public async Task Create_WithoutNumber_ContinuesFromAccountSetWideMaximum()
    {
        SeedVoucher("记", 2, 2025, 11);
        SeedVoucher("记", 5, 2026, 1);

        var created = await _service.CreateAsync(_accountSet.Id, "制单人", NewRequest());

        Assert.Equal(6, created.Number);
        Assert.Equal("记", created.Word);
        Assert.Equal(6, await _vouchers.GetMaxNumberAsync(_accountSet.Id, "记"));
    }

    [Fact]
    public async Task Create_AutoNumberIncrementsForEachNewVoucher()
    {
        var first = await _service.CreateAsync(_accountSet.Id, "制单人", NewRequest());
        var second = await _service.CreateAsync(_accountSet.Id, "制单人", NewRequest());

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
    }

    [Fact]
    public async Task Create_WithTakenNumber_ThrowsBusinessError()
    {
        SeedVoucher("记", 5, 2025, 12);

        var ex = await Assert.ThrowsAsync<VoucherException>(
            () => _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(number: 5)));

        Assert.Contains("记-5", ex.Message);
        Assert.Contains("已存在", ex.Message);
    }

    [Fact]
    public async Task Create_WithFreeNumber_KeepsNumberAndAdvancesAutoNumber()
    {
        SeedVoucher("记", 5, 2026, 1);

        var explicitNumber = await _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(number: 9));
        var autoNumber = await _service.CreateAsync(_accountSet.Id, "制单人", NewRequest());

        Assert.Equal(9, explicitNumber.Number);
        Assert.Equal(10, autoNumber.Number);
    }

    [Fact]
    public async Task Create_KeepsWordSequencesIndependent()
    {
        SeedVoucher("转", 3, 2026, 1);

        var created = await _service.CreateAsync(_accountSet.Id, "制单人", NewRequest(word: "转"));

        Assert.Equal(4, created.Number);
    }
}
