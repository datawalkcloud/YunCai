namespace KuaiJi.Modules.FixedAssets.Domain
{
    /// <summary>折旧方法（当前仅支持平均年限法）。</summary>
    public enum DepreciationMethod
    {
        /// <summary>平均年限法。</summary>
        StraightLine = 1,
    }

    /// <summary>资产属性。</summary>
    public enum FixedAssetAttr
    {
        /// <summary>固定资产。</summary>
        Fixed = 1,

        /// <summary>无形资产。</summary>
        Intangible = 2,

        /// <summary>长期待摊费用。</summary>
        LongTermExpense = 3,
    }

    /// <summary>资产状态。</summary>
    public enum FixedAssetStatus
    {
        /// <summary>使用中。</summary>
        InUse = 1,

        /// <summary>已清理。</summary>
        Cleared = 2,
    }

    /// <summary>资产变动单类别。</summary>
    public enum FixedAssetChangeKind
    {
        /// <summary>原值调整。</summary>
        OriginValueAdjust = 1,

        /// <summary>累计折旧调整。</summary>
        AccumulatedDepreciationAdjust = 2,

        /// <summary>资产清理。</summary>
        Clearing = 3,

        /// <summary>每月计提折旧。</summary>
        Depreciation = 4,
    }
}
