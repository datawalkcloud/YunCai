using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.Funds.Application;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Funds.Infrastructure;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace YunCai.Tests.Funds;

/// <summary>
/// 日记账（资金账户）应用服务测试。依据 AGENTS.md「出纳闭环（Modules/Funds）」：
/// 流水记账的收入/支出金额口径与逐行余额累计（期初 + 收 − 支）、按日期重排序号（同日保持录入先后）；
/// 停用账户只拦新流水（记账/导入/内部转账），历史数据可查可改可删、报表照常；
/// 已生成凭证的流水不可重复出票、修改保留关联凭证、凭证删除后引用自愈；
/// 批量操作按实际命中计数；流水批量生成凭证按合并方式（全部/日期/类别/方向）分组出票，
/// 对方科目优先统一科目、其次类别「默认对方科目」，任一流水定不了对方科目则整单拒绝。
/// </summary>
public sealed class JournalServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _cashAccount;    // 100101 货币资金-现金（末级）
    private readonly Account _bankAccount;    // 100201 银行存款-工行（末级）
    private readonly Account _incomeAccount;  // 600101 主营业务收入
    private readonly Account _expenseAccount; // 560201 管理费用-办公费
    private readonly JournalRepository _journals;
    private readonly VoucherRepository _vouchers;
    private readonly EntryCategoryRepository _categories;
    private readonly JournalService _service;

    public JournalServiceTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _cashAccount = _db.AddAccount(_accountSet, "100101", "库存现金");
        _bankAccount = _db.AddAccount(_accountSet, "100201", "工行存款");
        _incomeAccount = _db.AddAccount(_accountSet, "600101", "主营业务收入");
        _expenseAccount = _db.AddAccount(_accountSet, "560201", "管理费用-办公费");

        _journals = new JournalRepository(_db.Context);
        _vouchers = new VoucherRepository(_db.Context);
        _categories = new EntryCategoryRepository(_db.Context);
        _service = new JournalService(
            new AccountSetRepository(_db.Context),
            new AccountRepository(_db.Context),
            _journals,
            _vouchers,
            _categories);
    }

    public void Dispose() => _db.Dispose();

    // —— 种子助手 ——

    private static DateOnly Mar(int day) => new(2026, 3, day);

    /// <summary>直接经聚合根建一本日记账（可带初始分录），绕开建账校验，聚焦后续被测规则。</summary>
    private Journal SeedJournal(
        JournalType type,
        string name,
        Account account,
        decimal openingBalance = 0m,
        Action<Journal>? seedEntries = null)
    {
        var journal = Journal.Create(_accountSet.Id, type, name, account.Id, openingBalance);
        seedEntries?.Invoke(journal);
        _db.Context.Journals.Add(journal);
        _db.Context.SaveChanges();
        return journal;
    }

    private Voucher SeedVoucher(int number, DateOnly? date = null)
    {
        var voucher = Voucher.Create(_accountSet.Id, "记", number, date ?? Mar(10), "测试制单人");
        voucher.AddEntry("测试分录", _cashAccount.Id, 100m, 0m);
        voucher.AddEntry("测试分录", _expenseAccount.Id, 0m, 100m);
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();
        return voucher;
    }

    private int _categoryCode;

    /// <summary>种子收支类别（绕过服务层判重，编码自增避免撞库唯一索引）。</summary>
    private EntryCategory SeedCategory(EntryCategoryType type, string name, Guid? voucherSubjectId = null)
    {
        _categoryCode++;
        var category = EntryCategory.Create(_accountSet.Id, type, name, $"c{_categoryCode}");
        if (voucherSubjectId is { } sid)
        {
            category.SetVoucherSubject(sid);
        }

        _db.Context.EntryCategories.Add(category);
        _db.Context.SaveChanges();
        return category;
    }

    private static JournalEntryRequest Entry(
        DateOnly date,
        string summary,
        decimal income = 0m,
        decimal expense = 0m,
        string? category = null,
        string? counterparty = null) => new()
        {
            Date = date,
            Summary = summary,
            Income = income,
            Expense = expense,
            Category = category,
            Counterparty = counterparty,
        };

    private async Task<JournalDetailDto> ReloadAsync(Guid journalId)
    {
        _db.DetachAll();
        var detail = await _service.GetAsync(_accountSet.Id, journalId);
        Assert.NotNull(detail);
        return detail;
    }

    // —— 记账口径：金额、方向与逐行余额累计 ——

    [Fact]
    public async Task AddEntryAsync_IncomeThenExpense_AccumulatesLineBalanceFromOpening()
    {
        // 领域口径：逐行余额 = 期初 + 累计收入 − 累计支出；收入记收入栏、支出记支出栏，不混方向。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, openingBalance: 1000m);

        var afterIncome = await _service.AddEntryAsync(_accountSet.Id, journal.Id, Entry(Mar(5), "收到货款", income: 500m));
        Assert.NotNull(afterIncome);
        Assert.Equal(500m, afterIncome!.TotalIncome);
        Assert.Equal(0m, afterIncome.TotalExpense);

        var afterExpense = await _service.AddEntryAsync(_accountSet.Id, journal.Id, Entry(Mar(8), "支付运费", expense: 200m));
        Assert.NotNull(afterExpense);

        Assert.Collection(afterExpense.Entries.OrderBy(e => e.Sequence).ToList(),
            e =>
            {
                Assert.Equal(1, e.Sequence);
                Assert.Equal(500m, e.Income);
                Assert.Equal(0m, e.Expense);
                Assert.Equal(1500m, e.Balance);
            },
            e =>
            {
                Assert.Equal(2, e.Sequence);
                Assert.Equal(0m, e.Income);
                Assert.Equal(200m, e.Expense);
                Assert.Equal(1300m, e.Balance);
            });
        Assert.Equal(1300m, afterExpense.ClosingBalance);
    }

    [Fact]
    public async Task AddEntryAsync_BlankSummary_ThrowsChineseGuard()
    {
        // 域层业务校验必须透出纯净中文：ArgumentException 若带上 paramName，Message 会被拼成
        // 「摘要不能为空。 (Parameter 'summary')」，前端 400 文案就脏了（AGENTS.md「业务校验类 400 透出中文文案」）。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.AddEntryAsync(_accountSet.Id, journal.Id, Entry(Mar(1), "   ", income: 10m)));

        Assert.Equal("摘要不能为空。", ex.Message);
    }

    [Fact]
    public async Task AddEntryAsync_NegativeAmount_ThrowsChineseGuard()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.AddEntryAsync(_accountSet.Id, journal.Id, Entry(Mar(1), "退款", income: -1m)));

        Assert.Equal("收入与支出金额不能为负数。", ex.Message);
    }

    [Fact]
    public async Task AddEntryAsync_PadsWhitespaceOnSummaryAndCategory_TrimsForStorage()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount);

        var detail = await _service.AddEntryAsync(_accountSet.Id, journal.Id,
            Entry(Mar(1), "  收到货款  ", income: 66m, category: " 销售收入 ", counterparty: " 甲公司 "));

        Assert.NotNull(detail);
        var entry = Assert.Single(detail!.Entries);
        Assert.Equal("收到货款", entry.Summary);
        Assert.Equal("销售收入", entry.Category);
        Assert.Equal("甲公司", entry.Counterparty);
    }

    [Fact]
    public async Task AddEntryAsync_DateOutOfOrder_ReindexesSequenceByDateAndKeepsSameDayInsertionOrder()
    {
        // 期间与日期边界：账簿按日期重排序号与逐行余额，同日期保持录入先后（稳定排序）。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(5), "三月五收款", 100m, 0m);
            j.AddEntry(Mar(1), "三月一收款", 50m, 0m);
        });

        var detail = await _service.AddEntryAsync(_accountSet.Id, journal.Id, Entry(Mar(1), "三月一再收", income: 25m));

        Assert.NotNull(detail);
        var ordered = detail!.Entries.OrderBy(e => e.Sequence).ToList();
        Assert.Equal(3, ordered.Count);
        Assert.Equal([Mar(1), Mar(1), Mar(5)], ordered.Select(e => e.Date).ToList());
        Assert.Equal("三月一收款", ordered[0].Summary);
        Assert.Equal("三月一再收", ordered[1].Summary);
        // 余额按新顺序重算：50 → 75 → 175。
        Assert.Equal([50m, 75m, 175m], ordered.Select(e => e.Balance).ToList());
        Assert.Equal([1, 2, 3], ordered.Select(e => e.Sequence).ToList());
    }

    [Fact]
    public async Task UpdateAsync_ChangedOpeningBalance_RecomputesEveryLineBalance()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, openingBalance: 1000m, seedEntries: j =>
        {
            j.AddEntry(Mar(2), "收款", 500m, 0m);
            j.AddEntry(Mar(6), "付款", 0m, 200m);
        });

        var updated = await _service.UpdateAsync(_accountSet.Id, journal.Id, new UpdateJournalRequest
        {
            Name = "现金日记账",
            OpeningBalance = 2000m,
        });

        Assert.NotNull(updated);
        Assert.Equal(2000m, updated!.OpeningBalance);
        var ordered = updated.Entries.OrderBy(e => e.Sequence).ToList();
        Assert.Equal([2500m, 2300m], ordered.Select(e => e.Balance).ToList());
        Assert.Equal(2300m, updated.ClosingBalance);
    }

    [Fact]
    public async Task UpdateEntryAsync_ChangedAmount_RecalculatesFollowingBalances()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "收款", 100m, 0m);
            j.AddEntry(Mar(2), "收款", 50m, 0m);
        });
        var firstEntryId = journal.Entries[0].Id;

        var detail = await _service.UpdateEntryAsync(_accountSet.Id, journal.Id, firstEntryId, Entry(Mar(1), "收款改大", income: 300m));

        Assert.NotNull(detail);
        var ordered = detail!.Entries.OrderBy(e => e.Sequence).ToList();
        Assert.Equal(300m, ordered[0].Income);
        Assert.Equal(300m, ordered[0].Balance);
        Assert.Equal(350m, ordered[1].Balance);
        Assert.Equal(350m, detail.ClosingBalance);
    }

    [Fact]
    public async Task UpdateEntryAsync_EntryWithLinkedVoucher_PreservesVoucherLink()
    {
        // 出纳闭环：修改流水不动金额口径之外还要保留关联凭证（服务层显式回传 existing.VoucherId）。
        var voucher = SeedVoucher(1);
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收款", 100m, 0m, voucherId: voucher.Id));

        var detail = await _service.UpdateEntryAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id,
            Entry(Mar(1), "收款改摘要", income: 120m));

        Assert.NotNull(detail);
        var entry = Assert.Single(detail!.Entries);
        Assert.Equal(120m, entry.Income);
        Assert.Equal(voucher.Id, entry.VoucherId);
        Assert.Equal("记-1", entry.VoucherNo);
    }

    [Fact]
    public async Task UpdateEntryAsync_MissingEntry_ThrowsJournalException()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.UpdateEntryAsync(_accountSet.Id, journal.Id, Guid.NewGuid(), Entry(Mar(1), "收款", income: 1m)));

        Assert.Equal("日记账分录不存在。", ex.Message);
    }

    [Fact]
    public async Task AddEntryAsync_MissingJournal_ReturnsNull()
    {
        Assert.Null(await _service.AddEntryAsync(_accountSet.Id, Guid.NewGuid(), Entry(Mar(1), "收款", income: 1m)));
    }

    // —— 停用只拦新流水，历史数据照常 ——

    [Fact]
    public async Task AddEntryAsync_DisabledJournal_ThrowsWithAccountNameAndKeepsHistory()
    {
        // 出纳闭环：资金账户停用后只拦「新流水」；历史数据与余额保留可查。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "停用前收款", 800m, 0m));
        await _service.SetEnabledAsync(_accountSet.Id, journal.Id, enabled: false);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.AddEntryAsync(_accountSet.Id, journal.Id, Entry(Mar(9), "停用后收款", income: 100m)));

        Assert.Equal("账户「现金日记账」已停用，请先启用后再登记流水。", ex.Message);

        _db.DetachAll();
        var detail = await ReloadAsync(journal.Id);
        var entry = Assert.Single(detail.Entries);
        Assert.Equal(800m, entry.Income);
        Assert.Equal(800m, detail.ClosingBalance);
        Assert.False(detail.IsEnabled);
    }

    [Fact]
    public async Task ListAsync_DisabledJournal_StillListedWithTotals()
    {
        // 「历史数据与报表照常」：列表不因停用隐藏账户，合计与期末余额继续可取。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, openingBalance: 100m, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "收款", 50m, 0m);
            j.AddEntry(Mar(2), "付款", 0m, 30m);
        });
        await _service.SetEnabledAsync(_accountSet.Id, journal.Id, enabled: false);
        _db.DetachAll();

        var list = await _service.ListAsync(_accountSet.Id);

        var item = Assert.Single(list);
        Assert.False(item.IsEnabled);
        Assert.Equal(2, item.EntryCount);
        Assert.Equal(50m, item.TotalIncome);
        Assert.Equal(30m, item.TotalExpense);
        Assert.Equal(120m, item.ClosingBalance);
    }

    [Fact]
    public async Task UpdateEntryAsync_DisabledJournal_StillAllowsEditingHistory()
    {
        // 「停用只拦新流水」的边界：修改历史流水不属于新流水，应当放行。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "历史流水", 100m, 0m));
        await _service.SetEnabledAsync(_accountSet.Id, journal.Id, enabled: false);
        _db.DetachAll();

        var detail = await _service.UpdateEntryAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id,
            Entry(Mar(1), "历史流水修正", income: 150m));

        Assert.NotNull(detail);
        var entry = Assert.Single(detail!.Entries);
        Assert.Equal(150m, entry.Income);
        Assert.Equal(150m, entry.Balance);
    }

    // —— 已生成凭证的流水：不可重复出票、删除凭证后自愈 ——

    [Fact]
    public async Task GenerateVoucherAsync_IncomeEntry_DebitsMoneyAccountAndCreditsCounter()
    {
        // 出票方向口径：收入流水 → 借 货币资金 / 贷 对方科目。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(6), "收到货款", 680m, 0m));

        var voucherId = await _service.GenerateVoucherAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id, "出纳张三",
            new GenerateVoucherRequest { CounterAccountId = _incomeAccount.Id });

        var voucher = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == voucherId);
        Assert.Equal("记", voucher.Word);
        Assert.Equal(1, voucher.Number);
        Assert.Equal(Mar(6), voucher.Date);
        Assert.Equal("出纳张三", voucher.Preparer);
        Assert.Equal(2, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == _cashAccount.Id && e.Debit == 680m && e.Credit == 0m);
        Assert.Contains(voucher.Entries, e => e.AccountId == _incomeAccount.Id && e.Debit == 0m && e.Credit == 680m);
        Assert.True(voucher.IsBalanced);

        _db.DetachAll();
        var detail = await ReloadAsync(journal.Id);
        var entry = Assert.Single(detail.Entries);
        Assert.Equal(voucherId, entry.VoucherId);
        Assert.Equal("记-1", entry.VoucherNo);
    }

    [Fact]
    public async Task GenerateVoucherAsync_ExpenseEntry_DebitsCounterAndCreditsMoneyAccount()
    {
        // 出票方向口径：支出流水 → 借 对方科目 / 贷 货币资金。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(7), "支付办公费", 0m, 330m));

        var voucherId = await _service.GenerateVoucherAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id, "出纳张三",
            new GenerateVoucherRequest { CounterAccountId = _expenseAccount.Id, Word = "付" });

        var voucher = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == voucherId);
        Assert.Equal("付", voucher.Word);
        Assert.Contains(voucher.Entries, e => e.AccountId == _expenseAccount.Id && e.Debit == 330m);
        Assert.Contains(voucher.Entries, e => e.AccountId == _cashAccount.Id && e.Credit == 330m);
    }

    [Fact]
    public async Task GenerateVoucherAsync_EntryAlreadyHasVoucher_ThrowsNoDuplicate()
    {
        var voucher = SeedVoucher(1);
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收款", 100m, 0m, voucherId: voucher.Id));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id, "出纳张三",
                new GenerateVoucherRequest { CounterAccountId = _incomeAccount.Id }));

        Assert.Equal("该分录已生成凭证，请勿重复生成。", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucherAsync_ZeroAmountEntry_ThrowsCannotGenerate()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "空转一笔", 0m, 0m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id, "出纳张三",
                new GenerateVoucherRequest { CounterAccountId = _incomeAccount.Id }));

        Assert.Equal("该分录收支金额为零，无法生成凭证。", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucherAsync_CounterAccountMissing_Throws()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收款", 100m, 0m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id, "出纳张三",
                new GenerateVoucherRequest { CounterAccountId = Guid.NewGuid() }));

        Assert.Equal("指定的对方科目不存在。", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucherAsync_CounterAccountNotLeaf_Throws()
    {
        var parent = Account.CreateRoot(_accountSet.Id, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        parent.AddChild("660201", "办公费");
        _db.Context.Accounts.Add(parent);
        await _db.Context.SaveChangesAsync();

        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "付款", 0m, 100m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id, "出纳张三",
                new GenerateVoucherRequest { CounterAccountId = parent.Id }));

        Assert.Contains("不是末级科目", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucherAsync_CounterAccountDisabled_Throws()
    {
        _expenseAccount.Disable();
        await _db.Context.SaveChangesAsync();
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "付款", 0m, 100m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id, "出纳张三",
                new GenerateVoucherRequest { CounterAccountId = _expenseAccount.Id }));

        Assert.Contains("已停用", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucherAsync_AfterVoucherDeleted_HealsStaleLinkAndAllowsRegeneration()
    {
        // 自愈：凭证删除后流水残留 VoucherId 会被误判「已生成凭证」，取详情/出票前先清引用并允许重新出票。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收款", 100m, 0m));
        var firstVoucherId = await _service.GenerateVoucherAsync(_accountSet.Id, journal.Id, journal.Entries[0].Id, "出纳张三",
            new GenerateVoucherRequest { CounterAccountId = _incomeAccount.Id });
        var firstVoucher = await _db.Context.Vouchers.SingleAsync(v => v.Id == firstVoucherId);
        firstVoucher.SoftDelete(DateTimeOffset.Now);
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var detail = await ReloadAsync(journal.Id);
        var healed = Assert.Single(detail.Entries);
        Assert.Null(healed.VoucherId);
        Assert.Null(healed.VoucherNo);

        var secondVoucherId = await _service.GenerateVoucherAsync(_accountSet.Id, journal.Id, healed.Id, "出纳张三",
            new GenerateVoucherRequest { CounterAccountId = _incomeAccount.Id });

        Assert.NotEqual(firstVoucherId, secondVoucherId);
        _db.DetachAll();
        detail = await ReloadAsync(journal.Id);
        Assert.Equal(secondVoucherId, Assert.Single(detail.Entries).VoucherId);
    }

    // —— 批量操作计数 ——

    [Fact]
    public async Task BulkEntriesAsync_Delete_RemovesExistingOnlyAndDeduplicatesIds()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "一月收款", 50m, 0m);
            j.AddEntry(Mar(5), "五日收款", 100m, 0m);
            j.AddEntry(Mar(8), "八日收款", 25m, 0m);
        });

        var affected = await _service.BulkEntriesAsync(_accountSet.Id, journal.Id, new BulkJournalEntryRequest
        {
            Action = "delete",
            // 重复 id 只算一笔；不存在的 id 不计入受影响条数。
            EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id, journal.Entries[0].Id, Guid.NewGuid()],
        });

        Assert.Equal(2, affected);
        _db.DetachAll();
        var detail = await ReloadAsync(journal.Id);
        var entry = Assert.Single(detail.Entries);
        Assert.Equal("八日收款", entry.Summary);
        Assert.Equal(1, entry.Sequence);
        Assert.Equal(25m, entry.Balance);
        Assert.Equal(25m, detail.ClosingBalance);
    }

    [Fact]
    public async Task BulkEntriesAsync_EmptySelection_ThrowsSelectFirst()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.BulkEntriesAsync(_accountSet.Id, journal.Id, new BulkJournalEntryRequest { Action = "delete" }));

        Assert.Equal("请先勾选要操作的流水。", ex.Message);
    }

    [Fact]
    public async Task BulkEntriesAsync_UnknownAction_ThrowsWithActionName()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收款", 10m, 0m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.BulkEntriesAsync(_accountSet.Id, journal.Id, new BulkJournalEntryRequest
            {
                Action = "merge",
                EntryIds = [journal.Entries[0].Id],
            }));

        Assert.Equal("不支持的批量操作：merge。", ex.Message);
    }

    [Fact]
    public async Task BulkEntriesAsync_Summary_AppliesToAllSelectedAndCountsAffected()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "旧摘要一", 10m, 0m);
            j.AddEntry(Mar(2), "旧摘要二", 20m, 0m);
        });

        var affected = await _service.BulkEntriesAsync(_accountSet.Id, journal.Id, new BulkJournalEntryRequest
        {
            Action = "summary",
            Summary = "统一新摘要",
            EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id],
        });

        Assert.Equal(2, affected);
        _db.DetachAll();
        var detail = await ReloadAsync(journal.Id);
        Assert.All(detail.Entries, e => Assert.Equal("统一新摘要", e.Summary));
    }

    [Fact]
    public async Task BulkEntriesAsync_SummaryBlank_ThrowsEmptySummary()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收款", 10m, 0m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.BulkEntriesAsync(_accountSet.Id, journal.Id, new BulkJournalEntryRequest
            {
                Action = "summary",
                Summary = "  ",
                EntryIds = [journal.Entries[0].Id],
            }));

        Assert.Equal("摘要不能为空。", ex.Message);
    }

    [Fact]
    public async Task BulkEntriesAsync_CategoryNull_ClearsCategoryOnAllSelected()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "收款", 10m, 0m, category: "销售收入");
            j.AddEntry(Mar(2), "收款", 20m, 0m, category: "销售收入");
        });

        // 请求未带类别（null）→ 服务层按空串下传，语义为「清空」。
        var affected = await _service.BulkEntriesAsync(_accountSet.Id, journal.Id, new BulkJournalEntryRequest
        {
            Action = "category",
            EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id],
        });

        Assert.Equal(2, affected);
        _db.DetachAll();
        var detail = await ReloadAsync(journal.Id);
        Assert.All(detail.Entries, e => Assert.Null(e.Category));
    }

    [Fact]
    public async Task BulkEntriesAsync_UnlinkVoucher_ClearsVoucherReference()
    {
        var voucher = SeedVoucher(1);
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "收款", 10m, 0m, voucherId: voucher.Id);
            j.AddEntry(Mar(2), "收款", 20m, 0m, voucherId: voucher.Id);
        });

        var affected = await _service.BulkEntriesAsync(_accountSet.Id, journal.Id, new BulkJournalEntryRequest
        {
            Action = "unlink-voucher",
            EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id],
        });

        Assert.Equal(2, affected);
        _db.DetachAll();
        var detail = await ReloadAsync(journal.Id);
        Assert.All(detail.Entries, e =>
        {
            Assert.Null(e.VoucherId);
            Assert.Null(e.VoucherNo);
        });
    }

    [Fact]
    public async Task BulkEntriesAsync_LinkVoucherWithoutId_ThrowsSelectVoucher()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收款", 10m, 0m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.BulkEntriesAsync(_accountSet.Id, journal.Id, new BulkJournalEntryRequest
            {
                Action = "link-voucher",
                EntryIds = [journal.Entries[0].Id],
            }));

        Assert.Equal("请选择要关联的凭证。", ex.Message);
    }

    [Fact]
    public async Task BulkEntriesAsync_MissingJournal_ReturnsNull()
    {
        var affected = await _service.BulkEntriesAsync(_accountSet.Id, Guid.NewGuid(),
            new BulkJournalEntryRequest { Action = "delete", EntryIds = [Guid.NewGuid()] });

        Assert.Null(affected);
    }

    // —— 批量生成凭证：合并方式 / 对方科目回落 / 整单拒绝 ——

    [Fact]
    public async Task GenerateVouchersBatchAsync_MergeByAll_SumsSameAccountAndDirectionIntoOneVoucher()
    {
        // 合并方式 1（全部）：同科目同方向的行自动汇总为一行，摘要「首笔 等 N 笔」。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(2), "收到货款A", 100m, 0m);
            j.AddEntry(Mar(4), "收到货款B", 50m, 0m);
        });

        var result = await _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
            new GenerateVouchersBatchRequest
            {
                EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id],
                MergeBy = 1,
                CounterAccountId = _incomeAccount.Id,
            });

        Assert.Equal(2, result.EntryCount);
        var voucherRef = Assert.Single(result.Vouchers);
        Assert.Equal("记-1", voucherRef.Label);
        Assert.Equal(2, voucherRef.EntryCount);

        var voucher = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == voucherRef.Id);
        Assert.Equal(Mar(2), voucher.Date);
        Assert.Equal(2, voucher.Entries.Count);
        var moneyLine = voucher.Entries.Single(e => e.AccountId == _cashAccount.Id);
        Assert.Equal(150m, moneyLine.Debit);
        Assert.Contains("等 2 笔", moneyLine.Summary);
        var counterLine = voucher.Entries.Single(e => e.AccountId == _incomeAccount.Id);
        Assert.Equal(150m, counterLine.Credit);
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_MergeByDate_SplitsOneVoucherPerDay()
    {
        // 合并方式 2（按日期）：每个日期一张，凭证日期取该组最早一天。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(2), "二日收款一", 100m, 0m);
            j.AddEntry(Mar(2), "二日收款二", 40m, 0m);
            j.AddEntry(Mar(5), "五日收款", 60m, 0m);
        });

        var result = await _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
            new GenerateVouchersBatchRequest
            {
                EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id, journal.Entries[2].Id],
                MergeBy = 2,
                CounterAccountId = _incomeAccount.Id,
            });

        Assert.Equal(2, result.Vouchers.Count);
        Assert.Equal([2, 1], result.Vouchers.Select(v => v.EntryCount).ToList());
        Assert.Equal(["记-1", "记-2"], result.Vouchers.Select(v => v.Label).ToList());
        var first = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == result.Vouchers[0].Id);
        Assert.Equal(Mar(2), first.Date);
        Assert.Equal(140m, first.TotalDebit);
        var second = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == result.Vouchers[1].Id);
        Assert.Equal(Mar(5), second.Date);
        Assert.Equal(60m, second.TotalCredit);
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_MergeByDirection_SplitsIncomeAndExpense()
    {
        // 合并方式 4（按方向）：收入与支出各成一张，借贷方向各自正确。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(2), "收款", 100m, 0m);
            j.AddEntry(Mar(3), "付款", 0m, 30m);
        });

        var result = await _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
            new GenerateVouchersBatchRequest
            {
                EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id],
                MergeBy = 4,
                CounterAccountId = _expenseAccount.Id,
            });

        Assert.Equal(2, result.Vouchers.Count);
        var incomeVoucher = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == result.Vouchers[0].Id);
        Assert.Contains(incomeVoucher.Entries, e => e.AccountId == _cashAccount.Id && e.Debit == 100m);
        var expenseVoucher = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == result.Vouchers[1].Id);
        Assert.Contains(expenseVoucher.Entries, e => e.AccountId == _cashAccount.Id && e.Credit == 30m);
        Assert.Contains(expenseVoucher.Entries, e => e.AccountId == _expenseAccount.Id && e.Debit == 30m);

        _db.DetachAll();
        var detail = await ReloadAsync(journal.Id);
        Assert.Equal(2, detail.Entries.Count(e => e.VoucherId is not null));
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_MergeByCategory_GroupsByCategoryName()
    {
        // 合并方式 3（按收支类别）：类别不同分开出票，未填类别的归入「未分类」。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "办公费支出", 0m, 30m, category: "办公费");
            j.AddEntry(Mar(2), "无类别支出", 0m, 20m);
        });

        var result = await _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
            new GenerateVouchersBatchRequest
            {
                EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id],
                MergeBy = 3,
                CounterAccountId = _expenseAccount.Id,
            });

        Assert.Equal(2, result.Vouchers.Count);
        Assert.Equal([1, 1], result.Vouchers.Select(v => v.EntryCount).ToList());
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_NoUniformCounter_FallsBackToCategoryDefaultSubject()
    {
        // 出纳闭环：对方科目优先取统一科目、其次取类别「默认对方科目」（名称+方向一致才算命中）。
        SeedCategory(EntryCategoryType.Expense, "办公费", _expenseAccount.Id);
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "支付办公费", 0m, 80m, category: "办公费"));

        var result = await _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
            new GenerateVouchersBatchRequest
            {
                EntryIds = [journal.Entries[0].Id],
                MergeBy = 1,
            });

        var voucher = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == Assert.Single(result.Vouchers).Id);
        Assert.Contains(voucher.Entries, e => e.AccountId == _expenseAccount.Id && e.Debit == 80m);
        Assert.Contains(voucher.Entries, e => e.AccountId == _cashAccount.Id && e.Credit == 80m);
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_UniformCounterWinsOverCategoryDefault()
    {
        SeedCategory(EntryCategoryType.Income, "销售收入", _incomeAccount.Id);
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收到货款", 100m, 0m, category: "销售收入"));

        var result = await _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
            new GenerateVouchersBatchRequest
            {
                EntryIds = [journal.Entries[0].Id],
                MergeBy = 1,
                CounterAccountId = _expenseAccount.Id,
            });

        var voucher = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == Assert.Single(result.Vouchers).Id);
        Assert.Contains(voucher.Entries, e => e.AccountId == _expenseAccount.Id && e.Credit == 100m);
        Assert.DoesNotContain(voucher.Entries, e => e.AccountId == _incomeAccount.Id);
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_CategoryTypeMismatch_DoesNotResolveSubject()
    {
        // 「同类命中」口径：支出流水不能命中同名收入类别的默认对方科目，否则整单拒绝。
        SeedCategory(EntryCategoryType.Income, "办公费", _incomeAccount.Id);
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "支付办公费", 0m, 80m, category: "办公费"));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
                new GenerateVouchersBatchRequest
                {
                    EntryIds = [journal.Entries[0].Id],
                    MergeBy = 1,
                }));

        Assert.Contains("未能确定对方科目", ex.Message);
        Assert.Contains("第 1 笔（支付办公费）", ex.Message);
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_OneEntryWithoutCounter_RejectsWholeBatch()
    {
        // 出纳闭环：任一流水无法确定对方科目则整单拒绝（不做部分生成）。
        SeedCategory(EntryCategoryType.Expense, "办公费", _expenseAccount.Id);
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "支付办公费", 0m, 80m, category: "办公费");
            j.AddEntry(Mar(2), "支付差旅费", 0m, 50m, category: "差旅费");
        });

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
                new GenerateVouchersBatchRequest
                {
                    EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id],
                    MergeBy = 1,
                }));

        Assert.Contains("未能确定对方科目", ex.Message);
        Assert.Contains("第 2 笔（支付差旅费）", ex.Message);
        Assert.Empty(await _db.Context.Vouchers.Where(v => v.AccountSetId == _accountSet.Id).ToListAsync());
        _db.DetachAll();
        var detail = await ReloadAsync(journal.Id);
        Assert.All(detail.Entries, e => Assert.Null(e.VoucherId));
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_EntryAlreadyGenerated_RejectsWholeBatchWithSequence()
    {
        var voucher = SeedVoucher(9);
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "已出票", 100m, 0m, voucherId: voucher.Id);
            j.AddEntry(Mar(2), "未出票", 50m, 0m);
        });

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
                new GenerateVouchersBatchRequest
                {
                    EntryIds = [journal.Entries[0].Id, journal.Entries[1].Id],
                    MergeBy = 1,
                    CounterAccountId = _incomeAccount.Id,
                }));

        Assert.Equal("第 1 笔流水已生成凭证，请勿重复生成。", ex.Message);
        // 整单拒绝：除了预置的那张凭证，没有新生成任何凭证。
        var survivors = await _db.Context.Vouchers.Where(v => v.AccountSetId == _accountSet.Id).ToListAsync();
        var survivor = Assert.Single(survivors);
        Assert.Equal(voucher.Id, survivor.Id);
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_EntryWithBothIncomeAndExpense_RejectsWholeBatch()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收支混登", 100m, 30m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
                new GenerateVouchersBatchRequest
                {
                    EntryIds = [journal.Entries[0].Id],
                    MergeBy = 1,
                    CounterAccountId = _incomeAccount.Id,
                }));

        Assert.Equal("第 1 笔流水同时有收入与支出，无法生成凭证。", ex.Message);
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_InvalidMergeBy_Throws()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收款", 100m, 0m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
                new GenerateVouchersBatchRequest
                {
                    EntryIds = [journal.Entries[0].Id],
                    MergeBy = 5,
                    CounterAccountId = _incomeAccount.Id,
                }));

        Assert.Equal("合并方式不合法。", ex.Message);
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_EmptySelection_ThrowsSelectFirst()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
                new GenerateVouchersBatchRequest { MergeBy = 1, CounterAccountId = _incomeAccount.Id }));

        Assert.Equal("请先勾选要生成凭证的流水。", ex.Message);
    }

    [Fact]
    public async Task GenerateVouchersBatchAsync_UnknownEntryId_ThrowsStaleSelection()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
            j.AddEntry(Mar(1), "收款", 100m, 0m));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.GenerateVouchersBatchAsync(_accountSet.Id, journal.Id, "出纳张三",
                new GenerateVouchersBatchRequest
                {
                    EntryIds = [Guid.NewGuid()],
                    MergeBy = 1,
                    CounterAccountId = _incomeAccount.Id,
                }));

        Assert.Equal("存在无效的流水记录，请刷新后重试。", ex.Message);
    }

    // —— 建账与删除 ——

    [Fact]
    public async Task CreateAsync_CashJournalWithoutAccount_AutoCreatesDetailAccountAndSequentialCodes()
    {
        // 建账：省略科目时按编码方案（4-2-2-2）在 1001 下顺序建明细科目，账户编码按类型自增补零。
        // 用独立账套，避开主装置预置的 100101 明细科目（编码唯一索引）。
        var freshSet = _db.AddAccountSet("自动建科目账套");
        _db.AddAccount(freshSet, "1001", "库存现金");

        var first = await _service.CreateAsync(freshSet.Id, new CreateJournalRequest { Type = 1, Name = "基本现金" });
        var second = await _service.CreateAsync(freshSet.Id, new CreateJournalRequest { Type = 1, Name = "备用金" });

        Assert.Equal("100101", first.AccountCode);
        Assert.Equal("100102", second.AccountCode);
        Assert.Equal("001", first.Code);
        Assert.Equal("002", second.Code);
        Assert.Equal("人民币", first.Currency);
        Assert.Equal("现金日记账", first.TypeText);
    }

    [Fact]
    public async Task CreateAsync_WithoutAccountMissingParent1001_ThrowsOperativeHint()
    {
        // 上级科目缺失时不猜编码，给可操作中文提示。
        var freshSet = _db.AddAccountSet("无现金科目账套");

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(freshSet.Id, new CreateJournalRequest { Type = 1, Name = "基本现金" }));

        Assert.Contains("不存在科目「1001」", ex.Message);
        Assert.Contains("请先在科目管理中补建", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_NonCashAccount_ThrowsMoneyAccountOnly()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, new CreateJournalRequest
            {
                Type = 1,
                Name = "错误建账",
                AccountId = _expenseAccount.Id,
            }));

        Assert.Contains("货币资金科目", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_AccountAlreadyHasJournal_ThrowsNoDuplicate()
    {
        SeedJournal(JournalType.Cash, "现金日记账", _cashAccount);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, new CreateJournalRequest
            {
                Type = 1,
                Name = "重复建账",
                AccountId = _cashAccount.Id,
            }));

        Assert.Contains("已建立日记账", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_CodeTakenBySameType_ThrowsCodeOccupied()
    {
        // 账户编码在同一账套同一类型内唯一。
        var other = _db.AddAccount(_accountSet, "100102", "备用现金");
        await _service.CreateAsync(_accountSet.Id, new CreateJournalRequest
        {
            Type = 1,
            Name = "基本现金",
            AccountId = _cashAccount.Id,
            Code = "A1",
        });

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, new CreateJournalRequest
            {
                Type = 1,
                Name = "撞码账户",
                AccountId = other.Id,
                Code = "A1",
            }));

        Assert.Equal("账户编码「A1」已被占用，请换一个。", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_BankJournal_StoresBankInfo()
    {
        // 出纳闭环：资金账户支持开户行信息。
        var detail = await _service.CreateAsync(_accountSet.Id, new CreateJournalRequest
        {
            Type = 2,
            Name = "工行存款日记账",
            AccountId = _bankAccount.Id,
            BankName = " 中国工商银行 ",
            BankAccountNo = "6222020200111",
        });

        Assert.Equal("银行存款日记账", detail.TypeText);
        Assert.Equal("中国工商银行", detail.BankName);
        Assert.Equal("6222020200111", detail.BankAccountNo);
    }

    [Fact]
    public async Task UpdateAsync_CashJournalWithBankInfo_ThrowsCashCannotSetBank()
    {
        // 开户行信息仅银行存款账户可用。
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.UpdateAsync(_accountSet.Id, journal.Id, new UpdateJournalRequest
            {
                Name = "现金日记账",
                BankName = "工商银行",
            }));

        Assert.Equal("现金账户不能设置银行开户信息。", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_Existing_RemovesJournalAndAllEntries_MissingReturnsFalse()
    {
        var journal = SeedJournal(JournalType.Cash, "现金日记账", _cashAccount, seedEntries: j =>
        {
            j.AddEntry(Mar(1), "收款", 10m, 0m);
            j.AddEntry(Mar(2), "收款", 20m, 0m);
        });

        Assert.True(await _service.DeleteAsync(_accountSet.Id, journal.Id));
        _db.DetachAll();
        Assert.Null(await _journals.GetByIdAsync(_accountSet.Id, journal.Id));
        // 分录随账户级联删除（journal_entries.journal_id 外键级联）。
        Assert.Equal(0, await _db.Context.Set<JournalEntry>().CountAsync(e => e.JournalId == journal.Id));
        Assert.False(await _service.DeleteAsync(_accountSet.Id, journal.Id));
    }

    [Fact]
    public async Task GetAsync_MissingJournal_ReturnsNull()
    {
        Assert.Null(await _service.GetAsync(_accountSet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task ListAsync_UnknownAccountSet_Throws()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(() => _service.ListAsync(Guid.NewGuid()));

        Assert.Equal("指定的账套不存在。", ex.Message);
    }
}
