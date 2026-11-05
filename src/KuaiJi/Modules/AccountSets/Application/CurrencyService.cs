using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.AccountSets.Application
{
    /// <summary>币别相关业务错误。</summary>
    public sealed class SettingsException : Exception
    {
        public SettingsException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 币别应用服务：列表 / 新建 / 修改 / 删除。首次访问自动补种人民币本位币；
    /// 每个账套有且仅有一个本位币，删除本位币前须先将其他币别设为本位币。
    /// </summary>
    public sealed class CurrencyService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly ICurrencyRepository _currencyRepository;

        public CurrencyService(
            IAccountSetRepository accountSetRepository,
            ICurrencyRepository currencyRepository)
        {
            _accountSetRepository = accountSetRepository;
            _currencyRepository = currencyRepository;
        }

        /// <summary>获取全部币别；账套还没有币别时自动补种人民币本位币。</summary>
        public async Task<IReadOnlyList<CurrencyDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var currencies = await _currencyRepository.GetAllAsync(accountSetId, cancellationToken);
            if (currencies.Count == 0)
            {
                var rmb = Currency.Create(accountSetId, "RMB", "人民币", 1m, CurrencyConvertType.Multiply, true);
                await _currencyRepository.AddAsync(rmb, cancellationToken);
                await _currencyRepository.SaveChangesAsync(cancellationToken);
                currencies = [rmb];
            }

            return currencies
                .OrderByDescending(c => c.IsBase)
                .ThenBy(c => c.Code)
                .Select(CurrencyDto.FromDomain)
                .ToList();
        }

        /// <summary>新建币别。</summary>
        public async Task<CurrencyDto> CreateAsync(Guid accountSetId, SaveCurrencyRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var code = request.Code?.Trim() ?? string.Empty;
            if (await _currencyRepository.ExistsByCodeAsync(accountSetId, code, cancellationToken: cancellationToken))
            {
                throw new SettingsException($"币别「{code}」已存在。");
            }

            Currency currency;
            try
            {
                currency = Currency.Create(accountSetId, code, request.Name, request.Rate, request.ConvertType, request.IsBase);
            }
            catch (ArgumentException ex)
            {
                throw new SettingsException(ex.Message);
            }

            if (request.IsBase)
            {
                await ClearBaseFlagAsync(accountSetId, null, cancellationToken);
            }

            await _currencyRepository.AddAsync(currency, cancellationToken);
            await _currencyRepository.SaveChangesAsync(cancellationToken);
            return CurrencyDto.FromDomain(currency);
        }

        /// <summary>修改币别；不存在时返回 null。</summary>
        public async Task<CurrencyDto?> UpdateAsync(Guid accountSetId, Guid id, SaveCurrencyRequest request, CancellationToken cancellationToken = default)
        {
            var currency = await _currencyRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (currency is null)
            {
                return null;
            }

            var code = request.Code?.Trim() ?? string.Empty;
            if (await _currencyRepository.ExistsByCodeAsync(accountSetId, code, currency.Id, cancellationToken))
            {
                throw new SettingsException($"币别「{code}」已存在。");
            }

            try
            {
                currency.Update(code, request.Name, request.Rate, request.ConvertType, request.IsBase);
            }
            catch (ArgumentException ex)
            {
                throw new SettingsException(ex.Message);
            }

            if (request.IsBase)
            {
                await ClearBaseFlagAsync(accountSetId, currency.Id, cancellationToken);
            }

            await _currencyRepository.SaveChangesAsync(cancellationToken);
            return CurrencyDto.FromDomain(currency);
        }

        /// <summary>删除币别；本位币不可删除，不存在时返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var currency = await _currencyRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (currency is null)
            {
                return false;
            }

            if (currency.IsBase)
            {
                throw new SettingsException("本位币不可删除，请先将其他币别设为本位币。");
            }

            _currencyRepository.Remove(currency);
            await _currencyRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task ClearBaseFlagAsync(Guid accountSetId, Guid? excludeId, CancellationToken cancellationToken)
        {
            var all = await _currencyRepository.GetAllAsync(accountSetId, cancellationToken);
            foreach (var item in all.Where(c => c.IsBase && c.Id != excludeId))
            {
                item.Update(item.Code, item.Name, item.Rate, item.ConvertType, false);
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
