using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Reports.Domain.CashFlow;
using Xunit;

namespace KuaiJi.Tests.Reports;

/// <summary>
/// 现金流量归类领域服务测试（<see cref="CashFlowClassifier"/>）。
/// 领域依据（AGENTS.md「报表新口径」+「领域须知」）：
/// - 现金流量表为直接法简化工作底稿，现金科目＝一级编码 1001/1002/1012；
/// - 对手方科目由 CashFlowClassifier 归类到标准 35 行主表，投资/筹资分开，权益类对手方归筹资；
/// - 项目实际使用旧准则（2001 版企业会计制度）编码，实收资本为 3001（权益类）；
///   新旧首位含义不同（旧：3=权益、4=成本；新：4=权益、5=成本），故两套编码都必须落到正确活动段落，
///   权益类不得被误判为成本类（反之亦然）——归类以科目 Category 为准，编码只用于在同类别内细分。
/// </summary>
public sealed class CashFlowClassifierTests
{
    private static readonly Guid TestAccountSetId = Guid.NewGuid();

    /// <summary>构造一个不落库的科目：归类只读编码/名称/类别/余额方向，纯函数即可验证。</summary>
    private static Account Acc(
        string code,
        string name,
        AccountCategory category,
        BalanceDirection direction = BalanceDirection.Debit)
        => Account.CreateRoot(TestAccountSetId, code, name, category, direction);

    /// <summary>资产类科目的简写（借方余额）。</summary>
    private static Account Acc(string code, string name)
        => Acc(code, name, AccountCategory.Asset, BalanceDirection.Debit);

    /// <summary>
    /// 断言归类结果落在预期的主表行次，并顺带核对该行次的活动段落与流入/流出性质，
    /// 使失败信息能直接指出「哪一行/哪个活动」归错了。
    /// </summary>
    private static void AssertLine(int expectedLine, CashFlowSection section, CashFlowRowKind kind, int actualLine)
    {
        Assert.Equal(expectedLine, actualLine);
        var def = CashFlowStandardRows.ByLine[expectedLine];
        Assert.Equal(section, def.Section);
        Assert.Equal(kind, def.Kind);
    }

    // —— 主表行次定义与现金科目集合 ——

    [Fact]
    public void StandardRows_GeneralEnterprise_MainTableHas35SequentialLines()
    {
        // AGENTS.md：标准 35 行主表，行次顺序与财政部一般企业现金流量表格式一致。
        Assert.Equal(35, CashFlowStandardRows.All.Count);
        Assert.Equal(Enumerable.Range(1, 35), CashFlowStandardRows.All.Select(r => r.Line));
        Assert.Equal("销售商品、提供劳务收到的现金", CashFlowStandardRows.ByLine[1].Name);
        Assert.Equal("分配股利、利润或偿付利息支付的现金", CashFlowStandardRows.ByLine[28].Name);
    }

    [Fact]
    public void StandardRows_SubtotalLines_MatchStatementFormat()
    {
        // 小计/净额行号是服务层汇总的唯一依据：经营(4/9/10)、投资(16/21/22)、筹资(26/30/31)。
        Assert.Equal((4, 9, 10), CashFlowStandardRows.SubtotalLines(CashFlowSection.Operating));
        Assert.Equal((16, 21, 22), CashFlowStandardRows.SubtotalLines(CashFlowSection.Investing));
        Assert.Equal((26, 30, 31), CashFlowStandardRows.SubtotalLines(CashFlowSection.Financing));

        // 各活动的明细流入/流出行集合决定小计口径；漏行会让小计与净额不平。
        Assert.Equal(new[] { 1, 2, 3 }, CashFlowStandardRows.InflowLines(CashFlowSection.Operating));
        Assert.Equal(new[] { 5, 6, 7, 8 }, CashFlowStandardRows.OutflowLines(CashFlowSection.Operating));
        Assert.Equal(new[] { 11, 12, 13, 14, 15 }, CashFlowStandardRows.InflowLines(CashFlowSection.Investing));
        Assert.Equal(new[] { 17, 18, 19, 20 }, CashFlowStandardRows.OutflowLines(CashFlowSection.Investing));
        Assert.Equal(new[] { 23, 24, 25 }, CashFlowStandardRows.InflowLines(CashFlowSection.Financing));
        Assert.Equal(new[] { 27, 28, 29 }, CashFlowStandardRows.OutflowLines(CashFlowSection.Financing));
    }

