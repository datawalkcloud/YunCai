using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Reports.Domain.CashFlow;
using Xunit;

namespace KuaiJi.Tests.Reports;

/// <summary>
/// 现金流量归类领域服务 <see cref="CashFlowClassifier"/> 的测试（直接法简化工作底稿的归类规则）。
/// 口径（AGENTS.md「报表新口径」）：现金科目＝一级编码 1001/1002/1012；每张凭证的现金净收付按比例分摊到
/// 对手方科目，再由本分类器归到标准 35 行主表——投资/筹资要区分开，权益类对手方归筹资。
/// 编码同时兼容新旧准则（旧 3xxx 权益、4xxx 成本；新 4xxx 权益）。
/// </summary>
public sealed class CashFlowClassifierTests
{
    private static readonly AccountSet Set = AccountSet.Create(
        "现金流分类账套", "现金流分类账套", AccountingStandard.Enterprise, TaxpayerType.General, "人民币", 2026, 1);

    private static Account Acc(string code, string name, AccountCategory category, BalanceDirection direction)
        => Account.CreateRoot(Set.Id, code, name, category, direction);

    [Fact]
    public void CashAccountPrefixes_AreTheThreeStandardCodes()
    {
        // 现金口径就这三个一级编码，多一个少一个都会让整张表现金净收付算错
        Assert.Equal(["1001", "1002", "1012"], CashFlowClassifier.CashAccountPrefixes.Order().ToArray());
    }

    [Theory]
    [InlineData("3001", true, 23)]   // 旧准则实收资本：收到投资
    [InlineData("3001", false, 29)]   // 旧准则实收资本：减资退回记「支付其他筹资」
    [InlineData("4001", true, 23)]   // 新准则实收资本
    [InlineData("4002", true, 23)]   // 资本公积
    [InlineData("3103", true, 25)]   // 旧准则本年利润
    [InlineData("3103", false, 28)]  // 旧准则本年利润：分配股利利润
    [InlineData("4103", false, 28)]  // 新准则本年利润
    [InlineData("4104", false, 28)]  // 利润分配
    [InlineData("3101", false, 28)]  // 盈余公积
    public void MapLine_EquityCounterAccount_GoesToFinancing(string code, bool isCashInflow, int expectedLine)
    {
        var equity = Acc(code, "权益对手方", AccountCategory.Equity, BalanceDirection.Credit);

        // 权益类对手方一律进筹资活动（AGENTS.md 点名口径）
        Assert.Equal(expectedLine, CashFlowClassifier.MapLine(equity, isCashInflow));
        Assert.Equal(CashFlowSection.Financing, CashFlowStandardRows.ByLine[expectedLine].Section);
    }

    [Theory]
    [InlineData("2001", true, 24)]  // 短期借款：取得借款收到的现金
    [InlineData("2001", false, 27)] // 偿还债务支付的现金
    [InlineData("2501", false, 27)] // 长期借款
    [InlineData("2202", false, 5)]  // 应付账款：购买商品
    [InlineData("2202", true, 1)]   // 应付账款反常借方：并入销售收现
    [InlineData("2221", false, 7)]  // 应交税费：支付各项税费
    [InlineData("2221", true, 2)]   // 应交税费借方：收到的税费返还
    [InlineData("2211", false, 6)]  // 应付职工薪酬：支付给职工
    [InlineData("2211", true, 3)]   // 应付职工薪酬借方：收到其他经营
    [InlineData("2801", false, 5)] // 未列举的负债编码（长期应付款一类）：流出一律并入购买商品
    public void MapLine_LiabilityCounterAccount_SplitsByNature(string code, bool isCashInflow, int expectedLine)
    {
        var liability = Acc(code, "负债对手方", AccountCategory.Liability, BalanceDirection.Credit);

        Assert.Equal(expectedLine, CashFlowClassifier.MapLine(liability, isCashInflow));
    }

    [Fact]
    public void MapLine_LiabilityNameMentionsBorrowing_IsTreatedAsLoanEvenOutsideStandardCodes()
    {
        // 旧数据里借款挂在自编编码上时靠名称兜住：取得借款 / 偿还债务
        var loan = Acc("2399", "专项借款", AccountCategory.Liability, BalanceDirection.Credit);

        Assert.Equal(24, CashFlowClassifier.MapLine(loan, true));
        Assert.Equal(27, CashFlowClassifier.MapLine(loan, false));
    }

    [Theory]
    [InlineData("1501", "长期股权投资", true, 11)] // 收回投资收到的现金
    [InlineData("1501", "长期股权投资", false, 18)] // 投资支付的现金
    [InlineData("1601", "固定资产", true, 13)]     // 处置长期资产收回
    [InlineData("1601", "固定资产", false, 17)]    // 购建长期资产支付
    [InlineData("1701", "无形资产", false, 17)]
    [InlineData("1122", "应收账款", true, 1)]      // 应收收回并入销售收现
    [InlineData("1123", "预付账款", false, 5)]     // 预付并入购买商品
    [InlineData("1801", "长期待摊费用", false, 17)] // 名称含「长期」也按投资活动
    public void MapLine_AssetCounterAccount_SplitsInvestingFromOperating(string code, string name, bool isCashInflow, int expectedLine)
    {
        var asset = Acc(code, name, AccountCategory.Asset, BalanceDirection.Debit);

        Assert.Equal(expectedLine, CashFlowClassifier.MapLine(asset, isCashInflow));
        Assert.Equal(
            expectedLine is 11 or 13 or 17 or 18 ? CashFlowSection.Investing : CashFlowSection.Operating,
            CashFlowStandardRows.ByLine[expectedLine].Section);
    }

