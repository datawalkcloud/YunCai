namespace KuaiJi.Modules.Funds.Domain
{
    /// <summary>
    /// 日记账分录：账簿中的一行流水（一笔收入或支出）。
    /// 序号（<see cref="Sequence"/>）与余额（<see cref="Balance"/>）由所属 <see cref="Journal"/> 统一重算，
    /// 不可单独设置。分录的创建与修改均通过聚合根进行。
    /// </summary>
    public class JournalEntry
    {
        // EF Core 物化实体所需的无参构造函数。
        private JournalEntry()
        {
        }

        internal JournalEntry(
            Guid id,
            Guid journalId,
            DateOnly date,
            string summary,
            string? category,
            string? counterparty,
            string? project,
            string? department,
            decimal income,
            decimal expense,
            Guid? voucherId)
        {
            Id = id;
            JournalId = journalId;
            Date = date;
            Summary = summary;
            Category = category;
            Counterparty = counterparty;
            Project = project;
            Department = department;
            Income = income;
            Expense = expense;
            VoucherId = voucherId;
        }

        /// <summary>分录标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属日记账标识。</summary>
        public Guid JournalId { get; private set; }

        /// <summary>日记账序号（同一账簿内按日期顺序自 1 递增，由账簿重算）。</summary>
        public int Sequence { get; private set; }

        /// <summary>业务日期。</summary>
        public DateOnly Date { get; private set; }

        /// <summary>摘要。</summary>
        public string Summary { get; private set; } = null!;

        /// <summary>收支类别，可空。</summary>
        public string? Category { get; private set; }

        /// <summary>往来单位，可空。</summary>
        public string? Counterparty { get; private set; }

        /// <summary>项目，可空。</summary>
        public string? Project { get; private set; }

        /// <summary>部门，可空。</summary>
        public string? Department { get; private set; }

        /// <summary>收入金额（借方）。</summary>
        public decimal Income { get; private set; }

        /// <summary>支出金额（贷方）。</summary>
        public decimal Expense { get; private set; }

        /// <summary>登记后的账面余额（由账簿按顺序累计，由账簿重算）。</summary>
        public decimal Balance { get; private set; }

        /// <summary>关联凭证标识，可空（由凭证过账生成的分录会带上来源凭证）。</summary>
        public Guid? VoucherId { get; private set; }

        /// <summary>由所属账簿设置序号与余额。</summary>
        internal void SetComputed(int sequence, decimal balance)
        {
            Sequence = sequence;
            Balance = balance;
        }

        /// <summary>
        /// 清除失效的关联凭证：凭证被删除后流水仍残留 VoucherId，会导致账页显示空白、
        /// 且该流水因「已生成凭证」被永久排除、无法再次生成凭证。
        /// 传入仍存在的凭证集合，剔除指向已不存在凭证的引用。返回是否发生了变更。
        /// </summary>
        internal bool ClearStaleVoucher(IReadOnlySet<Guid> existingVoucherIds)
        {
            if (VoucherId is not { } vid || existingVoucherIds.Contains(vid))
            {
                return false;
            }

            VoucherId = null;
            return true;
        }

        /// <summary>
        /// 批量操作时的局部改写：只改传入的字段（null 表示保持原值），
        /// <paramref name="clearVoucher"/> 为真时清除关联凭证。金额与日期不动，因此无需重算余额。
        /// </summary>
        internal void Patch(string? summary, string? category, string? counterparty, string? project, string? department, bool clearVoucher, Guid? voucherId = null)
        {
            if (summary is not null)
            {
                Summary = summary.Trim();
            }

            if (category is not null)
            {
                Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            }

            if (counterparty is not null)
            {
                Counterparty = string.IsNullOrWhiteSpace(counterparty) ? null : counterparty.Trim();
            }

            if (project is not null)
            {
                Project = string.IsNullOrWhiteSpace(project) ? null : project.Trim();
            }

            if (department is not null)
            {
                Department = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            }

            if (clearVoucher)
            {
                VoucherId = null;
            }
            else if (voucherId is { } vid)
            {
                VoucherId = vid;
            }
        }

        /// <summary>由所属账簿更新分录内容（更新后需由账簿重算序号与余额）。</summary>
        internal void Update(
            DateOnly date,
            string summary,
            string? category,
            string? counterparty,
            string? project,
            string? department,
            decimal income,
            decimal expense,
            Guid? voucherId)
        {
            Date = date;
            Summary = summary;
            Category = category;
            Counterparty = counterparty;
            Project = project;
            Department = department;
            Income = income;
            Expense = expense;
            VoucherId = voucherId;
        }
    }
}
