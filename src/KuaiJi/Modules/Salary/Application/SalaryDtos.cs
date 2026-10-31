using KuaiJi.Modules.Salary.Domain;

namespace KuaiJi.Modules.Salary.Application
{
    /// <summary>员工 DTO。</summary>
    public sealed class EmployeeDto
    {
        public Guid Id { get; init; }

        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        public string GenderText { get; init; } = null!;

        public string Dept { get; init; } = null!;

        public string? Phone { get; init; }

        public string? Email { get; init; }

        public string IdTypeText { get; init; } = null!;

        public string? IdNo { get; init; }

        public Guid? SubjectAccountId { get; init; }

        public string SubjectText { get; init; } = null!;

        public string StatusText { get; init; } = null!;

        public DateOnly? HireDate { get; init; }

        public string? Remark { get; init; }

        public static string GenderTextOf(EmployeeGender gender) => gender == EmployeeGender.Male ? "男" : "女";

        public static string IdTypeTextOf(EmployeeIdType type) => type switch
        {
            EmployeeIdType.ResidentIdCard => "居民身份证",
            EmployeeIdType.Passport => "护照",
            _ => "其他",
        };

        public static string StatusTextOf(EmployeeStatus status) => status == EmployeeStatus.Active ? "在职" : "离职";
    }

    /// <summary>新建 / 修改员工请求。</summary>
    public sealed class SaveEmployeeRequest
    {
        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        /// <summary>性别（1 男 / 2 女）。</summary>
        public int Gender { get; init; }

        /// <summary>证件类型（1 居民身份证 / 2 护照 / 3 其他）。</summary>
        public int IdType { get; init; }

        public string? IdNo { get; init; }

        public string? Dept { get; init; }

        public string? Phone { get; init; }

        public string? Email { get; init; }

        public DateOnly? BirthDate { get; init; }

        public DateOnly? HireDate { get; init; }

        public string? BankName { get; init; }

        public string? BankAccountNo { get; init; }

        public string? Post { get; init; }

        public string? Title { get; init; }

        public Guid? SubjectAccountId { get; init; }

        /// <summary>员工状态（1 在职 / 2 离职）。</summary>
        public int Status { get; init; }

        public string? Remark { get; init; }
    }

    /// <summary>五险一金配置 DTO。</summary>
    public sealed class InsuranceItemDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = null!;

        /// <summary>缴纳方式展示文本（固定口径）。</summary>
        public string ModeText => "按比例缴纳";

        public decimal CompanyRate { get; init; }

        public decimal? PersonalRate { get; init; }

