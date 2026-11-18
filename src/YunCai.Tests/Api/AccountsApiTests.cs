using System.Net;
using System.Net.Http.Json;
using YunCai.Modules.Accounting.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// 科目 HTTP 集成测试：走真实组合根，锁住科目树在 HTTP 边界上的四条承诺——
/// - 编码方案（账套 4-2-2-2）决定每级的编码位数与最大级次，不符时 400 且文案带上方案本身；
/// - 建了下级就自动脱离「末级」，leafOnly 列表（凭证录入用）随之把它剔掉，停用科目也被剔掉；
/// - 预置科目与有下级的科目不可删除（中文文案），期初余额只允许录在末级、非末级按下级汇总且合计不重复计数；
/// - 导出文件可原样回传导入（编码已存在者计入跳过，不新建），模板/导出真的产出 xlsx 字节。
/// 每个用例一个独立账套，互不干扰。
/// </summary>
public sealed class AccountsApiTests : IDisposable
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ApiHarness _h = new();
    private readonly HttpClient _client;

    public AccountsApiTests() => _client = _h.Client;

    public void Dispose() => _h.Dispose();

    private static string AccountsPath(Guid accountSetId) => ApiHarness.ResourcePath(accountSetId, "Accounts");

    private static readonly System.Text.Json.JsonSerializerOptions JsonWeb =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private async Task<CreateResult> PostCreate(Guid accountSetId, object request)
    {
        var response = await _client.PostAsJsonAsync(AccountsPath(accountSetId), request);
        var body = await response.Content.ReadAsStringAsync();
        var account = response.StatusCode == HttpStatusCode.Created
            ? System.Text.Json.JsonSerializer.Deserialize<AccountModel>(body, JsonWeb)
            : null;
        return new CreateResult(response.StatusCode, body, account);
    }

    // —— 编码方案校验 ——

    [Fact]
    public async Task Create_RootCodeNotMatchingCodingScheme_Returns400NamingExpectedLength()
    {
        var id = await _h.CreateAccountSetAsync("科目编码位数");

        var tooShort = await PostCreate(id, new { code = "101", name = "库存现金", category = 1, direction = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.Status);
        // 文案必须把「期望位数」和「当前位数」都说出来，否则用户不知道该改成什么。
        Assert.Contains("一级科目编码应为 4 位", tooShort.Body);
        Assert.Contains("当前编码「101」为 3 位", tooShort.Body);

        var ok = await PostCreate(id, new { code = "1001", name = "库存现金", category = 1, direction = 1 });
        Assert.Equal(HttpStatusCode.Created, ok.Status);
    }

    [Fact]
    public async Task Create_ChildUnderParent_LowersParentToNonLeafAndIsFilteredByLeafOnly()
    {
        var id = await _h.CreateAccountSetAsync("科目末级过滤");
        var parent = await PostCreate(id, new { code = "1001", name = "库存现金", category = 1, direction = 1 });
        var parentId = parent.Account!.Id;

        // 明细科目：第 2 级按方案 4-2-2-2 应为 6 位，类别继承上级（不传也按上级判定）。
        var child = await _client.PostAsJsonAsync(AccountsPath(id),
            new { code = "100101", name = "现金日记账", parentId });
        Assert.Equal(HttpStatusCode.Created, child.StatusCode);

        var all = await _client.GetFromJsonAsync<List<AccountModel>>(AccountsPath(id));
        var parentAgain = all!.Single(a => a.Code == "1001");
        Assert.False(parentAgain.IsLeaf);
        Assert.Equal([parentAgain.Id], all!.Where(a => !a.IsLeaf).Select(a => a.Id).ToList());

        // leafOnly=true 供凭证录入选择：非末级的上级必须消失，末级保留。
        var leaves = await _client.GetFromJsonAsync<List<AccountModel>>($"{AccountsPath(id)}?leafOnly=true");
        Assert.Equal(["100101"], leaves!.Select(a => a.Code).ToList());

        // 停用末级科目同样不进 leafOnly（历史凭证仍要能显示，但不能再被选中）。
        var disabled = await _client.PutAsJsonAsync($"{AccountsPath(id)}/{leaves![0].Id}", new { isEnabled = false });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<List<AccountModel>>($"{AccountsPath(id)}?leafOnly=true"))!);

        // 列表按编码字典序返回，前端树形渲染依赖这个顺序（父级必须先于子级出现）。
        Assert.Equal(["1001", "100101"], (await _client.GetFromJsonAsync<List<AccountModel>>(AccountsPath(id)))!.Select(a => a.Code).ToList());
    }

    [Fact]
    public async Task Create_DuplicateCode_BlankCode_UnknownParent_AllReturnChineseBadRequest()
    {
        var id = await _h.CreateAccountSetAsync("科目建档校验");
        await PostCreate(id, new { code = "1001", name = "库存现金", category = 1, direction = 1 });

        Assert.Contains("科目编码已存在", (await PostCreate(id, new { code = "1001", name = "重名现金", category = 1, direction = 1 })).Body);
        Assert.Contains("请填写科目编码和名称", (await PostCreate(id, new { code = "  ", name = "缺编码", category = 1, direction = 1 })).Body);
        Assert.Contains("指定的上级科目不存在", (await PostCreate(id, new { code = "100101", name = "野孩子", parentId = Guid.NewGuid() })).Body);

        // 账套不存在时也必须是可读的 400，而不是 500 或静默成功。
        Assert.Contains("账套不存在", (await PostCreate(Guid.NewGuid(), new { code = "1001", name = "库存现金", category = 1, direction = 1 })).Body);
    }

    [Fact]
    public async Task Create_BeyondMaxLevelAndWrongChildLength_Returns400WithSchemeText()
    {
        var id = await _h.CreateAccountSetAsync("科目级次上限");
        var l1 = (await PostCreate(id, new { code = "1001", name = "一级", category = 1, direction = 1 })).Account!;
        var l2 = (await PostCreate(id, new { code = "100101", name = "二级", parentId = l1.Id })).Account!;
        var l3 = (await PostCreate(id, new { code = "10010101", name = "三级", parentId = l2.Id })).Account!;
        var l4 = (await PostCreate(id, new { code = "1001010101", name = "四级", parentId = l3.Id })).Account!;
        Assert.Equal(4, l4.Level);

        // 方案 4-2-2-2 只有四级，第五级直接拒绝（校验顺序：级次上限先于位数）。
        var fifth = await PostCreate(id, new { code = "100101010101", name = "五级", parentId = l4.Id });
        Assert.Equal(HttpStatusCode.BadRequest, fifth.Status);
        Assert.Contains("最多支持 4 级", fifth.Body);

        // 第 2 级需要 6 位：给 5 位时文案要点出方案与当前位数。
        var wrongLength = await PostCreate(id, new { code = "10011", name = "位数错", parentId = l1.Id });
        Assert.Equal(HttpStatusCode.BadRequest, wrongLength.Status);
        Assert.Contains("第 2 级科目编码应为 6 位", wrongLength.Body);

        // 前缀不属于上级：AddChild 的领域校验透出到 400。
        var wrongPrefix = await PostCreate(id, new { code = "100201", name = "前缀错", parentId = l1.Id });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPrefix.Status);
        Assert.Contains("1001", wrongPrefix.Body);
    }

    // —— 删除保护 ——

    [Fact]
    public async Task Delete_PresetAccount_Returns400AndKeepsIt()
    {
        var id = await _h.CreateAccountSetAsync("科目预置保护");
        var presetId = _h.AddRootAccount(id, "1001", "库存现金", isPreset: true);

        var response = await _client.DeleteAsync($"{AccountsPath(id)}/{presetId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("预置科目不可删除", await response.Content.ReadAsStringAsync());
        Assert.Single((await _client.GetFromJsonAsync<List<AccountModel>>(AccountsPath(id)))!);
    }

    [Fact]
    public async Task Delete_ParentWithChild_Returns400ThenSucceedsAfterChildRemoved()
    {
        var id = await _h.CreateAccountSetAsync("科目下级保护");
        var parentId = _h.AddRootAccount(id, "1002", "银行存款");
        var childId = _h.AddChildAccount(parentId, "100201", "工商银行");

        var blocked = await _client.DeleteAsync($"{AccountsPath(id)}/{parentId}");
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("该科目下仍有明细科目，请先删除下级科目", await blocked.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"{AccountsPath(id)}/{childId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"{AccountsPath(id)}/{parentId}")).StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<List<AccountModel>>(AccountsPath(id)))!);

        // 删干净之后再删同一个 id，走的是「科目不存在」→ 404，不是 500。
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"{AccountsPath(id)}/{childId}")).StatusCode);
    }

    // —— 期初余额 ——

    [Fact]
    public async Task SaveOpeningBalances_LeavesRollUpToParentWithoutDoubleCounting()
    {
        var id = await _h.CreateAccountSetAsync("科目期初");
        var cash = _h.AddRootAccount(id, "1001", "库存现金");
        var bank = _h.AddRootAccount(id, "1002", "银行存款");
        var icbc = _h.AddChildAccount(bank, "100201", "工商银行");
        var capital = _h.AddRootAccount(id, "3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);

        var saved = await _client.PutAsJsonAsync($"{AccountsPath(id)}/opening-balances", new
        {
            items = new object[]
            {
                new { accountId = cash, amount = 1000m },
                new { accountId = icbc, amount = 5000m },
                new { accountId = capital, amount = 6000m },
            },
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var table = await saved.Content.ReadFromJsonAsync<OpeningBalancesModel>();

        // 试算平衡：借方 1001 + 100201 = 6000，贷方 3001 = 6000。
        Assert.True(table!.Balanced);
        Assert.Equal(6000m, table.TotalDebit);
        Assert.Equal(6000m, table.TotalCredit);

        // 非末级的 1002 行按下级汇总（借 5000），但合计里不能再出现它，否则重复计数。
        var bankRow = table.Rows.Single(r => r.Code == "1002");
        Assert.False(bankRow.IsLeaf);
        Assert.Equal(5000m, bankRow.Debit);
        Assert.Equal(0m, bankRow.Credit);

        // 贷方科目按方向为正 → 列示在贷方栏。
        var capitalRow = table.Rows.Single(r => r.Code == "3001");
        Assert.Equal(0m, capitalRow.Debit);
        Assert.Equal(6000m, capitalRow.Credit);
    }

    [Fact]
    public async Task SaveOpeningBalances_NonLeafAndForeignAccount_ReturnChineseBadRequest()
    {
        var id = await _h.CreateAccountSetAsync("科目期初拦截");
        var bank = _h.AddRootAccount(id, "1002", "银行存款");
        _h.AddChildAccount(bank, "100201", "工商银行");

        // 余额只能录在末级：非末级被领域层拦下，HTTP 侧转 400。
        var onNonLeaf = await _client.PutAsJsonAsync($"{AccountsPath(id)}/opening-balances",
            new { items = new[] { new { accountId = bank, amount = 100m } } });
        Assert.Equal(HttpStatusCode.BadRequest, onNonLeaf.StatusCode);
        Assert.Contains("末级", await onNonLeaf.Content.ReadAsStringAsync());

        // 传入别的账套的科目 id：整批拒绝，不落任何改动。
        var foreign = await _client.PutAsJsonAsync($"{AccountsPath(id)}/opening-balances",
            new { items = new[] { new { accountId = Guid.NewGuid(), amount = 100m } } });
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Contains("不属于当前账套", await foreign.Content.ReadAsStringAsync());
        Assert.Equal(0m, (await _client.GetFromJsonAsync<OpeningBalancesModel>($"{AccountsPath(id)}/opening-balances"))!.TotalDebit);

        var unknownSet = await _client.GetAsync($"{AccountsPath(Guid.NewGuid())}/opening-balances");
        Assert.Equal(HttpStatusCode.BadRequest, unknownSet.StatusCode);
        Assert.Contains("指定的账套不存在", await unknownSet.Content.ReadAsStringAsync());
    }

    // —— 模板 / 导出 / 导入回环 ——

    [Fact]
    public async Task DownloadTemplate_ReturnsReadableXlsx()
    {
        var response = await _client.GetAsync($"{ApiHarness.ResourcePath(Guid.NewGuid(), "Accounts")}/import/template");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XlsxContentType, response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        // xlsx 是 zip 容器，魔数 PK\x03\x04；产不出这个说明模板生成器坏了。
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
        Assert.True(bytes.Length > 1_000);
    }

    [Fact]
    public async Task Export_ThenReimport_ReportsSkippedInsteadOfDuplicating()
    {
        var id = await _h.CreateAccountSetAsync("科目导出回环");
        _h.AddRootAccount(id, "1001", "库存现金");
        var bank = _h.AddRootAccount(id, "1002", "银行存款", AccountCategory.Asset);
        _h.AddChildAccount(bank, "100201", "工商银行");

        var export = await _client.GetAsync($"{AccountsPath(id)}/export");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal(XlsxContentType, export.Content.Headers.ContentType!.MediaType);
        var xlsx = await export.Content.ReadAsByteArrayAsync();

        var upload = new MultipartFormDataContent
        {
            { new ByteArrayContent(xlsx), "file", "科目表.xlsx" },
        };
        var reimported = await _client.PostAsync($"{AccountsPath(id)}/import", upload);

        Assert.Equal(HttpStatusCode.OK, reimported.StatusCode);
        var result = await reimported.Content.ReadFromJsonAsync<ImportResultModel>();
        // 导出的三行全部因「编码已存在」跳过：既不新建、也不报错，正是可反复回传的幂等口径。
        // 跳过逐行登记为告警（前端要能看到是哪几行没进去），但不影响成功状态码。
        Assert.Equal(3, result!.TotalRows);
        Assert.Equal(0, result.ImportedAccounts);
        Assert.Equal(3, result.SkippedAccounts);
        Assert.Equal(3, result.Warnings.Count);
        Assert.All(result.Warnings, w => Assert.Contains("编码已存在，已跳过", w));
        Assert.Contains(result.Warnings, w => w.Contains("100201 工商银行", System.StringComparison.Ordinal));
        Assert.Equal(3, (await _client.GetFromJsonAsync<List<AccountModel>>(AccountsPath(id)))!.Count);
    }

    [Fact]
    public async Task Import_NoFilePartOrEmptyFile_GetsFriendlyRejectionWithoutLeakingStack()
    {
        var id = await _h.CreateAccountSetAsync("科目导入拦截");

        // 请求体里压根没有 file 段：到不了控制器的「请选择」文案（框架先把这个 multipart 判为无效请求），
        // 但必须是 4xx 客户端错误，不能是 500，也不能把异常细节吐给前端。
        var noFile = await _client.PostAsync($"{AccountsPath(id)}/import", new MultipartFormDataContent());
        Assert.True((int)noFile.StatusCode is >= 400 and < 500, $"期望 4xx，实际 {(int)noFile.StatusCode}");
        Assert.DoesNotContain("at YunCai", await noFile.Content.ReadAsStringAsync());

        // 有 file 段但是 0 字节：控制器的「请选择要导入的科目模板文件」这条真的会被命中。
        var zeroLength = new MultipartFormDataContent
        {
            { new ByteArrayContent([]), "file", "空.xlsx" },
        };
        var emptyResponse = await _client.PostAsync($"{AccountsPath(id)}/import", zeroLength);
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        Assert.Contains("请选择要导入的科目模板文件", await emptyResponse.Content.ReadAsStringAsync());

        // 文件名 .xlsx 但内容不是 Excel：NPOI 打开失败被读取器包装成业务异常，
        // 走 400 友好文案（提示用系统导出的模板），不把解析库的内部异常吐给前端。
        var garbage = new MultipartFormDataContent
        {
            { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("这不是 Excel")), "file", "坏文件.xlsx" },
        };
        var garbageResponse = await _client.PostAsync($"{AccountsPath(id)}/import", garbage);
        var garbageBody = await garbageResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, garbageResponse.StatusCode);
        Assert.Contains("无法识别的表格文件", garbageBody);
        Assert.DoesNotContain("YunCai.Modules", garbageBody);
    }

    /// <summary>POST /Accounts 的返回：状态码 + 原始响应体（断言中文文案）+ 201 时的科目模型。</summary>
    private sealed record CreateResult(HttpStatusCode Status, string Body, AccountModel? Account);

    private sealed record AccountModel(
        Guid Id,
        string Code,
        string Name,
        AccountCategory Category,
        BalanceDirection Direction,
        Guid? ParentId,
        int Level,
        bool IsLeaf,
        bool IsEnabled);

    private sealed record OpeningBalanceRowModel(
        string Code,
        string Name,
        bool IsLeaf,
        decimal OpeningBalance,
        decimal Debit,
        decimal Credit);

    private sealed record OpeningBalancesModel(
        List<OpeningBalanceRowModel> Rows,
        decimal TotalDebit,
        decimal TotalCredit,
        bool Balanced);

    private sealed record ImportResultModel(
        int TotalRows,
        int ImportedAccounts,
        int SkippedAccounts,
        List<string> Warnings);
}
