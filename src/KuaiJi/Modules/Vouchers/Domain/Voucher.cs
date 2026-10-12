using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Vouchers.Domain
{
    /// <summary>
    /// 记账凭证聚合根。由凭证头（字/号、日期、附单据数）与若干会计分录组成。
    /// 遵循「制单 → 审核 → 过账」工作流：草稿状态可增删改分录，审核或过账后锁定；
    /// 审核与过账均要求借贷平衡且至少有一条分录。
    /// </summary>
    public class Voucher
    {
        private readonly List<VoucherEntry> _entries = new();

        // EF Core 物化实体所需的无参构造函数。
        private Voucher()
        {
        }

        private Voucher(Guid id, Guid accountSetId, string word, int number, DateOnly date, string preparer, int attachmentCount)
        {
            Id = id;
            AccountSetId = accountSetId;
            Word = word;
            Number = number;
            Date = date;
            Preparer = preparer;
            AttachmentCount = attachmentCount;
            Status = VoucherStatus.Draft;
        }

        /// <summary>凭证标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。凭证不能脱离账套独立存在。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>凭证字，如“记”。</summary>
        public string Word { get; private set; } = null!;

        /// <summary>凭证号（同一凭证字下顺序编号）。</summary>
        public int Number { get; private set; }

        /// <summary>凭证日期。</summary>
        public DateOnly Date { get; private set; }

        /// <summary>所附原始单据张数。</summary>
        public int AttachmentCount { get; private set; }

        /// <summary>凭证状态。</summary>
        public VoucherStatus Status { get; private set; }

        /// <summary>制单人。</summary>
        public string Preparer { get; private set; } = null!;

        /// <summary>审核人，未审核时为空。</summary>
        public string? Reviewer { get; private set; }

        /// <summary>凭证分录集合（按行号顺序）。</summary>
        public IReadOnlyList<VoucherEntry> Entries => _entries;

        /// <summary>借方合计。</summary>
        public decimal TotalDebit => _entries.Sum(e => e.Debit);

        /// <summary>贷方合计。</summary>
        public decimal TotalCredit => _entries.Sum(e => e.Credit);

        /// <summary>借贷是否平衡（且至少有一条分录）。</summary>
        public bool IsBalanced => _entries.Count > 0 && TotalDebit == TotalCredit;

        /// <summary>制单：创建一张草稿凭证，须归属于某一账套。</summary>
        public static Voucher Create(Guid accountSetId, string word, int number, DateOnly date, string preparer, int attachmentCount = 0)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。", nameof(accountSetId));
            }

            if (string.IsNullOrWhiteSpace(word))
            {
                throw new ArgumentException("凭证字不能为空。", nameof(word));
            }

            if (number <= 0)
            {
                throw new ArgumentException("凭证号必须为正整数。", nameof(number));
            }

            if (string.IsNullOrWhiteSpace(preparer))
            {
                throw new ArgumentException("制单人不能为空。", nameof(preparer));
            }

            if (attachmentCount < 0)
            {
                throw new ArgumentException("附单据数不能为负数。", nameof(attachmentCount));
            }

            return new Voucher(Guid.NewGuid(), accountSetId, word.Trim(), number, date, preparer.Trim(), attachmentCount);
        }

        /// <summary>
        /// 从外部平台导入一张凭证头。相较于 <see cref="Create"/>，导入路径对字段做宽松处理：
        /// 缺失的凭证字/号/制单人用默认值兜底，以尽量保留原始数据；分录须通过
        /// <see cref="AddImportedEntry"/> 追加。若给出审核人则直接置为已审核状态。
        /// </summary>
        public static Voucher Import(
            Guid accountSetId,
            string? word,
            int number,
            DateOnly date,
            string? preparer,
            int attachmentCount,
            string? reviewer)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。", nameof(accountSetId));
            }

            var voucher = new Voucher(
                Guid.NewGuid(),
                accountSetId,
                string.IsNullOrWhiteSpace(word) ? "记" : word.Trim(),
                number > 0 ? number : 1,
                date,
                string.IsNullOrWhiteSpace(preparer) ? "导入" : preparer.Trim(),
                attachmentCount < 0 ? 0 : attachmentCount);

            if (!string.IsNullOrWhiteSpace(reviewer))
            {
                voucher.Reviewer = reviewer.Trim();
                voucher.Status = VoucherStatus.Reviewed;
            }

            return voucher;
        }

        /// <summary>
        /// 导入专用：按原始数据追加一条分录，原样保留借贷金额（允许负数、允许同侧、允许为零），
        /// 不执行手工录入时的「单边且非零」校验，以忠实还原外部平台的凭证。
        /// </summary>
        public VoucherEntry AddImportedEntry(int lineNumber, string? summary, Guid accountId, decimal debit, decimal credit)
        {
            if (accountId == Guid.Empty)
            {
                throw new ArgumentException("必须指定会计科目。", nameof(accountId));
            }

            var entry = new VoucherEntry(Guid.NewGuid(), Id, summary?.Trim() ?? string.Empty, accountId, debit, credit);
            entry.SetLineNumber(lineNumber > 0 ? lineNumber : _entries.Count + 1);
            _entries.Add(entry);
            return entry;
        }

        /// <summary>新增一条分录（仅草稿状态可用）。传入辅助核算取值时按科目配置登记。</summary>
        public VoucherEntry AddEntry(string summary, Guid accountId, decimal debit, decimal credit, IReadOnlyCollection<AssistRef>? assists = null)
        {
            EnsureDraft();
            GuardEntry(summary, accountId, debit, credit);

            var entry = new VoucherEntry(Guid.NewGuid(), Id, summary.Trim(), accountId, debit, credit);
            _entries.Add(entry);
            if (assists is { Count: > 0 })
            {
                entry.SetAssists(assists);
            }
            Reindex();
            return entry;
        }

        /// <summary>修改一条分录（仅草稿状态可用）。传入空集合表示清除该分录的辅助核算取值。</summary>
        public void UpdateEntry(Guid entryId, string summary, Guid accountId, decimal debit, decimal credit, IReadOnlyCollection<AssistRef>? assists = null)
        {
            EnsureDraft();
            var entry = _entries.FirstOrDefault(e => e.Id == entryId)
                        ?? throw new InvalidOperationException("凭证分录不存在。");

            GuardEntry(summary, accountId, debit, credit);
            entry.Update(summary.Trim(), accountId, debit, credit);
            entry.SetAssists(assists ?? []);
            Reindex();
        }

        /// <summary>删除一条分录（仅草稿状态可用）。</summary>
        public void RemoveEntry(Guid entryId)
        {
            EnsureDraft();
            if (_entries.RemoveAll(e => e.Id == entryId) > 0)
            {
                Reindex();
            }
        }

        /// <summary>修改凭证头信息（仅草稿状态可用）。</summary>
        public void UpdateHeader(DateOnly date, int attachmentCount)
        {
            EnsureDraft();
            if (attachmentCount < 0)
            {
                throw new ArgumentException("附单据数不能为负数。", nameof(attachmentCount));
            }

            Date = date;
            AttachmentCount = attachmentCount;
        }

        /// <summary>审核：草稿 → 已审核。要求借贷平衡且至少一条分录。</summary>
        public void Review(string reviewer)
        {
            if (Status != VoucherStatus.Draft)
            {
                throw new InvalidOperationException("只有草稿状态的凭证才能审核。");
            }

            if (string.IsNullOrWhiteSpace(reviewer))
            {
                throw new ArgumentException("审核人不能为空。", nameof(reviewer));
            }

            EnsurePostable();
            Reviewer = reviewer.Trim();
            Status = VoucherStatus.Reviewed;
        }

        /// <summary>反审核：已审核 → 草稿。</summary>
        public void Unreview()
        {
            if (Status != VoucherStatus.Reviewed)
            {
                throw new InvalidOperationException("只有已审核未过账的凭证才能反审核。");
            }

            Reviewer = null;
            Status = VoucherStatus.Draft;
        }

        /// <summary>过账：已审核 → 已过账。要求借贷平衡。</summary>
        public void Post()
        {
            if (Status != VoucherStatus.Reviewed)
            {
                throw new InvalidOperationException("只有已审核的凭证才能过账。");
            }

            EnsurePostable();
            Status = VoucherStatus.Posted;
        }

        private void EnsureDraft()
        {
            if (Status != VoucherStatus.Draft)
            {
                throw new InvalidOperationException("凭证已审核或已过账，不能修改。");
            }
        }

        private void EnsurePostable()
        {
            if (_entries.Count == 0)
            {
                throw new InvalidOperationException("凭证至少需要一条分录。");
            }

            if (TotalDebit != TotalCredit)
            {
                throw new InvalidOperationException("借贷不平衡，借方合计必须等于贷方合计。");
            }
        }

        private void Reindex()
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                _entries[i].SetLineNumber(i + 1);
            }
        }

        private static void GuardEntry(string summary, Guid accountId, decimal debit, decimal credit)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                throw new ArgumentException("摘要不能为空。", nameof(summary));
            }

            if (accountId == Guid.Empty)
            {
                throw new ArgumentException("必须选择会计科目。", nameof(accountId));
            }

            // 每条分录必须且只能填写借方或贷方其一（允许负数，用于红字冲回）。
            if ((debit != 0m) == (credit != 0m))
            {
                throw new ArgumentException("每条分录只能填写借方或贷方其一，且金额不能为零。");
            }
        }
    }
}
