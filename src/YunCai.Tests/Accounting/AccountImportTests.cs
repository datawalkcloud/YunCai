using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Accounting;

/// <summary>
/// 科目导入应用服务测试：编码层级自动拆分与上级自动创建、显式上级编码、
/// 编码重复与失败行告警、类别/方向解析与继承、停用与助记码落库。
/// 读取器通过内存 <see cref="FakeAccountImportReader"/> 注入，不依赖真实 xlsx 文件。
///
/// 被测服务遵循的领域规则（编码方案取账套默认 4-2-2-2，累计位数 4/6/8/10，最多 4 级）：
/// 1. 行未填上级编码时，按累计位数定级，并用「编码前缀」逐级补齐缺失上级；
///    自动补建的上级不计入 ImportedAccounts（该计数=文件里成功导入的数据行数），只发告警。
/// 2. 行填了上级编码时，先确保上级存在，再依次校验级次上限、编码位数（4-2-2-2 下二级须为 6 位），
///    最后由领域模型 Account.AddChild 校验「明细编码必须以上级编码为前缀」——位数校验先于前缀校验。
/// 3. 明细科目的类别一律继承上级；方向留空继承上级、可显式覆盖；顶级科目方向按类别与名称兜底。
/// 4. 助记码留空时按名称拼音首字母生成，明细科目加「上级助记码_」前缀；已有助记码不被覆盖，
///    存量科目缺助记码时重导同一文件即可补全。
/// </summary>
public sealed class AccountImportTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly AccountRepository _accounts;
    private readonly AccountImportService _service;

    public AccountImportTests()
    {
        _accountSet = _db.AddAccountSet("导入账套", "4-2-2-2");
        _accounts = new AccountRepository(_db.Context);
        _service = new AccountImportService(
            new FakeAccountImportReader(Array.Empty<ImportedAccountRow>()),
            new AccountSetRepository(_db.Context),
            _accounts,
            new AccountAutoCreateService(_accounts));
    }

    public void Dispose() => _db.Dispose();

    /// <summary>构造一行导入数据（与模板列对应的原始文本）。</summary>
    private static ImportedAccountRow Row(
        int rowNumber, string code, string name,
        string category = "", string direction = "",
        string parentCode = "", string mnemonic = "", bool isEnabled = true) => new()
    {
        RowNumber = rowNumber,
        Code = code,
        Name = name,
        Category = category,
        Direction = direction,
        ParentCode = parentCode,
        Mnemonic = mnemonic,
        IsEnabled = isEnabled,
    };

    private AccountImportService WithRows(params ImportedAccountRow[] rows) => new(
        new FakeAccountImportReader(rows),
        new AccountSetRepository(_db.Context),
        _accounts,
        new AccountAutoCreateService(_accounts));

    private async Task<Account?> FindByCodeAsync(string code) =>
        await _accounts.GetByCodeAsync(_accountSet.Id, code);

    [Fact]
    public async Task Import_AccountSetMissing_ThrowsAccountImportException()
    {
        var ex = await Assert.ThrowsAsync<AccountImportException>(
            () => WithRows(Row(1, "1001", "库存现金", "资产")).ImportAsync(Guid.NewGuid(), Stream.Null));

        Assert.Equal("指定的账套不存在，无法导入科目。", ex.Message);
    }

    [Fact]
    public async Task Import_NoDataRows_ThrowsAccountImportException()
    {
        var ex = await Assert.ThrowsAsync<AccountImportException>(
            () => WithRows().ImportAsync(_accountSet.Id, Stream.Null));

        Assert.Equal("文件中未找到任何科目数据行。", ex.Message);
    }

    [Fact]
    public async Task Import_TopLevelRow_ParsesCategoryAndDirection()
    {
        var result = await WithRows(
            Row(1, "1001", "库存现金", "资产", "借", mnemonic: "KJXJ"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ImportedAccounts);
        Assert.Equal(0, result.SkippedAccounts);
        Assert.Empty(result.Warnings);

        var account = await FindByCodeAsync("1001");
        Assert.NotNull(account);
        Assert.Equal("库存现金", account!.Name);
        Assert.Equal(AccountCategory.Asset, account.Category);
        Assert.Equal(BalanceDirection.Debit, account.Direction);
        Assert.Equal("KJXJ", account.Mnemonic);
        Assert.True(account.IsLeaf);
        Assert.False(account.IsPreset);
    }

    [Fact]
    public async Task Import_MissingMnemonic_GeneratesPinyinInitials()
    {
        // 导出的科目表没有「助记码」列，导入时按科目名称的拼音首字母补上。
        await WithRows(Row(1, "1001", "库存现金"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        var account = await FindByCodeAsync("1001");
        Assert.NotNull(account);
        Assert.Equal("kcxj", account!.Mnemonic);
    }

    [Fact]
    public async Task Import_ChildMnemonic_PrefixedWithParentMnemonic()
    {
        var parent = _db.AddAccount(_accountSet, "1002", "银行存款");
        parent.SetMnemonic("yxck");
        _db.Context.SaveChanges();

        await WithRows(Row(1, "100201", "工商银行", parentCode: "1002"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        var child = await FindByCodeAsync("100201");
        Assert.NotNull(child);
        Assert.Equal("yxck_gsyh", child!.Mnemonic);
    }

    [Fact]
    public async Task Import_AutoCreatedParent_SharesPrefixWithChild()
    {
        // 6602 由导入自动补建，名称从「营业外收入-政府补助」中提取；
        // 子科目名称重复了上级名称时，生成前缀前先剥掉，得到 yywsr_zfbz 而不是 yywsr_yywsrzfbz。
        await WithRows(Row(1, "660201", "营业外收入-政府补助"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        var parent = await FindByCodeAsync("6602");
        var child = await FindByCodeAsync("660201");
        Assert.Equal("yywsr", parent!.Mnemonic);
        Assert.Equal("yywsr_zfbz", child!.Mnemonic);
    }

    [Fact]
    public async Task Import_ExistingAccountWithBlankMnemonic_IsBackfilled()
    {
        // 历史导入的科目没有助记码：重导同一份文件即可补全，但仍计入「跳过」。
        _db.AddAccount(_accountSet, "1001", "库存现金");

        var result = await WithRows(Row(1, "1001", "库存现金"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(0, result.ImportedAccounts);
        Assert.Equal(1, result.SkippedAccounts);
        var account = await FindByCodeAsync("1001");
        Assert.Equal("kcxj", account!.Mnemonic);
    }

    [Fact]
    public async Task Import_ExistingAccountWithMnemonic_IsNotOverwritten()
    {
        var existing = _db.AddAccount(_accountSet, "1001", "库存现金");
        existing.SetMnemonic("KEEP");
        _db.Context.SaveChanges();

        await WithRows(Row(1, "1001", "库存现金", mnemonic: "OTHER"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        var account = await FindByCodeAsync("1001");
        Assert.Equal("KEEP", account!.Mnemonic);
    }

    [Fact]
    public async Task Import_TopLevelRow_InfersCategoryAndDefaultDirectionByCode()
    {
        // 未填类别时按编码首位推断：1=资产；损益类「管理费用」默认借方。
        await WithRows(Row(1, "6602", "管理费用")).ImportAsync(_accountSet.Id, Stream.Null);

        var account = await FindByCodeAsync("6602");
        Assert.NotNull(account);
        Assert.Equal(AccountCategory.ProfitAndLoss, account!.Category);
        Assert.Equal(BalanceDirection.Debit, account.Direction);
    }

    [Fact]
    public async Task Import_IncomeAccount_DefaultsToCreditDirection()
    {
        await WithRows(Row(1, "6001", "主营业务收入")).ImportAsync(_accountSet.Id, Stream.Null);

        var account = await FindByCodeAsync("6001");
        Assert.NotNull(account);
        Assert.Equal(AccountCategory.ProfitAndLoss, account!.Category);
        Assert.Equal(BalanceDirection.Credit, account.Direction);
    }

    [Fact]
    public async Task Import_UnrecognizableCategory_SkipsRowWithWarning()
    {
        // 类别解析是两级兜底：先按模板文本（资产/负债/权益/成本/损益）解析，解析不出再按编码首位推断。
        // 9999 是合法的一级长度（4 位）但首位 9 不在 1~6 内，两条路都走不通 → 跳过且不建科目；
        // 此时行是按「顶级科目」分支处理的，告警文案为「无法推断类别」。
        var result = await WithRows(
            Row(1, "9999", "神秘科目", "外星类"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedAccounts);
        Assert.Equal(0, result.SkippedAccounts); // 只有「编码已存在」才计入跳过数
        Assert.Contains(result.Warnings,
            w => w.Contains("顶级科目「9999 神秘科目」无法推断类别，已跳过。"));
        Assert.Null(await FindByCodeAsync("9999"));
    }

    [Fact]
    public async Task Import_HierarchyCode_SplitsLevelsAndAutoCreatesParents()
    {
        var result = await WithRows(
            Row(1, "100201", "工商银行", "资产", "借"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        // 4-2-2-2 方案下 100201 是二级科目（6 位），其一级前缀是编码前 4 位「1002」，
        // 因此缺失的上级是 1002 银行存款，而不是 1001 库存现金。
        // ImportedAccounts 只统计文件里的数据行：1 行导入 1 个，自动补建的上级靠告警与落库体现。
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ImportedAccounts);
        Assert.Equal(0, result.SkippedAccounts);
        Assert.Contains(result.Warnings,
            w => w.Contains("上级科目「1002」不存在，已自动创建为顶级科目。"));

        var parent = await FindByCodeAsync("1002");
        var child = await FindByCodeAsync("100201");
        Assert.NotNull(parent);
        Assert.NotNull(child);
        // 上级名称无法从「工商银行」提取（没有分隔符），兜底为「科目+编码」。
        Assert.Equal("科目1002", parent!.Name);
        Assert.Equal(1, parent.Level);
        Assert.False(parent.IsLeaf);
        Assert.Equal(2, child!.Level);
        Assert.True(child.IsLeaf);
        Assert.Equal(parent.Id, child.ParentId);
        Assert.Equal(AccountCategory.Asset, child.Category); // 明细科目类别继承上级
        Assert.Equal(BalanceDirection.Debit, child.Direction);
    }

    [Fact]
    public async Task Import_ExplicitParent_CreatesChildUnderExistingParent()
    {
        // 银行存款的标准编码是 1002（1001 是库存现金），明细 100201 才与上级构成前缀关系。
        _db.AddAccount(_accountSet, "1002", "银行存款");

        var result = await WithRows(
            Row(1, "100201", "工商银行", parentCode: "1002", direction: "贷"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(1, result.ImportedAccounts);
        Assert.Equal(0, result.SkippedAccounts);
        Assert.Empty(result.Warnings);

        // 清跟踪后从库里读，验证上级非末级标记与父子关系确实落库。
        _db.DetachAll();

        var parent = await FindByCodeAsync("1002");
        var child = await FindByCodeAsync("100201");
        Assert.NotNull(child);
        Assert.Equal(parent!.Id, child!.ParentId);
        Assert.Equal(2, child.Level);
        Assert.False(parent.IsLeaf);
        Assert.True(child.IsLeaf);
        // 子级可显式覆盖方向（父借子贷），类别则恒继承上级。
        Assert.Equal(BalanceDirection.Credit, child.Direction);
        Assert.Equal(BalanceDirection.Debit, parent.Direction);
        Assert.Equal(AccountCategory.Asset, child.Category);
    }

    [Fact]
    public async Task Import_ExplicitParent_ChildInheritsCategoryAndDirectionWhenBlank()
    {
        _db.AddAccount(_accountSet, "1002", "银行存款");

        var result = await WithRows(
            Row(1, "100201", "工商银行", parentCode: "1002"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(1, result.ImportedAccounts);
        Assert.Empty(result.Warnings);

        _db.DetachAll();

        // 明细科目不重复登记类别/方向：类别一律继承上级，方向留空时同样继承上级（借）。
        var parent = await FindByCodeAsync("1002");
        var child = await FindByCodeAsync("100201");
        Assert.NotNull(child);
        Assert.Equal(parent!.Id, child!.ParentId);
        Assert.Equal(AccountCategory.Asset, child!.Category);
        Assert.Equal(BalanceDirection.Debit, child.Direction);
    }

    [Fact]
    public async Task Import_ExplicitParent_CodeNotPrefixedByParent_SkipsWithWarning()
    {
        _db.AddAccount(_accountSet, "1002", "银行存款");

        // 位数（6 位二级）与级次都合法，唯独前缀不符：220201 属应付账款（2202），不能挂到 1002 银行存款下。
        // 前缀规则由领域模型 Account.AddChild 强制，应用服务把它转成「创建失败」告警并整行放弃。
        var result = await WithRows(
            Row(1, "220201", "应付账款明细", parentCode: "1002"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(0, result.ImportedAccounts);
        Assert.Equal(0, result.SkippedAccounts);
        Assert.Contains(result.Warnings, w => w.Contains("第 1 行")
            && w.Contains("创建失败")
            && w.Contains("明细科目编码「220201」必须以上级科目编码「1002」为前缀"));
        Assert.Null(await FindByCodeAsync("220201"));
    }

    [Fact]
    public async Task Import_ExplicitParent_WrongCodeLength_SkipsWithWarning()
    {
        _db.AddAccount(_accountSet, "1002", "银行存款");

        // 二级编码应为 6 位（4-2 方案），给了 8 位；位数校验先于前缀校验触发。
        var result = await WithRows(
            Row(1, "1002010100", "工商银行北京分行", parentCode: "1002"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(0, result.ImportedAccounts);
        Assert.Contains(result.Warnings, w => w.Contains("编码长度应为 6 位"));
    }

    [Fact]
    public async Task Import_BeyondMaxLevel_SkipsWithWarning()
    {
        // 4-2-2-2 方案最多 4 级（10 位）；给 5 级（12 位）编码并显式指定 4 级上级。
        var result = await WithRows(
            Row(1, "100101010102", "五级明细", parentCode: "1001010101"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(0, result.ImportedAccounts);
        Assert.Contains(result.Warnings, w => w.Contains("超出编码方案最大级次 4"));
        Assert.Null(await FindByCodeAsync("100101010102"));
    }

    [Fact]
    public async Task Import_ExistingCode_IsSkippedWithWarning()
    {
        _db.AddAccount(_accountSet, "1001", "库存现金");

        var result = await WithRows(
            Row(1, "1001", "库存现金"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(1, result.SkippedAccounts);
        Assert.Equal(0, result.ImportedAccounts);
        Assert.Contains(result.Warnings, w => w.Contains("编码已存在"));
    }

    [Fact]
    public async Task Import_DuplicateCodeWithinFile_ImportsFirstSkipsSecond()
    {
        var result = await WithRows(
            Row(1, "1001", "库存现金", "资产"),
            Row(2, "1001", "库存现金重复行", "资产"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(1, result.ImportedAccounts);
        Assert.Equal(1, result.SkippedAccounts);
        Assert.Contains(result.Warnings, w => w.Contains("第 2 行") && w.Contains("编码已存在"));
    }

    [Fact]
    public async Task Import_RowMissingCodeOrName_SkipsWithWarning()
    {
        var result = await WithRows(
            Row(1, "1001", ""))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(0, result.ImportedAccounts);
        Assert.Contains(result.Warnings, w => w.Contains("第 1 行缺少科目编码或名称"));
    }

    [Fact]
    public async Task Import_DisabledRow_CreatesDisabledAccount()
    {
        await WithRows(
            Row(1, "1001", "库存现金", "资产", isEnabled: false))
            .ImportAsync(_accountSet.Id, Stream.Null);

        var account = await FindByCodeAsync("1001");
        Assert.NotNull(account);
        Assert.False(account!.IsEnabled);
    }

    [Fact]
    public async Task Import_ParentsPersistedAcrossBatch_AreFoundOnFreshQuery()
    {
        // 自动创建的上级只 AddAsync 未即时保存，Import 结尾统一 SaveChanges 后必须可查；
        // 同时上级会写回本次导入的已知科目字典，同批次后续行直接复用，不再重复补建。
        var result = await WithRows(
            Row(1, "100201", "工商银行", "资产"),
            Row(2, "100202", "建设银行", "资产"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedAccounts);
        Assert.Single(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("上级科目「1002」不存在，已自动创建为顶级科目。"));

        _db.Context.ChangeTracker.Clear();

        var parent = await FindByCodeAsync("1002");
        var first = await FindByCodeAsync("100201");
        var second = await FindByCodeAsync("100202");
        Assert.NotNull(parent);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, parent!.Level);
        Assert.False(parent.IsLeaf);
        Assert.Equal(parent.Id, first!.ParentId);
        Assert.Equal(parent.Id, second!.ParentId);
        Assert.Equal(2, second.Level);
    }

    [Fact]
    public async Task Import_CategoryUnparseable_CodeLengthNotInScheme_SkipsWithWarning()
    {
        // 编码长度 5 位既不是一级（4 位）也不是二级（6 位），末尾又不是可容错的 0，
        // 又没有可作上级的编码前缀 → 只能当顶级科目处理，而类别文本与首位都解析不出 → 「类别无法识别」。
        var result = await WithRows(
            Row(1, "99999", "神秘科目", "外星类"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(0, result.ImportedAccounts);
        Assert.Equal(0, result.SkippedAccounts);
        Assert.Contains(result.Warnings,
            w => w.Contains("科目「99999 神秘科目」的类别无法识别，已跳过。"));
        Assert.Null(await FindByCodeAsync("99999"));
    }

    [Fact]
    public async Task Import_CategoryUnparseable_StillFallsBackToCodePrefix()
    {
        // 现状：类别列填了无法识别的文本时，与留空走同一条兜底路径——按编码首位推断（2=负债）。
        // 即「填错类别」不会提示用户，只会静默按编码归类；见回报中待裁决项。
        var result = await WithRows(
            Row(1, "2201", "应付票据", "外星类"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(1, result.ImportedAccounts);
        Assert.Empty(result.Warnings);

        var account = await FindByCodeAsync("2201");
        Assert.NotNull(account);
        Assert.Equal(AccountCategory.Liability, account!.Category);
        Assert.Equal(BalanceDirection.Credit, account.Direction);
    }

    [Fact]
    public async Task Import_AutoCreatedParent_NameExtractedFromChildNameSeparator()
    {
        // 上级名称可以从子科目名里按分隔符提取：660201「营业外收入-政府补助」→ 上级 6602「营业外收入」，
        // 省掉一份「科目6602」这种无意义占位名。
        var result = await WithRows(
            Row(1, "660201", "营业外收入-政府补助"))
            .ImportAsync(_accountSet.Id, Stream.Null);

        Assert.Equal(1, result.ImportedAccounts);
        Assert.Contains(result.Warnings,
            w => w.Contains("上级科目「6602」不存在，已自动创建为顶级科目。"));

        _db.Context.ChangeTracker.Clear();

        var parent = await FindByCodeAsync("6602");
        var child = await FindByCodeAsync("660201");
        Assert.NotNull(parent);
        Assert.NotNull(child);
        Assert.Equal("营业外收入", parent!.Name);
        Assert.Equal(AccountCategory.ProfitAndLoss, parent.Category);
        Assert.Equal(1, parent.Level);
        Assert.False(parent.IsLeaf);
        Assert.Equal(parent.Id, child!.ParentId);
        Assert.Equal(AccountCategory.ProfitAndLoss, child.Category);
        // 明细方向留空时恒继承上级（当前实现下自动补建的损益上级方向为借方，
        // 收入类按业务应为贷方，属生产待修问题，此处只锁定「父子方向一致」这条继承规则）。
        Assert.Equal(parent.Direction, child.Direction);
    }

    /// <summary>内存假读取器：绕过 xlsx 解析，直接返回预置行。</summary>
    private sealed class FakeAccountImportReader(IReadOnlyList<ImportedAccountRow> rows) : IAccountImportReader
    {
        public IReadOnlyList<ImportedAccountRow> Read(Stream stream) => rows;
    }
}
