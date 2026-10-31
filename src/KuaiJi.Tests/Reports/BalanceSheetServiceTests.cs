using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Accounting.Infrastructure;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.AccountSets.Infrastructure;
using KuaiJi.Modules.Reports.Application;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.Modules.Vouchers.Infrastructure;
using KuaiJi.Tests.Support;
using Xunit;

namespace KuaiJi.Tests.Reports;

/// <summary>
/// 资产负债表（会企01表）的口径测试：
/// ① 行次归集要同时吃下新旧两套准则编码（AGENTS.md 点名的陷阱：旧 3=权益、新 4=权益，弄错会把权益当成本漏掉）；
/// ② 备抵科目与对应科目同行，靠借-贷净额自动抵减；
/// ③ 未做期末结转时，损益类余额并入「未分配利润」行，保证 资产总计 == 负债 + 所有者权益 恒成立；
/// ④ 只统计已过账凭证，期末取当月月末、年初取本年 1 月 1 日之前。
/// </summary>
public sealed class BalanceSheetServiceTests : IDisposable
{
    private readonly LedgerHarness _h = new();
    private readonly BalanceSheetService _service;

    public BalanceSheetServiceTests()
    {
        _service = new BalanceSheetService(
            new AccountSetRepository(_h.Context),
            new AccountRepository(_h.Context),
            new VoucherRepository(_h.Context));
    }

    public void Dispose() => _h.Dispose();

    private static decimal LineTotal(BalanceSheetDto sheet, Func<int, bool> isOnSide)
        => sheet.Lines.Where(l => isOnSide(l.Line)).Sum(l => l.Ending);

    // —— ① 行次归集 ——

    [Theory]
    [InlineData("3001", 51)] // 旧准则 实收资本
    [InlineData("4001", 51)] // 新准则 实收资本
    [InlineData("3002", 52)] // 旧准则 资本公积
    [InlineData("4002", 52)]
    [InlineData("3101", 53)] // 旧准则 盈余公积
    [InlineData("4101", 53)]
    [InlineData("3103", 54)] // 旧准则 本年利润
    [InlineData("3104", 54)] // 旧准则 利润分配
    [InlineData("4103", 54)]
    [InlineData("4104", 54)]
    [InlineData("3999", 54)] // 未识别权益（如库存股）并入未分配利润，保证总计能配平
    public void MapLine_EquityCodes_OldAndNewStandard_LandOnSameRows(string code, int expectedLine)
    {
        Assert.Equal(expectedLine, BalanceSheetService.MapLine(code, AccountCategory.Equity));
    }

    [Theory]
    [InlineData("1001", 1)]  // 货币资金
    [InlineData("1002", 1)]
    [InlineData("1012", 1)]
    [InlineData("1122", 4)]  // 应收账款
    [InlineData("1231", 4)]  // 坏账准备：与应收同行，靠净额抵减
    [InlineData("112201", 4)] // 明细科目按前四位归集
    [InlineData("1405", 7)]  // 库存商品 → 存货
    [InlineData("1601", 12)] // 固定资产
    [InlineData("1602", 12)] // 累计折旧同行为减项
    [InlineData("1701", 14)] // 无形资产
    [InlineData("1901", 9)]  // 未识别资产 → 其他流动资产
    public void MapLine_AssetCodes_GroupByStandardRows(string code, int expectedLine)
    {
        Assert.Equal(expectedLine, BalanceSheetService.MapLine(code, AccountCategory.Asset));
    }

    [Fact]
    public void MapLine_CostCategory_IsInventoryNotExpense()
    {
        // 旧准则 4=成本：生产成本余额是在产品，必须进资产负债表的存货行，不能被当成损益
        Assert.Equal(7, BalanceSheetService.MapLine("4101", AccountCategory.Cost));
        Assert.Equal(7, BalanceSheetService.MapLine("410101", AccountCategory.Cost));
    }

    [Theory]
    [InlineData("2001", 31)] // 短期借款
    [InlineData("2202", 33)] // 应付账款
    [InlineData("2211", 35)] // 应付职工薪酬
    [InlineData("2221", 36)] // 应交税费
    [InlineData("2701", 43)] // 长期应付款
    [InlineData("2999", 39)] // 未识别负债 → 其他流动负债
    public void MapLine_LiabilityCodes_GroupByStandardRows(string code, int expectedLine)
    {
        Assert.Equal(expectedLine, BalanceSheetService.MapLine(code, AccountCategory.Liability));
    }

    [Theory]
    [InlineData("6001")]
    [InlineData("5001")] // 旧准则收入编码
    public void MapLine_ProfitAndLossGoesToUndistributedProfit(string code)
    {
        // 未结转的当期损益并入「未分配利润」(54)，结转前后报表口径一致
        Assert.Equal(54, BalanceSheetService.MapLine(code, AccountCategory.ProfitAndLoss));
    }

