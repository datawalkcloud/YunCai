namespace YunCai.Modules.FixedAssets.Domain
{
    /// <summary>
    /// 资产类别：资产卡片归类的字典项，决定折旧方法、折旧年限与预计净残值率，
    /// 并关联资产对应的会计科目（供资产核对总账与生成凭证使用）。
    /// </summary>
    public class FixedAssetCategory
    {
        /// <summary>类别编码最大长度。</summary>
        public const int MaxCodeLength = 20;

        /// <summary>类别名称最大长度。</summary>
        public const int MaxNameLength = 100;

        private FixedAssetCategory()
        {
        }

        private FixedAssetCategory(
            Guid id,
            Guid accountSetId,
            string code,
            string name,
            DepreciationMethod method,
            FixedAssetAttr attr,
            Guid subjectAccountId,
            int months,
            decimal residualRate)
        {
            Id = id;
            AccountSetId = accountSetId;
            Code = code;
            Name = name;
            Method = method;
            Attr = attr;
            SubjectAccountId = subjectAccountId;
            Months = months;
            ResidualRate = residualRate;
        }

        /// <summary>类别标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>类别编码，同一账套内唯一。</summary>
        public string Code { get; private set; } = null!;

        /// <summary>类别名称（如 房屋、建筑物）。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>折旧方法。</summary>
        public DepreciationMethod Method { get; private set; }

        /// <summary>资产属性。</summary>
        public FixedAssetAttr Attr { get; private set; }

        /// <summary>资产对应会计科目标识（末级科目）。</summary>
        public Guid SubjectAccountId { get; private set; }

        /// <summary>折旧年限（月），必须大于零。</summary>
        public int Months { get; private set; }

        /// <summary>预计净残值率（百分比 0-100）。</summary>
        public decimal ResidualRate { get; private set; }

        /// <summary>新建资产类别，须归属于某一账套。</summary>
        public static FixedAssetCategory Create(
            Guid accountSetId,
            string code,
            string name,
            DepreciationMethod method,
            FixedAssetAttr attr,
            Guid subjectAccountId,
            int months,
            decimal residualRate)
        {
            GuardCommon(code, name, subjectAccountId, months, residualRate);
            return new FixedAssetCategory(
                Guid.NewGuid(),
                accountSetId,
                code.Trim(),
                name.Trim(),
                method,
                attr,
                subjectAccountId,
                months,
                residualRate);
        }

        /// <summary>修改资产类别。</summary>
        public void Update(
            string code,
            string name,
            DepreciationMethod method,
            FixedAssetAttr attr,
            Guid subjectAccountId,
            int months,
            decimal residualRate)
        {
            GuardCommon(code, name, subjectAccountId, months, residualRate);
            Code = code.Trim();
            Name = name.Trim();
            Method = method;
            Attr = attr;
            SubjectAccountId = subjectAccountId;
            Months = months;
            ResidualRate = residualRate;
        }

        private static void GuardCommon(string code, string name, Guid subjectAccountId, int months, decimal residualRate)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("类别编码不能为空。");
            }

            if (code.Trim().Length > MaxCodeLength)
            {
                throw new ArgumentException($"类别编码不能超过 {MaxCodeLength} 个字符。", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("类别名称不能为空。");
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"类别名称不能超过 {MaxNameLength} 个字符。", nameof(name));
            }

            if (subjectAccountId == Guid.Empty)
            {
                throw new ArgumentException("必须选择资产对应的会计科目。");
            }

            if (months <= 0)
            {
                throw new ArgumentException("折旧年限（月）必须大于零。");
            }

            if (residualRate < 0m || residualRate > 100m)
            {
                throw new ArgumentException("预计净残值率必须在 0-100 之间。");
            }
        }
    }
}
