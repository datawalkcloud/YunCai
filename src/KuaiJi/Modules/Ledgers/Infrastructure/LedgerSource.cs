using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Ledgers.Infrastructure
{
    /// <summary>
    /// 基于 EF Core 投影查询的账簿取数实现。
    /// 不物化 <see cref="Voucher"/> 聚合根：区间内的分录一次查出，期初净额交给数据库聚合，
    /// 避免把全部历史分录读进内存累加。所有取数一律限定已过账凭证（账簿口径 §0.1）。
    /// </summary>
    public sealed class LedgerSource : ILedgerSource
    {
        private readonly AppDbContext _context;

        public LedgerSource(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<LedgerFact>> GetFactsAsync(
            Guid accountSetId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        {
            // 不在 SQL 里排序：凭证字是中文，数据库排序规则与序号语义不一致，统一由装载工厂在内存排序。
            var rows = await EntriesInPeriod(accountSetId, startDate, endDate)
                .Select(x => new
                {
                    x.EntryId,
                    x.VoucherId,
                    x.Date,
                    x.Word,
                    x.Number,
                    x.LineNumber,
                    x.Summary,
                    x.AccountId,
                    x.Debit,
                    x.Credit,
                    x.Preparer,
                    x.Reviewer,
                    x.AttachmentCount,
                    x.Remark,
                })
                .ToListAsync(cancellationToken);

            var assistsByEntry = (await AssistsInPeriod(accountSetId, startDate, endDate).ToListAsync(cancellationToken))
                .GroupBy(a => a.EntryId)
                .ToDictionary(g => g.Key, g => g.Select(a => new AssistRef(a.Type, a.AssistItemId)).ToList());

            var facts = new List<LedgerFact>(rows.Count);
            foreach (var row in rows)
            {
                facts.Add(new LedgerFact
                {
                    VoucherId = row.VoucherId,
                    Date = row.Date,
                    Period = LedgerDataSet.PeriodOf(row.Date),
                    Word = row.Word,
                    Number = row.Number,
                    LineNumber = row.LineNumber,
                    Summary = row.Summary,
                    AccountId = row.AccountId,
                    Debit = row.Debit,
                    Credit = row.Credit,
                    Preparer = row.Preparer,
                    Reviewer = row.Reviewer,
                    AttachmentCount = row.AttachmentCount,
                    Remark = row.Remark,
                    Assists = assistsByEntry.TryGetValue(row.EntryId, out var assists) ? assists : [],
                });
            }

            return facts;
        }

        public async Task<IReadOnlyDictionary<Guid, decimal>> GetOpeningNetByAccountAsync(
            Guid accountSetId, DateOnly beforeDate, CancellationToken cancellationToken = default)
        {
            // 聚合必须直接对实体查询做 GroupBy：先投影成私有记录再分组，EF 无法翻译。
            var rows = await (
                from entry in _context.VoucherEntries.AsNoTracking()
                join voucher in _context.Vouchers.AsNoTracking() on entry.VoucherId equals voucher.Id
                where voucher.AccountSetId == accountSetId
                      && voucher.Status == VoucherStatus.Posted
                      && voucher.Date < beforeDate
                group entry by entry.AccountId into grouped
                select new
                {
                    AccountId = grouped.Key,
                    Net = grouped.Sum(e => e.Debit) - grouped.Sum(e => e.Credit),
                })
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(r => r.AccountId, r => r.Net);
        }

        public async Task<IReadOnlyDictionary<BalanceKey, decimal>> GetOpeningNetByAssistAsync(
            Guid accountSetId, DateOnly beforeDate, CancellationToken cancellationToken = default)
        {
            var rows = await (
                from assist in _context.VoucherEntryAssists.AsNoTracking()
                join entry in _context.VoucherEntries.AsNoTracking() on assist.VoucherEntryId equals entry.Id
                join voucher in _context.Vouchers.AsNoTracking() on entry.VoucherId equals voucher.Id
                where voucher.AccountSetId == accountSetId
                      && voucher.Status == VoucherStatus.Posted
                      && voucher.Date < beforeDate
                group entry by new { assist.AccountId, assist.Type, assist.AssistItemId } into grouped
                select new
                {
                    grouped.Key.AccountId,
                    grouped.Key.Type,
                    grouped.Key.AssistItemId,
                    Net = grouped.Sum(e => e.Debit) - grouped.Sum(e => e.Credit),
                })
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(
                r => new BalanceKey(r.AccountId, r.Type, r.AssistItemId),
                r => r.Net);
        }

        public async Task<int> CountUnpostedAsync(
            Guid accountSetId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        {
            return await _context.Vouchers
                .AsNoTracking()
                .CountAsync(v => v.AccountSetId == accountSetId
                                 && v.Status != VoucherStatus.Posted
                                 && v.Date >= startDate && v.Date <= endDate, cancellationToken);
        }

        public async Task<(DateOnly? First, DateOnly? Last)> GetVoucherDateBoundsAsync(
            Guid accountSetId, CancellationToken cancellationToken = default)
        {
            // 边界取全部凭证（含未过账），使缺省查询区间与实际录入范围一致。
            var dates = _context.Vouchers
                .AsNoTracking()
                .Where(v => v.AccountSetId == accountSetId)
                .Select(v => v.Date);

            if (!await dates.AnyAsync(cancellationToken))
            {
                return (null, null);
            }

            return (await dates.MinAsync(cancellationToken), await dates.MaxAsync(cancellationToken));
        }

        /// <summary>
        /// 区间内已过账分录（含展平的凭证头字段）。
        /// 日期条件必须写在查询表达式的 where 里：投影成记录类型之后再过滤，EF 无法翻译。
        /// </summary>
        private IQueryable<EntryRow> EntriesInPeriod(Guid accountSetId, DateOnly startDate, DateOnly endDate)
        {
            return from entry in _context.VoucherEntries.AsNoTracking()
                   join voucher in _context.Vouchers.AsNoTracking() on entry.VoucherId equals voucher.Id
                   where voucher.AccountSetId == accountSetId
                         && voucher.Status == VoucherStatus.Posted
                         && voucher.Date >= startDate && voucher.Date <= endDate
                   select new EntryRow(
                       entry.Id,
                       voucher.Id,
                       voucher.Date,
                       voucher.Word,
                       voucher.Number,
                       entry.LineNumber,
                       entry.Summary,
                       entry.AccountId,
                       entry.Debit,
                       entry.Credit,
                       voucher.Preparer,
                       voucher.Reviewer,
                       voucher.AttachmentCount,
                       voucher.Remark);
        }

        /// <summary>区间内已过账分录上的辅助核算取值（按分录标识与凭证日期关联）。</summary>
        private IQueryable<AssistRow> AssistsInPeriod(Guid accountSetId, DateOnly startDate, DateOnly endDate)
        {
            return from assist in _context.VoucherEntryAssists.AsNoTracking()
                   join entry in _context.VoucherEntries.AsNoTracking() on assist.VoucherEntryId equals entry.Id
                   join voucher in _context.Vouchers.AsNoTracking() on entry.VoucherId equals voucher.Id
                   where voucher.AccountSetId == accountSetId
                         && voucher.Status == VoucherStatus.Posted
                         && voucher.Date >= startDate && voucher.Date <= endDate
                   select new AssistRow(
                       entry.Id,
                       assist.Type,
                       assist.AssistItemId,
                       voucher.Date);
        }

        /// <summary>分录与凭证头展平后的查询投影行。</summary>
        private sealed record EntryRow(
            Guid EntryId,
            Guid VoucherId,
            DateOnly Date,
            string Word,
            int Number,
            int LineNumber,
            string Summary,
            Guid AccountId,
            decimal Debit,
            decimal Credit,
            string Preparer,
            string? Reviewer,
            int AttachmentCount,
            string? Remark);

        /// <summary>辅助核算取值投影行，日期取自所属凭证以便按区间过滤。</summary>
        private sealed record AssistRow(
            Guid EntryId,
            AssistType Type,
            Guid AssistItemId,
            DateOnly Date);
    }
}
