using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.Accounting.Application
{
    /// <summary>期初余额业务中的可预期错误（如账套不存在、科目非末级），用于向前端返回友好提示。</summary>
    public sealed class OpeningBalanceException : Exception
    {
        public OpeningBalanceException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 期初余额应用服务：查询与批量保存账套启用期间的科目期初余额。
    /// 余额录在末级科目上（按余额方向为正），非末级科目由下级汇总；
    /// 合计按借贷拆列并给出试算平衡结论（借方合计 = 贷方合计）。
    /// </summary>
    public sealed class OpeningBalanceService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;

        public OpeningBalanceService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
        }

        /// <summary>获取指定账套的期初余额表（全部科目，按编码排序，含试算平衡合计）。</summary>
        public async Task<OpeningBalancesDto> GetAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return Build(accounts);
        }

        /// <summary>
        /// 批量保存期初余额：仅末级科目允许录入，金额按科目余额方向为正（反方向余额用负数）。
        /// 保存后返回最新的期初余额表。
        /// </summary>
        public async Task<OpeningBalancesDto> SaveAsync(Guid accountSetId, SaveOpeningBalancesRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);

            foreach (var item in request.Items ?? Array.Empty<OpeningBalanceItem>())
            {
                if (!accountsById.TryGetValue(item.AccountId, out var account))
                {
                    throw new OpeningBalanceException("存在不属于当前账套的科目，请刷新后重试。");
                }

                try
                {
                    account.SetOpeningBalance(item.Amount);
                }
                catch (InvalidOperationException ex)
                {
                    throw new OpeningBalanceException(ex.Message);
                }
            }

            await _accountRepository.SaveChangesAsync(cancellationToken);
            return Build(accounts);
        }

        /// <summary>构建期初余额表：末级取自身余额，非末级按下级末级科目净额向上汇总。</summary>
        private static OpeningBalancesDto Build(IReadOnlyList<Account> accounts)
        {
            // 净借方余额（借-贷视角）：末级科目按方向换算，再沿上级链路向上累计。
            var netDebitById = new Dictionary<Guid, decimal>();
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);

            foreach (var account in accounts.Where(a => a.IsLeaf))
            {
                var netDebit = account.Direction == BalanceDirection.Debit
                    ? account.OpeningBalance
                    : -account.OpeningBalance;

                var current = account;
                while (current is not null)
                {
                    netDebitById[current.Id] = netDebitById.GetValueOrDefault(current.Id) + netDebit;
                    current = current.ParentId is { } parentId ? accountsById.GetValueOrDefault(parentId) : null;
                }
            }

            var rows = accounts
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .Select(a =>
                {
                    var netDebit = netDebitById.GetValueOrDefault(a.Id);
                    return new OpeningBalanceRowDto
                    {
                        AccountId = a.Id,
                        Code = a.Code,
                        Name = a.Name,
                        Category = a.Category,
                        Direction = a.Direction,
                        Level = a.Level,
                        IsLeaf = a.IsLeaf,
                        OpeningBalance = a.IsLeaf
                            ? a.OpeningBalance
                            : (a.Direction == BalanceDirection.Debit ? netDebit : -netDebit),
                        Debit = netDebit > 0m ? netDebit : 0m,
                        Credit = netDebit < 0m ? -netDebit : 0m,
                    };
                })
                .ToList();

            // 合计只统计末级科目，避免与上级汇总重复计数。
            var totalDebit = 0m;
            var totalCredit = 0m;
            foreach (var account in accounts.Where(a => a.IsLeaf))
            {
                var netDebit = netDebitById.GetValueOrDefault(account.Id);
                if (netDebit > 0m)
                {
                    totalDebit += netDebit;
                }
                else
                {
                    totalCredit += -netDebit;
                }
            }

            return new OpeningBalancesDto
            {
                Rows = rows,
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                Balanced = totalDebit == totalCredit,
            };
        }

        private async Task EnsureAccountSetExists(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new OpeningBalanceException("指定的账套不存在。");
            }
        }
    }
}
