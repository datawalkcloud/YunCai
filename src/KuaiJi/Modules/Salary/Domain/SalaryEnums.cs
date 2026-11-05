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

    /// <summary>任职受雇从业类型（个税人员报送口径）。</summary>
    public enum EmployeeEmploymentType
    {
        /// <summary>雇员。</summary>
        Employee = 1,

        /// <summary>保险营销员。</summary>
        InsuranceAgent = 2,

        /// <summary>证券经纪人。</summary>
        SecuritiesBroker = 3,

        /// <summary>劳务派遣。</summary>
        LaborDispatch = 4,

        /// <summary>其他。</summary>
        Other = 5,
    }
}
