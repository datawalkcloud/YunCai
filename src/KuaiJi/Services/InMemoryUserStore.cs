using KuaiJi.Models;

namespace KuaiJi.Services
{
    /// <summary>
    /// 用户存储抽象，便于后续替换为数据库实现。
    /// </summary>
    public interface IUserStore
    {
        AppUser? FindByUsername(string username);
    }

    /// <summary>
    /// 内存中的演示用户存储。密码在启动时哈希，避免明文落库。
    /// 演示账号：admin / admin123，user / user123。
    /// </summary>
    public class InMemoryUserStore : IUserStore
    {
        private readonly Dictionary<string, AppUser> _users;

        public InMemoryUserStore()
        {
            var seed = new[]
            {
                new AppUser
                {
                    Id = "1",
                    Username = "admin",
                    DisplayName = "管理员",
                    PasswordHash = PasswordHasher.Hash("admin123")
                },
                new AppUser
                {
                    Id = "2",
                    Username = "user",
                    DisplayName = "普通用户",
                    PasswordHash = PasswordHasher.Hash("user123")
                }
            };

            _users = seed.ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);
        }

        public AppUser? FindByUsername(string username)
        {
            return _users.GetValueOrDefault(username);
        }
    }
}
