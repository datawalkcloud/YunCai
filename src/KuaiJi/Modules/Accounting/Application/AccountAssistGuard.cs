using KuaiJi.Modules.Assist.Application;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Accounting.Application
{
    /// <summary>
    /// 待校验的一条分录：记账科目及其挂载的辅助核算取值。
    /// 借贷金额与辅助核算校验无关，故不进入本记录。
    /// </summary>
    /// <param name="Account">该分录使用的会计科目。</param>
    /// <param name="Assists">该分录挂载的辅助核算取值（可为空集合）。</param>
    public sealed record AssistLine(Account Account, IReadOnlyCollection<AssistRef> Assists);

    /// <summary>
    /// 凭证分录的辅助核算校验器（设计文档 A2 的规则集中于此，供凭证服务在制单/编辑时调用）：
    /// 一、分录挂的维度必须属于该科目已设置的辅助核算维度；
    /// 二、科目中 IsRequired 的维度必须至少挂 1 个档案；
    /// 三、档案类别须与挂载维度一致，非末级档案与已停用档案不可记账；
    /// 四、档案必须属于凭证所在账套（Guid 外键最容易出现的跨账套问题）；
    /// 五、现金流维度只允许挂在现金/银行存款/其他货币资金类分录上；
    /// 六、未在账套启用的类别不可挂载。
    /// 账套未启用任何类别时，本校验对不含辅助核算的分录完全无副作用（零回归）。
    /// 校验失败抛 <see cref="AssistanceException"/>，由调用方转换为友好提示。
    /// </summary>
    public sealed class AccountAssistGuard
    {
        /// <summary>
        /// 现金类科目编码前缀：1001（库存现金/现金）、1002（银行存款）、1009 与 1012（其他货币资金）。
        /// 这里按编码前缀而不是 <see cref="Account.Category"/> 判定，是因为 Category 只区分资产/负债等大类，
        /// 无法把现金类从其他资产中分离出来；同时新旧准则的编码首位含义不同
        /// （旧《企业会计制度》3=权益、4=成本，新准则 4=权益、5=成本），因此不看编码首位，
        /// 只按前四位匹配——两套体系的现金与银行存款均以 1001/1002 开头，
        /// 其他货币资金在两套体系下编号不同（1009 与 1012），一并纳入。
        /// 明细科目（如 100201 工商银行）因父编码为前缀的既有不变量而自然命中。
        /// </summary>
        private static readonly string[] CashAccountCodePrefixes = ["1001", "1002", "1009", "1012"];

        /// <summary>
        /// 按设计文档 A2 校验一批分录。
        /// </summary>
        /// <param name="accountSet">凭证所属账套（提供已启用类别与账套标识）。</param>
        /// <param name="assistItems">该账套的辅助核算档案（用于档案存在性、类别、末级与启用状态校验）。</param>
        /// <param name="lines">待校验的分录。</param>
        public void Validate(
            AccountSet accountSet,
            IEnumerable<AssistItem> assistItems,
            IReadOnlyCollection<AssistLine> lines)
        {
            ArgumentNullException.ThrowIfNull(accountSet);
            ArgumentNullException.ThrowIfNull(assistItems);
            ArgumentNullException.ThrowIfNull(lines);

            if (lines.Count == 0)
            {
                return;
            }

            var enabledTypes = accountSet.EnabledAssistTypeList();
            var itemsById = ToItemDictionary(assistItems);

            foreach (var line in lines)
            {
                var account = line.Account;
                // 只在账套已启用的类别范围内解释科目设置：类别被关闭后科目上的残留配置不再参与制单校验。
                var configured = account.AssistTypes
                    .Where(setting => enabledTypes.Contains(setting.Type))
                    .ToList();
                var references = line.Assists;

                foreach (var reference in references)
                {
                    if (!enabledTypes.Contains(reference.Type))
                    {
                        throw new AssistanceException($"账套未启用「{AssistTypeText.Of(reference.Type)}」辅助核算，不能用于记账。");
                    }

                    if (!configured.Any(setting => setting.Type == reference.Type))
                    {
                        throw new AssistanceException($"科目「{account.Code} {account.Name}」未设置「{AssistTypeText.Of(reference.Type)}」辅助核算。");
                    }
                }

                foreach (var setting in configured.Where(setting => setting.IsRequired))
                {
                    if (!references.Any(reference => reference.Type == setting.Type))
                    {
                        throw new AssistanceException($"科目「{account.Code} {account.Name}」的「{AssistTypeText.Of(setting.Type)}」辅助核算为必录项，请至少选择一个档案。");
                    }
                }

                if (references.Any(reference => reference.Type == AssistType.CashFlow) && !IsCashAccount(account))
                {
                    throw new AssistanceException(
                        $"「现金流」辅助核算只能挂在现金/银行存款/其他货币资金类分录上，科目「{account.Code} {account.Name}」不属于此类。");
                }

                foreach (var reference in references)
                {
                    var item = itemsById.GetValueOrDefault(reference.ItemId);
                    if (item is null || item.AccountSetId != accountSet.Id)
                    {
                        throw new AssistanceException($"辅助核算档案不存在或不属于账套「{accountSet.Name}」，请重新选择。");
                    }

                    if (item.Type != reference.Type)
                    {
                        throw new AssistanceException(
                            $"档案「{item.Code} {item.Name}」属于「{AssistTypeText.Of(item.Type)}」，不能作为科目「{account.Code} {account.Name}」的「{AssistTypeText.Of(reference.Type)}」记账。");
                    }

                    if (!item.IsLeaf)
                    {
                        throw new AssistanceException($"档案「{item.Code} {item.Name}」不是末级档案，不能用于记账。");
                    }

                    if (!item.IsEnabled)
                    {
                        throw new AssistanceException($"档案「{item.Code} {item.Name}」已停用，不能用于新增凭证。");
                    }
                }
            }
        }

        /// <summary>判定科目是否属于现金类（库存现金/银行存款/其他货币资金）。</summary>
        private static bool IsCashAccount(Account account) =>
            CashAccountCodePrefixes.Any(prefix => account.Code.StartsWith(prefix, StringComparison.Ordinal));

        /// <summary>按档案标识建立索引；同一标识重复出现时保留第一条。</summary>
        private static IReadOnlyDictionary<Guid, AssistItem> ToItemDictionary(IEnumerable<AssistItem> assistItems)
        {
            var dictionary = new Dictionary<Guid, AssistItem>();
            foreach (var item in assistItems)
            {
                dictionary.TryAdd(item.Id, item);
            }

            return dictionary;
        }
    }
}
