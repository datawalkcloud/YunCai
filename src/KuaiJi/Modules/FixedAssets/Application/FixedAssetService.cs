using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.FixedAssets.Domain;
using KuaiJi.Modules.FixedAssets.Infrastructure;
using KuaiJi.Modules.Vouchers.Application;
using KuaiJi.Modules.Vouchers.Domain;
using System.Globalization;

namespace KuaiJi.Modules.FixedAssets.Application
{
    /// <summary>资产相关业务错误。</summary>
    public sealed class FixedAssetException : Exception
    {
        public FixedAssetException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 固定资产应用服务：类别 / 卡片 / 变更记录 / 期间计提 / 折旧摊销明细 / 汇总表 / 核对总账，
    /// 以及折旧、摊销、清理凭证生成与资产导入导出。
    /// 折旧口径：平均年限法，月折旧额 = (原值 × (1 − 净残值率)) ÷ 折旧年限月数；
    /// 当月起用次月计提；累计折旧以应折旧总额封顶。
    /// </summary>
    public sealed class FixedAssetService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IFixedAssetRepository _assetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly VoucherService _voucherService;

        public FixedAssetService(
            IAccountSetRepository accountSetRepository,
            IFixedAssetRepository assetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            VoucherService voucherService)
        {
            _accountSetRepository = accountSetRepository;
            _assetRepository = assetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _voucherService = voucherService;
        }

        // —— 类别 ——

