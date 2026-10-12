using KuaiJi.Modules.Invoices.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Invoices.Infrastructure
{
    /// <summary>发票仓储实现（含明细行加载）。</summary>
    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly AppDbContext _context;

        public InvoiceRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Invoice?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Invoices
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.AccountSetId == accountSetId && i.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Invoice>> GetRangeAsync(
            Guid accountSetId,
            InvoiceDirection direction,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            InvoiceStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Invoices.AsNoTracking()
                .Include(i => i.Lines)
                .Where(i => i.AccountSetId == accountSetId && i.Direction == direction);
            if (startDate is { } st)
            {
                query = query.Where(i => i.IssueDate >= st);
            }

            if (endDate is { } en)
            {
                query = query.Where(i => i.IssueDate <= en);
            }

            if (status is { } stat)
            {
                query = query.Where(i => i.Status == stat);
            }

            var list = await query.OrderByDescending(i => i.InputDate).ToListAsync(cancellationToken);
            return list;
        }

        public Task<bool> ExistsByInvoiceNoAsync(Guid accountSetId, InvoiceDirection direction, string invoiceNo, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.Invoices
                .AnyAsync(i => i.AccountSetId == accountSetId && i.Direction == direction && i.InvoiceNo == invoiceNo && (excludeId == null || i.Id != excludeId), cancellationToken);
        }

        public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            await _context.Invoices.AddAsync(invoice, cancellationToken);
        }

        public void Remove(Invoice invoice)
        {
            _context.Invoices.Remove(invoice);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
