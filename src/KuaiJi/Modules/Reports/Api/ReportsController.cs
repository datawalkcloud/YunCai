using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.Modules.Reports.Application;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.Reports.Infrastructure;
using KuaiJi.SharedKernel.Infrastructure.Excel;
using KuaiJi.SharedKernel.Infrastructure.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Reports.Api
{
    /// <summary>
    /// 账簿与报表接口。以账套为边界，从凭证分录实时汇总出各类账表。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly TrialBalanceService _trialBalanceService;
        private readonly BalanceSheetService _balanceSheetService;
        private readonly CashJournalService _cashJournalService;
        private readonly IncomeStatementService _incomeStatementService;
        private readonly CashFlowService _cashFlowService;
        private readonly CashFlowSupplementService _cashFlowSupplementService;
        private readonly CashFlowFormulaService _cashFlowFormulaService;
        private readonly AssistIncomeService _assistIncomeService;
        private readonly ExpenseDetailService _expenseDetailService;
        private readonly EquityChangeService _equityChangeService;
        private readonly TaxStatisticsService _taxStatisticsService;
        private readonly FinancialSummaryService _financialSummaryService;
        private readonly ReportExportService _reportExportService;
        private readonly BalanceSheetXlsExportService _balanceSheetXlsExportService;
        private readonly BalanceSheetPdfExportService _balanceSheetPdfExportService;
        private readonly IAccountSetRepository _accountSetRepository;

        public ReportsController(
            TrialBalanceService trialBalanceService,
            BalanceSheetService balanceSheetService,
            CashJournalService cashJournalService,
            IncomeStatementService incomeStatementService,
            CashFlowService cashFlowService,
            CashFlowSupplementService cashFlowSupplementService,
            CashFlowFormulaService cashFlowFormulaService,
            AssistIncomeService assistIncomeService,
            ExpenseDetailService expenseDetailService,
            EquityChangeService equityChangeService,
            TaxStatisticsService taxStatisticsService,
            FinancialSummaryService financialSummaryService,
            ReportExportService reportExportService,
            BalanceSheetXlsExportService balanceSheetXlsExportService,
            BalanceSheetPdfExportService balanceSheetPdfExportService,
            IAccountSetRepository accountSetRepository)
        {
            _trialBalanceService = trialBalanceService;
            _balanceSheetService = balanceSheetService;
            _cashJournalService = cashJournalService;
            _incomeStatementService = incomeStatementService;
            _cashFlowService = cashFlowService;
            _cashFlowSupplementService = cashFlowSupplementService;
            _cashFlowFormulaService = cashFlowFormulaService;
            _assistIncomeService = assistIncomeService;
            _expenseDetailService = expenseDetailService;
            _equityChangeService = equityChangeService;
            _taxStatisticsService = taxStatisticsService;
            _financialSummaryService = financialSummaryService;
            _reportExportService = reportExportService;
            _balanceSheetXlsExportService = balanceSheetXlsExportService;
            _balanceSheetPdfExportService = balanceSheetPdfExportService;
            _accountSetRepository = accountSetRepository;
        }

        /// <summary>
        /// 导出报表。资产负债表按账套会计标准导出两栏版式的 .xls（会企01表 / 会小企01表）；
        /// 其余报表为 .xlsx。kind 取值：trial-balance / balance-sheet / income-statement / income-quarter /
        /// cash-flow / cash-flow-quarter / department-income / project-income / expense-detail / equity-change /
        /// tax / financial-summary；year/month/quarter 为可选期间参数，省略时取最近有凭证期间。
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(
            Guid accountSetId,
            [FromQuery] string kind,
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] int? quarter,
            CancellationToken cancellationToken)
        {
            if (!ReportExportKindText.TryParse(kind ?? string.Empty, out var exportKind))
            {
                return BadRequest(new { message = "不支持导出的报表类型。" });
            }

            try
            {
                if (exportKind == ReportExportKind.BalanceSheet)
                {
                    var (content, fileName) = await _balanceSheetXlsExportService.ExportAsync(
                        accountSetId, year, month, cancellationToken);
                    return File(content, BalanceSheetXlsBuilder.ContentType, fileName);
                }

                var book = await _reportExportService.ExportAsync(accountSetId, exportKind, year, month, quarter, cancellationToken);
                return File(FlatWorkbookBuilder.Build(book), FlatWorkbook.XlsxContentType, book.FileName);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 打印报表为 PDF（服务端渲染：A4 版式、跨页续表、页脚页码与打印时间，表格按各报表页面 1:1 复刻）。
        /// <c>kind</c> 与 year/month/quarter 同 <see cref="Export"/>；科目余额表另接受与 trial-balance 同名的显示条件，
        /// 现金流量表月报可用 <c>method</c>=formula / assist 切换取数口径（省略即标准表的直接法 + 补充资料），
        /// <c>showLastYear</c> 对应页面的「显示上年累计金额」。
        /// 资产负债表走专用接口 <c>balance-sheet/pdf</c>；项目利润表与权益变动表页面没有打印按钮，返回 400。
        /// </summary>
        [HttpGet("pdf")]
        public async Task<IActionResult> Pdf(
            Guid accountSetId,
            [FromQuery] string kind,
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] int? quarter,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] int? minLevel,
            [FromQuery] int? maxLevel,
            [FromQuery] bool? hideZeroBalance,
            [FromQuery] bool? hideNoActivity,
            [FromQuery] string? method,
            [FromQuery] bool showLastYear = false,
            CancellationToken cancellationToken = default)
        {
            if (!ReportExportKindText.TryParse(kind ?? string.Empty, out var printKind))
            {
                return BadRequest(new { message = "不支持打印的报表类型。" });
            }

            try
            {
                if (printKind == ReportExportKind.BalanceSheet)
                {
                    var (content, fileName) = await _balanceSheetPdfExportService.ExportAsync(
                        accountSetId, year, month, cancellationToken);
                    return File(content, BalanceSheetPdfBuilder.ContentType, fileName);
                }

                var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                                 ?? throw new ReportException("指定的账套不存在。");
                var context = new ReportPdfProjector.PrintContext(accountSet.Name, accountSet.Standard, accountSet.CompanyName);
                var options = new LedgerQueryOptions
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    AccountFrom = accountFrom,
                    AccountTo = accountTo,
                    MinLevel = minLevel,
                    MaxLevel = maxLevel,
                    HideZeroBalance = hideZeroBalance ?? false,
                    HideNoActivity = hideNoActivity ?? false,
                };

                object page = printKind switch
                {
                    ReportExportKind.TrialBalance => await _trialBalanceService.GetAsync(accountSetId, options, cancellationToken),
                    ReportExportKind.IncomeStatement => await _incomeStatementService.GetAsync(accountSetId, year, month, cancellationToken),
                    ReportExportKind.IncomeQuarter => await _incomeStatementService.GetQuarterAsync(accountSetId, year, quarter, cancellationToken),
                    ReportExportKind.DepartmentIncome => await _assistIncomeService.GetByDepartmentAsync(accountSetId, year, month, cancellationToken),
                    ReportExportKind.CashFlow => await LoadCashFlowAsync(accountSetId, method, year, month, cancellationToken),
                    ReportExportKind.CashFlowQuarter => await _cashFlowService.GetQuarterAsync(accountSetId, year, quarter, cancellationToken),
                    ReportExportKind.ExpenseDetail => await _expenseDetailService.GetAsync(accountSetId, year, month, cancellationToken),
                    ReportExportKind.TaxStatistics => await _taxStatisticsService.GetAsync(accountSetId, year, month, cancellationToken),
                    ReportExportKind.FinancialSummary => await _financialSummaryService.GetAsync(accountSetId, year, month, cancellationToken),
                    _ => throw new ReportException("该报表暂不支持打印。"),
                };

                // 补充资料只在一般企业标准现金流量表随主表一起印（月报页面不展示它，且必定带 method 参数）；
                // 小企业会计准则（会小企03表）没有补充资料。
                CashFlowSupplementDto? supplement = null;
                if (printKind == ReportExportKind.CashFlow
                    && string.IsNullOrWhiteSpace(method)
                    && accountSet.Standard != AccountingStandard.SmallBusiness)
                {
                    supplement = await _cashFlowSupplementService.GetPeriodAsync(accountSetId, year, month, cancellationToken);
                }

                var sheet = ReportPdfProjector.Project(
                    printKind,
                    page,
                    context,
                    supplement,
                    printKind == ReportExportKind.TrialBalance ? options : null,
                    showLastYear);
                var bytes = PrintSheetPdfBuilder.Build(sheet);
                return File(
                    bytes,
                    PrintSheetPdfBuilder.ContentType,
                    PrintSheetPdfBuilder.BuildFileName(sheet.Title, context.AccountSetName, ReportPdfProjector.SuffixOf(page)));
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 现金流量表月报的取数口径：公式法按账套保存的取数公式逐行算（附带公式文本）、
        /// 辅助核算法按现金分录挂的「现金流」项目归集，两者都是页面上的可选项；默认走标准表的直接法。
        /// </summary>
        private async Task<CashFlowDto> LoadCashFlowAsync(
            Guid accountSetId, string? method, int? year, int? month, CancellationToken cancellationToken) => method switch
        {
            "formula" => await _cashFlowFormulaService.GetPeriodAsync(accountSetId, year, month, cancellationToken),
            "assist" => await _cashFlowService.GetAssistAsync(accountSetId, year, month, cancellationToken),
            _ => await _cashFlowService.GetPeriodAsync(accountSetId, year, month, cancellationToken),
        };

        /// <summary>
        /// 科目余额表。可选 startDate/endDate 指定日期范围（含起止日，格式 yyyy-MM-dd）；省略时取全部已过账凭证。
        /// 查询区间按自然月对齐（起始日归一到月初、截止日归一到月末），仅统计已过账凭证。
        /// 另支持 accountFrom/accountTo（科目编码区间）、minLevel/maxLevel（级次区间）、
        /// hideZeroBalance（余额为 0 不显示）、hideNoActivity（无发生额且余额为 0 不显示）等显示裁剪。
        /// </summary>
        [HttpGet("trial-balance")]
        public async Task<ActionResult<TrialBalanceDto>> GetTrialBalance(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] int? minLevel,
            [FromQuery] int? maxLevel,
            [FromQuery] bool? hideZeroBalance,
            [FromQuery] bool? hideNoActivity,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _trialBalanceService.GetAsync(accountSetId, new LedgerQueryOptions
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    AccountFrom = accountFrom,
                    AccountTo = accountTo,
                    MinLevel = minLevel,
                    MaxLevel = maxLevel,
                    HideZeroBalance = hideZeroBalance ?? false,
                    HideNoActivity = hideNoActivity ?? false,
                }, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 资产负债表（会企01表）。可选 year/month 指定会计期间；省略时取最近有凭证的月份（无凭证则取账套启用期间）。
        /// 期末余额取期间月末，年初余额取该年年初；各标准行次金额已按报表方向净额列示。
        /// </summary>
        [HttpGet("balance-sheet")]
        public async Task<ActionResult<BalanceSheetDto>> GetBalanceSheet(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _balanceSheetService.GetAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 资产负债表 PDF（A4 纵向两栏版式）。可选 year/month 指定会计期间，省略时取最近有凭证的月份。
        /// 服务端渲染，不含浏览器打印的页眉页脚；版式与 .xls 导出、页面打印稿一致。
        /// </summary>
        [HttpGet("balance-sheet/pdf")]
        public async Task<IActionResult> ExportBalanceSheetPdf(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var (content, fileName) = await _balanceSheetPdfExportService.ExportAsync(
                    accountSetId, year, month, cancellationToken);
                return File(content, BalanceSheetPdfBuilder.ContentType, fileName);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 现金日记账。可选 startDate/endDate 指定日期范围（含起止日，格式 yyyy-MM-dd）；省略时取全部凭证。
        /// 由涉及库存现金科目的凭证分录派生，返回期初余额与本期序时流水。
        /// </summary>
        [HttpGet("cash-journal")]
        public async Task<ActionResult<CashJournalDto>> GetCashJournal(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashJournalService.GetAsync(accountSetId, startDate, endDate, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 利润表（会企 02 表）。可选 year/month 指定会计期间；省略时取最近有凭证的月份（无凭证则取账套启用期间）。
        /// 本期金额取选定月份，本年累计取本年 1 月 1 日至选定月末；各标准行次金额已按报表方向净额列示。
        /// </summary>
        [HttpGet("income-statement")]
        public async Task<ActionResult<IncomeStatementDto>> GetIncomeStatement(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _incomeStatementService.GetAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 利润表季报。可选 year/quarter（1-4）指定季度；省略时取最近有凭证的季度。
        /// 本季金额取该季度三个月，本年累计取年初至季末。
        /// </summary>
        [HttpGet("income-quarter")]
        public async Task<ActionResult<IncomeStatementQuarterDto>> GetIncomeQuarter(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? quarter,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _incomeStatementService.GetQuarterAsync(accountSetId, year, quarter, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 现金流量表（直接法简化口径）。可选 year/month 指定会计期间；省略时取最近有凭证的月份。
        /// 返回标准主表行次（本期金额 + 本年累计），小计/净额/期初期末由服务端汇总；
        /// 简版展示可由前端按 Section/Kind 聚合。
        /// </summary>
        [HttpGet("cash-flow")]
        public async Task<ActionResult<CashFlowDto>> GetCashFlow(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashFlowService.GetPeriodAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 现金流量表（辅助核算法）：按现金分录上挂的「现金流」辅助项目归集到标准行次
        /// （预置项目按名称匹配，自定义项目按编码前两位），未挂项目的现金收付不计入；可选 year/month。
        /// </summary>
        [HttpGet("cash-flow-assist")]
        public async Task<ActionResult<CashFlowDto>> GetCashFlowAssist(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashFlowService.GetAssistAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>现金流量表季报。可选 year/quarter（1-4）；返回标准主表行次（本季金额 + 本年累计）。</summary>
        [HttpGet("cash-flow-quarter")]
        public async Task<ActionResult<CashFlowQuarterDto>> GetCashFlowQuarter(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? quarter,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashFlowService.GetQuarterAsync(accountSetId, year, quarter, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 现金流量表补充资料（间接法）：把净利润按调节项还原为经营活动现金流量净额，
        /// 并核对现金及现金等价物净增加额；同时返回与主表直接法净额的勾稽差异。
        /// 可选 year/month，省略时取最近有凭证的月份（与主表同一解析规则）。
        /// </summary>
        [HttpGet("cash-flow-supplement")]
        public async Task<ActionResult<CashFlowSupplementDto>> GetCashFlowSupplement(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashFlowSupplementService.GetPeriodAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 现金流量表底稿（直接法）：逐张凭证列出现金收支按对手方科目分摊的结果，
        /// 并标注归入的标准行次与方向，用于核对主表行次金额的来源。
        /// 可选 year/month，省略时取最近有凭证的月份（与主表同一解析规则）。
        /// </summary>
        [HttpGet("cash-flow-workpaper")]
        public async Task<ActionResult<CashFlowWorkpaperDto>> GetCashFlowWorkpaper(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashFlowService.GetWorkpaperAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 期初现金余额：按现金科目列示期间起点前与截止日的余额（含科目启用期初），
        /// 合计与主表「期初现金余额」「期末现金余额」一致。可选 year/month，省略时取最近有凭证的月份。
        /// </summary>
        [HttpGet("cash-flow-opening")]
        public async Task<ActionResult<CashFlowOpeningDto>> GetCashFlowOpening(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashFlowService.GetOpeningAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 现金流量表（公式法）：每行金额按账套保存的取数公式计算，未配置的行使用系统默认公式。
        /// 返回本期 / 本年累计 / 上年同期三列，行上附带生效公式；仅统计已过账凭证。
        /// </summary>
        [HttpGet("cash-flow-formula")]
        public async Task<ActionResult<CashFlowDto>> GetCashFlowFormula(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashFlowFormulaService.GetPeriodAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>读取现金流量表 35 行的生效取数公式（自定义优先，否则默认公式）。</summary>
        [HttpGet("cash-flow-formulas")]
        public async Task<ActionResult<CashFlowFormulaSettingsDto>> GetCashFlowFormulas(
            Guid accountSetId,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashFlowFormulaService.GetSettingsAsync(accountSetId, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 保存现金流量表取数公式。只处理请求中出现的行次；公式为空表示删除该行配置、恢复默认公式。
        /// </summary>
        [HttpPut("cash-flow-formulas")]
        public async Task<ActionResult<CashFlowFormulaSettingsDto>> SaveCashFlowFormulas(
            Guid accountSetId,
            [FromBody] SaveCashFlowFormulasRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashFlowFormulaService.SaveAsync(accountSetId, request, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 部门利润表：按辅助核算「部门」维度拆解收入、成本费用与利润。
        /// 各部门列为 本期金额 + 本年累计金额；仅列示有损益发生的部门。
        /// </summary>
        [HttpGet("department-income")]
        public async Task<ActionResult<DepartmentIncomeDto>> GetDepartmentIncome(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _assistIncomeService.GetByDepartmentAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>项目利润表：按辅助核算「项目」维度列示各项目本期与累计利润额。</summary>
        [HttpGet("project-income")]
        public async Task<ActionResult<ProjectIncomeDto>> GetProjectIncome(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _assistIncomeService.GetByProjectAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>费用明细表：成本类与损益类（借方向）科目的本期发生与本年累计，含上级科目汇总行。</summary>
        [HttpGet("expense-detail")]
        public async Task<ActionResult<ExpenseDetailDto>> GetExpenseDetail(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _expenseDetailService.GetAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>所有者权益变动表：各权益项目本年与上年的期初/期末余额（贷方为正）。</summary>
        [HttpGet("equity-change")]
        public async Task<ActionResult<EquityChangeDto>> GetEquityChange(
            Guid accountSetId,
            [FromQuery] int? year,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _equityChangeService.GetAsync(accountSetId, year, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>纳税统计表：应交税费类科目的计提净额、同比变化与本年累计。</summary>
        [HttpGet("tax")]
        public async Task<ActionResult<TaxStatisticsDto>> GetTaxStatistics(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _taxStatisticsService.GetAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>财务概要信息：核心存量/流量指标的本年累计（同比）与本期（同比、环比）。</summary>
        [HttpGet("financial-summary")]
        public async Task<ActionResult<FinancialSummaryDto>> GetFinancialSummary(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _financialSummaryService.GetAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
