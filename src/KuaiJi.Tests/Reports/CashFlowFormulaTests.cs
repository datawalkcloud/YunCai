using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Accounting.Infrastructure;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.AccountSets.Infrastructure;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Assist.Infrastructure;
using KuaiJi.Modules.Reports.Application;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.Reports.Domain.CashFlow;
using KuaiJi.Modules.Reports.Infrastructure;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.Modules.Vouchers.Infrastructure;
using KuaiJi.Tests.Support;
using Xunit;

namespace KuaiJi.Tests.Reports;

/// <summary>
/// 现金流量表公式法测试：解析/求值语义、默认公式与直接法口径一致、
/// 自定义公式覆盖与恢复、行次引用顺序校验、现金流辅助项目取数。
/// </summary>
public sealed class CashFlowFormulaTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly CashFlowFormulaService _formulaService;
    private readonly CashFlowService _directService;

    public CashFlowFormulaTests()
    {
        _accountSet = _db.AddAccountSet("公式法测试账套");
        _formulaService = new CashFlowFormulaService(
            new AccountSetRepository(_db.Context),
            new AccountRepository(_db.Context),
            new VoucherRepository(_db.Context),
            new AssistItemRepository(_db.Context),
            new CashFlowFormulaRepository(_db.Context));
        _directService = new CashFlowService(
            new AccountSetRepository(_db.Context),
            new AccountRepository(_db.Context),
            new VoucherRepository(_db.Context),
            new AssistItemRepository(_db.Context));
    }

    public void Dispose() => _db.Dispose();

    // —— 种子助手 ——

    private Account AddAccount(
        string code,
        string name,
        AccountCategory category = AccountCategory.Asset,
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

    private Voucher PostVoucher(
        int number,
        DateOnly date,
        params (Guid AccountId, decimal Debit, decimal Credit, AssistRef[] Assists)[] entries)
    {
        var voucher = Voucher.Create(_accountSet.Id, "记", number, date, "测试制单人");
        foreach (var (accountId, debit, credit, assists) in entries)
        {
            voucher.AddEntry("测试分录", accountId, debit, credit, assists.Length > 0 ? assists : null);
        }

        voucher.Post();
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();
        return voucher;
    }

    private static (Guid AccountId, decimal Debit, decimal Credit, AssistRef[] Assists) Entry(
        Account account, decimal debit, decimal credit, params AssistRef[] assists)
        => (account.Id, debit, credit, assists);

    private static decimal Current(CashFlowDto dto, int line) => dto.Lines.Single(l => l.Line == line).Current;

    private static decimal YearToDate(CashFlowDto dto, int line) => dto.Lines.Single(l => l.Line == line).YearToDate;

    // —— 解析器与求值器 ——

    [Fact]
    public void Parse_ArithmeticPrecedenceAndFunctions_EvaluatesExpectedValue()
    {
        // 10 + 20 × 2 − 100 ÷ 2 = 0 不够有辨识度，换成 10 + 20 × 2 − 4 ÷ 2 = 48。
        var formulas = new Dictionary<int, CashFlowFormulaNode>
        {
            [1] = CashFlowFormulaParser.Parse("10"),
            [2] = CashFlowFormulaParser.Parse("20"),
            [3] = CashFlowFormulaParser.Parse("R1+R2*2-JF(\"1001\")/2"),
        };

        var result = CashFlowFormulaEvaluator.Evaluate(formulas, new FakeContext(debit: 4m));

        Assert.Equal(10m, result[1]);
        Assert.Equal(20m, result[2]);
        Assert.Equal(48m, result[3]);
    }

    [Fact]
    public void Parse_ParenthesesAndUnaryMinus_EvaluatesExpectedValue()
    {
        var formulas = new Dictionary<int, CashFlowFormulaNode>
        {
            [1] = CashFlowFormulaParser.Parse("-(2+3)*2"),
            [2] = CashFlowFormulaParser.Parse("+(5-3)"),
        };

        var result = CashFlowFormulaEvaluator.Evaluate(formulas, new FakeContext());

        Assert.Equal(-10m, result[1]);
        Assert.Equal(2m, result[2]);
    }

    [Fact]
    public void Evaluate_DivisionByZero_ReturnsZero()
    {
        var formulas = new Dictionary<int, CashFlowFormulaNode>
        {
            [1] = CashFlowFormulaParser.Parse("QC(\"1001\")/(DF(\"1001\")-20)"),
        };

        var result = CashFlowFormulaEvaluator.Evaluate(formulas, new FakeContext());

        Assert.Equal(0m, result[1]);
    }

    [Fact]
    public void Evaluate_Functions_ReadContextColumns()
    {
        var formulas = new Dictionary<int, CashFlowFormulaNode>
        {
            [1] = CashFlowFormulaParser.Parse("JF(\"1001\")+DF(\"1001\")+QC(\"1001\")+QM(\"1001\")"),
            [2] = CashFlowFormulaParser.Parse("XJS(\"01\")+XJF(\"01\")+XJ(\"01\")"),
            [3] = CashFlowFormulaParser.Parse("AUTO()"),
        };

        var result = CashFlowFormulaEvaluator.Evaluate(formulas, new FakeContext(debit: 10m));

        Assert.Equal(10m + 20m + 30m + 40m, result[1]);
        Assert.Equal(50m + 60m + 70m, result[2]);
        Assert.Equal(90m, result[3]);
    }

    [Fact]
    public void EnsureRowOrder_ForwardReference_Rejected()
    {
        var node = CashFlowFormulaParser.Parse("R4+1");

        var error = Assert.Throws<ReportException>(() => CashFlowFormulaParser.EnsureRowOrder(node, 4));
        Assert.Contains("R4", error.Message);
    }

    [Fact]
    public void Parse_BackwardReference_AcceptedAndCollected()
    {
        var node = CashFlowFormulaParser.Parse("R1+R2+10");

        CashFlowFormulaParser.EnsureRowOrder(node, 5);
        Assert.Equal(new[] { 1, 2 }, CashFlowFormulaParser.RowReferences(node));
    }

    [Theory]
    [InlineData("FOO(\"1001\")", "不支持的函数")]
    [InlineData("JF()", "需要 1 个字符串参数")]
    [InlineData("(1+2", "缺少右括号")]
    [InlineData("AUTO(\"x\")", "不接受参数")]
    [InlineData("R99", "行次引用")]
    [InlineData("1+", "公式不完整")]
    public void Parse_InvalidExpression_ThrowsWithChineseMessage(string expression, string expected)
    {
        var error = Assert.Throws<ReportException>(() => CashFlowFormulaParser.Parse(expression));
        Assert.Contains(expected, error.Message);
    }

    // —— 默认公式 ——

    [Fact]
    public async Task GetSettingsAsync_ReturnsAllStandardRowsWithDefaults()
    {
        var settings = await _formulaService.GetSettingsAsync(_accountSet.Id);

        Assert.Equal(35, settings.Lines.Count);
        Assert.Equal(Enumerable.Range(1, 35), settings.Lines.Select(l => l.Line));
        Assert.All(settings.Lines, line =>
        {
            Assert.False(line.IsCustomized);
            Assert.Equal(line.DefaultExpression, line.Expression);
            Assert.False(string.IsNullOrWhiteSpace(line.Expression));
        });
        Assert.Equal("R1+R2+R3", settings.Lines.Single(l => l.Line == 4).Expression);
        Assert.Equal(35, CashFlowFormulaDefaults.All.Count);
    }

    [Fact]
    public void DefaultFormulas_SubtotalsReferenceOnlySmallerRows()
    {
        foreach (var (line, expression) in CashFlowFormulaDefaults.All)
        {
            var node = CashFlowFormulaParser.Parse(expression);
            CashFlowFormulaParser.EnsureRowOrder(node, line);
        }
    }

    [Fact]
    public async Task GetPeriodAsync_DefaultFormulas_MatchDirectMethod()
    {
        // 默认公式必须与直接法主表逐行一致：明细行 AUTO() 复用同一套分摊/归类，
        // 小计、净额、期初期末由行次引用汇总。
        var bank = AddAccount("1002", "银行存款", opening: 5000m);
        var cash = AddAccount("1001", "库存现金", opening: 200m);
        var capital = AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var receivable = AddAccount("1122", "应收账款");
        var payable = AddAccount("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit);
        var equipment = AddAccount("1601", "固定资产");
        var admin = AddAccount("6602", "管理费用", AccountCategory.ProfitAndLoss);

        PostVoucher(1, new DateOnly(2026, 2, 10),
            Entry(cash, 400m, 0m),
            Entry(revenue, 0m, 400m));
        PostVoucher(2, new DateOnly(2026, 3, 5),
            Entry(bank, 1000m, 0m),
            Entry(capital, 0m, 700m),
            Entry(revenue, 0m, 300m));
        PostVoucher(3, new DateOnly(2026, 3, 18),
            Entry(bank, 0m, 2000m),
            Entry(equipment, 1500m, 0m),
            Entry(admin, 500m, 0m));
        PostVoucher(4, new DateOnly(2026, 3, 25),
            Entry(bank, 800m, 0m),
            Entry(receivable, 0m, 800m));
        PostVoucher(5, new DateOnly(2026, 3, 28),
            Entry(bank, 0m, 600m),
            Entry(payable, 600m, 0m));
        // 晚于报表期间：本年累计只到 3 月末，6 月凭证不能提前计入 3 月报表。
        PostVoucher(6, new DateOnly(2026, 6, 10),
            Entry(bank, 5000m, 0m),
            Entry(revenue, 0m, 5000m));

        var direct = await _directService.GetPeriodAsync(_accountSet.Id, 2026, 3);
        _db.DetachAll();
        var formula = await _formulaService.GetPeriodAsync(_accountSet.Id, 2026, 3);

        foreach (var line in Enumerable.Range(1, 35))
        {
            Assert.Equal(Current(direct, line), Current(formula, line));
            Assert.Equal(YearToDate(direct, line), YearToDate(formula, line));
            Assert.Equal(direct.Lines.Single(l => l.Line == line).LastYear, formula.Lines.Single(l => l.Line == line).LastYear);
        }
        Assert.Equal(-400m, YearToDate(formula, 33));

        Assert.All(formula.Lines, line => Assert.False(string.IsNullOrWhiteSpace(line.Formula)));
    }

    // —— 自定义公式 ——

    [Fact]
    public async Task SaveAsync_CustomFormula_TakesEffectAndResetRestoresDefault()
    {
        // 第 1 行默认按系统归类 = 300（收入）；改成直接取银行存款借方发生额后得到 1000。
        var bank = AddAccount("1002", "银行存款");
        var capital = AddAccount("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 3, 5),
            Entry(bank, 1000m, 0m),
            Entry(capital, 0m, 700m),
            Entry(revenue, 0m, 300m));

        var saved = await _formulaService.SaveAsync(_accountSet.Id, new SaveCashFlowFormulasRequest
        {
            Formulas = [new SaveCashFlowFormulaItem { Line = 1, Expression = "JF(\"1002\")" }],
        });

        var line1 = saved.Lines.Single(l => l.Line == 1);
        Assert.True(line1.IsCustomized);
        Assert.Equal("JF(\"1002\")", line1.Expression);
        Assert.Equal("AUTO()", line1.DefaultExpression);
        Assert.False(saved.Lines.Single(l => l.Line == 2).IsCustomized);

        _db.DetachAll();
        var custom = await _formulaService.GetPeriodAsync(_accountSet.Id, 2026, 3);
        Assert.Equal(1000m, Current(custom, 1));
        Assert.Equal(1000m, Current(custom, 4)); // 小计仍按行次引用汇总
        Assert.Equal("JF(\"1002\")", custom.Lines.Single(l => l.Line == 1).Formula);

        // 传空表达式 = 删除该行自定义配置，恢复默认 AUTO() 口径。
        await _formulaService.SaveAsync(_accountSet.Id, new SaveCashFlowFormulasRequest
        {
            Formulas = [new SaveCashFlowFormulaItem { Line = 1, Expression = "  " }],
        });
        _db.DetachAll();

        var restored = await _formulaService.GetPeriodAsync(_accountSet.Id, 2026, 3);
        Assert.Equal(300m, Current(restored, 1));
        Assert.Equal("AUTO()", restored.Lines.Single(l => l.Line == 1).Formula);

        var settings = await _formulaService.GetSettingsAsync(_accountSet.Id);
        Assert.All(settings.Lines, l => Assert.False(l.IsCustomized));
    }

    [Fact]
    public async Task SaveAsync_ForwardRowReference_RejectedWithLineName()
    {
        var error = await Assert.ThrowsAsync<ReportException>(() => _formulaService.SaveAsync(
            _accountSet.Id,
            new SaveCashFlowFormulasRequest
            {
                Formulas = [new SaveCashFlowFormulaItem { Line = 4, Expression = "R4+1" }],
            }));

        Assert.Contains("第 4 行", error.Message);
        Assert.Contains("R4", error.Message);
    }

    [Fact]
    public async Task SaveAsync_InvalidSyntaxOrUnknownLine_Rejected()
    {
        var syntax = await Assert.ThrowsAsync<ReportException>(() => _formulaService.SaveAsync(
            _accountSet.Id,
            new SaveCashFlowFormulasRequest
            {
                Formulas = [new SaveCashFlowFormulaItem { Line = 1, Expression = "JF(1001)" }],
            }));
        Assert.Contains("第 1 行取数公式无效", syntax.Message);

        var unknown = await Assert.ThrowsAsync<ReportException>(() => _formulaService.SaveAsync(
            _accountSet.Id,
            new SaveCashFlowFormulasRequest
            {
                Formulas = [new SaveCashFlowFormulaItem { Line = 99, Expression = "0" }],
            }));
        Assert.Contains("不存在行次 99", unknown.Message);

        var duplicate = await Assert.ThrowsAsync<ReportException>(() => _formulaService.SaveAsync(
            _accountSet.Id,
            new SaveCashFlowFormulasRequest
            {
                Formulas =
                [
                    new SaveCashFlowFormulaItem { Line = 1, Expression = "0" },
                    new SaveCashFlowFormulaItem { Line = 1, Expression = "1" },
                ],
            }));
        Assert.Contains("重复提交", duplicate.Message);
    }

    [Fact]
    public async Task SaveAsync_FormulasAreIsolatedByAccountSet()
    {
        // 账套 A 的自定义公式不能泄漏到账套 B。
        var bank = AddAccount("1002", "银行存款");
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        PostVoucher(1, new DateOnly(2026, 3, 5), Entry(bank, 1000m, 0m), Entry(revenue, 0m, 1000m));

        await _formulaService.SaveAsync(_accountSet.Id, new SaveCashFlowFormulasRequest
        {
            Formulas = [new SaveCashFlowFormulaItem { Line = 1, Expression = "JF(\"1002\")" }],
        });
        _db.DetachAll();

        var other = _db.AddAccountSet("另一账套");
        var settings = await _formulaService.GetSettingsAsync(other.Id);
        Assert.All(settings.Lines, l => Assert.False(l.IsCustomized));

        var otherDto = await _formulaService.GetPeriodAsync(other.Id, 2026, 3);
        Assert.Equal(0m, Current(otherDto, 1));
        Assert.Equal("AUTO()", otherDto.Lines.Single(l => l.Line == 1).Formula);
    }

    // —— 现金流辅助项目 ——

    [Fact]
    public async Task GetPeriodAsync_CashFlowAssistItems_FeedInflowOutflowAndNetFunctions()
    {
        // 现金分录挂现金流辅助项目：XJS/XJF/XJ 按项目编码取数（流出为正数），
        // 编码前缀匹配让父项目包含下级项目。
        var bank = AddAccount("1002", "银行存款");
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var payable = AddAccount("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit);
        var salesItem = AssistItem.Create(_accountSet.Id, AssistType.CashFlow, "01", "销售商品收到的现金");
        var purchaseItem = AssistItem.Create(_accountSet.Id, AssistType.CashFlow, "02", "购买商品支付的现金");
        _db.Context.AssistItems.AddRange(salesItem, purchaseItem);
        _db.Context.SaveChanges();

        PostVoucher(1, new DateOnly(2026, 3, 5),
            Entry(bank, 600m, 0m, new AssistRef(AssistType.CashFlow, salesItem.Id)),
            Entry(revenue, 0m, 600m));
        PostVoucher(2, new DateOnly(2026, 3, 20),
            Entry(bank, 0m, 200m, new AssistRef(AssistType.CashFlow, purchaseItem.Id)),
            Entry(payable, 200m, 0m));

        await _formulaService.SaveAsync(_accountSet.Id, new SaveCashFlowFormulasRequest
        {
            Formulas =
            [
                new SaveCashFlowFormulaItem { Line = 1, Expression = "XJS(\"01\")" },
                new SaveCashFlowFormulaItem { Line = 2, Expression = "XJF(\"02\")" },
                new SaveCashFlowFormulaItem { Line = 3, Expression = "XJ(\"01\")" },
                new SaveCashFlowFormulaItem { Line = 5, Expression = "XJ(\"02\")" },
                new SaveCashFlowFormulaItem { Line = 6, Expression = "XJS(\"0\")" },
                new SaveCashFlowFormulaItem { Line = 33, Expression = "XJ(\"01\")+XJ(\"02\")" },
            ],
        });
        _db.DetachAll();

        var dto = await _formulaService.GetPeriodAsync(_accountSet.Id, 2026, 3);

        Assert.Equal(600m, Current(dto, 1));   // 流入
        Assert.Equal(200m, Current(dto, 2));   // 流出（XJF 以正数返回）
        Assert.Equal(600m, Current(dto, 3));   // 净额 = 流入
        Assert.Equal(-200m, Current(dto, 5));  // 净额 = 流入 − 流出
        Assert.Equal(600m, Current(dto, 6));   // 前缀 "0" 命中项目 01、02 的流入
        Assert.Equal(400m, Current(dto, 33));  // 600 − 200
    }

    /// <summary>解析/求值单元测试用的固定取数上下文。</summary>
    private sealed class FakeContext : ICashFlowFormulaContext
    {
        private readonly decimal _debit;

        public FakeContext(decimal debit = 0m)
        {
            _debit = debit;
        }

        public decimal Debit(string accountCode) => _debit;

        public decimal Credit(string accountCode) => 20m;

        public decimal Opening(string accountCode) => 30m;

        public decimal Closing(string accountCode) => 40m;

        public decimal CashInflow(string itemCode) => 50m;

        public decimal CashOutflow(string itemCode) => 60m;

        public decimal CashNet(string itemCode) => 70m;

        public decimal Auto(int line) => 90m;
    }
}
