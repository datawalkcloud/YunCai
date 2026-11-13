using System.Net.Http.Json;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using KuaiJi.Tests.Support;

namespace KuaiJi.Tests.Api;

/// <summary>
/// Controller 集成测试的共用外壳：一个 <see cref="ApiTestFactory"/>（真实组合根 + 内存 SQLite + 播种）
/// 加一个带令牌的 HttpClient，并提供「建账套 / 播科目」两个最常用的一步到位动作。
/// 账套走真实的 POST /AccountSets（顺带覆盖账套端点，且拿到播种一致的凭证字与编码方案），
/// 科目直写库（科目端点自己的建/删行为由 AccountsApiTests 专门覆盖，这里只当数据准备）。
/// 每个测试类一个实例（每类一套独立库），用例之间用独立账套隔离。
/// </summary>
internal sealed class ApiHarness : IDisposable
{
    /// <summary>令牌里的显示名，会被凭证等端点当作制单人/审核人落库。</summary>
    public const string TokenDisplayName = "集成测试";

    private readonly ApiTestFactory _factory = new();

    public ApiHarness(string userId = "integration-test-user")
    {
        Client = _factory.CreateAuthenticatedClient(userId);
    }

    public HttpClient Client { get; }

    /// <summary>另开一个数据库上下文，等于一次新的请求作用域，用于直查库做断言或补种数据。</summary>
    public AppDbContext NewContext() => _factory.NewContext();

    /// <summary>经 POST /AccountSets 建一个独立账套（默认企业准则、编码方案 4-2-2-2、启用期间为当年 1 月）。</summary>
    public async Task<Guid> CreateAccountSetAsync(string name, AccountingStandard standard = AccountingStandard.Enterprise)
    {
        var response = await Client.PostAsJsonAsync("/AccountSets", new
        {
            name,
            companyName = $"{name}有限公司",
            standard = (int)standard,
            taxpayerType = (int)TaxpayerType.General,
            baseCurrency = "人民币",
            startYear = 2026,
            startMonth = 1,
            accountCodingScheme = "4-2-2-2",
            preset = false,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AccountSetStub>();
        return body!.Id;
    }

    /// <summary>
    /// 往账套里直写一个科目。返回科目标识，供后续请求引用。
    /// 顶级科目按编码方案必须是 4 位（如 1001），明细科目由 <see cref="AddChildAccountAsync"/> 建。
    /// </summary>
    public Guid AddRootAccount(
        Guid accountSetId,
        string code,
        string name,
        AccountCategory category = AccountCategory.Asset,
        BalanceDirection direction = BalanceDirection.Debit,
        bool isPreset = false)
    {
        using var db = NewContext();
        var account = Account.CreateRoot(accountSetId, code, name, category, direction, isPreset);
        db.Accounts.Add(account);
        db.SaveChanges();
        return account.Id;
    }

    /// <summary>把已存在的顶级科目变成非末级（建一个下级明细科目），用于校验「非末级不可记账/可汇总」。</summary>
    public Guid AddChildAccount(Guid parentId, string code, string name)
    {
        using var db = NewContext();
        var parent = db.Accounts.Single(a => a.Id == parentId);
        var child = parent.AddChild(code, name);
        db.Accounts.Add(child);
        db.SaveChanges();
        return child.Id;
    }

    /// <summary>某账套下资源的集合路径，如 ResourcePath(id, "Accounts") → /accountsets/{id}/Accounts。</summary>
    public static string ResourcePath(Guid accountSetId, string controller) => $"/accountsets/{accountSetId}/{controller}";

    /// <summary>
    /// 经 HTTP 制一张草稿凭证（制单即过账，这里再反过账退回草稿），制单人取令牌 displayName。
    /// 分录按「科目 + 借 + 贷」给，日期用 yyyy-MM-dd 字面量，与前端实际发出的 JSON 形态一致。
    /// </summary>
    public async Task<VoucherStub> CreateDraftVoucherAsync(
        Guid accountSetId,
        string date,
        params (Guid AccountId, decimal Debit, decimal Credit)[] entries)
    {
        var voucher = await CreatePostedVoucherAsync(accountSetId, date, entries);
        await UnpostVoucherAsync(accountSetId, voucher.Id);
        return voucher;
    }

    /// <summary>经 HTTP 制一张凭证（保存即过账），分录按「科目 + 借 + 贷」给。</summary>
    public async Task<VoucherStub> CreatePostedVoucherAsync(
        Guid accountSetId,
        string date,
        params (Guid AccountId, decimal Debit, decimal Credit)[] entries)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            word = "记",
            date,
            entries = entries.Select(e => new
            {
                summary = "报表测试分录",
                accountId = e.AccountId,
                debit = e.Debit,
                credit = e.Credit,
            }),
        });

        var response = await Client.PostAsync(
            ResourcePath(accountSetId, "Vouchers"),
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VoucherStub>())!;
    }

    /// <summary>反过账：已过账 → 草稿（编辑前的必经步骤）。</summary>
    public async Task UnpostVoucherAsync(Guid accountSetId, Guid voucherId)
    {
        var unpost = await Client.PostAsync($"{ResourcePath(accountSetId, "Vouchers")}/{voucherId}/unpost", null);
        unpost.EnsureSuccessStatusCode();
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>POST /AccountSets 响应里只需要 id。</summary>
    private sealed record AccountSetStub(Guid Id);

    /// <summary>凭证端点响应里测试常用的两个字段。</summary>
    public sealed record VoucherStub(Guid Id, string VoucherNo);
}
