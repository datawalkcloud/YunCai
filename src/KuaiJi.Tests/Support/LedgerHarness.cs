using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Accounting.Infrastructure;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.AccountSets.Infrastructure;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Assist.Infrastructure;
using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.Modules.Ledgers.Infrastructure;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Tests.Support;

/// <summary>
/// 账簿与报表测试装置：在 <see cref="SqliteTestContext"/> 之上补齐造数能力——
/// 任意类别/方向/层级的科目、期初、以及**指定状态**的凭证（可带辅助核算）。
/// <para>
/// 不复用 <c>SqliteTestContext.AddVoucher</c> 的原因：那条固定「借 100 / 贷 100」两行且停在草稿，
/// 而账簿与报表只认已过账凭证（AGENTS.md「领域须知」：草稿/已审核未过账不入账），
/// 必须能精确控制科目、方向、金额与状态，否则口径类断言无法成立。
/// </para>
/// </summary>
internal sealed class LedgerHarness : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private int _nextNumber = 1;

    public LedgerHarness(string accountSetName = "账簿测试账套")
    {
        AccountSet = _db.AddAccountSet(accountSetName); // 启用期间 2026-01，见 SqliteTestContext
    }

    public AccountSet AccountSet { get; }

    public AppDbContext Context => _db.Context;

    /// <summary>一次模拟「请求结束、DbContext 丢弃」，避免跟踪器与后续 Remove/Update 撞主键。</summary>
    public void DetachAll() => _db.DetachAll();

    /// <summary>装配账簿取数链路：真实 EF 实现 LedgerSource + 装载工厂，与生产 DI 注册一致。</summary>
    public LedgerDataSetFactory NewFactory() => new(
        new AccountSetRepository(Context),
        new AccountRepository(Context),
        new AssistItemRepository(Context),
        new LedgerSource(Context));

    /// <summary>建根科目。<c>opening</c> 为期初余额（按科目方向为正，录在末级科目上）。</summary>
    public Account AddAccount(
        string code,
        string name,
        AccountCategory category,
        BalanceDirection direction,
        decimal opening = 0m)
    {
        var account = Account.CreateRoot(AccountSet.Id, code, name, category, direction);
        if (opening != 0m)
        {
            account.SetOpeningBalance(opening);
        }

        Context.Accounts.Add(account);
        Context.SaveChanges();
        DetachAll();
        return account;
    }

    /// <summary>
    /// 在父科目下建明细科目（类别继承父级，方向可覆盖）。父科目自动变为非末级。
    /// 两处坑：① 子科目既挂在 Children 导航里又显式 <c>Add</c> 会撞 accounts(account_set_id, code) 唯一键；
    /// ② 复用调用方手里的旧实例（其导航里还挂着上一轮已入库的兄弟科目）会把兄弟再插一遍。
    /// 所以一律重新取一份带跟踪的父级，只让本次新增的子科目进图。
    /// </summary>
    public Account AddChild(
        Account parent,
        string code,
        string name,
        BalanceDirection? direction = null,
        decimal opening = 0m)
    {
        var tracked = Context.Accounts.Find(parent.Id)
            ?? throw new InvalidOperationException($"父科目「{parent.Code}」尚未入库，不能挂子科目。");

        var child = tracked.AddChild(code, name, direction);
        if (opening != 0m)
        {
            child.SetOpeningBalance(opening);
        }

        Context.SaveChanges();
        DetachAll();
        return child;
    }

    /// <summary>再建一个账套（用于「数据按账套隔离」的口径验证）。</summary>
    public AccountSet AddExtraAccountSet(string name)
    {
        var accountSet = AccountSet.Create(name, name, AccountingStandard.Enterprise, TaxpayerType.General, "人民币", 2026, 1);
        Context.AccountSets.Add(accountSet);
        Context.SaveChanges();
        return accountSet;
    }

    /// <summary>建辅助核算档案。</summary>
    public AssistItem AddAssistItem(AssistType type, string code, string name)
    {
        var item = AssistItem.Create(AccountSet.Id, type, code, name);
        Context.AssistItems.Add(item);
        Context.SaveChanges();
        return item;
    }

    /// <summary>分录种子：摘要 + 科目 + 借/贷 + 可选辅助核算取值。</summary>
    internal sealed record SeedEntry(string Summary, Guid AccountId, decimal Debit, decimal Credit, IReadOnlyList<AssistRef> Assists);

    public SeedEntry Entry(string summary, Guid accountId, decimal debit, decimal credit, params AssistRef[] assists)
        => new(summary, accountId, debit, credit, assists);

    public AssistRef Ref(AssistType type, AssistItem item) => new(type, item.Id);

    /// <summary>
    /// 记一张凭证并按 <paramref name="status"/> 推进状态：草稿不审核，已审核只审核，已过账审核+过账。
    /// 凭证字固定「记」，号自增；日期与状态由用例掌控，这是口径断言的关键变量。
    /// </summary>
    public Voucher AddVoucher(DateOnly date, VoucherStatus status, params SeedEntry[] entries)
        => AddVoucher("记", date, status, entries);

    public Voucher AddVoucher(string word, DateOnly date, VoucherStatus status, params SeedEntry[] entries)
    {
        var voucher = Voucher.Create(AccountSet.Id, word, _nextNumber++, date, "测试制单人");
        foreach (var entry in entries)
        {
            voucher.AddEntry(entry.Summary, entry.AccountId, entry.Debit, entry.Credit, entry.Assists);
        }

        if (status >= VoucherStatus.Reviewed)
        {
            voucher.Review("测试审核人");
        }

        if (status == VoucherStatus.Posted)
        {
            voucher.Post();
        }

        Context.Vouchers.Add(voucher);
        Context.SaveChanges();
        DetachAll();
        return voucher;
    }

    /// <summary>往指定账套记凭证（跨账套隔离验证用）；科目必须属于该账套。</summary>
    public Voucher AddVoucherTo(AccountSet target, DateOnly date, VoucherStatus status, params SeedEntry[] entries)
    {
        var voucher = Voucher.Create(target.Id, "记", _nextNumber++, date, "测试制单人");
        foreach (var entry in entries)
        {
            voucher.AddEntry(entry.Summary, entry.AccountId, entry.Debit, entry.Credit, entry.Assists);
        }

        if (status >= VoucherStatus.Reviewed)
        {
            voucher.Review("测试审核人");
        }

        if (status == VoucherStatus.Posted)
        {
            voucher.Post();
        }

        Context.Vouchers.Add(voucher);
        Context.SaveChanges();
        DetachAll();
        return voucher;
    }

    /// <summary>把账套期间指针按月推进到「已结转并结账至」指定月份（结转必须先于结账）。</summary>
    public void CloseThrough(int year, int month)
    {
        var target = year * 100 + month;
        var (y, m) = (AccountSet.StartYear, AccountSet.StartMonth);
        while (y * 100 + m <= target)
        {
            AccountSet.MarkCarried(y, m);
            AccountSet.ClosePeriod(y, m);
            (y, m) = m == 12 ? (y + 1, 1) : (y, m + 1);
        }

        Context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();
}
