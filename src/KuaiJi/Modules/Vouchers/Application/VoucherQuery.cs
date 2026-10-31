namespace KuaiJi.Modules.Vouchers.Application
{
    /// <summary>
    /// 凭证列表查询条件。对应柠檬云「查看凭证」页筛选面板的各个筛选项，
    /// 所有条件之间为「且」关系，未填写的条件不参与过滤。
    /// </summary>
    public sealed class VoucherQuery
    {
        /// <summary>会计期间起（含）。</summary>
        public DateOnly? StartDate { get; init; }

        /// <summary>会计期间止（含）。</summary>
        public DateOnly? EndDate { get; init; }

        /// <summary>凭证字，如「记」；为空表示全部。</summary>
        public string? Word { get; init; }

        /// <summary>制单人，精确匹配；为空表示全部。</summary>
        public string? Preparer { get; init; }

        /// <summary>是否审核：null 全部，true 已审核（含已过账），false 未审核。</summary>
        public bool? Reviewed { get; init; }

        /// <summary>辅助核算：null 全部，true 有辅助核算，false 无辅助核算。</summary>
        public bool? HasAssists { get; init; }

        /// <summary>摘要包含的关键字。</summary>
        public string? SummaryContains { get; init; }

        /// <summary>备注包含的关键字。</summary>
        public string? RemarkContains { get; init; }

        /// <summary>科目标识；命中该科目及其所有下级科目。</summary>
        public Guid? AccountId { get; init; }

        /// <summary>借贷方向：null 全部，true 借方，false 贷方。命中该方向上有金额的分录。</summary>
        public bool? Direction { get; init; }

        /// <summary>金额下限（按分录借方或贷方的绝对值比较）。</summary>
        public decimal? AmountFrom { get; init; }

        /// <summary>金额上限。</summary>
        public decimal? AmountTo { get; init; }

        /// <summary>凭证号下限。</summary>
        public int? NumberFrom { get; init; }

        /// <summary>凭证号上限。</summary>
        public int? NumberTo { get; init; }

        /// <summary>排序依据：number 按凭证号，date 按凭证日期（默认）。</summary>
        public string? SortBy { get; init; }

        /// <summary>是否降序；默认按日期升序。</summary>
        public bool SortDesc { get; init; }

        /// <summary>是否查询回收站中的凭证。</summary>
        public bool Deleted { get; init; }
    }
}
