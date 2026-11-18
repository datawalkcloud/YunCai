using System.Net;
using System.Net.Http.Json;
using YunCai.Modules.Accounting.Domain;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// 科目核算属性（二级类别 / 现金科目 / 数量核算 / 外币核算）与末级改码的 HTTP 集成测试：
/// - 建账与编辑接口能把属性写进去并原样回读，明细科目从上级继承（含币别列表）；
/// - 数量核算缺单位、外币核算缺币别、币别跨账套都在 400 上以中文文案挡下；
/// - 外币列表整体覆盖必须能换血（account_currencies 上有唯一索引，新增先于删除会撞键）；
/// - 编码只有末级可改，且受编码方案位数、上级前缀与账套内重号三条约束。
/// 每个用例一个独立账套，互不干扰。
/// </summary>
public sealed class AccountAttributesApiTests : IDisposable
{
    private readonly ApiHarness _h = new();
    private readonly HttpClient _client;

    public AccountAttributesApiTests() => _client = _h.Client;

    public void Dispose() => _h.Dispose();

    private static string AccountsPath(Guid accountSetId) => ApiHarness.ResourcePath(accountSetId, "Accounts");

    private static readonly System.Text.Json.JsonSerializerOptions JsonWeb =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private sealed record AccountModel(
        Guid Id,
        string Code,
        string Name,
        AccountCategory Category,
        AccountSubCategory? SubCategory,
        bool IsCashAccount,
        bool IsQuantityAccounted,
        string? Unit,
        bool IsForeignCurrencyAccounted,
        List<Guid> CurrencyIds,
        bool IsLeaf);

    private sealed record CurrencyModel(Guid Id, string Code, string Name, bool IsBase);

    private async Task<(HttpStatusCode Status, string Body, AccountModel? Account)> CreateAsync(Guid accountSetId, object request)
    {
        var response = await _client.PostAsJsonAsync(AccountsPath(accountSetId), request);
        var body = await response.Content.ReadAsStringAsync();
        var account = response.StatusCode == HttpStatusCode.Created
            ? System.Text.Json.JsonSerializer.Deserialize<AccountModel>(body, JsonWeb)
            : null;
        return (response.StatusCode, body, account);
    }

    private async Task<(HttpStatusCode Status, string Body, AccountModel? Account)> UpdateAsync(Guid accountSetId, Guid id, object request)
    {
        var response = await _client.PutAsJsonAsync($"{AccountsPath(accountSetId)}/{id}", request);
        var body = await response.Content.ReadAsStringAsync();
        var account = response.StatusCode == HttpStatusCode.OK
            ? System.Text.Json.JsonSerializer.Deserialize<AccountModel>(body, JsonWeb)
            : null;
        return (response.StatusCode, body, account);
    }

    private async Task<List<AccountModel>> ListAsync(Guid accountSetId)
        => await _client.GetFromJsonAsync<List<AccountModel>>(AccountsPath(accountSetId)) ?? [];

    /// <summary>新建一枚非本位币并返回标识（首次访问币别列表会自动补种人民币本位币）。</summary>
    private async Task<Guid> AddCurrencyAsync(Guid accountSetId, string code, string name)
    {
        var response = await _client.PostAsJsonAsync(
            ApiHarness.ResourcePath(accountSetId, "Currencies"),
            new { code, name, rate = 1m, isBase = false });
        response.EnsureSuccessStatusCode();
        var currency = await response.Content.ReadFromJsonAsync<CurrencyModel>();
        return currency!.Id;
    }

    private static string Explain(HttpStatusCode expected, (HttpStatusCode Status, string Body, AccountModel? Account) actual)
        => $"期望 {expected}，实际 {actual.Status}；响应体：{actual.Body}";

    /// <summary>币别集合不保证顺序（PUT 回的是内存插入序、列表回的是库返回序），两边排序后比较。</summary>
    private static void AssertSameIds(IEnumerable<Guid> expected, IEnumerable<Guid> actual)
        => Assert.Equal(expected.OrderBy(x => x).ToArray(), actual.OrderBy(x => x).ToArray());

    [Fact]
    public async Task Create_RootWithAttributes_PersistsAndReturnsThem()
    {
        var id = await _h.CreateAccountSetAsync("科目属性建账");
        var usd = await AddCurrencyAsync(id, "USD", "美元");

        var created = await CreateAsync(id, new
        {
            code = "1001",
            name = "库存现金",
            category = 1,
            direction = 1,
            subCategory = 1,
            isCashAccount = true,
            isQuantityAccounted = true,
            unit = "台",
            isForeignCurrencyAccounted = true,
            currencyIds = new[] { usd },
        });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(AccountSubCategory.CurrentAsset, created.Account!.SubCategory);
        Assert.True(created.Account.IsCashAccount);
        Assert.True(created.Account.IsQuantityAccounted);
        Assert.Equal("台", created.Account.Unit);
        Assert.Equal([usd], created.Account.CurrencyIds);

        // 列表也要带齐属性：编辑弹窗直接吃列表数据，缺字段会把真实配置覆盖成空。
        var listed = (await ListAsync(id)).Single(a => a.Code == "1001");
        Assert.Equal("台", listed.Unit);
        Assert.Equal([usd], listed.CurrencyIds);
    }

