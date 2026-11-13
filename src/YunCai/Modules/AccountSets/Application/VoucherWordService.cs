using YunCai.Modules.AccountSets.Domain;

namespace YunCai.Modules.AccountSets.Application
{
    /// <summary>
    /// 凭证字应用服务：列表 / 新建 / 修改 / 删除。首次访问按账套配置的凭证字补种；
    /// 默认凭证字有且仅有一个（设默认时自动取消其他默认）。
    /// 账套上的 <see cref="AccountSet.VoucherWords"/> 逗号串是制单下拉的数据源，
    /// 每次改动后都从这里回写，避免设置页与制单页各看各的。
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

        /// <summary>获取全部凭证字；为空时按账套配置的凭证字补种。</summary>
        public async Task<IReadOnlyList<VoucherWordDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var accountSet = await GetAccountSetAsync(accountSetId, cancellationToken);
            var words = await _wordRepository.GetAllAsync(accountSetId, cancellationToken);
            if (words.Count == 0)
            {
                words = accountSet.VoucherWordList()
                    .Select((word, index) => VoucherWord.Create(accountSetId, word, DefaultPrintTitle(word), index == 0))
                    .ToList();
                foreach (var seed in words)
                {
                    await _wordRepository.AddAsync(seed, cancellationToken);
                }

                await _wordRepository.SaveChangesAsync(cancellationToken);
            }

            await SyncAccountSetWordsAsync(accountSet, words, cancellationToken);

            return words
                .OrderByDescending(w => w.IsDefault)
                .ThenBy(w => w.Word)
                .Select(VoucherWordDto.FromDomain)
                .ToList();
        }

        /// <summary>新建凭证字。</summary>
        public async Task<VoucherWordDto> CreateAsync(Guid accountSetId, SaveVoucherWordRequest request, CancellationToken cancellationToken = default)
        {
            var accountSet = await GetAccountSetAsync(accountSetId, cancellationToken);
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
                await _wordRepository.ClearDefaultAsync(accountSetId, cancellationToken: cancellationToken);
            }

            await _wordRepository.AddAsync(entity, cancellationToken);
            await _wordRepository.SaveChangesAsync(cancellationToken);
            await SyncAccountSetWordsAsync(accountSet, await _wordRepository.GetAllAsync(accountSetId, cancellationToken), cancellationToken);
            return VoucherWordDto.FromDomain(entity);
        }

        /// <summary>修改凭证字；不存在时返回 null。</summary>
        public async Task<VoucherWordDto?> UpdateAsync(Guid accountSetId, Guid id, SaveVoucherWordRequest request, CancellationToken cancellationToken = default)
        {
            var accountSet = await GetAccountSetAsync(accountSetId, cancellationToken);
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

            // 取消唯一的默认会让制单失去默认凭证字，改为要求先把别的凭证字设为默认。
            if (!request.IsDefault && entity.IsDefault
                && !(await _wordRepository.GetAllAsync(accountSetId, cancellationToken))
                    .Any(w => w.IsDefault && w.Id != entity.Id))
            {
                throw new SettingsException("至少需要保留一个默认凭证字，请先将其他凭证字设为默认。");
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
                await _wordRepository.ClearDefaultAsync(accountSetId, entity.Id, cancellationToken);
            }

            await _wordRepository.SaveChangesAsync(cancellationToken);
            await SyncAccountSetWordsAsync(accountSet, await _wordRepository.GetAllAsync(accountSetId, cancellationToken), cancellationToken);
            return VoucherWordDto.FromDomain(entity);
        }

        /// <summary>删除凭证字；默认凭证字不可删除，不存在时返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var accountSet = await GetAccountSetAsync(accountSetId, cancellationToken);
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
            await SyncAccountSetWordsAsync(accountSet, await _wordRepository.GetAllAsync(accountSetId, cancellationToken), cancellationToken);
            return true;
        }

        /// <summary>把凭证字表回写成账套的逗号串（默认在前），制单下拉据此取值。</summary>
        private async Task SyncAccountSetWordsAsync(
            AccountSet accountSet,
            IReadOnlyList<VoucherWord> words,
            CancellationToken cancellationToken)
        {
            var joined = string.Join(',', words
                .OrderByDescending(w => w.IsDefault)
                .ThenBy(w => w.Word)
                .Select(w => w.Word));
            if (joined.Length == 0 || string.Equals(accountSet.VoucherWords, joined, StringComparison.Ordinal))
            {
                return;
            }

            accountSet.SetVoucherWords(joined);
            await _accountSetRepository.SaveChangesAsync(cancellationToken);
        }

        /// <summary>补种时的默认打印标题：常用四个字沿用习惯叫法，其余用「X凭证」。</summary>
        private static string DefaultPrintTitle(string word) => word switch
        {
            "记" => "记账凭证",
            "收" => "收款凭证",
            "付" => "付款凭证",
            "转" => "转账凭证",
            _ => $"{word}凭证",
        };

        private async Task<AccountSet> GetAccountSetAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new SettingsException("指定的账套不存在。");
            }

            return await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                ?? throw new SettingsException("指定的账套不存在。");
        }
    }
}
