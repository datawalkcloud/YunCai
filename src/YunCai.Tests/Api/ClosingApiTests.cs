using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YunCai.Modules.Accounting.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// 期末结转/结账的 HTTP 集成测试，锁住「回收站不参与期末计算」这条口径：
/// 软删除的草稿在凭证列表里已不可见，若结转仍把它算作「未过账」，
/// 用户删掉一张草稿就会让该期间永久无法结转损益、无法结账。
/// </summary>
public sealed class ClosingApiTests : IDisposable
{
    private readonly ApiHarness _h = new();
    private readonly HttpClient _client;

    public ClosingApiTests() => _client = _h.Client;

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task CarryForward_IgnoresRecycledDraft()
    {
        var accountSetId = await _h.CreateAccountSetAsync("期末回收站");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var revenue = _h.AddRootAccount(
            accountSetId, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10", (cash, 100m, 0m), (revenue, 0m, 100m));

        // 制一张草稿并删除（进回收站），它不应再挡结转
        var draft = await _h.CreateDraftVoucherAsync(accountSetId, "2026-01-11", (cash, 50m, 0m), (revenue, 0m, 50m));
        var recycle = await _client.DeleteAsync($"{ApiHarness.ResourcePath(accountSetId, "Vouchers")}/{draft.Id}");
        Assert.Equal(HttpStatusCode.NoContent, recycle.StatusCode);

        var checks = await _client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/checks?year=2026&month=1");
        checks.EnsureSuccessStatusCode();
        var checkBody = await checks.Content.ReadAsStringAsync();
        Assert.DoesNotContain("存在 1 张未过账凭证", checkBody);

        var carry = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward",
            new { year = 2026, month = 1, splitIncomeCost = false, transferToRetainedEarnings = false });
        Assert.True(carry.StatusCode == HttpStatusCode.OK, await carry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CarryForward_BlocksWhileLiveDraftExists()
    {
        // 反向对照：草稿没删掉时仍然必须拦住，否则上面的用例就成了「无条件放行」的空断言。
        var accountSetId = await _h.CreateAccountSetAsync("期末未过账");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var revenue = _h.AddRootAccount(
            accountSetId, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10", (cash, 100m, 0m), (revenue, 0m, 100m));
        await _h.CreateDraftVoucherAsync(accountSetId, "2026-01-11", (cash, 50m, 0m), (revenue, 0m, 50m));

        var carry = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward",
            new { year = 2026, month = 1, splitIncomeCost = false, transferToRetainedEarnings = false });
        Assert.Equal(HttpStatusCode.BadRequest, carry.StatusCode);
        Assert.Contains("未过账凭证", await carry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Close_BlocksWhileRecycledDraftExists()
    {
        var accountSetId = await _h.CreateAccountSetAsync("结账回收站");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var revenue = _h.AddRootAccount(
            accountSetId, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10", (cash, 100m, 0m), (revenue, 0m, 100m));
        var draft = await _h.CreateDraftVoucherAsync(accountSetId, "2026-01-11", (cash, 50m, 0m), (revenue, 0m, 50m));
        await _client.DeleteAsync($"{ApiHarness.ResourcePath(accountSetId, "Vouchers")}/{draft.Id}");

        var carry = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward",
            new { year = 2026, month = 1, splitIncomeCost = false, transferToRetainedEarnings = false });
        carry.EnsureSuccessStatusCode();

        var close = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/close", new { year = 2026, month = 1 });
        Assert.True(close.StatusCode == HttpStatusCode.OK, await close.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// 已结转期间的第 2 步必须能回显结转凭证，且摘要按后缀识别——
    /// 外部系统导入的结转凭证摘要是「1月  结转损益」，不是系统自身的「期末结转损益」。
    /// </summary>
    [Fact]
    public async Task CarryPreview_ShowsImportedCarryVoucher()
    {
        var accountSetId = await _h.CreateAccountSetAsync("期末导入摘要");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var expense = _h.AddRootAccount(
            accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var profit = _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10", (expense, 0.01m, 0m), (cash, 0m, 0.01m));

        var imported = await _client.PostAsJsonAsync(
            ApiHarness.ResourcePath(accountSetId, "Vouchers"),
            new
            {
                word = "记",
                date = "2026-01-31",
                entries = new[]
                {
                    new { summary = "1月  结转损益", accountId = expense, debit = 0m, credit = 0.01m },
                    new { summary = "1月  结转损益", accountId = profit, debit = 0.01m, credit = 0m },
                },
            });
        imported.EnsureSuccessStatusCode();

        using (var db = _h.NewContext())
        {
            var accountSet = db.AccountSets.Single(a => a.Id == accountSetId);
            accountSet.MarkCarried(2026, 1);
            db.SaveChanges();
        }

        var preview = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward/preview",
            new { year = 2026, month = 1, splitIncomeCost = false, transferToRetainedEarnings = false });
        preview.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.True(root.GetProperty("alreadyCarried").GetBoolean());
        var vouchers = root.GetProperty("vouchers");
        Assert.Equal(1, vouchers.GetArrayLength());
        Assert.Equal("1月  结转损益", vouchers[0].GetProperty("lines")[0].GetProperty("summary").GetString());
        // 费用 0.01 是净亏损；与未结转分支的 ProfitAmount 口径一致。
        Assert.Equal(-0.01m, root.GetProperty("profitAmount").GetDecimal());
    }

    /// <summary>
    /// 已结转的 12 月要能补做「本年利润 → 未分配利润」：生成后不重复生成，
    /// 且预览同步回显余额与已生成标记（导入数据没有这张凭证，需要补）。
    /// </summary>
    [Fact]
    public async Task RetainedEarnings_GeneratesOnceForCarriedDecember()
    {
        var accountSetId = await _h.CreateAccountSetAsync("年末补做");
        var expense = _h.AddRootAccount(
            accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var profit = _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);
        _h.AddRootAccount(
            accountSetId, "4104", "利润分配-未分配利润", AccountCategory.Equity, BalanceDirection.Credit);

        // 12 月结转损益后：借 本年利润 0.82 / 贷 管理费用 0.82（全年亏损 0.82）。
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-12-31", (profit, 0.82m, 0m), (expense, 0m, 0.82m));
        using (var db = _h.NewContext())
        {
            var accountSet = db.AccountSets.Single(a => a.Id == accountSetId);
            // 结转进度须逐月推进到 12 月。
            for (var month = 1; month <= 12; month++)
            {
                accountSet.MarkCarried(2026, month);
            }

            db.SaveChanges();
        }

        var preview = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward/preview",
            new { year = 2026, month = 12, splitIncomeCost = false, transferToRetainedEarnings = false });
        preview.EnsureSuccessStatusCode();
        using (var body = JsonDocument.Parse(await preview.Content.ReadAsStringAsync()))
        {
            var root = body.RootElement;
            Assert.True(root.GetProperty("alreadyCarried").GetBoolean());
            Assert.False(root.GetProperty("hasRetainedVoucher").GetBoolean());
            Assert.Equal(-0.82m, root.GetProperty("retainedProfitAmount").GetDecimal());
        }

        var transfer = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/retained-earnings", new { year = 2026, month = 12 });
        Assert.True(transfer.StatusCode == HttpStatusCode.OK, await transfer.Content.ReadAsStringAsync());
        using (var body = JsonDocument.Parse(await transfer.Content.ReadAsStringAsync()))
        {
            var root = body.RootElement;
            Assert.Contains("转入未分配利润", root.GetProperty("message").GetString());
            Assert.Equal("Retained", root.GetProperty("voucher").GetProperty("kind").GetString());
        }

        // 重复调用不应再生凭证。
        var again = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/retained-earnings", new { year = 2026, month = 12 });
        Assert.True(again.StatusCode == HttpStatusCode.OK, await again.Content.ReadAsStringAsync());
        using (var body = JsonDocument.Parse(await again.Content.ReadAsStringAsync()))
        {
            Assert.Contains("无需重复生成", body.RootElement.GetProperty("message").GetString());
        }

        var after = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward/preview",
            new { year = 2026, month = 12, splitIncomeCost = false, transferToRetainedEarnings = false });
        after.EnsureSuccessStatusCode();
        using var afterBody = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        var afterRoot = afterBody.RootElement;
        Assert.True(afterRoot.GetProperty("hasRetainedVoucher").GetBoolean());
        Assert.Equal(0m, afterRoot.GetProperty("retainedProfitAmount").GetDecimal());
        Assert.Contains(
            afterRoot.GetProperty("vouchers").EnumerateArray(),
            v => v.GetProperty("kind").GetString() == "Retained");
    }

    /// <summary>
    /// 新旧准则各建了一个「本年利润」科目时（4103 与 3103），年末结转要把两边余额合计；
    /// 只取其中一个科目会让未分配利润少转另一部分。
    /// </summary>
    [Fact]
    public async Task RetainedEarnings_SumsBothProfitAccounts()
    {
        var accountSetId = await _h.CreateAccountSetAsync("年末双本年利润");
        var revenue = _h.AddRootAccount(
            accountSetId, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var oldProfit = _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);
        var newProfit = _h.AddRootAccount(
            accountSetId, "3103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);
        var retained = _h.AddRootAccount(
            accountSetId, "4104", "利润分配-未分配利润", AccountCategory.Equity, BalanceDirection.Credit);

        // 11 月结转落 4103（贷 0.01），12 月结转落 3103（贷 0.81），年末应合计 0.82。
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-11-30", (revenue, 0.01m, 0m), (oldProfit, 0m, 0.01m));
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-12-31", (revenue, 0.81m, 0m), (newProfit, 0m, 0.81m));
        using (var db = _h.NewContext())
        {
            var accountSet = db.AccountSets.Single(a => a.Id == accountSetId);
            for (var month = 1; month <= 12; month++)
            {
                accountSet.MarkCarried(2026, month);
            }

            db.SaveChanges();
        }

        var preview = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward/preview",
            new { year = 2026, month = 12, splitIncomeCost = false, transferToRetainedEarnings = false });
        preview.EnsureSuccessStatusCode();
        using (var body = JsonDocument.Parse(await preview.Content.ReadAsStringAsync()))
        {
            var root = body.RootElement;
            Assert.Equal(0.82m, root.GetProperty("retainedProfitAmount").GetDecimal());
            var lines = root.GetProperty("retainedVoucher").GetProperty("lines").EnumerateArray().ToList();
            Assert.Contains(lines, l => l.GetProperty("accountCode").GetString() == "4103"
                                        && l.GetProperty("debit").GetDecimal() == 0.01m);
            Assert.Contains(lines, l => l.GetProperty("accountCode").GetString() == "3103"
                                        && l.GetProperty("debit").GetDecimal() == 0.81m);
            Assert.Contains(lines, l => l.GetProperty("accountCode").GetString() == "4104"
                                        && l.GetProperty("credit").GetDecimal() == 0.82m);
        }

        var transfer = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/retained-earnings", new { year = 2026, month = 12 });
        Assert.True(transfer.StatusCode == HttpStatusCode.OK, await transfer.Content.ReadAsStringAsync());

        using (var db = _h.NewContext())
        {
            var voucher = db.Vouchers
                .Include(v => v.Entries)
                .Single(v => v.AccountSetId == accountSetId && v.Entries.Any(e => e.AccountId == retained));
            Assert.Equal(0.01m, voucher.Entries.Single(e => e.AccountId == oldProfit).Debit);
            Assert.Equal(0.81m, voucher.Entries.Single(e => e.AccountId == newProfit).Debit);
            Assert.Equal(0.82m, voucher.Entries.Single(e => e.AccountId == retained).Credit);
        }
    }

    /// <summary>
    /// 第 1 步先生成「计提所得税」后，第 2 步结转要把 5801 一并结平，本年利润只包含税后利润；
    /// 同一组数据（收入 0.01、费用净额 0.73、所得税 0.19）结转出「贷 本年利润 0.55」。
    /// </summary>
    [Fact]
    public async Task CarryForward_IncludesGeneratedIncomeTaxAccrual()
    {
        var accountSetId = await _h.CreateAccountSetAsync("计提所得税结转");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var revenue = _h.AddRootAccount(
            accountSetId, "6301", "营业外收入-政府补助", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var expense = _h.AddRootAccount(
            accountSetId, "6602", "管理费用-办公费", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var taxExpense = _h.AddRootAccount(
            accountSetId, "5801", "所得税费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        _h.AddRootAccount(accountSetId, "2221", "应交所得税", AccountCategory.Liability, BalanceDirection.Credit);
        var profit = _h.AddRootAccount(
            accountSetId, "3103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-20", (cash, 0.01m, 0m), (revenue, 0m, 0.01m));
        // 退库退款：现金借方 0.73、管理费用贷方 0.73，费用净额为贷 0.73。
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-21", (cash, 0.73m, 0m), (expense, 0m, 0.73m));

        var accrual = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/pre-close/generate",
            new { year = 2026, month = 1, key = "income-tax" });
        Assert.True(accrual.StatusCode == HttpStatusCode.OK, await accrual.Content.ReadAsStringAsync());
        using (var body = JsonDocument.Parse(await accrual.Content.ReadAsStringAsync()))
        {
            Assert.Contains("所得税", body.RootElement.GetProperty("message").GetString());
        }

        var preview = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward/preview",
            new { year = 2026, month = 1, splitIncomeCost = false, transferToRetainedEarnings = false });
        preview.EnsureSuccessStatusCode();
        using (var body = JsonDocument.Parse(await preview.Content.ReadAsStringAsync()))
        {
            var voucher = body.RootElement.GetProperty("vouchers")[0];
            // 合计借 0.01 / 贷 0.01（负数金额与原方向相抵）。
            Assert.Equal(0.01m, voucher.GetProperty("totalDebit").GetDecimal());
            Assert.Equal(0.01m, voucher.GetProperty("totalCredit").GetDecimal());
            var lines = voucher.GetProperty("lines").EnumerateArray().ToList();
            // 本年利润排首行；收入类走借方、费用类走贷方，异常余额以红字（负数）留在原方向。
            Assert.Equal(profit, lines[0].GetProperty("accountId").GetGuid());
            Assert.Equal(0.55m, lines[0].GetProperty("credit").GetDecimal());
            Assert.Equal(0.55m, lines[0].GetProperty("balance").GetDecimal());
            Assert.Contains(lines, l => l.GetProperty("accountId").GetGuid() == revenue && l.GetProperty("debit").GetDecimal() == 0.01m);
            Assert.Contains(lines, l => l.GetProperty("accountId").GetGuid() == expense && l.GetProperty("credit").GetDecimal() == -0.73m);
            Assert.Contains(lines, l => l.GetProperty("accountId").GetGuid() == taxExpense && l.GetProperty("credit").GetDecimal() == 0.19m);
            // 损益类科目过账后余额归零（余额列）。
            Assert.All(
                lines.Where(l => l.GetProperty("accountId").GetGuid() != profit),
                l => Assert.Equal(0m, l.GetProperty("balance").GetDecimal()));
        }
    }

    /// <summary>
    /// 计提所得税按累计口径：1 月已结转并计提 0.19 后，3 月再实现利润 2.50，
    /// 本期补提 = 本年累计 3.24 × 25% − 0.19 = 0.62（口径），
    /// 而不是只看结转后剩余的当月余额 2.50 × 25% = 0.63。
    /// </summary>
    [Fact]
    public async Task IncomeTaxAccrual_UsesYearToDateProfit()
    {
        var accountSetId = await _h.CreateAccountSetAsync("累计计提所得税");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var revenue = _h.AddRootAccount(
            accountSetId, "6301", "营业外收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var expense = _h.AddRootAccount(
            accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        _h.AddRootAccount(accountSetId, "5801", "所得税费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        _h.AddRootAccount(accountSetId, "2221", "应交所得税", AccountCategory.Liability, BalanceDirection.Credit);
        _h.AddRootAccount(accountSetId, "3103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        // 1 月：收入 0.01、费用净额为贷 0.73（冲销），计提 0.19 后结转损益。
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-20", (cash, 0.01m, 0m), (revenue, 0m, 0.01m));
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-21", (cash, 0.73m, 0m), (expense, 0m, 0.73m));
        var janAccrual = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/pre-close/generate",
            new { year = 2026, month = 1, key = "income-tax" });
        Assert.True(janAccrual.StatusCode == HttpStatusCode.OK, await janAccrual.Content.ReadAsStringAsync());
        var janCarry = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward",
            new { year = 2026, month = 1, splitIncomeCost = false, transferToRetainedEarnings = false });
        Assert.True(janCarry.StatusCode == HttpStatusCode.OK, await janCarry.Content.ReadAsStringAsync());

        // 3 月：利息收入 2.50，期末未结转。
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-03-21", (cash, 2.5m, 0m), (revenue, 0m, 2.5m));

        var preClose = await _client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/pre-close?year=2026&month=3");
        preClose.EnsureSuccessStatusCode();
        using (var body = JsonDocument.Parse(await preClose.Content.ReadAsStringAsync()))
        {
            var item = body.RootElement.GetProperty("items").EnumerateArray()
                .Single(i => i.GetProperty("key").GetString() == "income-tax");
            Assert.Equal(0.62m, item.GetProperty("amount").GetDecimal());
        }
    }

    [Fact]
    public async Task RetainedEarnings_RejectsNonDecember()
    {
        var accountSetId = await _h.CreateAccountSetAsync("年末补做月份");

        var transfer = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/retained-earnings", new { year = 2026, month = 11 });
        Assert.Equal(HttpStatusCode.BadRequest, transfer.StatusCode);
        Assert.Contains("仅适用于 12 月", await transfer.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// 导入的外部结转凭证已把损益结平，批量标记应一次推进到最新有凭证的期间，
    /// 而不是逐月点「标记已结转」。
    /// </summary>
    [Fact]
    public async Task CarryBatch_MarksZeroPnlPeriodsThroughLatestVoucher()
    {
        var accountSetId = await _h.CreateAccountSetAsync("批量标记结转");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var bank = _h.AddRootAccount(accountSetId, "1002", "银行存款");
        var expense = _h.AddRootAccount(
            accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var profit = _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10", (expense, 10m, 0m), (cash, 0m, 10m));
        await PostVoucherAsync(accountSetId, "2026-01-31", "1月  结转损益",
            (expense, 0m, 10m), (profit, 10m, 0m));
        // 3 月只有非损益凭证；2 月无凭证。
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-03-05", (bank, 1m, 0m), (cash, 0m, 1m));

        var batch = await _client.PostAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-batch", null);
        Assert.True(batch.StatusCode == HttpStatusCode.OK, await batch.Content.ReadAsStringAsync());
        using (var body = JsonDocument.Parse(await batch.Content.ReadAsStringAsync()))
        {
            var root = body.RootElement;
            Assert.Equal(
                new[] { "2026-01", "2026-02", "2026-03" },
                root.GetProperty("markedPeriods").EnumerateArray().Select(p => p.GetString()).ToArray());
            Assert.Equal("2026-03", root.GetProperty("status").GetProperty("carriedPeriod").GetString());
        }
    }

    /// <summary>批量标记遇到尚未结转的损益必须停下，不能越过它继续推进期间的结转进度。</summary>
    [Fact]
    public async Task CarryBatch_StopsAtUncarriedPeriod()
    {
        var accountSetId = await _h.CreateAccountSetAsync("批量标记阻停");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var expense = _h.AddRootAccount(
            accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var profit = _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10", (expense, 10m, 0m), (cash, 0m, 10m));
        await PostVoucherAsync(accountSetId, "2026-01-31", "1月  结转损益",
            (expense, 0m, 10m), (profit, 10m, 0m));
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-02-10", (expense, 5m, 0m), (cash, 0m, 5m));

        var batch = await _client.PostAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-batch", null);
        Assert.True(batch.StatusCode == HttpStatusCode.OK, await batch.Content.ReadAsStringAsync());
        using (var body = JsonDocument.Parse(await batch.Content.ReadAsStringAsync()))
        {
            var root = body.RootElement;
            Assert.Equal(
                new[] { "2026-01" },
                root.GetProperty("markedPeriods").EnumerateArray().Select(p => p.GetString()).ToArray());
            Assert.Contains("2026-02 有损益余额未结转", root.GetProperty("message").GetString());
            Assert.Equal("2026-01", root.GetProperty("status").GetProperty("carriedPeriod").GetString());
        }
    }

    /// <summary>
    /// 没有损益发生的空期间应自动标记为已结转：账套启用 1 月、3 月才有第一笔有损益的凭证时，
    /// 打开期末处理就应直接停在 3 月，而不是让用户逐个去点「标记已结转」。
    /// </summary>
    [Fact]
    public async Task Status_AutoCarriesEmptyLeadingPeriods()
    {
        var accountSetId = await _h.CreateAccountSetAsync("自动跳过空期间");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var revenue = _h.AddRootAccount(
            accountSetId, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-03-10", (cash, 100m, 0m), (revenue, 0m, 100m));

        var status = await _client.GetAsync(ApiHarness.ResourcePath(accountSetId, "closing"));
        status.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Equal("2026-02", root.GetProperty("carriedPeriod").GetString());
        Assert.Equal(2026, root.GetProperty("nextCarryYear").GetInt32());
        Assert.Equal(3, root.GetProperty("nextCarryMonth").GetInt32());
        Assert.Equal("Carried", StateOf(root, "2026-01"));
        Assert.Equal("Carried", StateOf(root, "2026-02"));
        // 最新凭证月份是当前做账前沿，没有结转凭证时保留人工确认。
        Assert.Equal("NotCarried", StateOf(root, "2026-03"));
    }

    /// <summary>
    /// 直接结转第一个有损益的月份时，系统应自动跳过前面的空期间，不再要求先逐月标记。
    /// </summary>
    [Fact]
    public async Task CarryForward_AutoSkipsEmptyLeadingPeriods()
    {
        var accountSetId = await _h.CreateAccountSetAsync("结转自动跳过空期间");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var expense = _h.AddRootAccount(
            accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        _h.AddRootAccount(accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-03-10", (expense, 10m, 0m), (cash, 0m, 10m));

        var carry = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward",
            new { year = 2026, month = 3, splitIncomeCost = false, transferToRetainedEarnings = false });
        Assert.True(carry.StatusCode == HttpStatusCode.OK, await carry.Content.ReadAsStringAsync());

        var status = await _client.GetAsync(ApiHarness.ResourcePath(accountSetId, "closing"));
        status.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.Equal("2026-03", body.RootElement.GetProperty("carriedPeriod").GetString());
    }

    /// <summary>
    /// 最新凭证月份如果已有结转凭证（导入的历史账），也应自动标记，不必再补点一次。
    /// </summary>
    [Fact]
    public async Task Status_AutoCarriesImportedCarryVoucherAtFrontier()
    {
        var accountSetId = await _h.CreateAccountSetAsync("导入结转自动标记");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var expense = _h.AddRootAccount(
            accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var profit = _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10", (expense, 0.01m, 0m), (cash, 0m, 0.01m));
        await PostVoucherAsync(accountSetId, "2026-01-31", "1月  结转损益",
            (expense, 0m, 0.01m), (profit, 0.01m, 0m));

        var status = await _client.GetAsync(ApiHarness.ResourcePath(accountSetId, "closing"));
        status.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.Equal("2026-01", body.RootElement.GetProperty("carriedPeriod").GetString());
    }

    /// <summary>
    /// 撤销结转支持指定较早的已结转期间：删除该期间及其后的系统结转凭证，进度退回目标期间上一个月，
    /// 让被后续期间盖住的月份也能重新结转。
    /// </summary>
    [Fact]
    public async Task CancelCarry_CanRewindEarlierCarriedPeriod()
    {
        var accountSetId = await _h.CreateAccountSetAsync("撤销至指定期间");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var revenue = _h.AddRootAccount(
            accountSetId, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var profit = _h.AddRootAccount(
            accountSetId, "4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10", (cash, 100m, 0m), (revenue, 0m, 100m));
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-02-10", (cash, 50m, 0m), (revenue, 0m, 50m));

        foreach (var month in new[] { 1, 2 })
        {
            var carry = await _client.PostAsJsonAsync(
                $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward",
                new { year = 2026, month, splitIncomeCost = false, transferToRetainedEarnings = false });
            Assert.True(carry.StatusCode == HttpStatusCode.OK, await carry.Content.ReadAsStringAsync());
        }

        using (var db = _h.NewContext())
        {
            Assert.Equal(2, db.Vouchers.Count(v =>
                v.AccountSetId == accountSetId && v.Entries.Any(e => e.AccountId == profit)));
        }

        // 目标 1 月不是最近一次结转，但应连同 2 月一起回退。
        var cancel = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/cancel-carry", new { year = 2026, month = 1 });
        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());

        using (var db = _h.NewContext())
        {
            Assert.Equal(0, db.Vouchers.Count(v =>
                v.AccountSetId == accountSetId && v.Entries.Any(e => e.AccountId == profit)));
        }

        var status = await _client.GetAsync(ApiHarness.ResourcePath(accountSetId, "closing"));
        status.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        // 启用期间为 2026-01，退回其上一个月即在启用期之前，清零。
        Assert.Null(body.RootElement.GetProperty("carriedPeriod").GetString());

        var preview = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/carry-forward/preview",
            new { year = 2026, month = 1, splitIncomeCost = false, transferToRetainedEarnings = false });
        preview.EnsureSuccessStatusCode();
        using var previewBody = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.False(previewBody.RootElement.GetProperty("alreadyCarried").GetBoolean());
    }

    private static string? StateOf(JsonElement root, string periodText) =>
        root.GetProperty("periods").EnumerateArray()
            .Single(p => p.GetProperty("periodText").GetString() == periodText)
            .GetProperty("state").GetString();

    private async Task PostVoucherAsync(
        Guid accountSetId,
        string date,
        string summary,
        params (Guid AccountId, decimal Debit, decimal Credit)[] entries)
    {
        var response = await _client.PostAsJsonAsync(
            ApiHarness.ResourcePath(accountSetId, "Vouchers"),
            new
            {
                word = "记",
                date,
                entries = entries.Select(e => new { summary, accountId = e.AccountId, debit = e.Debit, credit = e.Credit }),
            });
        response.EnsureSuccessStatusCode();
    }
}
