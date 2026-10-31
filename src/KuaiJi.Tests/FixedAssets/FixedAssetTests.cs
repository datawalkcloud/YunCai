using KuaiJi.Modules.FixedAssets.Domain;
using Xunit;

namespace KuaiJi.Tests.FixedAssets;

/// <summary>
/// 固定资产卡片聚合根单测。锁住 <see cref="FixedAsset"/> 类注释里的领域口径：
/// 应折旧总额 = 原值 ×（1 − 净残值率）（四舍五入到分）；月折旧额 = 总额 ÷ 折旧年限月数；
/// 累计折旧以应折旧总额封顶（末期吃尾差、提满后拒绝再提）；当月新增次月计提；
/// 同一期间只提一次、期间只能向前推进；清理后终止计提且卡片不可再清；
/// 以及编号 / 名称 / 原值的入参校验与中文报错文案。
/// </summary>
public sealed class FixedAssetTests
{
    private static readonly Guid AccountSetId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    /// <summary>默认从 2025-11-05 起用：2026-01 起都是合法的「次月」计提期间。</summary>
    private static FixedAsset NewAsset(DateOnly? startDate = null, decimal originValue = 120000m)
        => FixedAsset.Create(
            AccountSetId, "01-001", CategoryId, "笔记本电脑", "T490", "行政部",
            startDate ?? new DateOnly(2025, 11, 5), originValue);

    private static DateOnly EndOf(int period)
        => new(period / 100, period % 100, DateTime.DaysInMonth(period / 100, period % 100));

    // —— 新建卡片 ——

    [Fact]
    public void Create_ValidInput_InitialisesAsInUseWithZeroDepreciation()
    {
        // 领域规则：新卡片默认「使用中」，累计折旧与最后计提期间从 0 起步；编号与名称去首尾空格，空白备注归一为 null。
        var asset = FixedAsset.Create(
            AccountSetId, " 01-001 ", CategoryId, " 笔记本电脑 ", "   ", null,
            new DateOnly(2026, 1, 10), 120000m);

        Assert.Equal(FixedAssetStatus.InUse, asset.Status);
        Assert.Equal(0m, asset.AccumulatedDepreciation);
        Assert.Equal(0, asset.LastDepreciatedPeriod);
        Assert.Equal("01-001", asset.Code);
        Assert.Equal("笔记本电脑", asset.Name);
        Assert.Null(asset.Model);
        Assert.Null(asset.ClearedDate);
    }

