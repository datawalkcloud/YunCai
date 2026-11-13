using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.FixedAssets.Application;
using KuaiJi.Modules.FixedAssets.Infrastructure;
using KuaiJi.SharedKernel.Infrastructure.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;

namespace KuaiJi.Modules.FixedAssets.Api
{
    /// <summary>
    /// 固定资产接口：类别 / 卡片 / 变更记录 / 期间计提 / 折旧摊销明细表 / 资产汇总表 / 资产核对总账，
    /// 以及折旧凭证、变更凭证、导入导出与批量操作。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/fixedassets")]
    [Authorize]
    public class FixedAssetsController : ControllerBase
    {
        private readonly FixedAssetService _assetService;
        private readonly IAccountSetRepository _accountSetRepository;

        public FixedAssetsController(FixedAssetService assetService, IAccountSetRepository accountSetRepository)
        {
            _assetService = assetService;
            _accountSetRepository = accountSetRepository;
        }

        /// <summary>获取全部资产类别。</summary>
        [HttpGet("categories")]
        public async Task<ActionResult<IReadOnlyList<FixedAssetCategoryDto>>> GetCategories(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.GetCategoriesAsync(accountSetId, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建资产类别。</summary>
        [HttpPost("categories")]
        public async Task<ActionResult<FixedAssetCategoryDto>> CreateCategory(Guid accountSetId, [FromBody] SaveFixedAssetCategoryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.CreateCategoryAsync(accountSetId, request, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改资产类别。</summary>
        [HttpPut("categories/{id:guid}")]
        public async Task<ActionResult<FixedAssetCategoryDto>> UpdateCategory(Guid accountSetId, Guid id, [FromBody] SaveFixedAssetCategoryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var category = await _assetService.UpdateCategoryAsync(accountSetId, id, request, cancellationToken);
                return category is null ? NotFound() : Ok(category);
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除资产类别（类别下仍有卡片时不可删）。</summary>
        [HttpDelete("categories/{id:guid}")]
        public async Task<IActionResult> DeleteCategory(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _assetService.DeleteCategoryAsync(accountSetId, id, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>获取资产卡片；传 period（yyyyMM）时按该期间回算折旧与净值。</summary>
        [HttpGet("assets")]
        public async Task<ActionResult<IReadOnlyList<FixedAssetDto>>> GetAssets(Guid accountSetId, int? period, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.GetAssetsAsync(accountSetId, period, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>获取期初（资产初始化）卡片；传 period（yyyyMM）时按该期间回算。</summary>
        [HttpGet("initial-assets")]
        public async Task<ActionResult<IReadOnlyList<FixedAssetDto>>> GetInitialAssets(Guid accountSetId, int? period, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.GetInitialAssetsAsync(accountSetId, period, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建期初（资产初始化）卡片。</summary>
        [HttpPost("initial-assets")]
        public async Task<ActionResult<FixedAssetDto>> CreateInitialAsset(Guid accountSetId, [FromBody] SaveFixedAssetRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.CreateInitialAssetAsync(accountSetId, request, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建资产卡片。</summary>
        [HttpPost("assets")]
        public async Task<ActionResult<FixedAssetDto>> CreateAsset(Guid accountSetId, [FromBody] SaveFixedAssetRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.CreateAssetAsync(accountSetId, request, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改资产卡片（原值变动写变更记录）。</summary>
        [HttpPut("assets/{id:guid}")]
        public async Task<ActionResult<FixedAssetDto>> UpdateAsset(Guid accountSetId, Guid id, [FromBody] SaveFixedAssetRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var asset = await _assetService.UpdateAssetAsync(accountSetId, id, request, cancellationToken);
                return asset is null ? NotFound() : Ok(asset);
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除资产卡片（已生成凭证的卡片不可删除）。</summary>
        [HttpDelete("assets/{id:guid}")]
        public async Task<IActionResult> DeleteAsset(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _assetService.DeleteAssetAsync(accountSetId, id, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>清理资产（终止计提，写清理变更记录）。</summary>
        [HttpPost("assets/{id:guid}/clear")]
        public async Task<ActionResult<FixedAssetDto>> ClearAsset(Guid accountSetId, Guid id, [FromBody] ClearAssetRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var asset = await _assetService.ClearAssetAsync(accountSetId, id, request.ClearedDate, cancellationToken);
                return asset is null ? NotFound() : Ok(asset);
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>获取变更记录（可按资产 / 期间过滤）。</summary>
        [HttpGet("changes")]
        public async Task<ActionResult<IReadOnlyList<FixedAssetChangeDto>>> GetChanges(
            Guid accountSetId,
            [FromQuery] Guid? assetId,
            [FromQuery] int? startPeriod,
            [FromQuery] int? endPeriod,
            CancellationToken cancellationToken)
        {
            return Ok(await _assetService.GetChangesAsync(accountSetId, assetId, startPeriod, endPeriod, cancellationToken));
        }

        /// <summary>按期间批量计提折旧。</summary>
        [HttpPost("depreciate")]
        public async Task<ActionResult<DepreciatePeriodResultDto>> Depreciate(Guid accountSetId, [FromBody] DepreciatePeriodRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.DepreciatePeriodAsync(accountSetId, request.Period, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>折旧摊销明细表（指定期间）。</summary>
        [HttpGet("depreciation-ledger")]
        public async Task<ActionResult<IReadOnlyList<DepreciationLedgerRowDto>>> GetDepreciationLedger(
            Guid accountSetId,
            [FromQuery] int period,
            CancellationToken cancellationToken)
        {
            return Ok(await _assetService.GetDepreciationLedgerAsync(accountSetId, period, cancellationToken));
        }

        /// <summary>资产汇总表（按部门或类别分组）。</summary>
        [HttpGet("summary")]
        public async Task<ActionResult<IReadOnlyList<FixedAssetSummaryRowDto>>> GetSummary(
            Guid accountSetId,
            [FromQuery] int startPeriod,
            [FromQuery] int endPeriod,
            [FromQuery] bool byCategory = false,
            CancellationToken cancellationToken = default)
        {
            return Ok(await _assetService.GetSummaryAsync(accountSetId, startPeriod, endPeriod, byCategory, cancellationToken));
        }

        /// <summary>资产核对总账（按资产属性五组：会计科目 vs 资产卡片 + 差异，期间口径）。</summary>
        [HttpGet("check")]
        public async Task<ActionResult<IReadOnlyList<FixedAssetCheckRowDto>>> GetCheck(
            Guid accountSetId,
            [FromQuery] int? startPeriod,
            [FromQuery] int? endPeriod,
            CancellationToken cancellationToken)
        {
            return Ok(await _assetService.GetCheckAsync(accountSetId, startPeriod, endPeriod, cancellationToken));
        }

        // —— 凭证科目设置 ——

        /// <summary>读取折旧 / 摊销 / 清理凭证科目映射（未配置项返回自动推断结果）。</summary>
        [HttpGet("voucher-setting")]
        public async Task<ActionResult<FixedAssetVoucherSettingDto>> GetVoucherSetting(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.GetVoucherSettingAsync(accountSetId, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>保存凭证科目映射；留空的项回落到自动推断。</summary>
        [HttpPut("voucher-setting")]
        public async Task<ActionResult<FixedAssetVoucherSettingDto>> SaveVoucherSetting(
            Guid accountSetId,
            [FromBody] SaveFixedAssetVoucherSettingRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.SaveVoucherSettingAsync(accountSetId, request, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // —— 折旧凭证 ——

        /// <summary>查询某期间折旧凭证状态。</summary>
        [HttpGet("depreciation-voucher")]
        public async Task<ActionResult<DepreciationVoucherInfoDto>> GetDepreciationVoucher(
            Guid accountSetId,
            [FromQuery] int period,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.GetDepreciationVoucherInfoAsync(accountSetId, period, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>生成某期间折旧 / 摊销凭证（同一科目合并为一行，生成一张凭证）。</summary>
        [HttpPost("depreciation-voucher")]
        public async Task<ActionResult<DepreciationVoucherInfoDto>> GenerateDepreciationVoucher(
            Guid accountSetId,
            [FromBody] GenerateDepreciationVoucherRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var preparer = User.FindFirst("displayName")?.Value
                               ?? User.Identity?.Name
                               ?? "系统";
                var body = new GenerateDepreciationVoucherRequest
                {
                    Period = request.Period,
                    Word = request.Word,
                    Preparer = preparer,
                };
                return Ok(await _assetService.GenerateDepreciationVoucherAsync(accountSetId, body, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // —— 变更记录凭证 ——

        /// <summary>变更记录生成凭证的建议分录。</summary>
        [HttpGet("changes/{id:guid}/voucher-preview")]
        public async Task<ActionResult<ChangeVoucherPreviewDto>> GetChangeVoucherPreview(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.GetChangeVoucherPreviewAsync(accountSetId, id, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>按确认后的分录生成变更凭证。</summary>
        [HttpPost("changes/{id:guid}/voucher")]
        public async Task<ActionResult<object>> GenerateChangeVoucher(
            Guid accountSetId,
            Guid id,
            [FromBody] GenerateChangeVoucherRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var preparer = User.FindFirst("displayName")?.Value
                               ?? User.Identity?.Name
                               ?? "系统";
                var body = new GenerateChangeVoucherRequest
                {
                    Entries = request.Entries,
                    Date = request.Date,
                    Word = request.Word,
                    Preparer = preparer,
                };
                var text = await _assetService.GenerateChangeVoucherAsync(accountSetId, id, body, cancellationToken);
                return Ok(new { voucherText = text });
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // —— 批量 / 复制 ——

        /// <summary>批量删除资产（已生成凭证的卡片计入跳过）。</summary>
        [HttpPost("assets/bulk-delete")]
        public async Task<ActionResult<FixedAssetBulkResultDto>> BulkDeleteAssets(
            Guid accountSetId,
            [FromBody] FixedAssetBulkIdsRequest request,
            CancellationToken cancellationToken)
            => Ok(await _assetService.DeleteAssetsAsync(accountSetId, request.Ids ?? [], cancellationToken));

        /// <summary>批量清理资产（统一按指定日期清理）。</summary>
        [HttpPost("assets/bulk-clear")]
        public async Task<ActionResult<FixedAssetBulkResultDto>> BulkClearAssets(
            Guid accountSetId,
            [FromBody] BulkClearAssetsRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.ClearAssetsAsync(accountSetId, request.Ids ?? [], request.ClearedDate, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>复制资产卡片。</summary>
        [HttpPost("assets/{id:guid}/copy")]
        public async Task<ActionResult<FixedAssetDto>> CopyAsset(
            Guid accountSetId,
            Guid id,
            [FromBody] CopyFixedAssetRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.CopyAssetAsync(accountSetId, id, request, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // —— 导入导出 ——

        /// <summary>导出资产清单为 .xlsx（列与导入模板一致，可改完原样导回）。</summary>
        [HttpGet("assets/export")]
        public async Task<IActionResult> ExportAssets(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _assetService.ExportAssetsAsync(accountSetId, cancellationToken);
                return File(bytes, FixedAssetExportBuilder.ContentType, "资产清单.xlsx");
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载资产导入模板（仅表头）。</summary>
        [HttpGet("assets/import-template")]
        public IActionResult DownloadAssetTemplate()
            => File(_assetService.BuildAssetTemplate(), FixedAssetExportBuilder.ContentType, "资产导入模板.xlsx");

        /// <summary>导入资产（按资产编号匹配，存在则更新）。</summary>
        [HttpPost("assets/import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<FixedAssetImportResultDto>> ImportAssets(
            Guid accountSetId,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var rows = ReadAssetRows(stream);
                return Ok(await _assetService.ImportAssetsAsync(accountSetId, rows, cancellationToken));
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return BadRequest(new { message = "文件读取失败，请确认是 .xlsx 格式的导入模板。" });
            }
        }

        /// <summary>导出折旧摊销明细表。</summary>
        [HttpGet("depreciation-ledger/export")]
        public async Task<IActionResult> ExportDepreciationLedger(Guid accountSetId, [FromQuery] int period, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _assetService.ExportDepreciationLedgerAsync(accountSetId, period, cancellationToken);
                return File(bytes, FixedAssetExportBuilder.ContentType, $"折旧摊销明细表_{period}.xlsx");
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出资产汇总表。</summary>
        [HttpGet("summary/export")]
        public async Task<IActionResult> ExportSummary(
            Guid accountSetId,
            [FromQuery] int startPeriod,
            [FromQuery] int endPeriod,
            [FromQuery] bool byCategory = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var bytes = await _assetService.ExportSummaryAsync(accountSetId, startPeriod, endPeriod, byCategory, cancellationToken);
                return File(bytes, FixedAssetExportBuilder.ContentType, $"资产汇总表_{startPeriod}-{endPeriod}.xlsx");
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出资产类别。</summary>
        [HttpGet("categories/export")]
        public async Task<IActionResult> ExportCategories(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _assetService.ExportCategoriesAsync(accountSetId, cancellationToken);
                return File(bytes, FixedAssetExportBuilder.ContentType, "资产类别.xlsx");
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出资产核对总账。</summary>
        [HttpGet("check/export")]
        public async Task<IActionResult> ExportCheck(
            Guid accountSetId,
            [FromQuery] int? startPeriod,
            [FromQuery] int? endPeriod,
            CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _assetService.ExportCheckAsync(accountSetId, startPeriod, endPeriod, cancellationToken);
                return File(bytes, FixedAssetExportBuilder.ContentType, "资产核对总账.xlsx");
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // —— 打印 PDF ——

        /// <summary>
        /// 打印固定资产清单（资产列表页签的表格，含表尾合计）。
        /// 取数与 <c>GET assets</c> 一致，按期间回算。
        /// </summary>
        [HttpGet("assets/pdf")]
        public async Task<IActionResult> PrintAssets(
            Guid accountSetId,
            [FromQuery] int? period,
            CancellationToken cancellationToken)
        {
            try
            {
                var context = await PrintContextAsync(accountSetId, cancellationToken);
                var assets = await _assetService.GetAssetsAsync(accountSetId, period, cancellationToken);
                return Pdf(FixedAssetsPdfProjector.Assets(assets, context, PeriodText(period)), context, period);
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>打印资产初始化清单（资产初始化页签的表格，期初累计折旧口径）。</summary>
        [HttpGet("initial-assets/pdf")]
        public async Task<IActionResult> PrintInitialAssets(
            Guid accountSetId,
            [FromQuery] int? period,
            CancellationToken cancellationToken)
        {
            try
            {
                var context = await PrintContextAsync(accountSetId, cancellationToken);
                var assets = await _assetService.GetInitialAssetsAsync(accountSetId, period, cancellationToken);
                return Pdf(FixedAssetsPdfProjector.InitialAssets(assets, context, PeriodText(period)), context, period);
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>打印折旧摊销明细表（取数与 <c>GET depreciation-ledger</c> 一致）。</summary>
        [HttpGet("depreciation-ledger/pdf")]
        public async Task<IActionResult> PrintDepreciationLedger(
            Guid accountSetId,
            [FromQuery] int period,
            CancellationToken cancellationToken)
        {
            try
            {
                var context = await PrintContextAsync(accountSetId, cancellationToken);
                var rows = await _assetService.GetDepreciationLedgerAsync(accountSetId, period, cancellationToken);
                return Pdf(FixedAssetsPdfProjector.DepreciationLedger(rows, period, context), context, period);
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>打印资产核对总账（取数与 <c>GET check</c> 一致；类别分组行与差异行按页面排布）。</summary>
        [HttpGet("check/pdf")]
        public async Task<IActionResult> PrintCheck(
            Guid accountSetId,
            [FromQuery] int? period,
            CancellationToken cancellationToken)
        {
            try
            {
                var context = await PrintContextAsync(accountSetId, cancellationToken);
                var rows = await _assetService.GetCheckAsync(accountSetId, period, period, cancellationToken);
                return Pdf(FixedAssetsPdfProjector.Check(rows, context), context, period);
            }
            catch (FixedAssetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>打印页头与文件名要用的账套名称 / 编制单位；账套不存在时按各模块口径回 400。</summary>
        private async Task<FixedAssetsPdfProjector.PrintContext> PrintContextAsync(
            Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new FixedAssetException("指定的账套不存在。");
            return new FixedAssetsPdfProjector.PrintContext(accountSet.Name, accountSet.CompanyName);
        }

        private IActionResult Pdf(PrintSheet sheet, FixedAssetsPdfProjector.PrintContext context, int? period)
            => File(PrintSheetPdfBuilder.Build(sheet), PrintSheetPdfBuilder.ContentType,
                PrintSheetPdfBuilder.BuildFileName(sheet.Title, context.AccountSetName, PeriodSuffix(period)));

        /// <summary>期间标识 202601 → 「2026年1月」（页头用）。</summary>
        private static string? PeriodText(int? period)
            => period is { } value && value >= 100_000
                ? string.Create(CultureInfo.InvariantCulture, $"{value / 100:D4}年{value % 100:D1}月")
                : null;

        /// <summary>文件名后缀用紧凑的 yyyyMM，与账页打印同风格。</summary>
        private static string? PeriodSuffix(int? period)
            => period is { } value && value >= 100_000
                ? value.ToString(CultureInfo.InvariantCulture)
                : null;

        /// <summary>把工作表读成「列名 → 单元格文本」的行集合（列名取自首行）。</summary>
        private static List<Dictionary<string, string?>> ReadSheet(Stream stream)
        {
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0) ?? throw new FixedAssetException("文件中没有工作表。");
            var headerRow = sheet.GetRow(0) ?? throw new FixedAssetException("文件缺少表头行。");

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < headerRow.LastCellNum; i++)
            {
                var text = headerRow.GetCell(i)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    index[text] = i;
                }
            }

            if (index.Count == 0)
            {
                throw new FixedAssetException("文件缺少表头行。");
            }

            var rows = new List<Dictionary<string, string?>>();
            for (var r = 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                {
                    continue;
                }

                var values = new Dictionary<string, string?>(StringComparer.Ordinal);
                var hasValue = false;
                foreach (var (name, column) in index)
                {
                    var text = row.GetCell(column) switch
                    {
                        null => null,
                        var cell => cell.CellType switch
                        {
                            CellType.Numeric when DateUtil.IsCellDateFormatted(cell)
                                => cell.DateCellValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            CellType.Numeric => cell.NumericCellValue.ToString("0.##", CultureInfo.InvariantCulture),
                            CellType.Formula => cell.NumericCellValue.ToString("0.##", CultureInfo.InvariantCulture),
                            _ => cell.ToString()?.Trim(),
                        },
                    };
                    values[name] = text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        hasValue = true;
                    }
                }

                if (hasValue)
                {
                    values["__row"] = (r + 1).ToString(CultureInfo.InvariantCulture);
                    rows.Add(values);
                }
            }

            return rows;
        }

        private static List<FixedAssetImportRow> ReadAssetRows(Stream stream)
        {
            var sheet = ReadSheet(stream);
            if (sheet.Count > 0 && !sheet[0].ContainsKey("资产编号"))
            {
                throw new FixedAssetException("模板不正确：缺少「资产编号」列。");
            }

            string? Get(Dictionary<string, string?> row, string key)
                => row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

            return sheet.Select(row => new FixedAssetImportRow
            {
                Row = int.TryParse(Get(row, "__row"), out var n) ? n : 0,
                Code = Get(row, "资产编号"),
                CategoryCode = Get(row, "资产类别编码"),
                CategoryName = Get(row, "资产类别"),
                Name = Get(row, "资产名称"),
                Model = Get(row, "规格型号"),
                Dept = Get(row, "使用部门"),
                StartDate = Get(row, "开始使用日期"),
                OriginValue = Get(row, "资产原值"),
                AccumulatedDepreciation = Get(row, "累计折旧"),
            }).ToList();
        }

        private static bool IsFileError(Exception ex)
            => ex is IOException
                or NPOI.OpenXml4Net.Exceptions.InvalidFormatException
                or ICSharpCode.SharpZipLib.Zip.ZipException;
    }

    /// <summary>批量清理资产请求。</summary>
    public sealed class BulkClearAssetsRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];

        public DateOnly ClearedDate { get; init; }
    }

    /// <summary>清理资产请求。</summary>
    public sealed class ClearAssetRequest
    {
        public DateOnly ClearedDate { get; init; }
    }

    /// <summary>期间计提请求。</summary>
    public sealed class DepreciatePeriodRequest
    {
        /// <summary>计提期间（yyyyMM）。</summary>
        public int Period { get; init; }
    }
}
