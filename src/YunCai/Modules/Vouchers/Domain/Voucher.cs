using YunCai.Modules.Assist.Domain;

namespace YunCai.Modules.Vouchers.Domain
{
    /// <summary>
    /// 记账凭证聚合根。由凭证头（字/号、日期、附单据数）与若干会计分录组成。
    /// 遵循「制单 → 过账」工作流：保存即登记入账并锁定；反过账退回暂存后可继续增删改分录。
    /// 过账要求借贷平衡且至少有一条分录。
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

        /// <summary>凭证备注（凭证头部的说明性文字），可为空。</summary>
        public string? Remark { get; private set; }

        /// <summary>凭证状态。</summary>
        public VoucherStatus Status { get; private set; }

        /// <summary>制单人。</summary>
        public string Preparer { get; private set; } = null!;

        /// <summary>审核人，未审核时为空。</summary>
        public string? Reviewer { get; private set; }

        /// <summary>移入回收站的时间，为空表示未删除（回收站为软删除，可还原）。</summary>
        public DateTimeOffset? DeletedAt { get; private set; }

        /// <summary>是否已在回收站中。</summary>
        public bool IsDeleted => DeletedAt.HasValue;

        /// <summary>凭证分录集合（按行号顺序）。</summary>
        public IReadOnlyList<VoucherEntry> Entries => _entries;

        /// <summary>借方合计。</summary>
        public decimal TotalDebit => _entries.Sum(e => e.Debit);

        /// <summary>贷方合计。</summary>
        public decimal TotalCredit => _entries.Sum(e => e.Credit);

        /// <summary>借贷是否平衡（且至少有一条分录）。</summary>
        public bool IsBalanced => _entries.Count > 0 && TotalDebit == TotalCredit;

