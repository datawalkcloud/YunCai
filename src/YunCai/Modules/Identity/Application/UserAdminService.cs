using System.Text.RegularExpressions;
using YunCai.Modules.Identity.Domain;
using YunCai.Modules.Identity.Infrastructure;

namespace YunCai.Modules.Identity.Application
{
    /// <summary>
    /// 系统管理员的登录账号管理：列表、开户、改资料、停用 / 启用、重置密码。
    /// 与账套成员管理分开——这里只管「能不能登录」，进哪个账套仍由成员表决定。
    /// </summary>
    public sealed class UserAdminService
    {
        private static readonly Regex PhonePattern = new(@"^1\d{10}$", RegexOptions.Compiled);

        private readonly IUserRepository _users;

        public UserAdminService(IUserRepository users)
        {
            _users = users;
        }

        public IReadOnlyList<UserDto> List(string? keyword = null)
            => _users.GetAll(keyword).Select(UserDto.From).ToList();

        public UserDto Create(CreateUserRequest request)
        {
            var displayName = (request.DisplayName ?? string.Empty).Trim();
            if (displayName.Length == 0)
            {
                throw new ArgumentException("请填写姓名。");
            }

            var phone = (request.Phone ?? string.Empty).Trim();
            if (!PhonePattern.IsMatch(phone))
            {
                throw new ArgumentException("手机号格式不正确。");
            }

            if (_users.FindByPhone(phone) is not null || _users.FindByUsername(phone) is not null)
            {
                throw new ArgumentException("该手机号已注册。");
            }

            var user = new User
            {
                Id = Guid.CreateVersion7().ToString("N"),
                Username = phone,
                DisplayName = displayName,
                Phone = phone
            };

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.SetPassword(PasswordHasher.Hash(PasswordPolicy.Validate(request.Password)));
            }

            _users.Add(user);
            _users.SaveChanges();
            return UserDto.From(user);
        }

        public UserDto UpdateProfile(string userId, UpdateUserRequest request)
        {
            var user = _users.FindById(userId) ?? throw new ArgumentException("用户不存在。");
            var phone = (request.Phone ?? string.Empty).Trim();
            if (phone.Length > 0)
            {
                if (!PhonePattern.IsMatch(phone))
                {
                    throw new ArgumentException("手机号格式不正确。");
                }

                var existing = _users.FindByPhone(phone);
                if (existing is not null && existing.Id != user.Id)
                {
                    throw new ArgumentException("该手机号已被其他账号使用。");
                }
            }

            try
            {
                user.UpdateProfile(request.DisplayName, phone);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ex.Message);
            }

            _users.SaveChanges();
            return UserDto.From(user);
        }

        public UserDto SetEnabled(string userId, bool enabled, string operatorUserId)
        {
            var user = _users.FindById(userId) ?? throw new ArgumentException("用户不存在。");
            if (!enabled)
            {
                if (user.Id == operatorUserId)
                {
                    throw new ArgumentException("不能停用当前登录账号。");
                }

                if (user.IsSystemAdmin && user.IsActive
                    && _users.GetAll().Count(u => u.IsSystemAdmin && u.IsActive) <= 1)
                {
                    throw new ArgumentException("至少保留一个启用的系统管理员。");
                }
            }

            if (enabled)
            {
                user.Enable();
            }
            else
            {
                user.Disable();
            }

            _users.SaveChanges();
            return UserDto.From(user);
        }

        public void ResetPassword(string userId, string newPassword)
        {
            var user = _users.FindById(userId) ?? throw new ArgumentException("用户不存在。");
            user.SetPassword(PasswordHasher.Hash(PasswordPolicy.Validate(newPassword)));
            _users.SaveChanges();
        }
    }

    public sealed class CreateUserRequest
    {
        public string DisplayName { get; init; } = string.Empty;

        public string Phone { get; init; } = string.Empty;

        /// <summary>初始密码，可留空；留空时用户用短信验证码登录后自行设置。</summary>
        public string? Password { get; init; }
    }

    public sealed class UpdateUserRequest
    {
        public string DisplayName { get; init; } = string.Empty;

        public string? Phone { get; init; }
    }
}
