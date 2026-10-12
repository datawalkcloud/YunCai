using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.FixedAssets.Domain;

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
    /// 固定资产应用服务：类别 / 卡片 / 变更记录 / 期间计提 / 折旧摊销明细 / 汇总表 / 核对总账。
    /// 折旧口径：平均年限法，月折旧额 = (原值 × (1 − 净残值率)) ÷ 折旧年限月数；
    /// 当月起用次月计提；累计折旧以应折旧总额封顶。
    /// </summary>
    public sealed class FixedAssetService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IFixedAssetRepository _assetRepository;
        private readonly IAccountRepository _accountRepository;

        public FixedAssetService(
            IAccountSetRepository accountSetRepository,
            IFixedAssetRepository assetRepository,
            IAccountRepository accountRepository)
        {
            _accountSetRepository = accountSetRepository;
            _assetRepository = assetRepository;
            _accountRepository = accountRepository;
        }

        // —— 类别 ——

        /// <summary>获取全部资产类别（附资产科目解析）。</summary>
        public async Task<IReadOnlyList<FixedAssetCategoryDto>> GetCategoriesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var categories = await _assetRepository.GetCategoriesAsync(accountSetId, cancellationToken);
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

        /// <summary>新建资产卡片（编号唯一；可录入初始化累计折旧）。</summary>
        public async Task<FixedAssetDto> CreateAssetAsync(Guid accountSetId, SaveFixedAssetRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var code = request.Code?.Trim() ?? string.Empty;
            if (await _assetRepository.AssetCodeExistsAsync(accountSetId, code, cancellationToken: cancellationToken))
            {
                throw new FixedAssetException($"资产编号「{code}」已存在。");
            }

            var category = await _assetRepository.GetCategoryByIdAsync(accountSetId, request.CategoryId, cancellationToken)
                           ?? throw new FixedAssetException("资产类别不存在。");

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
            var asset = await _assetRepository.GetAssetByIdAsync(accountSetId, id, cancellationToken);
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

        /// <summary>删除资产卡片（未生成过凭证的卡片可直接删除）；不存在时返回 false。</summary>
        public async Task<bool> DeleteAssetAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var asset = await _assetRepository.GetAssetByIdAsync(accountSetId, id, cancellationToken);
            if (asset is null)
            {
                return false;
            }

            _assetRepository.RemoveAsset(asset);
            await _assetRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>清理资产：记录清理变更并终止计提。不存在时返回 null。</summary>
        public async Task<FixedAssetDto?> ClearAssetAsync(Guid accountSetId, Guid id, DateOnly clearedDate, CancellationToken cancellationToken = default)
        {
            var asset = await _assetRepository.GetAssetByIdAsync(accountSetId, id, cancellationToken);
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

        /// <summary>获取变更记录（可按资产过滤）。</summary>
        public async Task<IReadOnlyList<FixedAssetChangeDto>> GetChangesAsync(Guid accountSetId, Guid? assetId = null, CancellationToken cancellationToken = default)
        {
            var changes = await _assetRepository.GetChangesAsync(accountSetId, assetId, cancellationToken);
            var assets = (await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken))
                .ToDictionary(a => a.Id, a => a);
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

            var assets = await _assetRepository.GetAssetsAsync(accountSetId, cancellationToken);
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
