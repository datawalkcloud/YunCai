using YunCai.Modules.Identity.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Identity.Infrastructure
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