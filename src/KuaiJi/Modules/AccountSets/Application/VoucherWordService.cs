using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.AccountSets.Application
{
    /// <summary>
    /// 凭证字应用服务：列表 / 新建 / 修改 / 删除。首次访问按账套默认凭证字补种 记/收/付/转；
    /// 默认凭证字有且仅有一个（设默认时自动取消其他默认）。
    /// </summary>
    public sealed class VoucherWordService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IVoucherWordRepository _wordRepository;

        public VoucherWordService(
            IAccountSetRepository accountSetRepository,
            IVoucherWordRepository wordRepository)
        {
            _accountSetRepository = accountSetRepository;
            _wordRepository = wordRepository;
        }

        /// <summary>获取全部凭证字；为空时按账套配置的默认凭证字补种。</summary>
        public async Task<IReadOnlyList<VoucherWordDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var words = await _wordRepository.GetAllAsync(accountSetId, cancellationToken);
            if (words.Count == 0)
            {
                var seeds = new List<VoucherWord>
                {
                    VoucherWord.Create(accountSetId, "记", "记账凭证", true),
                    VoucherWord.Create(accountSetId, "收", "收款凭证", false),
                    VoucherWord.Create(accountSetId, "付", "付款凭证", false),
                    VoucherWord.Create(accountSetId, "转", "转账凭证", false),
                };
                foreach (var seed in seeds)
                {
                    await _wordRepository.AddAsync(seed, cancellationToken);
                }

                await _wordRepository.SaveChangesAsync(cancellationToken);
                words = seeds;
            }

            return words
                .OrderByDescending(w => w.IsDefault)
                .ThenBy(w => w.Word)
                .Select(VoucherWordDto.FromDomain)
                .ToList();
        }

        /// <summary>新建凭证字。</summary>
        public async Task<VoucherWordDto> CreateAsync(Guid accountSetId, SaveVoucherWordRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var word = request.Word?.Trim() ?? string.Empty;
            if (await _wordRepository.ExistsByWordAsync(accountSetId, word, cancellationToken: cancellationToken))
            {
                throw new SettingsException($"凭证字「{word}」已存在。");
            }

            VoucherWord entity;
            try
            {
                entity = VoucherWord.Create(accountSetId, word, request.PrintTitle, request.IsDefault);
            }
            catch (ArgumentException ex)
            {
                throw new SettingsException(ex.Message);
            }

            if (request.IsDefault)
            {
                await ClearDefaultAsync(accountSetId, null, cancellationToken);
            }

            await _wordRepository.AddAsync(entity, cancellationToken);
            await _wordRepository.SaveChangesAsync(cancellationToken);
            return VoucherWordDto.FromDomain(entity);
        }

        /// <summary>修改凭证字；不存在时返回 null。</summary>
        public async Task<VoucherWordDto?> UpdateAsync(Guid accountSetId, Guid id, SaveVoucherWordRequest request, CancellationToken cancellationToken = default)
        {
            var entity = await _wordRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (entity is null)
            {
                return null;
            }

            var word = request.Word?.Trim() ?? string.Empty;
            if (await _wordRepository.ExistsByWordAsync(accountSetId, word, entity.Id, cancellationToken))
            {
                throw new SettingsException($"凭证字「{word}」已存在。");
            }

            try
            {
                entity.Update(word, request.PrintTitle, request.IsDefault);
            }
            catch (ArgumentException ex)
            {
                throw new SettingsException(ex.Message);
            }

            if (request.IsDefault)
            {
                await ClearDefaultAsync(accountSetId, entity.Id, cancellationToken);
            }

            await _wordRepository.SaveChangesAsync(cancellationToken);
            return VoucherWordDto.FromDomain(entity);
        }

        /// <summary>删除凭证字；默认凭证字不可删除，不存在时返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _wordRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (entity is null)
            {
                return false;
            }

            if (entity.IsDefault)
            {
                throw new SettingsException("默认凭证字不可删除，请先将其他凭证字设为默认。");
            }

            _wordRepository.Remove(entity);
            await _wordRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task ClearDefaultAsync(Guid accountSetId, Guid? excludeId, CancellationToken cancellationToken)
        {
            var all = await _wordRepository.GetAllAsync(accountSetId, cancellationToken);
            foreach (var item in all.Where(w => w.IsDefault && w.Id != excludeId))
            {
                item.MarkNotDefault();
            }
        }

        private async Task EnsureAccountSetExistsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new SettingsException("指定的账套不存在。");
            }
        }
    }
}
