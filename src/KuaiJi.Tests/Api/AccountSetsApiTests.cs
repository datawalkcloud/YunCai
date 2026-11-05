using System.Net;
using System.Net.Http.Json;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Funds.Domain;
using Xunit;

namespace KuaiJi.Tests.Api;

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

    private sealed record AccountSetModel(Guid Id, string Name, bool IsCurrent);
}
