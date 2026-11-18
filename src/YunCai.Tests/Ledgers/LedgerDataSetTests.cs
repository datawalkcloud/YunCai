using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Assist.Domain;
using YunCai.Modules.Ledgers.Application;
using Xunit;

namespace YunCai.Tests.Ledgers;

/// <summary>
/// 账簿装载结果 <see cref="LedgerDataSet"/>、余额累加器 <see cref="BalanceAmounts"/>
/// 与编码条件解析器 <see cref="AccountCodeSelector"/> 的口径测试（纯内存，不查库）。
/// 领域依据（AGENTS.md「领域须知」）：余额内部一律按「借 - 贷」净额累计，只在输出层用 SplitBySign 拆回两栏；
/// 上级科目行恒等于下级之和；会计期间标识 yyyyMM；显示条件不参与合计；
/// 辅助核算多选时同一分录对每个档案各计一次全额；本期无发生额但期初有数的档案仍要出余额。
/// </summary>
public sealed class LedgerDataSetTests
{
    // 每个用例一套独立科目对象：Account.AddChild 会改父级（挂子级、末级标志置假），
    // 静态共享会让「下级之和」这类断言随执行顺序漂移。
    private readonly AccountSet _set = AccountSet.Create(
        "口径测试账套", "口径测试账套", AccountingStandard.Enterprise, TaxpayerType.General, "人民币", 2026, 1);

    private readonly Account _cash;
    private readonly Account _receivable;
    private readonly Guid _itemA = Guid.NewGuid();
    private readonly Guid _itemB = Guid.NewGuid();

    public LedgerDataSetTests()
    {
        _cash = Account.CreateRoot(_set.Id, "1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        _receivable = Account.CreateRoot(_set.Id, "1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit);
    }

    /// <summary>造一条事实行：科目 + 借/贷，日期与凭证字号可省（默认 2026-03-10 记-1）。</summary>
    private static LedgerFact Fact(
        Guid accountId,
        decimal debit,
        decimal credit,
        DateOnly? date = null,
        string word = "记",
        int number = 1,
        int line = 1,
        Guid? voucherId = null,
        IReadOnlyList<AssistRef>? assists = null)
    {
        var day = date ?? new DateOnly(2026, 3, 10);
        return new LedgerFact
        {
            VoucherId = voucherId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Date = day,
            Period = LedgerDataSet.PeriodOf(day),
            Word = word,
            Number = number,
            LineNumber = line,
            Summary = $"{word}-{number} 第{line}行",
            AccountId = accountId,
            Debit = debit,
            Credit = credit,
            Preparer = "测试制单人",
            Reviewer = "测试审核人",
            AttachmentCount = 1,
            Assists = assists ?? [],
        };
    }

    private LedgerDataSet DataSet(
        IReadOnlyList<Account> accounts,
        IReadOnlyList<LedgerFact> facts,
        IReadOnlyDictionary<Guid, decimal>? opening = null,
        IReadOnlyDictionary<BalanceKey, decimal>? openingAssist = null,
        LedgerQueryOptions? options = null)
        => new()
        {
            AccountSet = _set,
            Options = options ?? new LedgerQueryOptions(),
            StartDate = new DateOnly(2026, 3, 1),
            EndDate = new DateOnly(2026, 3, 31),
            Accounts = accounts,
            AccountsById = accounts.ToDictionary(a => a.Id),
            AssistItemsById = new Dictionary<Guid, AssistItem>(),
            Facts = facts,
            OpeningByAccount = opening ?? new Dictionary<Guid, decimal>(),
            OpeningByAccountAssist = openingAssist ?? new Dictionary<BalanceKey, decimal>(),
            UnpostedVoucherCount = 0,
        };

    private List<Account> All(params Account[] extra) => [_cash, _receivable, .. extra];

    // —— 期间标识与净额累加 ——

    [Theory]
    [InlineData(2026, 1, 1, 202601)]
    [InlineData(2026, 9, 30, 202609)]
    [InlineData(2026, 12, 31, 202612)]
    public void PeriodOf_ReturnsYyyyMM(int year, int month, int day, int expected)
    {
        // 会计期间统一 yyyyMM：不是月份数字，也不是丢前导零的写法
        Assert.Equal(expected, LedgerDataSet.PeriodOf(new DateOnly(year, month, day)));
    }

