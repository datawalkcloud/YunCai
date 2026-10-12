namespace KuaiJi.Modules.Funds.Domain
{
    /// <summary>票据种类。</summary>
    public enum BillKind
    {
        /// <summary>银行承兑汇票。</summary>
        BankAcceptance = 1,

        /// <summary>商业承兑汇票。</summary>
        CommercialAcceptance = 2,

        /// <summary>支票。</summary>
        Cheque = 3,

        /// <summary>其他。</summary>
        Other = 4,
    }

    /// <summary>票据状态。</summary>
    public enum BillStatus
    {
        /// <summary>持有。</summary>
        Holding = 1,

        /// <summary>已贴现。</summary>
        Discounted = 2,

        /// <summary>已背书。</summary>
        Endorsed = 3,

        /// <summary>已托收。</summary>
        Collected = 4,

        /// <summary>已兑现。</summary>
        Cashed = 5,

        /// <summary>已作废。</summary>
        Voided = 6,
    }

    /// <summary>
    /// 票据聚合根：应收 / 应付票据台账。记录出票、承兑、到期与流转状态，
    /// 供票据到期提醒与生成凭证使用。
    /// </summary>
    public class Bill
    {
        /// <summary>票据号最大长度。</summary>
        public const int MaxBillNoLength = 50;

        private Bill()
        {
        }

        private Bill(
            Guid id,
            Guid accountSetId,
            string category,
            DateOnly issueDate,
            string billNo,
            decimal amount,
            BillKind kind,
            DateOnly? dueDate,
            string? drawer,
            string? acceptor,
            bool transferable,
            string? remark)
        {
            Id = id;
            AccountSetId = accountSetId;
            Category = category;
            IssueDate = issueDate;
            BillNo = billNo;
            Amount = amount;
            Kind = kind;
            DueDate = dueDate;
            Drawer = drawer;
            Acceptor = acceptor;
            Transferable = transferable;
            Remark = remark;
            Status = BillStatus.Holding;
        }

        /// <summary>票据标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>收支类别（票据对应的收支类别文本）。</summary>
        public string Category { get; private set; } = null!;

        /// <summary>出票日。</summary>
        public DateOnly IssueDate { get; private set; }

        /// <summary>票据号，同一账套内唯一。</summary>
        public string BillNo { get; private set; } = null!;

        /// <summary>票据金额，必须大于零。</summary>
        public decimal Amount { get; private set; }

        /// <summary>票据种类。</summary>
        public BillKind Kind { get; private set; }

        /// <summary>汇票到期日（支票可空）；不得早于出票日。</summary>
        public DateOnly? DueDate { get; private set; }

        /// <summary>出票人。</summary>
        public string? Drawer { get; private set; }

        /// <summary>承兑人。</summary>
        public string? Acceptor { get; private set; }

        /// <summary>能否转让。</summary>
        public bool Transferable { get; private set; }

        /// <summary>票据状态。</summary>
        public BillStatus Status { get; private set; }

        /// <summary>备注。</summary>
        public string? Remark { get; private set; }

        /// <summary>新建票据，须归属于某一账套。</summary>
        public static Bill Create(
            Guid accountSetId,
            string category,
            DateOnly issueDate,
            string billNo,
            decimal amount,
            BillKind kind,
            DateOnly? dueDate,
            string? drawer,
            string? acceptor,
            bool transferable,
            string? remark)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。", nameof(accountSetId));
            }

            GuardAmount(amount);
            GuardDates(issueDate, dueDate);
            if (string.IsNullOrWhiteSpace(billNo))
            {
                throw new ArgumentException("票据号不能为空。", nameof(billNo));
            }

            if (billNo.Trim().Length > MaxBillNoLength)
            {
                throw new ArgumentException($"票据号不能超过 {MaxBillNoLength} 个字符。", nameof(billNo));
            }

            return new Bill(
                Guid.NewGuid(),
                accountSetId,
                Normalize(category) ?? string.Empty,
                issueDate,
                billNo.Trim(),
                amount,
                kind,
                dueDate,
                Normalize(drawer),
                Normalize(acceptor),
                transferable,
                Normalize(remark));
        }

        /// <summary>修改票据要素（状态走 <see cref="ChangeStatus"/>）。</summary>
        public void Update(
            string category,
            DateOnly issueDate,
            decimal amount,
            BillKind kind,
            DateOnly? dueDate,
            string? drawer,
            string? acceptor,
            bool transferable,
            string? remark)
        {
            GuardAmount(amount);
            GuardDates(issueDate, dueDate);
            Category = Normalize(category) ?? string.Empty;
            IssueDate = issueDate;
            Amount = amount;
            Kind = kind;
            DueDate = dueDate;
            Drawer = Normalize(drawer);
            Acceptor = Normalize(acceptor);
            Transferable = transferable;
            Remark = Normalize(remark);
        }

        /// <summary>流转票据状态。已作废为终态，不可再变更。</summary>
        public void ChangeStatus(BillStatus status)
        {
            if (!Enum.IsDefined(status))
            {
                throw new ArgumentException("票据状态无效。", nameof(status));
            }

            if (Status == BillStatus.Voided)
            {
                throw new InvalidOperationException("票据已作废，不能再变更状态。");
            }

            Status = status;
        }

        private static void GuardAmount(decimal amount)
        {
            if (amount <= 0m)
            {
                throw new ArgumentException("票据金额必须大于零。", nameof(amount));
            }
        }

        private static void GuardDates(DateOnly issueDate, DateOnly? dueDate)
        {
            if (dueDate is { } due && due < issueDate)
            {
                throw new ArgumentException("汇票到期日不得早于出票日。", nameof(dueDate));
            }
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
