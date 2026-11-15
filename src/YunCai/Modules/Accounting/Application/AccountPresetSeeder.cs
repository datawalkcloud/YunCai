using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.AccountSets.Domain;

namespace YunCai.Modules.Accounting.Application
{
    /// <summary>
    /// 建账预置：按账套所采用的会计准则，从 <c>preset_accounts</c> 表读取初始科目清单为新账套生成科目。
    /// 与柠檬云新建账套的行为一致——建完即有整套科目，而不是空科目表。
    /// 清单本身是参考数据（企业会计准则 257 条、小企业会计准则 210 条），
    /// 由 <see cref="PresetChart"/> 经迁移写入，运行时不再回落到代码里的硬编码表。
    /// </summary>
    public static class AccountPresetSeeder
    {
        /// <summary>
        /// 为指定账套预置科目表；账套已有科目、或该准则没有预置清单时直接返回 0，不重复写入。
        /// 按编码长度排序写入，保证父级先建、子级后挂；上级一律取清单登记的父级编码。
        /// </summary>
        public static async Task<int> SeedAsync(
            Guid accountSetId,
            AccountingStandard standard,
            IPresetAccountRepository presetRepository,
            IAccountRepository accountRepository,
            CancellationToken cancellationToken = default)
        {
            var existing = await accountRepository.GetAllAsync(accountSetId, cancellationToken);
            if (existing.Count > 0)
            {
                return 0;
            }

            var preset = await presetRepository.GetByStandardAsync(standard, cancellationToken);

            var byCode = new Dictionary<string, Account>(StringComparer.Ordinal);
            var created = 0;

            foreach (var row in preset.OrderBy(r => r.Code.Length).ThenBy(r => r.OrderNo))
            {
                // 明细科目的类别继承上级（与领域模型一致），清单里的类别只用于兜底建顶级。
                Account account;
                if (row.ParentCode is { Length: > 0 } parentCode
                    && byCode.TryGetValue(parentCode, out var parent))
                {
                    account = parent.AddChild(row.Code, row.Name, row.Direction, isPreset: true);
                }
                else
                {
                    // 清单登记为顶级，或上级缺失（数据被改动）时退化为顶级科目，避免整表中断。
                    account = Account.CreateRoot(accountSetId, row.Code, row.Name, row.Category, row.Direction, isPreset: true);
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
