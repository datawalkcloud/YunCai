using YunCai.Modules.Assist.Domain;

namespace YunCai.Modules.Ledgers.Application
{
    /// <summary>
    /// 账页导出/打印的查询条件（从 query string 绑定）。参数名与九张账页的 GET 接口一致，
    /// 前端 <c>LedgerExportButton</c> 的拼串与后端一处对齐；<see cref="ToOptions"/> 负责补默认值。
    /// </summary>
    public sealed class LedgerPageQuery
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? AccountFrom { get; set; }
        public string? AccountTo { get; set; }
        public int? MaxLevel { get; set; }
        public int? MinLevel { get; set; }
        public string? SummaryKeyword { get; set; }
        public LedgerSortBy? SortBy { get; set; }
        public bool? ShowAssist { get; set; }
        public bool? ShowCounterAccount { get; set; }
        public bool? ShowOnlyLeaf { get; set; }
        public bool? HideZeroBalance { get; set; }
        public bool? HideNoActivity { get; set; }
        public bool? HideTotalsNoActivity { get; set; }
        public bool? IncludeIdleAccounts { get; set; }
        public AssistType? AssistType { get; set; }
        public AssistType? SecondaryAssistType { get; set; }
        public Guid? AssistItemId { get; set; }
        public Guid? AccountId { get; set; }

        /// <summary>转成账簿查询条件；省略项取页面默认口径（显示对方科目、只列明细科目）。</summary>
        public LedgerQueryOptions ToOptions() => new()
        {
            StartDate = StartDate,
            EndDate = EndDate,
            AccountFrom = AccountFrom,
            AccountTo = AccountTo,
            MaxLevel = MaxLevel,
            MinLevel = MinLevel,
            SummaryKeyword = SummaryKeyword,
            SortBy = SortBy ?? LedgerSortBy.VoucherNumber,
            ShowAssist = ShowAssist ?? false,
            ShowCounterAccount = ShowCounterAccount ?? true,
            ShowOnlyLeaf = ShowOnlyLeaf ?? true,
            HideZeroBalance = HideZeroBalance ?? false,
            HideNoActivity = HideNoActivity ?? false,
            HideTotalsNoActivity = HideTotalsNoActivity ?? false,
            IncludeIdleAccounts = IncludeIdleAccounts ?? false,
            AssistType = AssistType,
            SecondaryAssistType = SecondaryAssistType,
            AssistItemId = AssistItemId,
            SingleAccountId = AccountId,
        };
    }
}
