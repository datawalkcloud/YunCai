using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Reports.Domain;

namespace YunCai.Modules.Reports.Application
{
    /// <summary>
    /// 资产负债表导出：按账套会计标准选择版式（会企01表 / 会小企01表），
    /// 生成的 .xls 两栏八列文件。复用项目已有的 NPOI，不依赖 Office 组件。
    /// </summary>
    public sealed class BalanceSheetXlsExportService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly BalanceSheetService _balanceSheetService;

        public BalanceSheetXlsExportService(
            IAccountSetRepository accountSetRepository,
            BalanceSheetService balanceSheetService)
        {
            _accountSetRepository = accountSetRepository;
            _balanceSheetService = balanceSheetService;
        }

        /// <summary>生成导出文件字节与文件名。year/month 省略时取最近有凭证期间。</summary>
        public async Task<(byte[] Content, string FileName)> ExportAsync(
            Guid accountSetId,
            int? year,
            int? month,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");

            var dto = await _balanceSheetService.GetAsync(accountSetId, year, month, cancellationToken);
            var template = BalanceSheetTemplates.Resolve(accountSet.Standard);
            var (left, right) = BalanceSheetExportLayout.Build(template, dto.Lines);
            var balance = BalanceSheetExportLayout.Balance(left, right);

            var company = string.IsNullOrWhiteSpace(accountSet.CompanyName)
                ? accountSet.Name ?? "账套"
                : accountSet.CompanyName;
            var periodEnd = new DateOnly(dto.Year, dto.Month, 1).AddMonths(1).AddDays(-1);
            var content = BalanceSheetXlsBuilder.Build(new BalanceSheetXlsModel(
                company,
                periodEnd.ToString("yyyy-MM-dd"),
                dto.TemplateTitle,
                left,
                right,
                balance));

            return (content, $"资产负债表_{dto.Year:D4}年{dto.Month:D2}月_{Sanitize(company)}_{DateTime.Now:yyyyMMdd}.xls");
        }

        private static string Sanitize(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }
    }
}
