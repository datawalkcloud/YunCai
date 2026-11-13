using System.Globalization;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Reports.Domain;
using YunCai.Modules.Reports.Infrastructure;
using YunCai.Modules.Vouchers.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using MiniExcelLibs;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Reports.Application
{
    /// <summary>
    /// 账套归档：把整个账套的核心账簿（科目余额、凭证清单、凭证分录）导出为
    /// Excel 工作簿或 PDF 文件，对应柠檬云「备份恢复 → 归档Excel / 归档PDF」。
    /// </summary>
    public sealed class AccountSetArchiveService
    {
        private readonly AppDbContext _context;

        public AccountSetArchiveService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>归档 Excel（多页签工作簿）。</summary>
        public async Task<(byte[] Content, string FileName)> BuildExcelAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var (accountSet, balances, voucherRows, entryRows) = await CollectAsync(accountSetId, cancellationToken);

            var infoRows = new List<Dictionary<string, object?>>
            {
                new() { ["项目"] = "账套名称", ["内容"] = accountSet.CompanyName.Length > 0 ? accountSet.CompanyName : accountSet.Name },
                new() { ["项目"] = "账套标识", ["内容"] = accountSet.Id.ToString() },
                new() { ["项目"] = "会计准则", ["内容"] = accountSet.Standard.ToString() },
                new() { ["项目"] = "本位币", ["内容"] = accountSet.BaseCurrency },
                new() { ["项目"] = "启用期间", ["内容"] = $"{accountSet.StartYear}年{accountSet.StartMonth:00}月" },
                new() { ["项目"] = "末级科目数", ["内容"] = balances.Count },
                new() { ["项目"] = "凭证数", ["内容"] = voucherRows.Count },
                new() { ["项目"] = "分录数", ["内容"] = entryRows.Count },
                new() { ["项目"] = "归档时间", ["内容"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
            };

            var balanceRows = balances.Select(b => new Dictionary<string, object?>
            {
                ["科目编码"] = b.Code,
                ["科目名称"] = b.Name,
                ["科目类别"] = b.Category,
                ["期初余额"] = b.Opening,
                ["累计借方"] = b.Debit,
                ["累计贷方"] = b.Credit,
                ["期末余额"] = b.Closing,
            }).ToList();

            var sheets = new Dictionary<string, object>
            {
                ["归档说明"] = infoRows,
                ["科目余额表"] = balanceRows,
                ["凭证清单"] = voucherRows,
                ["凭证分录"] = entryRows.Select(e => new Dictionary<string, object?>
                {
                    ["凭证日期"] = e.Date.ToString("yyyy-MM-dd"),
                    ["凭证字号"] = $"{e.Word}-{e.Number}",
                    ["摘要"] = e.Summary,
                    ["科目编码"] = e.AccountCode,
                    ["科目名称"] = e.AccountName,
                    ["借方金额"] = e.Debit,
                    ["贷方金额"] = e.Credit,
                }).ToList(),
            };

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, sheets, excelType: ExcelType.XLSX);
            return (stream.ToArray(), $"账套归档_{DateTime.Now:yyyyMMddHHmm}.xlsx");
        }

        /// <summary>归档 PDF。</summary>
        public async Task<(byte[] Content, string FileName)> BuildPdfAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var (accountSet, balances, _, entries) = await CollectAsync(accountSetId, cancellationToken);
            var content = ArchivePdfBuilder.Build(accountSet, balances, entries, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            return (content, $"账套归档_{DateTime.Now:yyyyMMddHHmm}.pdf");
        }

        private async Task<(AccountSet AccountSet, List<ArchivePdfBuilder.BalanceRow> Balances, List<Dictionary<string, object?>> VoucherRows, List<ArchivePdfBuilder.EntryRow> Entries)>
            CollectAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = await _context.AccountSets.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountSetId, cancellationToken)
                ?? throw new ReportException("账套不存在。");

            var accounts = await _context.Accounts.AsNoTracking()
                .Where(a => a.AccountSetId == accountSetId)
                .OrderBy(a => a.Code)
                .ToListAsync(cancellationToken);

            var vouchers = await _context.Vouchers.AsNoTracking()
                .Include(v => v.Entries)
                .Where(v => v.AccountSetId == accountSetId)
                .ToListAsync(cancellationToken);

            var live = vouchers.Where(v => v.DeletedAt is null).OrderBy(v => v.Date).ThenBy(v => v.Number).ToList();

            var opening = new Dictionary<Guid, decimal>();
            var debit = new Dictionary<Guid, decimal>();
            var credit = new Dictionary<Guid, decimal>();
            foreach (var voucher in live)
            {
                foreach (var entry in voucher.Entries)
                {
                    opening[entry.AccountId] = opening.GetValueOrDefault(entry.AccountId) + entry.Debit - entry.Credit;
                    debit[entry.AccountId] = debit.GetValueOrDefault(entry.AccountId) + entry.Debit;
                    credit[entry.AccountId] = credit.GetValueOrDefault(entry.AccountId) + entry.Credit;
                }
            }

            var balances = accounts.Where(a => a.IsLeaf).Select(a =>
            {
                var open = a.OpeningBalance + opening.GetValueOrDefault(a.Id);
                var d = debit.GetValueOrDefault(a.Id);
                var c = credit.GetValueOrDefault(a.Id);
                return new ArchivePdfBuilder.BalanceRow(a.Code, a.Name, CategoryText(a.Category), open, d, c, open + d - c);
            }).ToList();

            var accountById = accounts.ToDictionary(a => a.Id);
            var voucherRows = live.Select(v => new Dictionary<string, object?>
            {
                ["凭证日期"] = v.Date.ToString("yyyy-MM-dd"),
                ["凭证字号"] = $"{v.Word}-{v.Number}",
                ["附件数"] = v.AttachmentCount,
                ["制单人"] = v.Preparer,
                ["审核人"] = v.Reviewer ?? string.Empty,
                ["状态"] = StatusText(v.Status),
                ["分录数"] = v.Entries.Count,
                ["借方合计"] = Math.Round(v.Entries.Sum(e => e.Debit), 2),
                ["贷方合计"] = Math.Round(v.Entries.Sum(e => e.Credit), 2),
            }).ToList();

            var entries = live
                .SelectMany(v => v.Entries.OrderBy(e => e.Id).Select(e =>
                {
                    var account = accountById.GetValueOrDefault(e.AccountId);
                    return new ArchivePdfBuilder.EntryRow(
                        v.Date, v.Word, v.Number, e.Summary, account?.Code ?? string.Empty, account?.Name ?? string.Empty, e.Debit, e.Credit);
                }))
                .ToList();

            return (accountSet, balances, voucherRows, entries);
        }

        private static string CategoryText(AccountCategory category) => category switch
        {
            AccountCategory.Asset => "资产",
            AccountCategory.Liability => "负债",
            AccountCategory.Equity => "所有者权益",
            AccountCategory.Cost => "成本",
            AccountCategory.ProfitAndLoss => "损益",
            _ => "其他",
        };

        private static string StatusText(VoucherStatus status) => status switch
        {
            VoucherStatus.Draft => "暂存",
            VoucherStatus.Posted => "正常",
            _ => "未知",
        };
    }
}
