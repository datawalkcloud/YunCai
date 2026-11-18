using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Assist.Infrastructure;
using YunCai.Modules.FixedAssets.Application;
using YunCai.Modules.FixedAssets.Domain;
using YunCai.Modules.FixedAssets.Infrastructure;
using YunCai.Modules.Vouchers.Application;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace YunCai.Tests.FixedAssets;

/// <summary>
/// 固定资产应用服务测试：卡片新增 / 编辑的校验分支与中文文案、编号自动生成（「类别编码-序号」）、
/// 期间批量计提（首期、跨月、末期尾差封顶、当月新增次月计提、已提足与清理后不再计提、返回计数与跳过原因）、
/// 删除与批量删除 / 清理的计数（已生成凭证的拦截与凭证删除后的自愈）、
/// 折旧凭证生成（借折旧费用 / 贷累计折旧、同科目合并一行、回填变更记录）与变更凭证生成分支。
/// 折旧口径依据 FixedAssetService 类注释与 AGENTS.md 领域须知。
/// </summary>
public sealed class FixedAssetServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly Account _assetAccount;       // 160101 固定资产（类别资产科目）
    private readonly Account _accumulatedAccount; // 160201 累计折旧（贷方，自动推断前缀 1602）
    private readonly Account _expenseAccount;     // 660201 折旧费（借方，自动推断前缀 6602）
    private readonly FixedAssetRepository _assets;
    private readonly FixedAssetService _service;

    public FixedAssetServiceTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _assetAccount = _db.AddAccount(_accountSet, "160101", "固定资产");
        _accumulatedAccount = _db.AddAccount(_accountSet, "160201", "累计折旧");
        _expenseAccount = _db.AddAccount(_accountSet, "660201", "折旧费");

        _assets = new FixedAssetRepository(_db.Context);
        var accountRepository = new AccountRepository(_db.Context);
        var voucherRepository = new VoucherRepository(_db.Context);
        var voucherService = new VoucherService(
            new AccountSetRepository(_db.Context),
            accountRepository,
            new AssistItemRepository(_db.Context),
            voucherRepository,
            new AccountAssistGuard());
        _service = new FixedAssetService(
            new AccountSetRepository(_db.Context),
            _assets,
            accountRepository,
            voucherRepository,
            voucherService);
    }

    public void Dispose() => _db.Dispose();

    // —— 种子助手 ——

    private async Task<FixedAssetCategoryDto> SeedCategoryAsync(
        string code = "01", int months = 12, decimal residualRate = 0m)
    {
        var dto = await _service.CreateCategoryAsync(_accountSet.Id, new SaveFixedAssetCategoryRequest
        {
            Code = code,
            Name = "电子设备",
            Method = (int)DepreciationMethod.StraightLine,
            Attr = (int)FixedAssetAttr.Fixed,
            SubjectAccountId = _assetAccount.Id,
            Months = months,
            ResidualRate = residualRate,
        });
        _db.DetachAll();
        return dto;
    }

    private async Task<FixedAssetDto> SeedAssetAsync(
        Guid categoryId, string code, decimal originValue, DateOnly startDate, decimal? initial = null)
    {
        var dto = await _service.CreateAssetAsync(_accountSet.Id, new SaveFixedAssetRequest
        {
            Code = code,
            CategoryId = categoryId,
            Name = "联想笔记本",
            Model = "T490",
            Dept = "行政部",
            StartDate = startDate,
            OriginValue = originValue,
            InitialAccumulatedDepreciation = initial,
        });
        _db.DetachAll();
        return dto;
    }

    private Task<FixedAsset> LoadAssetAsync(Guid id)
        => _db.Context.FixedAssets.AsNoTracking().SingleAsync(a => a.Id == id);

    /// <summary>直写一张凭证（不经过凭证服务，供「已生成凭证不可删」用例使用）。</summary>
    private Voucher SeedVoucher(int number)
    {
        var voucher = Voucher.Create(_accountSet.Id, "记", number, new DateOnly(2026, 1, 31), "测试制单人");
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();
        return voucher;
    }

    private async Task<FixedAssetChange> SeedChangeAsync(
        Guid assetId, FixedAssetChangeKind kind, Guid? voucherId = null, decimal amount = 0m)
    {
        var change = new FixedAssetChange(
            Guid.NewGuid(), _accountSet.Id, assetId, kind, "使用中", "已清理", 202601, amount);
        if (voucherId is { } id)
        {
            change.MarkVoucher(id);
        }

        await _db.Context.FixedAssetChanges.AddAsync(change);
        await _db.Context.SaveChangesAsync();
        return change;
    }

    private static GenerateChangeVoucherRequest ChangeVoucherRequest(
        params (Guid AccountId, decimal Debit, decimal Credit)[] entries) => new()
    {
        Entries = entries.Select(e => new ChangeVoucherEntryInput
        {
            Summary = "资产变更",
            AccountId = e.AccountId,
            Debit = e.Debit,
            Credit = e.Credit,
        }).ToList(),
    };

    // —— 资产类别：新增校验分支 ——

    [Fact]
    public async Task CreateCategory_Valid_ReturnsDtoWithSubjectAndAttrText()
    {
        var dto = await SeedCategoryAsync(months: 60, residualRate: 5m);

        Assert.Equal("01", dto.Code);
        Assert.Equal("平均年限法", dto.MethodText);
        Assert.Equal("固定资产", dto.AttrText);
        Assert.Equal("160101", dto.SubjectCode);
        Assert.Equal(60, dto.Months);
        Assert.Equal(5m, dto.ResidualRate);
    }

    [Fact]
    public async Task CreateCategory_DuplicateCode_ThrowsChineseHint()
    {
        await SeedCategoryAsync();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => SeedCategoryAsync());

        Assert.Contains("资产类别编码「01」已存在。", ex.Message);
    }

    [Fact]
    public async Task CreateCategory_ZeroMonths_ThrowsDomainGuardMessage()
    {
        // 领域规则：折旧年限（月）必须大于零（否则月折旧额无法计算）。
        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.CreateCategoryAsync(_accountSet.Id, new SaveFixedAssetCategoryRequest
            {
                Code = "02",
                Name = "机器设备",
                Method = 1,
                Attr = (int)FixedAssetAttr.Fixed,
                SubjectAccountId = _assetAccount.Id,
                Months = 0,
                ResidualRate = 5m,
            }));

        Assert.Contains("折旧年限（月）必须大于零。", ex.Message);
    }

    [Fact]
    public async Task CreateCategory_ResidualRateAbove100_ThrowsDomainGuardMessage()
    {
        // 领域规则：预计净残值率是百分比，必须落在 0-100。
        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.CreateCategoryAsync(_accountSet.Id, new SaveFixedAssetCategoryRequest
            {
                Code = "02",
                Name = "机器设备",
                Method = 1,
                Attr = (int)FixedAssetAttr.Fixed,
                SubjectAccountId = _assetAccount.Id,
                Months = 120,
                ResidualRate = 101m,
            }));

        Assert.Contains("预计净残值率必须在 0-100 之间。", ex.Message);
    }

    // —— 卡片新增 ——

    [Fact]
    public async Task CreateAsset_UnknownAccountSet_ThrowsChineseHint()
    {
        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.CreateAssetAsync(Guid.NewGuid(), new SaveFixedAssetRequest
            {
                Code = "01-001",
                CategoryId = Guid.NewGuid(),
                Name = "联想笔记本",
                StartDate = new DateOnly(2026, 1, 10),
                OriginValue = 12000m,
            }));

        Assert.Equal("指定的账套不存在。", ex.Message);
    }

    [Fact]
    public async Task CreateAsset_UnknownCategory_ThrowsChineseHint()
    {
        await SeedCategoryAsync();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.CreateAssetAsync(_accountSet.Id, new SaveFixedAssetRequest
            {
                Code = "01-001",
                CategoryId = Guid.NewGuid(),
                Name = "联想笔记本",
                StartDate = new DateOnly(2026, 1, 10),
                OriginValue = 12000m,
            }));

        Assert.Equal("资产类别不存在。", ex.Message);
    }

    [Fact]
    public async Task CreateAsset_BlankCode_AutoGeneratesByCategoryPrefixSequence()
    {
        // 领域规则：编号留空按「类别编码-序号」自动生成，序号三位补零、跳过已占用号。
        var category = await SeedCategoryAsync();

        var first = await SeedAssetAsync(category.Id, string.Empty, 12000m, new DateOnly(2025, 11, 5));
        var second = await SeedAssetAsync(category.Id, "", 24000m, new DateOnly(2025, 11, 5));

        Assert.Equal("01-001", first.Code);
        Assert.Equal("01-002", second.Code);
    }

    [Fact]
    public async Task CreateAsset_DuplicateCode_ThrowsChineseHint()
    {
        var category = await SeedCategoryAsync();
        await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => SeedAssetAsync(category.Id, "01-001", 24000m, new DateOnly(2025, 11, 5)));

        Assert.Contains("资产编号「01-001」已存在。", ex.Message);
    }

    [Fact]
    public async Task CreateAsset_ZeroOriginValue_ThrowsDomainGuardMessage()
    {
        // 领域规则：资产原值必须大于零；失败卡片不落库。
        var category = await SeedCategoryAsync();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => SeedAssetAsync(category.Id, "01-001", 0m, new DateOnly(2025, 11, 5)));

        Assert.Contains("资产原值必须大于零。", ex.Message);
        Assert.Empty(await _db.Context.FixedAssets.ToListAsync());
    }

    [Fact]
    public async Task CreateAsset_InitialAccumulatedWithinBase_SetsAccumulationAndNetValue()
    {
        // 领域规则：初始化录入已提折旧，净值 = 原值 − 累计折旧 = 12000 − 2000。
        var category = await SeedCategoryAsync();

        var dto = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5), initial: 2000m);

        Assert.Equal(2000m, dto.AccumulatedDepreciation);
        Assert.Equal(10000m, dto.NetValue);
        Assert.Equal("使用中", dto.StatusText);
    }

    [Fact]
    public async Task CreateAsset_InitialAccumulatedAboveBase_ThrowsAndPersistsNothing()
    {
        // 领域规则：初始化已提折旧不得超过应折旧总额（残值率 0 → 上限 = 原值 12000）。
        var category = await SeedCategoryAsync();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5), initial: 12000.01m));

        Assert.Contains("累计折旧不能超过应折旧总额。", ex.Message);
        Assert.Empty(await _db.Context.FixedAssets.ToListAsync());
    }

    // —— 卡片编辑 ——

    [Fact]
    public async Task UpdateAsset_NonExistent_ReturnsNull()
    {
        var category = await SeedCategoryAsync();

        Assert.Null(await _service.UpdateAssetAsync(_accountSet.Id, Guid.NewGuid(), new SaveFixedAssetRequest
        {
            Code = "01-009",
            CategoryId = category.Id,
            Name = "联想笔记本",
            StartDate = new DateOnly(2025, 11, 5),
            OriginValue = 12000m,
        }));
    }

    [Fact]
    public async Task UpdateAsset_OriginValueChanged_WritesOriginAdjustChangeWithDelta()
    {
        // 领域规则：原值变动必须留痕——变更记录「原值调整」，前后值为两位小数文本，金额 = 差额。
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));

        var dto = await _service.UpdateAssetAsync(_accountSet.Id, asset.Id, new SaveFixedAssetRequest
        {
            Code = "01-001",
            CategoryId = category.Id,
            Name = "联想笔记本",
            StartDate = new DateOnly(2025, 11, 5),
            OriginValue = 15000m,
        });
        _db.DetachAll();

        Assert.NotNull(dto);
        Assert.Equal(15000m, dto!.OriginValue);
        var change = Assert.Single(await _service.GetChangesAsync(_accountSet.Id));
        Assert.Equal("原值调整", change.KindText);
        Assert.Equal("12000.00", change.Before);
        Assert.Equal("15000.00", change.After);
        Assert.Equal(3000m, change.Amount);
    }

    [Fact]
    public async Task UpdateAsset_WithoutOriginValueChange_WritesNoChangeRecord()
    {
        // 领域规则：只有原值变动才记变更单；改名等要素调整不留「原值调整」痕。
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));

        var dto = await _service.UpdateAssetAsync(_accountSet.Id, asset.Id, new SaveFixedAssetRequest
        {
            Code = "01-001",
            CategoryId = category.Id,
            Name = "ThinkPad 笔记本",
            StartDate = new DateOnly(2025, 11, 5),
            OriginValue = 12000m,
        });
        _db.DetachAll();

        Assert.Equal("ThinkPad 笔记本", dto!.Name);
        Assert.Empty(await _service.GetChangesAsync(_accountSet.Id));
    }

    [Fact]
    public async Task UpdateAsset_CodeTakenByOtherAsset_ThrowsChineseHint()
    {
        var category = await SeedCategoryAsync();
        await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        var second = await SeedAssetAsync(category.Id, "01-002", 24000m, new DateOnly(2025, 11, 5));

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.UpdateAssetAsync(_accountSet.Id, second.Id, new SaveFixedAssetRequest
            {
                Code = "01-001",
                CategoryId = category.Id,
                Name = "联想笔记本",
                StartDate = new DateOnly(2025, 11, 5),
                OriginValue = 24000m,
            }));

        Assert.Contains("资产编号「01-001」已存在。", ex.Message);
    }

    [Fact]
    public async Task UpdateAsset_ClearedAsset_ThrowsChineseHint()
    {
        // 领域规则：清理后不可再修改卡片要素（清理是不可逆流转）。
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.ClearAssetAsync(_accountSet.Id, asset.Id, new DateOnly(2026, 1, 31));
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.UpdateAssetAsync(_accountSet.Id, asset.Id, new SaveFixedAssetRequest
            {
                Code = "01-001",
                CategoryId = category.Id,
                Name = "联想笔记本",
                StartDate = new DateOnly(2025, 11, 5),
                OriginValue = 15000m,
            }));

        Assert.Equal("资产已清理，不可修改。", ex.Message);
    }

    // —— 清理 ——

    [Fact]
    public async Task ClearAsset_Normal_TransitionsToClearedAndRecordsChange()
    {
        // 领域规则：清理终止计提，留痕「资产清理」变更（使用中 → 已清理），期间 = 清理日期所在月。
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));

        var dto = await _service.ClearAssetAsync(_accountSet.Id, asset.Id, new DateOnly(2026, 2, 10));
        _db.DetachAll();

        Assert.NotNull(dto);
        Assert.Equal("已清理", dto!.StatusText);
        Assert.Equal(new DateOnly(2026, 2, 10), dto.ClearedDate);
        Assert.Equal(FixedAssetStatus.Cleared, (await LoadAssetAsync(asset.Id)).Status);
        var change = Assert.Single(await _service.GetChangesAsync(_accountSet.Id));
        Assert.Equal("资产清理", change.KindText);
        Assert.Equal("使用中", change.Before);
        Assert.Equal("已清理", change.After);
        Assert.Equal("2026-02", change.Period);
    }

    [Fact]
    public async Task ClearAsset_DateBeforeStartDate_ThrowsAndStaysInUse()
    {
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2026, 1, 10));

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.ClearAssetAsync(_accountSet.Id, asset.Id, new DateOnly(2025, 12, 31)));

        Assert.Contains("清理日期不得早于开始使用日期。", ex.Message);
        _db.DetachAll();
        Assert.Equal(FixedAssetStatus.InUse, (await LoadAssetAsync(asset.Id)).Status);
    }

    [Fact]
    public async Task ClearAssetsAsync_MixedIds_CountsAffectedSkippedAndMessages()
    {
        // 批量清理计数口径：正常清理计入 affected；已清理的静默跳过；
        // 清理日期早于起用日的跳过并回传「编号 名称：中文原因」。
        var category = await SeedCategoryAsync();
        var a = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        var b = await SeedAssetAsync(category.Id, "01-002", 24000m, new DateOnly(2025, 11, 5));
        var c = await SeedAssetAsync(category.Id, "01-003", 6000m, new DateOnly(2025, 11, 5));
        await _service.ClearAssetAsync(_accountSet.Id, c.Id, new DateOnly(2026, 1, 31));
        var tooEarly = await SeedAssetAsync(category.Id, "01-004", 6000m, new DateOnly(2026, 3, 1));
        _db.DetachAll();

        var result = await _service.ClearAssetsAsync(
            _accountSet.Id, [a.Id, b.Id, c.Id, tooEarly.Id], new DateOnly(2026, 2, 28));

        Assert.Equal(2, result.Affected);
        Assert.Equal(2, result.Skipped);
        var message = Assert.Single(result.Messages);
        Assert.Contains("01-004", message);
        Assert.Contains("清理日期不得早于开始使用日期", message);
        Assert.Equal(FixedAssetStatus.Cleared, (await LoadAssetAsync(a.Id)).Status);
        Assert.Equal(FixedAssetStatus.InUse, (await LoadAssetAsync(tooEarly.Id)).Status);
    }

    // —— 删除与批量删除 ——

    [Fact]
    public async Task DeleteAsset_NonExistent_ReturnsFalse()
    {
        Assert.False(await _service.DeleteAssetAsync(_accountSet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsset_ChangeLinkedToLiveVoucher_ThrowsChineseHint()
    {
        // 领域规则：已生成凭证的卡片不可删除，须先删凭证。
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        var voucher = SeedVoucher(1);
        await SeedChangeAsync(asset.Id, FixedAssetChangeKind.Clearing, voucherId: voucher.Id);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.DeleteAssetAsync(_accountSet.Id, asset.Id));

        Assert.Contains("01-001 联想笔记本", ex.Message);
        Assert.Contains("已生成凭证，请先删除相关凭证再删除资产。", ex.Message);
        Assert.NotNull(await _db.Context.FixedAssets.FindAsync(asset.Id));
    }

    [Fact]
    public async Task DeleteAsset_StaleVoucherLink_HealsAndDeletesTogetherWithChanges()
    {
        // 领域规则：凭证已被删除时变更记录的失效关联先自愈，卡片不得被永久锁死；删除卡片连带删除变更记录。
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await SeedChangeAsync(asset.Id, FixedAssetChangeKind.Clearing, voucherId: Guid.NewGuid());

        Assert.True(await _service.DeleteAssetAsync(_accountSet.Id, asset.Id));
        Assert.Empty(await _db.Context.FixedAssets.ToListAsync());
        Assert.Empty(await _db.Context.FixedAssetChanges.ToListAsync());
    }

    [Fact]
    public async Task DeleteAssetsAsync_MixedIds_CountsAffectedSkippedAndMessages()
    {
        // 批量删除口径：可删的计入 affected；已生成凭证的计入 skipped 并回传原因；不存在的计入 skipped 无消息。
        var category = await SeedCategoryAsync();
        var clean = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        var linked = await SeedAssetAsync(category.Id, "01-002", 24000m, new DateOnly(2025, 11, 5));
        var voucher = SeedVoucher(1);
        await SeedChangeAsync(linked.Id, FixedAssetChangeKind.Clearing, voucherId: voucher.Id);
        _db.DetachAll();

        var result = await _service.DeleteAssetsAsync(_accountSet.Id, [clean.Id, linked.Id, Guid.NewGuid()]);

        Assert.Equal(1, result.Affected);
        Assert.Equal(2, result.Skipped);
        var message = Assert.Single(result.Messages);
        Assert.Contains("已生成凭证", message);
        Assert.Null(await _db.Context.FixedAssets.FindAsync(clean.Id));
        Assert.NotNull(await _db.Context.FixedAssets.FindAsync(linked.Id));
    }

    // —— 期间计提 ——

    [Fact]
    public async Task DepreciatePeriod_FirstPeriod_DeductsMonthlyAndWritesChange()
    {
        // 领域规则：2025-11 起用、2026-01 首个计提期间合法；
        // 平均年限法月折旧额 = 12000 ÷ 12 = 1000，留痕「每月计提」0.00 → 1000.00。
        var category = await SeedCategoryAsync(months: 12);
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));

        var result = await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);

        Assert.Equal(202601, result.Period);
        Assert.Equal(1, result.DepreciatedCount);
        Assert.Empty(result.Skipped);
        var stored = await LoadAssetAsync(asset.Id);
        Assert.Equal(1000m, stored.AccumulatedDepreciation);
        Assert.Equal(202601, stored.LastDepreciatedPeriod);
        var change = Assert.Single(await _service.GetChangesAsync(_accountSet.Id));
        Assert.Equal("每月计提", change.KindText);
        Assert.Equal("0.00", change.Before);
        Assert.Equal("1000.00", change.After);
        Assert.Equal(1000m, change.Amount);
        Assert.Equal("2026-01", change.Period);
    }

    [Fact]
    public async Task DepreciatePeriod_ConsecutiveMonths_AccumulatesMonthByMonth()
    {
        // 跨月连续计提：两个月各 1000，累计折旧 2000，最后计提期间推进到 202602。
        var category = await SeedCategoryAsync(months: 12);
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));

        await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);
        var second = await _service.DepreciatePeriodAsync(_accountSet.Id, 202602);

        Assert.Equal(1, second.DepreciatedCount);
        var stored = await LoadAssetAsync(asset.Id);
        Assert.Equal(2000m, stored.AccumulatedDepreciation);
        Assert.Equal(202602, stored.LastDepreciatedPeriod);
        Assert.Equal(2, (await _service.GetChangesAsync(_accountSet.Id)).Count);
    }

    [Fact]
    public async Task DepreciatePeriod_RepeatSamePeriod_SkipsWithAlreadyDepreciatedReason()
    {
        // 领域规则：同一期间重复计提整批跳过并给出原因，累计折旧不得翻倍。
        var category = await SeedCategoryAsync(months: 12);
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);

        var again = await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);

        Assert.Equal(0, again.DepreciatedCount);
        var reason = Assert.Single(again.Skipped);
        Assert.Contains("01-001 联想笔记本", reason);
        Assert.Contains("期间 202601 已计提过折旧", reason);
        Assert.Equal(1000m, (await LoadAssetAsync(asset.Id)).AccumulatedDepreciation);
    }

    [Fact]
    public async Task DepreciatePeriod_MonthlyRoundingDifference_FinalPeriodCapsAtBase()
    {
        // 领域规则「末期尾差」：10000 ÷ 3 = 3333.33（到分），前三期共 9999.99，
        // 第四期只能提剩余的 0.01，累计折旧恰好到 10000.00 封顶；再提报「已提足」。
        var category = await SeedCategoryAsync(months: 3);
        var asset = await SeedAssetAsync(category.Id, "01-001", 10000m, new DateOnly(2025, 11, 5));

        foreach (var period in new[] { 202601, 202602, 202603, 202604 })
        {
            Assert.Equal(1, (await _service.DepreciatePeriodAsync(_accountSet.Id, period)).DepreciatedCount);
        }

        var stored = await LoadAssetAsync(asset.Id);
        Assert.Equal(10000m, stored.AccumulatedDepreciation);
        var changes = await _service.GetChangesAsync(_accountSet.Id, asset.Id);
        Assert.Equal(4, changes.Count);
        Assert.Equal(10000m, changes.Sum(c => c.Amount));
        Assert.Contains(changes, c => c.Amount == 0.01m && c.After == "10000.00");

        var fifth = await _service.DepreciatePeriodAsync(_accountSet.Id, 202605);
        Assert.Equal(0, fifth.DepreciatedCount);
        Assert.Contains(fifth.Skipped, s => s.Contains("应折旧总额已提足"));
    }

    [Fact]
    public async Task DepreciatePeriod_AssetStartedMidMonth_StartMonthSkipped_NextMonthWorks()
    {
        // 领域规则「当月新增次月计提」：2026-01-15 起用，1 月计提批次跳过（带原因），2 月正常计提。
        var category = await SeedCategoryAsync(months: 12);
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2026, 1, 15));

        var first = await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);
        Assert.Equal(0, first.DepreciatedCount);
        Assert.Contains(first.Skipped, s => s.Contains("当月不计提折旧"));

        var second = await _service.DepreciatePeriodAsync(_accountSet.Id, 202602);
        Assert.Equal(1, second.DepreciatedCount);
        Assert.Equal(1000m, (await LoadAssetAsync(asset.Id)).AccumulatedDepreciation);
    }

    [Fact]
    public async Task DepreciatePeriod_ClearedAsset_NotDepreciatedAtAll()
    {
        // 领域规则：清理后停止计提——已清理资产不进入计提批次（也不产生跳过消息），累计折旧保持 0。
        var category = await SeedCategoryAsync(months: 12);
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.ClearAssetAsync(_accountSet.Id, asset.Id, new DateOnly(2025, 12, 31));
        _db.DetachAll();

        var result = await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);

        Assert.Equal(0, result.DepreciatedCount);
        Assert.Empty(result.Skipped);
        Assert.Equal(0m, (await LoadAssetAsync(asset.Id)).AccumulatedDepreciation);
    }

    [Fact]
    public async Task DepreciatePeriod_CategoryMissing_SkipsWithReason()
    {
        // 脏数据防线：类别被删但卡片还在时不崩溃、不计算，跳过并说明「所属类别缺失」。
        var category = await SeedCategoryAsync(months: 12);
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        _db.DetachAll();
        _db.Context.FixedAssetCategories.RemoveRange(_db.Context.FixedAssetCategories);
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var result = await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);

        Assert.Equal(0, result.DepreciatedCount);
        Assert.Contains(result.Skipped, s => s.Contains("所属类别缺失"));
        Assert.Equal(0m, (await LoadAssetAsync(asset.Id)).AccumulatedDepreciation);
    }

    [Fact]
    public async Task DepreciatePeriod_PeriodNotSixDigits_ThrowsInvalidPeriod()
    {
        // 领域规则：会计期间标识统一 yyyyMM 六位整数，格式不对直接中文报错（AGENTS.md 领域须知）。
        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.DepreciatePeriodAsync(_accountSet.Id, 20261));

        Assert.Equal("计提期间无效。", ex.Message);
    }

    [Fact]
    public async Task DepreciatePeriod_MonthThirteen_CurrentlyCrashesRaw_MonthRangeNotValidated()
    {
        // 现状记录（疑似生产问题，待裁决）：FixedAssetService.cs:543 只校验 100000-999999 数字区间，
        // 202613（月=13）通过校验后在构造期末日时抛裸 ArgumentOutOfRangeException，
        // HTTP 层会落成 500 而不是「计提期间无效。」的 400 中文文案。
        var category = await SeedCategoryAsync();
        await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.DepreciatePeriodAsync(_accountSet.Id, 202613));
    }

    // —— 折旧凭证 ——

    [Fact]
    public async Task GetDepreciationVoucherInfo_AfterDepreciationBeforeVoucher_ReportsCountTotalNotGenerated()
    {
        // 回显口径：本期计提 2 条、合计 1000 + 2000 = 3000，尚未生成凭证。
        var category = await SeedCategoryAsync(months: 12);
        await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await SeedAssetAsync(category.Id, "01-002", 24000m, new DateOnly(2025, 11, 5));
        await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);

        var info = await _service.GetDepreciationVoucherInfoAsync(_accountSet.Id, 202601);

        Assert.Equal(202601, info.Period);
        Assert.Equal(2, info.ChangeCount);
        Assert.Equal(3000m, info.TotalAmount);
        Assert.False(info.Generated);
        Assert.Null(info.VoucherId);
        var empty = await _service.GetDepreciationVoucherInfoAsync(_accountSet.Id, 202602);
        Assert.Equal(0, empty.ChangeCount);
        Assert.Equal(0m, empty.TotalAmount);
    }

    [Fact]
    public async Task GenerateDepreciationVoucher_NoRecordsInPeriod_ThrowsChineseHint()
    {
        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.GenerateDepreciationVoucherAsync(
                _accountSet.Id, new GenerateDepreciationVoucherRequest { Period = 202602 }));

        Assert.Contains("还没有计提折旧记录", ex.Message);
        Assert.Contains("请先点击「计提折旧」", ex.Message);
    }

    [Fact]
    public async Task GenerateDepreciationVoucher_TwoAssets_MergesIntoOneVoucherDebitExpenseCreditAccumulated()
    {
        // 领域规则：折旧凭证「借 折旧费用 / 贷 累计折旧」，同一科目合并成一行（1000+2000=3000），
        // 摘要「YYYY-MM 计提折旧」，日期 = 期间月末，全部计提记录回填同一凭证。
        var category = await SeedCategoryAsync(months: 12);
        await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await SeedAssetAsync(category.Id, "01-002", 24000m, new DateOnly(2025, 11, 5));
        await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);
        _db.DetachAll();

        var info = await _service.GenerateDepreciationVoucherAsync(
            _accountSet.Id, new GenerateDepreciationVoucherRequest { Period = 202601 });
        _db.DetachAll();

        Assert.True(info.Generated);
        Assert.Equal("记-1", info.VoucherText);
        Assert.Equal(2, info.ChangeCount);
        Assert.Equal(3000m, info.TotalAmount);
        var voucher = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == info.VoucherId);
        Assert.Equal(new DateOnly(2026, 1, 31), voucher.Date);
        Assert.Equal(2, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == _expenseAccount.Id && e.Debit == 3000m && e.Credit == 0m && e.Summary == "2026-01 计提折旧");
        Assert.Contains(voucher.Entries, e => e.AccountId == _accumulatedAccount.Id && e.Debit == 0m && e.Credit == 3000m);
        var changes = await _service.GetChangesAsync(_accountSet.Id);
        Assert.Equal(2, changes.Count);
        Assert.All(changes, c => Assert.Equal(voucher.Id, c.VoucherId));
        Assert.All(changes, c => Assert.Equal("记-1", c.VoucherText));
    }

    [Fact]
    public async Task GenerateDepreciationVoucher_AlreadyGenerated_ThrowsNoNeedToRepeat()
    {
        var category = await SeedCategoryAsync(months: 12);
        await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);
        await _service.GenerateDepreciationVoucherAsync(
            _accountSet.Id, new GenerateDepreciationVoucherRequest { Period = 202601 });
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.GenerateDepreciationVoucherAsync(
                _accountSet.Id, new GenerateDepreciationVoucherRequest { Period = 202601 }));

        Assert.Contains("的折旧凭证已生成，无需重复生成。", ex.Message);
    }

    [Fact]
    public async Task GenerateDepreciationVoucher_NoUsableAccumulatedAccount_ThrowsOperativeHint()
    {
        // 自动推断口径：贷方取 1602 开头启用末级科目；没有可用科目时给可操作中文提示，且不留半成品凭证。
        var category = await SeedCategoryAsync(months: 12);
        await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);
        // 重新取跟踪实例再停用：种子阶段的 DetachAll 已把 _accumulatedAccount 脱管，直接改属性不会落库。
        var accumulated = await _db.Context.Accounts.SingleAsync(a => a.Id == _accumulatedAccount.Id);
        accumulated.Disable();
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.GenerateDepreciationVoucherAsync(
                _accountSet.Id, new GenerateDepreciationVoucherRequest { Period = 202601 }));

        Assert.Contains("未找到「累计折旧」科目（1602 开头的末级科目）", ex.Message);
        Assert.Empty(await _db.Context.Vouchers.ToListAsync());
    }

    [Fact]
    public async Task GenerateDepreciationVoucher_ChangeReferencesDeletedAsset_ThrowsDeleteChangeFirst()
    {
        // 脏数据防线：计提记录指向的卡片已被直删时拒绝出票并给出处置指引，不得凭空造凭证。
        var category = await SeedCategoryAsync(months: 12);
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);
        _db.DetachAll();
        _db.Context.FixedAssets.Remove(await _db.Context.FixedAssets.SingleAsync(a => a.Id == asset.Id));
        await _db.Context.SaveChangesAsync();
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.GenerateDepreciationVoucherAsync(
                _accountSet.Id, new GenerateDepreciationVoucherRequest { Period = 202601 }));

        Assert.Contains("计提记录对应的资产已删除", ex.Message);
        Assert.Empty(await _db.Context.Vouchers.ToListAsync());
    }

    // —— 变更记录凭证 ——

    [Fact]
    public async Task GenerateChangeVoucher_ChangeNotExists_ThrowsChineseHint()
    {
        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.GenerateChangeVoucherAsync(
                _accountSet.Id, Guid.NewGuid(), ChangeVoucherRequest(
                    (_accumulatedAccount.Id, 100m, 0m), (_assetAccount.Id, 0m, 100m))));

        Assert.Equal("变更记录不存在。", ex.Message);
    }

    [Fact]
    public async Task GenerateChangeVoucher_DepreciationChange_ThrowsRedirectToPeriodPage()
    {
        // 领域规则：每月计提记录只能走「按期生成折旧凭证」整批出票，不允许单条生成，防止同月出两张折旧凭证。
        var category = await SeedCategoryAsync(months: 12);
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);
        var change = await _db.Context.FixedAssetChanges
            .AsNoTracking()
            .SingleAsync(c => c.AssetId == asset.Id && c.Kind == FixedAssetChangeKind.Depreciation);

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.GenerateChangeVoucherAsync(
                _accountSet.Id, change.Id, ChangeVoucherRequest(
                    (_expenseAccount.Id, 1000m, 0m), (_accumulatedAccount.Id, 0m, 1000m))));

        Assert.Contains("折旧记录请在「计提折旧及生成凭证」页按期生成折旧凭证", ex.Message);
        Assert.Empty(await _db.Context.Vouchers.ToListAsync());
    }

    [Fact]
    public async Task GenerateChangeVoucher_UnbalancedEntries_ThrowsDebitCreditImbalance()
    {
        // 领域规则：借贷必相等，不平衡拒绝出票。
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.ClearAssetAsync(_accountSet.Id, asset.Id, new DateOnly(2026, 1, 31));
        var change = await _db.Context.FixedAssetChanges.AsNoTracking()
            .SingleAsync(c => c.Kind == FixedAssetChangeKind.Clearing);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.GenerateChangeVoucherAsync(
                _accountSet.Id, change.Id, ChangeVoucherRequest(
                    (_accumulatedAccount.Id, 1000m, 0m), (_assetAccount.Id, 0m, 900m))));

        Assert.Contains("借贷不平衡：借方合计 1000.00，贷方合计 900.00", ex.Message);
        Assert.Empty(await _db.Context.Vouchers.ToListAsync());
    }

    [Fact]
    public async Task GenerateChangeVoucher_EntryMissingAccount_ThrowsChineseHint()
    {
        // 领域规则：每条分录必须挂到本账套的会计科目，否则拒绝出票。
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.ClearAssetAsync(_accountSet.Id, asset.Id, new DateOnly(2026, 1, 31));
        var change = await _db.Context.FixedAssetChanges.AsNoTracking()
            .SingleAsync(c => c.Kind == FixedAssetChangeKind.Clearing);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.GenerateChangeVoucherAsync(
                _accountSet.Id, change.Id, ChangeVoucherRequest(
                    (Guid.Empty, 1000m, 0m), (_assetAccount.Id, 0m, 1000m))));

        Assert.Contains("请为每一条分录选择会计科目。", ex.Message);
    }

    [Fact]
    public async Task GenerateChangeVoucher_ClearingChange_Success_MarksVoucherAndReturnsText()
    {
        // 领域规则：清理变更按用户确认的分录出票（借 累计折旧 + 借 折旧费 / 贷 固定资产，借贷平衡 12000），
        // 返回字号「记-1」并回填变更记录；只回写本条，其他记录不受影响。
        var category = await SeedCategoryAsync(months: 12);
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        await _service.DepreciatePeriodAsync(_accountSet.Id, 202601);
        await _service.ClearAssetAsync(_accountSet.Id, asset.Id, new DateOnly(2026, 2, 10));
        var clearingId = (await _db.Context.FixedAssetChanges.AsNoTracking()
            .SingleAsync(c => c.Kind == FixedAssetChangeKind.Clearing)).Id;
        _db.DetachAll();

        var text = await _service.GenerateChangeVoucherAsync(
            _accountSet.Id, clearingId, ChangeVoucherRequest(
                (_accumulatedAccount.Id, 1000m, 0m),
                (_expenseAccount.Id, 11000m, 0m),
                (_assetAccount.Id, 0m, 12000m)));
        _db.DetachAll();

        Assert.Equal("记-1", text);
        var changes = await _service.GetChangesAsync(_accountSet.Id, asset.Id);
        var clearing = Assert.Single(changes, c => c.KindText == "资产清理");
        Assert.NotNull(clearing.VoucherId);
        Assert.Equal("记-1", clearing.VoucherText);
        Assert.All(changes.Where(c => c.KindText == "每月计提"), c => Assert.Null(c.VoucherId));
        var voucher = await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == clearing.VoucherId);
        Assert.Equal(new DateOnly(2026, 2, 28), voucher.Date);
        Assert.Equal(3, voucher.Entries.Count);
        Assert.Equal(12000m, voucher.Entries.Sum(e => e.Debit));
        Assert.Equal(12000m, voucher.Entries.Sum(e => e.Credit));
    }

    [Fact]
    public async Task GenerateChangeVoucher_ChangeAlreadyLinked_ThrowsNoNeedToRepeat()
    {
        var category = await SeedCategoryAsync();
        var asset = await SeedAssetAsync(category.Id, "01-001", 12000m, new DateOnly(2025, 11, 5));
        var voucher = SeedVoucher(1);
        var change = await SeedChangeAsync(asset.Id, FixedAssetChangeKind.Clearing, voucherId: voucher.Id);
        _db.DetachAll();

        var ex = await Assert.ThrowsAsync<FixedAssetException>(
            () => _service.GenerateChangeVoucherAsync(
                _accountSet.Id, change.Id, ChangeVoucherRequest(
                    (_accumulatedAccount.Id, 100m, 0m), (_assetAccount.Id, 0m, 100m))));

        Assert.Contains("该变更记录已生成凭证，请勿重复生成。", ex.Message);
    }
}
