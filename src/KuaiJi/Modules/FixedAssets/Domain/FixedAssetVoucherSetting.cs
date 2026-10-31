namespace KuaiJi.Modules.FixedAssets.Domain
{
    /// <summary>
    /// 资产凭证科目映射设置（每账套至多一条）。
    /// 对应界面上的「折旧 / 摊销凭证设置」：把计提折旧、无形资产摊销、资产清理要用的科目定下来，
    /// 未配置时由服务层按编码前缀自动推断（1602 累计折旧 / 1702 累计摊销 / 6602 折旧费用 / 1606 固定资产清理）。
    /// </summary>
    public class FixedAssetVoucherSetting
    {
        private FixedAssetVoucherSetting()
        {
        }

        private FixedAssetVoucherSetting(Guid id, Guid accountSetId)
        {
            Id = id;
            AccountSetId = accountSetId;
        }

        /// <summary>设置标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>折旧费用科目（计提固定资产折旧凭证借方）。</summary>
        public Guid? DepreciationExpenseAccountId { get; private set; }

        /// <summary>累计折旧科目（计提固定资产折旧凭证贷方）。</summary>
        public Guid? DepreciationAccountId { get; private set; }

        /// <summary>摊销费用科目（无形资产 / 长期待摊费用摊销凭证借方）。</summary>
        public Guid? AmortizationExpenseAccountId { get; private set; }

        /// <summary>累计摊销科目（无形资产摊销凭证贷方）。</summary>
        public Guid? AmortizationAccountId { get; private set; }

        /// <summary>固定资产清理科目（资产清理凭证借方）。</summary>
        public Guid? DisposalAccountId { get; private set; }

        /// <summary>为账套创建默认设置（全部留空，走自动推断）。</summary>
        public static FixedAssetVoucherSetting Create(Guid accountSetId)
            => new(Guid.NewGuid(), accountSetId);

        /// <summary>覆盖科目映射；传 null 表示该科目回落到自动推断。</summary>
        public void Update(
            Guid? depreciationExpenseAccountId,
            Guid? depreciationAccountId,
            Guid? amortizationExpenseAccountId,
            Guid? amortizationAccountId,
            Guid? disposalAccountId)
        {
            DepreciationExpenseAccountId = EmptyToNull(depreciationExpenseAccountId);
            DepreciationAccountId = EmptyToNull(depreciationAccountId);
            AmortizationExpenseAccountId = EmptyToNull(amortizationExpenseAccountId);
            AmortizationAccountId = EmptyToNull(amortizationAccountId);
            DisposalAccountId = EmptyToNull(disposalAccountId);
        }

        private static Guid? EmptyToNull(Guid? value) => value is null || value == Guid.Empty ? null : value;
    }
}
