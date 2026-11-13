using YunCai.Modules.AccountSets.Application;
using YunCai.Modules.AccountSets.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.AccountSets.Infrastructure
{
    /// <summary>账套成员仓储实现（EF Core）。</summary>
    public class AccountSetMemberRepository : IAccountSetMemberRepository
    {
        private readonly AppDbContext _context;

        public AccountSetMemberRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<AccountSetMember>> GetMembersAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.AccountSetMembers
                .AsNoTracking()
                .Where(m => m.AccountSetId == accountSetId)
                .OrderByDescending(m => m.IsOwner)
                .ThenBy(m => m.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public Task<AccountSetMember?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.AccountSetMembers
                .FirstOrDefaultAsync(m => m.AccountSetId == accountSetId && m.Id == id, cancellationToken);
        }

        public Task<bool> AnyWithRoleAsync(Guid accountSetId, Guid roleId, CancellationToken cancellationToken = default)
        {
            return _context.AccountSetMembers
                .AsNoTracking()
                .AnyAsync(m => m.AccountSetId == accountSetId && m.RoleId == roleId, cancellationToken);
        }

        public async Task AddAsync(AccountSetMember member, CancellationToken cancellationToken = default)
        {
            await _context.AccountSetMembers.AddAsync(member, cancellationToken);
        }

        public void Remove(AccountSetMember member) => _context.AccountSetMembers.Remove(member);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
    }
}
