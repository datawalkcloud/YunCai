namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>序时账的一行（一条已过账分录）。附件数在凭证首行显示，其余行为 0。</summary>
    public sealed class VoucherJournalRowDto
    {
        /// <summary>凭证日期。</summary>
        public required string Date { get; init; }

        /// <summary>凭证字号，如「记-1」。</summary>
        public required string VoucherNo { get; init; }

        /// <summary>分录摘要。</summary>
        public required string Summary { get; init; }

        /// <summary>科目编码。</summary>
        public required string AccountCode { get; init; }

        /// <summary>科目名称。</summary>
        public required string AccountName { get; init; }

        /// <summary>借方金额。</summary>
        public decimal Debit { get; init; }

        /// <summary>贷方金额。</summary>
        public decimal Credit { get; init; }

        /// <summary>制单人。</summary>
        public required string Preparer { get; init; }

        /// <summary>审核人（未审核时为空）。</summary>
        public string? Reviewer { get; init; }

        /// <summary>凭证附件张数，同一凭证只在首行列示以免重复计数。</summary>
        public int AttachmentCount { get; init; }
    }

    /// <summary>序时账。</summary>
    public sealed class VoucherJournalDto
    {
        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>序时流水，已按查询条件的排序方式排列。</summary>
        public IReadOnlyList<VoucherJournalRowDto> Rows { get; init; } = [];

        /// <summary>借方合计。</summary>
        public decimal TotalDebit { get; init; }

        /// <summary>贷方合计。</summary>
        public decimal TotalCredit { get; init; }

        /// <summary>涉及凭证张数（按凭证去重，非分录行数）。</summary>
        public int VoucherCount { get; init; }

        /// <summary>涉及附件总张数（按凭证取一次，不随分录行数重复累加）。</summary>
        public int TotalAttachments { get; init; }
    }

    /// <summary>
    /// 序时账应用服务：把已过账分录按查询条件的排序方式逐笔流水列出，并带出凭证头的制单人、审核人与附件数。
    /// 科目条件、级次与摘要关键字在这里过滤（合计随之变化，因为序时账本身就是流水账）。
    /// </summary>
    public sealed class VoucherJournalService
    {
        private readonly LedgerDataSetFactory _factory;

        public VoucherJournalService(LedgerDataSetFactory factory)
        {
            _factory = factory;
        }

        /// <summary>生成序时账。</summary>
        public async Task<VoucherJournalDto> GetAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var dataSet = await _factory.LoadAsync(accountSetId, options, cancellationToken);

            var listedAccountIds = dataSet.SelectAccounts().Select(a => a.Id).ToHashSet();
            var keyword = options.SummaryKeyword?.Trim();

            var rows = new List<VoucherJournalRowDto>();
            var listedVouchers = new HashSet<Guid>();
            decimal totalDebit = 0m, totalCredit = 0m;
            var totalAttachments = 0;

            foreach (var fact in dataSet.Facts)
            {
                // 一张凭证只处理一次：命中科目条件且摘要命中的分录触发列示，随后整张凭证的分录一并列出，
                // 避免同一凭证被摘要关键字拆成半张。
                if (listedVouchers.Contains(fact.VoucherId)
                    || !listedAccountIds.Contains(fact.AccountId)
                    || (keyword is { Length: > 0 } && !fact.Summary.Contains(keyword, StringComparison.Ordinal)))
                {
                    continue;
                }

                listedVouchers.Add(fact.VoucherId);
                totalAttachments += fact.AttachmentCount;

                var isFirstRow = true;
                foreach (var entry in dataSet.FactsOfVoucher(fact.VoucherId))
                {
                    if (!listedAccountIds.Contains(entry.AccountId))
                    {
                        continue;
                    }

                    totalDebit += entry.Debit;
                    totalCredit += entry.Credit;
                    rows.Add(new VoucherJournalRowDto
                    {
                        Date = entry.Date.ToString("yyyy-MM-dd"),
                        VoucherNo = entry.VoucherNo,
                        Summary = entry.Summary,
                        AccountCode = dataSet.AccountsById.GetValueOrDefault(entry.AccountId)?.Code ?? string.Empty,
                        AccountName = dataSet.AccountsById.GetValueOrDefault(entry.AccountId)?.Name ?? string.Empty,
                        Debit = entry.Debit,
                        Credit = entry.Credit,
                        Preparer = entry.Preparer,
                        Reviewer = entry.Reviewer,
                        // 附件数属于凭证头，只在首行列示，合计才不会随分录行数重复累加。
                        AttachmentCount = isFirstRow ? entry.AttachmentCount : 0,
                    });
                    isFirstRow = false;
                }
            }

            return new VoucherJournalDto
            {
                Meta = LedgerMetaDto.From(dataSet),
                Rows = rows,
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                VoucherCount = listedVouchers.Count,
                TotalAttachments = totalAttachments,
            };
        }
    }
}
