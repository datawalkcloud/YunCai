using KuaiJi.Modules.FixedAssets.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.FixedAssets.Api
{
    /// <summary>
    /// 固定资产接口：类别 / 卡片 / 变更记录 / 期间计提 / 折旧摊销明细表 / 资产汇总表 / 资产核对总账。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/fixedassets")]
    [Authorize]
    public class FixedAssetsController : ControllerBase
    {
        private readonly FixedAssetService _assetService;

        public FixedAssetsController(FixedAssetService assetService)
        {
            _assetService = assetService;
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

        /// <summary>获取全部资产卡片。</summary>
        [HttpGet("assets")]
        public async Task<ActionResult<IReadOnlyList<FixedAssetDto>>> GetAssets(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assetService.GetAssetsAsync(accountSetId, cancellationToken));
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

        /// <summary>删除资产卡片。</summary>
        [HttpDelete("assets/{id:guid}")]
        public async Task<IActionResult> DeleteAsset(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _assetService.DeleteAssetAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
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

        /// <summary>获取变更记录（可按资产过滤）。</summary>
        [HttpGet("changes")]
        public async Task<ActionResult<IReadOnlyList<FixedAssetChangeDto>>> GetChanges(Guid accountSetId, [FromQuery] Guid? assetId, CancellationToken cancellationToken)
        {
            return Ok(await _assetService.GetChangesAsync(accountSetId, assetId, cancellationToken));
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

        /// <summary>资产核对总账（类别分组：会计科目 vs 资产卡片 + 差异）。</summary>
        [HttpGet("check")]
        public async Task<ActionResult<IReadOnlyList<FixedAssetCheckRowDto>>> GetCheck(Guid accountSetId, CancellationToken cancellationToken)
        {
            return Ok(await _assetService.GetCheckAsync(accountSetId, cancellationToken));
        }
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
