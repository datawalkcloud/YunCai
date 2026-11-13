namespace YunCai.Modules.Reports.Domain
{
    /// <summary>自定义报表取值口径。</summary>
    public enum CustomReportValueType
    {
        /// <summary>期末余额（各月末的借-贷净额）。</summary>
        EndingBalance = 1,

        /// <summary>期间发生额（各月的借-贷净发生额）。</summary>
        PeriodAmount = 2,
    }

    /// <summary>
    /// 自定义报表（数据透视表）聚合根：账套内的报表定义（编码/名称/取值口径），
    /// 渲染结果按定义在应用服务中实时计算，不持久化快照。
    /// </summary>
    public sealed class CustomReport
    {
        public const int MaxCodeLength = 50;
        public const int MaxNameLength = 100;

        public Guid Id { get; private set; }

        public Guid AccountSetId { get; private set; }

        /// <summary>报表编码（账套内唯一）。</summary>
        public string Code { get; private set; } = null!;

        /// <summary>报表名称。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>取值口径。</summary>
        public CustomReportValueType ValueType { get; private set; }

        public string CreatedBy { get; private set; } = null!;

        public DateTime CreatedAt { get; private set; }

        public string? ModifiedBy { get; private set; }

        public DateTime? ModifiedAt { get; private set; }

        private CustomReport()
        {
        }

        /// <summary>创建自定义报表定义（校验编码与名称必填）。</summary>
        public static CustomReport Create(Guid accountSetId, string code, string name, CustomReportValueType valueType, string createdBy)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ReportException("报表编码不能为空。");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ReportException("报表名称不能为空。");
            }

            code = code.Trim();
            name = name.Trim();
            if (code.Length > MaxCodeLength)
            {
                throw new ReportException($"报表编码不能超过 {MaxCodeLength} 个字符。");
            }

            if (name.Length > MaxNameLength)
            {
                throw new ReportException($"报表名称不能超过 {MaxNameLength} 个字符。");
            }

            return new CustomReport
            {
                Id = Guid.NewGuid(),
                AccountSetId = accountSetId,
                Code = code,
                Name = name,
                ValueType = valueType,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
            };
        }

        /// <summary>修改报表名称与取值口径（记录修改人与时间）。</summary>
        public void Update(string name, CustomReportValueType valueType, string modifiedBy)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ReportException("报表名称不能为空。");
            }

            name = name.Trim();
            if (name.Length > MaxNameLength)
            {
                throw new ReportException($"报表名称不能超过 {MaxNameLength} 个字符。");
            }

            Name = name;
            ValueType = valueType;
            ModifiedBy = modifiedBy;
            ModifiedAt = DateTime.UtcNow;
        }
    }
}
