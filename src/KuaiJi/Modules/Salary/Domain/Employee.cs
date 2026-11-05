namespace KuaiJi.Modules.Salary.Domain
{
    /// <summary>
    /// 员工档案聚合根。计提工资科目用于生成计提工资凭证；
    /// 编号同一账套内唯一。
    /// </summary>
    public class Employee
    {
        /// <summary>员工编号最大长度。</summary>
        public const int MaxCodeLength = 20;

        /// <summary>姓名最大长度。</summary>
        public const int MaxNameLength = 50;

        private Employee()
        {
        }

        /// <summary>新建员工，须归属于某一账套。</summary>
        public static Employee Create(Guid accountSetId, EmployeeProfile profile)
        {
            GuardCode(profile.Code);
            GuardName(profile.Name);
            return new Employee
            {
                Id = Guid.NewGuid(),
                AccountSetId = accountSetId,
                Code = profile.Code.Trim(),
                Name = profile.Name.Trim(),
                Gender = profile.Gender,
                IdType = profile.IdType,
                IdNo = Normalize(profile.IdNo),
                Dept = Normalize(profile.Dept),
                Phone = Normalize(profile.Phone),
                Email = Normalize(profile.Email),
                BirthDate = profile.BirthDate,
                HireDate = profile.HireDate,
                BankName = Normalize(profile.BankName),
                BankAccountNo = Normalize(profile.BankAccountNo),
                Post = Normalize(profile.Post),
                Title = Normalize(profile.Title),
                Education = Normalize(profile.Education),
                IsDisabled = profile.IsDisabled,
                IsMartyrFamily = profile.IsMartyrFamily,
                IsSoloElder = profile.IsSoloElder,
                IsSeniorExpert = profile.IsSeniorExpert,
                EmploymentType = profile.EmploymentType,
                DeductBasicExpense = profile.DeductBasicExpense,
                PayStartDate = profile.PayStartDate,
                SubjectAccountId = profile.SubjectAccountId,
                PaysSocialInsurance = profile.PaysSocialInsurance,
                Status = profile.Status,
                LeaveDate = profile.Status == EmployeeStatus.Resigned ? profile.LeaveDate ?? DateOnly.FromDateTime(DateTime.UtcNow) : null,
                Remark = Normalize(profile.Remark),
            };
        }

        /// <summary>员工标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>员工编号，同一账套内唯一。</summary>
        public string Code { get; private set; } = null!;

        /// <summary>姓名。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>性别。</summary>
        public EmployeeGender Gender { get; private set; }

        /// <summary>证件类型。</summary>
        public EmployeeIdType IdType { get; private set; }

        /// <summary>证件号码。</summary>
        public string? IdNo { get; private set; }

        /// <summary>部门。</summary>
        public string? Dept { get; private set; }

        /// <summary>手机。</summary>
        public string? Phone { get; private set; }

        /// <summary>邮箱。</summary>
        public string? Email { get; private set; }

        /// <summary>出生日期。</summary>
        public DateOnly? BirthDate { get; private set; }

        /// <summary>入职日期。</summary>
        public DateOnly? HireDate { get; private set; }

        /// <summary>离职日期（离职时填写）。</summary>
        public DateOnly? LeaveDate { get; private set; }

        /// <summary>银行名称。</summary>
        public string? BankName { get; private set; }

        /// <summary>银行账号。</summary>
        public string? BankAccountNo { get; private set; }

        /// <summary>岗位。</summary>
        public string? Post { get; private set; }

        /// <summary>职称。</summary>
        public string? Title { get; private set; }

        /// <summary>学历。</summary>
        public string? Education { get; private set; }

        /// <summary>是否残疾人。</summary>
        public bool IsDisabled { get; private set; }

        /// <summary>是否烈属。</summary>
        public bool IsMartyrFamily { get; private set; }

        /// <summary>是否孤老。</summary>
        public bool IsSoloElder { get; private set; }

        /// <summary>是否高级专家（未选择为 null）。</summary>
        public bool? IsSeniorExpert { get; private set; }

        /// <summary>任职受雇从业类型。</summary>
        public EmployeeEmploymentType EmploymentType { get; private set; }

        /// <summary>是否扣除减除费用。</summary>
        public bool DeductBasicExpense { get; private set; } = true;

        /// <summary>工资始发日期。</summary>
        public DateOnly? PayStartDate { get; private set; }

        /// <summary>计提工资科目标识（生成计提工资凭证的借方科目）。</summary>
        public Guid? SubjectAccountId { get; private set; }

        /// <summary>是否缴纳五险一金。</summary>
        public bool PaysSocialInsurance { get; private set; } = true;

        /// <summary>员工状态。</summary>
        public EmployeeStatus Status { get; private set; }

        /// <summary>备注。</summary>
        public string? Remark { get; private set; }

        /// <summary>修改员工档案。离职时记录离职日期。</summary>
        public void Update(EmployeeProfile profile)
        {
            GuardCode(profile.Code);
            GuardName(profile.Name);
            Code = profile.Code.Trim();
            Name = profile.Name.Trim();
            Gender = profile.Gender;
            IdType = profile.IdType;
            IdNo = Normalize(profile.IdNo);
            Dept = Normalize(profile.Dept);
            Phone = Normalize(profile.Phone);
            Email = Normalize(profile.Email);
            BirthDate = profile.BirthDate;
            HireDate = profile.HireDate;
            BankName = Normalize(profile.BankName);
            BankAccountNo = Normalize(profile.BankAccountNo);
            Post = Normalize(profile.Post);
            Title = Normalize(profile.Title);
            Education = Normalize(profile.Education);
            IsDisabled = profile.IsDisabled;
            IsMartyrFamily = profile.IsMartyrFamily;
            IsSoloElder = profile.IsSoloElder;
            IsSeniorExpert = profile.IsSeniorExpert;
            EmploymentType = profile.EmploymentType;
            DeductBasicExpense = profile.DeductBasicExpense;
            PayStartDate = profile.PayStartDate;
            SubjectAccountId = profile.SubjectAccountId;
            PaysSocialInsurance = profile.PaysSocialInsurance;
            Status = profile.Status;
            LeaveDate = profile.Status == EmployeeStatus.Resigned
                ? profile.LeaveDate ?? LeaveDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
                : null;
            Remark = Normalize(profile.Remark);
        }

        private static void GuardCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("员工编号不能为空。");
            }

            if (code.Trim().Length > MaxCodeLength)
            {
                throw new ArgumentException($"员工编号不能超过 {MaxCodeLength} 个字符。", nameof(code));
            }
        }

        private static void GuardName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("员工姓名不能为空。");
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"员工姓名不能超过 {MaxNameLength} 个字符。", nameof(name));
            }
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
