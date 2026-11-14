using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Reports.Application;
using YunCai.Modules.Reports.Domain;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Reports;

/// <summary>
/// 报表下钻（行 → 科目）的口径测试：下钻明细的逐科目金额相加必须等于报表行金额，
/// 否则前端两级下钻会出现「点开对不上」；同时锁住只统计已过账凭证、结转凭证剔除、小企业多行归集等既有口径。
/// </summary>
public sealed class ReportDrilldownTests : IDisposable
{
    private readonly LedgerHarness _h = new();
    private readonly BalanceSheetService _balanceSheet;
    private readonly IncomeStatementService _incomeStatement;

    public ReportDrilldownTests()
    {
        _balanceSheet = new BalanceSheetService(
            new AccountSetRepository(_h.Context),
            new AccountRepository(_h.Context),
            new VoucherRepository(_h.Context));
        _incomeStatement = new IncomeStatementService(
            new AccountSetRepository(_h.Context),
            new AccountRepository(_h.Context),
            new VoucherRepository(_h.Context));
    }

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task BalanceSheetDrilldown_SplitsLineIntoAccountsAndAddsUp()
    {
        var cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        var receivable = _h.AddAccount("1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit, opening: 300m);
        var capital = _h.AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 1_300m);
        var revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

        // 3 月收现 200：期末货币资金 1,200，年初仍是 1,000。
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("销售收现", cash.Id, 200m, 0m), _h.Entry("销售收现", revenue.Id, 0m, 200m));

