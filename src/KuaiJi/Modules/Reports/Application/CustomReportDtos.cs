using KuaiJi.Modules.Reports.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>自定义报表（数据透视表）的创建/更新请求。</summary>
    public sealed class SaveCustomReportRequest
    {
        /// <summary>报表编码（账套内唯一）。</summary>
        public string Code { get; init; } = null!;

        /// <summary>报表名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>取值口径：1=期末余额，2=期间发生额。</summary>
        public CustomReportValueType ValueType { get; init; }
    }

    /// <summary>自定义报表定义行（列表）。</summary>
    public sealed class CustomReportDto
    {
        public Guid Id { get; init; }

        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        /// <summary>取值口径。</summary>
        public CustomReportValueType ValueType { get; init; }

        public string CreatedBy { get; init; } = null!;

        public DateTime CreatedAt { get; init; }

        public string? ModifiedBy { get; init; }

        public DateTime? ModifiedAt { get; init; }
    }

    /// <summary>自定义报表渲染结果的一行：一个科目的 12 个月金额与合计。</summary>
    public sealed class CustomReportRenderRowDto
    {
        /// <summary>科目编码。</summary>
        public string Code { get; init; } = null!;

        /// <summary>科目名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>1-12 月各月金额。</summary>
        public IReadOnlyList<decimal> Months { get; init; } = [];

        /// <summary>全年合计（余额口径为年末余额）。</summary>
        public decimal Total { get; init; }
    }

    /// <summary>自定义报表渲染结果。</summary>
    public sealed class CustomReportRenderDto
    {
        public Guid Id { get; init; }

        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        /// <summary>取值口径。</summary>
        public CustomReportValueType ValueType { get; init; }

        /// <summary>渲染年度。</summary>
        public int Year { get; init; }

        /// <summary>科目行（按编码排序）。</summary>
        public IReadOnlyList<CustomReportRenderRowDto> Rows { get; init; } = [];
    }
}
