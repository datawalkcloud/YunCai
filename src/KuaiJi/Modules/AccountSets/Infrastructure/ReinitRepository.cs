using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.AccountSets.Infrastructure
{
    /// <summary>重新初始化仓储实现：批量清空账套业务数据（ExecuteDelete 直删）。</summary>
    public class ReinitRepository : IReinitRepository
    {
        private readonly AppDbContext _context;

        public ReinitRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>清空账套全部业务数据。科目：删除非预置科目（预置科目保留但清零期初余额）。</summary>
        public async Task<IReadOnlyDictionary<string, int>> WipeBusinessDataAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var wiped = new Dictionary<string, int>();

            wiped["凭证"] = await _context.Vouchers.Where(v => v.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["日记账"] = await _context.Journals.Where(j => j.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["转账"] = await _context.Transfers.Where(t => t.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["票据"] = await _context.Bills.Where(b => b.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["发票"] = await _context.Invoices.Where(i => i.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["工资单"] = await _context.SalaryBills.Where(b => b.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["员工"] = await _context.Employees.Where(e => e.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["五险一金"] = await _context.InsuranceItems.Where(i => i.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["资产"] = await _context.FixedAssets.Where(a => a.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["资产类别"] = await _context.FixedAssetCategories.Where(c => c.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["资产变动"] = await _context.FixedAssetChanges.Where(c => c.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["电子档案"] = await _context.ArchiveItems.Where(a => a.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["凭证模板"] = await _context.VoucherTemplates.Where(t => t.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["期末结转模板"] = await _context.ClosingTemplates.Where(t => t.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["收支类别"] = await _context.EntryCategories.Where(c => c.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);
            wiped["辅助核算档案"] = await _context.AssistItems.Where(a => a.AccountSetId == accountSetId).ExecuteDeleteAsync(cancellationToken);

            // 科目：预置科目保留并清零期初余额；非预置（新增）科目删除。
            var accounts = await _context.Accounts
                .Where(a => a.AccountSetId == accountSetId)
                .ToListAsync(cancellationToken);
            var presetIds = accounts.Where(a => a.IsPreset).Select(a => a.Id).ToList();
            wiped["非预置科目"] = await _context.Accounts
                .Where(a => a.AccountSetId == accountSetId && !a.IsPreset)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Accounts
                .Where(a => a.AccountSetId == accountSetId && presetIds.Contains(a.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.OpeningBalance, 0m), cancellationToken);

            return wiped;
        }

        public async Task ResetPeriodsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await _context.AccountSets
                .Where(s => s.Id == accountSetId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.CarriedPeriod, 0)
                    .SetProperty(a => a.ClosedPeriod, 0), cancellationToken);
        }
    }
}
