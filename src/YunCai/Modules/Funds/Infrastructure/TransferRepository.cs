using YunCai.Modules.Funds.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Funds.Infrastructure
{
    /// <summary>基于 EF Core 的内部转账仓储实现。</summary>
    public class TransferRepository : ITransferRepository
    {
        private readonly AppDbContext _context;

        public TransferRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Transfer?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Transfers.SingleOrDefaultAsync(
                t => t.AccountSetId == accountSetId && t.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Transfer>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.Transfers
                .AsNoTracking()
                .Where(t => t.AccountSetId == accountSetId)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Summary)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Transfer transfer, CancellationToken cancellationToken = default)
        {
            var entry = _context.Entry(transfer);
            if (entry.State != EntityState.Detached)
            {
                return;
            }

            await _context.Transfers.AddAsync(transfer, cancellationToken);
        }

        public void Remove(Transfer transfer)
        {
            _context.Transfers.Remove(transfer);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
