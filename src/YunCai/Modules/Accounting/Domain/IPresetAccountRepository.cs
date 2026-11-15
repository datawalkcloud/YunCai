using YunCai.Modules.AccountSets.Domain;

namespace YunCai.Modules.Accounting.Domain
{
    /// <summary>预置科目清单（<c>preset_accounts</c>）的读取入口。清单是参考数据，运行期只读。</summary>
    public interface IPresetAccountRepository
    {
        /// <summary>取某会计准则的全部预置科目，按清单原序返回（上级先于下级）。</summary>
        Task<IReadOnlyList<PresetAccount>> GetByStandardAsync(AccountingStandard standard, CancellationToken cancellationToken = default);
    }
}
