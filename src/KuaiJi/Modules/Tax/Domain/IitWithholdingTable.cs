namespace KuaiJi.Modules.Tax.Domain
{
    /// <summary>
    /// 综合所得（工资薪金）累计预扣法的预扣率表，按累计应纳税所得额取预扣率与速算扣除数。
    /// 口径见《个人所得税预扣率表一》（居民个人工资、薪金所得预扣预缴适用）。
    /// </summary>
    public static class IitWithholdingTable
    {
        private static readonly (decimal Ceiling, decimal Rate, decimal QuickDeduction)[] Brackets =
        [
            (36_000m, 0.03m, 0m),
            (144_000m, 0.10m, 2_520m),
            (300_000m, 0.20m, 16_920m),
            (420_000m, 0.25m, 31_920m),
            (660_000m, 0.30m, 52_920m),
            (960_000m, 0.35m, 85_920m),
            (decimal.MaxValue, 0.45m, 181_920m),
        ];

        /// <summary>按累计应纳税所得额（元）取预扣率与速算扣除数；负数按 0 处理。</summary>
        public static (decimal Rate, decimal QuickDeduction) Lookup(decimal taxableIncome)
        {
            var value = taxableIncome > 0m ? taxableIncome : 0m;
            foreach (var bracket in Brackets)
            {
                if (value <= bracket.Ceiling)
                {
                    return (bracket.Rate, bracket.QuickDeduction);
                }
            }

            return (0.45m, 181_920m);
        }
    }
}
