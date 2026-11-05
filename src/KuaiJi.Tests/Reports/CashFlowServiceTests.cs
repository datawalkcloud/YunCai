using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Accounting.Infrastructure;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.AccountSets.Infrastructure;
using KuaiJi.Modules.Reports.Application;
using KuaiJi.Modules.Reports.Domain.CashFlow;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.Modules.Vouchers.Infrastructure;
using KuaiJi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KuaiJi.Tests.Reports;

/// <summary>
/// 现金流量表应用服务测试：验证「每张凭证的现金净收付按比例分摊到对手方科目 → 归类到 35 行主表
/// → 小计/净额/期初期末由明细行汇总」这条取数链路（AGENTS.md「报表新口径」）。
/// 分摊比例与小计口径错了，报表给出的现金净增加额就会与现金科目实际变动对不上。
/// </summary>
public sealed class CashFlowServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly CashFlowService _service;

    public CashFlowServiceTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _service = new CashFlowService(
            new AccountSetRepository(_db.Context),
            new AccountRepository(_db.Context),
            new VoucherRepository(_db.Context));
    }

    public void Dispose() => _db.Dispose();

    // —— 种子助手 ——

    /// <summary>资产类科目简写（借方余额、无期初）。</summary>
    private Account AddAccount(string code, string name)
        => AddAccount(code, name, AccountCategory.Asset, BalanceDirection.Debit);

    /// <summary>按指定类别/方向/期初植入科目（现金流量归类依赖类别与方向，不能用 Support 里的资产科目简写）。</summary>
    private Account AddAccount(
        string code,
        string name,
        AccountCategory category,
        BalanceDirection direction = BalanceDirection.Debit,
        decimal opening = 0m)
    {
        var account = Account.CreateRoot(_accountSet.Id, code, name, category, direction);
        if (opening != 0m)
        {
            account.SetOpeningBalance(opening);
        }

        _db.Context.Accounts.Add(account);
        _db.Context.SaveChanges();
        return account;
    }

    /// <summary>直接落一张已过账凭证（本测试只关心报表取数口径，不走凭证应用服务的制单校验）。</summary>
    private Voucher PostVoucher(int number, DateOnly date, params (Guid AccountId, decimal Debit, decimal Credit)[] entries)
    {
        var voucher = Voucher.Create(_accountSet.Id, "记", number, date, "测试制单人");
        foreach (var (accountId, debit, credit) in entries)
        {
            voucher.AddEntry("测试分录", accountId, debit, credit);
        }

        voucher.Post();
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();
        return voucher;
    }

    /// <summary>取某行次的本期金额。</summary>
    private static decimal Current(CashFlowDto dto, int line) => dto.Lines.Single(l => l.Line == line).Current;

    /// <summary>取某行次的本年累计金额。</summary>
    private static decimal YearToDate(CashFlowDto dto, int line) => dto.Lines.Single(l => l.Line == line).YearToDate;

    private static decimal Current(CashFlowQuarterDto dto, int line) => dto.Lines.Single(l => l.Line == line).Current;

    private static decimal YearToDate(CashFlowQuarterDto dto, int line) => dto.Lines.Single(l => l.Line == line).YearToDate;

    // —— 分摊与归类 ——

    [Fact]
    public async Task GetWorkpaperAsync_CapitalInjectionAndSalesReceipt_SplitsCashNetByCounterpartAmounts()
    {
        // 现金流入 1000，对手方为实收资本 700 + 主营业务收入 300：
        // 按对手方各自方向金额分摊，权益类（旧准则 3001）落筹资第 23 行，收入落经营第 1 行。
        var bank = AddAccount("1002", "银行存款");
        var capital = AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 3, 5),
            (bank.Id, 1000m, 0m),
            (capital.Id, 0m, 700m),
            (revenue.Id, 0m, 300m));

        var paper = await _service.GetWorkpaperAsync(_accountSet.Id, 2026, 3);

        Assert.Equal(2, paper.Rows.Count);
        var financing = paper.Rows.Single(r => r.AccountCode == "3001");
        Assert.Equal(23, financing.Line);
        Assert.Equal("吸收投资收到的现金", financing.LineName);
        Assert.True(financing.Inflow);
        Assert.Equal(700m, financing.Amount);

        var operating = paper.Rows.Single(r => r.AccountCode == "6001");
        Assert.Equal(1, operating.Line);
        Assert.Equal("销售商品、提供劳务收到的现金", operating.LineName);
        Assert.Equal(300m, operating.Amount);

        Assert.Equal(1000m, paper.InflowTotal);
        Assert.Equal(0m, paper.OutflowTotal);
    }

    [Fact]
    public async Task GetPeriodAsync_CapitalInjectionAndSalesReceipt_RollsUpSubtotalsNetAndCashBalances()
    {
        // 主表小计口径：经营流入小计(4)=1+2+3，筹资流入小计(26)=23+24+25，
        // 净增加额(33)=经营净额(10)+投资净额(22)+筹资净额(31)，期末(35)=期初(34)+净增加额(33)。
        // 银行存款启用期初 5000，本期收现 1000 → 期末现金必须等于 6000。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, 5000m);
        var capital = AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 3, 5),
            (bank.Id, 1000m, 0m),
            (capital.Id, 0m, 700m),
            (revenue.Id, 0m, 300m));

        var dto = await _service.GetPeriodAsync(_accountSet.Id, 2026, 3);

        Assert.Equal(300m, Current(dto, 1));
        Assert.Equal(300m, Current(dto, 4));
        Assert.Equal(0m, Current(dto, 9));
        Assert.Equal(300m, Current(dto, 10));
        Assert.Equal(700m, Current(dto, 23));
        Assert.Equal(700m, Current(dto, 26));
        Assert.Equal(700m, Current(dto, 31));
        Assert.Equal(1000m, Current(dto, 33));
        Assert.Equal(5000m, Current(dto, 34));
        Assert.Equal(6000m, Current(dto, 35));

        // 本年累计与本期一致（本年只有这一张凭证）。
        Assert.Equal(1000m, YearToDate(dto, 33));
        Assert.Equal(5000m, YearToDate(dto, 34));
        Assert.Equal(6000m, YearToDate(dto, 35));
    }

    [Fact]
    public async Task GetPeriodAsync_FixedAssetPurchaseAndExpense_SplitsInvestingAndOperatingOutflow()
    {
        // 同一张付款凭证里，固定资产（1601，长期资产）走投资第 17 行，管理费用（6602）走经营第 8 行；
        // 两段净额符号相反，净增加额必须等于现金实际付出 2000。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, 3000m);
        var equipment = AddAccount("1601", "固定资产");
        var admin = AddAccount("6602", "管理费用", AccountCategory.ProfitAndLoss);
        PostVoucher(1, new DateOnly(2026, 6, 20),
            (bank.Id, 0m, 2000m),
            (equipment.Id, 1500m, 0m),
            (admin.Id, 500m, 0m));

        var dto = await _service.GetPeriodAsync(_accountSet.Id, 2026, 6);

        Assert.Equal(1500m, Current(dto, 17));
        Assert.Equal(1500m, Current(dto, 21));
        Assert.Equal(-1500m, Current(dto, 22));
        Assert.Equal(500m, Current(dto, 8));
        Assert.Equal(500m, Current(dto, 9));
        Assert.Equal(-500m, Current(dto, 10));
        Assert.Equal(-2000m, Current(dto, 33));
        Assert.Equal(3000m, Current(dto, 34));
        Assert.Equal(1000m, Current(dto, 35));
    }

    [Fact]
    public async Task GetWorkpaperAsync_VoucherWithTwoCashAccounts_AllocationNormalizedToCashNetMovement()
    {
        // 多现金行凭证：现金净收付 = 库存现金借 1000 − 银行存款贷 400 = +600，
        // 对手方合计（1122 借 200、收入贷 500、应付贷 300）方向净额也是 600，按各自方向分摊；
        // 应收增加的分录分摊到负额（销售收现的冲减项），保证归集合计等于现金实际净增加。
        var cash = AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, 200m);
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, 1000m);
        var receivable = AddAccount("1122", "应收账款");
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var payable = AddAccount("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 8, 12),
            (cash.Id, 1000m, 0m),
            (receivable.Id, 200m, 0m),
            (bank.Id, 0m, 400m),
            (revenue.Id, 0m, 500m),
            (payable.Id, 0m, 300m));

        var paper = await _service.GetWorkpaperAsync(_accountSet.Id, 2026, 8);
        Assert.Equal(3, paper.Rows.Count);
        Assert.Equal(500m, paper.Rows.Single(r => r.AccountCode == "6001").Amount);
        Assert.Equal(300m, paper.Rows.Single(r => r.AccountCode == "2202").Amount);
        Assert.Equal(-200m, paper.Rows.Single(r => r.AccountCode == "1122").Amount);
        Assert.All(paper.Rows, row => Assert.Equal(1, row.Line));
        Assert.Equal(600m, paper.InflowTotal);

        // 主表勾稽：第 1 行 = 分摊合计 = 现金科目净增加 = 第 33 行；期末 = 期初 1200 + 600。
        var dto = await _service.GetPeriodAsync(_accountSet.Id, 2026, 8);
        Assert.Equal(600m, Current(dto, 1));
        Assert.Equal(600m, Current(dto, 33));
        Assert.Equal(1200m, Current(dto, 34));
        Assert.Equal(1800m, Current(dto, 35));
    }

    [Fact]
    public async Task GetPeriodAsync_ReceivableCollectionAndPayment_KeepsSalesLineAndPurchaseLine()
    {
        // 收到客户欠款（现金借 + 应收账款贷）按应收/收入口径都并入第 1 行销售收现；
        // 偿付应付账款（现金贷 + 应付借）并入第 5 行购买商品，两段互不串门。
        var bank = AddAccount("1002", "银行存款");
        var receivable = AddAccount("1122", "应收账款");
        var payable = AddAccount("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 9, 3), (bank.Id, 800m, 0m), (receivable.Id, 0m, 800m));
        PostVoucher(2, new DateOnly(2026, 9, 18), (bank.Id, 0m, 600m), (payable.Id, 600m, 0m));

        var dto = await _service.GetPeriodAsync(_accountSet.Id, 2026, 9);

        Assert.Equal(800m, Current(dto, 1));
        Assert.Equal(600m, Current(dto, 5));
        Assert.Equal(800m, Current(dto, 4));
        Assert.Equal(600m, Current(dto, 9));
        Assert.Equal(200m, Current(dto, 10));
        Assert.Equal(200m, Current(dto, 33));
        Assert.Equal(0m, Current(dto, 34));
        Assert.Equal(200m, Current(dto, 35));
    }

    [Fact]
    public async Task GetWorkpaperAsync_UnbalancedImportedVoucher_CashNetStillScalesAllocation()
    {
        // 导入的外部凭证可能借贷不平（AddImportedEntry 不做平衡校验）：这里现金净收付 600，
        // 而对手方方向合计 900。分摊必须按现金净额归一（sideTotal 比例缩放），
        // 否则底稿/主表合计会等于 900，与现金科目实际净增加 600 对不上。
        var cash = AddAccount("1001", "库存现金");
        var bank = AddAccount("1002", "银行存款");
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

        var voucher = Voucher.Import(_accountSet.Id, "记", 9, new DateOnly(2026, 10, 9), "导入", 0, "测试审核人");
        voucher.AddImportedEntry(1, "现金存入", cash.Id, 1000m, 0m);
        voucher.AddImportedEntry(2, "银行付款", bank.Id, 0m, 400m);
        voucher.AddImportedEntry(3, "确认收入", revenue.Id, 0m, 900m);
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();

        // 直接置为已过账：领域层只允许平衡凭证走 Post()，报表口径要能兜住历史脏数据。
        var posted = (int)VoucherStatus.Posted;
        _db.Context.Database.ExecuteSqlInterpolated($"UPDATE vouchers SET status = {posted} WHERE id = {voucher.Id}");
        _db.DetachAll();

        var paper = await _service.GetWorkpaperAsync(_accountSet.Id, 2026, 10);

        Assert.Single(paper.Rows);
        Assert.Equal(600m, paper.Rows[0].Amount); // 900 × (600 / 900)
        Assert.Equal(1, paper.Rows[0].Line);
        Assert.Equal(600m, paper.InflowTotal);

        var dto = await _service.GetPeriodAsync(_accountSet.Id, 2026, 10);
        Assert.Equal(600m, Current(dto, 1));
        Assert.Equal(600m, Current(dto, 33)); // 现金净增加：库存现金 +1000、银行存款 −400
        Assert.Equal(600m, Current(dto, 35));
    }

    // —— 结转凭证与未过账凭证 ——
    [Fact]
    public async Task GetWorkpaperAsync_ProfitCarryoverVoucher_ProducesNoRowsBecauseNoCashInvolved()
    {
        // 期末结转损益凭证（借：主营业务收入 / 贷：本年利润 4103）不含现金科目，现金净额为 0，
        // 既不进主表也不进底稿；否则会把内部结转金额重复计进销售收现，虚增经营现金流。
        var bank = AddAccount("1002", "银行存款");
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var currentProfit = AddAccount("4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 12, 5), (bank.Id, 1000m, 0m), (revenue.Id, 0m, 1000m));
        PostVoucher(2, new DateOnly(2026, 12, 31), (revenue.Id, 1000m, 0m), (currentProfit.Id, 0m, 1000m));

        var paper = await _service.GetWorkpaperAsync(_accountSet.Id, 2026, 12);

        Assert.Single(paper.Rows);
        Assert.DoesNotContain(paper.Rows, r => r.AccountCode == "4103");
        Assert.Equal(1000m, paper.InflowTotal);

        var dto = await _service.GetPeriodAsync(_accountSet.Id, 2026, 12);
        Assert.Equal(1000m, Current(dto, 1));
        Assert.Equal(1000m, Current(dto, 33));
        Assert.Equal(1000m, Current(dto, 35));
    }

    [Fact]
    public async Task GetPeriodAsync_DraftVoucherWithCash_ExcludedFromMainTable()
    {
        // AGENTS.md「领域须知」：账簿与报表只统计已过账凭证（VoucherStatus.Posted）。
        // 草稿凭证的 900 元现金收付不能入表，否则净增加额与现金科目账面余额对不上。
        var bank = AddAccount("1002", "银行存款");
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 4, 9), (bank.Id, 1000m, 0m), (revenue.Id, 0m, 1000m));

        var draft = Voucher.Create(_accountSet.Id, "记", 2, new DateOnly(2026, 4, 30), "测试制单人");
        draft.AddEntry("未过账收款", bank.Id, 900m, 0m);
        draft.AddEntry("未过账收款", revenue.Id, 0m, 900m);
        _db.Context.Vouchers.Add(draft);
        _db.Context.SaveChanges();

        var dto = await _service.GetPeriodAsync(_accountSet.Id, 2026, 4);

        Assert.Equal(1000m, Current(dto, 1));
        Assert.Equal(1000m, Current(dto, 33));
        Assert.Equal(1000m, Current(dto, 35));
    }

    // —— 三种页面共用一套口径 ——

    [Fact]
    public async Task GetQuarterAsync_TwoVouchersAcrossQuarters_PeriodIsQuarterWindowAndYtdIsFullYear()
    {
        // 季报与月报共用同一分摊/归类逻辑：本期取季度自然窗口（4-6 月），本年累计取 1 月 1 日至季末，
        // 期初行取窗口起点前的现金余额（含 2 月已收的 1000）。
        var bank = AddAccount("1002", "银行存款");
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 2, 10), (bank.Id, 1000m, 0m), (revenue.Id, 0m, 1000m));
        PostVoucher(2, new DateOnly(2026, 5, 10), (bank.Id, 500m, 0m), (revenue.Id, 0m, 500m));

        var quarter = await _service.GetQuarterAsync(_accountSet.Id, 2026, 2);

        Assert.Equal("2026年第2季度", quarter.PeriodText);
        Assert.Equal(500m, Current(quarter, 1));
        Assert.Equal(1500m, YearToDate(quarter, 1));
        Assert.Equal(500m, Current(quarter, 33));
        Assert.Equal(1500m, YearToDate(quarter, 33));
        Assert.Equal(1000m, Current(quarter, 34));
        Assert.Equal(1500m, Current(quarter, 35));
        Assert.Equal(0m, YearToDate(quarter, 34));
    }

    [Fact]
    public async Task GetOpeningAsync_CashAccountsByCode_SplitsOpeningAndClosingPerAccount()
    {
        // 主表第 34/35 行的来源明细：按现金科目（1001/1002）逐户列示期初期末，
        // 合计必须等于主表的期初期末行，否则报表无法与银行对账单核对。
        var cash = AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, 300m);
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, 2000m);
        var receivable = AddAccount("1122", "应收账款");
        PostVoucher(1, new DateOnly(2026, 7, 8), (bank.Id, 1200m, 0m), (receivable.Id, 0m, 1200m));

        var opening = await _service.GetOpeningAsync(_accountSet.Id, 2026, 7);

        Assert.Equal(2300m, opening.OpeningTotal);
        Assert.Equal(3500m, opening.ClosingTotal);
        Assert.Equal(300m, opening.Rows.Single(r => r.AccountCode == "1001").Opening);
        Assert.Equal(2000m, opening.Rows.Single(r => r.AccountCode == "1002").Opening);
        Assert.Equal(3200m, opening.Rows.Single(r => r.AccountCode == "1002").Closing);

        var table = await _service.GetPeriodAsync(_accountSet.Id, 2026, 7);
        Assert.Equal(opening.OpeningTotal, Current(table, 34));
        Assert.Equal(opening.ClosingTotal, Current(table, 35));
    }
}
