using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.Modules.Ledgers.Infrastructure;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KuaiJi.Tests.Ledgers;

/// <summary>
/// 账簿取数端口 <see cref="LedgerSource"/> 的口径测试（真实 EF 投影查询 + 内存 SQLite）。
/// 领域依据见 AGENTS.md「领域须知」：账簿只统计**已过账**凭证；区间含端点；
/// 期初按「借 - 贷」净额由数据库端聚合；辅助核算按「科目 × 档案」聚合；
/// 未过账张数供页头提示；缺省区间的边界取全部凭证（含未过账）。
/// 这些断言同时守住常见陷阱里的「EF 投影后再过滤无法翻译」——日期条件必须落在 where 里。
/// </summary>
public sealed class LedgerSourceTests : IDisposable
{
    private readonly LedgerHarness _h = new();
    private readonly ILedgerSource _source;
    private readonly Account _cash;
    private readonly Account _revenue;

    public LedgerSourceTests()
    {
        _source = new LedgerSource(_h.Context);
        _cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        _revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
    }

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task GetFactsAsync_UnpostedVouchers_AreNotTakenIntoLedger()
    {
        // 同一科目同一天的三张凭证，只有已过账那张入账
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Draft,
            _h.Entry("草稿款", _cash.Id, 111m, 0m), _h.Entry("草稿款", _revenue.Id, 0m, 111m));
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Draft,
            _h.Entry("另一张草稿", _cash.Id, 222m, 0m), _h.Entry("另一张草稿", _revenue.Id, 0m, 222m));
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("已过账", _cash.Id, 333m, 0m), _h.Entry("已过账", _revenue.Id, 0m, 333m));

        var facts = await _source.GetFactsAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(2, facts.Count);
        Assert.All(facts, f => Assert.Equal("已过账", f.Summary));
        Assert.Equal(333m, facts.Single(f => f.AccountId == _cash.Id).Debit);
        Assert.Equal(333m, facts.Single(f => f.AccountId == _revenue.Id).Credit);
    }

    [Fact]
    public async Task GetFactsAsync_DateOnBothEndpoints_IsInclusive()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 1), VoucherStatus.Posted,
            _h.Entry("月初", _cash.Id, 10m, 0m), _h.Entry("月初对方", _revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 3, 31), VoucherStatus.Posted,
            _h.Entry("月末", _cash.Id, 20m, 0m), _h.Entry("月末对方", _revenue.Id, 0m, 20m));
        _h.AddVoucher(new DateOnly(2026, 4, 1), VoucherStatus.Posted,
            _h.Entry("次月一日", _cash.Id, 30m, 0m), _h.Entry("次月一日对方", _revenue.Id, 0m, 30m));

        var facts = await _source.GetFactsAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        var cashSummaries = facts.Where(f => f.AccountId == _cash.Id).Select(f => f.Summary).ToList();

        Assert.Equal(new[] { "月初", "月末" }, cashSummaries.Order().ToArray());
    }

    [Fact]
    public async Task GetFactsAsync_EntriesOfOtherAccountSet_AreNotMixedIn()
    {
        var other = _h.AddExtraAccountSet("隔壁账套");
        var otherCash = Account.CreateRoot(other.Id, "1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        var otherRevenue = Account.CreateRoot(other.Id, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        _h.Context.Accounts.AddRange(otherCash, otherRevenue);
        _h.Context.SaveChanges();

        // 凭证字与号和本账套重叠，日期也重叠：若漏了 accountSetId 条件就会串账
        _h.AddVoucherTo(other, new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("隔壁的现金", otherCash.Id, 999m, 0m), _h.Entry("隔壁的收入", otherRevenue.Id, 0m, 999m));

        var facts = await _source.GetFactsAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Empty(facts);
    }

    [Fact]
    public async Task GetFactsAsync_PeriodIsYyyyMM_OfVoucherDate()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 15), VoucherStatus.Posted,
            _h.Entry("三月款", _cash.Id, 50m, 0m), _h.Entry("三月款", _revenue.Id, 0m, 50m));

        var facts = await _source.GetFactsAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        // 会计期间标识统一 yyyyMM（AGENTS.md），不是 3 也不是 20263；一张凭证的两行都要一致
        Assert.Equal(2, facts.Count);
        Assert.All(facts, f => Assert.Equal(202603, f.Period));
    }

    [Fact]
    public async Task GetFactsAsync_FlattensVoucherHeaderOntoEachEntry()
    {
        _h.AddVoucher("收", new DateOnly(2026, 3, 15), VoucherStatus.Posted,
            _h.Entry("收款一行", _cash.Id, 70m, 0m), _h.Entry("收款二行", _cash.Id, 30m, 0m), _h.Entry("收款对应", _revenue.Id, 0m, 100m));

        var facts = await _source.GetFactsAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var cashFacts = facts.Where(f => f.AccountId == _cash.Id).ToList();
        Assert.Equal(2, cashFacts.Count);
        Assert.All(cashFacts, f =>
        {
            Assert.Equal("收", f.Word);
            Assert.Equal("测试制单人", f.Preparer);
            // 保存即入账后不再有审核环节，未经导入的历史凭证没有审核人。
            Assert.Null(f.Reviewer);
            Assert.Equal(1, f.Number); // 本用例里第一张凭证，号由装置自增从 1 起
            Assert.Equal($"{f.Word}-{f.Number}", f.VoucherNo);
        });
        // 行号在凭证内连续，保证同一凭证内稳定排序
        Assert.Equal([1, 2], cashFacts.Select(f => f.LineNumber).Order().ToArray());
    }

    [Fact]
    public async Task GetFactsAsync_AssistReferences_AreCarriedPerEntry()
    {
        var customer = _h.AddAssistItem(AssistType.Customer, "C01", "甲客户");
        var vendor = _h.AddAssistItem(AssistType.Vendor, "V01", "乙供应商");

        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("双档案行", _cash.Id, 100m, 0m, _h.Ref(AssistType.Customer, customer), _h.Ref(AssistType.Vendor, vendor)),
            _h.Entry("无档案行", _revenue.Id, 0m, 100m));

        var facts = await _source.GetFactsAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var withAssist = facts.Single(f => f.Summary == "双档案行");
        Assert.Equal(2, withAssist.Assists.Count);
        Assert.Contains(withAssist.Assists, a => a.Type == AssistType.Customer && a.ItemId == customer.Id);
        Assert.Contains(withAssist.Assists, a => a.Type == AssistType.Vendor && a.ItemId == vendor.Id);
        Assert.Empty(facts.Single(f => f.Summary == "无档案行").Assists);
    }

    [Fact]
    public async Task GetOpeningNetByAccountAsync_SumsDebitMinusCreditBeforeOnlyPostedUpToStart()
    {
        // 起始日之前：已过账借 300 / 贷 100 → 净额 +200
        _h.AddVoucher(new DateOnly(2026, 1, 20), VoucherStatus.Posted,
            _h.Entry("期初前借方", _cash.Id, 300m, 0m), _h.Entry("期初前借方", _revenue.Id, 0m, 300m));
        _h.AddVoucher(new DateOnly(2026, 2, 5), VoucherStatus.Posted,
            _h.Entry("期初前贷方", _cash.Id, 0m, 100m), _h.Entry("期初前贷方", _revenue.Id, 100m, 0m));
        // 草稿不计入期初
        _h.AddVoucher(new DateOnly(2026, 2, 20), VoucherStatus.Draft,
            _h.Entry("草稿不计", _cash.Id, 5_000m, 0m), _h.Entry("草稿不计", _revenue.Id, 0m, 5_000m));
        // 起始日当天的分录属于「本期发生」，不属于期初
        _h.AddVoucher(new DateOnly(2026, 3, 1), VoucherStatus.Posted,
            _h.Entry("起始日当天", _cash.Id, 8_888m, 0m), _h.Entry("起始日当天", _revenue.Id, 0m, 8_888m));

        var opening = await _source.GetOpeningNetByAccountAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1));

        // 内部一律按借 - 贷净额（AGENTS.md 领域须知）
        Assert.Equal(200m, opening[_cash.Id]);
        Assert.Equal(-200m, opening[_revenue.Id]);
        Assert.DoesNotContain(8_888m, opening.Values);
    }

    [Fact]
    public async Task GetOpeningNetByAssistAsync_GroupsByAccountAndItemAndType()
    {
        var customerA = _h.AddAssistItem(AssistType.Customer, "C01", "甲客户");
        var customerB = _h.AddAssistItem(AssistType.Customer, "C02", "乙客户");

        _h.AddVoucher(new DateOnly(2026, 1, 10), VoucherStatus.Posted,
            _h.Entry("甲客户销售", _revenue.Id, 0m, 400m),
            _h.Entry("甲客户现金", _cash.Id, 400m, 0m, _h.Ref(AssistType.Customer, customerA)));
        _h.AddVoucher(new DateOnly(2026, 1, 12), VoucherStatus.Posted,
            _h.Entry("乙客户销售", _revenue.Id, 0m, 150m),
            _h.Entry("乙客户现金", _cash.Id, 150m, 0m, _h.Ref(AssistType.Customer, customerB)));

        var opening = await _source.GetOpeningNetByAssistAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1));

        // 只有登记了档案的分录参与聚合：本例现金行挂客户档案，收入行未挂 → 仅两个键
        Assert.Equal(2, opening.Count);
        Assert.Equal(400m, opening[new BalanceKey(_cash.Id, AssistType.Customer, customerA.Id)]);
        Assert.Equal(150m, opening[new BalanceKey(_cash.Id, AssistType.Customer, customerB.Id)]);
        Assert.DoesNotContain(opening.Keys, k => k.AccountId == _revenue.Id);
    }

    [Fact]
    public async Task CountUnpostedAsync_CountsOnlyNonPostedVouchersInsideRange()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 5), VoucherStatus.Draft,
            _h.Entry("草稿", _cash.Id, 10m, 0m), _h.Entry("草稿", _revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 3, 6), VoucherStatus.Draft,
            _h.Entry("未过账草稿", _cash.Id, 10m, 0m), _h.Entry("未过账草稿", _revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 3, 7), VoucherStatus.Posted,
            _h.Entry("已过账", _cash.Id, 10m, 0m), _h.Entry("已过账", _revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 4, 5), VoucherStatus.Draft,
            _h.Entry("区间外草稿", _cash.Id, 10m, 0m), _h.Entry("区间外草稿", _revenue.Id, 0m, 10m));

        Assert.Equal(2, await _source.CountUnpostedAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public async Task GetVoucherDateBoundsAsync_IncludesUnpostedSoDefaultRangeCoversInput()
    {
        // 边界取全部凭证（含未过账），使缺省查询区间与录入范围一致——否则草稿日期落在区间外会让人以为没数据
        _h.AddVoucher(new DateOnly(2026, 2, 10), VoucherStatus.Draft,
            _h.Entry("最早是草稿", _cash.Id, 10m, 0m), _h.Entry("最早是草稿", _revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("中间已过账", _cash.Id, 10m, 0m), _h.Entry("中间已过账", _revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 5, 20), VoucherStatus.Draft,
            _h.Entry("最晚是草稿", _cash.Id, 10m, 0m), _h.Entry("最晚是草稿", _revenue.Id, 0m, 10m));

        var (first, last) = await _source.GetVoucherDateBoundsAsync(_h.AccountSet.Id);

        Assert.Equal(new DateOnly(2026, 2, 10), first);
        Assert.Equal(new DateOnly(2026, 5, 20), last);
    }

    [Fact]
    public async Task GetVoucherDateBoundsAsync_AccountSetWithoutVouchers_ReturnsNullPair()
    {
        var (first, last) = await _source.GetVoucherDateBoundsAsync(Guid.NewGuid());

        Assert.Null(first);
        Assert.Null(last);
    }

    [Fact]
    public async Task GetFactsAsync_VoucherUnposted_AppliesLatestStatus()
    {
        // 过账 → 反过账 后必须从账上消失：账簿读的是当前状态，不是历史快照
        var voucher = _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("反过账", _cash.Id, 60m, 0m), _h.Entry("反过账", _revenue.Id, 0m, 60m));
        var start = new DateOnly(2026, 3, 1);
        var end = new DateOnly(2026, 3, 31);

        var postedFacts = await _source.GetFactsAsync(_h.AccountSet.Id, start, end);
        Assert.Equal(2, postedFacts.Count); // 一借一贷两行都在账上

        var tracked = await _h.Context.Vouchers.SingleAsync(v => v.Id == voucher.Id);
        tracked.Unpost(); // 已过账 → 草稿
        await _h.Context.SaveChangesAsync();
        _h.DetachAll();

        Assert.Empty(await _source.GetFactsAsync(_h.AccountSet.Id, start, end));
        Assert.Equal(1, await _source.CountUnpostedAsync(_h.AccountSet.Id, start, end));
    }
}