    [Fact]
    public void BalanceAmounts_EndingNet_IsOpeningPlusDebitMinusCredit()
    {
        var balance = new BalanceAmounts(OpeningNet: 1_000m, PeriodDebit: 300m, PeriodCredit: 120m);

        Assert.Equal(1_180m, balance.EndingNet);
        Assert.Equal(180m, balance.PeriodNet);
        Assert.False(balance.IsEmpty);
    }

    [Fact]
    public void SplitBySign_SplitsNetBackIntoDebitAndCredit()
    {
        // InlineData 传不了 decimal 字面量，这里按三种情形显式列出来
        (decimal net, decimal debit, decimal credit)[] cases =
        [
            (500m, 500m, 0m),    // 借方余额留在借方
            (0m, 0m, 0m),        // 零余额记借方
            (-500m, 0m, 500m),   // 贷方余额取绝对值进贷方
        ];

        foreach (var (net, expectedDebit, expectedCredit) in cases)
        {
            var (debit, credit) = BalanceAmounts.SplitBySign(net);

            Assert.Equal(expectedDebit, debit);
            Assert.Equal(expectedCredit, credit);
            // 拆回去再减一次必须还原净额，否则「借-贷」口径就白定了
            Assert.Equal(net, debit - credit);
        }
    }

    [Fact]
    public void IsEmpty_OnlyWhenNoOpeningAndNoActivity()
    {
        Assert.True(new BalanceAmounts(0m, 0m, 0m).IsEmpty);
        Assert.False(new BalanceAmounts(0m, 10m, 10m).IsEmpty); // 本期有发生额（即便净额为 0）不算空
        Assert.False(new BalanceAmounts(1m, 0m, 0m).IsEmpty);   // 期初有数不算空
        Assert.False(new BalanceAmounts(0m, 0m, 5m).IsEmpty);   // 只有贷方发生额也不算空
    }

    // —— 两级聚合 ——

    [Fact]
    public void Balances_PeriodActivity_IsAccumulatedAsDebitMinusCreditPerAccount()
    {
        var dataSet = DataSet([_cash], [
            Fact(_cash.Id, 300m, 0m),
            Fact(_cash.Id, 0m, 100m, line: 2),
        ]);

        var balance = dataSet.Balances.ByAccount[_cash.Id];

        Assert.Equal(300m, balance.PeriodDebit);
        Assert.Equal(100m, balance.PeriodCredit);
        Assert.Equal(200m, balance.EndingNet);
    }

    [Fact]
    public void Balances_ParentRow_EqualsSumOfSubtree()
    {
        var childA = _receivable.AddChild("112201", "甲公司");
        var childB = _receivable.AddChild("112202", "乙公司");
        var dataSet = DataSet(All(childA, childB), [
            Fact(childA.Id, 500m, 0m),
            Fact(childB.Id, 0m, 200m),
        ]);

        var parent = dataSet.GetAccountBalance(_receivable);

        // 总账/余额表的上级行 = 全部下级之和（AGENTS.md：保证账页之间对得上）
        Assert.Equal(500m, parent.PeriodDebit);
        Assert.Equal(200m, parent.PeriodCredit);
        Assert.Equal(300m, parent.EndingNet);
    }

    [Fact]
    public void Balances_ParentWithOwnPosting_IncludesItsOwnLine()
    {
        // 历史数据里直接挂在非末级科目上的分录不能被下级合计丢掉
        var child = _receivable.AddChild("112201", "甲公司");
        var dataSet = DataSet(All(child), [
            Fact(_receivable.Id, 100m, 0m),
            Fact(child.Id, 50m, 0m),
        ]);

        Assert.Equal(150m, dataSet.GetAccountBalance(_receivable).PeriodDebit);
    }

    [Fact]
    public void Balances_ParentOpeningNet_AlsoRollsUpFromChildren()
    {
        var childA = _receivable.AddChild("112201", "甲公司");
        var childB = _receivable.AddChild("112202", "乙公司");
        var dataSet = DataSet(All(childA, childB), [], opening: new Dictionary<Guid, decimal>
        {
            [childA.Id] = 300m,
            [childB.Id] = -80m,
        });

        var parent = dataSet.GetAccountBalance(_receivable);

        Assert.Equal(220m, parent.OpeningNet);
        Assert.Equal(220m, parent.EndingNet);
    }