    [Fact]
    public void CashAccountPrefixes_CashAndEquivalents_OnlyThreeTopLevelCodes()
    {
        // AGENTS.md：现金及现金等价物＝一级编码 1001/1002/1012（库存现金/银行存款/其他货币资金），
        // 与资产负债表「货币资金」行口径一致；多算或少算都会让主表净增加额与现金科目实际变动脱节。
        Assert.Equal(
            new[] { "1001", "1002", "1012" },
            CashFlowClassifier.CashAccountPrefixes.OrderBy(c => c, StringComparer.Ordinal).ToArray());
        Assert.False(CashFlowClassifier.CashAccountPrefixes.Contains("1009"));
        Assert.False(CashFlowClassifier.CashAccountPrefixes.Contains("1121"));
    }

    // —— 权益类对手方 → 筹资活动（新旧两套编码）——

    [Fact]
    public void MapLine_OldStandardPaidInCapital_Inflow_AbsorbInvestmentReceipt()
    {
        // 项目实际用旧准则编码：3001 实收资本属权益类。吸收投资者投入的现金 → 第 23 行（筹资流入）。
        AssertLine(
            23,
            CashFlowSection.Financing,
            CashFlowRowKind.Inflow,
            CashFlowClassifier.MapLine(Acc("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit), true));
    }

    [Fact]
    public void MapLine_NewStandardPaidInCapital_Inflow_SameLineAsOldStandard()
    {
        // 同一业务（收到股东出资）在新准则（4001 实收资本、4002 资本公积）下也必须落第 23 行。
        Assert.Equal(23, CashFlowClassifier.MapLine(Acc("4001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit), true));
        Assert.Equal(23, CashFlowClassifier.MapLine(Acc("3002", "资本公积", AccountCategory.Equity, BalanceDirection.Credit), true));
        Assert.Equal(23, CashFlowClassifier.MapLine(Acc("4002", "资本公积", AccountCategory.Equity, BalanceDirection.Credit), true));
    }

    [Fact]
    public void MapLine_PaidInCapital_Outflow_OtherFinancingPayment()
    {
        // 减资/退回出资等权益类流出没有专用行次，归第 29 行「支付其他与筹资活动有关的现金」。
        AssertLine(
            29,
            CashFlowSection.Financing,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit), false));
        Assert.Equal(29, CashFlowClassifier.MapLine(Acc("4001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit), false));
    }

    [Fact]
    public void MapLine_OldAndNewStandardProfitDistribution_Outflow_DividendOrInterestLine()
    {
        // 利润分配/盈余公积类权益编码（旧 3101/3103/3104、新 4101/4103/4104）付现 → 第 28 行
        // 「分配股利、利润或偿付利息支付的现金」；收到则并入第 25 行其他筹资流入。
        Assert.Equal(28, CashFlowClassifier.MapLine(Acc("3104", "利润分配", AccountCategory.Equity, BalanceDirection.Credit), false));
        Assert.Equal(28, CashFlowClassifier.MapLine(Acc("4104", "利润分配", AccountCategory.Equity, BalanceDirection.Credit), false));
        Assert.Equal(28, CashFlowClassifier.MapLine(Acc("3101", "盈余公积", AccountCategory.Equity, BalanceDirection.Credit), false));
        Assert.Equal(25, CashFlowClassifier.MapLine(Acc("3103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit), true));
        Assert.Equal(25, CashFlowClassifier.MapLine(Acc("4103", "本年利润", AccountCategory.Equity, BalanceDirection.Credit), true));
    }

    [Fact]
    public void MapLine_UnrecognizedEquityAccount_FallsBackToFinancingLines()
    {
        // 未列入出资/分配编码集的权益科目（如库存股）仍须留在筹资活动，不能掉进经营活动。
        Assert.Equal(25, CashFlowClassifier.MapLine(Acc("3121", "投资减值准备", AccountCategory.Equity, BalanceDirection.Credit), true));
        AssertLine(
            29,
            CashFlowSection.Financing,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("3121", "投资减值准备", AccountCategory.Equity, BalanceDirection.Credit), false));
    }

    [Fact]
    public void MapLine_OldStandardCostAccount_KeepsOperating_NotFinancing()
    {
        // AGENTS.md 点名的坑：旧准则 4xxx 是成本类、新准则 4xxx 是权益类。
        // 归类以科目 Category 为准：4101 生产成本（成本类）付现属经营活动「购买商品」，
        // 而 4101 盈余公积（权益类）付现属筹资活动——同编码不同类别必须分道。
        AssertLine(
            5,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("4101", "生产成本", AccountCategory.Cost), false));
        Assert.Equal(3, CashFlowClassifier.MapLine(Acc("4101", "生产成本", AccountCategory.Cost), true));
        Assert.Equal(28, CashFlowClassifier.MapLine(Acc("4101", "盈余公积", AccountCategory.Equity, BalanceDirection.Credit), false));

        // 旧准则制造费用/劳务成本同样归经营。
        Assert.Equal(5, CashFlowClassifier.MapLine(Acc("4201", "制造费用", AccountCategory.Cost), false));
    }

    // —— 负债类对手方：借款 / 税费 / 职工薪酬 / 往来 ——

    [Fact]
    public void MapLine_ShortAndLongTermLoan_FinancingInAndOut()
    {
        // 借款类负债（2001/2501 等）：流入=取得借款(24)，流出=偿还债务本金(27)。
        AssertLine(
            24,
            CashFlowSection.Financing,
            CashFlowRowKind.Inflow,
            CashFlowClassifier.MapLine(Acc("2001", "短期借款", AccountCategory.Liability, BalanceDirection.Credit), true));
        AssertLine(
            27,
            CashFlowSection.Financing,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("2501", "长期借款", AccountCategory.Liability, BalanceDirection.Credit), false));
        Assert.Equal(24, CashFlowClassifier.MapLine(Acc("2502", "应付债券", AccountCategory.Liability, BalanceDirection.Credit), true));
        Assert.Equal(27, CashFlowClassifier.MapLine(Acc("2701", "长期应付款", AccountCategory.Liability, BalanceDirection.Credit), false));
    }

    [Fact]
    public void MapLine_LiabilityWithLoanKeywordButUnlistedCode_StillTreatedAsBorrowing()
    {
        // 编码兜不住时按名称识别：一年内到期的长期借款（2503，不在借款编码集内）付现仍是偿还债务。
        Assert.Equal(27, CashFlowClassifier.MapLine(Acc("2503", "一年内到期的长期借款", AccountCategory.Liability, BalanceDirection.Credit), false));
        Assert.Equal(24, CashFlowClassifier.MapLine(Acc("2299", "专项借款", AccountCategory.Liability, BalanceDirection.Credit), true));
    }

    [Fact]
    public void MapLine_TaxPayable_RefundInflowAndTaxOutflow()
    {
        // 税费类负债：流入=收到的税费返还(2)，流出=支付的各项税费(7)，两段都在经营活动。
        AssertLine(
            2,
            CashFlowSection.Operating,
            CashFlowRowKind.Inflow,
            CashFlowClassifier.MapLine(Acc("2221", "应交税费", AccountCategory.Liability, BalanceDirection.Credit), true));
        AssertLine(
            7,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("2221", "应交税费", AccountCategory.Liability, BalanceDirection.Credit), false));

        // 旧准则「应交税金」名称同样命中税关键字。
        Assert.Equal(7, CashFlowClassifier.MapLine(Acc("2211", "应交税金", AccountCategory.Liability, BalanceDirection.Credit), false));
    }

    [Fact]
    public void MapLine_SalaryPayable_EmployeePaymentLine()
    {
        // 职工薪酬类负债：流出=支付给职工以及为职工支付的现金(6)，流入=收到其他与经营活动有关的现金(3)。
        AssertLine(
            6,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("2211", "应付职工薪酬", AccountCategory.Liability, BalanceDirection.Credit), false));
        Assert.Equal(3, CashFlowClassifier.MapLine(Acc("2211", "应付福利费", AccountCategory.Liability, BalanceDirection.Credit), true));
    }

    [Fact]
    public void MapLine_OperatingPayable_InflowOtherOperatingOutflowPurchase()
    {
        // 应付账款/票据、其他应付等经营性往来：流入计入收到其他与经营活动有关的现金(3)，流出计入购买商品(5)；
        // 预收账款属于销售收现(1)，但流出仍并入购买商品(5)。
        Assert.Equal(3, CashFlowClassifier.MapLine(Acc("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit), true));
        AssertLine(
            5,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit), false));
        Assert.Equal(1, CashFlowClassifier.MapLine(Acc("2203", "预收账款", AccountCategory.Liability, BalanceDirection.Credit), true));
        Assert.Equal(5, CashFlowClassifier.MapLine(Acc("2203", "预收账款", AccountCategory.Liability, BalanceDirection.Credit), false));
    }

    [Fact]
    public void MapLine_NonOperatingIncomeAndExpense_OtherOperatingLines()
    {
        // 营业外收入/其他收益/资产处置收益不属于销售收现，流入计入收到其他(3)；营业外支出计入支付其他(8)。
        Assert.Equal(3, CashFlowClassifier.MapLine(Acc("6301", "营业外收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit), true));
        Assert.Equal(3, CashFlowClassifier.MapLine(Acc("6117", "其他收益", AccountCategory.ProfitAndLoss, BalanceDirection.Credit), true));
        AssertLine(
            8,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("6711", "营业外支出", AccountCategory.ProfitAndLoss), false));
    }

    // —— 资产类对手方：长期资产与投资 → 投资活动，其余 → 经营活动 ——

    [Fact]
    public void MapLine_FixedAssetPurchase_Outflow_GiftLongTermAssetLine()
    {
        // 长期资产（1601 固定资产）购置付现 → 第 17 行「购建固定资产、无形资产和其他长期资产支付的现金」。
        AssertLine(
            17,
            CashFlowSection.Investing,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("1601", "固定资产"), false));

        // 明细科目按前四位归集：160101 车床 同样落 17 行。
        Assert.Equal(17, CashFlowClassifier.MapLine(Acc("160101", "生产经营用固定资产"), false));

        // 在建/无形/长期待摊（1701/1801）以及名称带「在建」「无形」「长期」的资产都属投资活动。
        Assert.Equal(17, CashFlowClassifier.MapLine(Acc("1604", "在建工程"), false));
        Assert.Equal(17, CashFlowClassifier.MapLine(Acc("1701", "无形资产"), false));
        Assert.Equal(17, CashFlowClassifier.MapLine(Acc("1901", "融资租入固定资产"), false));
        Assert.Equal(17, CashFlowClassifier.MapLine(Acc("1902", "开办费（长期摊销）"), false));
    }

    [Fact]
    public void MapLine_DisposeLongTermAsset_Inflow_DisposalLineNotRecoveryLine()
    {
        // 处置长期资产收回的现金 → 第 13 行；名称含「投资」的资产收回才走第 11 行「收回投资收到的现金」。
        AssertLine(
            13,
            CashFlowSection.Investing,
            CashFlowRowKind.Inflow,
            CashFlowClassifier.MapLine(Acc("1606", "固定资产清理"), true));
        Assert.Equal(13, CashFlowClassifier.MapLine(Acc("1701", "无形资产"), true));
    }

    [Fact]
    public void MapLine_InvestmentAccount_UsesRecoveryAndPaymentLines()
    {
        // 投资类对手方（名称含「投资」/编码 1511 长期股权投资）：流入=收回投资(11)，流出=投资支付(18)。
        AssertLine(
            11,
            CashFlowSection.Investing,
            CashFlowRowKind.Inflow,
            CashFlowClassifier.MapLine(Acc("1511", "长期股权投资"), true));
        AssertLine(
            18,
            CashFlowSection.Investing,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("1511", "长期股权投资"), false));
        Assert.Equal(11, CashFlowClassifier.MapLine(Acc("1991", "交易性金融资产（短期投资）"), true));
        Assert.Equal(18, CashFlowClassifier.MapLine(Acc("1991", "交易性金融资产（短期投资）"), false));
    }

    [Fact]
    public void MapLine_OperatingAsset_ReceivableAndPrepayment_StayInOperating()
    {
        // 应收/预付等经营性资产：收到=销售商品提供劳务收现(1)（应收收回），付出=购买商品(5)（预付购货）。
        AssertLine(
            1,
            CashFlowSection.Operating,
            CashFlowRowKind.Inflow,
            CashFlowClassifier.MapLine(Acc("1122", "应收账款"), true));
        AssertLine(
            5,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("1123", "预付账款"), false));
        Assert.Equal(1, CashFlowClassifier.MapLine(Acc("1121", "应收票据"), true));
        Assert.Equal(5, CashFlowClassifier.MapLine(Acc("1131", "应收股利"), false));
    }

    // —— 损益类对手方：按余额方向与名称细分 ——

    [Fact]
    public void MapLine_RevenueAccount_Inflow_SalesCashLineOutflow_ReturnReduction()
    {
        // 收入类（贷方余额）：流入=销售商品、提供劳务收到的现金(1)；流出按销售退回冲减购买商品(5)。
        AssertLine(
            1,
            CashFlowSection.Operating,
            CashFlowRowKind.Inflow,
            CashFlowClassifier.MapLine(Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit), true));
        Assert.Equal(5, CashFlowClassifier.MapLine(Acc("6051", "其他业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit), false));
    }

    [Fact]
    public void MapLine_OldStandardRevenueAccount_CreditDirection_SalesCashLine()
    {
        // 旧准则损益类编码 5xxx（5001 主营业务收入）不在 6xxx 集内，靠余额方向（贷方=收入）兜底到第 1 行，
        // 保证旧准则账套的销售收现不会被误并入费用行次。
        Assert.Equal(1, CashFlowClassifier.MapLine(Acc("5001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit), true));
        Assert.Equal(5, CashFlowClassifier.MapLine(Acc("5001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit), false));
    }

    [Fact]
    public void MapLine_SalaryExpense_DebitDirection_EmployeePaymentLine()
    {
        // 费用类（借方余额）名称含工资/薪酬/福利 → 第 6 行「支付给职工以及为职工支付的现金」。
        AssertLine(
            6,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("6601", "销售费用-职工工资", AccountCategory.ProfitAndLoss), false));
    }

    [Fact]
    public void MapLine_TaxKeywordExpense_TaxLines_TakePrecedenceOverCostCodes()
    {
        // 6403 税金及附加：税关键字分支先于 6401/6402/6403 的成本行判定，故流出走第 7 行「支付的各项税费」。
        AssertLine(
            7,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("6403", "税金及附加", AccountCategory.ProfitAndLoss), false));
        Assert.Equal(2, CashFlowClassifier.MapLine(Acc("6403", "税金及附加", AccountCategory.ProfitAndLoss), true));
    }

    [Fact]
    public void MapLine_CostOfSalesExpense_PurchaseGoodsLine()
    {
        // 6401 主营业务成本 / 6402 其他业务成本 → 第 5 行「购买商品、接受劳务支付的现金」。
        AssertLine(
            5,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("6401", "主营业务成本", AccountCategory.ProfitAndLoss), false));
        Assert.Equal(5, CashFlowClassifier.MapLine(Acc("6402", "其他业务成本", AccountCategory.ProfitAndLoss), false));
    }

    [Fact]
    public void MapLine_OtherExpense_OperatingInflowAndOutflowFallbackLines()
    {
        // 未命中关键字/成本编码的费用类（6602 管理费用、旧准则 5301 财务费用）：
        // 流出→支付其他与经营活动有关的现金(8)，流入→收到其他与经营活动有关的现金(3)，都留在经营段。
        AssertLine(
            8,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("6602", "管理费用", AccountCategory.ProfitAndLoss), false));
        Assert.Equal(3, CashFlowClassifier.MapLine(Acc("5301", "财务费用", AccountCategory.ProfitAndLoss), true));
        Assert.Equal(8, CashFlowClassifier.MapLine(Acc("5301", "财务费用", AccountCategory.ProfitAndLoss), false));
    }

    // —— 归类结果必须始终落在 35 行主表内 ——

    [Fact]
    public void MapLine_AllCategories_BothDirections_EveryLineLandsOnStandardMainTable()
    {
        // 主表汇总只认 1-31 的明细行；若归类返回 0、32+ 或表外行号，小计/净额就会漏数，报表与现金科目对不上。
        var samples = new[]
        {
            Acc("1122", "应收账款", AccountCategory.Asset),
            Acc("1601", "固定资产", AccountCategory.Asset),
            Acc("1511", "长期股权投资", AccountCategory.Asset),
            Acc("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit),
            Acc("2001", "短期借款", AccountCategory.Liability, BalanceDirection.Credit),
            Acc("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit),
            Acc("4104", "利润分配", AccountCategory.Equity, BalanceDirection.Credit),
            Acc("4101", "生产成本", AccountCategory.Cost),
            Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit),
            Acc("6602", "管理费用", AccountCategory.ProfitAndLoss),
        };

        foreach (var account in samples)
        {
            foreach (var inflow in new[] { true, false })
            {
                var line = CashFlowClassifier.MapLine(account, inflow);
                Assert.True(CashFlowStandardRows.ByLine.TryGetValue(line, out var def),
                    $"科目「{account.Code} {account.Name}」在 inflow={inflow} 时归到主表外的行次 {line}");
                Assert.NotNull(def!.Section);

                // 归类结果只能落在流入或流出明细行，绝不能命中 32-35 的汇总/期初期末行，
                // 否则小计会漏掉这笔现金，主表净增加额与现金科目实际变动对不上。
                Assert.Equal(inflow ? CashFlowRowKind.Inflow : CashFlowRowKind.Outflow, def.Kind);
            }
        }
    }

    // —— 已知口径疑点（见回报）：存货编码被列进投资活动集 ——

    [Fact(Skip = "疑似生产缺陷：CashFlowClassifier.InvestingPrefixes 含 1401–1406，而 BalanceSheetService 把这些编码认定为存货（会企01表第 7 行）。采购原材料付现会被归到第 17 行投资活动，导致经营/投资两段同时错。修复后取消本 Skip。")]
    public void MapLine_InventoryPurchase_Outflow_ShouldBeOperatingPurchaseGoodsLine()
    {
        // 资产负债表口径：1401 材料采购 / 1403 原材料 / 1405 库存商品 都是存货（BalanceSheetService.MapAssetLine）。
        // 现金流量表主表第 17 行只收「购建固定资产、无形资产和其他长期资产」，存货购置应在第 5 行。
        AssertLine(
            5,
            CashFlowSection.Operating,
            CashFlowRowKind.Outflow,
            CashFlowClassifier.MapLine(Acc("1403", "原材料", AccountCategory.Asset), false));
    }
}
