using System.Net;
using System.Net.Http.Json;
using YunCai.Modules.Accounting.Domain;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// 凭证 HTTP 集成测试：走真实组合根（认证、模型绑定、Problem Details、业务异常→400 中文文案全部生效），
/// 数据库换成内存 SQLite。锁住的是「HTTP 边界上的行为」，与 VoucherLifecycleTests（直接调用应用服务）互补：
/// - 制单人取自 JWT 的 displayName 声明，而不是请求体；
/// - 保存即入账：制单直接返回「正常」，不平衡在保存时就被 400 拦下；编辑走反过账 → 改 → 保存重新过账；
/// - 查询串里的日期能被 LenientDateOnlyModelBinder 接住（前端客户端会序列化成 ISO 带时间缀，默认绑定器会 400）；
/// - 软删除后列表默认不可见、?deleted=true 才见回收站；
/// - 凭证 PDF 由服务端渲染，返回 application/pdf 附件。
/// 每个用例一个独立账套（经 POST /AccountSets 创建，科目直写库），互不干扰。
/// </summary>
public sealed class VouchersApiTests : IDisposable
{
    private readonly ApiHarness _h = new();
    private readonly HttpClient _client;

    public VouchersApiTests() => _client = _h.Client;

    public void Dispose() => _h.Dispose();

    /// <summary>
    /// 建一个独立账套并播两个末级科目（库存现金 1001 借方 / 管理费用 6602 借方，
    /// 后者按旧准则属损益类，用作凭证的对方科目）。
    /// </summary>
    private async Task<(Guid AccountSetId, Guid CashId, Guid CounterId)> NewSeededAccountSetAsync(string name)
    {
        var accountSetId = await _h.CreateAccountSetAsync(name);
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var counter = _h.AddRootAccount(accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss);
        return (accountSetId, cash, counter);
    }

    private static string VouchersPath(Guid accountSetId) => ApiHarness.ResourcePath(accountSetId, "Vouchers");

    private static string Body(string word, string date, params (string Summary, Guid AccountId, decimal Debit, decimal Credit)[] entries)
    {
        var lines = entries.Select(e => new
        {
            summary = e.Summary,
            accountId = e.AccountId,
            debit = e.Debit,
            credit = e.Credit,
        });
        return System.Text.Json.JsonSerializer.Serialize(new { word, date, entries = lines });
    }

    /// <summary>把请求体按前端真实发出的 JSON 形态提交（application/json + UTF-8）。</summary>
    private static HttpContent JsonContent(string body) =>
        new StringContent(body, System.Text.Encoding.UTF8, "application/json");

    // —— 全生命周期 ——