    [Fact]
    public void Balances_AssistMultiSelection_CountsFullAmountForEveryItem()
    {
        var entry = Fact(_cash.Id, 400m, 0m,
            assists: [new AssistRef(AssistType.Customer, _itemA), new AssistRef(AssistType.Customer, _itemB)]);
        var dataSet = DataSet([_cash], [entry]);

        var a = dataSet.Balances.ByAccountAssist[new BalanceKey(_cash.Id, AssistType.Customer, _itemA)];
        var b = dataSet.Balances.ByAccountAssist[new BalanceKey(_cash.Id, AssistType.Customer, _itemB)];

        // 同一分录挂同一类别两个档案：各计一次全额（多选辅助核算的通行口径），而不是对半拆
        Assert.Equal(400m, a.PeriodDebit);
        Assert.Equal(400m, b.PeriodDebit);
    }

    [Fact]
    public void Balances_DifferentAssistDimensions_AreSeparateKeys()
    {
        var entry = Fact(_cash.Id, 100m, 0m,
            assists: [new AssistRef(AssistType.Customer, _itemA), new AssistRef(AssistType.Department, _itemA)]);
        var dataSet = DataSet([_cash], [entry]);

        // 同一个档案在两个维度下各算一遍，科目×档案×类别是聚合主键
        Assert.Equal(2, dataSet.Balances.ByAccountAssist.Count);
        Assert.Equal(100m, dataSet.Balances.ByAccountAssist[new BalanceKey(_cash.Id, AssistType.Customer, _itemA)].PeriodDebit);
        Assert.Equal(100m, dataSet.Balances.ByAccountAssist[new BalanceKey(_cash.Id, AssistType.Department, _itemA)].PeriodDebit);
    }

    [Fact]
    public void Balances_AccountWithOpeningButNoActivity_StillGetsRow()
    {
        var dataSet = DataSet([_cash], [], opening: new Dictionary<Guid, decimal> { [_cash.Id] = 9_999m });

        var balance = dataSet.Balances.ByAccount[_cash.Id];

        Assert.Equal(9_999m, balance.OpeningNet);
        Assert.Equal(9_999m, balance.EndingNet);
        Assert.False(balance.IsEmpty);
    }

    // —— 辅助核算档案集合 ——

    [Fact]
    public void AssistItemIds_IncludesItemsSeenOnlyInPriorPeriodOpening()
    {
        // 本期完全没有发生额、但期初挂过账的档案仍要出现在余额表里，否则余额凭空少一块
        var dataSet = DataSet(
            [_cash],
            [],
            openingAssist: new Dictionary<BalanceKey, decimal>
            {
                [new BalanceKey(_cash.Id, AssistType.Customer, _itemA)] = 100m,
            });

        Assert.Equal([_itemA], dataSet.AssistItemIds(AssistType.Customer, [_cash.Id]).ToArray());
    }

    [Fact]
    public void AssistItemIds_DoesNotLeakAcrossDimensionsOrAccounts()
    {
        var entry = Fact(_cash.Id, 10m, 0m, assists: [new AssistRef(AssistType.Department, _itemA)]);
        var dataSet = DataSet(All(), [entry]);

        Assert.Empty(dataSet.AssistItemIds(AssistType.Customer, [_cash.Id]));
        Assert.Equal([_itemA], dataSet.AssistItemIds(AssistType.Department, [_cash.Id]).ToArray());
        Assert.Empty(dataSet.AssistItemIds(AssistType.Department, [_receivable.Id]));
    }

    [Fact]
    public void AssistItemIds_CoversWholeSubtreeWhenGivenParentIds()
    {
        var child = _receivable.AddChild("112201", "甲公司");
        var entry = Fact(child.Id, 0m, 60m, assists: [new AssistRef(AssistType.Customer, _itemA)]);
        var dataSet = DataSet(All(child), [entry]);

        var ids = dataSet.AssistItemIds(AssistType.Customer, dataSet.SubtreeAccountIds(_receivable));

        // 核算项目余额表按科目合计 == 科目余额表该科目：上级取数必须把下级的档案带上
        Assert.Equal([_itemA], ids.ToArray());
    }

    // —— 科目子树与路径 ——

    [Fact]
    public void SubtreeAccountIds_ContainsSelfAndDescendantsOrderedByCode()
    {
        var b2 = _receivable.AddChild("112202", "乙公司");
        var b1 = _receivable.AddChild("112201", "甲公司");
        var b11 = b1.AddChild("11220101", "甲公司投标");
        var dataSet = DataSet(All(b1, b2, b11), []);

        var ids = dataSet.SubtreeAccountIds(_receivable);

        // 含自身，同层按编码排；层级按上级链路展开，不依赖编码前缀约定
        Assert.Equal([_receivable.Id, b1.Id, b11.Id, b2.Id], ids.ToArray());
    }

