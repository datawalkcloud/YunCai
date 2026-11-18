using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Ledgers.Application;
using YunCai.Modules.Reports.Application;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Reports;

/// <summary>
/// 科目余额表 <see cref="TrialBalanceService"/> 的口径测试——它是账簿侧最常被引用的一张表，
/// 三条核心口径都在这里落地（AGENTS.md「领域须知」/ 设计文档 §0）：
/// 只统计已过账凭证；查询区间归一到自然月；内部按「借 - 贷」净额累计，只在输出层拆回借/贷两栏。
/// 另附试算平衡不变式：每张凭证借贷平衡且期初经校验 ⇒ 期初合计、本期合计、期末合计三组恒借贷相等。
/// </summary>
public sealed class TrialBalanceServiceTests : IDisposable
{
    private readonly LedgerHarness _h = new();
    private readonly TrialBalanceService _service;
    private readonly Account _cash;
    private readonly Account _payable;
    private readonly Account _revenue;
    private readonly Account _expense;

    public TrialBalanceServiceTests()
    {
        _service = new TrialBalanceService(_h.NewFactory());
        _cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        _payable = _h.AddAccount("2101", "应付账款", AccountCategory.Liability, BalanceDirection.Credit);
        _revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        _expense = _h.AddAccount("6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
    }

    public void Dispose() => _h.Dispose();

    private TrialBalanceRowDto Row(TrialBalanceDto dto, string code)
        => dto.Rows.Single(r => r.Code == code);

    [Fact]
    public async Task GetAsync_UnpostedVouchers_AreAbsentFromBalances()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 5), VoucherStatus.Draft,
            _h.Entry("草稿", _cash.Id, 8_000m, 0m), _h.Entry("草稿", _revenue.Id, 0m, 8_000m));
        _h.AddVoucher(new DateOnly(2026, 3, 6), VoucherStatus.Draft,
            _h.Entry("未过账草稿", _cash.Id, 7_000m, 0m), _h.Entry("未过账草稿", _revenue.Id, 0m, 7_000m));
        _h.AddVoucher(new DateOnly(2026, 3, 7), VoucherStatus.Posted,
            _h.Entry("已过账", _cash.Id, 300m, 0m), _h.Entry("已过账", _revenue.Id, 0m, 300m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        // 制单后必须审核并过账，余额表才有数字（AGENTS.md 领域须知第一条）
        Assert.Equal(300m, Row(dto, "1001").PeriodDebit);
        Assert.Equal(2, dto.Rows.Count);
    }

    [Fact]
    public async Task GetAsync_UserPicksMidMonth_ReturnsWholeNaturalMonth()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 3), VoucherStatus.Posted,
            _h.Entry("月初三", _cash.Id, 100m, 0m), _h.Entry("月初三", _revenue.Id, 0m, 100m));
        _h.AddVoucher(new DateOnly(2026, 3, 29), VoucherStatus.Posted,
            _h.Entry("月末二十九", _cash.Id, 200m, 0m), _h.Entry("月末二十九", _revenue.Id, 0m, 200m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 20));

        // 用户只选了 3-15~3-20，但期间归一到自然月：月初与月末两张都要在内
        Assert.Equal("2026-03-01", dto.StartDate);
        Assert.Equal("2026-03-31", dto.EndDate);
        Assert.Equal("2026-03-01 至 2026-03-31", dto.PeriodText);
        Assert.Equal(300m, Row(dto, "1001").PeriodDebit);
    }

    [Fact]
    public async Task GetAsync_CreditBalance_SplitIntoCreditColumnOnly()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("赊购", _cash.Id, 500m, 0m), _h.Entry("赊购", _payable.Id, 0m, 500m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        var payable = Row(dto, "2101");

        // 净额 -500 只在输出层拆回贷方，期初/期末两栏都不得出现借方数
        Assert.Equal(0m, payable.PeriodDebit);
        Assert.Equal(500m, payable.PeriodCredit);
        Assert.Equal(0m, payable.EndingDebit);
        Assert.Equal(500m, payable.EndingCredit);
        Assert.Equal(0m, payable.OpeningCredit);
    }

    [Fact]
    public async Task GetAsync_EveryRow_SatisfiesEndingEqualsOpeningPlusPeriodNet()
    {
        var bank = _h.AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("现金存银行", _cash.Id, 400m, 0m), _h.Entry("现金存银行", bank.Id, 0m, 400m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.NotEmpty(dto.Rows);
        foreach (var row in dto.Rows)
        {
            var openingNet = row.OpeningDebit - row.OpeningCredit;
            var periodNet = row.PeriodDebit - row.PeriodCredit;
            Assert.Equal(openingNet + periodNet, row.EndingDebit - row.EndingCredit);
        }
    }

    [Fact]
    public async Task GetAsync_TrialBalanceHolds_OnOpeningPeriodAndEndingTotals()
    {
        // 期初本身平衡（银行存款借 2_000 = 实收资本贷 2_000），本期每张凭证借贷平衡
        var bank = _h.AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, opening: 2_000m);
        _h.AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, opening: 2_000m);

        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("支付办公费", _expense.Id, 600m, 0m), _h.Entry("支付办公费", bank.Id, 0m, 600m));
        _h.AddVoucher(new DateOnly(2026, 3, 20), VoucherStatus.Posted,
            _h.Entry("收到货款", bank.Id, 1_500m, 0m), _h.Entry("收到货款", _revenue.Id, 0m, 1_500m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        // 上级行已含下级合计，试算平衡只取一级科目累加
        var roots = dto.Rows.Where(r => r.ParentCode is null).ToList();
        Assert.Equal(roots.Sum(r => r.OpeningDebit), roots.Sum(r => r.OpeningCredit));
        Assert.Equal(roots.Sum(r => r.PeriodDebit), roots.Sum(r => r.PeriodCredit));
        Assert.Equal(roots.Sum(r => r.EndingDebit), roots.Sum(r => r.EndingCredit));
        // 数字本身也要对得上：期末银行存款 2_000 - 600 + 1_500 = 2_900（借方）
        Assert.Equal(2_900m, Row(dto, "1002").EndingDebit);
    }

    [Fact]
    public async Task GetAsync_AccountWithNoBalanceAndNoActivity_IsNotListed()
    {
        var idle = _h.AddAccount("1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit);
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("收现", _cash.Id, 10m, 0m), _h.Entry("收现", _revenue.Id, 0m, 10m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.DoesNotContain(idle.Code, dto.Rows.Select(r => r.Code));
        Assert.Equal(2, dto.Rows.Count);
    }

    [Fact]
    public async Task GetAsync_RowsOrderedByAccountCode()
    {
        // 用构造函数未占用的号段（1001/2101/6001/6602 已建），且每个都带期初——
        // 无余额又无本期发生额的科目不入表，只有 0 期初的科目会被剔除。
        _h.AddAccount("6401", "主营业务成本", AccountCategory.ProfitAndLoss, BalanceDirection.Debit, opening: 5m);
        _h.AddAccount("1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit, opening: 5m);
        _h.AddAccount("2202", "应付账款——明细", AccountCategory.Liability, BalanceDirection.Credit, opening: 5m);

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        // 建表顺序是 6401→1122→2202（插入序），断言输出按编码升序，说明排序取的是编码而非入库顺序。
        Assert.Equal(["1122", "2202", "6401"], dto.Rows.Select(r => r.Code).ToArray());
    }

    [Fact]
    public async Task GetAsync_PriorPeriodVouchers_AppearAsOpeningNotPeriodActivity()
    {
        _h.AddVoucher(new DateOnly(2026, 1, 15), VoucherStatus.Posted,
            _h.Entry("上年一月", _cash.Id, 700m, 0m), _h.Entry("上年一月", _revenue.Id, 0m, 700m));
        _h.AddVoucher(new DateOnly(2026, 3, 15), VoucherStatus.Posted,
            _h.Entry("本期三月", _cash.Id, 300m, 0m), _h.Entry("本期三月", _revenue.Id, 0m, 300m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        var cash = Row(dto, "1001");

        // 1 月那张进期初，不该混进本期发生额
        Assert.Equal(700m, cash.OpeningDebit);
        Assert.Equal(300m, cash.PeriodDebit);
        Assert.Equal(1_000m, cash.EndingDebit);
    }

    [Fact]
    public async Task GetAsync_PeriodStraddlesMonths_CumulativeActivityBothMonths()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("三月", _cash.Id, 100m, 0m), _h.Entry("三月", _revenue.Id, 0m, 100m));
        _h.AddVoucher(new DateOnly(2026, 4, 10), VoucherStatus.Posted,
            _h.Entry("四月", _cash.Id, 200m, 0m), _h.Entry("四月", _revenue.Id, 0m, 200m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 30));

        // 跨月查询按区间累计发生额，边界仍归一到自然月
        Assert.Equal("2026-03-01 至 2026-04-30", dto.PeriodText);
        Assert.Equal(300m, Row(dto, "1001").PeriodDebit);
    }

    [Fact]
    public async Task GetAsync_NoDates_DefaultsFromEnabledPeriodToLastVoucherMonth()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("只有三月有凭证", _cash.Id, 100m, 0m), _h.Entry("只有三月有凭证", _revenue.Id, 0m, 100m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, null, null);

        // 启用期间 2026-01 起算，末月取最后一张凭证所在月
        Assert.Equal("2026-01-01 至 2026-03-31", dto.PeriodText);
        Assert.Equal(100m, Row(dto, "1001").PeriodDebit);
    }

    [Fact]
    public async Task GetAsync_NonLeafParent_RollsUpChildAmounts()
    {
        var parent = _h.AddAccount("1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit);
        var child = _h.AddChild(parent, "112201", "甲公司");
        var grandchild = _h.AddChild(child, "11220101", "乙公司");
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("挂明细", grandchild.Id, 500m, 0m), _h.Entry("挂明细", _revenue.Id, 0m, 500m));

        var dto = await _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        // 上级行汇总整棵子树：1122、112201 都没有直接挂账，但凭最末级的 500 也要层层汇总；前端据此挂成树
        var parentRow = Row(dto, "1122");
        Assert.Equal(1, parentRow.Level);
        Assert.Null(parentRow.ParentCode);
        Assert.Equal(500m, parentRow.PeriodDebit);
        Assert.Equal(500m, parentRow.EndingDebit);

        var childRow = Row(dto, "112201");
        Assert.Equal(2, childRow.Level);
        Assert.Equal("1122", childRow.ParentCode);
        Assert.Equal(500m, childRow.PeriodDebit);

        var grandchildRow = Row(dto, "11220101");
        Assert.Equal(3, grandchildRow.Level);
        Assert.Equal("112201", grandchildRow.ParentCode);
        Assert.Equal(500m, grandchildRow.PeriodDebit);
    }

    [Fact]
    public async Task GetAsync_StartAfterEnd_ThrowsChineseHint()
    {
        var error = await Assert.ThrowsAsync<LedgerException>(() =>
            _service.GetAsync(_h.AccountSet.Id, new DateOnly(2026, 5, 31), new DateOnly(2026, 3, 31)));

        Assert.Equal("起始日期不能晚于截止日期。", error.Message);
    }

    [Fact]
    public async Task GetAsync_UnknownAccountSet_ThrowsChineseHint()
    {
        var error = await Assert.ThrowsAsync<LedgerException>(() =>
            _service.GetAsync(Guid.NewGuid(), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));

        Assert.Equal("指定的账套不存在。", error.Message);
    }
}