    [Fact]
    public async Task Create_BalancedVoucher_IsPostedAndCanBeUnposted()
    {
        var (id, cash, counter) = await NewSeededAccountSetAsync("凭证保存即入账");

        var response = await _client.PostAsync(
            VouchersPath(id),
            JsonContent(Body("记", "2026-02-15", ("收现", cash, 100m, 0m), ("付现", counter, 0m, 100m))));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var posted = await response.Content.ReadFromJsonAsync<VoucherModel>();

        Assert.Equal(1, posted!.Number);
        Assert.Equal("记-1", posted.VoucherNo);
        // 保存即入账：不再有审核环节，制单返回的就是正常状态。
        Assert.Equal("正常", posted.StatusText);
        Assert.True(posted.IsBalanced);
        Assert.Equal(100m, posted.TotalDebit);
        Assert.Equal(100m, posted.TotalCredit);
        // 制单人来自令牌，请求体里没有这个字段。
        Assert.Equal(ApiHarness.TokenDisplayName, posted.Preparer);
        // CreatedAtAction 必须给出可回访的 Location，否则前端拿不到新建资源地址。
        Assert.Equal($"/accountsets/{id}/Vouchers/{posted.Id}", response.Headers.Location!.AbsolutePath);

        // 反过账退回草稿；再反一次必须被状态机拦下。
        var unposted = await _client.PostAsync($"{VouchersPath(id)}/{posted.Id}/unpost", null);
        Assert.Equal(HttpStatusCode.OK, unposted.StatusCode);
        Assert.Equal("暂存", (await unposted.Content.ReadFromJsonAsync<VoucherModel>())!.StatusText);

        var badUnpost = await _client.PostAsync($"{VouchersPath(id)}/{posted.Id}/unpost", null);
        Assert.Equal(HttpStatusCode.BadRequest, badUnpost.StatusCode);
        Assert.Contains("只有正常凭证才能反过账", await badUnpost.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_Unbalanced_Returns400AndPersistsNothing()
    {
        var (id, cash, counter) = await NewSeededAccountSetAsync("凭证不平衡");

        var response = await _client.PostAsync(
            VouchersPath(id),
            JsonContent(Body("记", "2026-02-15", ("收现", cash, 100m, 0m), ("付现", counter, 0m, 60m))));

        // 保存即入账，借贷平衡成为保存的硬性条件，不再落一张不平衡的草稿。
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("借贷不平衡", await response.Content.ReadAsStringAsync());
        Assert.Empty((await _client.GetFromJsonAsync<List<VoucherModel>>(VouchersPath(id)))!);
    }

    // —— 日期模型绑定（AGENTS.md 记录的陷阱）——

    [Fact]
    public async Task GetAll_IsoDateTimeWithTimeSuffix_IsBoundAndFiltersByDay()
    {
        var (id, cash, counter) = await NewSeededAccountSetAsync("凭证日期绑定");
        await _client.PostAsync(
            VouchersPath(id),
            JsonContent(Body("记", "2026-02-15", ("付办公费", cash, 100m, 0m), ("付办公费", counter, 0m, 100m))));

        // openapi-generator 生成的客户端把 DateOnly 序列化成 ISO 8601 带时间缀和 Z 尾
        // （前端用 toISOString().substring(0,10) 之前，查询串里就是这个形态）；
        // .NET 10 的默认 DateOnly 绑定能吃掉 "2026-02-15T00:00:00"，但带 Z 会直接 400，
        // 这里锁住 LenientDateOnlyModelBinder 对两种形态都生效（去掉注册本用例即变红）。
        foreach (var suffix in new[] { "T00:00:00", "T00:00:00.000Z" })
        {
            var withTime = await _client.GetAsync(
                $"{VouchersPath(id)}?startDate=2026-02-15{suffix}&endDate=2026-02-15{suffix}");
            Assert.Equal(HttpStatusCode.OK, withTime.StatusCode);
            Assert.Single((await withTime.Content.ReadFromJsonAsync<List<VoucherModel>>())!);
        }

        // 区间回退到 1 月时，同一张 2 月凭证必须被过滤掉，证明条件真的进了查询而不是被忽略。
        var january = await _client.GetAsync($"{VouchersPath(id)}?startDate=2026-01-01&endDate=2026-01-31");
        Assert.Equal(HttpStatusCode.OK, january.StatusCode);
        Assert.Empty((await january.Content.ReadFromJsonAsync<List<VoucherModel>>())!);
    }

    [Fact]
    public async Task GetAll_UnknownAccountSet_Returns400WithChineseMessage()
    {
        var response = await _client.GetAsync(VouchersPath(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("指定的账套不存在", await response.Content.ReadAsStringAsync());
    }

    // —— 删除 / 回收站 ——

    [Fact]
    public async Task Delete_PostedVoucher_Returns400AndKeepsItInList()
    {
        var (id, cash, counter) = await NewSeededAccountSetAsync("凭证已过账不可删");
        var created = await _client.PostAsync(
            VouchersPath(id),
            JsonContent(Body("记", "2026-02-15", ("付办公费", cash, 100m, 0m), ("付办公费", counter, 0m, 100m))));
        var voucher = await created.Content.ReadFromJsonAsync<VoucherModel>();

        var response = await _client.DeleteAsync($"{VouchersPath(id)}/{voucher!.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("已是正常状态，不能删除", await response.Content.ReadAsStringAsync());

        // 拒绝删除后凭证必须还在原位，不能被部分删除。
        var list = await _client.GetFromJsonAsync<List<VoucherModel>>(VouchersPath(id));
        Assert.Equal([voucher.Id], list!.Select(v => v.Id).ToList());
    }

    [Fact]
    public async Task Delete_Draft_HidesFromListButVisibleInRecycleBin()
    {
        var (id, cash, counter) = await NewSeededAccountSetAsync("凭证回收站");
        var created = await _client.PostAsync(
            VouchersPath(id),
            JsonContent(Body("记", "2026-02-15", ("付办公费", cash, 100m, 0m), ("付办公费", counter, 0m, 100m))));
        var voucher = await created.Content.ReadFromJsonAsync<VoucherModel>();
        // 制单即入账，移入回收站前必须先反过账退回暂存。
        await _client.PostAsync($"{VouchersPath(id)}/{voucher!.Id}/unpost", null);

        var response = await _client.DeleteAsync($"{VouchersPath(id)}/{voucher.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // 软删除：默认列表不再出现，回收站口径（deleted=true）才列出它。
        Assert.Empty((await _client.GetFromJsonAsync<List<VoucherModel>>(VouchersPath(id)))!);
        var recycleBin = await _client.GetFromJsonAsync<List<VoucherModel>>($"{VouchersPath(id)}?deleted=true");
        Assert.Equal([voucher.Id], recycleBin!.Select(v => v.Id).ToList());

        // 详情端点不按 deleted 过滤（VoucherRepository.GetByIdAsync 只按账套+id 取），
        // 回收站点开某张凭证查看/还原依赖这条口径，这里是有意为之的不对称。
        var detail = await _client.GetFromJsonAsync<VoucherModel>($"{VouchersPath(id)}/{voucher.Id}");
        Assert.Equal(voucher.Id, detail!.Id);
        Assert.Equal("暂存", detail.StatusText);

        // 还原后回到正常列表，状态仍是草稿。
        var restored = await _client.PostAsync($"{VouchersPath(id)}/{voucher.Id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal("暂存", (await restored.Content.ReadFromJsonAsync<VoucherModel>())!.StatusText);
        Assert.Single((await _client.GetFromJsonAsync<List<VoucherModel>>(VouchersPath(id)))!);
    }

    // —— 404 与 PDF ——

    [Fact]
    public async Task GetById_UnknownVoucher_Returns404()
    {
        var (id, _, _) = await NewSeededAccountSetAsync("凭证不存在");

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{VouchersPath(id)}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Pdf_ExistingVoucher_ReturnsPdfAttachment()
    {
        var (id, cash, counter) = await NewSeededAccountSetAsync("凭证打印");
        var created = await _client.PostAsync(
            VouchersPath(id),
            JsonContent(Body("记", "2026-02-15", ("付办公费", cash, 100m, 0m), ("付办公费", counter, 0m, 100m))));
        var voucher = await created.Content.ReadFromJsonAsync<VoucherModel>();

        var response = await _client.GetAsync($"{VouchersPath(id)}/{voucher!.Id}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        // 真的渲染出了 PDF 文件（%PDF 魔数），不是空串或 HTML 错误页。
        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));

        var missing = await _client.GetAsync($"{VouchersPath(id)}/{Guid.NewGuid()}/pdf");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("凭证不存在或已删除", await missing.Content.ReadAsStringAsync());
    }


    /// <summary>凭证端点的响应模型（字段名与生产 DTO 一致，camelCase 由 Web 默认选项匹配）。</summary>
    private sealed record VoucherModel(
        Guid Id,
        string Word,
        int Number,
        string VoucherNo,
        DateOnly Date,
        int Status,
        string StatusText,
        string Preparer,
        string? Reviewer,
        decimal TotalDebit,
        decimal TotalCredit,
        bool IsBalanced);
}
