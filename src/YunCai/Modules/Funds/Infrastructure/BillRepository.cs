using YunCai.Modules.Funds.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Funds.Infrastructure
{
    /// <summary>票据仓储实现。</summary>
    public class BillRepository : IBillRepository
    {
        private readonly AppDbContext _context;

        public BillRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Bill?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Bills
                .FirstOrDefaultAsync(b => b.AccountSetId == accountSetId && b.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Bill>> GetRangeAsync(Guid accountSetId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Bills.AsNoTracking()
                .Where(b => b.AccountSetId == accountSetId);
            if (startDate is { } st)
            {
                query = query.Where(b => b.IssueDate >= st);
            }

            if (endDate is { } en)
            {
                query = query.Where(b => b.IssueDate <= en);
            }

            var list = await query.OrderByDescending(b => b.IssueDate).ToListAsync(cancellationToken);
            return list;
        }

        public Task<bool> ExistsByBillNoAsync(Guid accountSetId, string billNo, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.Bills
                .AnyAsync(b => b.AccountSetId == accountSetId && b.BillNo == billNo && (excludeId == null || b.Id != excludeId), cancellationToken);
        }

        public async Task AddAsync(Bill bill, CancellationToken cancellationToken = default)
        {
            await _context.Bills.AddAsync(bill, cancellationToken);
        }

        public void Remove(Bill bill)
        {
            _context.Bills.Remove(bill);
        }

        public async Task<IReadOnlyList<BillAttachmentInfo>> ListAttachmentsAsync(Guid billId, CancellationToken cancellationToken = default)
        {
            return await _context.BillAttachments
                .AsNoTracking()
                .Where(a => a.BillId == billId)
                .OrderBy(a => a.UploadedAt)
                .Select(a => new BillAttachmentInfo(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAt))
                .ToListAsync(cancellationToken);
        }

        public Task<BillAttachment?> GetAttachmentAsync(Guid billId, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            return _context.BillAttachments
                .FirstOrDefaultAsync(a => a.BillId == billId && a.Id == attachmentId, cancellationToken);
        }

        public async Task AddAttachmentAsync(BillAttachment attachment, CancellationToken cancellationToken = default)
        {
            await _context.BillAttachments.AddAsync(attachment, cancellationToken);
        }

        public void RemoveAttachment(BillAttachment attachment)
        {
            _context.BillAttachments.Remove(attachment);
        }

        public async Task<IReadOnlyDictionary<Guid, int>> CountAttachmentsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var rows = await _context.BillAttachments
                .AsNoTracking()
                .Where(a => _context.Bills.Any(b => b.Id == a.BillId && b.AccountSetId == accountSetId))
                .GroupBy(a => a.BillId)
                .Select(g => new { BillId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);
            return rows.ToDictionary(r => r.BillId, r => r.Count);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