        public static InsuranceItemDto FromDomain(InsuranceItem item) => new()
        {
            Id = item.Id,
            Name = item.Name,
            CompanyRate = item.CompanyRate,
            PersonalRate = item.PersonalRate,
        };
    }

    /// <summary>新建 / 修改五险一金请求。</summary>
    public sealed class SaveInsuranceRequest
    {
        public string Name { get; init; } = null!;

        public decimal CompanyRate { get; init; }

        public decimal? PersonalRate { get; init; }
    }

    /// <summary>月度工资单 DTO。</summary>
    public sealed class SalaryBillDto
    {
        public Guid Id { get; init; }

        public Guid EmployeeId { get; init; }

        public string EmployeeCode { get; init; } = null!;

        public string EmployeeName { get; init; } = null!;

        public string Dept { get; init; } = null!;

        public decimal Gross { get; init; }

        public decimal Deductible { get; init; }

        public decimal Payable { get; init; }

        public decimal PersonalDeduction { get; init; }

        public decimal TaxThreshold { get; init; }

        public decimal Tax { get; init; }

        public decimal Adjust { get; init; }

        public decimal Net { get; init; }

        public decimal CompanyPart { get; init; }

        public decimal Cost { get; init; }

        /// <summary>计提 / 发放凭证文本（如 记-3）。</summary>
        public string? AccruedVoucherText { get; init; }

        public string? PaidVoucherText { get; init; }
    }

    /// <summary>保存工资单请求（同期间同员工唯一）。</summary>
    public sealed class SaveSalaryBillRequest
    {
        public int Period { get; init; }

        public Guid EmployeeId { get; init; }

        public decimal Gross { get; init; }

        public decimal Deductible { get; init; }

        public decimal PersonalDeduction { get; init; }

        public decimal TaxThreshold { get; init; } = 5000m;

        public decimal Tax { get; init; }

        public decimal Adjust { get; init; }

        public decimal CompanyPart { get; init; }
    }

    /// <summary>工资统计表行 DTO（按部门汇总）。</summary>
    public sealed class SalarySummaryRowDto
    {
        public string Dept { get; init; } = null!;

        public int EmployeeCount { get; init; }

        public decimal Payable { get; init; }

        public decimal PersonalDeduction { get; init; }

        public decimal CompanyPart { get; init; }

        public decimal Net { get; init; }

        public decimal Cost { get; init; }
    }

    /// <summary>工资凭证科目映射 DTO（含科目显示文本，便于前端直接渲染）。</summary>
    public sealed class SalaryVoucherSettingDto
    {
        public Guid? PayrollAccountId { get; init; }

        public Guid? TaxAccountId { get; init; }

        public Guid? InsuranceAccountId { get; init; }

        public Guid? BankAccountId { get; init; }

        /// <summary>应付职工薪酬科目文本「2211 应付职工薪酬」；未配置时为自动推断结果。</summary>
        public string? PayrollAccountText { get; init; }

        public string? TaxAccountText { get; init; }

        public string? InsuranceAccountText { get; init; }

        public string? BankAccountText { get; init; }

        /// <summary>自动推断项是否命中（未命中时界面提示需要手工指定）。</summary>
        public bool PayrollResolved { get; init; }

        public bool BankResolved { get; init; }
    }

    /// <summary>保存工资凭证科目映射请求；字段留空表示回落到自动推断。</summary>
    public sealed class SaveSalaryVoucherSettingRequest
    {
        public Guid? PayrollAccountId { get; init; }

        public Guid? TaxAccountId { get; init; }

        public Guid? InsuranceAccountId { get; init; }

        public Guid? BankAccountId { get; init; }
    }

    /// <summary>导入失败明细（行号从 1 起，含表头）。</summary>
    public sealed class SalaryImportFailureDto
    {
        public int Row { get; init; }

        public string Reason { get; init; } = null!;
    }

    /// <summary>员工导入结果。</summary>
    public sealed class EmployeeImportResultDto
    {
        public int Imported { get; init; }

        public int Updated { get; init; }

        public int Skipped { get; init; }

        public IReadOnlyList<SalaryImportFailureDto> Failures { get; init; } = [];
    }

    /// <summary>工资表导入结果。</summary>
    public sealed class SalaryBillImportResultDto
    {
        public int Imported { get; init; }

        public int Updated { get; init; }

        public IReadOnlyList<SalaryImportFailureDto> Failures { get; init; } = [];
    }

    /// <summary>批量操作结果（员工 / 工资单通用）。</summary>
    public sealed class SalaryBulkResultDto
    {
        public int Affected { get; init; }

        public int Skipped { get; init; }

        public IReadOnlyList<string> Messages { get; init; } = [];
    }

    /// <summary>按标识批量操作的请求。</summary>
    public sealed class SalaryBulkIdsRequest
    {
        public IReadOnlyList<Guid>? Ids { get; init; }
    }

    /// <summary>员工导入行（由控制器解析 .xlsx 得到，服务层负责校验与落库）。</summary>
    public sealed class EmployeeImportRow
    {
        public int Row { get; init; }

        public string? Code { get; init; }

        public string? Name { get; init; }

        public string? GenderText { get; init; }

        public string? IdTypeText { get; init; }

        public string? IdNo { get; init; }

        public string? Dept { get; init; }

        public string? Phone { get; init; }

        public string? Email { get; init; }

        public string? HireDate { get; init; }

        public string? Post { get; init; }

        public string? Title { get; init; }

        /// <summary>计提工资科目编码（如 5602001）；留空表示不设置。</summary>
        public string? SubjectAccountCode { get; init; }

        public string? StatusText { get; init; }

        public string? Remark { get; init; }
    }

    /// <summary>工资表导入行。</summary>
    public sealed class SalaryBillImportRow
    {
        public int Row { get; init; }

        public string? EmployeeCode { get; init; }

        public string? EmployeeName { get; init; }

        public string? Gross { get; init; }

        public string? Deductible { get; init; }

        public string? PersonalDeduction { get; init; }

        public string? TaxThreshold { get; init; }

        public string? Tax { get; init; }

        public string? Adjust { get; init; }

        public string? CompanyPart { get; init; }
    }
}
