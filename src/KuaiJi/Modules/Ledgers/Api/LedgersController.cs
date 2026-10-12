using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Ledgers.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Ledgers.Api
{
    /// <summary>
    /// 账簿接口。以账套为边界，从已过账凭证分录派生各类账页；每张账页一个只读 GET。
    /// 日期区间会归一到所在自然月，起止均省略时取全部凭证的日期边界。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/[controller]")]
    [Authorize]
    public class LedgersController : ControllerBase
    {
        private readonly SubsidiaryLedgerService _subsidiaryLedgerService;
        private readonly GeneralLedgerService _generalLedgerService;
        private readonly VoucherJournalService _voucherJournalService;
        private readonly VoucherSummaryService _voucherSummaryService;
        private readonly AssistLedgerService _assistLedgerService;
        private readonly MultiColumnLedgerService _multiColumnLedgerService;
        private readonly AgeingAnalysisService _ageingAnalysisService;

        public LedgersController(
            SubsidiaryLedgerService subsidiaryLedgerService,
            GeneralLedgerService generalLedgerService,
            VoucherJournalService voucherJournalService,
            VoucherSummaryService voucherSummaryService,
            AssistLedgerService assistLedgerService,
            MultiColumnLedgerService multiColumnLedgerService,
            AgeingAnalysisService ageingAnalysisService)
        {
            _subsidiaryLedgerService = subsidiaryLedgerService;
            _generalLedgerService = generalLedgerService;
            _voucherJournalService = voucherJournalService;
            _voucherSummaryService = voucherSummaryService;
            _assistLedgerService = assistLedgerService;
            _multiColumnLedgerService = multiColumnLedgerService;
            _ageingAnalysisService = ageingAnalysisService;
        }

        /// <summary>
        /// 明细账。按科目分段列示逐笔发生额与滚动余额，每月末插「本月合计」、末尾插「本年累计」。
        /// 非明细科目自动带出其全部下级科目的流水；摘要关键字只影响列示行，不改变余额。
        /// </summary>
        [HttpGet("subsidiary")]
        public async Task<ActionResult<SubsidiaryLedgerDto>> GetSubsidiaryLedger(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] int? maxLevel,
            [FromQuery] string? summaryKeyword,
            [FromQuery] LedgerSortBy? sortBy,
            [FromQuery] bool? showCounterAccount,
            [FromQuery] bool? showOnlyLeaf,
            [FromQuery] bool? hideZeroBalance,
            [FromQuery] bool? hideNoActivity,
            [FromQuery] bool? includeIdleAccounts,
            CancellationToken cancellationToken)
        {
            var options = new LedgerQueryOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                AccountFrom = accountFrom,
                AccountTo = accountTo,
                MaxLevel = maxLevel,
                SummaryKeyword = summaryKeyword,
                SortBy = sortBy ?? LedgerSortBy.VoucherNumber,
                ShowCounterAccount = showCounterAccount ?? true,
                ShowOnlyLeaf = showOnlyLeaf ?? true,
                HideZeroBalance = hideZeroBalance ?? false,
                HideNoActivity = hideNoActivity ?? false,
                IncludeIdleAccounts = includeIdleAccounts ?? false,
            };

            try
            {
                return Ok(await _subsidiaryLedgerService.GetAsync(accountSetId, options, cancellationToken));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 总账。按科目分段、每个自然月一行列示期初、本月借贷、期末与余额方向；
        /// 上级科目的发生额与余额恒为其下级之和，无发生额的月份也占一行以保持余额连续。
        /// </summary>
        [HttpGet("general")]
        public async Task<ActionResult<GeneralLedgerDto>> GetGeneralLedger(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] int? maxLevel,
            [FromQuery] bool? showOnlyLeaf,
            [FromQuery] bool? hideZeroBalance,
            [FromQuery] bool? hideNoActivity,
            [FromQuery] bool? includeIdleAccounts,
            CancellationToken cancellationToken)
        {
            var options = new LedgerQueryOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                AccountFrom = accountFrom,
                AccountTo = accountTo,
                MaxLevel = maxLevel,
                ShowOnlyLeaf = showOnlyLeaf ?? true,
                HideZeroBalance = hideZeroBalance ?? false,
                HideNoActivity = hideNoActivity ?? false,
                IncludeIdleAccounts = includeIdleAccounts ?? false,
            };

            try
            {
                return Ok(await _generalLedgerService.GetAsync(accountSetId, options, cancellationToken));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 序时账。按查询排序方式逐笔流水列示已过账分录，带科目编码名称、制单人、审核人与附件张数；
        /// 摘要关键字命中任一分录时整张凭证列示。
        /// </summary>
        [HttpGet("journal")]
        public async Task<ActionResult<VoucherJournalDto>> GetJournal(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] string? summaryKeyword,
            [FromQuery] LedgerSortBy? sortBy,
            [FromQuery] bool? showOnlyLeaf,
            CancellationToken cancellationToken)
        {
            var options = new LedgerQueryOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                AccountFrom = accountFrom,
                AccountTo = accountTo,
                SummaryKeyword = summaryKeyword,
                SortBy = sortBy ?? LedgerSortBy.VoucherNumber,
                ShowOnlyLeaf = showOnlyLeaf ?? true,
            };

            try
            {
                return Ok(await _voucherJournalService.GetAsync(accountSetId, options, cancellationToken));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 凭证汇总表。按科目列示区间内借贷发生额合计，表头统计涉及凭证张数与附件张数（均按凭证去重）。
        /// </summary>
        [HttpGet("voucher-summary")]
        public async Task<ActionResult<VoucherSummaryDto>> GetVoucherSummary(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] string? summaryKeyword,
            [FromQuery] bool? showOnlyLeaf,
            CancellationToken cancellationToken)
        {
            var options = new LedgerQueryOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                AccountFrom = accountFrom,
                AccountTo = accountTo,
                SummaryKeyword = summaryKeyword,
                ShowOnlyLeaf = showOnlyLeaf ?? true,
            };

            try
            {
                return Ok(await _voucherSummaryService.GetAsync(accountSetId, options, cancellationToken));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 核算项目余额表。按辅助核算类别列示各档案在科目下的期初、本期发生额与期末余额；
        /// 一条分录在同一类别下挂多个档案时各档案分别计全额。
        /// </summary>
        [HttpGet("assist-balance")]
        public async Task<ActionResult<AssistBalanceDto>> GetAssistBalance(
            Guid accountSetId,
            AssistType assistType,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] Guid? assistItemId,
            [FromQuery] bool? hideZeroBalance,
            [FromQuery] bool? hideNoActivity,
            [FromQuery] bool? includeIdleAccounts,
            CancellationToken cancellationToken)
        {
            var options = AssistOptions(assistType, startDate, endDate, accountFrom, accountTo, assistItemId,
                hideZeroBalance, hideNoActivity, includeIdleAccounts);

            try
            {
                return Ok(await _assistLedgerService.GetBalanceAsync(accountSetId, options, cancellationToken));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 核算项目明细账。按「科目 × 档案」分段列示逐笔发生额与滚动余额，行结构与明细账一致。
        /// </summary>
        [HttpGet("assist-subsidiary")]
        public async Task<ActionResult<AssistSubsidiaryDto>> GetAssistSubsidiary(
            Guid accountSetId,
            AssistType assistType,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] Guid? assistItemId,
            [FromQuery] string? summaryKeyword,
            [FromQuery] bool? showCounterAccount,
            [FromQuery] bool? hideZeroBalance,
            [FromQuery] bool? hideNoActivity,
            CancellationToken cancellationToken)
        {
            var options = AssistOptions(assistType, startDate, endDate, accountFrom, accountTo, assistItemId,
                hideZeroBalance, hideNoActivity, includeIdleAccounts: null) with
            {
                SummaryKeyword = summaryKeyword,
                ShowCounterAccount = showCounterAccount ?? true,
            };

            try
            {
                return Ok(await _assistLedgerService.GetSubsidiaryAsync(accountSetId, options, cancellationToken));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 核算项目组合表。主、次两个辅助核算类别交叉列示区间内借贷发生额，每个主档案一组。
        /// </summary>
        [HttpGet("assist-combine")]
        public async Task<ActionResult<AssistCombineDto>> GetAssistCombine(
            Guid accountSetId,
            AssistType assistType,
            AssistType secondaryAssistType,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            CancellationToken cancellationToken)
        {
            var options = new LedgerQueryOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                AccountFrom = accountFrom,
                AccountTo = accountTo,
                AssistType = assistType,
                SecondaryAssistType = secondaryAssistType,
            };

            try
            {
                return Ok(await _assistLedgerService.GetCombineAsync(accountSetId, options, cancellationToken));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 多栏账。选定一个科目（如管理费用），把其下级科目展开成栏逐笔登记；
        /// 主科目本身为明细科目时退化为单栏。
        /// </summary>
        [HttpGet("multi-column")]
        public async Task<ActionResult<MultiColumnDto>> GetMultiColumn(
            Guid accountSetId,
            [FromQuery] Guid accountId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? summaryKeyword,
            CancellationToken cancellationToken)
        {
            var options = new LedgerQueryOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                SingleAccountId = accountId,
                SummaryKeyword = summaryKeyword,
            };

            try
            {
                return Ok(await _multiColumnLedgerService.GetAsync(accountSetId, options, cancellationToken));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 账龄分析表。按「科目 × 档案」的期末未核销余额，从最晚分录向前倒序消耗后归入固定七档；
        /// 一期未做单据核销，返回的 note 需在页脚原样提示。
        /// </summary>
        [HttpGet("ageing")]
        public async Task<ActionResult<AgeingAnalysisDto>> GetAgeing(
            Guid accountSetId,
            AssistType assistType,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] Guid? assistItemId,
            CancellationToken cancellationToken)
        {
            var options = new LedgerQueryOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                AccountFrom = accountFrom,
                AccountTo = accountTo,
                AssistType = assistType,
                AssistItemId = assistItemId,
            };

            try
            {
                return Ok(await _ageingAnalysisService.GetAsync(accountSetId, options, cancellationToken));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 导出账页为 .xlsx。<c>kind</c> 即上述账页的路径段（subsidiary / general / journal / voucher-summary /
        /// assist-balance / assist-subsidiary / assist-combine / multi-column / ageing），
        /// 查询条件与对应 GET 接口同名，取各页的并集，未用到的条件对该页无影响。
        /// </summary>
        [HttpGet("{kind}/export")]
        public async Task<IActionResult> Export(
            Guid accountSetId,
            string kind,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? accountFrom,
            [FromQuery] string? accountTo,
            [FromQuery] int? maxLevel,
            [FromQuery] string? summaryKeyword,
            [FromQuery] LedgerSortBy? sortBy,
            [FromQuery] bool? showCounterAccount,
            [FromQuery] bool? showOnlyLeaf,
            [FromQuery] bool? hideZeroBalance,
            [FromQuery] bool? hideNoActivity,
            [FromQuery] bool? includeIdleAccounts,
            [FromQuery] AssistType? assistType,
            [FromQuery] AssistType? secondaryAssistType,
            [FromQuery] Guid? assistItemId,
            [FromQuery] Guid? accountId,
            CancellationToken cancellationToken)
        {
            if (!LedgerExportKindText.TryParse(kind, out var exportKind))
            {
                return BadRequest(new { message = "不支持导出的账页类型。" });
            }

            var options = new LedgerQueryOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                AccountFrom = accountFrom,
                AccountTo = accountTo,
                MaxLevel = maxLevel,
                SummaryKeyword = summaryKeyword,
                SortBy = sortBy ?? LedgerSortBy.VoucherNumber,
                ShowCounterAccount = showCounterAccount ?? true,
                ShowOnlyLeaf = showOnlyLeaf ?? true,
                HideZeroBalance = hideZeroBalance ?? false,
                HideNoActivity = hideNoActivity ?? false,
                IncludeIdleAccounts = includeIdleAccounts ?? false,
                AssistType = assistType,
                SecondaryAssistType = secondaryAssistType,
                AssistItemId = assistItemId,
                SingleAccountId = accountId,
            };

            try
            {
                var page = exportKind switch
                {
                    LedgerExportKind.Subsidiary => (object)await _subsidiaryLedgerService.GetAsync(accountSetId, options, cancellationToken),
                    LedgerExportKind.General => (object)await _generalLedgerService.GetAsync(accountSetId, options, cancellationToken),
                    LedgerExportKind.Journal => (object)await _voucherJournalService.GetAsync(accountSetId, options, cancellationToken),
                    LedgerExportKind.VoucherSummary => (object)await _voucherSummaryService.GetAsync(accountSetId, options, cancellationToken),
                    LedgerExportKind.AssistBalance => (object)await _assistLedgerService.GetBalanceAsync(accountSetId, options, cancellationToken),
                    LedgerExportKind.AssistSubsidiary => (object)await _assistLedgerService.GetSubsidiaryAsync(accountSetId, options, cancellationToken),
                    LedgerExportKind.AssistCombine => (object)await _assistLedgerService.GetCombineAsync(accountSetId, options, cancellationToken),
                    LedgerExportKind.MultiColumn => (object)await _multiColumnLedgerService.GetAsync(accountSetId, options, cancellationToken),
                    _ => (object)await _ageingAnalysisService.GetAsync(accountSetId, options, cancellationToken),
                };

                var sheet = LedgerExportProjector.Project(exportKind, page);
                return File(LedgerExportBuilder.Build(sheet), LedgerExportBuilder.ContentType, LedgerExportBuilder.BuildFileName(sheet));
            }
            catch (LedgerException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private static LedgerQueryOptions AssistOptions(
            AssistType assistType,
            DateOnly? startDate,
            DateOnly? endDate,
            string? accountFrom,
            string? accountTo,
            Guid? assistItemId,
            bool? hideZeroBalance,
            bool? hideNoActivity,
            bool? includeIdleAccounts) => new()
        {
            StartDate = startDate,
            EndDate = endDate,
            AccountFrom = accountFrom,
            AccountTo = accountTo,
            AssistType = assistType,
            AssistItemId = assistItemId,
            HideZeroBalance = hideZeroBalance ?? false,
            HideNoActivity = hideNoActivity ?? false,
            IncludeIdleAccounts = includeIdleAccounts ?? false,
        };
    }
}
