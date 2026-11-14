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

        public User? FindByPhone(string phone)
        {
            return _context.Users.SingleOrDefault(u => u.Phone == phone);
        }

        public User? FindById(string id)
        {
            return _context.Users.SingleOrDefault(u => u.Id == id);
        }

        public IReadOnlyList<User> GetAll(string? keyword = null)
        {
            var query = _context.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var pattern = $"%{keyword.Trim()}%";
                query = query.Where(u =>
                    EF.Functions.Like(u.Username, pattern)
                    || EF.Functions.Like(u.DisplayName, pattern)
                    || (u.Phone != null && EF.Functions.Like(u.Phone, pattern)));
            }

            return query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Username).ToList();
        }

        public void Add(User user) => _context.Users.Add(user);

        public void SaveChanges() => _context.SaveChanges();
    }
}