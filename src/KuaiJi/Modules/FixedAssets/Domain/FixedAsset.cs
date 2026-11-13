namespace KuaiJi.Modules.FixedAssets.Domain
{
    /// <summary>
    /// 固定资产卡片聚合根。登记资产原值、开始使用日期与所属类别，
    /// 折旧按平均年限法逐月计提（月折旧额 = (原值 − 原值 × 净残值率) ÷ 折旧年限月数），
    /// 累计折旧不超过应折旧总额；清理后停止计提。
    /// </summary>
    public class FixedAsset
    {
        /// <summary>资产编号最大长度。</summary>
        public const int MaxCodeLength = 50;

        /// <summary>资产名称最大长度。</summary>
        public const int MaxNameLength = 100;

        /// <summary>规格型号 / 部门最大长度。</summary>
        public const int MaxTextLength = 100;

        private FixedAsset()
        {
        }

        private FixedAsset(
            Guid id,
            Guid accountSetId,
            string code,
            Guid categoryId,
            string name,
            string? model,
            string? dept,
            DateOnly startDate,
            decimal originValue)
        {
            Id = id;
            AccountSetId = accountSetId;
            Code = code;
            CategoryId = categoryId;
            Name = name;
            Model = model;
            Dept = dept;
            StartDate = startDate;
            OriginValue = originValue;
            AccumulatedDepreciation = 0m;
            Status = FixedAssetStatus.InUse;
        }

        /// <summary>资产卡片标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>资产编号，同一账套内唯一。</summary>
        public string Code { get; private set; } = null!;

        /// <summary>所属资产类别标识。</summary>
        public Guid CategoryId { get; private set; }

        /// <summary>资产名称。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>规格型号，可为空。</summary>
        public string? Model { get; private set; }

        /// <summary>使用部门，可为空。</summary>
        public string? Dept { get; private set; }

        /// <summary>开始使用日期。</summary>
        public DateOnly StartDate { get; private set; }

        /// <summary>资产原值，必须大于零。</summary>
        public decimal OriginValue { get; private set; }

        /// <summary>累计折旧（含初始化录入的已提折旧）。</summary>
        public decimal AccumulatedDepreciation { get; private set; }

        /// <summary>资产状态。</summary>
        public FixedAssetStatus Status { get; private set; }

        /// <summary>是否为期初（资产初始化）卡片；期初卡片只在「资产初始化」页签展示。</summary>
        public bool IsInitial { get; private set; }

        /// <summary>最后计提折旧的期间（yyyyMM，0 表示未计提过）。</summary>
        public int LastDepreciatedPeriod { get; private set; }

        /// <summary>清理日期（已清理时填写）。</summary>
        public DateOnly? ClearedDate { get; private set; }

        /// <summary>应折旧总额 = 原值 × (1 − 净残值率)。</summary>
        public decimal DepreciableBase(decimal residualRate)
            => decimal.Round(OriginValue * (1m - residualRate / 100m), 2);

        /// <summary>月折旧额 = 应折旧总额 ÷ 折旧年限月数。</summary>
        public decimal MonthlyDepreciation(decimal residualRate, int months)
            => months <= 0 ? 0m : decimal.Round(DepreciableBase(residualRate) / months, 2);

        /// <summary>新建资产卡片，须归属于某一账套；<paramref name="isInitial"/> 标记期初初始化卡片。</summary>
        public static FixedAsset Create(
            Guid accountSetId,
            string code,
            Guid categoryId,
            string name,
            string? model,
            string? dept,
            DateOnly startDate,
            decimal originValue,
            bool isInitial = false)
        {
            GuardCommon(code, name, startDate, originValue);
            return new FixedAsset(
                Guid.NewGuid(),
                accountSetId,
                code.Trim(),
                categoryId,
                name.Trim(),
                Normalize(model),
                Normalize(dept),
                startDate,
                originValue)
            {
                IsInitial = isInitial,
            };
        }

        /// <summary>修改卡片要素（编号 / 类别 / 名称 / 型号 / 部门 / 起用日 / 原值）。</summary>
        public void Update(
            string code,
            Guid categoryId,
            string name,
            string? model,
            string? dept,
            DateOnly startDate,
            decimal originValue)
        {
            GuardCommon(code, name, startDate, originValue);
            Code = code.Trim();
            CategoryId = categoryId;
            Name = name.Trim();
            Model = Normalize(model);
            Dept = Normalize(dept);
            StartDate = startDate;
            OriginValue = originValue;
        }

        /// <summary>直接调整累计折旧（初始化录入 / 变更单），不得低于零或高于应折旧总额。</summary>
        public void AdjustAccumulatedDepreciation(decimal value, decimal depreciableBase)
        {
            if (value < 0m)
            {
                throw new ArgumentException("累计折旧不能为负数。");
            }

            if (value > depreciableBase)
            {
                throw new ArgumentException("累计折旧不能超过应折旧总额。");
            }

            AccumulatedDepreciation = value;
        }

        /// <summary>
        /// 计提某一期间的折旧。同一期间只能计提一次；期间必须晚于上次计提期间且不早于开始使用期间；
        /// 清理后的资产不再计提。返回本次计提金额。
        /// </summary>
        public decimal Depreciate(int period, decimal monthlyAmount, decimal depreciableBase, DateOnly periodEndDate)
        {
            if (Status != FixedAssetStatus.InUse)
            {
                throw new InvalidOperationException("资产已清理，不能再计提折旧。");
            }

            if (LastDepreciatedPeriod == period)
            {
                throw new InvalidOperationException($"期间 {period} 已计提过折旧。");
            }

            if (LastDepreciatedPeriod != 0 && period <= LastDepreciatedPeriod)
            {
                throw new InvalidOperationException("计提期间必须晚于上次计提期间。");
            }

            var startPeriod = StartDate.Year * 100 + StartDate.Month;
            if (period < startPeriod)
            {
                throw new InvalidOperationException("计提期间不能早于开始使用期间。");
            }

            var remaining = depreciableBase - AccumulatedDepreciation;
            if (remaining <= 0m)
            {
                throw new InvalidOperationException("应折旧总额已提足，不能再计提。");
            }

            if (periodEndDate < StartDate.AddMonths(1).AddDays(-1) && StartDate.Day > 1)
            {
                // 当月新增次月计提口径：起用当月不计提。
                throw new InvalidOperationException($"资产 {StartDate:yyyy-MM} 起用，当月不计提折旧。");
            }

            var amount = monthlyAmount > remaining ? remaining : monthlyAmount;
            AccumulatedDepreciation += amount;
            LastDepreciatedPeriod = period;
            return amount;
        }

        /// <summary>清理资产（终止计提）。清理后不可再修改卡片要素。</summary>
        public void Clear(DateOnly clearedDate)
        {
            if (Status == FixedAssetStatus.Cleared)
            {
                throw new InvalidOperationException("资产已清理。");
            }

            if (clearedDate < StartDate)
            {
                throw new ArgumentException("清理日期不得早于开始使用日期。");
            }

            Status = FixedAssetStatus.Cleared;
            ClearedDate = clearedDate;
        }

        private static void GuardCommon(string code, string name, DateOnly startDate, decimal originValue)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("资产编号不能为空。");
            }

            if (code.Trim().Length > MaxCodeLength)
            {
                throw new ArgumentException($"资产编号不能超过 {MaxCodeLength} 个字符。", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("资产名称不能为空。");
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"资产名称不能超过 {MaxNameLength} 个字符。", nameof(name));
            }

            if (originValue <= 0m)
            {
                throw new ArgumentException("资产原值必须大于零。");
            }
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
