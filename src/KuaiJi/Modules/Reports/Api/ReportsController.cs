using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.Modules.Reports.Application;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.SharedKernel.Infrastructure.Excel;
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
        private readonly AssistIncomeService _assistIncomeService;
        private readonly ExpenseDetailService _expenseDetailService;
        private readonly EquityChangeService _equityChangeService;
        private readonly TaxStatisticsService _taxStatisticsService;
        private readonly FinancialSummaryService _financialSummaryService;
        private readonly ReportExportService _reportExportService;

        public ReportsController(
            TrialBalanceService trialBalanceService,
            BalanceSheetService balanceSheetService,
            CashJournalService cashJournalService,
            IncomeStatementService incomeStatementService,
            CashFlowService cashFlowService,
            CashFlowSupplementService cashFlowSupplementService,
            AssistIncomeService assistIncomeService,
            ExpenseDetailService expenseDetailService,
            EquityChangeService equityChangeService,
            TaxStatisticsService taxStatisticsService,
            FinancialSummaryService financialSummaryService,
            ReportExportService reportExportService)
        {
            _trialBalanceService = trialBalanceService;
            _balanceSheetService = balanceSheetService;
            _cashJournalService = cashJournalService;
            _incomeStatementService = incomeStatementService;
            _cashFlowService = cashFlowService;
            _cashFlowSupplementService = cashFlowSupplementService;
            _assistIncomeService = assistIncomeService;
            _expenseDetailService = expenseDetailService;
            _equityChangeService = equityChangeService;
            _taxStatisticsService = taxStatisticsService;
            _financialSummaryService = financialSummaryService;
            _reportExportService = reportExportService;
        }

        /// <summary>
        /// 导出报表为 .xlsx。kind 取值：trial-balance / balance-sheet / income-statement / income-quarter /
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
                var book = await _reportExportService.ExportAsync(accountSetId, exportKind, year, month, quarter, cancellationToken);
                return File(FlatWorkbookBuilder.Build(book), FlatWorkbook.XlsxContentType, book.FileName);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 科目余额表。可选 startDate/endDate 指定日期范围（含起止日，格式 yyyy-MM-dd）；省略时取全部已过账凭证。
        /// 查询区间按自然月对齐（起始日归一到月初、截止日归一到月末），仅统计已过账凭证。
        /// </summary>
        [HttpGet("trial-balance")]
        public async Task<ActionResult<TrialBalanceDto>> GetTrialBalance(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _trialBalanceService.GetAsync(accountSetId, startDate, endDate, cancellationToken);
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
