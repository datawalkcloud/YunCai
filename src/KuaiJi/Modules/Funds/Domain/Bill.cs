namespace KuaiJi.Modules.Funds.Domain
{
    /// <summary>票据种类。取值与柠檬云「票据种类」一致。</summary>
    public enum BillKind
    {
        /// <summary>银行汇票。</summary>
        BankAcceptance = 1,

        /// <summary>商业汇票。</summary>
        CommercialAcceptance = 2,

        /// <summary>支票。</summary>
        Cheque = 3,

        /// <summary>其他。</summary>
        Other = 4,
    }

    /// <summary>票据状态。与柠檬云「票据状态」下拉一致，作废为内部终态。</summary>
    public enum BillStatus
    {
        /// <summary>提示收票已签收。</summary>
        Holding = 1,

        /// <summary>买断式贴现已签收。</summary>
        Discounted = 2,

        /// <summary>背书已签收。</summary>
        Endorsed = 3,

        /// <summary>已托收（历史状态，新单据不再使用）。</summary>
        Collected = 4,

        /// <summary>票据已结清。</summary>
        Cashed = 5,

        /// <summary>已作废（终态）。</summary>
        Voided = 6,

        /// <summary>背书待签收。</summary>
        Endorsing = 7,
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

        /// <summary>出票人全称。</summary>
        public string? Drawer { get; private set; }

        /// <summary>收票人全称。</summary>
        public string? Payee { get; private set; }

        /// <summary>承兑人全称。</summary>
        public string? Acceptor { get; private set; }

        /// <summary>出票人账号。</summary>
        public string? DrawerAccountNo { get; private set; }

        /// <summary>收票人账号。</summary>
        public string? PayeeAccountNo { get; private set; }

        /// <summary>承兑人账号 / 行号。</summary>
        public string? AcceptorAccountNo { get; private set; }

        /// <summary>出票人开户银行。</summary>
        public string? DrawerBank { get; private set; }

        /// <summary>收票人开户银行。</summary>
        public string? PayeeBank { get; private set; }

        /// <summary>承兑人开户银行。</summary>
        public string? AcceptorBank { get; private set; }

        /// <summary>能否转让。</summary>
        public bool Transferable { get; private set; }

        /// <summary>票据状态。</summary>
        public BillStatus Status { get; private set; }

        /// <summary>备注。</summary>
        public string? Remark { get; private set; }

        /// <summary>已生成的凭证标识（为空表示尚未生成凭证）。</summary>
        public Guid? VoucherId { get; private set; }

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
                throw new ArgumentException("票据号码不能为空。", nameof(billNo));
            }

            if (billNo.Trim().Length > MaxBillNoLength)
            {
                throw new ArgumentException($"票据号码不能超过 {MaxBillNoLength} 个字符。", nameof(billNo));
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

        /// <summary>补齐票据当事人与银行信息（新增 / 修改时调用，避免 Create 参数过长）。</summary>
        public void FillParties(
            string? payee,
            string? drawerAccountNo,
            string? payeeAccountNo,
            string? acceptorAccountNo,
            string? drawerBank,
            string? payeeBank,
            string? acceptorBank)
        {
            Payee = Normalize(payee);
            DrawerAccountNo = Normalize(drawerAccountNo);
            PayeeAccountNo = Normalize(payeeAccountNo);
            AcceptorAccountNo = Normalize(acceptorAccountNo);
            DrawerBank = Normalize(drawerBank);
            PayeeBank = Normalize(payeeBank);
            AcceptorBank = Normalize(acceptorBank);
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

        /// <summary>修改票据号码（唯一性由应用服务校验）。</summary>
        public void ChangeBillNo(string billNo)
        {
            if (string.IsNullOrWhiteSpace(billNo))
            {
                throw new ArgumentException("票据号码不能为空。", nameof(billNo));
            }

            if (billNo.Trim().Length > MaxBillNoLength)
            {
                throw new ArgumentException($"票据号码不能超过 {MaxBillNoLength} 个字符。", nameof(billNo));
            }

            BillNo = billNo.Trim();
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

        /// <summary>关联已生成的凭证；已关联时抛错，避免重复生成。</summary>
        public void AttachVoucher(Guid voucherId)
        {
            if (voucherId == Guid.Empty)
            {
                throw new ArgumentException("凭证标识无效。", nameof(voucherId));
            }

            if (VoucherId is not null)
            {
                throw new InvalidOperationException("该票据已生成凭证，请勿重复生成。");
            }

            VoucherId = voucherId;
        }

        /// <summary>是否已生成凭证。</summary>
        public bool HasVoucher => VoucherId is not null;

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
