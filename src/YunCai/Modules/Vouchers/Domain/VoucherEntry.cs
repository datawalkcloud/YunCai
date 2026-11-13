using YunCai.Modules.Assist.Domain;

namespace YunCai.Modules.Vouchers.Domain
{
    /// <summary>
    /// 记账凭证分录（会计分录）：凭证中的一行，登记某一科目的借方或贷方发生额。
    /// 一条分录只能填写借方或贷方其一。行号由所属 <see cref="Voucher"/> 统一维护，
    /// 分录的创建与修改均通过聚合根进行。
    /// </summary>
    public class VoucherEntry
    {
        private readonly List<VoucherEntryAssist> _assists = new();

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

        /// <summary>该分录登记的辅助核算取值（未启用辅助核算时为空集合）。</summary>
        public IReadOnlyList<VoucherEntryAssist> Assists => _assists;

        /// <summary>由所属凭证设置行号。</summary>
        internal void SetLineNumber(int lineNumber) => LineNumber = lineNumber;

        /// <summary>
        /// 由所属凭证更新分录内容。科目变更会使原有辅助核算维度失效，故一并清空取值，
        /// 由调用方随后通过 <see cref="SetAssists"/> 重新登记。
        /// </summary>
        internal void Update(string summary, Guid accountId, decimal debit, decimal credit)
        {
            if (accountId != AccountId)
            {
                _assists.Clear();
            }

            Summary = summary;
            AccountId = accountId;
            Debit = debit;
            Credit = credit;
        }

        /// <summary>由所属凭证整体覆盖辅助核算取值（同一「类别 + 档案」只保留一条）。</summary>
        internal void SetAssists(IReadOnlyCollection<AssistRef> references)
        {
            _assists.Clear();
            foreach (var reference in references.Distinct())
            {
                _assists.Add(VoucherEntryAssist.Create(Id, AccountId, reference));
            }
        }
    }
}
