namespace YunCai.Modules.Identity.Domain
{
    /// <summary>
    /// 登录账号。账号与账套成员分离：成员表决定「能进哪个账套、有什么角色」，
    /// 用户表只负责身份、密码与登录状态。密码可为空（手机验证码首次登录自动建号）。
    /// </summary>
    public class User
    {
        public required string Id { get; init; }
        public required string Username { get; init; }

        /// <summary>密码哈希；手机号首次登录自动建号时为 null，表示尚未设置密码。</summary>
        public string? PasswordHash { get; set; }

        public required string DisplayName { get; set; }

        /// <summary>手机号；手机验证码首次登录自动建号时写入，历史账号可为空。</summary>
        public string? Phone { get; set; }

        /// <summary>是否启用；停用后登录与已签发的令牌同时失效。</summary>
        public bool IsActive { get; private set; } = true;

        /// <summary>是否系统管理员（可管理全部登录账号；与账套内的「账套管理员」是两件事）。</summary>
        public bool IsSystemAdmin { get; private set; }

        /// <summary>令牌版本：改密、停用、启用等动作会自增，使此前签发的 JWT 立即失效。</summary>
        public int TokenVersion { get; private set; }

        public DateTime? LastLoginAt { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        /// <summary>设置密码并使旧令牌失效。</summary>
        public void SetPassword(string passwordHash)
        {
            PasswordHash = passwordHash;
            InvalidateSessions();
        }

        /// <summary>停用账号，并立即吊销已签发的令牌。</summary>
        public void Disable()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            InvalidateSessions();
        }

        /// <summary>启用账号；历史令牌仍然失效，需要重新登录。</summary>
        public void Enable()
        {
            if (IsActive)
            {
                return;
            }

            IsActive = true;
            InvalidateSessions();
        }

        /// <summary>修改资料（显示名 / 手机号）。</summary>
        public void UpdateProfile(string displayName, string? phone)
        {
            var name = (displayName ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                throw new ArgumentException("请填写姓名。");
            }

            if (name.Length > 100)
            {
                throw new ArgumentException("姓名不能超过 100 个字符。");
            }

            DisplayName = name;
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>授予系统管理员；已签发的令牌需要重新登录才能获得管理入口。</summary>
        public void PromoteToSystemAdmin()
        {
            if (IsSystemAdmin)
            {
                return;
            }

            IsSystemAdmin = true;
            InvalidateSessions();
        }

        public void RecordLogin()
        {
            LastLoginAt = DateTime.UtcNow;
            UpdatedAt = LastLoginAt.Value;
        }

        /// <summary>吊销该账号已签发的全部 JWT。</summary>
        public void InvalidateSessions()
        {
            TokenVersion++;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public interface IUserRepository
    {
        User? FindByUsername(string username);
        User? FindByPhone(string phone);
        User? FindById(string id);
        IReadOnlyList<User> GetAll(string? keyword = null);
        void Add(User user);
        void SaveChanges();
    }
}
