using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.Tests.Support;
using Xunit;

namespace KuaiJi.Tests.Ledgers;

/// <summary>
/// 账簿装载工厂 <see cref="LedgerDataSetFactory"/> 的口径测试。
/// 领域依据（AGENTS.md「领域须知」/ 设计文档 §0）：
/// ① 查询区间一律归一到自然月（起始归月初、截止归月末），缺省区间从账套启用期间起算并覆盖全部凭证（含未过账）；
/// ② 期初 = 科目启用期初（按方向换算为借 - 贷）+ 起始日之前已过账分录累计，且启用期初只认末级科目；
/// ③ 装载只按「日期 + 已过账」裁剪，科目/摘要/级次等显示条件不影响装载集合；
/// ④ 同一次请求内按「账套 + 区间 + 排序方式」复用装载结果；条件非法或账套不存在时抛中文文案。
/// </summary>
public sealed class LedgerDataSetFactoryTests : IDisposable
{
    private readonly LedgerHarness _h = new();
    private readonly LedgerDataSetFactory _factory;
    private readonly Account _cash;
    private readonly Account _revenue;

    public LedgerDataSetFactoryTests()
    {
        _factory = _h.NewFactory();
        _cash = _h.AddAccount("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        _revenue = _h.AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
    }

    public void Dispose() => _h.Dispose();

    // —— ① 区间归一到自然月 ——

    [Fact]
    public async Task LoadAsync_StartMidMonth_NormalizesToMonthStart_AndTakesWholeMonthFacts()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 5), VoucherStatus.Posted,
            _h.Entry("月初发生", _cash.Id, 100m, 0m), _h.Entry("月初发生", _revenue.Id, 0m, 100m));
        _h.AddVoucher(new DateOnly(2026, 2, 20), VoucherStatus.Posted,
            _h.Entry("上月发生", _cash.Id, 500m, 0m), _h.Entry("上月发生", _revenue.Id, 0m, 500m));

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 15),
            EndDate = new DateOnly(2026, 3, 20),
        });

        // 归一后整月入账：3-05 虽早于用户填的 3-15 仍属本期
        Assert.Equal(new DateOnly(2026, 3, 1), dataSet.StartDate);
        Assert.Equal(new DateOnly(2026, 3, 31), dataSet.EndDate);
        Assert.Equal([new DateOnly(2026, 3, 5)], dataSet.Facts.Select(f => f.Date).Distinct().ToArray());
        // 上月那张变成期初累计，不进本期发生额
        Assert.Equal(500m, dataSet.OpeningByAccount[_cash.Id]);
        Assert.Equal(-500m, dataSet.OpeningByAccount[_revenue.Id]);
    }

    [Fact]
    public async Task LoadAsync_EndMidMonth_NormalizesToMonthEnd_AndTakesLateMonthFacts()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 28), VoucherStatus.Posted,
            _h.Entry("月末发生", _cash.Id, 20m, 0m), _h.Entry("月末发生", _revenue.Id, 0m, 20m));

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
            EndDate = new DateOnly(2026, 3, 4),
        });

        // 用户填到 3-04，归一到 3-31 后 3-28 的分录仍在区间内——这正是「归一到自然月」的实际效果
        Assert.Equal(new DateOnly(2026, 3, 31), dataSet.EndDate);
        Assert.Single(dataSet.Facts, f => f.AccountId == _cash.Id);
    }

    [Fact]
    public async Task LoadAsync_OnlyStartDate_ReturnsThatSingleMonth()
    {
        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 5, 6),
        });

        Assert.Equal(new DateOnly(2026, 5, 1), dataSet.StartDate);
        Assert.Equal(new DateOnly(2026, 5, 31), dataSet.EndDate);
        Assert.Equal("2026-05-01 至 2026-05-31", dataSet.PeriodText);
    }

    [Fact]
    public async Task LoadAsync_NoDates_UsesVoucherBoundsIncludingUnposted()
    {
        _h.AddVoucher(new DateOnly(2026, 2, 10), VoucherStatus.Posted,
            _h.Entry("最早已过账", _cash.Id, 10m, 0m), _h.Entry("最早已过账", _revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 4, 20), VoucherStatus.Posted,
            _h.Entry("最晚已过账", _cash.Id, 10m, 0m), _h.Entry("最晚已过账", _revenue.Id, 0m, 10m));
        // 边界含未过账：1 月那张草稿把起始月拉到 2026-01，但它自己的分录不进账
        _h.AddVoucher(new DateOnly(2026, 1, 5), VoucherStatus.Draft,
            _h.Entry("草稿在更早已过账前", _cash.Id, 7_777m, 0m), _h.Entry("草稿在更早已过账前", _revenue.Id, 0m, 7_777m));

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions());

        Assert.Equal(new DateOnly(2026, 1, 1), dataSet.StartDate);
        Assert.Equal(new DateOnly(2026, 4, 30), dataSet.EndDate);
        Assert.DoesNotContain(dataSet.Facts, f => f.Summary == "草稿在更早已过账前");
        Assert.Equal(202601, dataSet.StartPeriod);
        Assert.Equal(202604, dataSet.EndPeriod);
    }

    [Fact]
    public async Task LoadAsync_NoDates_StartsFromEnabledPeriodEvenBeforeFirstVoucher()
    {
        // 与柠檬云一致：启用后尚无凭证的月份也要出现在账页里，缺省区间从启用期间起算
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("首笔", _cash.Id, 10m, 0m), _h.Entry("首笔", _revenue.Id, 0m, 10m));

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions());

        Assert.Equal(new DateOnly(2026, 1, 1), dataSet.StartDate);
        Assert.Equal(new DateOnly(2026, 3, 31), dataSet.EndDate);
    }

    [Fact]
    public async Task LoadAsync_NoDatesAndNoVouchers_FallsBackToEnabledPeriod()
    {
        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions());

        // 账套启用期间 2026-01（见 SqliteTestContext），无凭证时按启用月出空白账页
        Assert.Equal(new DateOnly(2026, 1, 1), dataSet.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 31), dataSet.EndDate);
        Assert.Empty(dataSet.Facts);
        Assert.Equal(0, dataSet.UnpostedVoucherCount);
    }

    // —— ② 期初合并 ——

    [Fact]
    public async Task LoadAsync_Opening_MergesEnabledOpeningBalanceWithPriorPostedNet()
    {
        // 启用期初 1_000（借方为正）+ 起始日前已过账净额（借 500 - 贷 200）= 1_300
        var cash = _h.AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        _h.AddVoucher(new DateOnly(2026, 1, 15), VoucherStatus.Posted,
            _h.Entry("年初借方", cash.Id, 500m, 0m), _h.Entry("年初借方", _revenue.Id, 0m, 500m));
        _h.AddVoucher(new DateOnly(2026, 1, 20), VoucherStatus.Posted,
            _h.Entry("年初贷方", cash.Id, 0m, 200m), _h.Entry("年初贷方", _revenue.Id, 200m, 0m));

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
        });

        Assert.Equal(1_300m, dataSet.OpeningByAccount[cash.Id]);
        // 收入科目没录启用期初，期初只来自凭证净额：贷 500 - 借 200 → 净额 -300
        Assert.Equal(-300m, dataSet.OpeningByAccount[_revenue.Id]);
    }

    [Fact]
    public async Task LoadAsync_CreditDirectionOpening_CountsAsNegativeNet()
    {
        // 旧准则负债类贷方为正：启用期初 800 换算成「借 - 贷」净额是 -800
        var payable = _h.AddAccount("2101", "应付账款", AccountCategory.Liability, BalanceDirection.Credit, opening: 800m);

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
        });

        Assert.Equal(-800m, dataSet.OpeningByAccount[payable.Id]);
        var (debit, credit) = BalanceAmounts.SplitBySign(dataSet.OpeningByAccount[payable.Id]);
        Assert.Equal(0m, debit);
        Assert.Equal(800m, credit);
    }

    [Fact]
    public async Task LoadAsync_NonLeafAccount_DoesNotCarryEnabledOpening()
    {
        // 科目 1001 有下级后不再是末级；启用期初只登记在末级科目上（Account.SetOpeningBalance 也有同一道闸）
        var parent = _h.AddAccount("1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit);
        var childA = _h.AddChild(parent, "112201", "甲公司", opening: 300m);
        var childB = _h.AddChild(parent, "112202", "乙公司", opening: 400m);

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
        });

        Assert.Equal(300m, dataSet.OpeningByAccount[childA.Id]);
        Assert.Equal(400m, dataSet.OpeningByAccount[childB.Id]);
        Assert.False(dataSet.OpeningByAccount.ContainsKey(parent.Id));

        // 末级标志要落到库里（用装载回来的科目看，而不是本地旧实例），且默认「只列最明细」把它筛掉
        var parentFromDb = dataSet.Accounts.Single(a => a.Id == parent.Id);
        Assert.False(parentFromDb.IsLeaf);
        Assert.DoesNotContain(parentFromDb, dataSet.SelectAccounts());
    }

    // —— ③ 显示条件不改变装载集合 ——

    [Fact]
    public async Task LoadAsync_DisplayFilters_DoNotTrimLoadedFactsOrTotals()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("销售收现", _cash.Id, 100m, 0m), _h.Entry("销售收现", _revenue.Id, 0m, 100m));

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
            AccountFrom = "1001",     // 只显示现金
            SummaryKeyword = "不相关", // 摘要根本匹配不上
        });

        // 装载全量，裁剪留到投影阶段：否则「合计」会随显示条件变，账页之间对不上
        Assert.Equal(2, dataSet.Facts.Count);
        Assert.Equal(100m, dataSet.Balances.ByAccount[_revenue.Id].PeriodCredit);
        Assert.Single(dataSet.SelectAccounts(), a => a.Id == _cash.Id);
    }

    [Fact]
    public async Task LoadAsync_UnpostedVouchersInPeriod_AreReportedInHeaderCountOnly()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 5), VoucherStatus.Draft,
            _h.Entry("草稿", _cash.Id, 10m, 0m), _h.Entry("草稿", _revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 3, 6), VoucherStatus.Draft,
            _h.Entry("未过账草稿", _cash.Id, 10m, 0m), _h.Entry("未过账草稿", _revenue.Id, 0m, 10m));
        _h.AddVoucher(new DateOnly(2026, 3, 7), VoucherStatus.Posted,
            _h.Entry("已过账", _cash.Id, 10m, 0m), _h.Entry("已过账", _revenue.Id, 0m, 10m));

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
            EndDate = new DateOnly(2026, 3, 31),
        });

        Assert.Equal(2, dataSet.UnpostedVoucherCount);
        Assert.Equal(2, dataSet.Facts.Count); // 只有已过账那张进了账
    }

    // —— ④ 缓存与条件校验 ——

    [Fact]
    public async Task LoadAsync_SameAccountSetRangeAndSort_ReusesCachedDataSet()
    {
        var options = new LedgerQueryOptions { StartDate = new DateOnly(2026, 3, 10) };

        var first = await _factory.LoadAsync(_h.AccountSet.Id, options);
        var second = await _factory.LoadAsync(_h.AccountSet.Id, options with { StartDate = new DateOnly(2026, 3, 15) });

        // 两次都被归一到 2026-03-01，键相同 → 同一份装载结果（一个账页里多次取数不重复查库）
        Assert.Same(first, second);
    }

    [Fact]
    public async Task LoadAsync_DifferentSortBy_LoadsSeparateDataSet()
    {
        var byNumber = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
            SortBy = LedgerSortBy.VoucherNumber,
        });
        var byDate = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
            SortBy = LedgerSortBy.VoucherDate,
        });

        // 排序方式进缓存键：同一份数据有两种顺序，共用实例会串
        Assert.NotSame(byNumber, byDate);
    }

    [Fact]
    public async Task LoadAsync_SortedByVoucherNumber_OrdersWordThenNumberNotDate()
    {
        // 号随装置自增：记-1(3-10)、收-2(3-09)、记-3(3-08)
        _h.AddVoucher("记", new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("第三张日期", _cash.Id, 1m, 0m), _h.Entry("第三张日期", _revenue.Id, 0m, 1m));
        _h.AddVoucher("收", new DateOnly(2026, 3, 9), VoucherStatus.Posted,
            _h.Entry("第一张日期", _cash.Id, 1m, 0m), _h.Entry("第一张日期", _revenue.Id, 0m, 1m));
        _h.AddVoucher("记", new DateOnly(2026, 3, 8), VoucherStatus.Posted,
            _h.Entry("第二张日期", _cash.Id, 1m, 0m), _h.Entry("第二张日期", _revenue.Id, 0m, 1m));

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
            SortBy = LedgerSortBy.VoucherNumber,
        });

        // 凭证字按序号语义排（收 < 记 是 Ordinal 比较），号相同时再看日期
        Assert.Equal(["收-2", "记-1", "记-3"], dataSet.Facts.Select(f => f.VoucherNo).Distinct().ToArray());
    }

    [Fact]
    public async Task LoadAsync_SortedByVoucherDate_OrdersDateThenWordThenNumber()
    {
        _h.AddVoucher("记", new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("十号", _cash.Id, 1m, 0m), _h.Entry("十号", _revenue.Id, 0m, 1m));
        _h.AddVoucher("记", new DateOnly(2026, 3, 8), VoucherStatus.Posted,
            _h.Entry("八号", _cash.Id, 1m, 0m), _h.Entry("八号", _revenue.Id, 0m, 1m));

        var dataSet = await _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 1),
            SortBy = LedgerSortBy.VoucherDate,
        });

        Assert.Equal(
            [new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 10)],
            dataSet.Facts.Select(f => f.Date).ToArray());
    }

    [Fact]
    public async Task LoadAsync_StartAfterEnd_ThrowsChineseHint()
    {
        var error = await Assert.ThrowsAsync<LedgerException>(() => _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 3, 20),
            EndDate = new DateOnly(2026, 3, 10),
        }));

        Assert.Equal("起始日期不能晚于截止日期。", error.Message);
    }

    [Fact]
    public async Task LoadAsync_MaxLevelBelowOne_ThrowsChineseHint()
    {
        var error = await Assert.ThrowsAsync<LedgerException>(() => _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            MaxLevel = 0,
        }));

        Assert.Equal("科目级次必须不小于 1。", error.Message);
    }

    [Fact]
    public async Task LoadAsync_QtyAmountNotImplemented_ThrowsChineseHint()
    {
        var error = await Assert.ThrowsAsync<LedgerException>(() => _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            ShowQtyAmount = true,
        }));

        Assert.Equal("数量金额式尚未启用，请先在科目上设置计量单位。", error.Message);
    }

    [Fact]
    public async Task LoadAsync_SamePrimaryAndSecondaryAssistType_ThrowsChineseHint()
    {
        var error = await Assert.ThrowsAsync<LedgerException>(() => _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            AssistType = AssistType.Department,
            SecondaryAssistType = AssistType.Department,
        }));

        Assert.Equal("组合表的主、次辅助核算类别不能相同。", error.Message);
    }

    [Fact]
    public async Task LoadAsync_RangeInsideStartAccount_ThrowsChineseHint()
    {
        var error = await Assert.ThrowsAsync<LedgerException>(() => _factory.LoadAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            AccountFrom = "1001-1122",
            AccountTo = "1501",
        }));

        Assert.Equal("填写了终止科目时，起始科目请只填编码，不要再写区间。", error.Message);
    }

    [Fact]
    public async Task LoadAsync_UnknownAccountSet_ThrowsChineseHint()
    {
        var error = await Assert.ThrowsAsync<LedgerException>(() => _factory.LoadAsync(Guid.NewGuid(), new LedgerQueryOptions()));

        Assert.Equal("指定的账套不存在。", error.Message);
    }

    [Fact]
    public async Task ResolveRangeAsync_ReturnsNormalizedMonthWithoutLoadingFacts()
    {
        _h.AddVoucher(new DateOnly(2026, 3, 10), VoucherStatus.Posted,
            _h.Entry("边界查询不该带发生额", _cash.Id, 9m, 0m), _h.Entry("边界查询不该带发生额", _revenue.Id, 0m, 9m));

        var (start, end) = await _factory.ResolveRangeAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 7, 19),
            EndDate = new DateOnly(2026, 9, 3),
        });

        // 页头只要边界：7 月首到 9 月末，不装载事实
        Assert.Equal(new DateOnly(2026, 7, 1), start);
        Assert.Equal(new DateOnly(2026, 9, 30), end);
    }

    [Fact]
    public async Task ResolveRangeAsync_InvalidRange_ThrowsSameHintAsLoad()
    {
        var error = await Assert.ThrowsAsync<LedgerException>(() => _factory.ResolveRangeAsync(_h.AccountSet.Id, new LedgerQueryOptions
        {
            StartDate = new DateOnly(2026, 9, 3),
            EndDate = new DateOnly(2026, 7, 19),
        }));

        Assert.Equal("起始日期不能晚于截止日期。", error.Message);
    }
}
