using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Assist.Application
{
    /// <summary>辅助核算业务中的可预期错误（如类别未启用、编码重复、档案被引用），用于向前端返回友好提示。</summary>
    public sealed class AssistanceException : Exception
    {
        public AssistanceException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 辅助核算档案应用服务：面向「辅助核算」页的类别概览、档案列表与增删改、启停，
    /// 以及科目维度设置与账套级类别开关。
    /// 档案归属于账套与某一类别，编码在「账套 + 类别」内唯一；已有下级或已被凭证分录引用的档案不可删除，
    /// 只能停用（停用后历史凭证与账簿仍保留该档案）。
    /// </summary>
    public sealed class AssistItemService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IAssistItemRepository _assistItemRepository;

        public AssistItemService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IAssistItemRepository assistItemRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _assistItemRepository = assistItemRepository;
        }

        /// <summary>获取七个辅助核算类别的概览：中文名、本账套是否启用、该类别下的档案数。</summary>
        public async Task<IReadOnlyList<AssistCategoryStatDto>> GetCategoriesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            var counts = await _assistItemRepository.CountByTypeAsync(accountSetId, cancellationToken);
            var enabled = accountSet.EnabledAssistTypeList();

            return AssistTypeText.All
                .Select(type => new AssistCategoryStatDto
                {
                    Type = type,
                    TypeText = AssistTypeText.Of(type),
                    IsEnabled = enabled.Contains(type),
                    ItemCount = counts.GetValueOrDefault(type),
                })
                .ToList();
        }

        /// <summary>获取指定类别的全部档案（按编码排序，含非末级与已停用者，前端自行组树）。</summary>
        public async Task<IReadOnlyList<AssistItemDto>> GetAllAsync(Guid accountSetId, AssistType type, CancellationToken cancellationToken = default)
        {
            await LoadAccountSet(accountSetId, cancellationToken);
            EnsureTypeDefined(type);

            var items = await _assistItemRepository.GetByTypeAsync(accountSetId, type, cancellationToken);
            return items.Select(AssistItemDto.FromDomain).ToList();
        }

        /// <summary>
        /// 获取指定类别下的直接下级档案；parentId 为 null 时返回顶级档案。
        /// 无层级类别（客户/供应商/职员）指定 parentId 会报错。
        /// </summary>
        public async Task<IReadOnlyList<AssistItemDto>> GetChildrenAsync(Guid accountSetId, AssistType type, Guid? parentId, CancellationToken cancellationToken = default)
        {
            await LoadAccountSet(accountSetId, cancellationToken);
            EnsureTypeDefined(type);
            await EnsureParentUsable(accountSetId, type, parentId, cancellationToken);

            var items = await _assistItemRepository.GetByTypeAsync(accountSetId, type, cancellationToken);
            return items
                .Where(i => i.ParentId == parentId)
                .Select(AssistItemDto.FromDomain)
                .ToList();
        }

        /// <summary>
        /// 给出新增表单的建议编码：层级型类别在上级编码后补一段流水，无层级类别给 4 位流水号。
        /// 建议值仅供前端预填，保存时仍以实际校验为准。
        /// </summary>
        public async Task<string> NextCodeAsync(Guid accountSetId, AssistType type, Guid? parentId, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            EnsureTypeDefined(type);
            EnsureCategoryEnabled(accountSet, type);

            var parent = await EnsureParentUsable(accountSetId, type, parentId, cancellationToken);
            var items = await _assistItemRepository.GetByTypeAsync(accountSetId, type, cancellationToken);
            return AssistCodingScheme.NextCode(type, parent?.Code, items.Select(i => i.Code));
        }

        /// <summary>
        /// 新增档案：类别须已在账套启用，编码长度须符合该类别的编码方案（层级型须以上级编码为前缀），
        /// 且在同账套同类别内唯一。
        /// </summary>
        public async Task<AssistItemDto> CreateAsync(Guid accountSetId, CreateAssistItemRequest request, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            EnsureTypeDefined(request.Type);
            EnsureCategoryEnabled(accountSet, request.Type);

            var code = request.Code?.Trim() ?? string.Empty;
            var name = request.Name?.Trim() ?? string.Empty;
            if (code.Length == 0 || name.Length == 0)
            {
                throw new AssistanceException("请填写档案编码和名称。");
            }

            if (code.Length > AssistItem.MaxCodeLength)
            {
                throw new AssistanceException($"档案编码不能超过 {AssistItem.MaxCodeLength} 个字符。");
            }

            var typeText = AssistTypeText.Of(request.Type);
            var parent = await EnsureParentUsable(accountSetId, request.Type, request.ParentId, cancellationToken);
            var newLevel = parent is null ? 1 : parent.Level + 1;
            var expectedLength = AssistCodingScheme.CodeLengthFor(request.Type, newLevel);
            if (code.Length != expectedLength)
            {
                throw new AssistanceException(
                    parent is null
                        ? $"「{typeText}」顶级档案编码应为 {expectedLength} 位，当前编码「{code}」为 {code.Length} 位。"
                        : $"「{typeText}」第 {newLevel} 级档案编码应为 {expectedLength} 位（上级编码 + 每级 {AssistCodingScheme.HierarchyLevelLength} 位），当前编码「{code}」为 {code.Length} 位。");
            }

            if (await _assistItemRepository.ExistsByCodeAsync(accountSetId, request.Type, code, cancellationToken))
            {
                throw new AssistanceException($"档案编码「{code}」在「{typeText}」类别下已存在。");
            }

            AssistItem item;
            try
            {
                item = parent is null
                    ? AssistItem.Create(accountSetId, request.Type, code, name)
                    : parent.AddChild(code, name);
            }
            catch (ArgumentException ex)
            {
                throw new AssistanceException(ex.Message);
            }

            item.SetContact(request.Contact);
            item.SetRemark(request.Remark);

            // 经上级 AddChild 创建的子级同样需要登记；仓储对已追踪实体不会重复添加。
            await _assistItemRepository.AddAsync(item, cancellationToken);
            await _assistItemRepository.SaveChangesAsync(cancellationToken);

            return AssistItemDto.FromDomain(item);
        }

        /// <summary>修改档案的名称、联系方式与备注（编码与所属类别不可变更）；不存在时返回 null。</summary>
        public async Task<AssistItemDto?> UpdateAsync(Guid accountSetId, Guid id, UpdateAssistItemRequest request, CancellationToken cancellationToken = default)
        {
            var item = await _assistItemRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (item is null)
            {
                return null;
            }

            try
            {
                if (request.Name is not null)
                {
                    item.Rename(request.Name);
                }

                if (request.Contact is not null)
                {
                    item.SetContact(request.Contact);
                }

                if (request.Remark is not null)
                {
                    item.SetRemark(request.Remark);
                }
            }
            catch (ArgumentException ex)
            {
                throw new AssistanceException(ex.Message);
            }

            await _assistItemRepository.SaveChangesAsync(cancellationToken);
            return AssistItemDto.FromDomain(item);
        }

        /// <summary>启用/停用档案；停用只影响新凭证选用，历史凭证与账簿仍保留该档案。不存在时返回 null。</summary>
        public async Task<AssistItemDto?> SetEnabledAsync(Guid accountSetId, Guid id, bool enabled, CancellationToken cancellationToken = default)
        {
            var item = await _assistItemRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (item is null)
            {
                return null;
            }

            if (enabled)
            {
                item.Enable();
            }
            else
            {
                item.Disable();
            }

            await _assistItemRepository.SaveChangesAsync(cancellationToken);
            return AssistItemDto.FromDomain(item);
        }

        /// <summary>
        /// 删除档案：存在下级或已被凭证分录引用时拒绝（改提示为停用），与预置科目不可删除属同一类保护。
        /// 档案不存在时返回 false。
        /// </summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var item = await _assistItemRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (item is null)
            {
                return false;
            }

            if (await _assistItemRepository.HasChildrenAsync(accountSetId, id, cancellationToken))
            {
                throw new AssistanceException("该档案下仍有下级档案，请先删除下级档案。");
            }

            var references = await _assistItemRepository.CountVoucherReferencesAsync(accountSetId, id, cancellationToken);
            if (references > 0)
            {
                throw new AssistanceException($"该档案已被 {references} 条分录引用，请改用停用。");
            }

            _assistItemRepository.Remove(item);
            await _assistItemRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// 设置账套启用的辅助核算类别（整体覆盖，传空集合表示关闭辅助核算）。
        /// 已建档案的类别不允许关闭，否则历史分录会失去维度。
        /// </summary>
        public async Task<AccountSet> SetAccountSetAssistTypesAsync(Guid accountSetId, IReadOnlyCollection<AssistType> types, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(types);

            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            foreach (var type in types)
            {
                EnsureTypeDefined(type);
            }

            var wanted = types.Distinct().ToList();
            var counts = await _assistItemRepository.CountByTypeAsync(accountSetId, cancellationToken);
            foreach (var pair in counts)
            {
                if (pair.Value > 0 && !wanted.Contains(pair.Key))
                {
                    throw new AssistanceException($"「{AssistTypeText.Of(pair.Key)}」类别下已有 {pair.Value} 个档案，不能关闭；请先删除这些档案。");
                }
            }

            accountSet.SetEnabledAssistTypes(wanted);
            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            return accountSet;
        }

        /// <summary>获取科目已设置的辅助核算维度（含是否必录）。</summary>
        public async Task<IReadOnlyList<AccountAssistTypeDto>> GetAccountAssistTypesAsync(Guid accountSetId, Guid accountId, CancellationToken cancellationToken = default)
        {
            var account = await _accountRepository.GetByIdWithAssistTypesAsync(accountSetId, accountId, cancellationToken)
                          ?? throw new AssistanceException("指定的科目不存在。");

            return account.AssistTypes.Select(AccountAssistTypeDto.FromDomain).ToList();
        }

        /// <summary>
        /// 整体覆盖科目的辅助核算维度设置（同一类别只保留一条）。
        /// 待设置的类别必须已在账套启用；科目未设置的维度在制单时不可挂档案。
        /// </summary>
        public async Task<IReadOnlyList<AccountAssistTypeDto>> SetAccountAssistTypesAsync(
            Guid accountSetId,
            Guid accountId,
            IReadOnlyCollection<AccountAssistTypeRequest> settings,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            var enabled = accountSet.EnabledAssistTypeList();
            var account = await _accountRepository.GetByIdWithAssistTypesAsync(accountSetId, accountId, cancellationToken)
                          ?? throw new AssistanceException("指定的科目不存在。");

            // 同一类别重复提交时以最后一条为准。
            var wanted = new Dictionary<AssistType, bool>();
            foreach (var setting in settings)
            {
                EnsureTypeDefined(setting.Type);
                if (!enabled.Contains(setting.Type))
                {
                    throw new AssistanceException($"账套未启用「{AssistTypeText.Of(setting.Type)}」辅助核算，请先在账套设置中开启该类别。");
                }

                wanted[setting.Type] = setting.IsRequired;
            }

            // account_assist_types 上有 (account_id, type) 唯一索引，而一次 SaveChanges 内新增先于删除执行，
            // 直接覆盖会让新旧两行撞在同一索引键上，故分两次提交：先清空旧的维度设置，再写入新的。
            account.SetAssistTypes([]);
            await _accountRepository.SaveChangesAsync(cancellationToken);

            account.SetAssistTypes(wanted.Select(pair => (pair.Key, pair.Value)).ToList());
            await _accountRepository.SaveChangesAsync(cancellationToken);

            return account.AssistTypes.Select(AccountAssistTypeDto.FromDomain).ToList();
        }

        /// <summary>加载账套（类别启用状态与编码方案都挂在账套上）；不存在时抛出友好错误。</summary>
        private async Task<AccountSet> LoadAccountSet(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = accountSetId == Guid.Empty
                ? null
                : await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            return accountSet ?? throw new AssistanceException("指定的账套不存在。");
        }

        /// <summary>校验类别取值合法（七类内置维度之外的值一律拒绝）。</summary>
        private static void EnsureTypeDefined(AssistType type)
        {
            if (!Enum.IsDefined(type))
            {
                throw new AssistanceException("辅助核算类别无效。");
            }
        }

        /// <summary>校验该类别已在账套启用。</summary>
        private static void EnsureCategoryEnabled(AccountSet accountSet, AssistType type)
        {
            if (!accountSet.EnabledAssistTypeList().Contains(type))
            {
                throw new AssistanceException($"账套未启用「{AssistTypeText.Of(type)}」辅助核算，请先在账套设置中开启该类别。");
            }
        }

        /// <summary>
        /// 校验上级档案可用（类别支持层级、上级存在且属于同一类别），返回上级档案；parentId 为 null 时返回 null。
        /// </summary>
        private async Task<AssistItem?> EnsureParentUsable(Guid accountSetId, AssistType type, Guid? parentId, CancellationToken cancellationToken)
        {
            if (parentId is null)
            {
                return null;
            }

            if (!AssistCodingScheme.IsHierarchical(type))
            {
                throw new AssistanceException($"「{AssistTypeText.Of(type)}」类档案不支持层级，不能有上级档案。");
            }

            var parent = await _assistItemRepository.GetByIdAsync(accountSetId, parentId.Value, cancellationToken)
                         ?? throw new AssistanceException("指定的上级档案不存在。");

            if (parent.Type != type)
            {
                throw new AssistanceException($"上级档案「{parent.Code} {parent.Name}」属于「{AssistTypeText.Of(parent.Type)}」，不能作为「{AssistTypeText.Of(type)}」的上级。");
            }

            return parent;
        }
    }
}
