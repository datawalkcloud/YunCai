using YunCai.Modules.Accounting.Domain;

namespace YunCai.Modules.Accounting.Application
{
    /// <summary>
    /// 建账预置：按企业会计准则（2007 版）科目表为新账套生成科目。
    /// 与柠檬云新建账套的行为一致——建完即有整套 257 个科目，而不是空科目表。
    /// </summary>
    public static class AccountPresetSeeder
    {
        /// <summary>
        /// 为指定账套预置科目表；账套已有科目时直接返回，不重复写入。
        /// 按编码长度排序写入，保证父级先建、子级后挂。
        /// </summary>
        public static async Task<int> SeedAsync(
            Guid accountSetId,
            IAccountRepository accountRepository,
            CancellationToken cancellationToken = default)
        {
            var existing = await accountRepository.GetAllAsync(accountSetId, cancellationToken);
            if (existing.Count > 0)
            {
                return 0;
            }

            var byCode = new Dictionary<string, Account>(StringComparer.Ordinal);
            var created = 0;

            foreach (var row in DefaultChartOfAccounts.Rows
                         .OrderBy(r => r.Code.Length)
                         .ThenBy(r => r.Code, StringComparer.Ordinal))
            {
                var category = (AccountCategory)row.Category;
                var direction = (BalanceDirection)row.Direction;

                Account account;
                if (row.Code.Length == 4)
                {
                    account = Account.CreateRoot(accountSetId, row.Code, row.Name, category, direction, isPreset: true);
                }
                else
                {
                    var parentCode = row.Code.Length switch
                    {
                        7 => row.Code[..4],
                        9 => row.Code[..7],
                        11 => row.Code[..9],
                        _ => row.Code[..4],
                    };

                    if (!byCode.TryGetValue(parentCode, out var parent))
                    {
                        // 找不到父级时退化为顶级科目，避免整表中断。
                        account = Account.CreateRoot(accountSetId, row.Code, row.Name, category, direction, isPreset: true);
                    }
                    else
                    {
                        account = parent.AddChild(row.Code, row.Name, direction, isPreset: true);
                    }
                }

                byCode[row.Code] = account;
                await accountRepository.AddAsync(account, cancellationToken);
                created++;
            }

            await accountRepository.SaveChangesAsync(cancellationToken);
            return created;
        }
    }
}
