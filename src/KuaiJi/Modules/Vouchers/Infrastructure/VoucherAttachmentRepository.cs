using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Vouchers.Infrastructure
{
    /// <summary>基于 EF Core 的凭证附件仓储实现。</summary>
    public class VoucherAttachmentRepository : IVoucherAttachmentRepository
    {
        private readonly AppDbContext _context;

        public VoucherAttachmentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<VoucherAttachmentInfo>> ListByVoucherAsync(Guid voucherId, CancellationToken cancellationToken = default)
        {
            // 列表只取元数据，避免把大文件读进内存。
            return await _context.VoucherAttachments
                .AsNoTracking()
                .Where(a => a.VoucherId == voucherId)
                .OrderBy(a => a.UploadedAt)
                .Select(a => new VoucherAttachmentInfo(a.Id, a.VoucherId, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAt))
                .ToListAsync(cancellationToken);
        }

        public Task<VoucherAttachment?> GetAsync(Guid voucherId, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            return _context.VoucherAttachments
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.VoucherId == voucherId && a.Id == attachmentId, cancellationToken);
        }

        public async Task AddAsync(VoucherAttachment attachment, CancellationToken cancellationToken = default)
        {
            await _context.VoucherAttachments.AddAsync(attachment, cancellationToken);
        }

        public void Remove(VoucherAttachment attachment)
        {
            _context.VoucherAttachments.Remove(attachment);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