    [Fact]
    public async Task Create_ChildInheritsAttributesAndCurrenciesFromParent()
    {
        var id = await _h.CreateAccountSetAsync("科目属性继承");
        var usd = await AddCurrencyAsync(id, "USD", "美元");
        var eur = await AddCurrencyAsync(id, "EUR", "欧元");
        var parent = (await CreateAsync(id, new
        {
            code = "1405",
            name = "库存商品",
            category = 1,
            direction = 1,
            subCategory = 1,
            isQuantityAccounted = true,
            unit = "件",
            isForeignCurrencyAccounted = true,
            currencyIds = new[] { usd, eur },
        })).Account!;

        // 请求里故意给一套不同的值：明细科目的核算属性一律以父级为准。
        var child = await CreateAsync(id, new
        {
            code = "140501",
            name = "甲商品",
            parentId = parent.Id,
            subCategory = 2,
            isCashAccount = true,
            isQuantityAccounted = false,
            isForeignCurrencyAccounted = false,
        });

        Assert.Equal(HttpStatusCode.Created, child.Status);
        var listed = (await ListAsync(id)).Single(a => a.Code == "140501");
        Assert.Equal(AccountSubCategory.CurrentAsset, listed.SubCategory);
        Assert.False(listed.IsCashAccount);
        Assert.True(listed.IsQuantityAccounted);
        Assert.Equal("件", listed.Unit);
        AssertSameIds(new[] { usd, eur }, listed.CurrencyIds);
    }

    // CurrencyIds 是「缺省即空集合」的字段，测试按前端真实请求形态传空数组（传 null 会先撞上模型校验）。
    [Theory]
    [InlineData(true, "", false, new string[0], "开启数量核算的科目必须填写计量单位")]
    [InlineData(false, null, true, new string[0], "开启外币核算的科目至少要选择一个币别")]
    public async Task Create_InvalidAttributeCombination_Returns400WithReason(
        bool quantity, string? unit, bool foreign, string[]? currencyIds, string expectedMessage)
    {
        var id = await _h.CreateAccountSetAsync("科目属性校验");

        var created = await CreateAsync(id, new
        {
            code = "1001",
            name = "库存现金",
            category = 1,
            direction = 1,
            isQuantityAccounted = quantity,
            unit,
            isForeignCurrencyAccounted = foreign,
            currencyIds,
        });

        Assert.True(created.Status == HttpStatusCode.BadRequest, Explain(HttpStatusCode.BadRequest, created));
        Assert.True(created.Body.Contains(expectedMessage), $"响应体：{created.Body}");
        Assert.DoesNotContain("YunCai.Modules", created.Body);
    }

    [Fact]
    public async Task Create_ForeignCurrencyOfAnotherAccountSet_Returns400()
    {
        var id = await _h.CreateAccountSetAsync("科目外币归属");
        var otherSet = await _h.CreateAccountSetAsync("另一账套");
        var foreignCurrency = await AddCurrencyAsync(otherSet, "USD", "美元");

        var created = await CreateAsync(id, new
        {
            code = "1001",
            name = "库存现金",
            category = 1,
            direction = 1,
            isForeignCurrencyAccounted = true,
            currencyIds = new[] { foreignCurrency },
        });

        Assert.Equal(HttpStatusCode.BadRequest, created.Status);
        Assert.Contains("不属于本账套", created.Body);
    }

    [Fact]
    public async Task Update_SetsAttributesAndTreatsSubCategoryAsFullOverwrite()
    {
        var id = await _h.CreateAccountSetAsync("科目属性编辑");
        var usd = await AddCurrencyAsync(id, "USD", "美元");
        var created = await CreateAsync(id, new { code = "1001", name = "库存现金", category = 1, direction = 1 });
        var accountId = created.Account!.Id;

        var updated = await UpdateAsync(id, accountId, new
        {
            subCategory = 2,
            isCashAccount = true,
            isQuantityAccounted = true,
            unit = " 台 ",
            isForeignCurrencyAccounted = true,
            currencyIds = new[] { usd },
        });
        Assert.True(updated.Status == HttpStatusCode.OK, Explain(HttpStatusCode.OK, updated));
        Assert.Equal(AccountSubCategory.NonCurrentAsset, updated.Account!.SubCategory);
        Assert.True(updated.Account.IsCashAccount);
        // 计量单位入库前去掉首尾空白。
        Assert.Equal("台", updated.Account.Unit);
        Assert.Equal([usd], updated.Account.CurrencyIds);

        // 二级类别按整体覆盖处理：不传即清空，编辑弹窗清空下拉时才会真的落库为空。
        var cleared = await UpdateAsync(id, accountId, new { name = "库存现金" });
        Assert.True(cleared.Status == HttpStatusCode.OK, Explain(HttpStatusCode.OK, cleared));
        Assert.Null(cleared.Account!.SubCategory);

        // 关闭数量核算要连带清掉单位，否则下次再开会残留旧值。
        var quantityOff = await UpdateAsync(id, accountId, new { isQuantityAccounted = false });
        Assert.True(quantityOff.Status == HttpStatusCode.OK, Explain(HttpStatusCode.OK, quantityOff));
        Assert.False(quantityOff.Account!.IsQuantityAccounted);
        Assert.Null(quantityOff.Account.Unit);
    }

