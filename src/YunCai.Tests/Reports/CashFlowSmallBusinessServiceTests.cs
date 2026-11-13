using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Assist.Domain;
using YunCai.Modules.Assist.Infrastructure;
using YunCai.Modules.Reports.Application;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Reports;

/// <summary>
/// 小企业会计准则账套的现金流量表：22 行（会小企03表）、筹资流出按本金/利息/分配利润拆分，
/// 净增加额/期初/期末落在 20-22 行，与一般企业 35 行口径并存。
/// </summary>
public sealed class CashFlowSmallBusinessServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly CashFlowService _service;

    public CashFlowSmallBusinessServiceTests()
    {
        _accountSet = _db.AddAccountSet("小准则测试账套", standard: AccountingStandard.SmallBusiness);
        _service = new CashFlowService(
            new AccountSetRepository(_db.Context),
            new AccountRepository(_db.Context),
            new VoucherRepository(_db.Context),
            new AssistItemRepository(_db.Context));
    }

    public void Dispose() => _db.Dispose();

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

    private void PostVoucher(int number, DateOnly date, params (Guid AccountId, decimal Debit, decimal Credit)[] entries)
    {
        var voucher = Voucher.Create(_accountSet.Id, "记", number, date, "测试制单人");
        foreach (var (accountId, debit, credit) in entries)
        {
            voucher.AddEntry("测试分录", accountId, debit, credit);
        }

        voucher.Post();
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();
    }

    private static decimal Current(CashFlowDto dto, int line) => dto.Lines.Single(l => l.Line == line).Current;

    [Fact]
    public async Task GetPeriodAsync_SmallBusinessStandard_Returns22RowsAndSplitsFinancingOutflow()
    {
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, 10000m);
        var revenue = AddAccount("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var capital = AddAccount("4001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);
        var loan = AddAccount("2001", "短期借款", AccountCategory.Liability, BalanceDirection.Credit);
        var finance = AddAccount("6603", "财务费用", AccountCategory.ProfitAndLoss);
        var distribution = AddAccount("4104", "利润分配", AccountCategory.Equity, BalanceDirection.Credit);

        PostVoucher(1, new DateOnly(2026, 3, 5), (bank.Id, 400m, 0m), (revenue.Id, 0m, 400m));
        PostVoucher(2, new DateOnly(2026, 3, 6), (bank.Id, 1000m, 0m), (capital.Id, 0m, 1000m));
        PostVoucher(3, new DateOnly(2026, 3, 7), (bank.Id, 2000m, 0m), (loan.Id, 0m, 2000m));
        PostVoucher(4, new DateOnly(2026, 3, 8), (loan.Id, 500m, 0m), (bank.Id, 0m, 500m));
        PostVoucher(5, new DateOnly(2026, 3, 9), (finance.Id, 100m, 0m), (bank.Id, 0m, 100m));
        PostVoucher(6, new DateOnly(2026, 3, 10), (distribution.Id, 300m, 0m), (bank.Id, 0m, 300m));

        var dto = await _service.GetPeriodAsync(_accountSet.Id, 2026, 3);

        Assert.Equal(Enumerable.Range(1, 22), dto.Lines.Select(l => l.Line));
        Assert.Equal(400m, Current(dto, 1));
        Assert.Equal(400m, Current(dto, 7));
        Assert.Equal(2000m, Current(dto, 14));
        Assert.Equal(1000m, Current(dto, 15));
        Assert.Equal(500m, Current(dto, 16));
        Assert.Equal(100m, Current(dto, 17));
        Assert.Equal(300m, Current(dto, 18));
        Assert.Equal(2100m, Current(dto, 19));
        Assert.Equal(2500m, Current(dto, 20));
        Assert.Equal(10000m, Current(dto, 21));
        Assert.Equal(12500m, Current(dto, 22));

        // 本年累计与本期一致；期末余额与现金科目实际变动 10000+2500 对齐。
        Assert.Equal(2500m, dto.Lines.Single(l => l.Line == 20).YearToDate);
        Assert.Equal(12500m, dto.Lines.Single(l => l.Line == 22).YearToDate);
    }

    [Fact]
    public async Task GetPeriodAsync_SmallBusinessStandard_FoldsTaxRefundAndKeepsInvestingSeparate()
    {
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset);
        var tax = AddAccount("2221", "应交税费", AccountCategory.Liability, BalanceDirection.Credit);
        var equipment = AddAccount("1601", "固定资产", AccountCategory.Asset);

        PostVoucher(1, new DateOnly(2026, 4, 3), (bank.Id, 200m, 0m), (tax.Id, 0m, 200m));
        PostVoucher(2, new DateOnly(2026, 4, 8), (equipment.Id, 1200m, 0m), (bank.Id, 0m, 1200m));

        var dto = await _service.GetPeriodAsync(_accountSet.Id, 2026, 4);

        // 一般企业第 2 行「收到的税费返还」在小准则并入「收到其他与经营活动有关的现金」。
        Assert.Equal(200m, Current(dto, 2));
        Assert.Equal(200m, Current(dto, 7));
        Assert.Equal(1200m, Current(dto, 12));
        Assert.Equal(-1200m, Current(dto, 13));
        Assert.Equal(-1000m, Current(dto, 20));
        Assert.Equal(-1000m, Current(dto, 22));
    }

    [Fact]
    public async Task GetAssistAsync_SmallBusinessPresetItems_MapInterestAndProfitToSeparateRows()
    {
        var bank = AddAccount("1002", "银行存款", AccountCategory.Asset);
        var loan = AddAccount("2001", "短期借款", AccountCategory.Liability, BalanceDirection.Credit);
        var interest = AssistItem.Create(_accountSet.Id, AssistType.CashFlow, "17", "偿还借款利息支付的现金");
        var dividend = AssistItem.Create(_accountSet.Id, AssistType.CashFlow, "18", "分配利润支付的现金");
        _db.Context.AssistItems.AddRange(interest, dividend);
        _db.Context.SaveChanges();

        var voucher = Voucher.Create(_accountSet.Id, "记", 1, new DateOnly(2026, 5, 9), "测试制单人");
        voucher.AddEntry("偿还利息", bank.Id, 0m, 300m,
            [new AssistRef(AssistType.CashFlow, interest.Id)]);
        voucher.AddEntry("分配利润", bank.Id, 0m, 200m,
            [new AssistRef(AssistType.CashFlow, dividend.Id)]);
        voucher.AddEntry("偿还借款", loan.Id, 500m, 0m);
        voucher.Post();
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();

        var dto = await _service.GetAssistAsync(_accountSet.Id, 2026, 5);

        Assert.Equal(22, dto.Lines.Count);
        Assert.Equal(300m, Current(dto, 17));
        Assert.Equal(200m, Current(dto, 18));
        Assert.Equal(-500m, Current(dto, 19));
        Assert.Equal(-500m, Current(dto, 20));
    }
}
