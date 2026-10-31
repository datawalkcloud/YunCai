using KuaiJi.Modules.Funds.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace KuaiJi.Modules.Funds.Api
{
    /// <summary>
    /// 收支类别接口。类别是日记账记账时对流水归类的字典项（分收入 / 支出两类），
    /// 归属于某一账套；分录按类别名称关联，删除类别不影响既有流水。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/entrycategories")]
    [Authorize]
    public class EntryCategoriesController : ControllerBase
    {
        private static readonly string[] ExportColumns =
        [
            "类型", "编码", "名称", "上级类别", "关联现金流", "智能匹配关键字", "启用状态",
        ];

        private static readonly string[] ImportColumns =
        [
            "类型", "编码", "名称", "上级类别", "关联现金流", "智能匹配关键字",
        ];

        private readonly EntryCategoryService _categoryService;

        public EntryCategoriesController(EntryCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>获取指定账套的全部收支类别（按类型、编码排列）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<EntryCategoryDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _categoryService.ListAsync(accountSetId, cancellationToken));
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建收支类别。同一账套同一类型内名称不可重复。</summary>
        [HttpPost]
        public async Task<ActionResult<EntryCategoryDto>> Create(Guid accountSetId, [FromBody] CreateEntryCategoryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var category = await _categoryService.CreateAsync(accountSetId, request, cancellationToken);
                return Ok(category);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改类别。既有流水中的类别文本不随之变更。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<EntryCategoryDto>> Update(Guid accountSetId, Guid id, [FromBody] UpdateEntryCategoryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var category = await _categoryService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return category is null ? NotFound() : Ok(category);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>启用 / 停用类别（停用后不再出现在记账下拉中，历史数据保留）。</summary>
        [HttpPatch("{id:guid}/enabled")]
        public async Task<ActionResult<EntryCategoryDto>> SetEnabled(Guid accountSetId, Guid id, [FromBody] SetEntryCategoryEnabledRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var category = await _categoryService.SetEnabledAsync(accountSetId, id, request.Enabled, cancellationToken);
                return category is null ? NotFound() : Ok(category);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除类别。既有流水中的类别文本保留不变。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _categoryService.DeleteAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>导出收支类别（type 为空导出全部；1=收入，2=支出）。</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(Guid accountSetId, [FromQuery] int? type, CancellationToken cancellationToken)
        {
            try
            {
                var categories = await _categoryService.ListAsync(accountSetId, cancellationToken);
                if (type is 1 or 2)
                {
                    categories = categories.Where(c => c.Type == type).ToList();
                }

                var rows = categories
                    .Select(c => new Dictionary<string, object?>
                    {
                        ["类型"] = c.TypeText,
                        ["编码"] = c.Code,
                        ["名称"] = c.Name,
                        ["上级类别"] = c.ParentName ?? string.Empty,
                        ["关联现金流"] = c.CashFlowName ?? string.Empty,
                        ["智能匹配关键字"] = c.SmartKeywords ?? string.Empty,
                        ["启用状态"] = c.IsEnabled ? "启用" : "停用",
                    })
                    .ToList();

                if (rows.Count == 0)
                {
                    rows.Add(ExportColumns.ToDictionary(c => c, _ => (object?)string.Empty));
                }

                using var stream = new MemoryStream();
                MiniExcelLibs.MiniExcel.SaveAs(
                    stream,
                    new Dictionary<string, object> { ["收支类别"] = rows },
                    excelType: MiniExcelLibs.ExcelType.XLSX);
                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"收支类别_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载导入模板（表头 + 一行示例）。</summary>
        [HttpGet("import/template")]
        public IActionResult DownloadTemplate()
        {
            var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("收支类别");
            var header = sheet.CreateRow(0);
            for (var i = 0; i < ImportColumns.Length; i++)
            {
                header.CreateCell(i).SetCellValue(ImportColumns[i]);
            }

            var sample = sheet.CreateRow(1);
            sample.CreateCell(0).SetCellValue("收入");
            sample.CreateCell(1).SetCellValue("1");
            sample.CreateCell(2).SetCellValue("销售收入");
            sample.CreateCell(3).SetCellValue("");
            sample.CreateCell(4).SetCellValue("");
            sample.CreateCell(5).SetCellValue("销售,收入,回款");

            for (var i = 0; i < ImportColumns.Length; i++)
            {
                sheet.SetColumnWidth(i, 22 * 256);
            }

            using var stream = new MemoryStream();
            workbook.Write(stream, leaveOpen: true);
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "收支类别导入模板.xlsx");
        }

        /// <summary>导入收支类别（按名称去重，重复行跳过并告警）。</summary>
        [HttpPost("import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<EntryCategoryImportResult>> Import(Guid accountSetId, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var rows = ReadImportRows(stream);
                return Ok(await _categoryService.ImportAsync(accountSetId, rows, cancellationToken));
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (ex is IOException or NPOI.OpenXml4Net.Exceptions.InvalidFormatException or ICSharpCode.SharpZipLib.Zip.ZipException)
            {
                return BadRequest(new { message = "文件读取失败，请确认是 .xlsx 格式的导入模板。" });
            }
        }

        /// <summary>按表头定位各列后逐行读取（表头顺序不限）。</summary>
        private static List<EntryCategoryImportRow> ReadImportRows(Stream stream)
        {
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0) ?? throw new JournalException("文件中没有工作表。");
            var headerRow = sheet.GetRow(0) ?? throw new JournalException("文件缺少表头行。");

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < headerRow.LastCellNum; i++)
            {
                var text = headerRow.GetCell(i)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    index[text] = i;
                }
            }

            if (!index.ContainsKey("名称"))
            {
                throw new JournalException("模板不正确：缺少「名称」列。");
            }

            string? Cell(IRow row, string column)
                => index.TryGetValue(column, out var i)
                    ? row.GetCell(i) switch
                    {
                        null => null,
                        var cell => cell.CellType switch
                        {
                            CellType.Numeric => cell.NumericCellValue.ToString("0.##"),
                            _ => cell.ToString()?.Trim(),
                        },
                    }
                    : null;

            var rows = new List<EntryCategoryImportRow>();
            for (var r = 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                {
                    continue;
                }

                var name = Cell(row, "名称");
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(Cell(row, "编码")))
                {
                    continue;
                }

                rows.Add(new EntryCategoryImportRow
                {
                    TypeText = Cell(row, "类型"),
                    Code = Cell(row, "编码"),
                    Name = name,
                    ParentName = Cell(row, "上级类别"),
                    CashFlowName = Cell(row, "关联现金流"),
                    SmartKeywords = Cell(row, "智能匹配关键字"),
                });
            }

            return rows;
        }
    }

    /// <summary>启用 / 停用请求。</summary>
    public sealed class SetEntryCategoryEnabledRequest
    {
        public bool Enabled { get; init; }
    }
}