    [Fact]
    public void AccountPath_JoinsNamesFromRootToSelf()
    {
        var child = _receivable.AddChild("112201", "甲公司");
        var dataSet = DataSet(All(child), []);

        Assert.Equal("应收账款 / 甲公司", dataSet.AccountPath(child));
        Assert.Equal("库存现金", dataSet.AccountPath(_cash));
    }

    // —— 显示条件与页头状态 ——

    [Fact]
    public void SelectAccounts_LeafOnlyByDefault_AndRespectsMaxLevelAndCodeInterval()
    {
        var child = _receivable.AddChild("112201", "甲公司");
        var all = All(child);

        Assert.DoesNotContain(_receivable, DataSet(all, []).SelectAccounts());

        var withNonLeaf = DataSet(all, [], options: new LedgerQueryOptions { ShowOnlyLeaf = false }).SelectAccounts();
        Assert.Contains(_receivable, withNonLeaf);

        // 区间端点等长视为闭区间：1001-1122 覆盖 1001 与 1122 系，不含 1501
        var longTerm = Account.CreateRoot(_set.Id, "1501", "长期股权投资", AccountCategory.Asset, BalanceDirection.Debit);
        var interval = DataSet(
            [.. all, longTerm],
            [],
            options: new LedgerQueryOptions { ShowOnlyLeaf = false, AccountFrom = "1001", AccountTo = "1122" }).SelectAccounts();
        Assert.Equal([_cash.Id, _receivable.Id, child.Id], interval.Select(a => a.Id).ToArray());

        // 级次上限：MaxLevel=1 时二级明细不列示
        var capped = DataSet(all, [], options: new LedgerQueryOptions { ShowOnlyLeaf = false, MaxLevel = 1 }).SelectAccounts();
        Assert.DoesNotContain(child, capped);
        Assert.Contains(_receivable, capped);
    }

    [Fact]
    public void SelectAccounts_PrefixCondition_PullsWholeSubtree()
    {
        var child = _receivable.AddChild("112201", "甲公司");
        var all = All(child);

        var leafOnly = DataSet(all, [], options: new LedgerQueryOptions { AccountFrom = "1122" }).SelectAccounts();
        var withNonLeaf = DataSet(all, [], options: new LedgerQueryOptions { AccountFrom = "1122", ShowOnlyLeaf = false }).SelectAccounts();

        // 选中 1122 即带出 112201（前缀语义），现金不沾边；父级是否列示仍受「只列最明细」支配
        Assert.Equal([child.Id], leafOnly.Select(a => a.Id).ToArray());
        Assert.Equal([_receivable.Id, child.Id], withNonLeaf.Select(a => a.Id).ToArray());
    }

    [Fact]
    public void FactsOfVoucher_ReturnsAllLegsOfThatVoucherInLoadOrder()
    {
        var voucherId = Guid.NewGuid();
        var dataSet = DataSet(All(), [
            Fact(_cash.Id, 100m, 0m, voucherId: voucherId, line: 1),
            Fact(_cash.Id, 30m, 0m, voucherId: voucherId, line: 2),
            Fact(_receivable.Id, 0m, 130m, line: 1),
        ]);

        Assert.Equal([1, 2], dataSet.FactsOfVoucher(voucherId).Select(f => f.LineNumber).ToArray());
        Assert.Empty(dataSet.FactsOfVoucher(Guid.NewGuid()));
    }

    [Fact]
    public void FactsByAccount_GroupsLoadedFacts()
    {
        var child = _receivable.AddChild("112201", "甲公司");
        var dataSet = DataSet(All(child), [
            Fact(_cash.Id, 10m, 0m),
            Fact(child.Id, 0m, 10m),
            Fact(_cash.Id, 20m, 0m, line: 2),
        ]);

        Assert.Equal(2, dataSet.FactsByAccount[_cash.Id].Count());
        Assert.Single(dataSet.FactsByAccount[child.Id]);
        Assert.Empty(dataSet.FactsByAccount[Guid.NewGuid()]);
    }

    [Fact]
    public void ClosedPeriodState_ReflectsAccountSetPointers()
    {
        var dataSet = DataSet([_cash], []);

        // 尚未结账：页头不显示已结期间，期末判「未结账」但账页照常可查
        Assert.Null(_set.ClosedPeriodText());
        Assert.False(dataSet.IsEndPeriodClosed);

        // 结转先于结账、按月顺序推进（AGENTS.md）：推到 2026-03 后期末即已结账
        _set.MarkCarried(2026, 1);
        _set.ClosePeriod(2026, 1);
        _set.MarkCarried(2026, 2);
        _set.ClosePeriod(2026, 2);
        _set.MarkCarried(2026, 3);
        _set.ClosePeriod(2026, 3);

        Assert.Equal("2026-03", _set.ClosedPeriodText());
        Assert.True(dataSet.IsEndPeriodClosed);
    }