        /// <summary>获取全部资产类别（附资产科目解析）；账套尚无类别时补种八类预置类别。</summary>
        public async Task<IReadOnlyList<FixedAssetCategoryDto>> GetCategoriesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var categories = await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken);
            if (categories.Count == 0 && await SeedDefaultCategoriesAsync(accountSetId, cancellationToken))
            {
                categories = await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken);
            }

            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            return categories
                .OrderBy(c => c.Code)
                .Select(c =>
                {
                    var subject = accountsById.GetValueOrDefault(c.SubjectAccountId);
                    return new FixedAssetCategoryDto
                    {
                        Id = c.Id,
                        Code = c.Code,
                        Name = c.Name,
                        AttrText = FixedAssetCategoryDto.AttrTextOf(c.Attr),
                        SubjectAccountId = c.SubjectAccountId,
                        SubjectCode = subject?.Code ?? string.Empty,
                        SubjectName = subject?.Name ?? string.Empty,
                        Months = c.Months,
                        ResidualRate = c.ResidualRate,
                    };
                })
                .ToList();
        }

        /// <summary>预置八类资产类别（与柠檬云默认口径一致）；账套无科目时科目留空待用户指定。</summary>
        private async Task<bool> SeedDefaultCategoriesAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            if (accountsById.Count == 0)
            {
                // 科目表还没建，按空科目预置会让后续折旧凭证全部失败，等科目就绪后再补种。
                return false;
            }

            var fixedAssetAccount = AutoByPrefix(accountsById, "1601", "固定资产");
            var intangibleAccount = AutoByPrefix(accountsById, "1701", "无形资产");
            var longTermAccount = AutoByPrefix(accountsById, "1801", "长期待摊费用");
            var defs = new (string Code, string Name, FixedAssetAttr Attr, int Months, decimal Residual, Guid SubjectId)[]
            {
                ("01", "房屋及建筑物", FixedAssetAttr.Fixed, 240, 5m, fixedAssetAccount?.Id ?? Guid.Empty),
                ("02", "机器设备", FixedAssetAttr.Fixed, 120, 5m, fixedAssetAccount?.Id ?? Guid.Empty),
                ("03", "运输设备", FixedAssetAttr.Fixed, 60, 5m, fixedAssetAccount?.Id ?? Guid.Empty),
                ("04", "电子设备", FixedAssetAttr.Fixed, 36, 5m, fixedAssetAccount?.Id ?? Guid.Empty),
                ("05", "办公家具", FixedAssetAttr.Fixed, 60, 5m, fixedAssetAccount?.Id ?? Guid.Empty),
                ("06", "其他设备", FixedAssetAttr.Fixed, 60, 5m, fixedAssetAccount?.Id ?? Guid.Empty),
                ("07", "无形资产", FixedAssetAttr.Intangible, 120, 0m, intangibleAccount?.Id ?? Guid.Empty),
                ("08", "长期待摊费用", FixedAssetAttr.LongTermExpense, 36, 0m, longTermAccount?.Id ?? Guid.Empty),
            };

            foreach (var def in defs)
            {
                await _assetRepository.AddCategoryAsync(FixedAssetCategory.Create(
                    accountSetId,
                    def.Code,
                    def.Name,
                    DepreciationMethod.StraightLine,
                    def.Attr,
                    def.SubjectId,
                    def.Months,
                    def.Residual));
            }

            await _assetRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>新建资产类别。预置八类在账套启用时由初始化逻辑创建，这里支持补充。</summary>
        public async Task<FixedAssetCategoryDto> CreateCategoryAsync(Guid accountSetId, SaveFixedAssetCategoryRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var code = request.Code?.Trim() ?? string.Empty;
            if (await _assetRepository.CategoryCodeExistsAsync(accountSetId, code, cancellationToken: cancellationToken))
            {
                throw new FixedAssetException($"资产类别编码「{code}」已存在。");
            }

            var category = BuildCategory(accountSetId, request);
            await _assetRepository.AddCategoryAsync(category, cancellationToken);
            await _assetRepository.SaveChangesAsync(cancellationToken);
            return (await GetCategoriesAsync(accountSetId, cancellationToken)).First(c => c.Id == category.Id);
        }

        /// <summary>修改资产类别；不存在时返回 null。</summary>
        public async Task<FixedAssetCategoryDto?> UpdateCategoryAsync(Guid accountSetId, Guid id, SaveFixedAssetCategoryRequest request, CancellationToken cancellationToken = default)
        {
            var category = await _assetRepository.GetCategoryByIdAsync(accountSetId, id, cancellationToken);
            if (category is null)
            {
                return null;
            }

            var code = request.Code?.Trim() ?? string.Empty;
            if (await _assetRepository.CategoryCodeExistsAsync(accountSetId, code, category.Id, cancellationToken))
            {
                throw new FixedAssetException($"资产类别编码「{code}」已存在。");
            }

            try
            {
                category.Update(
                    code,
                    request.Name,
                    (DepreciationMethod)request.Method,
                    (FixedAssetAttr)request.Attr,
                    request.SubjectAccountId,
                    request.Months,
                    request.ResidualRate);
            }
            catch (ArgumentException ex)
            {
                throw new FixedAssetException(ex.Message);
            }

            await _assetRepository.SaveChangesAsync(cancellationToken);
            return (await GetCategoriesAsync(accountSetId, cancellationToken)).First(c => c.Id == category.Id);
        }

        /// <summary>删除资产类别；类别下仍有资产卡片时不可删除。</summary>
        public async Task<bool> DeleteCategoryAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var category = await _assetRepository.GetCategoryByIdAsync(accountSetId, id, cancellationToken);
            if (category is null)
            {
                return false;
            }

            if (await _assetRepository.CategoryHasAssetsAsync(accountSetId, id, cancellationToken))
            {
                throw new FixedAssetException("该类别下仍有资产卡片，不可删除。");
            }

            _assetRepository.RemoveCategory(category);
            await _assetRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        // —— 卡片 ——

        /// <summary>获取全部资产卡片。</summary>
        public async Task<IReadOnlyList<FixedAssetDto>> GetAssetsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var assets = await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken);
            var categories = (await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken))
                .ToDictionary(c => c.Id, c => c);
            return assets
                .OrderBy(a => a.Code)
                .Select(a =>
                {
                    var category = categories.GetValueOrDefault(a.CategoryId);
                    return new FixedAssetDto
                    {
                        Id = a.Id,
                        Code = a.Code,
                        CategoryId = a.CategoryId,
                        CategoryName = category?.Name ?? string.Empty,
                        AttrText = FixedAssetCategoryDto.AttrTextOf(category?.Attr ?? FixedAssetAttr.Fixed),
                        Name = a.Name,
                        Model = a.Model,
                        Dept = a.Dept,
                        StartDate = a.StartDate,
                        OriginValue = a.OriginValue,
                        AccumulatedDepreciation = a.AccumulatedDepreciation,
                        StatusText = a.Status == FixedAssetStatus.InUse ? "使用中" : "已清理",
                        ClearedDate = a.ClearedDate,
                    };
                })
                .ToList();
        }

        /// <summary>新建资产卡片（编号唯一，留空按「类别编码-序号」自动生成；可录入初始化累计折旧）。</summary>
        public async Task<FixedAssetDto> CreateAssetAsync(Guid accountSetId, SaveFixedAssetRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var category = await _assetRepository.GetCategoryByIdAsync(accountSetId, request.CategoryId, cancellationToken)
                           ?? throw new FixedAssetException("资产类别不存在。");

            var code = request.Code?.Trim() ?? string.Empty;
            if (code.Length == 0)
            {
                code = await GenerateAssetCodeAsync(accountSetId, category, cancellationToken);
            }

            if (await _assetRepository.AssetCodeExistsAsync(accountSetId, code, cancellationToken: cancellationToken))
            {
                throw new FixedAssetException($"资产编号「{code}」已存在。");
            }

            FixedAsset asset;
            try
            {
                asset = FixedAsset.Create(
                    accountSetId,
                    code,
                    request.CategoryId,
                    request.Name,
                    request.Model,
                    request.Dept,
                    request.StartDate,
                    request.OriginValue);
                if (request.InitialAccumulatedDepreciation is { } initial)
                {
                    asset.AdjustAccumulatedDepreciation(initial, asset.DepreciableBase(category.ResidualRate));
                }
            }
            catch (ArgumentException ex)
            {
                throw new FixedAssetException(ex.Message);
            }

            await _assetRepository.AddAssetAsync(asset, cancellationToken);
            await _assetRepository.SaveChangesAsync(cancellationToken);
            return (await GetAssetsAsync(accountSetId, cancellationToken)).First(a => a.Id == asset.Id);
        }

        /// <summary>修改资产卡片；原值变动写变更记录。不存在时返回 null。</summary>
        public async Task<FixedAssetDto?> UpdateAssetAsync(Guid accountSetId, Guid id, SaveFixedAssetRequest request, CancellationToken cancellationToken = default)
        {
            var asset = await _assetRepository.GetAssetForUpdateAsync(accountSetId, id, cancellationToken);
            if (asset is null)
            {
                return null;
            }

            if (asset.Status != FixedAssetStatus.InUse)
            {
                throw new FixedAssetException("资产已清理，不可修改。");
            }

            var code = request.Code?.Trim() ?? string.Empty;
            if (await _assetRepository.AssetCodeExistsAsync(accountSetId, code, asset.Id, cancellationToken))
            {
                throw new FixedAssetException($"资产编号「{code}」已存在。");
            }

            var before = asset.OriginValue;
            try
            {
                asset.Update(code, request.CategoryId, request.Name, request.Model, request.Dept, request.StartDate, request.OriginValue);
            }
            catch (ArgumentException ex)
            {
                throw new FixedAssetException(ex.Message);
            }

            var period = DateTime.UtcNow.Year * 100 + DateTime.UtcNow.Month;
            if (before != asset.OriginValue)
            {
                await _assetRepository.AddChangeAsync(new FixedAssetChange(
                    Guid.NewGuid(),
                    accountSetId,
                    asset.Id,
                    FixedAssetChangeKind.OriginValueAdjust,
                    before.ToString("0.00"),
                    asset.OriginValue.ToString("0.00"),
                    period,
                    asset.OriginValue - before));
            }

            await _assetRepository.SaveChangesAsync(cancellationToken);
            return (await GetAssetsAsync(accountSetId, cancellationToken)).First(a => a.Id == asset.Id);
        }

        /// <summary>删除资产卡片：连同其变更记录一并删除；已生成凭证的卡片不可删除。</summary>
        public async Task<bool> DeleteAssetAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var asset = await _assetRepository.GetAssetByIdAsync(accountSetId, id, cancellationToken);
            if (asset is null)
            {
                return false;
            }

            var changes = await _assetRepository.GetChangesForUpdateAsync(accountSetId, assetId: id, cancellationToken: cancellationToken);
            // 凭证可能已被删除，先自愈清理失效关联，否则卡片会被永久锁死。
            await ClearStaleVouchersAsync(changes, await LoadVouchersByIdAsync(accountSetId, cancellationToken), cancellationToken);
            if (changes.Any(c => c.VoucherId is not null))
            {
                throw new FixedAssetException($"资产「{asset.Code} {asset.Name}」已生成凭证，请先删除相关凭证再删除资产。");
            }

            if (changes.Count > 0)
            {
                _assetRepository.RemoveChanges(changes);
            }

            _assetRepository.RemoveAsset(asset);
            await _assetRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>批量删除资产：逐条尝试，已生成凭证的计入跳过。</summary>
        public async Task<FixedAssetBulkResultDto> DeleteAssetsAsync(Guid accountSetId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
        {
            var affected = 0;
            var skipped = 0;
            var messages = new List<string>();
            foreach (var id in ids.Distinct())
            {
                try
                {
                    if (await DeleteAssetAsync(accountSetId, id, cancellationToken))
                    {
                        affected++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (FixedAssetException ex)
                {
                    skipped++;
                    messages.Add(ex.Message);
                }
            }

            return new FixedAssetBulkResultDto { Affected = affected, Skipped = skipped, Messages = messages };
        }

        /// <summary>批量清理资产：统一按指定日期清理，已清理 / 已生成清理凭证的计入跳过。</summary>
        public async Task<FixedAssetBulkResultDto> ClearAssetsAsync(Guid accountSetId, IReadOnlyList<Guid> ids, DateOnly clearedDate, CancellationToken cancellationToken = default)
        {
            var affected = 0;
            var skipped = 0;
            var messages = new List<string>();
            foreach (var id in ids.Distinct())
            {
                var asset = await _assetRepository.GetAssetForUpdateAsync(accountSetId, id, cancellationToken);
                if (asset is null || asset.Status != FixedAssetStatus.InUse)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    asset.Clear(clearedDate);
                    await _assetRepository.AddChangeAsync(new FixedAssetChange(
                        Guid.NewGuid(),
                        accountSetId,
                        asset.Id,
                        FixedAssetChangeKind.Clearing,
                        "使用中",
                        "已清理",
                        clearedDate.Year * 100 + clearedDate.Month,
                        0m));
                    affected++;
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    skipped++;
                    messages.Add($"{asset.Code} {asset.Name}：{ex.Message}");
                }
            }

            if (affected > 0)
            {
                await _assetRepository.SaveChangesAsync(cancellationToken);
            }

            return new FixedAssetBulkResultDto { Affected = affected, Skipped = skipped, Messages = messages };
        }

        /// <summary>复制资产：以同一卡片要素新建一张卡片，编号不填时自动追加序号。</summary>
        public async Task<FixedAssetDto> CopyAssetAsync(Guid accountSetId, Guid id, CopyFixedAssetRequest request, CancellationToken cancellationToken = default)
        {
            var source = await _assetRepository.GetAssetByIdAsync(accountSetId, id, cancellationToken)
                         ?? throw new FixedAssetException("要复制的资产不存在。");

            var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
            if (code is null)
            {
                for (var i = 1; i < 1000; i++)
                {
                    var candidate = $"{source.Code}-{i}";
                    if (!await _assetRepository.AssetCodeExistsAsync(accountSetId, candidate, cancellationToken: cancellationToken))
                    {
                        code = candidate;
                        break;
                    }
                }
            }

            if (code is null)
            {
                throw new FixedAssetException("自动生成资产编号失败，请手工填写新编号。");
            }

            if (await _assetRepository.AssetCodeExistsAsync(accountSetId, code, cancellationToken: cancellationToken))
            {
                throw new FixedAssetException($"资产编号「{code}」已存在。");
            }

            var name = string.IsNullOrWhiteSpace(request.Name) ? source.Name : request.Name.Trim();
            var copy = FixedAsset.Create(
                accountSetId,
                code,
                source.CategoryId,
                name,
                source.Model,
                source.Dept,
                source.StartDate,
                source.OriginValue);

            var category = await _assetRepository.GetCategoryByIdAsync(accountSetId, source.CategoryId, cancellationToken);
            if (source.AccumulatedDepreciation > 0m && category is not null)
            {
                copy.AdjustAccumulatedDepreciation(source.AccumulatedDepreciation, copy.DepreciableBase(category.ResidualRate));
            }

            await _assetRepository.AddAssetAsync(copy, cancellationToken);
            await _assetRepository.SaveChangesAsync(cancellationToken);
            return (await GetAssetsAsync(accountSetId, cancellationToken)).First(a => a.Id == copy.Id);
        }

        /// <summary>清理资产：记录清理变更并终止计提。不存在时返回 null。</summary>
        public async Task<FixedAssetDto?> ClearAssetAsync(Guid accountSetId, Guid id, DateOnly clearedDate, CancellationToken cancellationToken = default)
        {
            var asset = await _assetRepository.GetAssetForUpdateAsync(accountSetId, id, cancellationToken);
            if (asset is null)
            {
                return null;
            }

            try
            {
                asset.Clear(clearedDate);
            }
            catch (ArgumentException ex)
            {
                throw new FixedAssetException(ex.Message);
            }

            var period = clearedDate.Year * 100 + clearedDate.Month;
            await _assetRepository.AddChangeAsync(new FixedAssetChange(
                Guid.NewGuid(),
                accountSetId,
                asset.Id,
                FixedAssetChangeKind.Clearing,
                "使用中",
                "已清理",
                period,
                0m));
            await _assetRepository.SaveChangesAsync(cancellationToken);
            return (await GetAssetsAsync(accountSetId, cancellationToken)).First(a => a.Id == asset.Id);
        }

        // —— 变更记录 ——

        /// <summary>获取变更记录（可按资产过滤），附关联凭证字号；凭证被删除时自愈清空关联。</summary>
        public async Task<IReadOnlyList<FixedAssetChangeDto>> GetChangesAsync(Guid accountSetId, Guid? assetId = null, CancellationToken cancellationToken = default)
        {
            var changes = await _assetRepository.GetChangesAsync(accountSetId, assetId, cancellationToken);
            var assets = (await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken))
                .ToDictionary(a => a.Id, a => a);
            var vouchersById = await LoadVouchersByIdAsync(accountSetId, cancellationToken);
            await ClearStaleVouchersAsync(changes, vouchersById, cancellationToken);

            return changes
                .OrderByDescending(c => c.CreatedAt)
                .Select(c =>
                {
                    var asset = assets.GetValueOrDefault(c.AssetId);
                    return new FixedAssetChangeDto
                    {
                        Id = c.Id,
                        AssetId = c.AssetId,
                        AssetCode = asset?.Code ?? string.Empty,
                        AssetName = asset?.Name ?? string.Empty,
                        KindText = FixedAssetChangeDto.KindTextOf(c.Kind),
                        Before = c.Before,
                        After = c.After,
                        Period = FormatPeriod(c.Period),
                        Amount = c.Amount,
                        VoucherId = c.VoucherId,
                        VoucherText = VoucherTextOf(c.VoucherId, vouchersById),
                    };
                })
                .ToList();
        }

        // —— 期间计提 ——

        /// <summary>按期间批量计提折旧：对所有在用、当期可提的资产计提，逐条留痕。
        /// 未到起用次月 / 已提足的资产跳过并给出原因。</summary>
        public async Task<DepreciatePeriodResultDto> DepreciatePeriodAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            if (period is < 100000 or > 999999)
            {
                throw new FixedAssetException("计提期间无效。");
            }

            // 计提要回写累计折旧与最后计提期间，必须用跟踪查询（AsNoTracking 改完不落库）。
            var assets = await _assetRepository.GetAssetsForUpdateAsync(accountSetId, cancellationToken);
            var categories = (await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken))
                .ToDictionary(c => c.Id, c => c);
            var skipped = new List<string>();
            var count = 0;
            foreach (var asset in assets.Where(a => a.Status == FixedAssetStatus.InUse))
            {
                var category = categories.GetValueOrDefault(asset.CategoryId);
                if (category is null)
                {
                    skipped.Add($"{asset.Code} {asset.Name}：所属类别缺失，跳过");
                    continue;
                }

                var periodEnd = new DateOnly(period / 100, period % 100, DateTime.DaysInMonth(period / 100, period % 100));
                try
                {
                    var amount = asset.Depreciate(period, asset.MonthlyDepreciation(category.ResidualRate, category.Months), asset.DepreciableBase(category.ResidualRate), periodEnd);
                    await _assetRepository.AddChangeAsync(new FixedAssetChange(
                        Guid.NewGuid(),
                        accountSetId,
                        asset.Id,
                        FixedAssetChangeKind.Depreciation,
                        (asset.AccumulatedDepreciation - amount).ToString("0.00"),
                        asset.AccumulatedDepreciation.ToString("0.00"),
                        period,
                        amount));
                    count++;
                }
                catch (InvalidOperationException ex)
                {
                    skipped.Add($"{asset.Code} {asset.Name}：{ex.Message}");
                }
            }

            await _assetRepository.SaveChangesAsync(cancellationToken);
            return new DepreciatePeriodResultDto { Period = period, DepreciatedCount = count, Skipped = skipped };
        }

        /// <summary>折旧摊销明细表：指定期间全部资产的折旧取数。</summary>
        public async Task<IReadOnlyList<DepreciationLedgerRowDto>> GetDepreciationLedgerAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            var assets = await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken);
            var categories = (await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken))
                .ToDictionary(c => c.Id, c => c);
            var changes = await _assetRepository.GetChangesAsync(accountSetId, cancellationToken: cancellationToken);
            var yearPrefix = (period / 100).ToString("D4");

            return assets
                .OrderBy(a => a.Code)
                .Select(a =>
                {
                    var category = categories.GetValueOrDefault(a.CategoryId);
                    var periodDep = changes
                        .Where(c => c.AssetId == a.Id && c.Kind == FixedAssetChangeKind.Depreciation && c.Period == period)
                        .Sum(c => c.Amount);
                    var yearAccumulated = changes
                        .Where(c => c.AssetId == a.Id && c.Kind == FixedAssetChangeKind.Depreciation && (c.Period / 100).ToString("D4") == yearPrefix && c.Period <= period)
                        .Sum(c => c.Amount);
                    return new DepreciationLedgerRowDto
                    {
                        AssetId = a.Id,
                        AssetCode = a.Code,
                        CategoryName = category?.Name ?? string.Empty,
                        AttrText = FixedAssetCategoryDto.AttrTextOf(category?.Attr ?? FixedAssetAttr.Fixed),
                        AssetName = a.Name,
                        Model = a.Model,
                        Dept = a.Dept,
                        OriginValue = a.OriginValue,
                        PeriodDepreciation = periodDep,
                        YearAccumulated = yearAccumulated,
                        TotalAccumulated = a.AccumulatedDepreciation,
                    };
                })
                .ToList();
        }

        /// <summary>资产汇总表：按部门或类别分组，原值 / 累计折旧 / 净值四列口径。
        /// 期初数 = 期间开始前已存在部分的取值；本期增加 = 期间内起用（原值）或期间内计提（折旧）；
        /// 本期减少 = 期间内清理；期末数 = 当前值。近似口径详见各分项注释。</summary>
        public async Task<IReadOnlyList<FixedAssetSummaryRowDto>> GetSummaryAsync(Guid accountSetId, int startPeriod, int endPeriod, bool byCategory, CancellationToken cancellationToken = default)
        {
            var assets = await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken);
            var categories = (await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken))
                .ToDictionary(c => c.Id, c => c);
            var changes = await _assetRepository.GetChangesAsync(accountSetId, cancellationToken: cancellationToken);

            var groups = byCategory
                ? assets.GroupBy(a => categories.GetValueOrDefault(a.CategoryId)?.Name ?? string.Empty)
                : assets.GroupBy(a => a.Dept ?? "未分配");

            return groups
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    decimal originOpening = 0, originIncrease = 0, originDecrease = 0;
                    decimal depOpening = 0, depIncrease = 0, depDecrease = 0;
                    foreach (var asset in g)
                    {
                        var startPeriodOfAsset = asset.StartDate.Year * 100 + asset.StartDate.Month;
                        if (startPeriodOfAsset < startPeriod)
                        {
                            originOpening += asset.OriginValue;
                        }
                        else if (startPeriodOfAsset <= endPeriod)
                        {
                            originIncrease += asset.OriginValue;
                        }

                        if (asset.ClearedDate is { } cleared)
                        {
                            var clearedPeriod = cleared.Year * 100 + cleared.Month;
                            if (clearedPeriod >= startPeriod && clearedPeriod <= endPeriod)
                            {
                                originDecrease += asset.OriginValue;
                            }
                        }

                        var assetChanges = changes.Where(c => c.AssetId == asset.Id && c.Kind == FixedAssetChangeKind.Depreciation);
                        foreach (var change in assetChanges)
                        {
                            if (change.Period < startPeriod)
                            {
                                depOpening += change.Amount;
                            }
                            else if (change.Period <= endPeriod)
                            {
                                depIncrease += change.Amount;
                            }
                        }
                    }

                    var originClosing = g.Where(a => a.Status == FixedAssetStatus.InUse).Sum(a => a.OriginValue);
                    var depClosing = g.Sum(a => a.AccumulatedDepreciation);
                    return new FixedAssetSummaryRowDto
                    {
                        GroupName = g.Key,
                        OriginOpening = originOpening,
                        OriginIncrease = originIncrease,
                        OriginDecrease = originDecrease,
                        OriginClosing = originClosing,
                        DepOpening = depOpening,
                        DepIncrease = depIncrease,
                        DepDecrease = depDecrease,
                        DepClosing = depClosing,
                    };
                })
                .ToList();
        }

        /// <summary>资产核对总账：按类别分组，会计科目行（期初口径）与资产卡片行成对，附差异行。
        /// 会计科目侧当前取科目期初余额（尚未叠加已过账凭证发生额），口径升级留待账簿源接入。</summary>
        public async Task<IReadOnlyList<FixedAssetCheckRowDto>> GetCheckAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var assets = await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken);
            var categories = await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);

            var result = new List<FixedAssetCheckRowDto>();
            foreach (var category in categories.OrderBy(c => c.Code))
            {
                var subject = accountsById.GetValueOrDefault(category.SubjectAccountId);
                result.Add(new FixedAssetCheckRowDto
                {
                    Item = "▼ " + category.Name,
                    Name = string.Empty,
                });
                result.Add(new FixedAssetCheckRowDto
                {
                    Item = "会计科目",
                    Name = subject is null ? string.Empty : $"{subject.Code} {subject.Name}",
                    Opening = subject?.OpeningBalance ?? 0m,
                    Balance = subject?.OpeningBalance ?? 0m,
                });
                var cards = assets.Where(a => a.CategoryId == category.Id).ToList();
                var origin = cards.Sum(a => a.OriginValue);
                var accumulated = cards.Sum(a => a.AccumulatedDepreciation);
                var cardText = category.Attr switch
                {
                    FixedAssetAttr.Intangible => "无形资产原值",
                    FixedAssetAttr.LongTermExpense => "长期待摊费用净值",
                    _ => "固定资产原值",
                };
                result.Add(new FixedAssetCheckRowDto
                {
                    Item = "资产卡片",
                    Name = cardText,
                    Opening = origin,
                    Balance = origin - accumulated,
                });
                var diff = (subject?.OpeningBalance ?? 0m) - origin;
                result.Add(new FixedAssetCheckRowDto
                {
                    Item = "差异",
                    Name = string.Empty,
                    Opening = diff,
                    Balance = diff,
                    IsDiff = true,
                });
            }

            return result;
        }

        // —— 凭证科目设置 ——

        /// <summary>读取资产凭证科目映射；未配置项回落到自动推断并标出是否解析到。</summary>
        public async Task<FixedAssetVoucherSettingDto> GetVoucherSettingAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var setting = await _assetRepository.GetVoucherSettingAsync(accountSetId, cancellationToken);
            var resolved = ResolveVoucherAccounts(setting, accountsById);

            return new FixedAssetVoucherSettingDto
            {
                DepreciationExpenseAccountId = setting?.DepreciationExpenseAccountId,
                DepreciationAccountId = setting?.DepreciationAccountId,
                AmortizationExpenseAccountId = setting?.AmortizationExpenseAccountId,
                AmortizationAccountId = setting?.AmortizationAccountId,
                DisposalAccountId = setting?.DisposalAccountId,
                DepreciationExpenseAccountText = Text(accountsById, resolved.DepreciationExpense?.Id),
                DepreciationAccountText = Text(accountsById, resolved.Depreciation?.Id),
                AmortizationExpenseAccountText = Text(accountsById, resolved.AmortizationExpense?.Id),
                AmortizationAccountText = Text(accountsById, resolved.Amortization?.Id),
                DisposalAccountText = Text(accountsById, resolved.Disposal?.Id),
                DepreciationResolved = resolved.Depreciation is not null && resolved.DepreciationExpense is not null,
                AmortizationResolved = resolved.Amortization is not null && resolved.AmortizationExpense is not null,
                DisposalResolved = resolved.Disposal is not null,
            };
        }

        /// <summary>保存资产凭证科目映射；留空的项回落到自动推断。</summary>
        public async Task<FixedAssetVoucherSettingDto> SaveVoucherSettingAsync(
            Guid accountSetId,
            SaveFixedAssetVoucherSettingRequest request,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            ValidateAccount(accountsById, request.DepreciationExpenseAccountId, "折旧费用科目");
            ValidateAccount(accountsById, request.DepreciationAccountId, "累计折旧科目");
            ValidateAccount(accountsById, request.AmortizationExpenseAccountId, "摊销费用科目");
            ValidateAccount(accountsById, request.AmortizationAccountId, "累计摊销科目");
            ValidateAccount(accountsById, request.DisposalAccountId, "固定资产清理科目");
            if (request.DepreciationExpenseAccountId is { } de && de != Guid.Empty
                && request.DepreciationAccountId is { } da && da == de)
            {
                throw new FixedAssetException("折旧费用科目与累计折旧科目不能相同。");
            }

            if (request.AmortizationExpenseAccountId is { } ae && ae != Guid.Empty
                && request.AmortizationAccountId is { } aa && aa == ae)
            {
                throw new FixedAssetException("摊销费用科目与累计摊销科目不能相同。");
            }

            var setting = await _assetRepository.GetVoucherSettingAsync(accountSetId, cancellationToken);
            if (setting is null)
            {
                setting = FixedAssetVoucherSetting.Create(accountSetId);
                await _assetRepository.AddVoucherSettingAsync(setting, cancellationToken);
            }

            setting.Update(
                request.DepreciationExpenseAccountId,
                request.DepreciationAccountId,
                request.AmortizationExpenseAccountId,
                request.AmortizationAccountId,
                request.DisposalAccountId);
            await _assetRepository.SaveChangesAsync(cancellationToken);
            return await GetVoucherSettingAsync(accountSetId, cancellationToken);
        }

        // —— 折旧凭证 ——

        /// <summary>查询某期间折旧凭证状态（供「计提折旧」页回显）。</summary>
        public async Task<DepreciationVoucherInfoDto> GetDepreciationVoucherInfoAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            var changes = await _assetRepository.GetChangesAsync(accountSetId, cancellationToken: cancellationToken);
            var depChanges = changes.Where(c => c.Kind == FixedAssetChangeKind.Depreciation && c.Period == period).ToList();
            var voucherId = depChanges.Select(c => c.VoucherId).FirstOrDefault(id => id is not null);
            var vouchersById = voucherId is null
                ? new Dictionary<Guid, Voucher>()
                : await LoadVouchersByIdAsync(accountSetId, cancellationToken);

            return new DepreciationVoucherInfoDto
            {
                Period = period,
                ChangeCount = depChanges.Count,
                TotalAmount = depChanges.Sum(c => c.Amount),
                Generated = voucherId is not null,
                VoucherId = voucherId,
                VoucherText = VoucherTextOf(voucherId, vouchersById),
            };
        }

        /// <summary>
        /// 生成某期间的折旧 / 摊销凭证：把当期尚无关联合同的计提记录按资产属性归集，
        /// 固定资产走「借 折旧费用 / 贷 累计折旧」，无形资产走「借 摊销费用 / 贷 累计摊销」，
        /// 长期待摊费用直接冲减其资产科目；同一科目合并成一行，最终生成一张凭证。
        /// </summary>
        public async Task<DepreciationVoucherInfoDto> GenerateDepreciationVoucherAsync(
            Guid accountSetId,
            GenerateDepreciationVoucherRequest request,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var period = request.Period;
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var setting = await _assetRepository.GetVoucherSettingAsync(accountSetId, cancellationToken);
            var resolved = ResolveVoucherAccounts(setting, accountsById);

            var periodChanges = await _assetRepository.GetChangesForUpdateAsync(accountSetId, period, cancellationToken: cancellationToken);
            var depChanges = periodChanges.Where(c => c.Kind == FixedAssetChangeKind.Depreciation).ToList();
            if (depChanges.Count == 0)
            {
                throw new FixedAssetException($"{FormatPeriod(period)} 还没有计提折旧记录，请先点击「计提折旧」。");
            }

            var pending = depChanges.Where(c => c.VoucherId is null).ToList();
            if (pending.Count == 0)
            {
                throw new FixedAssetException($"{FormatPeriod(period)} 的折旧凭证已生成，无需重复生成。");
            }

            var assets = (await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken)).ToDictionary(a => a.Id, a => a);
            var categories = (await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken)).ToDictionary(c => c.Id, c => c);
            var debits = new Dictionary<Guid, decimal>();
            var credits = new Dictionary<Guid, decimal>();

            foreach (var change in pending)
            {
                if (change.Amount <= 0m)
                {
                    continue;
                }

                var asset = assets.GetValueOrDefault(change.AssetId)
                            ?? throw new FixedAssetException("计提记录对应的资产已删除，请先删除该变更记录后再生成凭证。");
                var category = categories.GetValueOrDefault(asset.CategoryId)
                               ?? throw new FixedAssetException($"资产「{asset.Code}」所属类别已删除，无法生成凭证。");

                var (debitId, creditId) = ResolveDepreciationPair(category, resolved, accountsById);
                Accumulate(debits, debitId, change.Amount);
                Accumulate(credits, creditId, change.Amount);
            }

            if (debits.Count == 0)
            {
                throw new FixedAssetException($"{FormatPeriod(period)} 的计提金额为零，无需生成凭证。");
            }

            var summary = $"{FormatPeriod(period)} 计提折旧";
            var entries = new List<CreateVoucherEntryRequest>();
            entries.AddRange(debits.Where(d => d.Value != 0m)
                .Select(d => new CreateVoucherEntryRequest { Summary = summary, AccountId = d.Key, Debit = d.Value, Credit = 0m }));
            entries.AddRange(credits.Where(c => c.Value != 0m)
                .Select(c => new CreateVoucherEntryRequest { Summary = summary, AccountId = c.Key, Debit = 0m, Credit = c.Value }));

            var voucher = await _voucherService.CreateAsync(
                accountSetId,
                string.IsNullOrWhiteSpace(request.Preparer) ? "系统" : request.Preparer,
                new CreateVoucherRequest
                {
                    Word = string.IsNullOrWhiteSpace(request.Word) ? "记" : request.Word.Trim(),
                    Number = 0,
                    Date = PeriodEndDate(period),
                    AttachmentCount = 0,
                    Entries = entries,
                },
                cancellationToken);

            foreach (var change in pending)
            {
                change.MarkVoucher(voucher.Id);
            }

            await _assetRepository.SaveChangesAsync(cancellationToken);

            return new DepreciationVoucherInfoDto
            {
                Period = period,
                ChangeCount = depChanges.Count,
                TotalAmount = depChanges.Sum(c => c.Amount),
                Generated = true,
                VoucherId = voucher.Id,
                VoucherText = $"{voucher.Word}-{voucher.Number}",
            };
        }

        // —— 变更记录凭证 ——

        /// <summary>变更记录生成凭证的建议分录（界面可改科目与金额后提交）。</summary>
        public async Task<ChangeVoucherPreviewDto> GetChangeVoucherPreviewAsync(Guid accountSetId, Guid changeId, CancellationToken cancellationToken = default)
        {
            var change = await _assetRepository.GetChangeByIdAsync(accountSetId, changeId, cancellationToken)
                         ?? throw new FixedAssetException("变更记录不存在。");
            var asset = await _assetRepository.GetAssetByIdAsync(accountSetId, change.AssetId, cancellationToken)
                        ?? throw new FixedAssetException("变更记录对应的资产已删除。");
            var category = await _assetRepository.GetCategoryByIdAsync(accountSetId, asset.CategoryId, cancellationToken);
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var setting = await _assetRepository.GetVoucherSettingAsync(accountSetId, cancellationToken);
            var resolved = ResolveVoucherAccounts(setting, accountsById);
            var subjectId = category?.SubjectAccountId ?? Guid.Empty;
            var entries = new List<ChangeVoucherEntryDto>();
            string? hint = null;

            switch (change.Kind)
            {
                case FixedAssetChangeKind.OriginValueAdjust:
                {
                    var delta = change.Amount;
                    if (delta >= 0m)
                    {
                        entries.Add(Entry("原值调整", subjectId, accountsById, debit: Math.Abs(delta)));
                        entries.Add(Entry("原值调整", Guid.Empty, accountsById, credit: Math.Abs(delta)));
                        hint = "原值增加：借方为资产科目，请补充贷方对方科目（如银行存款 / 应付账款）。";
                    }
                    else
                    {
                        entries.Add(Entry("原值调整", Guid.Empty, accountsById, debit: Math.Abs(delta)));
                        entries.Add(Entry("原值调整", subjectId, accountsById, credit: Math.Abs(delta)));
                        hint = "原值减少：贷方为资产科目，请补充借方对方科目。";
                    }

                    break;
                }

                case FixedAssetChangeKind.AccumulatedDepreciationAdjust:
                {
                    var delta = change.Amount;
                    var accumulatedId = category?.Attr == FixedAssetAttr.Intangible
                        ? resolved.Amortization?.Id ?? Guid.Empty
                        : resolved.Depreciation?.Id ?? Guid.Empty;
                    if (delta >= 0m)
                    {
                        entries.Add(Entry("累计折旧调整", Guid.Empty, accountsById, debit: Math.Abs(delta)));
                        entries.Add(Entry("累计折旧调整", accumulatedId, accountsById, credit: Math.Abs(delta)));
                    }
                    else
                    {
                        entries.Add(Entry("累计折旧调整", accumulatedId, accountsById, debit: Math.Abs(delta)));
                        entries.Add(Entry("累计折旧调整", Guid.Empty, accountsById, credit: Math.Abs(delta)));
                    }

                    hint = "累计折旧调整：另一方可指定为以前年度损益调整 / 管理费用等科目。";
                    break;
                }

                case FixedAssetChangeKind.Clearing:
                {
                    var accumulated = asset.AccumulatedDepreciation;
                    var net = asset.OriginValue - accumulated;
                    if (accumulated > 0m)
                    {
                        entries.Add(Entry("资产清理", resolved.Depreciation?.Id ?? resolved.Amortization?.Id ?? Guid.Empty, accountsById, debit: accumulated));
                    }

                    if (net > 0m)
                    {
                        entries.Add(Entry("资产清理", resolved.Disposal?.Id ?? Guid.Empty, accountsById, debit: net));
                    }

                    entries.Add(Entry("资产清理", subjectId, accountsById, credit: asset.OriginValue));
                    hint = "资产清理：借累计折旧与固定资产清理、贷资产原值；处置收入与清理损益请另行录入。";
                    break;
                }

                default:
                {
                    entries.Add(Entry($"{FormatPeriod(change.Period)} 计提折旧", subjectId, accountsById, debit: change.Amount));
                    entries.Add(Entry($"{FormatPeriod(change.Period)} 计提折旧", resolved.Depreciation?.Id ?? Guid.Empty, accountsById, credit: change.Amount));
                    hint = "计提折旧：借方为资产科目，请按需改为折旧费用科目。";
                    break;
                }
            }

            return new ChangeVoucherPreviewDto
            {
                ChangeId = change.Id,
                KindText = FixedAssetChangeDto.KindTextOf(change.Kind),
                AssetCode = asset.Code,
                AssetName = asset.Name,
                Date = change.Period > 0 ? PeriodEndDate(change.Period) : DateOnly.FromDateTime(DateTime.UtcNow),
                Entries = entries,
                Hint = hint,
            };
        }

        /// <summary>按用户确认的分录生成变更凭证并回填关联。返回凭证字号（如「记-12」）。</summary>
        public async Task<string> GenerateChangeVoucherAsync(
            Guid accountSetId,
            Guid changeId,
            GenerateChangeVoucherRequest request,
            CancellationToken cancellationToken = default)
        {
            var change = await _assetRepository.GetChangeByIdAsync(accountSetId, changeId, cancellationToken)
                         ?? throw new FixedAssetException("变更记录不存在。");
            if (change.VoucherId is not null)
            {
                throw new FixedAssetException("该变更记录已生成凭证，请勿重复生成。");
            }

            if (change.Kind == FixedAssetChangeKind.Depreciation)
            {
                throw new FixedAssetException("折旧记录请在「计提折旧及生成凭证」页按期生成折旧凭证。");
            }

            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var entries = new List<CreateVoucherEntryRequest>();
            foreach (var input in request.Entries)
            {
                if (input.Debit == 0m && input.Credit == 0m)
                {
                    continue;
                }

                if (input.AccountId == Guid.Empty || !accountsById.TryGetValue(input.AccountId, out var account))
                {
                    throw new FixedAssetException("请为每一条分录选择会计科目。");
                }

                if (!account.IsLeaf)
                {
                    throw new FixedAssetException($"科目「{account.Code} {account.Name}」不是末级科目，不能用于记账。");
                }

                if (!account.IsEnabled)
                {
                    throw new FixedAssetException($"科目「{account.Code} {account.Name}」已停用，不能用于记账。");
                }

                if (input.Debit != 0m && input.Credit != 0m)
                {
                    throw new FixedAssetException("同一条分录不能同时填写借方与贷方金额。");
                }

                entries.Add(new CreateVoucherEntryRequest
                {
                    Summary = string.IsNullOrWhiteSpace(input.Summary) ? "资产变更" : input.Summary.Trim(),
                    AccountId = input.AccountId,
                    Debit = input.Debit,
                    Credit = input.Credit,
                });
            }

            if (entries.Count < 2)
            {
                throw new FixedAssetException("凭证至少需要两条分录。");
            }

            var debitTotal = entries.Sum(e => e.Debit);
            var creditTotal = entries.Sum(e => e.Credit);
            if (debitTotal != creditTotal)
            {
                throw new FixedAssetException($"借贷不平衡：借方合计 {debitTotal:0.00}，贷方合计 {creditTotal:0.00}。");
            }

            if (debitTotal == 0m)
            {
                throw new FixedAssetException("凭证金额不能为零。");
            }

            var voucher = await _voucherService.CreateAsync(
                accountSetId,
                string.IsNullOrWhiteSpace(request.Preparer) ? "系统" : request.Preparer,
                new CreateVoucherRequest
                {
                    Word = string.IsNullOrWhiteSpace(request.Word) ? "记" : request.Word.Trim(),
                    Number = 0,
                    Date = request.Date ?? (change.Period > 0 ? PeriodEndDate(change.Period) : DateOnly.FromDateTime(DateTime.UtcNow)),
                    AttachmentCount = 0,
                    Entries = entries,
                },
                cancellationToken);

            var tracked = await _assetRepository.GetChangesForUpdateAsync(accountSetId, assetId: change.AssetId, cancellationToken: cancellationToken);
            var target = tracked.FirstOrDefault(c => c.Id == change.Id);
            target?.MarkVoucher(voucher.Id);
            await _assetRepository.SaveChangesAsync(cancellationToken);
            return $"{voucher.Word}-{voucher.Number}";
        }

        // —— 导入导出 ——

        /// <summary>导出全部资产卡片。</summary>
        public async Task<byte[]> ExportAssetsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var assets = await GetAssetsAsync(accountSetId, cancellationToken);
            return FixedAssetExportBuilder.BuildAssets(assets, await LoadCategoryCodesAsync(accountSetId, cancellationToken));
        }

        /// <summary>资产导入模板（表头 + 空行）。</summary>
        public byte[] BuildAssetTemplate() => FixedAssetExportBuilder.BuildAssetTemplate();

        /// <summary>导入资产：按资产编号匹配，存在则更新卡片要素，不存在则新建。</summary>
        public async Task<FixedAssetImportResultDto> ImportAssetsAsync(
            Guid accountSetId,
            IReadOnlyList<FixedAssetImportRow> rows,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var categories = await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken);
            var byCode = categories.ToDictionary(c => c.Code, c => c, StringComparer.Ordinal);
            var byName = categories
                .GroupBy(c => c.Name, StringComparer.Ordinal)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var failures = new List<FixedAssetImportFailureDto>();
            var imported = 0;
            var updated = 0;
            var existingByCode = (await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken))
                .ToDictionary(a => a.Code, a => a.Id, StringComparer.Ordinal);

            foreach (var row in rows)
            {
                var code = row.Code?.Trim();
                if (string.IsNullOrEmpty(code))
                {
                    failures.Add(new FixedAssetImportFailureDto { Row = row.Row, Reason = "资产编号不能为空。" });
                    continue;
                }

                FixedAssetCategory? category = null;
                if (!string.IsNullOrWhiteSpace(row.CategoryCode) && byCode.TryGetValue(row.CategoryCode.Trim(), out var byCodeHit))
                {
                    category = byCodeHit;
                }
                else if (!string.IsNullOrWhiteSpace(row.CategoryName) && byName.TryGetValue(row.CategoryName.Trim(), out var byNameHit))
                {
                    category = byNameHit;
                }

                if (category is null)
                {
                    failures.Add(new FixedAssetImportFailureDto
                    {
                        Row = row.Row,
                        Reason = string.IsNullOrWhiteSpace(row.CategoryCode) && string.IsNullOrWhiteSpace(row.CategoryName)
                            ? "未填写资产类别。"
                            : $"资产类别「{row.CategoryCode ?? row.CategoryName}」不存在，请先在「资产类别设置」中新增。",
                    });
                    continue;
                }

                if (!DateOnly.TryParse(row.StartDate?.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
                {
                    failures.Add(new FixedAssetImportFailureDto { Row = row.Row, Reason = "开始使用日期格式不正确，应为 yyyy-MM-dd。" });
                    continue;
                }

                decimal origin;
                decimal accumulated;
                try
                {
                    origin = ParseAmount(row.OriginValue);
                    accumulated = ParseAmount(row.AccumulatedDepreciation);
                }
                catch (FormatException)
                {
                    failures.Add(new FixedAssetImportFailureDto { Row = row.Row, Reason = "金额格式不正确，应填写数字（如 10000.00）。" });
                    continue;
                }

                var existing = existingByCode.TryGetValue(code, out var existingId)
                    ? await _assetRepository.GetAssetForUpdateAsync(accountSetId, existingId, cancellationToken)
                    : null;

                try
                {
                    if (existing is null)
                    {
                        var asset = FixedAsset.Create(accountSetId, code, category.Id, row.Name ?? string.Empty, row.Model, row.Dept, startDate, origin);
                        if (accumulated > 0m)
                        {
                            asset.AdjustAccumulatedDepreciation(accumulated, asset.DepreciableBase(category.ResidualRate));
                        }

                        await _assetRepository.AddAssetAsync(asset, cancellationToken);
                        imported++;
                    }
                    else
                    {
                        if (existing.Status != FixedAssetStatus.InUse)
                        {
                            failures.Add(new FixedAssetImportFailureDto { Row = row.Row, Reason = $"资产「{code}」已清理，不可更新。" });
                            continue;
                        }

                        existing.Update(code, category.Id, row.Name ?? string.Empty, row.Model, row.Dept, startDate, origin);
                        existing.AdjustAccumulatedDepreciation(accumulated, existing.DepreciableBase(category.ResidualRate));
                        updated++;
                    }
                }
                catch (ArgumentException ex)
                {
                    failures.Add(new FixedAssetImportFailureDto { Row = row.Row, Reason = ex.Message });
                }
            }

            if (imported + updated > 0)
            {
                await _assetRepository.SaveChangesAsync(cancellationToken);
            }

            return new FixedAssetImportResultDto { Imported = imported, Updated = updated, Failures = failures };
        }

        /// <summary>导出折旧摊销明细表。</summary>
        public async Task<byte[]> ExportDepreciationLedgerAsync(Guid accountSetId, int period, CancellationToken cancellationToken = default)
        {
            var rows = await GetDepreciationLedgerAsync(accountSetId, period, cancellationToken);
            return FixedAssetExportBuilder.BuildDepreciationLedger(rows, period);
        }

        /// <summary>导出资产汇总表。</summary>
        public async Task<byte[]> ExportSummaryAsync(Guid accountSetId, int startPeriod, int endPeriod, bool byCategory, CancellationToken cancellationToken = default)
        {
            var rows = await GetSummaryAsync(accountSetId, startPeriod, endPeriod, byCategory, cancellationToken);
            return FixedAssetExportBuilder.BuildSummary(rows, byCategory);
        }

        /// <summary>导出资产类别。</summary>
        public async Task<byte[]> ExportCategoriesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var categories = await GetCategoriesAsync(accountSetId, cancellationToken);
            return FixedAssetExportBuilder.BuildCategories(categories);
        }

        /// <summary>导出资产核对总账。</summary>
        public async Task<byte[]> ExportCheckAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var rows = await GetCheckAsync(accountSetId, cancellationToken);
            return FixedAssetExportBuilder.BuildCheck(rows);
        }

        /// <summary>类别编码映射（导出资产时附类别编码，便于回导）。</summary>
        private async Task<IReadOnlyDictionary<string, string>> LoadCategoryCodesAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var categories = await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken);
            return categories.ToDictionary(c => c.Name, c => c.Code, StringComparer.Ordinal);
        }

        private static FixedAssetCategory BuildCategory(Guid accountSetId, SaveFixedAssetCategoryRequest request)
        {
            try
            {
                return FixedAssetCategory.Create(
                    accountSetId,
                    request.Code,
                    request.Name,
                    (DepreciationMethod)request.Method,
                    (FixedAssetAttr)request.Attr,
                    request.SubjectAccountId,
                    request.Months,
                    request.ResidualRate);
            }
            catch (ArgumentException ex)
            {
                throw new FixedAssetException(ex.Message);
            }
        }

        private static string FormatPeriod(int period) => period == 0 ? string.Empty : $"{period / 100:D4}-{period % 100:D2}";

        /// <summary>按「类别编码-序号」生成下一个可用资产编号（如 01-003）。</summary>
        private async Task<string> GenerateAssetCodeAsync(Guid accountSetId, FixedAssetCategory category, CancellationToken cancellationToken)
        {
            var assets = await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken);
            var prefix = string.IsNullOrWhiteSpace(category.Code) ? "ZC" : category.Code.Trim();
            for (var i = 1; i < 10000; i++)
            {
                var candidate = $"{prefix}-{i:D3}";
                if (assets.All(a => !string.Equals(a.Code, candidate, StringComparison.Ordinal)))
                {
                    return candidate;
                }
            }

            throw new FixedAssetException("自动生成资产编号失败，请手工填写资产编号。");
        }

        // —— 凭证科目解析 ——

        /// <summary>解析后的凭证科目集合（null 表示账套缺少该类科目）。</summary>
        private sealed record VoucherAccounts(
            Account? DepreciationExpense,
            Account? Depreciation,
            Account? AmortizationExpense,
            Account? Amortization,
            Account? Disposal);

        /// <summary>按设置优先、自动推断兜底解析凭证科目。</summary>
        private static VoucherAccounts ResolveVoucherAccounts(FixedAssetVoucherSetting? setting, IReadOnlyDictionary<Guid, Account> accountsById)
        {
            var depreciation = Pick(setting?.DepreciationAccountId, AutoByPrefix(accountsById, "1602", "累计折旧"), accountsById);
            var amortization = Pick(setting?.AmortizationAccountId, AutoByPrefix(accountsById, "1702", "累计摊销"), accountsById);
            var depreciationExpense = Pick(setting?.DepreciationExpenseAccountId, AutoByPrefix(accountsById, "6602", "折旧"), accountsById)
                                      ?? AutoExpenseByName(accountsById, "折旧");
            var amortizationExpense = Pick(setting?.AmortizationExpenseAccountId, AutoByPrefix(accountsById, "6602", "摊销"), accountsById)
                                      ?? AutoExpenseByName(accountsById, "摊销");
            var disposal = Pick(setting?.DisposalAccountId, AutoByPrefix(accountsById, "1606", "固定资产清理"), accountsById);
            return new VoucherAccounts(depreciationExpense, depreciation, amortizationExpense, amortization, disposal);
        }

        /// <summary>按资产属性取「借 / 贷」科目对；缺科目时抛出可读错误。</summary>
        private static (Guid DebitId, Guid CreditId) ResolveDepreciationPair(
            FixedAssetCategory category,
            VoucherAccounts resolved,
            IReadOnlyDictionary<Guid, Account> accountsById)
        {
            switch (category.Attr)
            {
                case FixedAssetAttr.Intangible:
                {
                    var debit = resolved.AmortizationExpense
                                ?? throw new FixedAssetException("未找到无形资产摊销费用科目（6602 下名称含「摊销」的末级科目），请在「折旧凭证设置」中指定。");
                    var credit = resolved.Amortization
                                 ?? throw new FixedAssetException("未找到「累计摊销」科目（1702 开头的末级科目），请在「折旧凭证设置」中指定。");
                    EnsureUsable(credit, accountsById);
                    EnsureUsable(debit, accountsById);
                    if (debit.Id == credit.Id)
                    {
                        throw new FixedAssetException("摊销费用科目与累计摊销科目不能相同，请在「折旧凭证设置」中分别指定。");
                    }

                    return (debit.Id, credit.Id);
                }

                case FixedAssetAttr.LongTermExpense:
                {
                    var debit = resolved.AmortizationExpense
                                ?? throw new FixedAssetException("未找到长期待摊费用摊销科目（6602 下名称含「摊销」的末级科目），请在「折旧凭证设置」中指定。");
                    var credit = accountsById.GetValueOrDefault(category.SubjectAccountId)
                                 ?? throw new FixedAssetException("长期待摊费用类别未设置有效的资产科目，请在「资产类别设置」中补充。");
                    EnsureUsable(credit, accountsById);
                    EnsureUsable(debit, accountsById);
                    return (debit.Id, credit.Id);
                }

                default:
                {
                    var debit = resolved.DepreciationExpense
                                ?? throw new FixedAssetException("未找到折旧费用科目（6602 下名称含「折旧」的末级科目），请在「折旧凭证设置」中指定。");
                    var credit = resolved.Depreciation
                                 ?? throw new FixedAssetException("未找到「累计折旧」科目（1602 开头的末级科目），请在「折旧凭证设置」中指定。");
                    EnsureUsable(credit, accountsById);
                    EnsureUsable(debit, accountsById);
                    if (debit.Id == credit.Id)
                    {
                        throw new FixedAssetException("折旧费用科目与累计折旧科目不能相同，请在「折旧凭证设置」中分别指定。");
                    }

                    return (debit.Id, credit.Id);
                }
            }
        }

        private static void EnsureUsable(Account account, IReadOnlyDictionary<Guid, Account> accountsById)
        {
            if (!accountsById.ContainsKey(account.Id))
            {
                throw new FixedAssetException($"科目「{account.Code} {account.Name}」不存在。");
            }

            if (!account.IsLeaf)
            {
                throw new FixedAssetException($"科目「{account.Code} {account.Name}」不是末级科目，不能用于记账。");
            }

            if (!account.IsEnabled)
            {
                throw new FixedAssetException($"科目「{account.Code} {account.Name}」已停用，不能用于记账。");
            }
        }

        private static Account? Pick(Guid? configured, Account? fallback, IReadOnlyDictionary<Guid, Account> accountsById)
            => configured is { } id && accountsById.TryGetValue(id, out var account) ? account : fallback;

        private static Account? AutoByPrefix(IReadOnlyDictionary<Guid, Account> accountsById, string prefix, string keyword)
        {
            var candidates = accountsById.Values
                .Where(a => a.IsLeaf && a.IsEnabled && a.Code.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .ToList();
            return candidates.FirstOrDefault(a => a.Name.Contains(keyword, StringComparison.Ordinal)) ?? candidates.FirstOrDefault();
        }

        /// <summary>按名称找损益类费用科目（排除「累计折旧 / 累计摊销」这类资产备抵科目）。</summary>
        private static Account? AutoExpenseByName(IReadOnlyDictionary<Guid, Account> accountsById, string keyword)
            => accountsById.Values
                .Where(a => a.IsLeaf
                            && a.IsEnabled
                            && a.Category == AccountCategory.ProfitAndLoss
                            && a.Name.Contains(keyword, StringComparison.Ordinal)
                            && !a.Name.Contains("累计", StringComparison.Ordinal))
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .FirstOrDefault();

        private static string? Text(IReadOnlyDictionary<Guid, Account> accountsById, Guid? id)
            => id is { } value && accountsById.TryGetValue(value, out var account)
                ? $"{account.Code} {account.Name}"
                : null;

        private static void ValidateAccount(IReadOnlyDictionary<Guid, Account> accountsById, Guid? id, string roleName)
        {
            if (id is not { } value || value == Guid.Empty)
            {
                return;
            }

            if (!accountsById.TryGetValue(value, out var account))
            {
                throw new FixedAssetException($"所选{roleName}不存在。");
            }

            if (!account.IsLeaf)
            {
                throw new FixedAssetException($"所选{roleName}「{account.Code} {account.Name}」不是末级科目，不能用于记账。");
            }
        }

        private static ChangeVoucherEntryDto Entry(string summary, Guid accountId, IReadOnlyDictionary<Guid, Account> accountsById, decimal debit = 0m, decimal credit = 0m)
            => new()
            {
                Summary = summary,
                AccountId = accountId,
                AccountText = Text(accountsById, accountId == Guid.Empty ? null : accountId) ?? string.Empty,
                Debit = debit,
                Credit = credit,
            };

        private static void Accumulate(IDictionary<Guid, decimal> target, Guid accountId, decimal amount)
            => target[accountId] = target.TryGetValue(accountId, out var current) ? current + amount : amount;

        private static decimal ParseAmount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0m;
            }

            var cleaned = text.Trim().Replace(",", string.Empty).Replace("￥", string.Empty).Replace("¥", string.Empty);
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new FormatException();
        }

        private static DateOnly PeriodEndDate(int period)
        {
            var year = period / 100;
            var month = period % 100;
            return new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        }

        /// <summary>加载账套内未删除的凭证（按 Id 索引），用于回填字号与自愈判断。</summary>
        private async Task<IReadOnlyDictionary<Guid, Voucher>> LoadVouchersByIdAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            return vouchers.Where(v => v.DeletedAt is null).ToDictionary(v => v.Id, v => v);
        }

        /// <summary>凭证被删除后清空变更记录上的关联，避免永久锁死。</summary>
        private async Task ClearStaleVouchersAsync(
            IReadOnlyList<FixedAssetChange> changes,
            IReadOnlyDictionary<Guid, Voucher> vouchersById,
            CancellationToken cancellationToken)
        {
            var existing = vouchersById.Keys.ToHashSet();
            var stale = changes.Where(c => c.ClearStaleVoucher(existing)).ToList();
            if (stale.Count == 0)
            {
                return;
            }

            foreach (var change in stale)
            {
                _assetRepository.Update(change);
            }

            await _assetRepository.SaveChangesAsync(cancellationToken);
        }

        private static string? VoucherTextOf(Guid? voucherId, IReadOnlyDictionary<Guid, Voucher> vouchersById)
            => voucherId is { } id && vouchersById.TryGetValue(id, out var voucher)
                ? $"{voucher.Word}-{voucher.Number}"
                : null;

        private async Task EnsureAccountSetExistsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new FixedAssetException("指定的账套不存在。");
            }
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsByIdAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }
    }
}