    [Fact]
    public void Create_BlankCode_ThrowsChineseGuard()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => FixedAsset.Create(AccountSetId, "   ", CategoryId, "笔记本电脑", null, null, new DateOnly(2026, 1, 10), 120000m));

        Assert.Contains("资产编号不能为空。", ex.Message);
    }

    [Fact]
    public void Create_OverlongCode_ThrowsLengthGuard()
    {
        var longCode = new string('A', FixedAsset.MaxCodeLength + 1);
        var ex = Assert.Throws<ArgumentException>(
            () => FixedAsset.Create(AccountSetId, longCode, CategoryId, "笔记本电脑", null, null, new DateOnly(2026, 1, 10), 120000m));

        Assert.Contains("资产编号不能超过 50 个字符。", ex.Message);
    }

    [Fact]
    public void Create_BlankName_ThrowsChineseGuard()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => FixedAsset.Create(AccountSetId, "01-001", CategoryId, "  ", null, null, new DateOnly(2026, 1, 10), 120000m));

        Assert.Contains("资产名称不能为空。", ex.Message);
    }

    [Fact]
    public void Create_OverlongName_ThrowsLengthGuard()
    {
        var longName = new string('电', FixedAsset.MaxNameLength + 1);
        var ex = Assert.Throws<ArgumentException>(
            () => FixedAsset.Create(AccountSetId, "01-001", CategoryId, longName, null, null, new DateOnly(2026, 1, 10), 120000m));

        Assert.Contains("资产名称不能超过 100 个字符。", ex.Message);
    }

    [Fact]
    public void Create_NonPositiveOriginValue_ThrowsChineseGuard()
    {
        // 领域规则：资产原值必须大于零（零与负数都被拒绝）。
        foreach (var originValue in new[] { 0m, -1m })
        {
            var ex = Assert.Throws<ArgumentException>(
                () => FixedAsset.Create(AccountSetId, "01-001", CategoryId, "笔记本电脑", null, null, new DateOnly(2026, 1, 10), originValue));

            Assert.Contains("资产原值必须大于零。", ex.Message);
        }
    }

    // —— 折旧计算口径 ——

    [Fact]
    public void DepreciableBase_FivePercentResidual_RoundsToCent()
    {
        // 领域规则：应折旧总额 = 原值 ×（1 − 净残值率%），保留两位小数。
        var asset = NewAsset(originValue: 100000m);

        Assert.Equal(95000m, asset.DepreciableBase(5m));
        Assert.Equal(100000m, asset.DepreciableBase(0m));
        // 10000.01 × 0.95 = 9500.0095 → 9500.01（四舍五入到分）。
        var odd = NewAsset(originValue: 10000.01m);
        Assert.Equal(9500.01m, odd.DepreciableBase(5m));
    }

    [Fact]
    public void MonthlyDepreciation_StraightLine_DividesBaseByMonths()
    {
        // 领域规则：月折旧额 = 应折旧总额 ÷ 折旧年限月数，保留两位小数。
        var asset = NewAsset(originValue: 120000m);

        Assert.Equal(1000m, asset.MonthlyDepreciation(0m, 120));
        // 10000 ÷ 3 = 3333.333… → 3333.33，尾差留到末期由 Depreciate 封顶吸收。
        var odd = NewAsset(originValue: 10000m);
        Assert.Equal(3333.33m, odd.MonthlyDepreciation(0m, 3));
        // 残值率 5%：95000 ÷ 100 月 = 950.00。
        var residual = NewAsset(originValue: 100000m);
        Assert.Equal(950m, residual.MonthlyDepreciation(5m, 100));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-12)]
    public void MonthlyDepreciation_NonPositiveMonths_ReturnsZero(int months)
    {
        // 领域规则：折旧年限月数必须为正，否则不产生月折旧额（类别校验兜底，不应为 0 月数的资产给出 0 而不是除零崩溃）。
        Assert.Equal(0m, NewAsset().MonthlyDepreciation(5m, months));
    }

    // —— 累计折旧直接调整（初始化录入） ——

    [Fact]
    public void AdjustAccumulated_Negative_ThrowsChineseGuard()
    {
        var ex = Assert.Throws<ArgumentException>(() => NewAsset().AdjustAccumulatedDepreciation(-0.01m, 120000m));

        Assert.Contains("累计折旧不能为负数。", ex.Message);
    }

    [Fact]
    public void AdjustAccumulated_AboveDepreciableBase_ThrowsChineseGuard()
    {
        // 领域规则：累计折旧不得超过应折旧总额（原值 120000、残值 0 → 上限 120000）。
        var ex = Assert.Throws<ArgumentException>(() => NewAsset().AdjustAccumulatedDepreciation(120000.01m, 120000m));

        Assert.Contains("累计折旧不能超过应折旧总额。", ex.Message);
    }

    [Fact]
    public void AdjustAccumulated_WithinBase_SetsAccumulationAndNetValue()
    {
        // 领域规则：初始化录入已提折旧后，净值 = 原值 − 累计折旧。
        var asset = NewAsset();

        asset.AdjustAccumulatedDepreciation(20000m, asset.DepreciableBase(0m));

        Assert.Equal(20000m, asset.AccumulatedDepreciation);
        Assert.Equal(100000m, asset.OriginValue - asset.AccumulatedDepreciation);
    }

    // —— 计提折旧 ——

    [Fact]
    public void Depreciate_FirstPeriod_DeductsMonthlyAmountAndMarksPeriod()
    {
        // 领域规则：计提成功后累计折旧按计提额增加，最后计提期间推进到本期（yyyyMM）。
        var asset = NewAsset();

        var amount = asset.Depreciate(202601, 1000m, 12000m, EndOf(202601));

        Assert.Equal(1000m, amount);
        Assert.Equal(1000m, asset.AccumulatedDepreciation);
        Assert.Equal(202601, asset.LastDepreciatedPeriod);
    }

    [Fact]
    public void Depreciate_LastPeriod_CapsAtRemainingSoAccumulationReachesBaseExactly()
    {
        // 领域规则：末期尾差——月折旧额 4000、应折旧总额 10000，第 3 期只剩 2000，
        // 计提额必须被封顶到剩余额，累计折旧恰好等于总额、绝不透支。
        var asset = NewAsset(originValue: 10000m);

        Assert.Equal(4000m, asset.Depreciate(202601, 4000m, 10000m, EndOf(202601)));
        Assert.Equal(4000m, asset.Depreciate(202602, 4000m, 10000m, EndOf(202602)));
        Assert.Equal(2000m, asset.Depreciate(202603, 4000m, 10000m, EndOf(202603)));

        Assert.Equal(10000m, asset.AccumulatedDepreciation);
    }

    [Fact]
    public void Depreciate_AfterBaseFullyDepreciated_ThrowsCantRepeat()
    {
        // 领域规则：累计折旧已等于应折旧总额时再计提必须报「已提足」。
        var asset = NewAsset(originValue: 10000m);
        asset.Depreciate(202601, 5000m, 10000m, EndOf(202601));
        asset.Depreciate(202602, 5000m, 10000m, EndOf(202602));

        var ex = Assert.Throws<InvalidOperationException>(
            () => asset.Depreciate(202603, 5000m, 10000m, EndOf(202603)));

        Assert.Contains("应折旧总额已提足，不能再计提。", ex.Message);
        Assert.Equal(10000m, asset.AccumulatedDepreciation);
    }

    [Fact]
    public void Depreciate_SamePeriodTwice_ThrowsAlreadyDepreciated()
    {
        // 领域规则：同一期间只能计提一次（防止重复点「计提折旧」把一个月提两遍）。
        var asset = NewAsset();
        asset.Depreciate(202601, 1000m, 12000m, EndOf(202601));

        var ex = Assert.Throws<InvalidOperationException>(
            () => asset.Depreciate(202601, 1000m, 12000m, EndOf(202601)));

        Assert.Contains("期间 202601 已计提过折旧。", ex.Message);
        Assert.Equal(1000m, asset.AccumulatedDepreciation);
    }

    [Fact]
    public void Depreciate_PeriodEarlierThanLast_ThrowsMustAdvance()
    {
        // 领域规则：计提期间必须晚于上次计提期间（不允许回填补提历史月份）。
        var asset = NewAsset();
        asset.Depreciate(202602, 1000m, 12000m, EndOf(202602));

        var ex = Assert.Throws<InvalidOperationException>(
            () => asset.Depreciate(202601, 1000m, 12000m, EndOf(202601)));

        Assert.Contains("计提期间必须晚于上次计提期间。", ex.Message);
    }

    [Fact]
    public void Depreciate_BeforeStartPeriod_ThrowsTooEarly()
    {
        // 领域规则：计提期间不能早于开始使用期间（yyyyMM 比较）。
        var asset = NewAsset(startDate: new DateOnly(2026, 3, 10));

        var ex = Assert.Throws<InvalidOperationException>(
            () => asset.Depreciate(202601, 1000m, 12000m, EndOf(202601)));

        Assert.Contains("计提期间不能早于开始使用期间。", ex.Message);
    }

    [Fact]
    public void Depreciate_StartMonthWithDayAfterFirst_ThrowsNextMonthRule()
    {
        // 领域规则「当月新增次月计提」：2026-01-15 起用，2026-01 不计提，累计折旧保持 0。
        var asset = NewAsset(startDate: new DateOnly(2026, 1, 15));

        var ex = Assert.Throws<InvalidOperationException>(
            () => asset.Depreciate(202601, 1000m, 12000m, EndOf(202601)));

        Assert.Contains("当月不计提折旧", ex.Message);
        Assert.Equal(0m, asset.AccumulatedDepreciation);
        Assert.Equal(0, asset.LastDepreciatedPeriod);
    }

    [Fact]
    public void Depreciate_StartOnFirstOfMonth_CurrentlyAllowsStartMonth_DocConflictAsIs()
    {
        // 现状记录（疑似生产问题，待裁决）：FixedAsset.cs:187 的次月计提校验带 `&& StartDate.Day > 1` 条件，
        // 起用日为当月 1 号时跳过校验，起用当月即可计提——与类注释「当月新增次月计提」口径冲突。
        // 断言按当前实际行为写，改动生产码后此处应变红。
        var asset = NewAsset(startDate: new DateOnly(2026, 1, 1));

        var amount = asset.Depreciate(202601, 1000m, 12000m, EndOf(202601));

        Assert.Equal(1000m, amount);
        Assert.Equal(202601, asset.LastDepreciatedPeriod);
    }

    [Fact]
    public void Depreciate_ClearedAsset_ThrowsCleared()
    {
        // 领域规则：清理后停止计提（状态机 InUse → Cleared 不可逆）。
        var asset = NewAsset();
        asset.Clear(new DateOnly(2026, 1, 31));

        var ex = Assert.Throws<InvalidOperationException>(
            () => asset.Depreciate(202602, 1000m, 12000m, EndOf(202602)));

        Assert.Contains("资产已清理，不能再计提折旧。", ex.Message);
    }

    // —— 清理（状态流转） ——

    [Fact]
    public void Clear_AfterStart_TransitionsToClearedWithDate()
    {
        // 领域规则：清理把状态推进为「已清理」并记录清理日期；累计折旧保留（供清理凭证取数）。
        var asset = NewAsset();
        asset.Depreciate(202601, 1000m, 12000m, EndOf(202601));

        asset.Clear(new DateOnly(2026, 2, 10));

        Assert.Equal(FixedAssetStatus.Cleared, asset.Status);
        Assert.Equal(new DateOnly(2026, 2, 10), asset.ClearedDate);
        Assert.Equal(1000m, asset.AccumulatedDepreciation);
    }

    [Fact]
    public void Clear_Twice_ThrowsAlreadyCleared()
    {
        // 领域规则：清理是不可逆流转，已清理资产不可重复清理。
        var asset = NewAsset();
        asset.Clear(new DateOnly(2026, 2, 10));

        var ex = Assert.Throws<InvalidOperationException>(() => asset.Clear(new DateOnly(2026, 3, 10)));

        Assert.Contains("资产已清理。", ex.Message);
    }

    [Fact]
    public void Clear_DateBeforeStartDate_ThrowsGuardAndKeepsInUse()
    {
        // 领域规则：清理日期不得早于开始使用日期；校验失败时状态不得被改脏。
        var asset = NewAsset(startDate: new DateOnly(2026, 1, 10));

        var ex = Assert.Throws<ArgumentException>(() => asset.Clear(new DateOnly(2026, 1, 1)));

        Assert.Contains("清理日期不得早于开始使用日期。", ex.Message);
        Assert.Equal(FixedAssetStatus.InUse, asset.Status);
        Assert.Null(asset.ClearedDate);
    }

    // —— 修改卡片 ——

    [Fact]
    public void Update_ChangesCardFieldsButKeepsAccumulation()
    {
        // 领域规则：编辑卡片要素（编号/名称/原值等）不动累计折旧与计提进度。
        var asset = NewAsset();
        asset.Depreciate(202601, 1000m, 12000m, EndOf(202601));

        asset.Update("01-009", Guid.NewGuid(), "服务器", "R740", "技术部", new DateOnly(2025, 10, 1), 240000m);

        Assert.Equal("01-009", asset.Code);
        Assert.Equal("服务器", asset.Name);
        Assert.Equal(240000m, asset.OriginValue);
        Assert.Equal(1000m, asset.AccumulatedDepreciation);
        Assert.Equal(202601, asset.LastDepreciatedPeriod);
    }

    [Fact]
    public void Update_ZeroOriginValue_ThrowsAndKeepsOldValues()
    {
        // 领域规则：原值必须大于零；校验在赋值前执行，失败后卡片保持原样。
        var asset = NewAsset();

        var ex = Assert.Throws<ArgumentException>(
            () => asset.Update("01-001", CategoryId, "笔记本电脑", null, null, new DateOnly(2025, 11, 5), 0m));

        Assert.Contains("资产原值必须大于零。", ex.Message);
        Assert.Equal(120000m, asset.OriginValue);
    }
}
