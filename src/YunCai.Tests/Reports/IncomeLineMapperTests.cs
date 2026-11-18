using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Reports.Application;
using YunCai.Modules.Reports.Domain;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace YunCai.Tests.Reports;

/// <summary>
/// 利润表（会企02表）行次归集口径测试（<see cref="IncomeLineMapper"/> 及其消费方
/// <see cref="IncomeStatementService"/>）。
/// 领域依据（AGENTS.md「报表新口径」+「领域须知」）：
/// - 利润表/季报/部门/项目利润表/费用明细表的行次归集统一走 IncomeLineMapper，含新旧准则编码兼容；
/// - 结转凭证（含本年利润 4103，兼容旧准则 3103）一律剔除，否则结转后各行清零、利润表空表；
/// - 只有损益类（Category=ProfitAndLoss）入利润表；成本类（旧准则 4xxx）属在产品，不入损益；
/// - 报表只统计已过账凭证。
/// </summary>
public sealed class IncomeLineMapperTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly IncomeStatementService _service;

    public IncomeLineMapperTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _service = new IncomeStatementService(
            new AccountSetRepository(_db.Context),
            new AccountRepository(_db.Context),
            new VoucherRepository(_db.Context));
    }

    public void Dispose() => _db.Dispose();

    // —— 种子助手 ——

    private Account AddAccount(
        string code,
        string name,
        AccountCategory category,
        BalanceDirection direction)
    {
        var account = Account.CreateRoot(_accountSet.Id, code, name, category, direction);
        _db.Context.Accounts.Add(account);
        _db.Context.SaveChanges();
        return account;
    }

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

    /// <summary>手工装配已过账凭证（含分录），供 internal 的净利润归集入口使用。</summary>
    private List<Voucher> PostedVouchers() => _db.Context.Vouchers
        .AsNoTracking()
        .Include(v => v.Entries)
        .Where(v => v.Status == VoucherStatus.Posted)
        .ToList();

    private static decimal Current(IncomeStatementDto dto, int line) => dto.Lines.Single(l => l.Line == line).Current;

    // —— IncomeLineMapper：编码 → 标准行次 ——

    [Theory]
    [InlineData("6001", BalanceDirection.Credit, 1)]  // 主营业务收入 → 营业收入
    [InlineData("6051", BalanceDirection.Credit, 1)]  // 其他业务收入 → 营业收入
    [InlineData("6401", BalanceDirection.Debit, 2)]   // 主营业务成本 → 营业成本
    [InlineData("6402", BalanceDirection.Debit, 2)]   // 其他业务成本 → 营业成本
    [InlineData("6403", BalanceDirection.Debit, 3)]   // 税金及附加
    [InlineData("6601", BalanceDirection.Debit, 4)]   // 销售费用
    [InlineData("6602", BalanceDirection.Debit, 5)]   // 管理费用
    [InlineData("6604", BalanceDirection.Debit, 5)]   // 研发费用：2007 版式无此行，并入管理费用
    [InlineData("6603", BalanceDirection.Debit, 6)]   // 财务费用
    [InlineData("6702", BalanceDirection.Debit, 7)]   // 信用减值损失：并入资产减值损失
    [InlineData("6701", BalanceDirection.Debit, 7)]   // 资产减值损失
    [InlineData("6101", BalanceDirection.Credit, 8)]  // 公允价值变动收益
    [InlineData("6111", BalanceDirection.Credit, 9)]  // 投资收益
    [InlineData("6117", BalanceDirection.Credit, 12)] // 其他收益：版式无此行，并入营业外收入
    [InlineData("6115", BalanceDirection.Credit, 12)] // 资产处置收益：同上并入营业外收入
    [InlineData("6116", BalanceDirection.Credit, 12)] // 资产处置损益（另一编码）
    [InlineData("6301", BalanceDirection.Credit, 12)] // 营业外收入
    [InlineData("6711", BalanceDirection.Debit, 14)]  // 营业外支出
    [InlineData("6801", BalanceDirection.Debit, 17)]  // 所得税费用
    public void MapLine_NewStandardProfitAndLossCodes_MapsToStandardLine(
        string code, BalanceDirection direction, int expectedLine)
    {
        // 损益类一级编码 → 会企02表行次。行次口径以生产模板为准（IncomeLineMapper 注释：
        // 与「企业会计准则（2007 年）」版式一致，1-32 行），2019 版才单列的研发费用/其他收益/
        // 信用减值损失/资产处置收益在本版式里没有独立行次，按 MapLine 的注释并入相邻行。
        Assert.Equal(expectedLine, IncomeLineMapper.MapLine(code, direction));
    }

    [Theory]
    [InlineData("600101", 1)] // 主营业务收入——A 产品
    [InlineData("60010201", 1)]
    [InlineData("6602001", 5)] // 管理费用——办公费
    [InlineData("1002", 2)]    // 非损益编码走方向兜底（借方 → 营业成本）
    public void MapLine_DetailCode_AggregatesByFirstFourDigits(string code, int expectedLine)
    {
        // 明细科目以一级编码为归集键（AGENTS.md：科目编码有层级，最多 10 级）。
        Assert.Equal(expectedLine, IncomeLineMapper.MapLine(code, BalanceDirection.Debit));
    }

    [Theory]
    [InlineData("5001", BalanceDirection.Credit, 1)] // 旧准则主营业务收入 → 营业收入
    [InlineData("5401", BalanceDirection.Debit, 2)]  // 旧准则主营业务成本 → 营业成本
    [InlineData("5201", BalanceDirection.Credit, 1)] // 旧准则其他业务收入 → 营业收入
    [InlineData("5301", BalanceDirection.Debit, 2)]  // 旧准则财务费用 → 按方向兜底并入营业成本
    public void MapLine_OldStandardCodes_FallbackByBalanceDirection(string code, BalanceDirection direction, int expectedLine)
    {
        // 旧准则（2001 版）损益类用 5xxx 编码，不在 6xxx 表内：按余额方向兜底，
        // 贷方并入营业收入(1)、借方并入营业成本(2)。行次可能不够精细，但收入/费用方向不会反，
        // 因而净利润仍然完整（见 MapLine_OldStandardBook_NetProfit_UnaffectedByFallbackRows）。
        Assert.Equal(expectedLine, IncomeLineMapper.MapLine(code, direction));
    }

    [Theory]
    [InlineData(1, true)]   // 营业收入
    [InlineData(8, true)]   // 公允价值变动收益
    [InlineData(9, true)]   // 投资收益
    [InlineData(12, true)]  // 营业外收入
    [InlineData(2, false)]  // 营业成本
    [InlineData(3, false)]  // 税金及附加
    [InlineData(4, false)]  // 销售费用
    [InlineData(5, false)]  // 管理费用
    [InlineData(6, false)]  // 财务费用
    [InlineData(7, false)]  // 资产减值损失
    [InlineData(14, false)] // 营业外支出
    [InlineData(17, false)] // 所得税费用
    [InlineData(10, false)] // 「其中：对联营/合营投资收益」不参与加减，不能当收入行
    [InlineData(13, false)] // 「其中：非流动资产处置利得」同上
    [InlineData(0, false)]  // 越界行次走默认分支，不得误判为收入
    [InlineData(33, false)]
    public void IsRevenueLine_OnlyCreditPositiveRows(int line, bool expected)
    {
        // 该判定决定取数符号（收入行取贷-借、费用行取借-贷）与净利润加减，判错一行净利润就反号。
        Assert.Equal(expected, IncomeLineMapper.IsRevenueLine(line));
    }

    [Theory]
    [InlineData(1, "营业收入")]
    [InlineData(2, "营业成本")]
    [InlineData(3, "税金及附加")]
    [InlineData(4, "销售费用")]
    [InlineData(5, "管理费用")]
    [InlineData(6, "财务费用")]
    [InlineData(7, "资产减值损失")]
    [InlineData(8, "公允价值变动收益")]
    [InlineData(9, "投资收益")]
    [InlineData(12, "营业外收入")]
    [InlineData(14, "营业外支出")]
    [InlineData(17, "所得税费用")]
    [InlineData(10, "其他项目")] // 「其中」行与 19 行之后的其他综合收益行没有归集项目名：错标会污染部门/项目利润表
    [InlineData(11, "其他项目")]
    [InlineData(13, "其他项目")]
    [InlineData(19, "其他项目")]
    [InlineData(0, "其他项目")]
    public void LineName_StandardRows_ReturnsStatementItemName(int line, string expected)
    {
        // 部门/项目利润表、费用明细表按项目名输出，行名与行次必须一一对应；
        // 行名错位会让同一张利润表在不同页面上显示成不同的项目。
        Assert.Equal(expected, IncomeLineMapper.LineName(line));
    }

    [Theory]
    [InlineData("6001", "6001")]
    [InlineData("600101", "6001")]
    [InlineData("6", "6")] // 编码不足四位时原样返回，不能截出空串
    public void Prefix_TopLevelKeyOfCode(string code, string expected)
    {
        Assert.Equal(expected, IncomeLineMapper.Prefix(code));
    }

    // —— 利润表服务：剔除结转凭证、只归集损益类 ——

    [Fact]
    public async Task GetAsync_NewStandardCarryoverVoucher_ExcludedSoRevenueAndCostKept()
    {
        // AGENTS.md：结转凭证（含 4103 本年利润）一律剔除——否则 1 月末结转后损益科目余额归零，
        // 利润表本期数会变成 0。剔除后应保留结转前的收入 1000 / 成本 600。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var cost = AddAccount("6401", "主营业务成本", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var currentProfit = AddAccount("4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        PostVoucher(1, new DateOnly(2026, 1, 10), (bank.Id, 1000m, 0m), (revenue.Id, 0m, 1000m));
        PostVoucher(2, new DateOnly(2026, 1, 12), (cost.Id, 600m, 0m), (bank.Id, 0m, 600m));
        PostVoucher(3, new DateOnly(2026, 1, 31), (revenue.Id, 1000m, 0m), (currentProfit.Id, 0m, 1000m));
        PostVoucher(4, new DateOnly(2026, 1, 31), (currentProfit.Id, 600m, 0m), (cost.Id, 0m, 600m));

        var dto = await _service.GetAsync(_accountSet.Id, 2026, 1);

        Assert.Equal(1000m, Current(dto, 1));
        Assert.Equal(600m, Current(dto, 2));
        Assert.Equal(2, dto.Lines.Count);
    }

    [Fact]
    public async Task GetAsync_OldStandardCarryoverVoucher_ExcludedSameAsNewStandard()
    {
        // 旧准则账套的本年利润是 3103：剔除规则必须同时认 4103 与 3103，
        // 只写一个就会让另一套账的利润表在结转后变成空表。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = AddAccount("5001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var cost = AddAccount("5401", "主营业务成本", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var currentProfit = AddAccount("3103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit);

        PostVoucher(1, new DateOnly(2026, 1, 10), (bank.Id, 1000m, 0m), (revenue.Id, 0m, 1000m));
        PostVoucher(2, new DateOnly(2026, 1, 12), (cost.Id, 600m, 0m), (bank.Id, 0m, 600m));
        PostVoucher(3, new DateOnly(2026, 1, 31), (revenue.Id, 1000m, 0m), (currentProfit.Id, 0m, 1000m));
        PostVoucher(4, new DateOnly(2026, 1, 31), (currentProfit.Id, 600m, 0m), (cost.Id, 0m, 600m));

        var dto = await _service.GetAsync(_accountSet.Id, 2026, 1);

        // 与上一例（新准则）行次、金额完全一致：两套编码归集结果应相同。
        Assert.Equal(1000m, Current(dto, 1));
        Assert.Equal(600m, Current(dto, 2));
    }

    [Fact]
    public async Task GetAsync_OldStandardUnmappedExpense_NetProfitStillComplete()
    {
        // 旧准则 5301 财务费用按方向兜底并入营业成本行（行次不精细），
        // 但符号正确，所以净利润 = 1000 −（600 + 50）= 350，不因兜底行次而失真——这是该兜底规则的目的。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = AddAccount("5001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var cost = AddAccount("5401", "主营业务成本", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var finance = AddAccount("5301", "财务费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);

        PostVoucher(1, new DateOnly(2026, 2, 10), (bank.Id, 1000m, 0m), (revenue.Id, 0m, 1000m));
        PostVoucher(2, new DateOnly(2026, 2, 11), (cost.Id, 600m, 0m), (bank.Id, 0m, 600m));
        PostVoucher(3, new DateOnly(2026, 2, 12), (finance.Id, 50m, 0m), (bank.Id, 0m, 50m));

        var dto = await _service.GetAsync(_accountSet.Id, 2026, 2);

        Assert.Equal(1000m, Current(dto, 1));
        Assert.Equal(650m, Current(dto, 2));

        var accountsById = _db.Context.Accounts.ToDictionary(a => a.Id, a => a);
        var profit = IncomeStatementService.NetProfit(
            PostedVouchers(),
            accountsById,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 1));
        Assert.Equal(350m, profit.Current);
        Assert.Equal(350m, profit.YearToDate);
    }

    [Fact]
    public async Task GetAsync_CostCategoryAccount_NotAggregatedIntoIncomeStatement()
    {
        // AGENTS.md 陷阱：旧准则 4xxx 是成本类、新准则 4xxx 是权益类，编码首位不能用来判类别。
        // 生产成本（4101，成本类）的借方发生额属在产品成本，不能进利润表；
        // 若把成本类也当损益归集，4101 会被兜底成营业成本，营业成本虚增 300。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var cost = AddAccount("6401", "主营业务成本", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var production = AddAccount("4101", "生产成本", AccountCategory.Cost, BalanceDirection.Debit);
        var surplus = AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);

        PostVoucher(1, new DateOnly(2026, 3, 5), (cost.Id, 600m, 0m), (bank.Id, 0m, 600m));
        PostVoucher(2, new DateOnly(2026, 3, 6), (production.Id, 300m, 0m), (bank.Id, 0m, 300m));
        PostVoucher(3, new DateOnly(2026, 3, 7), (bank.Id, 5000m, 0m), (surplus.Id, 0m, 5000m));

        var dto = await _service.GetAsync(_accountSet.Id, 2026, 3);

        Assert.Equal(600m, Current(dto, 2));
        Assert.Single(dto.Lines);
    }

    [Fact]
    public async Task GetAsync_PeriodWindowAndYearToDate_UseNaturalMonthRanges()
    {
        // 本期取选定月份、本年累计取 1 月 1 日至该月末（AGENTS.md：区间归一到自然月）。
        // 同时验证收入行的符号口径：收入贷方为正（1000），退回借方则冲减。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 1, 15), (bank.Id, 1000m, 0m), (revenue.Id, 0m, 1000m));
        PostVoucher(2, new DateOnly(2026, 2, 15), (bank.Id, 0m, 400m), (revenue.Id, 400m, 0m));
        PostVoucher(3, new DateOnly(2026, 2, 20), (bank.Id, 800m, 0m), (revenue.Id, 0m, 800m));

        var dto = await _service.GetAsync(_accountSet.Id, 2026, 2);
        var line1 = dto.Lines.Single(l => l.Line == 1);

        Assert.Equal(400m, line1.Current);   // 2 月：+800 − 400（销售退回记借方）
        Assert.Equal(1400m, line1.YearToDate); // 1 月 1000 + 2 月 400
    }

    [Fact]
    public async Task GetQuarterAsync_TwoQuartersApart_PeriodIsQuarterWindowAndYtdIsFullYear()
    {
        // 季报与月报共用 IncomeLineMapper 归集口径：本期取季度自然窗口（4-6 月），本年累计取 1 月 1 日至季末。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 1, 20), (bank.Id, 700m, 0m), (revenue.Id, 0m, 700m));
        PostVoucher(2, new DateOnly(2026, 5, 10), (bank.Id, 300m, 0m), (revenue.Id, 0m, 300m));

        var dto = await _service.GetQuarterAsync(_accountSet.Id, 2026, 2);

        Assert.Equal("2026年第2季度", dto.PeriodText);
        var line1 = dto.Lines.Single(l => l.Line == 1);
        Assert.Equal(300m, line1.Current);
        Assert.Equal(1000m, line1.YearToDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task GetQuarterAsync_QuarterOutOfRange_ThrowsWithChineseMessage(int quarter)
    {
        // 期间参数校验的中文文案面向用户，须原样透出（AGENTS.md：业务校验类 400 透出中文文案）。
        var ex = await Assert.ThrowsAsync<ReportException>(
            () => _service.GetQuarterAsync(_accountSet.Id, 2026, quarter));
        Assert.Equal("季度必须在 1 到 4 之间。", ex.Message);
    }

    [Fact]
    public async Task GetAsync_UnpostedVoucherWithRevenue_ExcludedFromStatement()
    {
        // AGENTS.md「领域须知」第一条：只有已过账凭证入报表；草稿收入 900 不得出现在利润表。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 4, 8), (bank.Id, 1000m, 0m), (revenue.Id, 0m, 1000m));

        var draft = Voucher.Create(_accountSet.Id, "记", 2, new DateOnly(2026, 4, 9), "测试制单人");
        draft.AddEntry("草稿收入", bank.Id, 900m, 0m);
        draft.AddEntry("草稿收入", revenue.Id, 0m, 900m);
        _db.Context.Vouchers.Add(draft);
        _db.Context.SaveChanges();

        var dto = await _service.GetAsync(_accountSet.Id, 2026, 4);

        Assert.Equal(1000m, Current(dto, 1));
        Assert.Single(dto.Lines);
    }

    [Fact]
    public async Task GetAsync_InterestAndGovernmentGrant_SubLinesListedWithoutAffectingTotals()
    {
        // 「其中：利息费用」(33) 与「其中：政府补助」(34) 是单列子项，不参与营业利润/利润总额/净利润合计。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var finance = AddAccount("6603001", "财务费用-利息费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var otherIncome = AddAccount("6301002", "营业外收入-政府补助", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 5, 8),
            (bank.Id, 80m, 0m),
            (finance.Id, 0m, 50m),
            (otherIncome.Id, 0m, 30m));

        var dto = await _service.GetAsync(_accountSet.Id, 2026, 5);

        Assert.Equal(-50m, Current(dto, 6));   // 财务费用（利息收入以负数填列）
        Assert.Equal(-50m, Current(dto, 33));  // 其中：利息费用
        Assert.Equal(30m, Current(dto, 12));   // 营业外收入
        Assert.Equal(30m, Current(dto, 34));   // 其中：政府补助
        Assert.DoesNotContain(dto.Lines, l => l.Line is 18 or 16 or 11);
    }

    [Fact]
    public async Task GetAsync_FinanceExpenseWithoutInterestName_NoInterestSubLine()
    {
        // 只有名称含「利息」的财务费用子科目才计入「其中：利息费用」，手续费等不得被误列。
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var finance = AddAccount("6603002", "财务费用-手续费", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        PostVoucher(1, new DateOnly(2026, 5, 9), (finance.Id, 8m, 0m), (bank.Id, 0m, 8m));

        var dto = await _service.GetAsync(_accountSet.Id, 2026, 5);

        Assert.Equal(8m, Current(dto, 6));
        Assert.DoesNotContain(dto.Lines, l => l.Line == 33);
    }
}