    [Theory]
    [InlineData(1, "货币资金")]
    [InlineData(7, "存货")]
    [InlineData(36, "应交税费")]
    [InlineData(54, "未分配利润")]
    [InlineData(120, "行次120")] // 未识别行次也要有可读名称，不能是空串
    public void LineName_ReturnsStandardItemNames(int line, string expected)
    {
        Assert.Equal(expected, BalanceSheetService.LineName(line));
    }

    // —— ③④ 报表主体 ——

    [Fact]
    public async Task GetAsync_WithoutClosingEntry_BalanceSheetStillBalances()
    {
        var cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        _h.AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 1_000m);
        var revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

        // 未做期末结转：账上留着 200 的损益贷方净额
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("销售收现", cash.Id, 200m, 0m), _h.Entry("销售收现", revenue.Id, 0m, 200m));

        var sheet = await _service.GetAsync(_h.AccountSet.Id, 2026, 3);

        Assert.Equal(1_200m, LineTotal(sheet, l => l <= 16));          // 资产总计
        Assert.Equal(1_200m, LineTotal(sheet, l => l >= 31));          // 负债和所有者权益总计
        Assert.Equal(1_000m, sheet.Lines.Single(l => l.Line == 51).Ending); // 实收资本
        Assert.Equal(200m, sheet.Lines.Single(l => l.Line == 54).Ending);   // 未分配利润含本期损益
    }

    [Fact]
    public async Task GetAsync_DraftVoucher_DoesNotMoveBalances()
    {
        var cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        _h.AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 1_000m);
        var revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Draft,
            _h.Entry("草稿大额", cash.Id, 9_000m, 0m), _h.Entry("草稿大额", revenue.Id, 0m, 9_000m));

        var sheet = await _service.GetAsync(_h.AccountSet.Id, 2026, 3);

        // 账簿与报表只统计已过账凭证（AGENTS.md）：草稿再大也不入表
        Assert.Equal(1_000m, sheet.Lines.Single(l => l.Line == 1).Ending);
        Assert.Equal(1_000m, LineTotal(sheet, l => l >= 31));
    }

    [Fact]
    public async Task GetAsync_OpeningColumn_CoversOnlyBeforeYearStart()
    {
        var cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        _h.AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 1_000m);
        var revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

        // 本年 2 月的凭证进期末、不进年初
        _h.AddVoucher(new DateOnly(2026, 2, 10), VoucherStatus.Posted,
            _h.Entry("本年二月", cash.Id, 300m, 0m), _h.Entry("本年二月", revenue.Id, 0m, 300m));

        var sheet = await _service.GetAsync(_h.AccountSet.Id, 2026, 3);

        var cashLine = sheet.Lines.Single(l => l.Line == 1);
        Assert.Equal(1_000m, cashLine.Opening); // 年初＝启用期初（本年 1 月 1 日之前）
        Assert.Equal(1_300m, cashLine.Ending);  // 期末＝截至 3 月末
        Assert.Equal("2026年01月01日", sheet.OpeningDate);
        Assert.Equal("2026年03月31日", sheet.EndingDate);
    }

    [Fact]
    public async Task GetAsync_AccumulatedDepreciation_NetsAgainstFixedAssets()
    {
        var cost = _h.AddAccount("1601", "固定资产", AccountCategory.Asset, BalanceDirection.Debit, opening: 5_000m);
        var accumulated = _h.AddAccount("1602", "累计折旧", AccountCategory.Asset, BalanceDirection.Credit, opening: 1_200m);
        _h.AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 3_800m);

        var sheet = await _service.GetAsync(_h.AccountSet.Id, 2026, 1);

        // 备抵科目与对应科目同一行，报表上直接是账面净值
        Assert.Equal(3_800m, sheet.Lines.Single(l => l.Line == 12).Ending);
    }

    [Fact]
    public async Task GetAsync_NoPeriodGiven_UsesLatestVoucherMonth()
    {
        var cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        _h.AddVoucher(new DateOnly(2026, 2, 5), VoucherStatus.Posted,
            _h.Entry("二月", cash.Id, 10m, 0m), _h.Entry("二月", revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 5, 20), VoucherStatus.Posted,
            _h.Entry("五月", cash.Id, 20m, 0m), _h.Entry("五月", revenue.Id, 0m, 20m));

        var sheet = await _service.GetAsync(_h.AccountSet.Id, null, null);

        // 与利润表/现金流量表共用 ReportPeriodResolver：三张表表头期间必须一致
        Assert.Equal(2026, sheet.Year);
        Assert.Equal(5, sheet.Month);
    }

    [Fact]
    public async Task GetAsync_UnknownAccountSet_ThrowsChineseHint()
    {
        var error = await Assert.ThrowsAsync<ReportException>(() => _service.GetAsync(Guid.NewGuid(), 2026, 1));

        Assert.Equal("指定的账套不存在。", error.Message);
    }
}
