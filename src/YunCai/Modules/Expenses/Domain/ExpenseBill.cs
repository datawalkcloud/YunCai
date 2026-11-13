namespace YunCai.Modules.Expenses.Domain
{
    /// <summary>
    /// 费用单据聚合根：一张费用报销 / 支付单据。
    /// 单据金额为含税总额，可抵扣税额单独记录；生成凭证时
    /// 借 费用科目（不含税额）+ 借 进项税额（可抵扣税额）/ 贷 对方科目（单据金额）。
    /// </summary>
    public class ExpenseBill
    {
        /// <summary>单据编号最大长度。</summary>
        public const int MaxBillNoLength = 50;

        /// <summary>事由 / 类型 / 收款人等文本最大长度。</summary>
        public const int MaxTextLength = 200;

        private ExpenseBill()
        {
        }

        private ExpenseBill(
            Guid id,
            Guid accountSetId,
            string billNo,
            DateOnly billDate,
            string reason,
            string expenseType,
            string? payee,
            string? settleMethod,
            decimal amount,
            decimal deductibleTax)
        {
            Id = id;
            AccountSetId = accountSetId;
            BillNo = billNo;
            BillDate = billDate;
            Reason = reason;
            ExpenseType = expenseType;
            Payee = payee;
            SettleMethod = settleMethod;
            Amount = amount;
            DeductibleTax = deductibleTax;
            Status = ExpenseBillStatus.Registered;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>单据标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>单据编号，同一账套内唯一。</summary>
        public string BillNo { get; private set; } = null!;

        /// <summary>单据日期。</summary>
        public DateOnly BillDate { get; private set; }

        /// <summary>费用事由。</summary>
        public string Reason { get; private set; } = null!;

        /// <summary>费用类型。</summary>
        public string ExpenseType { get; private set; } = null!;

        /// <summary>收款人，可为空。</summary>
        public string? Payee { get; private set; }

        /// <summary>结算方式，可为空。</summary>
        public string? SettleMethod { get; private set; }

        /// <summary>单据金额（含税），必须大于零。</summary>
        public decimal Amount { get; private set; }

        /// <summary>可抵扣税额，不得超过单据金额。</summary>
        public decimal DeductibleTax { get; private set; }

        /// <summary>单据状态。</summary>
        public ExpenseBillStatus Status { get; private set; }

        /// <summary>关联凭证标识（生成凭证后回填；凭证删除后自愈清空）。</summary>
        public Guid? VoucherId { get; private set; }

        /// <summary>登记时间。</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>登记一张费用单据；编号留空时由服务层生成。</summary>
        public static ExpenseBill Create(
            Guid accountSetId,
            string? billNo,
            DateOnly billDate,
            string reason,
            string expenseType,
            string? payee,
            string? settleMethod,
            decimal amount,
            decimal deductibleTax)
        {
            Guard(billDate, reason, expenseType, amount, deductibleTax);
            return new ExpenseBill(
                Guid.NewGuid(),
                accountSetId,
                string.IsNullOrWhiteSpace(billNo) ? string.Empty : billNo.Trim(),
                billDate,
                reason.Trim(),
                expenseType.Trim(),
                Normalize(payee),
                Normalize(settleMethod),
                amount,
                deductibleTax);
        }

        /// <summary>修改单据要素；已入账单据不可修改。</summary>
        public void Update(
            DateOnly billDate,
            string reason,
            string expenseType,
            string? payee,
            string? settleMethod,
            decimal amount,
            decimal deductibleTax)
        {
            if (Status == ExpenseBillStatus.Posted)
            {
                throw new InvalidOperationException("单据已生成凭证，请先删除凭证再修改。");
            }

            Guard(billDate, reason, expenseType, amount, deductibleTax);
            BillDate = billDate;
            Reason = reason.Trim();
            ExpenseType = expenseType.Trim();
            Payee = Normalize(payee);
            SettleMethod = Normalize(settleMethod);
            Amount = amount;
            DeductibleTax = deductibleTax;
        }

        /// <summary>补充单据编号（创建时留空由服务层生成后回填）。</summary>
        public void AssignBillNo(string billNo)
        {
            if (string.IsNullOrWhiteSpace(billNo))
            {
                throw new ArgumentException("单据编号不能为空。");
            }

            BillNo = billNo.Trim();
        }

        /// <summary>标记已生成凭证（同时置为已入账）。</summary>
        public void MarkPosted(Guid voucherId)
        {
            VoucherId = voucherId;
            Status = ExpenseBillStatus.Posted;
        }

        /// <summary>凭证被删除后解除关联，单据回到已登记状态。</summary>
        internal bool ClearStaleVoucher(IReadOnlySet<Guid> existingVoucherIds)
        {
            if (VoucherId is { } id && !existingVoucherIds.Contains(id))
            {
                VoucherId = null;
                Status = ExpenseBillStatus.Registered;
                return true;
            }

            return false;
        }

        /// <summary>按凭证关联修正入账状态（历史数据 / 并发写入兜底）。</summary>
        internal bool SyncStatus()
        {
            if (VoucherId is not null && Status != ExpenseBillStatus.Posted)
            {
                Status = ExpenseBillStatus.Posted;
                return true;
            }

            if (VoucherId is null && Status == ExpenseBillStatus.Posted)
            {
                Status = ExpenseBillStatus.Registered;
                return true;
            }

            return false;
        }

        private static void Guard(DateOnly billDate, string reason, string expenseType, decimal amount, decimal deductibleTax)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("费用事由不能为空。");
            }

            if (reason.Trim().Length > MaxTextLength)
            {
                throw new ArgumentException($"费用事由不能超过 {MaxTextLength} 个字符。", nameof(reason));
            }

            if (string.IsNullOrWhiteSpace(expenseType))
            {
                throw new ArgumentException("费用类型不能为空。");
            }

            if (amount <= 0m)
            {
                throw new ArgumentException("单据金额必须大于零。");
            }

            if (deductibleTax < 0m)
            {
                throw new ArgumentException("可抵扣税额不能为负数。");
            }

            if (deductibleTax >= amount)
            {
                throw new ArgumentException("可抵扣税额必须小于单据金额。");
            }
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
