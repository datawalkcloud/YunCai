namespace KuaiJi.Domain.Vouchers
{
    /// <summary>
    /// 记账凭证分录（会计分录）：凭证中的一行，登记某一科目的借方或贷方发生额。
    /// 一条分录只能填写借方或贷方其一。行号由所属 <see cref="Voucher"/> 统一维护，
    /// 分录的创建与修改均通过聚合根进行。
    /// </summary>
    public class VoucherEntry
    {
        // EF Core 物化实体所需的无参构造函数。
        private VoucherEntry()
        {
        }

        internal VoucherEntry(
            Guid id,
            Guid voucherId,
            string summary,
            Guid accountId,
            decimal debit,
            decimal credit)
        {
            Id = id;
            VoucherId = voucherId;
            Summary = summary;
            AccountId = accountId;
            Debit = debit;
            Credit = credit;
        }

        /// <summary>分录标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属凭证标识。</summary>
        public Guid VoucherId { get; private set; }

        /// <summary>凭证内的分录行号（自 1 递增，由凭证重排）。</summary>
        public int LineNumber { get; private set; }

        /// <summary>摘要。</summary>
        public string Summary { get; private set; } = null!;

        /// <summary>会计科目标识（应为末级科目）。</summary>
        public Guid AccountId { get; private set; }

        /// <summary>借方金额。</summary>
        public decimal Debit { get; private set; }

        /// <summary>贷方金额。</summary>
        public decimal Credit { get; private set; }

        /// <summary>由所属凭证设置行号。</summary>
        internal void SetLineNumber(int lineNumber) => LineNumber = lineNumber;

        /// <summary>由所属凭证更新分录内容。</summary>
        internal void Update(string summary, Guid accountId, decimal debit, decimal credit)
        {
            Summary = summary;
            AccountId = accountId;
            Debit = debit;
            Credit = credit;
        }
    }
}
