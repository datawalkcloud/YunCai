using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Vouchers.Application
{
    /// <summary>凭证模板行 DTO。</summary>
    public sealed class VoucherTemplateLineDto
    {
        public Guid Id { get; init; }

        public int LineNumber { get; init; }

        public string Summary { get; init; } = null!;

        public Guid AccountId { get; init; }

        public string AccountCode { get; init; } = null!;

        public string AccountName { get; init; } = null!;

        /// <summary>借贷方向（借 / 贷）。</summary>
        public string DirectionText { get; init; } = null!;

        public bool IsDebit { get; init; }
    }

    /// <summary>凭证模板 DTO（含模板行）。</summary>
    public sealed class VoucherTemplateDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = null!;

        public string TypeText { get; init; } = null!;

        public IReadOnlyList<VoucherTemplateLineDto> Lines { get; init; } = [];

        public static string TypeTextOf(VoucherTemplateType type) => type switch
        {
            VoucherTemplateType.DailyExpense => "日常支出",
            VoucherTemplateType.Purchase => "采购",
            VoucherTemplateType.Sales => "销售",
            _ => "其他",
        };
    }

    /// <summary>新建 / 修改凭证模板请求。</summary>
    public sealed class SaveVoucherTemplateRequest
    {
        public string Name { get; init; } = null!;

        /// <summary>模板类型（1 日常支出 / 2 采购 / 3 销售 / 4 其他）。</summary>
        public int Type { get; init; }

        public IReadOnlyList<SaveVoucherTemplateLineRequest> Lines { get; init; } = [];
    }

    /// <summary>凭证模板行请求。</summary>
    public sealed class SaveVoucherTemplateLineRequest
    {
        public string Summary { get; init; } = null!;

        public Guid AccountId { get; init; }

        /// <summary>是否借方。</summary>
        public bool IsDebit { get; init; }
    }

    /// <summary>凭证模板导入行。</summary>
    public sealed class VoucherTemplateImportRow
    {
        public int Row { get; init; }

        public string? Name { get; init; }

        public string? TypeText { get; init; }

        public string? Summary { get; init; }

        public string? AccountCode { get; init; }

        public string? Direction { get; init; }
    }

    /// <summary>凭证模板导入结果。</summary>
    public sealed class VoucherTemplateImportResultDto
    {
        /// <summary>成功导入的模板数。</summary>
        public int Imported { get; init; }

        /// <summary>因同名跳过的模板数。</summary>
        public int Skipped { get; init; }

        public IReadOnlyList<VoucherTemplateImportFailureDto> Failures { get; init; } = [];
    }

    /// <summary>凭证模板导入失败明细。</summary>
    public sealed class VoucherTemplateImportFailureDto
    {
        public int Row { get; init; }

        public string Reason { get; init; } = null!;
    }
}