        /// <summary>制单：创建一张暂存凭证，须归属于某一账套。</summary>
        public static Voucher Create(Guid accountSetId, string word, int number, DateOnly date, string preparer, int attachmentCount = 0, string? remark = null)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。");
            }

            if (string.IsNullOrWhiteSpace(word))
            {
                throw new ArgumentException("凭证字不能为空。");
            }

            if (number <= 0)
            {
                throw new ArgumentException("凭证号必须为正整数。");
            }

            if (string.IsNullOrWhiteSpace(preparer))
            {
                throw new ArgumentException("制单人不能为空。");
            }

            if (attachmentCount < 0)
            {
                throw new ArgumentException("附单据数不能为负数。");
            }

            return new Voucher(Guid.NewGuid(), accountSetId, word.Trim(), number, date, preparer.Trim(), attachmentCount)
            {
                Remark = NormalizeRemark(remark),
            };
        }

        /// <summary>备注归一：去空白、超长截断、空串转 null。</summary>
        private static string? NormalizeRemark(string? remark)
        {
            var value = remark?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return value.Length > 255 ? value[..255] : value;
        }

        /// <summary>
        /// 从外部平台导入一张凭证头。相较于 <see cref="Create"/>，导入路径对字段做宽松处理：
        /// 缺失的凭证字/号/制单人用默认值兜底，以尽量保留原始数据；分录须通过
        /// <see cref="AddImportedEntry"/> 追加。审核人仅作为来源平台的历史信息保留，不影响入账。
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
                throw new ArgumentException("必须指定所属账套。");
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
                throw new ArgumentException("必须指定会计科目。");
            }

            var entry = new VoucherEntry(Guid.NewGuid(), Id, summary?.Trim() ?? string.Empty, accountId, debit, credit);
            entry.SetLineNumber(lineNumber > 0 ? lineNumber : _entries.Count + 1);
            _entries.Add(entry);
            return entry;
        }

        /// <summary>新增一条分录（仅暂存状态可用）。传入辅助核算取值时按科目配置登记。</summary>
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

        /// <summary>修改一条分录（仅暂存状态可用）。传入空集合表示清除该分录的辅助核算取值。</summary>
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

        /// <summary>删除一条分录（仅暂存状态可用）。</summary>
        public void RemoveEntry(Guid entryId)
        {
            EnsureDraft();
            if (_entries.RemoveAll(e => e.Id == entryId) > 0)
            {
                Reindex();
            }
        }

        /// <summary>修改凭证头信息（仅暂存状态可用）。</summary>
        public void UpdateHeader(DateOnly date, int attachmentCount, string? remark = null)
        {
            EnsureDraft();
            if (attachmentCount < 0)
            {
                throw new ArgumentException("附单据数不能为负数。");
            }

            Date = date;
            AttachmentCount = attachmentCount;
            Remark = NormalizeRemark(remark);
        }

        /// <summary>
        /// 过账：暂存 → 正常（登记入账）。要求借贷平衡且至少一条分录。
        /// 保存凭证时直接调用，因此制单即入账，不再有单独的审核环节。
        /// </summary>
        public void Post()
        {
            if (Status != VoucherStatus.Draft)
            {
                throw new InvalidOperationException("凭证已是正常状态，不能重复入账。");
            }

            EnsurePostable();
            Status = VoucherStatus.Posted;
        }

        /// <summary>
        /// 反过账：正常 → 暂存（撤销登记入账，重新变为可修改状态）。
        /// 与过账互为逆操作，用于入账后发现错误时退回修改。
        /// </summary>
        public void Unpost()
        {
            if (Status != VoucherStatus.Posted)
            {
                throw new InvalidOperationException("只有正常凭证才能反过账。");
            }

            Status = VoucherStatus.Draft;
        }

        /// <summary>
        /// 复制一张凭证：保留分录与辅助核算，生成同凭证字下的新暂存凭证。
        /// 摘要可整体加前缀（红冲用「冲销202601记-1_」这类前缀），金额可按需取反。
        /// </summary>
        public static Voucher Duplicate(
            Voucher source,
            int number,
            DateOnly date,
            string preparer,
            string? summaryPrefix = null,
            bool negateAmounts = false)
        {
            var copy = new Voucher(
                Guid.NewGuid(),
                source.AccountSetId,
                source.Word,
                number,
                date,
                preparer,
                source.AttachmentCount)
            {
                Remark = source.Remark,
            };

            foreach (var entry in source._entries.OrderBy(e => e.LineNumber))
            {
                var summary = string.IsNullOrEmpty(summaryPrefix) ? entry.Summary : summaryPrefix + entry.Summary;
                var debit = negateAmounts ? -entry.Debit : entry.Debit;
                var credit = negateAmounts ? -entry.Credit : entry.Credit;
                var cloned = new VoucherEntry(Guid.NewGuid(), copy.Id, summary, entry.AccountId, debit, credit);
                cloned.SetLineNumber(entry.LineNumber);
                copy._entries.Add(cloned);
                if (entry.Assists.Count > 0)
                {
                    cloned.SetAssists(entry.Assists.Select(a => new AssistRef(a.Type, a.AssistItemId)).ToList());
                }
            }

            copy.Reindex();
            return copy;
        }

        /// <summary>
        /// 合并另一张暂存凭证：要求同一凭证字、同一凭证日期，分录按来源顺序追加并保留辅助核算。
        /// 调用方负责移除来源凭证；合并结果仍为暂存，编辑完成后再保存入账。
        /// </summary>
        public void MergeFrom(Voucher source)
        {
            ArgumentNullException.ThrowIfNull(source);
            EnsureDraft();
            if (source.Id == Id)
            {
                throw new InvalidOperationException("不能与自身合并。");
            }

            source.EnsureDraft();
            if (!string.Equals(Word, source.Word, StringComparison.Ordinal) || Date != source.Date)
            {
                throw new InvalidOperationException("只能合并同一凭证字、同一日期的暂存凭证。");
            }

            foreach (var entry in source._entries.OrderBy(e => e.LineNumber))
            {
                var appended = new VoucherEntry(Guid.NewGuid(), Id, entry.Summary, entry.AccountId, entry.Debit, entry.Credit);
                _entries.Add(appended);
                if (entry.Assists.Count > 0)
                {
                    appended.SetAssists(entry.Assists.Select(a => new AssistRef(a.Type, a.AssistItemId)).ToList());
                }
            }

            Reindex();
        }

        /// <summary>批量修改摘要：用同一摘要覆盖全部分录（仅暂存状态可用）。</summary>
        public void ApplySummary(string summary)
        {
            EnsureDraft();
            var text = summary?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                throw new ArgumentException("摘要不能为空。");
            }

            foreach (var entry in _entries)
            {
                entry.Update(text, entry.AccountId, entry.Debit, entry.Credit);
            }
        }

        /// <summary>
        /// 按科目统一设置或清除某类辅助核算（仅暂存状态可用）。
        /// 只影响指定科目的分录，其余类别取值原样保留；itemId 为空表示清除该类取值。
        /// </summary>
        public void ApplyAssist(Guid accountId, AssistType type, Guid? itemId)
        {
            EnsureDraft();
            if (accountId == Guid.Empty)
            {
                throw new ArgumentException("必须指定会计科目。");
            }

            foreach (var entry in _entries.Where(e => e.AccountId == accountId))
            {
                var references = entry.Assists
                    .Where(a => a.Type != type)
                    .Select(a => new AssistRef(a.Type, a.AssistItemId))
                    .ToList();
                if (itemId is Guid item)
                {
                    references.Add(new AssistRef(type, item));
                }

                entry.SetAssists(references);
            }
        }

        /// <summary>调整凭证号（整理凭证 / 凭证号重排时使用）。</summary>
        public void SetNumber(int number)
        {
            if (number <= 0)
            {
                throw new ArgumentException("凭证号必须为正整数。");
            }

            Number = number;
        }

        /// <summary>移入回收站（软删除，可还原）。</summary>
        public void SoftDelete(DateTimeOffset at)
        {
            if (IsDeleted)
            {
                return;
            }

            DeletedAt = at;
        }

        /// <summary>从回收站还原。</summary>
        public void Restore()
        {
            DeletedAt = null;
        }

        private void EnsureDraft()
        {
            if (Status != VoucherStatus.Draft)
            {
                throw new InvalidOperationException("凭证已是正常状态，请先反过账再修改。");
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
                throw new ArgumentException("摘要不能为空。");
            }

            if (accountId == Guid.Empty)
            {
                throw new ArgumentException("必须选择会计科目。");
            }

            // 每条分录必须且只能填写借方或贷方其一（允许负数，用于红字冲回）。
            if ((debit != 0m) == (credit != 0m))
            {
                throw new ArgumentException("每条分录只能填写借方或贷方其一，且金额不能为零。");
            }
        }
    }
}
