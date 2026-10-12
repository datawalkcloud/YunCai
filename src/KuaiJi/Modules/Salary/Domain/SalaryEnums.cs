namespace KuaiJi.Modules.Salary.Domain
{
    /// <summary>员工状态。</summary>
    public enum EmployeeStatus
    {
        /// <summary>在职。</summary>
        Active = 1,

        /// <summary>离职。</summary>
        Resigned = 2,
    }

    /// <summary>员工证件类型。</summary>
    public enum EmployeeIdType
    {
        /// <summary>居民身份证。</summary>
        ResidentIdCard = 1,

        /// <summary>护照。</summary>
        Passport = 2,

        /// <summary>其他。</summary>
        Other = 3,
    }

    /// <summary>员工性别。</summary>
    public enum EmployeeGender
    {
        /// <summary>男。</summary>
        Male = 1,

        /// <summary>女。</summary>
        Female = 2,
    }
}
