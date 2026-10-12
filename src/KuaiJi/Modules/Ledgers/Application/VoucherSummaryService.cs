namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>凭证汇总表的一行：一个科目在区间内的借、贷合计。</summary>
    public sealed class VoucherSummaryRowDto
    {
        /// <summary>科目编码。</summary>
        public required string Code { get; init; }

        /// <summary>科目名称。</summary>
        public required string Name { get; init; }

        /// <summary>该科目涉及的凭证张数（按凭证去重）。</summary>
        public int VoucherCount { get; init; }

        /// <summary>借方合计。</summary>
        public decimal Debit { get; init; }

        /// <summary>贷方合计。</summary>
        public decimal Credit { get; init; }
    }

    /// <summary>凭证汇总表。</summary>
    public sealed class VoucherSummaryDto
    {
        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>表头统计：区间内涉及凭证张数（按凭证去重）。</summary>
        public int VoucherCount { get; init; }

        /// <summary>表头统计：附件总张数（每张凭证只累加一次）。</summary>
        public int AttachmentCount { get; init; }

        /// <summary>各科目的借贷合计行，按编码排序。</summary>
        public IReadOnlyList<VoucherSummaryRowDto> Rows { get; init; } = [];

        /// <summary>借方总计，恒等于贷方总计（借贷记账法）。</summary>
        public decimal TotalDebit { get; init; }

        /// <summary>贷方总计。</summary>
        public decimal TotalCredit { get; init; }
    }

    /// <summary>
    /// 凭证汇总表应用服务：按科目汇总区间内借贷发生额，并统计涉及凭证张数与附件张数。
    /// 张数一律按凭证去重，附件张数每张凭证只累加一次。
    /// </summary>
    public sealed class VoucherSummaryService
    {
        private readonly LedgerDataSetFactory _factory;

        public VoucherSummaryService(LedgerDataSetFactory factory)
        {
            _factory = factory;
        }

        /// <summary>生成凭证汇总表。</summary>
        public async Task<VoucherSummaryDto> GetAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var dataSet = await _factory.LoadAsync(accountSetId, options, cancellationToken);
            var keyword = options.SummaryKeyword?.Trim();
            var factsByAccount = dataSet.FactsByAccount;
            var accounts = dataSet.SelectAccounts();
            var subtreeByAccount = accounts.ToDictionary(a => a, a => dataSet.SubtreeAccountIds(a));

            var matches = (LedgerFact f) => keyword is not { Length: > 0 } || f.Summary.Contains(keyword, StringComparison.Ordinal);

            var rows = new List<VoucherSummaryRowDto>();
            foreach (var account in accounts)
            {
                // 上级行的合计取其全部下级科目之和，与科目余额表、总账口径一致。
                var facts = subtreeByAccount[account]
                    .SelectMany(id => factsByAccount[id])
                    .Where(matches)
                    .ToList();

                if (facts.Count == 0)
                {
                    continue;
                }

                rows.Add(new VoucherSummaryRowDto
                {
                    Code = account.Code,
                    Name = account.Name,
                    VoucherCount = facts.Select(f => f.VoucherId).Distinct().Count(),
                    Debit = facts.Sum(f => f.Debit),
                    Credit = facts.Sum(f => f.Credit),
                });
            }

            // 总计按「被列示科目覆盖到的分录」去重后计算：同时列示上级与下级时行合计会重复包含下级，总计不能重复。
            var coveredIds = subtreeByAccount.Values.SelectMany(ids => ids).ToHashSet();
            var coveredFacts = dataSet.Facts.Where(f => coveredIds.Contains(f.AccountId) && matches(f)).ToList();

            return new VoucherSummaryDto
            {
                Meta = LedgerMetaDto.From(dataSet),
                VoucherCount = coveredFacts.Select(f => f.VoucherId).Distinct().Count(),
                AttachmentCount = coveredFacts.GroupBy(f => f.VoucherId).Sum(g => g.First().AttachmentCount),
                Rows = rows,
                TotalDebit = coveredFacts.Sum(f => f.Debit),
                TotalCredit = coveredFacts.Sum(f => f.Credit),
            };
        }
    }
}
