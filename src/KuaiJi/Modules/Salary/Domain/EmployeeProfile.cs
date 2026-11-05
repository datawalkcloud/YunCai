namespace KuaiJi.Modules.Salary.Domain
{
    /// <summary>
    /// 员工档案的可编辑字段集合（基本信息 + 任职受雇信息 + 扣除款项开关）。
    /// 字段已多于位置参数可读上限，故以记录承载，供 Create / Update 一次传入。
    /// 各险种缴纳基数按险种单独成行，不在本记录内（见 <see cref="EmployeeInsurance" />）。
    /// </summary>
    public sealed record EmployeeProfile
    {
        public required string Code { get; init; }

        public required string Name { get; init; }

        public EmployeeGender Gender { get; init; } = EmployeeGender.Male;

        public EmployeeIdType IdType { get; init; } = EmployeeIdType.ResidentIdCard;

        public string? IdNo { get; init; }

        public string? Dept { get; init; }

        public string? Phone { get; init; }

        public string? Email { get; init; }

        public DateOnly? BirthDate { get; init; }

        public DateOnly? HireDate { get; init; }

        /// <summary>离职日期；仅在状态为离职时生效，留空由系统按当天补。</summary>
        public DateOnly? LeaveDate { get; init; }

        public string? BankName { get; init; }

        public string? BankAccountNo { get; init; }

        public string? Post { get; init; }

        public string? Title { get; init; }

        public string? Education { get; init; }

        /// <summary>是否残疾人（个税减除情形）。</summary>
        public bool IsDisabled { get; init; }

        /// <summary>是否烈属（个税减除情形）。</summary>
        public bool IsMartyrFamily { get; init; }

        /// <summary>是否孤老（个税减除情形）。</summary>
        public bool IsSoloElder { get; init; }

        /// <summary>是否高级专家；未选择时为 null。</summary>
        public bool? IsSeniorExpert { get; init; }

        public EmployeeEmploymentType EmploymentType { get; init; } = EmployeeEmploymentType.Employee;

        /// <summary>是否扣除减除费用（个税 5000 元/月基本减除）。</summary>
        public bool DeductBasicExpense { get; init; } = true;

        /// <summary>工资始发日期（首次发放工资的所属期间起点）。</summary>
        public DateOnly? PayStartDate { get; init; }

        public Guid? SubjectAccountId { get; init; }

        /// <summary>是否缴纳五险一金；为 false 时不登记各险种缴纳基数。</summary>
        public bool PaysSocialInsurance { get; init; } = true;

        public EmployeeStatus Status { get; init; } = EmployeeStatus.Active;

        public string? Remark { get; init; }
    }
}
