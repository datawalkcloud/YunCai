using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Application;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Auditing.Application;
using YunCai.Modules.Auditing.Infrastructure;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Funds.Infrastructure;
using YunCai.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace YunCai.Tests.AccountSets;

/// <summary>
/// 重新初始化测试：清空业务数据之外，还要把预置档案恢复成默认。
/// 预置功能之前建的账套整套科目都不带预置标记，清空时会被整批删掉，
/// 所以这里锁住「删空后按账套准则重种默认科目表」这条链路，以及资金预置（收支类别 + 现金账户）的恢复。
/// </summary>
public sealed class ReinitServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly ReinitService _service;
    private readonly AccountRepository _accounts;
    private readonly EntryCategoryRepository _categories;
    private readonly JournalRepository _journals;

    public ReinitServiceTests()
    {
        _accounts = new AccountRepository(_db.Context);
        _categories = new EntryCategoryRepository(_db.Context);
        _journals = new JournalRepository(_db.Context);

        _service = new ReinitService(
            new AccountSetRepository(_db.Context),
            new ReinitRepository(_db.Context),
            new PresetAccountRepository(_db.Context),
            _accounts,
            _categories,
            _journals,
            new OperationLogService(new OperationLogRepository(_db.Context)));
    }

    public void Dispose() => _db.Dispose();

    private Task<ReinitResultDto> Wipe(AccountSet accountSet) =>
        _service.WipeAsync(accountSet.Id, accountSet.Name, "u1", "测试员");

    [Fact]
    public async Task Wipe_LegacyAccountSet_RestoresDefaultChartForItsStandard()
    {
        // 旧建账路径：整套科目都是普通科目（is_preset=false），清空时一律删除。
        var accountSet = _db.AddAccountSet("老账套", standard: AccountingStandard.SmallBusiness);
        var oldCash = _db.AddAccount(accountSet, "1001", "现金");
        _db.AddAccount(accountSet, "5601", "财务费用");
        _db.DetachAll();

        await Wipe(accountSet);
        _db.DetachAll();

        var all = await _accounts.GetAllAsync(accountSet.Id);
        Assert.Equal(210, all.Count);
        Assert.All(all, account => Assert.True(account.IsPreset));
        // 默认表里的科目名与旧表可以同名（1001 库存现金），但 id 是重种的新一代。
        Assert.Contains("1001", all.Select(a => a.Code));

        // 资金预置同样恢复：19 条收支类别 + 指向新 1001 的现金账户。
        Assert.Equal(19, (await _categories.GetAllAsync(accountSet.Id)).Count);
        var cashJournal = Assert.Single(await _journals.GetAllAsync(accountSet.Id));
        Assert.Equal(all.Single(a => a.Code == "1001").Id, cashJournal.AccountId);
        Assert.NotEqual(oldCash.Id, cashJournal.AccountId);
    }

    [Fact]
    public async Task Wipe_PresetAccountSet_KeepsChartAndDropsManualAccounts()
    {
        var accountSet = _db.AddAccountSet("新账套", standard: AccountingStandard.Enterprise);
        await AccountPresetSeeder.SeedAsync(
            accountSet.Id, accountSet.Standard, new PresetAccountRepository(_db.Context), _accounts);

        // 用户自己加的科目：清空后不保留；预置科目留下但期初余额清零。
        var manual = Account.CreateRoot(accountSet.Id, "1091", "自定义科目", AccountCategory.Asset, BalanceDirection.Debit);
        _db.Context.Accounts.Add(manual);
        _db.Context.Accounts.Where(a => a.AccountSetId == accountSet.Id && a.Code == "1001")
            .ExecuteUpdate(s => s.SetProperty(a => a.OpeningBalance, 888m));
        _db.DetachAll();

        await Wipe(accountSet);
        _db.DetachAll();

        var all = await _accounts.GetAllAsync(accountSet.Id);
        Assert.Equal(257, all.Count);
        Assert.DoesNotContain("1091", all.Select(a => a.Code));
        Assert.Equal(0m, all.Single(a => a.Code == "1001").OpeningBalance);
    }

    [Fact]
    public async Task Wipe_DoesNotTouchCurrenciesOrVoucherWords()
    {
        var accountSet = _db.AddAccountSet("只清业务数据", standard: AccountingStandard.Enterprise);
        _db.Context.Currencies.Add(Currency.Create(accountSet.Id, "CNY", "人民币", 1m, CurrencyConvertType.Multiply, true));
        _db.Context.VoucherWords.Add(VoucherWord.Create(accountSet.Id, "记", "记账凭证", true));
        _db.Context.SaveChanges();
        _db.DetachAll();

        await Wipe(accountSet);
        _db.DetachAll();

        Assert.Equal(1, await _db.Context.Currencies.CountAsync(c => c.AccountSetId == accountSet.Id));
        Assert.Equal(1, await _db.Context.VoucherWords.CountAsync(w => w.AccountSetId == accountSet.Id));
    }

    [Fact]
    public async Task Wipe_WrongConfirmName_ThrowsAndKeepsData()
    {
        var accountSet = _db.AddAccountSet("口令不对", standard: AccountingStandard.Enterprise);
        await AccountPresetSeeder.SeedAsync(
            accountSet.Id, accountSet.Standard, new PresetAccountRepository(_db.Context), _accounts);
        _db.DetachAll();

        await Assert.ThrowsAsync<ReinitException>(() => _service.WipeAsync(accountSet.Id, "别的名字", "u1", "测试员"));
        _db.DetachAll();

        Assert.Equal(257, (await _accounts.GetAllAsync(accountSet.Id)).Count);
    }
}
