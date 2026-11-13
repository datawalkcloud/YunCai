using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Assist.Domain;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// 账簿 HTTP 集成测试：一套「两级科目 + 双辅助核算 + 跨两个自然月」的数据，把九个账页与导出跑齐。
/// 断言盯住会计口径本身，而不是只要求 200：
/// - 明细账期初/本月合计/本年累计行齐备，滚动余额等于逐笔借贷累加；
/// - 上级科目（showOnlyLeaf=false）的发生额恒等于其下级之和；
/// - 序时账与凭证汇总表的借贷合计相等，且只认已过账凭证；
/// - 核算项目余额表按档案分别计全额，组合表主次类别交叉，账龄按七档归集；
/// - 每种账页都能导出非空 xlsx。
/// </summary>
public sealed class LedgersApiTests : IAsyncLifetime, IDisposable
{
    private readonly ApiHarness _h = new();
    private readonly HttpClient _client;

    private Guid _accountSetId;
    private Guid _cash;
    private Guid _ar;
    private Guid _arA;
    private Guid _arB;
    private Guid _exp;
    private Guid _expWage;
    private Guid _rev;
    private Guid _cap;
    private Guid _customerA;
    private Guid _vendorB;

    public LedgersApiTests() => _client = _h.Client;

    public void Dispose() => _h.Dispose();

    private string Ledgers(string path) => $"{ApiHarness.ResourcePath(_accountSetId, "Ledgers")}{path}";

