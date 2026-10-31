using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.AccountSets.Application
{
    /// <summary>账套成员与角色服务：列表补种创建者、增删改与启停。</summary>
    public class MemberService
    {
        private readonly IAccountSetMemberRepository _memberRepository;
        private readonly IAccountSetRepository _accountSetRepository;

        public MemberService(IAccountSetMemberRepository memberRepository, IAccountSetRepository accountSetRepository)
        {
            _memberRepository = memberRepository;
            _accountSetRepository = accountSetRepository;
        }

        /// <summary>角色显示文本。</summary>
        public static string RoleText(AccountSetMemberRole role) => role switch
        {
            AccountSetMemberRole.Admin => "管理员",
            AccountSetMemberRole.Accountant => "会计",
            AccountSetMemberRole.Cashier => "出纳",
            AccountSetMemberRole.Viewer => "查看",
            _ => "查看",
        };

        /// <summary>角色权限说明。</summary>
        public static string PermissionText(AccountSetMemberRole role) => role switch
        {
            AccountSetMemberRole.Admin => "全部功能，含账套设置与成员管理",
            AccountSetMemberRole.Accountant => "凭证、账簿、报表、结账与单据",
            AccountSetMemberRole.Cashier => "资金日记账、银行对账与收付款",
            AccountSetMemberRole.Viewer => "只读查看，不能新增或修改",
            _ => "只读查看",
        };

        /// <summary>获取成员列表；首次访问时补种账套创建者（管理员）。</summary>
        public async Task<IReadOnlyList<AccountSetMemberDto>> GetAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var members = await _memberRepository.GetMembersAsync(accountSetId, cancellationToken);
            if (members.Count == 0)
            {
                var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                    ?? throw new MemberException("账套不存在。");
                var ownerName = string.IsNullOrWhiteSpace(accountSet.CompanyName) ? accountSet.Name : accountSet.CompanyName;
                var owner = AccountSetMember.CreateOwner(accountSetId, ownerName);
                await _memberRepository.AddAsync(owner, cancellationToken);
                await _memberRepository.SaveChangesAsync(cancellationToken);
                members = await _memberRepository.GetMembersAsync(accountSetId, cancellationToken);
            }

            return members.Select(ToDto).ToList();
        }

        /// <summary>新增成员。</summary>
        public async Task<AccountSetMemberDto> CreateAsync(Guid accountSetId, SaveAccountSetMemberRequest request, CancellationToken cancellationToken = default)
        {
            Validate(request);
            var existing = await _memberRepository.GetMembersAsync(accountSetId, cancellationToken);
            var phone = request.Phone.Trim();
            if (phone.Length > 0 && existing.Any(m => m.Phone == phone))
            {
                throw new MemberException($"手机号 {phone} 已存在于本账套成员中。");
            }

            var member = AccountSetMember.Create(accountSetId, request.Name, phone, request.Email, request.Role);
            await _memberRepository.AddAsync(member, cancellationToken);
            await _memberRepository.SaveChangesAsync(cancellationToken);
            return ToDto(member);
        }

        /// <summary>修改成员资料与角色。</summary>
        public async Task<AccountSetMemberDto> UpdateAsync(Guid accountSetId, Guid id, SaveAccountSetMemberRequest request, CancellationToken cancellationToken = default)
        {
            Validate(request);
            var member = await _memberRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                ?? throw new MemberException("成员不存在。");
            if (member.IsOwner && request.Role != AccountSetMemberRole.Admin)
            {
                throw new MemberException("账套创建者必须保持「管理员」角色。");
            }

            member.Update(request.Name, request.Phone ?? string.Empty, request.Email, request.Role);
            await _memberRepository.SaveChangesAsync(cancellationToken);
            return ToDto(member);
        }

        /// <summary>启用 / 停用成员。</summary>
        public async Task<AccountSetMemberDto> SetEnabledAsync(Guid accountSetId, Guid id, bool enabled, CancellationToken cancellationToken = default)
        {
            var member = await _memberRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                ?? throw new MemberException("成员不存在。");
            if (member.IsOwner && !enabled)
            {
                throw new MemberException("账套创建者不能被停用。");
            }

            member.SetEnabled(enabled);
            await _memberRepository.SaveChangesAsync(cancellationToken);
            return ToDto(member);
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

        private static AccountSetMemberDto ToDto(AccountSetMember member) => new()
        {
            Id = member.Id,
            Name = member.Name,
            Phone = member.Phone,
            Email = member.Email,
            Role = member.Role,
            RoleText = RoleText(member.Role),
            PermissionText = PermissionText(member.Role),
            Enabled = member.Enabled,
            IsOwner = member.IsOwner,
            CreatedAt = member.CreatedAt,
        };
    }
}
