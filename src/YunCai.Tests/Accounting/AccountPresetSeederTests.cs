using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace YunCai.Tests.Accounting;

/// <summary>
/// 建账预置测试：科目清单已经由代码里的硬编码表改为 <c>preset_accounts</c> 表
/// （数据随迁移写入，来源是 <see cref="PresetChart"/>），因此这里同时锁两件事：
/// 一是表里的清单与代码清单一致、层级完整；二是建账按账套准则各取各的清单。
/// </summary>
public sealed class AccountPresetSeederTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountRepository _accounts;
    private readonly PresetAccountRepository _presets;

    public AccountPresetSeederTests()
    {
        _accounts = new AccountRepository(_db.Context);
        _presets = new PresetAccountRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task PresetTable_HasEveryChartInCodeList()
    {
        var enterprise = await _presets.GetByStandardAsync(AccountingStandard.Enterprise);
        var small = await _presets.GetByStandardAsync(AccountingStandard.SmallBusiness);
        var standard2006 = await _presets.GetByStandardAsync(AccountingStandard.NewEnterprise2006);
        var nonProfit = await _presets.GetByStandardAsync(AccountingStandard.NonProfit);

        Assert.Equal(PresetChart.Enterprise.Count, enterprise.Count);
        Assert.Equal(PresetChart.SmallBusiness.Count, small.Count);
        Assert.Equal(PresetChart.NewEnterprise2006.Count, standard2006.Count);
        Assert.Equal(PresetChart.NonProfit.Count, nonProfit.Count);
        Assert.Equal(257, enterprise.Count);
        Assert.Equal(210, small.Count);
        Assert.Equal(273, standard2006.Count);
        Assert.Equal(84, nonProfit.Count);

        // 顺序号即清单原序，上级必须先于下级出现，建树时才不会退化成顶级科目。
        foreach (var rows in new[] { enterprise, small, standard2006, nonProfit })
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (row.ParentCode is { Length: > 0 })
                {
                    Assert.True(seen.Contains(row.ParentCode), $"上级「{row.ParentCode}」排在「{row.Code}」之后");
                }

                seen.Add(row.Code);
            }
        }
    }

    [Fact]
    public async Task PresetTable_SmallBusinessCoversOfficialFirstLevelAccounts()
    {
        var small = await _presets.GetByStandardAsync(AccountingStandard.SmallBusiness);

        // 财会〔2011〕17 号附录的一级科目必须齐全（5403 用现行名「税金及附加」）。
        var topLevel = small.Where(p => p.ParentCode is null).Select(p => p.Code).ToList();
        Assert.Contains("1001", topLevel);
        Assert.Contains("2221", topLevel);
        Assert.Contains("3103", topLevel);
        Assert.Contains("5801", topLevel);
        Assert.Equal(topLevel.Count, topLevel.Distinct().Count());
        Assert.All(topLevel, code => Assert.Equal(4, code.Length));
    }

    [Fact]
    public async Task Seed_EnterpriseAccountSet_CreatesEnterpriseChart()
    {
        var accountSet = _db.AddAccountSet("企业账套", standard: AccountingStandard.Enterprise);

        var created = await AccountPresetSeeder.SeedAsync(
            accountSet.Id, accountSet.Standard, _presets, _accounts);

        Assert.Equal(257, created);
        _db.DetachAll();

        var all = await _accounts.GetAllAsync(accountSet.Id);
        Assert.Equal(257, all.Count);
        Assert.All(all, account => Assert.True(account.IsPreset));

        var cash = all.Single(a => a.Code == "1001");
        Assert.Equal("库存现金", cash.Name);
        Assert.Equal(AccountCategory.Asset, cash.Category);
        Assert.Equal(BalanceDirection.Debit, cash.Direction);
        Assert.Equal(1, cash.Level);
        Assert.Null(cash.ParentId);

        // 三级编码的上级是二级编码，不能因为编码长度就挂到一级下面。
        var provision = all.Single(a => a.Code == "1231001");
        var parent = all.Single(a => a.Code == "1231");
        Assert.Equal(parent.Id, provision.ParentId);
        Assert.Equal(2, provision.Level);
        Assert.False(parent.IsLeaf);
    }

    [Fact]
    public async Task Seed_SmallBusinessAccountSet_CreatesSmallBusinessChart()
    {
        // 回归：旧实现只给企业准则账套预置，小企业账套建出来是空科目表。
        var accountSet = _db.AddAccountSet("小企业账套", "4-2-2", standard: AccountingStandard.SmallBusiness);

        var created = await AccountPresetSeeder.SeedAsync(
            accountSet.Id, accountSet.Standard, _presets, _accounts);

        Assert.Equal(210, created);
        _db.DetachAll();

        var all = await _accounts.GetAllAsync(accountSet.Id);
        Assert.Equal(210, all.Count);

        // 小企业准则的损益在 5xxx、成本在 4xxx，与 2007 企业准则的首位含义相反。
        var income = all.Single(a => a.Code == "5001");
        Assert.Equal("主营业务收入", income.Name);
        Assert.Equal(AccountCategory.ProfitAndLoss, income.Category);
        Assert.Equal(BalanceDirection.Credit, income.Direction);

        var production = all.Single(a => a.Code == "4001");
        Assert.Equal("生产成本", production.Name);
        Assert.Equal(AccountCategory.Cost, production.Category);

        // 企业准则独有的编码不应出现在小企业科目表里。
        Assert.Null(all.SingleOrDefault(a => a.Code == "6602"));
    }

    [Fact]
    public async Task Seed_SmallBusiness_KeepsThreeLevelVatDetail()
    {
        var accountSet = _db.AddAccountSet("小企业账套", "4-2-2", standard: AccountingStandard.SmallBusiness);

        await AccountPresetSeeder.SeedAsync(accountSet.Id, accountSet.Standard, _presets, _accounts);
        _db.DetachAll();

        var all = await _accounts.GetAllAsync(accountSet.Id);

        // 2221 应交税费 → 222101 应交增值税 → 22210101 进项税额：三级明细挂在二级下，不平铺到一级。
        var vat = all.Single(a => a.Code == "2221");
        var output = all.Single(a => a.Code == "222101");
        var input = all.Single(a => a.Code == "22210101");
        Assert.Equal(1, vat.Level);
        Assert.Equal(vat.Id, output.ParentId);
        Assert.Equal(output.Id, input.ParentId);
        Assert.Equal(3, input.Level);
        Assert.Equal("进项税额", input.Name);
        Assert.Equal(AccountCategory.Liability, input.Category); // 明细类别继承上级
    }

    [Fact]
    public async Task Seed_NewEnterprise2006AccountSet_CreatesNewStandardChart()
    {
        // 新会计准则（2006年颁）是另一套编码：权益 4xxx、成本 5xxx、损益 6xxx，
        // 与企业会计准则预置表（沿用 2001 版编码：权益 3xxx、成本 4xxx）不能混用。
        var accountSet = _db.AddAccountSet("新准则账套", standard: AccountingStandard.NewEnterprise2006);

        var created = await AccountPresetSeeder.SeedAsync(
            accountSet.Id, accountSet.Standard, _presets, _accounts);

        Assert.Equal(273, created);
        _db.DetachAll();

        var all = await _accounts.GetAllAsync(accountSet.Id);
        Assert.Equal(273, all.Count);

        var capital = all.Single(a => a.Code == "4001");
        Assert.Equal("实收资本", capital.Name);
        Assert.Equal(AccountCategory.Equity, capital.Category);
        Assert.Equal(BalanceDirection.Credit, capital.Direction);

        var production = all.Single(a => a.Code == "5001");
        Assert.Equal("生产成本", production.Name);
        Assert.Equal(AccountCategory.Cost, production.Category);

        var income = all.Single(a => a.Code == "6001");
        Assert.Equal("主营业务收入", income.Name);
        Assert.Equal(AccountCategory.ProfitAndLoss, income.Category);
        Assert.Equal(BalanceDirection.Credit, income.Direction);

        // 共同类（衍生工具/套期工具/被套期项目）本系统没有对应类别，按借方余额归入资产。
        var derivative = all.Single(a => a.Code == "3101");
        Assert.Equal("衍生工具", derivative.Name);
        Assert.Equal(AccountCategory.Asset, derivative.Category);
        Assert.Equal(BalanceDirection.Debit, derivative.Direction);

        var levels = all.GroupBy(a => a.Level).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(93, levels[1]);
        Assert.Equal(160, levels[2]);
        Assert.Equal(20, levels[3]);
    }

    [Fact]
    public async Task Seed_NonProfitAccountSet_CreatesNonProfitChart()
    {
        // 民非制度：净资产 3xxx、收入 4xxx、费用 5xxx。收入与费用都归损益类，靠余额方向区分借贷。
        var accountSet = _db.AddAccountSet("民非账套", standard: AccountingStandard.NonProfit);

        var created = await AccountPresetSeeder.SeedAsync(
            accountSet.Id, accountSet.Standard, _presets, _accounts);

        Assert.Equal(84, created);
        _db.DetachAll();

        var all = await _accounts.GetAllAsync(accountSet.Id);
        Assert.Equal(84, all.Count);

        var unrestricted = all.Single(a => a.Code == "3101");
        Assert.Equal("非限定性净资产", unrestricted.Name);
        Assert.Equal(AccountCategory.Equity, unrestricted.Category);
        Assert.Equal(BalanceDirection.Credit, unrestricted.Direction);

        var donation = all.Single(a => a.Code == "4101");
        Assert.Equal("捐赠收入", donation.Name);
        Assert.Equal(AccountCategory.ProfitAndLoss, donation.Category);
        Assert.Equal(BalanceDirection.Credit, donation.Direction);

        var cost = all.Single(a => a.Code == "5101");
        Assert.Equal("业务活动成本", cost.Name);
        Assert.Equal(AccountCategory.ProfitAndLoss, cost.Category);
        Assert.Equal(BalanceDirection.Debit, cost.Direction);

        var levels = all.GroupBy(a => a.Level).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new[] { 1, 2 }, levels.Keys.OrderBy(k => k));
        Assert.Equal(48, levels[1]);
        Assert.Equal(36, levels[2]);
    }

    [Fact]
    public async Task Seed_StandardWithoutChart_SeedsNothing()
    {
        // 库里出现没有预置清单的准则取值（历史数据或脏数据）时，预置必须安静地什么都不做。
        var accountSetId = Guid.NewGuid();

        var created = await AccountPresetSeeder.SeedAsync(
            accountSetId, (AccountingStandard)99, _presets, _accounts);

        Assert.Equal(0, created);
        Assert.Empty(await _accounts.GetAllAsync(accountSetId));
    }

    [Fact]
    public async Task Seed_AccountSetWithExistingAccounts_IsNoOp()
    {
        var accountSet = _db.AddAccountSet("已建账账套", standard: AccountingStandard.SmallBusiness);
        _db.AddAccount(accountSet, "1001", "库存现金");

        var created = await AccountPresetSeeder.SeedAsync(
            accountSet.Id, accountSet.Standard, _presets, _accounts);

        Assert.Equal(0, created);
        Assert.Single(await _accounts.GetAllAsync(accountSet.Id));
    }

    [Fact]
    public async Task Seed_PersistsAcrossDbContext_SoAccountsApiCanReadImmediately()
    {
        var accountSet = _db.AddAccountSet("企业账套", standard: AccountingStandard.Enterprise);

        await AccountPresetSeeder.SeedAsync(accountSet.Id, accountSet.Standard, _presets, _accounts);
        _db.DetachAll();

        // 层级关系靠父子导航属性建立，落库后必须能从库里读回完整的树。
        var levels = await _db.Context.Accounts
            .Where(a => a.AccountSetId == accountSet.Id)
            .GroupBy(a => a.Level)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        Assert.Equal(new[] { 1, 2, 3 }, levels.Keys.OrderBy(k => k));
        Assert.Equal(159, levels[1]);
        Assert.Equal(88, levels[2]);
        Assert.Equal(10, levels[3]);
    }
}
