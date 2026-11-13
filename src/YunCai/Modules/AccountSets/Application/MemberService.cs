using YunCai.Modules.AccountSets.Domain;

namespace YunCai.Modules.AccountSets.Application
{
    /// <summary>账套成员服务：列表补种创建者、增删改与启停；成员角色来自账套角色表。</summary>
    public class MemberService
    {
        private readonly IAccountSetMemberRepository _memberRepository;
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly RoleService _roleService;

        public MemberService(
            IAccountSetMemberRepository memberRepository,
            IAccountSetRepository accountSetRepository,
            RoleService roleService)
        {
            _memberRepository = memberRepository;
            _accountSetRepository = accountSetRepository;
            _roleService = roleService;
        }

        /// <summary>获取成员列表；首次访问时补种账套创建者（管理员角色）。</summary>
        public async Task<IReadOnlyList<AccountSetMemberDto>> GetAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var roles = await _roleService.GetListAsync(accountSetId, cancellationToken);
            var roleMap = roles.ToDictionary(r => r.Id);

            var members = await _memberRepository.GetMembersAsync(accountSetId, cancellationToken);
            if (members.Count == 0)
            {
                var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                    ?? throw new MemberException("账套不存在。");
                var adminRole = roles.FirstOrDefault(r => r.IsAdmin)
                    ?? throw new MemberException("账套缺少管理员角色。");
                var ownerName = string.IsNullOrWhiteSpace(accountSet.CompanyName) ? accountSet.Name : accountSet.CompanyName;
                var owner = AccountSetMember.CreateOwner(accountSetId, ownerName, adminRole.Id);
                await _memberRepository.AddAsync(owner, cancellationToken);
                await _memberRepository.SaveChangesAsync(cancellationToken);
                members = await _memberRepository.GetMembersAsync(accountSetId, cancellationToken);
            }

            return members.Select(m => ToDto(m, roleMap)).ToList();
        }

        /// <summary>新增成员。</summary>
        public async Task<AccountSetMemberDto> CreateAsync(Guid accountSetId, SaveAccountSetMemberRequest request, CancellationToken cancellationToken = default)
        {
            Validate(request);
            var roleMap = (await _roleService.GetListAsync(accountSetId, cancellationToken)).ToDictionary(r => r.Id);
            if (!roleMap.ContainsKey(request.RoleId))
            {
                throw new MemberException("请选择有效的角色。");
            }

            var existing = await _memberRepository.GetMembersAsync(accountSetId, cancellationToken);
            var phone = request.Phone.Trim();
            if (phone.Length > 0 && existing.Any(m => m.Phone == phone))
            {
                throw new MemberException($"手机号 {phone} 已存在于本账套成员中。");
            }

            var member = AccountSetMember.Create(accountSetId, request.Name, phone, request.Email, request.RoleId);
            await _memberRepository.AddAsync(member, cancellationToken);
            await _memberRepository.SaveChangesAsync(cancellationToken);
            return ToDto(member, roleMap);
        }

        /// <summary>修改成员资料与角色。</summary>
        public async Task<AccountSetMemberDto> UpdateAsync(Guid accountSetId, Guid id, SaveAccountSetMemberRequest request, CancellationToken cancellationToken = default)
        {
            Validate(request);
            var roleMap = (await _roleService.GetListAsync(accountSetId, cancellationToken)).ToDictionary(r => r.Id);
            var member = await _memberRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                ?? throw new MemberException("成员不存在。");

            if (!roleMap.TryGetValue(request.RoleId, out var role))
            {
                throw new MemberException("请选择有效的角色。");
            }

            if (member.IsOwner && !role.IsAdmin)
            {
                throw new MemberException("账套创建者必须保持「账套管理员」角色。");
            }

            member.Update(request.Name, request.Phone ?? string.Empty, request.Email, request.RoleId);
            await _memberRepository.SaveChangesAsync(cancellationToken);
            return ToDto(member, roleMap);
        }

        /// <summary>启用 / 停用成员。</summary>
        public async Task<AccountSetMemberDto> SetEnabledAsync(Guid accountSetId, Guid id, bool enabled, CancellationToken cancellationToken = default)
        {
            var roleMap = (await _roleService.GetListAsync(accountSetId, cancellationToken)).ToDictionary(r => r.Id);
            var member = await _memberRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                ?? throw new MemberException("成员不存在。");
            if (member.IsOwner && !enabled)
            {
                throw new MemberException("账套创建者不能被停用。");
            }

            member.SetEnabled(enabled);
            await _memberRepository.SaveChangesAsync(cancellationToken);
            return ToDto(member, roleMap);
        }

        /// <summary>删除成员；创建者不可删除。</summary>
        public async Task DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var member = await _memberRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                ?? throw new MemberException("成员不存在。");
            if (member.IsOwner)
            {
                throw new MemberException("账套创建者不能删除。");
            }

            _memberRepository.Remove(member);
            await _memberRepository.SaveChangesAsync(cancellationToken);
        }

        private static void Validate(SaveAccountSetMemberRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new MemberException("请填写成员姓名。");
            }

            if (request.Name.Trim().Length > 20)
            {
                throw new MemberException("成员姓名不能超过 20 个字符。");
            }

            var phone = request.Phone?.Trim() ?? string.Empty;
            if (phone.Length > 0 && (phone.Length != 11 || phone.Any(c => !char.IsAsciiDigit(c)) || phone[0] != '1'))
            {
                throw new MemberException("手机号需为 11 位数字且以 1 开头。");
            }

            var email = request.Email?.Trim() ?? string.Empty;
            if (email.Length > 0 && (!email.Contains('@') || email.Length < 5 || email.Contains(' ')))
            {
                throw new MemberException("邮箱格式不正确。");
            }
        }

        private static AccountSetMemberDto ToDto(AccountSetMember member, IReadOnlyDictionary<Guid, AccountSetRoleDto> roleMap)
        {
            roleMap.TryGetValue(member.RoleId, out var role);
            return new AccountSetMemberDto
            {
                Id = member.Id,
                Name = member.Name,
                Phone = member.Phone,
                Email = member.Email,
                RoleId = member.RoleId,
                RoleName = role?.Name ?? "未分配",
                PermissionText = role?.PermissionText ?? string.Empty,
                Enabled = member.Enabled,
                IsOwner = member.IsOwner,
                CreatedAt = member.CreatedAt,
            };
        }
    }
}
