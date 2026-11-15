using System.Net;
using System.Net.Http.Json;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Funds.Domain;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// 账套删除的 HTTP 集成测试：删除是级联的（科目、凭证、账簿、字典一并清除），
/// 删掉当前账套后剩余账套中必须有一本接任当前标记，不存在的账套返回 404。
/// </summary>
public sealed class AccountSetsApiTests : IDisposable
{
    private readonly ApiHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Delete_WithBusinessData_RemovesAccountSetAndChildren()
    {
        var doomed = await _h.CreateAccountSetAsync("待删账套");
        var kept = await _h.CreateAccountSetAsync("保留账套");

        var cash = _h.AddRootAccount(doomed, "1001", "库存现金");
        var income = _h.AddRootAccount(doomed, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var voucher = await _h.CreatePostedVoucherAsync(doomed, "2026-01-15", (cash, 100m, 0m), (income, 0m, 100m));

        using (var db = _h.NewContext())
        {
            db.Journals.Add(Journal.Create(doomed, JournalType.Cash, "现金日记账", cash));
            db.EntryCategories.Add(EntryCategory.Create(doomed, EntryCategoryType.Income, "销售收入"));
            db.SaveChanges();
        }

        var response = await _h.Client.DeleteAsync($"/AccountSets/{doomed}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var sets = await _h.Client.GetFromJsonAsync<List<AccountSetModel>>("/AccountSets");
        Assert.DoesNotContain(sets!, s => s.Id == doomed);
        Assert.Contains(sets!, s => s.Id == kept);

        using var check = _h.NewContext();
        Assert.False(check.AccountSets.Any(s => s.Id == doomed));
        Assert.False(check.Accounts.Any(a => a.AccountSetId == doomed));
        Assert.False(check.Vouchers.Any(v => v.AccountSetId == doomed));
        Assert.False(check.VoucherEntries.Any(e => e.VoucherId == voucher.Id));
        Assert.False(check.Journals.Any(j => j.AccountSetId == doomed));
        Assert.False(check.EntryCategories.Any(c => c.AccountSetId == doomed));

        // 其他账套不受影响。
        Assert.True(check.AccountSets.Any(s => s.Id == kept));
    }

    [Fact]
    public async Task Delete_CurrentAccountSet_PromotesAnotherOne()
    {
        var replacement = await _h.CreateAccountSetAsync("替代账套");

        var before = await _h.Client.GetFromJsonAsync<List<AccountSetModel>>("/AccountSets");
        var current = before!.Single(s => s.IsCurrent);
        Assert.NotEqual(replacement, current.Id);

        var response = await _h.Client.DeleteAsync($"/AccountSets/{current.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await _h.Client.GetFromJsonAsync<List<AccountSetModel>>("/AccountSets");
        var promoted = after!.Single(s => s.IsCurrent);
        Assert.Equal(replacement, promoted.Id);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        var response = await _h.Client.DeleteAsync($"/AccountSets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnsupportedStandard_ReturnsChineseBadRequest()
    {
        var response = await _h.Client.PostAsJsonAsync("/AccountSets", new
        {
            name = "无清单准则账套",
            companyName = "无清单准则账套有限公司",
            standard = 99,
            taxpayerType = (int)TaxpayerType.General,
            baseCurrency = "人民币",
            startYear = 2026,
            startMonth = 1,
            preset = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("会计准则取值无效", await response.Content.ReadAsStringAsync());

        // 被拒的账套不得半落库。
        using var db = _h.NewContext();
        Assert.False(db.AccountSets.Any(s => s.Name == "无清单准则账套"));
    }

    [Theory]
    [InlineData(AccountingStandard.NewEnterprise2006, 273)]
    [InlineData(AccountingStandard.NonProfit, 84)]
    public async Task Create_WithPreset_SeedsChartOfThatStandard(AccountingStandard standard, int expected)
    {
        // 建账预置只认请求里的 preset，不再按准则设卡：每种准则各灌各自的默认科目表。
        var response = await _h.Client.PostAsJsonAsync("/AccountSets", new
        {
            name = $"{standard}账套",
            companyName = $"{standard}账套有限公司",
            standard = (int)standard,
            taxpayerType = (int)TaxpayerType.General,
            baseCurrency = "人民币",
            startYear = 2026,
            startMonth = 1,
            preset = true,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<AccountSetModel>();
        using var db = _h.NewContext();
        Assert.Equal(expected, db.Accounts.Count(a => a.AccountSetId == created!.Id));
    }

    [Fact]
    public async Task Update_EnterpriseToSmallBusiness_ReturnsBadRequestAndKeepsStandard()
    {
        var id = await _h.CreateAccountSetAsync("企业准则账套");

        var response = await _h.Client.PutAsJsonAsync($"/AccountSets/{id}", new
        {
            name = "企业准则账套",
            companyName = "企业准则账套有限公司",
            standard = (int)AccountingStandard.SmallBusiness,
            taxpayerType = (int)TaxpayerType.General,
            baseCurrency = "人民币",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("不能切换", await response.Content.ReadAsStringAsync());

        using var db = _h.NewContext();
        Assert.Equal(AccountingStandard.Enterprise, db.AccountSets.Single(s => s.Id == id).Standard);
    }

    [Fact]
    public async Task Update_SmallBusinessToEnterprise_Succeeds()
    {
        var id = await _h.CreateAccountSetAsync("小企业准则账套", AccountingStandard.SmallBusiness);

        var response = await _h.Client.PutAsJsonAsync($"/AccountSets/{id}", new
        {
            name = "小企业准则账套",
            companyName = "小企业准则账套有限公司",
            standard = (int)AccountingStandard.Enterprise,
            taxpayerType = (int)TaxpayerType.General,
            baseCurrency = "人民币",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = _h.NewContext();
        Assert.Equal(AccountingStandard.Enterprise, db.AccountSets.Single(s => s.Id == id).Standard);
    }

    private sealed record AccountSetModel(Guid Id, string Name, bool IsCurrent);
}
