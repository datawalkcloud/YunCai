using KuaiJi.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Controllers
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

        public ReportsController(
            TrialBalanceService trialBalanceService,
            BalanceSheetService balanceSheetService,
            CashJournalService cashJournalService)
        {
            _trialBalanceService = trialBalanceService;
            _balanceSheetService = balanceSheetService;
            _cashJournalService = cashJournalService;
        }

        /// <summary>
        /// 科目余额表。可选 year/month 指定会计期间；省略时取最近有凭证的月份（无凭证则取账套启用期间）。
        /// 返回选定期间及各科目的期初、本期发生额与期末余额。
        /// </summary>
        [HttpGet("trial-balance")]
        public async Task<ActionResult<TrialBalanceDto>> GetTrialBalance(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _trialBalanceService.GetAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
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
        /// 现金日记账。可选 year/month 指定会计期间；省略时取最近有凭证的月份（无凭证则取账套启用期间）。
        /// 由涉及库存现金科目的凭证分录派生，返回期初余额与本期序时流水。
        /// </summary>
        [HttpGet("cash-journal")]
        public async Task<ActionResult<CashJournalDto>> GetCashJournal(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cashJournalService.GetAsync(accountSetId, year, month, cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
