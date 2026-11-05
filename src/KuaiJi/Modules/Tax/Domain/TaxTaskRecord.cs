namespace KuaiJi.Modules.Tax.Domain
{
    /// <summary>任务状态：后台任务先落「进行中」，办完再回填成成功或失败。</summary>
    public enum TaxTaskStatus
    {
        /// <summary>成功</summary>
        Success = 0,

        /// <summary>失败</summary>
        Failed = 1,

        /// <summary>
        /// 进行中。排在最后是为了沿用已入库的 0/1 取值——status 列存的是整数，
        /// 给这个状态换号会让历史行全部串位。
        /// </summary>
        Running = 2,
    }

    /// <summary>
    /// 任务中心的一条记录：服务端每向税局办一件事（登录、取税费种、取申报记录、
    /// 同步个税人员与专项附加扣除）就先落一行「进行中」，办完回填成功或失败，
    /// 失败时把税局回的话写进「解决建议」。个税取数跑在请求之外，靠这一行让人看到进展，
    /// 所以它是可回填的台账，不是审计日志——只动状态、建议与结束时间三列。
    /// </summary>
    public class TaxTaskRecord
    {
        /// <summary>任务说明的最大长度。</summary>
        public const int MaxTitleLength = 100;

        /// <summary>解决建议的最大长度，税局的报错文案可能很长。</summary>
        public const int MaxSuggestionLength = 500;

        private TaxTaskRecord()
        {
        }

        public TaxTaskRecord(
            Guid accountSetId,
            string userId,
            string userName,
            int period,
            string title,
            TaxTaskStatus status,
            string? suggestion,
            DateTimeOffset submittedAt,
            DateTimeOffset finishedAt,
            Guid? id = null)
        {
            Id = id ?? Guid.NewGuid();
            AccountSetId = accountSetId;
            UserId = userId?.Trim() ?? string.Empty;
            UserName = string.IsNullOrWhiteSpace(userName) ? "未知用户" : userName.Trim();
            Period = period;
            Title = Clip(title, MaxTitleLength);
            Status = status;
            Suggestion = status == TaxTaskStatus.Failed ? Clip(suggestion, MaxSuggestionLength) : null;
            SubmittedAt = submittedAt;
            FinishedAt = finishedAt;
        }

        /// <summary>任务标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>发起人标识。</summary>
        public string UserId { get; private set; } = null!;

        /// <summary>发起人显示名，即页面上的「操作人」。</summary>
        public string UserName { get; private set; } = null!;

        /// <summary>税款所属期（yyyyMM），取任务提交时所在的自然月。</summary>
        public int Period { get; private set; }

        /// <summary>任务说明，如「同步自然人电子税局人员信息」。</summary>
        public string Title { get; private set; } = null!;

        public TaxTaskStatus Status { get; private set; }

        /// <summary>失败时给用户的下一步建议；成功为空。</summary>
        public string? Suggestion { get; private set; }

        /// <summary>任务提交时间。</summary>
        public DateTimeOffset SubmittedAt { get; private set; }

        /// <summary>任务结束时间；进行中的任务先等于提交时间，回填时才写真实时间。</summary>
        public DateTimeOffset FinishedAt { get; private set; }

        /// <summary>任务办完（或办砸）时回填状态、结束时间与解决建议。</summary>
        public void Complete(bool success, string? suggestion, DateTimeOffset finishedAt)
        {
            Status = success ? TaxTaskStatus.Success : TaxTaskStatus.Failed;
            Suggestion = success ? null : Clip(suggestion, MaxSuggestionLength);
            FinishedAt = finishedAt;
        }

        private static string Clip(string? text, int max)
        {
            var value = (text ?? string.Empty).Trim();
            return value.Length <= max ? value : value[..max];
        }
    }
}