        var sheet = await _balanceSheet.GetAsync(_h.AccountSet.Id, 2026, 3);
        var drill = await _balanceSheet.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 1);

        // 金额与报表同源：合计 = 报表行金额。
        Assert.Equal(sheet.Lines.Single(l => l.Line == 1).Ending, drill.TotalCurrent);
        Assert.Equal(sheet.Lines.Single(l => l.Line == 1).Opening, drill.TotalYearToDate);

        var row = Assert.Single(drill.Accounts);
        Assert.Equal(cash.Id, row.AccountId);
        Assert.Equal("1001", row.Code);
        Assert.Equal(1_200m, row.Current);
        Assert.Equal(1_000m, row.YearToDate);

        // 应收账款行只有 1122，余额不受货款分录影响。
        var arDrill = await _balanceSheet.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 4);
        Assert.Equal(receivable.Id, Assert.Single(arDrill.Accounts).AccountId);
        Assert.Equal(300m, arDrill.TotalCurrent);

        // 未结转的损益并入未分配利润行（60），下钻出来的是收入科目，方向按贷方为正。
        var profitDrill = await _balanceSheet.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 60);
        Assert.Contains(profitDrill.Accounts, a => a.AccountId == revenue.Id && a.Current == 200m);
        Assert.Equal(sheet.Lines.Single(l => l.Line == 60).Ending, profitDrill.TotalCurrent);

        // 权益行同样能拆到实收资本。
        var capitalDrill = await _balanceSheet.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 55);
        Assert.Equal(capital.Id, Assert.Single(capitalDrill.Accounts).AccountId);
    }

    [Fact]
    public async Task BalanceSheetDrilldown_SkipsDraftsAndRejectsUnknownLine()
    {
        var cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        _h.AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 1_000m);
        var draftCounter = _h.AddAccount("1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

        // 草稿凭证不进报表，也不应出现在下钻明细里。
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Draft,
            _h.Entry("草稿", draftCounter.Id, 999m, 0m), _h.Entry("草稿", revenue.Id, 0m, 999m));

        var line1 = await _balanceSheet.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 1);
        Assert.Equal(1_000m, line1.TotalCurrent);
        Assert.Empty((await _balanceSheet.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 4)).Accounts);

        await Assert.ThrowsAsync<ReportException>(
            () => _balanceSheet.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 0));
    }

    [Fact]
    public async Task BalanceSheetDrilldown_SmallBusinessInventoryShowsMainAndDetailRows()
    {
        using var harness = new LedgerHarness("小企业下钻", AccountingStandard.SmallBusiness);
        var service = new BalanceSheetService(
            new AccountSetRepository(harness.Context),
            new AccountRepository(harness.Context),
            new VoucherRepository(harness.Context));

        var stock = harness.AddAccount("1405", "库存商品", AccountCategory.Asset, BalanceDirection.Debit, opening: 800m);
        harness.AddAccount("4001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 800m);

        // 小企业：同一科目既进存货主行 9，也进「其中：库存商品」行 12。
        var main = await service.GetLineDrilldownAsync(harness.AccountSet.Id, 2026, 1, line: 9);
        var detail = await service.GetLineDrilldownAsync(harness.AccountSet.Id, 2026, 1, line: 12);

        Assert.Equal(stock.Id, Assert.Single(main.Accounts).AccountId);
        Assert.Equal(800m, main.TotalCurrent);
        Assert.Equal(stock.Id, Assert.Single(detail.Accounts).AccountId);
        Assert.Equal(800m, detail.TotalCurrent);
    }

    [Fact]
    public async Task IncomeStatementDrilldown_SplitsLinesAndMatchesReport()
    {
        var revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var admin = _h.AddAccount("6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 10_000m);
        var capital = _h.AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 10_000m);

        // 2 月收入 100（只进本年累计）、3 月收入 500 + 管理费用 80。
        _h.AddVoucher(new DateOnly(2026, 2, 10), VoucherStatus.Posted,
            _h.Entry("二月收入", cash.Id, 100m, 0m), _h.Entry("二月收入", revenue.Id, 0m, 100m));
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("三月收入", cash.Id, 500m, 0m), _h.Entry("三月收入", revenue.Id, 0m, 500m));
        _h.AddVoucher(new DateOnly(2026, 3, 20), VoucherStatus.Posted,
            _h.Entry("付办公费", admin.Id, 80m, 0m), _h.Entry("付办公费", cash.Id, 0m, 80m));

        var statement = await _incomeStatement.GetAsync(_h.AccountSet.Id, 2026, 3);
        var revenueDrill = await _incomeStatement.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 1);
        var adminDrill = await _incomeStatement.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 5);

        var revenueLine = statement.Lines.Single(l => l.Line == 1);
        Assert.Equal(revenueLine.Current, revenueDrill.TotalCurrent);
        Assert.Equal(revenueLine.YearToDate, revenueDrill.TotalYearToDate);
        Assert.Equal(500m, revenueDrill.TotalCurrent);
        Assert.Equal(600m, revenueDrill.TotalYearToDate);

        var row = Assert.Single(revenueDrill.Accounts);
        Assert.Equal(revenue.Id, row.AccountId);
        Assert.Equal(500m, row.Current);
        Assert.Equal(600m, row.YearToDate);

        var adminLine = statement.Lines.Single(l => l.Line == 5);
        Assert.Equal(adminLine.Current, adminDrill.TotalCurrent);
        Assert.Equal(admin.Id, Assert.Single(adminDrill.Accounts).AccountId);
        Assert.Equal(80m, adminDrill.TotalCurrent);

        // 非损益类科目即使有发生额也不能出现在利润表下钻里。
        Assert.DoesNotContain(revenueDrill.Accounts, a => a.AccountId == cash.Id || a.AccountId == capital.Id);
    }

    [Fact]
    public async Task IncomeStatementDrilldown_ExcludesCarryForwardVoucher()
    {
        var revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var profitCarry = _h.AddAccount("4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);
        var cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        _h.AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 1_000m);

        _h.AddVoucher(new DateOnly(2026, 3, 5), VoucherStatus.Posted,
            _h.Entry("销售", cash.Id, 200m, 0m), _h.Entry("销售", revenue.Id, 0m, 200m));
        // 结转凭证：收入 → 本年利润；利润表与下钻都必须整张剔除，避免收入行清零。
        _h.AddVoucher(new DateOnly(2026, 3, 31), VoucherStatus.Posted,
            _h.Entry("结转损益", revenue.Id, 200m, 0m), _h.Entry("结转损益", profitCarry.Id, 0m, 200m));

        var drill = await _incomeStatement.GetLineDrilldownAsync(_h.AccountSet.Id, 2026, 3, line: 1);

        Assert.Equal(200m, drill.TotalCurrent);
        Assert.Equal(200m, Assert.Single(drill.Accounts).Current);
    }
}
