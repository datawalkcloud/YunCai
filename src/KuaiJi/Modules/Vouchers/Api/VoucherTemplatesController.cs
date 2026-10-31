using System.Globalization;
using KuaiJi.Modules.Vouchers.Application;
using KuaiJi.Modules.Vouchers.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace KuaiJi.Modules.Vouchers.Api
{
    /// <summary>凭证模板接口：分录骨架的套用字典，行整体替换；支持 xlsx 导入导出。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/vouchertemplates")]
    [Authorize]
    public class VoucherTemplatesController : ControllerBase
    {
        private readonly VoucherTemplateService _templateService;

        public VoucherTemplatesController(VoucherTemplateService templateService)
        {
            _templateService = templateService;
        }

        /// <summary>获取全部凭证模板（含模板行与科目解析）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<VoucherTemplateDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _templateService.ListAsync(accountSetId, cancellationToken));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建凭证模板。</summary>
        [HttpPost]
        public async Task<ActionResult<VoucherTemplateDto>> Create(Guid accountSetId, [FromBody] SaveVoucherTemplateRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _templateService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改凭证模板（整体替换模板行）。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<VoucherTemplateDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveVoucherTemplateRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var template = await _templateService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return template is null ? NotFound() : Ok(template);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除凭证模板。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _templateService.DeleteAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>导出全部凭证模板为 .xlsx。</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _templateService.ExportAsync(accountSetId, cancellationToken);
                return File(bytes, VoucherTemplateExportBuilder.ContentType, "凭证模板.xlsx");
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载凭证模板导入模板。</summary>
        [HttpGet("import-template")]
        public IActionResult DownloadImportTemplate()
            => File(_templateService.BuildImportTemplate(), VoucherTemplateExportBuilder.ContentType, "凭证模板导入模板.xlsx");

        /// <summary>导入凭证模板（按模板名称分组，同名跳过）。</summary>
        [HttpPost("import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<VoucherTemplateImportResultDto>> Import(Guid accountSetId, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var rows = ReadRows(stream);
                return Ok(await _templateService.ImportAsync(accountSetId, rows, cancellationToken));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (ex is IOException or NPOI.OpenXml4Net.Exceptions.InvalidFormatException or ICSharpCode.SharpZipLib.Zip.ZipException)
            {
                return BadRequest(new { message = "文件读取失败，请确认是 .xlsx 格式的导入模板。" });
            }
        }

        /// <summary>把工作表读成凭证模板导入行集合（列名取自首行）。</summary>
        private static List<VoucherTemplateImportRow> ReadRows(Stream stream)
        {
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0) ?? throw new VoucherException("文件中没有工作表。");
            var headerRow = sheet.GetRow(0) ?? throw new VoucherException("文件缺少表头行。");

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
                throw new VoucherException("文件缺少表头行。");
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

            return rows.Select(row => new VoucherTemplateImportRow
            {
                Row = int.TryParse(row.GetValueOrDefault("__row"), out var n) ? n : 0,
                Name = row.GetValueOrDefault("模板名称"),
                TypeText = row.GetValueOrDefault("模板类型"),
                Summary = row.GetValueOrDefault("摘要"),
                AccountCode = row.GetValueOrDefault("科目编码"),
                Direction = row.GetValueOrDefault("方向"),
            }).ToList();
        }
    }
}