    [Fact]
    public async Task Update_ReplacesCurrencyList_WithoutTrippingUniqueIndex()
    {
        var id = await _h.CreateAccountSetAsync("科目外币换血");
        var usd = await AddCurrencyAsync(id, "USD", "美元");
        var eur = await AddCurrencyAsync(id, "EUR", "欧元");
        var jpy = await AddCurrencyAsync(id, "JPY", "日元");
        var created = await CreateAsync(id, new
        {
            code = "1002",
            name = "银行存款",
            category = 1,
            direction = 1,
            isForeignCurrencyAccounted = true,
            currencyIds = new[] { usd, eur },
        });
        var accountId = created.Account!.Id;

        // 新旧列表有交集（eur）：一次 SaveChanges 内新增先于删除，会撞 (account_id, currency_id) 唯一索引，
        // 控制器为此分两次提交。这里若退化成 500 说明两段式保存被改坏了。
        var replaced = await UpdateAsync(id, accountId, new
        {
            isForeignCurrencyAccounted = true,
            currencyIds = new[] { eur, jpy },
        });

        Assert.True(replaced.Status == HttpStatusCode.OK, Explain(HttpStatusCode.OK, replaced));
        AssertSameIds(new[] { eur, jpy }, replaced.Account!.CurrencyIds);
        AssertSameIds(new[] { eur, jpy }, (await ListAsync(id)).Single(a => a.Code == "1002").CurrencyIds);
    }

    [Fact]
    public async Task Update_CodeFollowsLeafSchemePrefixAndUniquenessRules()
    {
        var id = await _h.CreateAccountSetAsync("科目改码");
        var parent = (await CreateAsync(id, new { code = "1001", name = "库存现金", category = 1, direction = 1 })).Account!;
        var first = (await CreateAsync(id, new { code = "100101", name = "人民币户", parentId = parent.Id })).Account!;
        var second = (await CreateAsync(id, new { code = "100102", name = "美元户", parentId = parent.Id })).Account!;

        // 非末级：改码会让下级前缀失效，直接挡下。
        var nonLeaf = await UpdateAsync(id, parent.Id, new { code = "1002" });
        Assert.Equal(HttpStatusCode.BadRequest, nonLeaf.Status);
        Assert.Contains("非末级科目不允许修改科目编码", nonLeaf.Body);

        // 末级：位数按编码方案 4-2-2-2 的第 2 级 = 6 位。
        var wrongLength = await UpdateAsync(id, first.Id, new { code = "1001011" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongLength.Status);
        Assert.Contains("第 2 级科目编码应为 6 位", wrongLength.Body);

        // 重号与脱离上级前缀分别给出可辨识的文案。
        var duplicate = await UpdateAsync(id, first.Id, new { code = second.Code });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.Status);
        Assert.Contains("科目编码已存在", duplicate.Body);

        var wrongPrefix = await UpdateAsync(id, first.Id, new { code = "100201" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPrefix.Status);
        Assert.Contains("必须以上级科目编码「1001」为前缀", wrongPrefix.Body);

        var renamed = await UpdateAsync(id, first.Id, new { code = "100103" });
        Assert.True(renamed.Status == HttpStatusCode.OK, Explain(HttpStatusCode.OK, renamed));
        Assert.Equal("100103", renamed.Account!.Code);
        Assert.Equal(["1001", "100102", "100103"], (await ListAsync(id)).Select(a => a.Code).ToArray());
    }

    [Fact]
    public async Task Update_SubCategoryMismatchingCategory_Returns400()
    {
        var id = await _h.CreateAccountSetAsync("科目类别越界");
        var created = await CreateAsync(id, new { code = "2202", name = "应付账款", category = 2, direction = 2 });

        // 负债类科目只有流动/非流动负债两档，传「流动资产」必须被挡。
        var updated = await UpdateAsync(id, created.Account!.Id, new { subCategory = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, updated.Status);
        Assert.Contains("负债」类科目没有二级类别「流动资产」", updated.Body);
    }
}
