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

        private Employee(
            Guid id,
            Guid accountSetId,
            string code,
            string name,
            EmployeeGender gender,
            EmployeeIdType idType,
            string? idNo,
            string? dept,
            string? phone,
            string? email,
            DateOnly? birthDate,
            DateOnly? hireDate,
            string? bankName,
            string? bankAccountNo,
            string? post,
            string? title,
            Guid? subjectAccountId,
            EmployeeStatus status,
            string? remark)
        {
            Id = id;
            AccountSetId = accountSetId;
            Code = code;
            Name = name;
            Gender = gender;
            IdType = idType;
            IdNo = idNo;
            Dept = dept;
            Phone = phone;
            Email = email;
            BirthDate = birthDate;
            HireDate = hireDate;
            BankName = bankName;
            BankAccountNo = bankAccountNo;
            Post = post;
            Title = title;
            SubjectAccountId = subjectAccountId;
            Status = status;
            Remark = remark;
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

        /// <summary>计提工资科目标识（生成计提工资凭证的借方科目）。</summary>
        public Guid? SubjectAccountId { get; private set; }

        /// <summary>员工状态。</summary>
        public EmployeeStatus Status { get; private set; }

        /// <summary>备注。</summary>
        public string? Remark { get; private set; }

        /// <summary>新建员工，须归属于某一账套。</summary>
        public static Employee Create(
            Guid accountSetId,
            string code,
            string name,
            EmployeeGender gender,
            EmployeeIdType idType,
            string? idNo,
            string? dept,
            string? phone,
            string? email,
            DateOnly? birthDate,
            DateOnly? hireDate,
            string? bankName,
            string? bankAccountNo,
            string? post,
            string? title,
            Guid? subjectAccountId,
            EmployeeStatus status,
            string? remark)
        {
            GuardCode(code);
            GuardName(name);
            return new Employee(
                Guid.NewGuid(),
                accountSetId,
                code.Trim(),
                name.Trim(),
                gender,
                idType,
                Normalize(idNo),
                Normalize(dept),
                Normalize(phone),
                Normalize(email),
                birthDate,
                hireDate,
                Normalize(bankName),
                Normalize(bankAccountNo),
                Normalize(post),
                Normalize(title),
                subjectAccountId,
                status,
                Normalize(remark));
        }

        /// <summary>修改员工档案。离职时记录离职日期。</summary>
        public void Update(
            string code,
            string name,
            EmployeeGender gender,
            EmployeeIdType idType,
            string? idNo,
            string? dept,
            string? phone,
            string? email,
            DateOnly? birthDate,
            DateOnly? hireDate,
            string? bankName,
            string? bankAccountNo,
            string? post,
            string? title,
            Guid? subjectAccountId,
            EmployeeStatus status,
            string? remark)
        {
            GuardCode(code);
            GuardName(name);
            Code = code.Trim();
            Name = name.Trim();
            Gender = gender;
            IdType = idType;
            IdNo = Normalize(idNo);
            Dept = Normalize(dept);
            Phone = Normalize(phone);
            Email = Normalize(email);
            BirthDate = birthDate;
            HireDate = hireDate;
            BankName = Normalize(bankName);
            BankAccountNo = Normalize(bankAccountNo);
            Post = Normalize(post);
            Title = Normalize(title);
            SubjectAccountId = subjectAccountId;
            Status = status;
            LeaveDate = status == EmployeeStatus.Resigned ? LeaveDate ?? DateOnly.FromDateTime(DateTime.UtcNow) : null;
            Remark = Normalize(remark);
        }

        private static void GuardCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("员工编号不能为空。", nameof(code));
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
                throw new ArgumentException("员工姓名不能为空。", nameof(name));
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
