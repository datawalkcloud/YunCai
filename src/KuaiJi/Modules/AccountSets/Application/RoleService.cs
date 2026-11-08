using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.AccountSets.Application
{
    /// <summary>
    /// 账套角色应用服务：首次读取时播种预置角色（账套管理员 / 主管 / 制单人 / 出纳 / 查看），
    /// 支持自建角色的增删改；账套管理员固定全部权限，预置角色不可删除、不可改名。
    /// </summary>
    public sealed class RoleService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountSetRoleRepository _roleRepository;
        private readonly IAccountSetMemberRepository _memberRepository;

        public RoleService(
            IAccountSetRepository accountSetRepository,
            IAccountSetRoleRepository roleRepository,
            IAccountSetMemberRepository memberRepository)
        {
            _accountSetRepository = accountSetRepository;
            _roleRepository = roleRepository;
            _memberRepository = memberRepository;
        }

        /// <summary>权限目录（模块 × 操作），供前端渲染权限勾选面板。</summary>
        public static IReadOnlyList<PermissionModuleDto> GetCatalog()
            => RolePermissionCatalog.Modules
                .Select(m => new PermissionModuleDto
                {
                    Key = m.Key,
                    Name = m.Name,
                    Actions = m.Actions
                        .Select(a => new PermissionActionDto { Key = a.Key, Name = a.Name })
                        .ToList(),
                })
                .ToList();

        /// <summary>获取账套角色列表；空账套自动播种预置角色。</summary>
        public async Task<IReadOnlyList<AccountSetRoleDto>> GetListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(accountSetId, cancellationToken);
            var roles = await _roleRepository.GetListAsync(accountSetId, cancellationToken);
            return roles.Select(ToDto).ToList();
        }

        /// <summary>新增自建角色。</summary>
        public async Task<AccountSetRoleDto> CreateAsync(Guid accountSetId, SaveAccountSetRoleRequest request, CancellationToken cancellationToken = default)
        {
            var roles = await GetListAsync(accountSetId, cancellationToken);
            var name = ValidateName(request.Name, roles, null);
            var permissions = ValidatePermissions(request.Permissions);

            var sortOrder = roles.Count == 0 ? 0 : roles.Max(r => r.SortOrder) + 1;
            AccountSetRole role;
            try
            {
                role = AccountSetRole.Create(accountSetId, name, permissions, sortOrder: sortOrder);
            }
            catch (ArgumentException ex)
            {
                throw new RoleException(ex.Message);
            }

            await _roleRepository.AddAsync(role, cancellationToken);
            await _roleRepository.SaveChangesAsync(cancellationToken);
            return ToDto(role);
        }

        /// <summary>修改角色：自建角色可改名，预置角色只改权限，账套管理员不可修改。</summary>
        public async Task<AccountSetRoleDto> UpdateAsync(Guid accountSetId, Guid id, SaveAccountSetRoleRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(accountSetId, cancellationToken);
            var role = await _roleRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                ?? throw new RoleException("角色不存在。");
            if (role.IsAdmin)
            {
                throw new RoleException("账套管理员为固定角色，权限不能修改。");
            }

            if (!role.IsSystem)
            {
                var roles = await _roleRepository.GetListAsync(accountSetId, cancellationToken);
                role.Rename(ValidateName(request.Name, roles.Select(ToDto).ToList(), id));
            }

            var permissions = ValidatePermissions(request.Permissions);
            try
            {
                role.SetPermissions(permissions);
            }
            catch (InvalidOperationException ex)
            {
                throw new RoleException(ex.Message);
            }

            await _roleRepository.SaveChangesAsync(cancellationToken);
            return ToDto(role);
        }

        /// <summary>删除自建角色；预置角色或仍有成员使用时拒绝。</summary>
        public async Task DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            await EnsureSeededAsync(accountSetId, cancellationToken);
            var role = await _roleRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                ?? throw new RoleException("角色不存在。");
            if (role.IsSystem)
            {
                throw new RoleException("预置角色不能删除。");
            }

            if (await _memberRepository.AnyWithRoleAsync(accountSetId, role.Id, cancellationToken))
            {
                throw new RoleException("该角色下还有成员，请先调整成员角色。");
            }

            _roleRepository.Remove(role);
            await _roleRepository.SaveChangesAsync(cancellationToken);
        }

        /// <summary>确保账套已有预置角色；没有则按目录播种。</summary>
        public async Task EnsureSeededAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new RoleException("缺少账套。");
            }

            if (await _roleRepository.AnyAsync(accountSetId, cancellationToken))
            {
                return;
            }

            if (await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new RoleException("账套不存在。");
            }

            var roles = RolePermissionCatalog.Presets
                .Select((preset, index) => AccountSetRole.Create(
                    accountSetId,
                    preset.Name,
                    preset.Keys,
                    isSystem: true,
                    isAdmin: preset.IsAdmin,
                    sortOrder: index))
                .ToList();
            await _roleRepository.AddRangeAsync(roles, cancellationToken);
            await _roleRepository.SaveChangesAsync(cancellationToken);
        }

        private static string ValidateName(string? name, IReadOnlyList<AccountSetRoleDto> roles, Guid? excludeId)
        {
            var trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                throw new RoleException("请填写角色名称。");
            }

            if (trimmed.Length > AccountSetRole.MaxNameLength)
            {
                throw new RoleException($"角色名称不能超过 {AccountSetRole.MaxNameLength} 个字符。");
            }

            if (roles.Any(r => r.Id != excludeId && string.Equals(r.Name, trimmed, StringComparison.Ordinal)))
            {
                throw new RoleException($"角色「{trimmed}」已存在。");
            }

            return trimmed;
        }

        private static IReadOnlyList<string> ValidatePermissions(IReadOnlyList<string>? permissions)
        {
            var normalized = RolePermissionCatalog.Normalize(permissions);
            if (normalized.Count == 0)
            {
                throw new RoleException("请至少勾选一项权限。");
            }

            return normalized;
        }

        private static AccountSetRoleDto ToDto(AccountSetRole role)
        {
            var permissions = role.IsAdmin ? RolePermissionCatalog.AllKeys : role.PermissionKeys;
            return new AccountSetRoleDto
            {
                Id = role.Id,
                Name = role.Name,
                IsSystem = role.IsSystem,
                IsAdmin = role.IsAdmin,
                Permissions = permissions,
                PermissionText = RolePermissionCatalog.BuildSummary(permissions),
                SortOrder = role.SortOrder,
            };
        }
    }
}