    public async Task InitializeAsync()
    {
        _accountSetId = await _h.CreateAccountSetAsync("账簿联调");
        _cash = _h.AddRootAccount(_accountSetId, "1001", "库存现金");
        _ar = _h.AddRootAccount(_accountSetId, "1122", "应收账款");
        _arA = _h.AddChildAccount(_ar, "112201", "应收账款—甲");
        _arB = _h.AddChildAccount(_ar, "112202", "应收账款—乙");
        _exp = _h.AddRootAccount(_accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        _expWage = _h.AddChildAccount(_exp, "660201", "工资");
        _rev = _h.AddRootAccount(_accountSetId, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        _cap = _h.AddRootAccount(_accountSetId, "4001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);

        await PutJsonAsync($"/AccountSets/{_accountSetId}/assist-types", new[] { (int)AssistType.Customer, (int)AssistType.Vendor });
        _customerA = await CreateAssistItemAsync(AssistType.Customer, "0001", "甲公司");
        _vendorB = await CreateAssistItemAsync(AssistType.Vendor, "0002", "乙工厂");
        await PutJsonAsync($"{ApiHarness.ResourcePath(_accountSetId, "Accounts")}/{_arA}/assist-types", new[]
        {
            new { type = (int)AssistType.Customer, isRequired = false },
            new { type = (int)AssistType.Vendor, isRequired = false },
        });

        // 1 月：赊销挂客户+供应商；收款冲销
        await PostVoucherAsync("2026-01-05", new[]
        {
            ("赊销甲公司产品", _arA, 1000m, 0m, new[] { (_customerA, (int)AssistType.Customer), (_vendorB, (int)AssistType.Vendor) }),
            ("赊销甲公司产品", _rev, 0m, 1000m, Array.Empty<(Guid, int)>()),
        });
        await PostVoucherAsync("2026-01-31", new[]
        {
            ("收到甲公司货款", _cash, 500m, 0m, Array.Empty<(Guid, int)>()),
            ("收到甲公司货款", _arA, 0m, 500m, new[] { (_customerA, (int)AssistType.Customer) }),
        });
        // 2 月：费用 + 投入资本
        await PostVoucherAsync("2026-02-10", new[]
        {
            ("发放工资", _expWage, 300m, 0m, Array.Empty<(Guid, int)>()),
            ("发放工资", _cash, 0m, 300m, Array.Empty<(Guid, int)>()),
        });
        await PostVoucherAsync("2026-02-20", new[]
        {
            ("股东投入", _cash, 2000m, 0m, Array.Empty<(Guid, int)>()),
            ("股东投入", _cap, 0m, 2000m, Array.Empty<(Guid, int)>()),
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Subsidiary_RollsBalanceAndInsertsMonthAndYearTotals()
    {
        var ledger = await GetAsync<SubsidiaryLedger>(Ledgers("/subsidiary?startDate=2026-01-01&endDate=2026-02-28&accountFrom=1001&accountTo=1122"));

        // 应收账款只有 1 月发生额（1122 上级取其下级 112201）：1、2 月各一组合计行，2 月合计为 0
        var ar = ledger.Accounts.Single(a => a.Code == "112201");
        Assert.Equal(1000m, ar.TotalDebit);
        Assert.Equal(500m, ar.TotalCredit);
        Assert.Single(ar.Rows, r => r.Kind == 1);
        Assert.Equal(2, ar.Rows.Count(r => r.Kind == 0));
        Assert.Equal(2, ar.Rows.Count(r => r.Kind == 2));
        Assert.Equal(2, ar.Rows.Count(r => r.Kind == 3));
        var febMonth = ar.Rows.Last(r => r.Kind == 2);
        Assert.Equal("2026-02-28", febMonth.Date);
        Assert.Equal(0m, febMonth.Debit);
        Assert.Equal(500m, febMonth.Balance);

        // 滚动余额必须等于逐笔借贷的净额累加
        var running = 0m;
        foreach (var row in ar.Rows.Where(r => r.Kind == 0))
        {
            running += row.Debit - row.Credit;
            Assert.Equal(running, row.Balance!.Value);
        }
        Assert.Equal(500m, running);

        // 库存现金跨两个月：每月一组「本月合计 + 本年累计」，末组累计等于全区间发生额
        var cash = ledger.Accounts.Single(a => a.Code == "1001");
        Assert.Equal(2, cash.Rows.Count(r => r.Kind == 2));
        Assert.Equal(2, cash.Rows.Count(r => r.Kind == 3));
        var cashYear = cash.Rows.Last(r => r.Kind == 3);
        Assert.Equal(2500m, cash.Rows.Where(r => r.Kind == 0).Sum(r => r.Debit));
        Assert.Equal(2500m, cashYear.Debit);
        Assert.Equal(300m, cashYear.Credit);
        Assert.Equal("本年累计", cashYear.Summary);
        Assert.Equal("本月合计", cash.Rows.Last(r => r.Kind == 2).Summary);
    }

    [Fact]
    public async Task Subsidiary_RangeStartingMidYearRollsOpeningAndYearTotal()
    {
        // 区间从年中开始时：期初已含之前月份的余额，本年累计仍从 1 月起算
        var ledger = await GetAsync<SubsidiaryLedger>(
            Ledgers("/subsidiary?startDate=2026-02-01&endDate=2026-02-28&accountFrom=1001&accountTo=1001"));
        var cash = ledger.Accounts.Single(a => a.Code == "1001");

        var opening = cash.Rows.Single(r => r.Kind == 1);
        Assert.Equal("2026-02-01", opening.Date);
        Assert.Equal(500m, opening.Balance);
        // 期初行借贷两栏是年初至期初的累计发生额（1 月收到货款 500）
        Assert.Equal(500m, opening.Debit);
        Assert.Equal(0m, opening.Credit);

        var entries = cash.Rows.Where(r => r.Kind == 0).ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal(200m, entries[0].Balance);
        Assert.Equal(2200m, entries[1].Balance);

        var month = cash.Rows.Single(r => r.Kind == 2);
        Assert.Equal(2000m, month.Debit);
        Assert.Equal(300m, month.Credit);
        Assert.Equal(2200m, month.Balance);

        var year = cash.Rows.Single(r => r.Kind == 3);
        Assert.Equal(2500m, year.Debit);
        Assert.Equal(300m, year.Credit);
        Assert.Equal(2200m, year.Balance);
        Assert.Equal("2026-02-28", year.Date);
    }

    [Fact]
    public async Task Subsidiary_SummaryKeywordKeepsBalanceButFiltersRows()
    {
        // 摘要关键字只影响列示行，不改变发生额合计与余额（会计口径的硬要求）
        var all = await GetAsync<SubsidiaryLedger>(Ledgers("/subsidiary?startDate=2026-01-01&endDate=2026-01-31&accountFrom=1122&accountTo=1122"));
        var filtered = await GetAsync<SubsidiaryLedger>(
            Ledgers("/subsidiary?startDate=2026-01-01&endDate=2026-01-31&accountFrom=1122&accountTo=1122&summaryKeyword=赊销"));

        Assert.Equal(all.Accounts[0].TotalDebit, filtered.Accounts[0].TotalDebit);
        Assert.Single(filtered.Accounts[0].Rows, r => r.Kind == 0);
        Assert.Equal(2, all.Accounts[0].Rows.Count(r => r.Kind == 0));
    }

    [Fact]
    public async Task General_ParentEqualsSumOfChildren()
    {
        var ledger = await GetAsync<GeneralLedger>(Ledgers("/general?startDate=2026-01-01&endDate=2026-02-28&showOnlyLeaf=false"));
        var parent = ledger.Accounts.Single(a => a.Code == "1122");
        var child = ledger.Accounts.Single(a => a.Code == "112201");

        Assert.Equal(child.TotalDebit, parent.TotalDebit);
        Assert.Equal(child.TotalCredit, parent.TotalCredit);

        // 1122 只有 1 月发生额：2 月仍占一行且期末延续上月，保证余额连续
        Assert.Equal(2, parent.Rows.Length);
        Assert.Equal(parent.Rows[0].EndingDebit, parent.Rows[1].OpeningDebit);
        Assert.Equal(500m, parent.Rows[1].EndingDebit);
    }

    [Fact]
    public async Task Journal_AndVoucherSummary_AreBalancedAndPostedOnly()
    {
        var journal = await GetAsync<JsonElement>(Ledgers("/journal?startDate=2026-01-01&endDate=2026-02-28"));
        Assert.Equal(journal.GetProperty("totalDebit").GetDecimal(), journal.GetProperty("totalCredit").GetDecimal());
        Assert.Equal(3800m, journal.GetProperty("totalDebit").GetDecimal());
        Assert.Equal(4, journal.GetProperty("voucherCount").GetInt32());

        // 凭证汇总表默认只列末级科目，借贷合计必然相等
        var leaf = await GetAsync<VoucherSummary>(Ledgers("/voucher-summary?startDate=2026-01-01&endDate=2026-02-28"));
        Assert.Equal(leaf.Rows.Sum(r => r.Debit), leaf.Rows.Sum(r => r.Credit));
        Assert.Equal(4, leaf.VoucherCount);
        Assert.Equal(2500m, leaf.Rows.Single(r => r.Code == "1001").Debit);
        Assert.Equal(300m, leaf.Rows.Single(r => r.Code == "1001").Credit);

        // showOnlyLeaf=false 时上级科目汇总其全部下级的发生额
        var rolled = await GetAsync<VoucherSummary>(Ledgers("/voucher-summary?startDate=2026-01-01&endDate=2026-02-28&showOnlyLeaf=false"));
        Assert.Equal(1000m, rolled.Rows.Single(r => r.Code == "1122").Debit);
        Assert.Equal(500m, rolled.Rows.Single(r => r.Code == "1122").Credit);
        // 页面按 parentCode 把平表挂成树：下级指向上级，一级科目为空
        Assert.Equal("1122", rolled.Rows.Single(r => r.Code == "112201").ParentCode);
        Assert.Null(rolled.Rows.Single(r => r.Code == "1122").ParentCode);
    }

    [Fact]
    public async Task UnpostedDraftIsReportedInMetaAndExcludedFromRows()
    {
        // 再制一张不过账的草稿：账页不列它，但 meta 要把未过账张数透出给页面提示
        await PostVoucherAsync("2026-02-25", new[]
        {
            ("草稿不提", _cash, 777m, 0m, Array.Empty<(Guid, int)>()),
            ("草稿不提", _cap, 0m, 777m, Array.Empty<(Guid, int)>()),
        }, post: false);

        var ledger = await GetAsync<SubsidiaryLedger>(Ledgers("/subsidiary?startDate=2026-02-01&endDate=2026-02-28&accountFrom=1001&accountTo=1001"));
        Assert.Equal(1, ledger.Meta.UnpostedVoucherCount);
        Assert.DoesNotContain(ledger.Accounts[0].Rows, r => r.Summary == "草稿不提");
    }

    [Fact]
    public async Task SoftDeletedDraftIsNotReportedInUnpostedMeta()
    {
        // 回收站里的草稿不算「未入账草稿」：提示只应指向仍可打开保存的凭证，
        // 否则用户按提示跳过去看不到任何待处理单据。
        var draftId = await PostVoucherAsync("2026-02-26", new[]
        {
            ("已删草稿", _cash, 888m, 0m, Array.Empty<(Guid, int)>()),
            ("已删草稿", _cap, 0m, 888m, Array.Empty<(Guid, int)>()),
        }, post: false);

        var before = await GetAsync<SubsidiaryLedger>(Ledgers("/subsidiary?startDate=2026-02-01&endDate=2026-02-28&accountFrom=1001&accountTo=1001"));
        Assert.Equal(1, before.Meta.UnpostedVoucherCount);

        var deleted = await _client.DeleteAsync($"{ApiHarness.ResourcePath(_accountSetId, "Vouchers")}/{draftId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var after = await GetAsync<SubsidiaryLedger>(Ledgers("/subsidiary?startDate=2026-02-01&endDate=2026-02-28&accountFrom=1001&accountTo=1001"));
        Assert.Equal(0, after.Meta.UnpostedVoucherCount);
    }

    [Fact]
    public async Task AssistBalance_PerItemCarriesFullAmount()
    {
        var balance = await GetAsync<AssistBalance>(
            Ledgers($"/assist-balance?assistType={(int)AssistType.Customer}&startDate=2026-01-01&endDate=2026-02-28"));
        Assert.Equal("客户", balance.AssistTypeText);
        var row = balance.Accounts.Single(a => a.Code == "112201").Rows.Single(r => r.Name.Contains("甲公司"));
        Assert.Equal(1000m, row.PeriodDebit);
        Assert.Equal(500m, row.PeriodCredit);
        Assert.Equal(500m, row.EndingDebit);
    }

    [Fact]
    public async Task AssistSubsidiaryAndCombine_CrossReferenceBothTypes()
    {
        var subsidiary = await GetAsync<AssistSubsidiary>(
            Ledgers($"/assist-subsidiary?assistType={(int)AssistType.Customer}&startDate=2026-01-01&endDate=2026-02-28"));
        var customerItem = subsidiary.Accounts
            .Single(a => a.Code == "112201")
            .Items
            .Single(i => i.Name.Contains("甲公司"));
        Assert.Equal(1000m, customerItem.TotalDebit);
        Assert.Equal(500m, customerItem.TotalCredit);
        // 档案段复用明细账的行结构：期初 + 两笔逐行（均在 1 月）+ 1、2 月各一组「本月合计 + 本年累计」
        Assert.Single(customerItem.Rows, r => r.Kind == 1);
        Assert.Equal(2, customerItem.Rows.Count(r => r.Kind == 0));
        Assert.Equal(2, customerItem.Rows.Count(r => r.Kind == 2));
        Assert.Equal(2, customerItem.Rows.Count(r => r.Kind == 3));
        // 末笔余额等于「借 - 贷」净额，与明细账同一口径
        Assert.Equal(500m, customerItem.Rows.Single(r => r.Kind == 0 && r.Debit == 0m).Balance);

        var combine = await GetAsync<AssistCombine>(
            Ledgers($"/assist-combine?assistType={(int)AssistType.Customer}&secondaryAssistType={(int)AssistType.Vendor}"
                + "&startDate=2026-01-01&endDate=2026-02-28"));
        Assert.Equal("客户", combine.AssistTypeText);
        Assert.Equal("供应商", combine.SecondaryAssistTypeText);
        // 唯一同时挂客户与供应商的分录：甲公司 × 乙工厂 = 1000 借方
        var group = Assert.Single(combine.Groups);
        Assert.Equal(1000m, group.TotalDebit);
        Assert.Equal(0m, group.TotalCredit);
        var cell = Assert.Single(group.Cells, c => c.Name.Contains("乙工厂"));
        Assert.Equal(1000m, cell.Debit);
        // 只有同时挂主、次两类档案的分录才进组合表：贷方那笔没挂供应商，因此不进表
        Assert.Equal(1000m, combine.TotalDebit);
        Assert.Equal(0m, combine.TotalCredit);
    }

    [Fact]
    public async Task MultiColumn_SpreadsChildAccountsIntoColumns()
    {
        var multi = await GetAsync<MultiColumn>(
            Ledgers($"/multi-column?accountId={_exp}&startDate=2026-01-01&endDate=2026-02-28"));
        Assert.Equal("6602", multi.Code);
        Assert.Contains("工资", multi.Columns.Select(c => c.Name));
        Assert.Equal(300m, multi.TotalDebit);
    }

    [Fact]
    public async Task Ageing_ReturnsSevenBucketsAndCarriesTheWarningNote()
    {
        var ageing = await GetAsync<Ageing>(
            Ledgers($"/ageing?assistType={(int)AssistType.Customer}&startDate=2026-01-01&endDate=2026-02-28"));
        Assert.Equal(7, ageing.BucketLabels.Length);
        Assert.False(string.IsNullOrWhiteSpace(ageing.Note));
        Assert.Equal("2026-02-28", ageing.AsOfDate);
    }

    [Theory]
    [InlineData("subsidiary")]
    [InlineData("general")]
    [InlineData("journal")]
    [InlineData("voucher-summary")]
    [InlineData("assist-balance")]
    [InlineData("assist-subsidiary")]
    [InlineData("assist-combine")]
    [InlineData("multi-column")]
    [InlineData("ageing")]
    public async Task Export_ProducesNonEmptyWorkbook(string kind)
    {
        var assist = kind.StartsWith("assist") || kind == "ageing" ? $"&assistType={(int)AssistType.Customer}&secondaryAssistType={(int)AssistType.Vendor}" : string.Empty;
        var column = kind == "multi-column" ? $"&accountId={_exp}" : string.Empty;
        var response = await _client.GetAsync(
            Ledgers($"/{kind}/export?startDate=2026-01-01&endDate=2026-02-28{assist}{column}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        // xlsx 是 zip 容器，头两字节固定为 PK
        Assert.True(bytes.Length > 100, $"{kind} 导出内容过短：{bytes.Length} 字节");
        Assert.Equal(new byte[] { 0x50, 0x4B }, bytes[..2]);
    }

    [Fact]
    public async Task Ledger_InvalidDateRangeReturnsChineseMessage()
    {
        var response = await _client.GetAsync(Ledgers("/subsidiary?startDate=2026-03-01&endDate=2026-01-01"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Parameter '", body);
    }

    private async Task PutJsonAsync(string path, object body)
    {
        var response = await _client.PutAsJsonAsync(path, body);
        Assert.True(response.IsSuccessStatusCode, $"{path} → {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<Guid> CreateAssistItemAsync(AssistType type, string code, string name)
    {
        var response = await _client.PostAsJsonAsync(
            ApiHarness.ResourcePath(_accountSetId, "AssistItems"),
            new { type = (int)type, code, name });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>制单（可带辅助核算）；post=false 时反过账退回草稿，用于验证账簿只认已过账凭证。</summary>
    private async Task<Guid> PostVoucherAsync(
        string date,
        (string Summary, Guid AccountId, decimal Debit, decimal Credit, (Guid ItemId, int Type)[] Assists)[] entries,
        bool post = true)
    {
        var body = JsonSerializer.Serialize(new
        {
            word = "记",
            date,
            entries = entries.Select(e => new
            {
                summary = e.Summary,
                accountId = e.AccountId,
                debit = e.Debit,
                credit = e.Credit,
                assists = e.Assists.Select(a => new { type = a.Type, itemId = a.ItemId }).ToArray(),
            }),
        });
        var response = await _client.PostAsync(
            ApiHarness.ResourcePath(_accountSetId, "Vouchers"),
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        var created = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, created);

        using var doc = JsonDocument.Parse(created);
        var voucherId = doc.RootElement.GetProperty("id").GetGuid();
        if (!post)
        {
            await _h.UnpostVoucherAsync(_accountSetId, voucherId);
        }

        return voucherId;
    }

    private async Task<T> GetAsync<T>(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} → {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private sealed record LedgerMeta(string StartDate, string EndDate, int UnpostedVoucherCount);

    private sealed record SubsidiaryRow(int Kind, string? Date, string? Summary, decimal Debit, decimal Credit, decimal? Balance);

    private sealed record SubsidiaryAccount(string Code, string Name, decimal TotalDebit, decimal TotalCredit, SubsidiaryRow[] Rows);

    private sealed record SubsidiaryLedger(LedgerMeta Meta, SubsidiaryAccount[] Accounts);

    private sealed record GeneralRow(string PeriodText, decimal OpeningDebit, decimal Debit, decimal Credit, decimal EndingDebit, decimal EndingCredit);

    private sealed record GeneralAccount(string Code, string Name, decimal TotalDebit, decimal TotalCredit, GeneralRow[] Rows);

    private sealed record GeneralLedger(LedgerMeta Meta, GeneralAccount[] Accounts);

    private sealed record SummaryRow(string Code, string Name, string? ParentCode, decimal Debit, decimal Credit);

    private sealed record VoucherSummary(LedgerMeta Meta, int VoucherCount, SummaryRow[] Rows);

    private sealed record ItemRow(string Name, decimal PeriodDebit, decimal PeriodCredit, decimal EndingDebit, decimal EndingCredit);

    private sealed record ItemAccount(string Code, string Name, ItemRow[] Rows);

    private sealed record AssistBalance(LedgerMeta Meta, string AssistTypeText, ItemAccount[] Accounts);

    private sealed record AssistSubsidiaryItem(string Code, string Name, decimal TotalDebit, decimal TotalCredit, SubsidiaryRow[] Rows);

    private sealed record AssistSubsidiaryAccount(string Code, string Name, AssistSubsidiaryItem[] Items);

    private sealed record AssistSubsidiary(LedgerMeta Meta, string AssistTypeText, AssistSubsidiaryAccount[] Accounts);

    private sealed record CombineCell(string Code, string Name, decimal Debit, decimal Credit);

    private sealed record CombineGroup(string Code, string Name, CombineCell[] Cells, decimal TotalDebit, decimal TotalCredit);

    private sealed record AssistCombine(LedgerMeta Meta, string AssistTypeText, string SecondaryAssistTypeText, CombineGroup[] Groups, decimal TotalDebit, decimal TotalCredit);

    private sealed record Column(string Code, string Name);

    private sealed record MultiColumn(LedgerMeta Meta, string Code, string Name, Column[] Columns, decimal TotalDebit, decimal TotalCredit);

    private sealed record Ageing(LedgerMeta Meta, string AssistTypeText, string AsOfDate, string[] BucketLabels, string Note);
}