    [Fact(Skip = "疑似缺陷待拍板：InvestingPrefixes 含 1401-1406（本项目里是存货编码，InvoiceService 用 1405=库存商品，BalanceSheetService 也把 1401-1409 归到资产负债表「存货」行 7），"
        + "于是采购入库的现金流出被归到投资活动 17「购建固定资产…」而不是经营活动 5「购买商品…」。修法应从 CashFlowClassifier.InvestingPrefixes 摘掉存货段，不应用例即转绿。")]
    public void MapLine_InventoryCounterAccount_GoesToOperatingActivity()
    {
        // 采购商品付款：借 1405 库存商品 / 贷 1001 库存现金，对手方是存货，属经营活动
        var inventory = Acc("1405", "库存商品", AccountCategory.Asset, BalanceDirection.Debit);

        Assert.Equal(5, CashFlowClassifier.MapLine(inventory, false));
        Assert.Equal(CashFlowSection.Operating, CashFlowStandardRows.ByLine[5].Section);
    }

    [Theory]
    [InlineData(true, 3)]   // 成本类贷方（结转出去）→ 收到其他经营
    [InlineData(false, 5)]  // 成本类借方（采购归集）→ 购买商品
    public void MapLine_CostCounterAccount_GoesToOperating(bool isCashInflow, int expectedLine)
    {
        var cost = Acc("4101", "生产成本", AccountCategory.Cost, BalanceDirection.Debit);

        Assert.Equal(expectedLine, CashFlowClassifier.MapLine(cost, isCashInflow));
    }

    [Theory]
    [InlineData("6001", "主营业务收入", BalanceDirection.Credit, true, 1)]   // 销售收现
    [InlineData("6001", "主营业务收入", BalanceDirection.Credit, false, 5)]  // 销售退回按购买商品冲减口径
    [InlineData("6602", "管理费用——工资", BalanceDirection.Debit, false, 6)] // 支付职工
    [InlineData("6602", "管理费用——工资", BalanceDirection.Debit, true, 6)]  // 借方损益的工资行不看方向
    [InlineData("6701", "所得税费用", BalanceDirection.Debit, false, 7)]     // 支付税费
    [InlineData("6701", "所得税费用", BalanceDirection.Debit, true, 2)]      // 税费返还
    [InlineData("6401", "主营业务成本", BalanceDirection.Debit, true, 5)]    // 营业成本恒进购买商品
    [InlineData("6403", "税金及附加", BalanceDirection.Debit, false, 5)]
    [InlineData("6603", "财务费用", BalanceDirection.Debit, true, 3)]        // 其他经营流入
    [InlineData("6603", "财务费用", BalanceDirection.Debit, false, 8)]       // 其他经营流出
    public void MapLine_ProfitAndLossCounterAccount_FollowsDirectionAndName(
        string code, string name, BalanceDirection direction, bool isCashInflow, int expectedLine)
    {
        var pnl = Acc(code, name, AccountCategory.ProfitAndLoss, direction);

        Assert.Equal(expectedLine, CashFlowClassifier.MapLine(pnl, isCashInflow));
    }

    [Fact]
    public void MapLine_EveryResult_LandsOnADetailRowOfTheCorrectSide()
    {
        // 全量兜底不变式：归类结果只能是主表的流入/流出明细行，绝不能落在小计、净额或期初期末行上，
        // 否则工作底稿汇总时会把小计行当明细行二次累加。
        var counterAccounts = new[]
        {
            Acc("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit),
            Acc("3103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit),
            Acc("2001", "短期借款", AccountCategory.Liability, BalanceDirection.Credit),
            Acc("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit),
            Acc("2211", "应付职工薪酬", AccountCategory.Liability, BalanceDirection.Credit),
            Acc("2221", "应交税费", AccountCategory.Liability, BalanceDirection.Credit),
            Acc("1501", "长期股权投资", AccountCategory.Asset, BalanceDirection.Debit),
            Acc("1601", "固定资产", AccountCategory.Asset, BalanceDirection.Debit),
            Acc("1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit),
            Acc("4101", "生产成本", AccountCategory.Cost, BalanceDirection.Debit),
            Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit),
            Acc("6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit),
        };

        foreach (var account in counterAccounts)
        {
            var inflow = CashFlowClassifier.MapLine(account, true);
            var outflow = CashFlowClassifier.MapLine(account, false);

            Assert.Contains(inflow, CashFlowStandardRows.InflowLines(CashFlowStandardRows.ByLine[inflow].Section!.Value));
            Assert.Contains(outflow, CashFlowStandardRows.OutflowLines(CashFlowStandardRows.ByLine[outflow].Section!.Value));
        }
    }

    [Fact]
    public void MapLine_OldAndNewStandardEquityCodes_BothReachFinancingRows()
    {
        // 新旧准则编码首位含义不同（旧 3=权益、新 4=权益）：两边都必须归到筹资，不能把权益当成成本漏掉
        var oldStandard = Acc("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);
        var newStandard = Acc("4001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);

        Assert.Equal(CashFlowClassifier.MapLine(oldStandard, true), CashFlowClassifier.MapLine(newStandard, true));
        Assert.Equal(CashFlowSection.Financing, CashFlowStandardRows.ByLine[CashFlowClassifier.MapLine(oldStandard, false)].Section);
    }
}