    [Fact]
    public void PeriodText_ShowNormalizedMonthBoundaries()
    {
        var dataSet = DataSet([_cash], []);

        Assert.Equal("2026-03-01 至 2026-03-31", dataSet.PeriodText);
        Assert.Equal(202603, dataSet.StartPeriod);
        Assert.Equal(202603, dataSet.EndPeriod);
    }

    // —— 编码条件解析与查询条件模型 ——

    [Fact]
    public void AccountExpression_EqualLengthBecomesInterval_UnequalBecomesTwoPrefixes()
    {
        Assert.Equal("1001-1122", new LedgerQueryOptions { AccountFrom = "1001", AccountTo = "1122" }.AccountExpression);
        Assert.Equal("1001,112201", new LedgerQueryOptions { AccountFrom = "1001", AccountTo = "112201" }.AccountExpression);
        Assert.Equal("1001", new LedgerQueryOptions { AccountFrom = "1001" }.AccountExpression);
        Assert.Equal("1122", new LedgerQueryOptions { AccountTo = "1122" }.AccountExpression);
    }

    [Fact]
    public void Parse_UnequalLengthInterval_ThrowsChineseHint()
    {
        var error = Assert.Throws<LedgerException>(() => AccountCodeSelector.Parse("1001-112201"));

        Assert.Equal("科目编码区间「1001-112201」不合法：两端必须为等长的纯数字编码。", error.Message);
    }

    [Fact]
    public void Parse_ReversedInterval_ThrowsChineseHint()
    {
        var error = Assert.Throws<LedgerException>(() => AccountCodeSelector.Parse("1122-1001"));

        Assert.Equal("科目编码区间「1122-1001」起始值不能大于终止值。", error.Message);
    }

    [Fact]
    public void Parse_IntervalWiderThanSpanLimit_ThrowsChineseHint()
    {
        // 跨度上限 5000 个编码，超了要提示改用逗号，否则会一次性展开出巨量前缀
        var error = Assert.Throws<LedgerException>(() => AccountCodeSelector.Parse("0001-9999"));

        Assert.Equal("科目编码区间「0001-9999」跨度超过 5000 个编码，请改用逗号分隔或更宽的级次条件。", error.Message);
    }

    [Fact]
    public void Validate_ReturnsHintInsteadOfThrowing_ForQueryStage()
    {
        // 查询条件模型在校验阶段复用解析，不能把异常抛到控制器
        Assert.Equal(
            "科目编码区间「1122-1001」起始值不能大于终止值。",
            AccountCodeSelector.Validate("1122-1001"));
        Assert.Null(AccountCodeSelector.Validate("1001,1122"));
        Assert.Null(AccountCodeSelector.Validate(null));
    }

    [Fact]
    public void Matches_EmptyExpression_SelectsEverything()
    {
        var selector = AccountCodeSelector.Parse("  ");
        var child = _receivable.AddChild("112201", "甲公司");

        Assert.True(selector.MatchAll);
        Assert.True(selector.Matches(_cash));
        Assert.True(selector.Matches(child));
    }

    [Fact]
    public void Matches_MultipleCommaSegments_AreIndependent()
    {
        // 「1001」按前缀带下级；「1501-1502」展开成 1501、1502 两个编码，各自再按前缀带下级
        var selector = AccountCodeSelector.Parse("1001,1501-1502");
        var longTerm = Account.CreateRoot(_set.Id, "1501", "长期股权投资", AccountCategory.Asset, BalanceDirection.Debit);
        var longTermChild = longTerm.AddChild("150101", "股票投资");
        var other = Account.CreateRoot(_set.Id, "2101", "应付账款", AccountCategory.Liability, BalanceDirection.Credit);
        var nearMiss = Account.CreateRoot(_set.Id, "1511", "其他股权投资", AccountCategory.Asset, BalanceDirection.Debit);

        Assert.True(selector.Matches(_cash));
        Assert.True(selector.Matches(longTerm));
        Assert.True(selector.Matches(longTermChild));
        Assert.False(selector.Matches(nearMiss)); // 区间只展开到 1502，不能顺手吃掉 1511
        Assert.False(selector.Matches(other));
    }
}
