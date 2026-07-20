using KuaiJi.Domain.Users;
using KuaiJi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Infrastructure.Persistence
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public User? FindByUsername(string username)
        {
            return _context.Users.SingleOrDefault(u => u.Username == username);
        }
    }
}