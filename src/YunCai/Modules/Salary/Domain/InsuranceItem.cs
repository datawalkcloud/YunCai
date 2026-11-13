namespace YunCai.Modules.Salary.Domain
{
    /// <summary>
    /// 五险一金配置项：按比例缴纳的险种及其公司 / 个人承担比例，以及默认缴纳基数。
    /// 同一账套同一险种名唯一；比例按百分比存储（如 20 表示 20%）。
    /// </summary>
    public class InsuranceItem
    {
        /// <summary>险种名称最大长度。</summary>
        public const int MaxNameLength = 50;

        private InsuranceItem()
        {
        }

        public InsuranceItem(
            Guid id,
            Guid accountSetId,
            string name,
            decimal companyRate,
            decimal? personalRate,
            int sortOrder,
            decimal baseAmount = 0m)
        {
            Id = id;
            AccountSetId = accountSetId;
            Name = name;
            CompanyRate = companyRate;
            PersonalRate = personalRate;
            SortOrder = sortOrder;
            BaseAmount = baseAmount < 0m ? 0m : baseAmount;
        }

        /// <summary>险种配置标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>险种名称（养老保险 / 医疗保险…）。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>公司缴纳比例（百分比）。</summary>
        public decimal CompanyRate { get; private set; }

        /// <summary>个人缴纳比例（百分比），可为空（工伤 / 生育个人不缴）。</summary>
        public decimal? PersonalRate { get; private set; }

        /// <summary>排序号（展示顺序）。</summary>
        public int SortOrder { get; private set; }

        /// <summary>默认缴纳基数；员工未单独设置基数时按此值计算（0 表示未设置）。</summary>
        public decimal BaseAmount { get; private set; }

        /// <summary>修改险种配置。</summary>
        public void Update(string name, decimal companyRate, decimal? personalRate, decimal baseAmount)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("险种名称不能为空。");
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"险种名称不能超过 {MaxNameLength} 个字符。", nameof(name));
            }

            if (companyRate < 0m || companyRate > 100m)
            {
                throw new ArgumentException("公司缴纳比例必须在 0-100 之间。");
            }

            if (personalRate is { } pr && (pr < 0m || pr > 100m))
            {
                throw new ArgumentException("个人缴纳比例必须在 0-100 之间。");
            }

            if (baseAmount < 0m)
            {
                throw new ArgumentException("缴纳基数不能为负数。");
            }

            Name = name.Trim();
            CompanyRate = companyRate;
            PersonalRate = personalRate;
            BaseAmount = baseAmount;
        }
    }
}
