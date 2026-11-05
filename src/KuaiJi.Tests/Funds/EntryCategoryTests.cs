using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.AccountSets.Infrastructure;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Accounting.Infrastructure;
using KuaiJi.Modules.Assist.Infrastructure;
using KuaiJi.Modules.Funds.Application;
using KuaiJi.Modules.Funds.Domain;
using KuaiJi.Modules.Funds.Infrastructure;
using KuaiJi.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KuaiJi.Tests.Funds;

/// <summary>
/// 收支类别测试。依据 AGENTS.md「出纳闭环（Modules/Funds）」：类别归属账套、编码同账套唯一
/// （默认按序编号，纯数字取最大值 +1）、名称同账套同类型唯一；可挂上级类别（须同类型、不能是自己）；
/// 默认对方科目须为已启用末级科目（流水批量出票时按「统一科目 → 类别默认科目」回落）；
/// 智能匹配关键字按摘要包含自动归类、同类取最长命中、方向不符或都不命中则不归类；
/// 改名/删除类别不回写既有流水。
/// </summary>
public sealed class EntryCategoryTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _cashAccount;     // 100101 库存现金（供导入用日记账绑定）
    private readonly Account _leafSubject;     // 560201 管理费用-办公费（末级、启用）
    private readonly Account _parentSubject;   // 5602 管理费用（挂下级后非末级）
    private readonly Account _disabledSubject; // 660101 已停用末级
    private readonly EntryCategoryService _service;
    private readonly JournalRepository _journals;

    public EntryCategoryTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _cashAccount = _db.AddAccount(_accountSet, "100101", "库存现金");
        _leafSubject = _db.AddAccount(_accountSet, "560201", "管理费用-办公费");
        _parentSubject = Account.CreateRoot(_accountSet.Id, "5602", "管理费用",
            AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        _parentSubject.AddChild("560202", "差旅费");
        _disabledSubject = _db.AddAccount(_accountSet, "660101", "已停用科目");
        _disabledSubject.Disable();
        _db.Context.Accounts.Add(_parentSubject);
        _db.Context.SaveChanges();

        _journals = new JournalRepository(_db.Context);
        _service = new EntryCategoryService(
            new AccountSetRepository(_db.Context),
            new EntryCategoryRepository(_db.Context),
            new AccountRepository(_db.Context),
            new AssistItemRepository(_db.Context));
    }

    public void Dispose() => _db.Dispose();

    // —— 种子助手 ——

    private static CreateEntryCategoryRequest Req(
        EntryCategoryType type,
        string name,
        string? code = null,
        Guid? parentId = null,
        string? smartKeywords = null,
        Guid? voucherSubjectId = null) => new()
        {
            Type = (int)type,
            Name = name,
            Code = code,
            ParentId = parentId,
            SmartKeywords = smartKeywords,
            VoucherSubjectId = voucherSubjectId,
        };

    // —— 编码规则 ——

    [Fact]
    public async Task CreateAsync_WithoutCode_AutoNumbersFromMaxNumericPlusOne()
    {
        // 领域口径：默认按序编号 1、2、3…（纯数字编码取最大值 +1，含手工指定的 8）。
        var first = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入"));
        var second = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "办公费"));
        await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "租金收入", code: "8"));
        var fourth = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "利息收入"));

        Assert.Equal("1", first.Code);
        Assert.Equal("2", second.Code);
        Assert.Equal("9", fourth.Code);
    }

    [Fact]
    public async Task CreateAsync_OnlyNonNumericCodes_AutoNumberStartsAtOne()
    {
        // NextCode 只认纯数字编码：非数字编码不参与取号，首个自动编号仍是 1。
        await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入", code: "Z1"));

        var created = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "办公费"));

        Assert.Equal("1", created.Code);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ThrowsWithCodeText()
    {
        await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入", code: "7"));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "办公费", code: "7")));

        Assert.Equal("类别编码「7」已存在。", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_CodeTooLong_ThrowsLengthGuard()
    {
        var longCode = new string('9', EntryCategory.MaxCodeLength + 1);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入", code: longCode)));

        // 域层 ArgumentException 带 paramName，透出文案会拼上「 (Parameter 'code')」尾巴（生产文案问题，另见 JournalServiceTests 留红用例）。
        Assert.Contains("类别编码不能超过 32 个字符。", ex.Message);
    }

    // —— 名称唯一与长度 ——

    [Fact]
    public async Task CreateAsync_DuplicateNameSameType_ThrowsTypeTextMessage()
    {
        // 同账套同类型内名称唯一；重名报错文案带「收入/支出」前缀。
        await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入"));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入")));

        Assert.Equal("收入类别「销售收入」已存在。", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_SameNameDifferentType_Allowed()
    {
        await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "结算款"));

        var expense = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "结算款"));

        Assert.Equal("支出", expense.TypeText);
    }

    [Fact]
    public async Task CreateAsync_BlankName_ThrowsNameGuard()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "   ")));

        // 中文文案尾巴带英文 "(Parameter 'name')"（生产文案问题，锁红用例见 JournalServiceTests.AddEntryAsync_BlankSummary）。
        Assert.Contains("类别名称不能为空。", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_NameTooLong_ThrowsLengthGuard()
    {
        var longName = new string('名', EntryCategory.MaxNameLength + 1);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, longName)));

        Assert.Contains($"类别名称不能超过 {EntryCategory.MaxNameLength} 个字符。", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_InvalidType_Throws()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, new CreateEntryCategoryRequest { Type = 3, Name = "未知类别" }));

        Assert.Equal("类别类型无效。", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_UnknownAccountSet_Throws()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(Guid.NewGuid(), Req(EntryCategoryType.Income, "销售收入")));

        Assert.Equal("指定的账套不存在。", ex.Message);
    }

    // —— 父子层级 ——

    [Fact]
    public async Task CreateAsync_ValidParent_ListResolvesParentName()
    {
        var parent = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售类"));

        var child = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "主营业务收入", parentId: parent.Id));

        Assert.Equal(parent.Id, child.ParentId);
        var list = await _service.ListAsync(_accountSet.Id);
        Assert.Equal("销售类", list.Single(c => c.Id == child.Id).ParentName);
        Assert.Null(list.Single(c => c.Id == parent.Id).ParentName);
    }

    [Fact]
    public async Task CreateAsync_ParentTypeMismatch_Throws()
    {
        var parent = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售类"));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "办公费", parentId: parent.Id)));

        Assert.Equal("上级类别与当前类别类型不一致。", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_ParentNotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入", parentId: Guid.NewGuid())));

        Assert.Equal("上级类别不存在。", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_SetSelfAsParent_Throws()
    {
        var category = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入"));

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.UpdateAsync(_accountSet.Id, category.Id, new UpdateEntryCategoryRequest
            {
                Name = "销售收入",
                ParentId = category.Id,
            }));

        Assert.Equal("上级类别不能是自己。", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_ParentEmptyGuid_BackToTopLevel()
    {
        var parent = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售类"));
        var child = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "主营业务收入", parentId: parent.Id));

        var updated = await _service.UpdateAsync(_accountSet.Id, child.Id, new UpdateEntryCategoryRequest
        {
            Name = "主营业务收入",
            ParentId = Guid.Empty,
        });

        Assert.NotNull(updated);
        Assert.Null(updated!.ParentId);
    }

    // —— 默认对方科目 ——

    [Fact]
    public async Task CreateAsync_WithLeafSubject_ResolvesCodeAndName()
    {
        // 出纳闭环：类别可配「默认对方科目」，流水批量出票回落使用；须为账套内已启用末级科目。
        var created = await _service.CreateAsync(_accountSet.Id,
            Req(EntryCategoryType.Expense, "办公费", voucherSubjectId: _leafSubject.Id));

        Assert.Equal(_leafSubject.Id, created.VoucherSubjectId);
        Assert.Equal("560201", created.VoucherSubjectCode);
        Assert.Equal("管理费用-办公费", created.VoucherSubjectName);
    }

    [Fact]
    public async Task CreateAsync_SubjectNotLeaf_Throws()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id,
                Req(EntryCategoryType.Expense, "办公费", voucherSubjectId: _parentSubject.Id)));

        Assert.Equal("默认对方科目「5602 管理费用」不是末级科目。", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_SubjectDisabled_Throws()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id,
                Req(EntryCategoryType.Expense, "办公费", voucherSubjectId: _disabledSubject.Id)));

        Assert.Equal("默认对方科目「660101 已停用科目」已停用。", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_SubjectUnknown_Throws()
    {
        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id,
                Req(EntryCategoryType.Expense, "办公费", voucherSubjectId: Guid.NewGuid())));

        Assert.Equal("默认对方科目不存在。", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_SubjectEmptyGuid_ClearsDefault()
    {
        var created = await _service.CreateAsync(_accountSet.Id,
            Req(EntryCategoryType.Expense, "办公费", voucherSubjectId: _leafSubject.Id));

        var updated = await _service.UpdateAsync(_accountSet.Id, created.Id, new UpdateEntryCategoryRequest
        {
            Name = "办公费",
            VoucherSubjectId = Guid.Empty,
        });

        Assert.NotNull(updated);
        Assert.Null(updated!.VoucherSubjectId);
        Assert.Null(updated.VoucherSubjectCode);
    }

    // —— 智能匹配关键字 ——

    [Fact]
    public void KeywordList_SplitsOnAllSeparatorsAndDeduplicates()
    {
        // 解析口径：逗号（中英文）、顿号、空格均可作分隔，去空去重。
        var category = EntryCategory.Create(_accountSet.Id, EntryCategoryType.Income, "销售收入", "1");
        category.SetSmartKeywords("货款, 汇款、汇款 工资，工资  ");

        Assert.Equal(["货款", "汇款", "工资"], category.KeywordList());
        category.SetSmartKeywords("   ");
        Assert.Empty(category.KeywordList());
    }

    [Fact]
    public async Task CreateAsync_KeywordsTooLong_ThrowsLengthGuard()
    {
        var longKeywords = new string('字', EntryCategory.MaxKeywordsLength + 1);

        var ex = await Assert.ThrowsAsync<JournalException>(
            () => _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入", smartKeywords: longKeywords)));

        Assert.Contains($"智能匹配关键字不能超过 {EntryCategory.MaxKeywordsLength} 个字符。", ex.Message);
    }

    [Fact]
    public async Task Import_SummaryMatchesIncomeKeywords_TakesLongestHit()
    {
        // 出纳闭环：导入按摘要包含自动归类，同类型中命中关键字最长者优先；
        // 收入行不得命中支出类别（即使其关键字更长且包含）。
        var rows = new[]
        {
            new ImportedJournalRow { RowNumber = 1, Date = new DateOnly(2026, 3, 1), Summary = "收到客户货款", Income = 500m },
        };
        var (journal, result) = await RunImportAsync(rows, setup: async svc =>
        {
            await svc.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "零星收款", smartKeywords: "款"));
            await svc.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "大宗销售", smartKeywords: "客户货款,定金"));
            await svc.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "误入类", smartKeywords: "收到客户货款"));
        });

        Assert.Equal(1, result.ImportedEntries);
        Assert.Contains(result.Warnings, w => w.Contains("摘要命中「大宗销售」的智能匹配关键字，已自动归类。"));
        var reloaded = await _journals.GetByIdAsync(_accountSet.Id, journal.Id);
        var entry = Assert.Single(reloaded!.Entries);
        Assert.Equal("大宗销售", entry.Category);
    }

    [Fact]
    public async Task Import_ExpenseRow_MatchesOnlyExpenseCategory()
    {
        var rows = new[]
        {
            new ImportedJournalRow { RowNumber = 1, Date = new DateOnly(2026, 3, 1), Summary = "支付办公费用", Expense = 120m },
        };
        var (journal, result) = await RunImportAsync(rows, setup: async svc =>
        {
            await svc.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "收款类", smartKeywords: "办公"));
            await svc.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "办公费", smartKeywords: "办公"));
        });

        Assert.Equal(1, result.ImportedEntries);
        var reloaded = await _journals.GetByIdAsync(_accountSet.Id, journal.Id);
        Assert.Equal("办公费", Assert.Single(reloaded!.Entries).Category);
    }

    [Fact]
    public async Task Import_NoKeywordHit_LeavesCategoryEmpty()
    {
        // 不命中回落：类别留空、流水照常导入，且不误报命中告警。
        var rows = new[]
        {
            new ImportedJournalRow { RowNumber = 1, Date = new DateOnly(2026, 3, 1), Summary = "存入利息", Income = 9m },
        };
        var (journal, result) = await RunImportAsync(rows, setup: async svc =>
        {
            await svc.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入", smartKeywords: "货款,定金"));
        });

        Assert.Equal(1, result.ImportedEntries);
        Assert.Empty(result.Warnings);
        var reloaded = await _journals.GetByIdAsync(_accountSet.Id, journal.Id);
        Assert.Null(Assert.Single(reloaded!.Entries).Category);
    }

    [Fact]
    public async Task Import_RowWithExplicitCategory_KeepsItUnchanged()
    {
        // 文件里已填类别的行不做智能匹配，以人工填写为准。
        var rows = new[]
        {
            new ImportedJournalRow { RowNumber = 1, Date = new DateOnly(2026, 3, 1), Summary = "收到客户货款", Income = 100m, Category = "手工类别" },
        };
        var (journal, result) = await RunImportAsync(rows, setup: async svc =>
        {
            await svc.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "大宗销售", smartKeywords: "客户货款"));
        });

        Assert.Equal(1, result.ImportedEntries);
        Assert.Empty(result.Warnings);
        var reloaded = await _journals.GetByIdAsync(_accountSet.Id, journal.Id);
        Assert.Equal("手工类别", Assert.Single(reloaded!.Entries).Category);
    }

    [Fact]
    public async Task Import_DisabledJournal_ThrowsAndImportsNothing()
    {
        // 出纳闭环：停用只拦新流水——「导入」也在拦截范围内。
        var journal = SeedCashJournal(disabled: true);
        var svc = BuildImportService([
            new ImportedJournalRow { RowNumber = 1, Date = new DateOnly(2026, 3, 1), Summary = "收款", Income = 10m },
        ]);

        var ex = await Assert.ThrowsAsync<JournalImportException>(
            () => svc.ImportAsync(_accountSet.Id, journal.Id, new MemoryStream()));

        Assert.Equal("账户「现金日记账」已停用，请先启用后再导入流水。", ex.Message);
        _db.DetachAll();
        var reloaded = await _journals.GetByIdAsync(_accountSet.Id, journal.Id);
        Assert.Empty(reloaded!.Entries);
    }

    [Fact]
    public async Task Import_InvalidRows_SkippedWithLineWarnings()
    {
        // 非法行跳过并告警，不阻断整体导入（跳行计数与行号定位）。
        var rows = new[]
        {
            new ImportedJournalRow { RowNumber = 1, Date = default, Summary = "缺日期", Income = 10m },
            new ImportedJournalRow { RowNumber = 2, Date = new DateOnly(2026, 3, 1), Summary = "  ", Income = 10m },
            new ImportedJournalRow { RowNumber = 3, Date = new DateOnly(2026, 3, 1), Summary = "零金额" },
            new ImportedJournalRow { RowNumber = 4, Date = new DateOnly(2026, 3, 1), Summary = "正常收款", Income = 30m },
        };
        var svc = BuildImportService(rows);

        var result = await svc.ImportAsync(_accountSet.Id, SeedCashJournal(disabled: false).Id, new MemoryStream());

        Assert.Equal(4, result.TotalRows);
        Assert.Equal(1, result.ImportedEntries);
        Assert.Equal(3, result.SkippedRows);
        Assert.Contains(result.Warnings, w => w.Contains("日期缺失或格式不正确"));
        Assert.Contains(result.Warnings, w => w.Contains("摘要为空"));
        Assert.Contains(result.Warnings, w => w.Contains("收入与支出均为零"));
    }

    // —— 历史数据与类别字典解耦 ——

    [Fact]
    public async Task UpdateAsync_RenameCategory_KeepsExistingJournalEntryText()
    {
        // 分录按类别名称关联：改名不回写既有流水（AGENTS「删除类别不影响既有流水」同理于改名）。
        var created = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "办公费"));
        var journal = SeedCashJournal(withCategoryText: created.Name);

        var updated = await _service.UpdateAsync(_accountSet.Id, created.Id, new UpdateEntryCategoryRequest
        {
            Name = "办公费用",
        });

        Assert.NotNull(updated);
        Assert.Equal("办公费用", updated!.Name);
        _db.DetachAll();
        var reloaded = await _journals.GetByIdAsync(_accountSet.Id, journal.Id);
        Assert.Equal("办公费", Assert.Single(reloaded!.Entries).Category);
    }

    [Fact]
    public async Task DeleteAsync_ExistingCategory_RemovesItAndKeepsFlowText()
    {
        var created = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "办公费"));
        var journal = SeedCashJournal(withCategoryText: created.Name);

        Assert.True(await _service.DeleteAsync(_accountSet.Id, created.Id));
        Assert.Empty(await _service.ListAsync(_accountSet.Id));
        _db.DetachAll();
        var reloaded = await _journals.GetByIdAsync(_accountSet.Id, journal.Id);
        Assert.Equal("办公费", Assert.Single(reloaded!.Entries).Category);

        Assert.False(await _service.DeleteAsync(_accountSet.Id, created.Id));
    }

    [Fact]
    public async Task SetEnabledAsync_Disable_ReflectsInDto_AndMissingReturnsNull()
    {
        // 停用后不再出现在记账下拉，但档案保留（IsEnabled=false 可见可查）。
        var created = await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "销售收入"));

        var disabled = await _service.SetEnabledAsync(_accountSet.Id, created.Id, enabled: false);
        Assert.NotNull(disabled);
        Assert.False(disabled!.IsEnabled);

        Assert.Null(await _service.SetEnabledAsync(_accountSet.Id, Guid.NewGuid(), enabled: true));
        Assert.Null(await _service.UpdateAsync(_accountSet.Id, Guid.NewGuid(), new UpdateEntryCategoryRequest { Name = "任何" }));
    }

    [Fact]
    public async Task ListAsync_OrdersByTypeThenNumericCode()
    {
        // 编码多为序号：同类型内「2」排在「10」前（按数值而非字符串），收入(1)先于支出(2)。
        await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "类别十", code: "10"));
        await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Income, "类别二", code: "2"));
        await _service.CreateAsync(_accountSet.Id, Req(EntryCategoryType.Expense, "类别支", code: "3"));

        var list = await _service.ListAsync(_accountSet.Id);

        Assert.Equal(["2", "10", "3"], list.Select(c => c.Code).ToList());
        Assert.Equal([1, 1, 2], list.Select(c => c.Type).ToList());
    }

    // —— 导入装置助手 ——

    private Journal SeedCashJournal(bool disabled = false, string? withCategoryText = null)
    {
        var journal = Journal.Create(_accountSet.Id, JournalType.Cash, "现金日记账", _cashAccount.Id);
        if (withCategoryText is not null)
        {
            journal.AddEntry(new DateOnly(2026, 3, 1), "历史流水", 100m, 0m, category: withCategoryText);
        }

        if (disabled)
        {
            journal.SetEnabled(false);
        }

        _db.Context.Journals.Add(journal);
        _db.Context.SaveChanges();
        return journal;
    }

    private JournalImportService BuildImportService(IReadOnlyList<ImportedJournalRow> rows) =>
        new(_journals, new FixedRowReader(rows), new EntryCategoryRepository(_db.Context));

    /// <summary>用本类的 EntryCategoryService 建好类别后，把 rows 导入一本新建现金日记账。</summary>
    private async Task<(Journal Journal, JournalImportResult Result)> RunImportAsync(
        ImportedJournalRow[] rows,
        Func<EntryCategoryService, Task> setup)
    {
        await setup(_service);
        var journal = SeedCashJournal();
        var importService = BuildImportService(rows);
        var result = await importService.ImportAsync(_accountSet.Id, journal.Id, new MemoryStream());
        return (journal, result);
    }

    /// <summary>固定行假读取器：绕开 Excel 解析，专测导入服务的智能归类与行校验。</summary>
    private sealed class FixedRowReader : IJournalImportReader
    {
        private readonly IReadOnlyList<ImportedJournalRow> _rows;

        public FixedRowReader(IReadOnlyList<ImportedJournalRow> rows) => _rows = rows;

        public IReadOnlyList<ImportedJournalRow> Read(Stream stream) => _rows;
    }
}
