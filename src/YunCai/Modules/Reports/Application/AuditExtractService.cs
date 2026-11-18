using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Reports.Domain;
using YunCai.Modules.Vouchers.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using MiniExcelLibs;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Reports.Application
{
    /// <summary>
    /// 审计取数：把选定期间的科目余额表、凭证清单与凭证分录导出为一个多页签 Excel，
    /// 供审计软件（鼎信诺等）直接取数，等价于「审计对接 → 一键提取」。
    /// </summary>
    public sealed class AuditExtractService
    {
        private readonly AppDbContext _context;

        public AuditExtractService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>生成审计取数工作簿（xlsx 字节流）。</summary>
        public async Task<(byte[] Content, string FileName)> ExtractAsync(Guid accountSetId, int year, int month, CancellationToken cancellationToken = default)
        {
            if (month is < 1 or > 12)
            {
                throw new ReportException("会计期间月份需在 1 ~ 12 之间。");
            }

            var accountSet = await _context.AccountSets.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountSetId, cancellationToken)
                ?? throw new ReportException("账套不存在。");

            var start = new DateOnly(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var accounts = await _context.Accounts.AsNoTracking()
                .Where(a => a.AccountSetId == accountSetId)
                .OrderBy(a => a.Code)
                .ToListAsync(cancellationToken);

            var vouchers = await _context.Vouchers.AsNoTracking()
                .Include(v => v.Entries)
                .Where(v => v.AccountSetId == accountSetId)
                .ToListAsync(cancellationToken);

            var live = vouchers.Where(v => v.DeletedAt is null).OrderBy(v => v.Date).ThenBy(v => v.Number).ToList();

            // 期初 = 科目建档期初 + 期间之前的累计发生净额。
            var opening = new Dictionary<Guid, decimal>();
            var periodDebit = new Dictionary<Guid, decimal>();
            var periodCredit = new Dictionary<Guid, decimal>();

            foreach (var voucher in live)
            {
                foreach (var entry in voucher.Entries)
                {
                    if (voucher.Date < start)
                    {
                        opening[entry.AccountId] = opening.GetValueOrDefault(entry.AccountId) + entry.Debit - entry.Credit;
                    }
                    else if (voucher.Date <= end)
                    {
                        periodDebit[entry.AccountId] = periodDebit.GetValueOrDefault(entry.AccountId) + entry.Debit;
                        periodCredit[entry.AccountId] = periodCredit.GetValueOrDefault(entry.AccountId) + entry.Credit;
                    }
                }
            }

            var balanceRows = new List<Dictionary<string, object?>>();
            decimal totalOpeningDebit = 0m, totalOpeningCredit = 0m, totalDebit = 0m, totalCredit = 0m;

            foreach (var account in accounts.Where(a => a.IsLeaf))
            {
                var open = account.OpeningBalance + opening.GetValueOrDefault(account.Id);
                var debit = periodDebit.GetValueOrDefault(account.Id);
                var credit = periodCredit.GetValueOrDefault(account.Id);
                var close = open + debit - credit;

                balanceRows.Add(new Dictionary<string, object?>
                {
                    ["科目编码"] = account.Code,
                    ["科目名称"] = account.Name,
                    ["科目类别"] = CategoryText(account.Category),
                    ["余额方向"] = account.Direction == BalanceDirection.Debit ? "借" : "贷",
                    ["期初余额"] = Math.Round(open, 2),
                    ["本期借方"] = Math.Round(debit, 2),
                    ["本期贷方"] = Math.Round(credit, 2),
                    ["期末余额"] = Math.Round(close, 2),
                });

                if (open >= 0)
                {
                    totalOpeningDebit += open;
                }
                else
                {
                    totalOpeningCredit += -open;
                }

                totalDebit += debit;
                totalCredit += credit;
            }

            balanceRows.Add(new Dictionary<string, object?>
            {
                ["科目编码"] = "合计",
                ["科目名称"] = string.Empty,
                ["科目类别"] = string.Empty,
                ["余额方向"] = string.Empty,
                ["期初余额"] = Math.Round(totalOpeningDebit - totalOpeningCredit, 2),
                ["本期借方"] = Math.Round(totalDebit, 2),
                ["本期贷方"] = Math.Round(totalCredit, 2),
                ["期末余额"] = Math.Round(totalOpeningDebit - totalOpeningCredit + totalDebit - totalCredit, 2),
            });

            var accountById = accounts.ToDictionary(a => a.Id);
            var periodVouchers = live.Where(v => v.Date >= start && v.Date <= end).ToList();

            var voucherRows = periodVouchers.Select(v => new Dictionary<string, object?>
            {
                ["凭证日期"] = v.Date.ToString("yyyy-MM-dd"),
                ["凭证字号"] = $"{v.Word}-{v.Number}",
                ["会计期间"] = $"{v.Date.Year}-{v.Date.Month:00}",
                ["附件数"] = v.AttachmentCount,
                ["制单人"] = v.Preparer,
                ["审核人"] = v.Reviewer ?? string.Empty,
                ["状态"] = StatusText(v.Status),
                ["备注"] = v.Remark ?? string.Empty,
                ["分录数"] = v.Entries.Count,
                ["借方合计"] = Math.Round(v.Entries.Sum(e => e.Debit), 2),
                ["贷方合计"] = Math.Round(v.Entries.Sum(e => e.Credit), 2),
            }).ToList();

            var entryRows = periodVouchers
                .SelectMany(v => v.Entries.OrderBy(e => e.Id).Select(e => new Dictionary<string, object?>
                {
                    ["凭证日期"] = v.Date.ToString("yyyy-MM-dd"),
                    ["凭证字号"] = $"{v.Word}-{v.Number}",
                    ["摘要"] = e.Summary,
                    ["科目编码"] = accountById.TryGetValue(e.AccountId, out var account) ? account.Code : string.Empty,
                    ["科目名称"] = accountById.TryGetValue(e.AccountId, out var acc) ? acc.Name : string.Empty,
                    ["借方金额"] = Math.Round(e.Debit, 2),
                    ["贷方金额"] = Math.Round(e.Credit, 2),
                }))
                .ToList();

            var outlineRows = new List<Dictionary<string, object?>>
            {
                new() { ["项目"] = "账套名称", ["内容"] = accountSet.CompanyName.Length > 0 ? accountSet.CompanyName : accountSet.Name },
                new() { ["项目"] = "账套标识", ["内容"] = accountSet.Id.ToString() },
                new() { ["项目"] = "取数期间", ["内容"] = $"{year}年{month:00}月（{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}）" },
                new() { ["项目"] = "末级科目数", ["内容"] = accounts.Count(a => a.IsLeaf) },
                new() { ["项目"] = "本期凭证数", ["内容"] = periodVouchers.Count },
                new() { ["项目"] = "本期分录数", ["内容"] = entryRows.Count },
                new() { ["项目"] = "期初借贷差额", ["内容"] = Math.Round(totalOpeningDebit - totalOpeningCredit, 2) },
                new() { ["项目"] = "本期借贷发生额", ["内容"] = $"{Math.Round(totalDebit, 2):0.00} / {Math.Round(totalCredit, 2):0.00}" },
                new() { ["项目"] = "取数时间", ["内容"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                new() { ["项目"] = "说明", ["内容"] = "本工作簿由系统按正常凭证实时生成，可直接导入审计软件；金额单位为元。" },
            };

            var sheets = new Dictionary<string, object>
            {
                ["科目余额表"] = balanceRows,
                ["凭证清单"] = voucherRows,
                ["凭证分录"] = entryRows,
                ["取数说明"] = outlineRows,
            };

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, sheets, excelType: ExcelType.XLSX);
            return (stream.ToArray(), $"审计取数_{year}{month:00}.xlsx");
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
