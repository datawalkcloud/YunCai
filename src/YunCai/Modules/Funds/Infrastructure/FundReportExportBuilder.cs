using System.Globalization;
using YunCai.Modules.Funds.Application;
using MiniExcelLibs;

namespace YunCai.Modules.Funds.Infrastructure
{
    /// <summary>
    /// 资金报表导出生成器：把当前页签的汇总结果导出为 .xlsx。
    /// 分段（现金账户 / 银行账户、收入类别 / 支出类别 …）以「分组」列区分，段尾输出小计行，
    /// 多段时再补一行总计——与页面上的表格层级一致。
    /// </summary>
    public static class FundReportExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private static readonly string[] AccountColumns =
        [
            "分组", "编码", "账户", "期初余额", "收入总额", "支出总额", "余额", "收入笔数", "支出笔数",
        ];

        private static readonly string[] CategoryColumns =
        [
            "分组", "编码", "类别", "收入总额", "支出总额", "收入笔数", "支出笔数",
        ];

        private static readonly string[] CounterpartyColumns =
        [
            "分组", "往来单位", "收入总额", "支出总额", "收入笔数", "支出笔数",
        ];

        private static readonly string[] ProjectColumns =
        [
            "分组", "编码", "项目", "收入总额", "支出总额", "收入笔数", "支出笔数",
        ];

        private static readonly string[] DepartmentColumns =
        [
            "分组", "编码", "部门", "收入总额", "支出总额", "收入笔数", "支出笔数",
        ];

        /// <summary>组合维度列名依赖页面选定的主 / 次类别，按报表结果动态生成（流水上的项目/部门是文本，无编码可出）。</summary>
        private static string[] ComboColumnsOf(FundReportDto report)
        {
            var primary = report.PrimaryText ?? "收支类别";
            var secondary = report.SecondaryText ?? "项目";
            return ["分组", primary, secondary, "收入总额", "支出总额", "收入笔数", "支出笔数"];
        }

        /// <summary>该维度导出时使用的列（顺序即表头顺序）。</summary>
        public static string[] ColumnsOf(FundReportDto report) => report.Dimension switch
        {
            FundReportDimensions.Account => AccountColumns,
            FundReportDimensions.Category => CategoryColumns,
            FundReportDimensions.Counterparty => CounterpartyColumns,
            FundReportDimensions.Project => ProjectColumns,
            FundReportDimensions.Department => DepartmentColumns,
            FundReportDimensions.Combo => ComboColumnsOf(report),
            _ => CategoryColumns,
        };

        /// <summary>生成导出文件的字节内容。</summary>
        public static byte[] Build(FundReportDto report)
        {
            var dimension = report.Dimension;
            var rows = new List<Dictionary<string, object?>>();

            foreach (var section in report.Sections)
            {
                foreach (var row in section.Rows)
                {
                    rows.Add(ToRow(report, section.Name, row));
                }

                rows.Add(ToRow(report, section.Name, section.Subtotal));
            }

            if (report.Sections.Count > 1)
            {
                rows.Add(ToRow(report, "合计", report.Total));
            }

            if (rows.Count == 0)
            {
                rows.Add(ColumnsOf(report).ToDictionary(c => c, _ => (object?)string.Empty));
            }

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(
                stream,
                new Dictionary<string, object> { [SheetNameOf(dimension)] = rows },
                excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        /// <summary>工作表名（与页签同名）。</summary>
        public static string SheetNameOf(string dimension) => dimension switch
        {
            FundReportDimensions.Account => "按账户汇总",
            FundReportDimensions.Category => "按收支类别汇总",
            FundReportDimensions.Counterparty => "按往来单位汇总",
            FundReportDimensions.Project => "按项目汇总",
            FundReportDimensions.Department => "按部门汇总",
            FundReportDimensions.Combo => "按组合汇总",
            _ => "资金报表",
        };

        /// <summary>核对总账导出列（项目/名称/币别/期初余额/借方(收入)/贷方(支出)/余额 + 账面余额/差异）。</summary>
        public static readonly string[] ReconciliationColumns =
        [
            "项目", "名称", "币别", "期初余额", "借方(收入)", "贷方(支出)", "余额", "账面余额", "差异",
        ];

        /// <summary>生成核对总账导出文件：逐账户一行，末尾补合计行。</summary>
        public static byte[] BuildReconciliation(FundReconciliationDto data)
        {
            var rows = data.Rows
                .Select(r => new Dictionary<string, object?>
                {
                    ["项目"] = r.ProjectText,
                    ["名称"] = r.JournalName,
                    ["币别"] = r.Currency,
                    ["期初余额"] = Amount(r.OpeningBalance),
                    ["借方(收入)"] = Amount(r.Debit),
                    ["贷方(支出)"] = Amount(r.Credit),
                    ["余额"] = Amount(r.Balance),
                    ["账面余额"] = Amount(r.LedgerBalance),
                    ["差异"] = Amount(r.Difference),
                })
                .ToList();

            if (rows.Count == 0)
            {
                rows.Add(ReconciliationColumns.ToDictionary(c => c, _ => (object?)string.Empty));
            }
            else
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["项目"] = "合计",
                    ["名称"] = string.Empty,
                    ["币别"] = string.Empty,
                    ["期初余额"] = Amount(data.Rows.Sum(r => r.OpeningBalance)),
                    ["借方(收入)"] = Amount(data.Rows.Sum(r => r.Debit)),
                    ["贷方(支出)"] = Amount(data.Rows.Sum(r => r.Credit)),
                    ["余额"] = Amount(data.Rows.Sum(r => r.Balance)),
                    ["账面余额"] = Amount(data.Rows.Sum(r => r.LedgerBalance)),
                    ["差异"] = Amount(data.Rows.Sum(r => r.Difference)),
                });
            }

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(
                stream,
                new Dictionary<string, object> { ["核对总账"] = rows },
                excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        /// <summary>组合维度行：主类别与次类别的列名随页面选择变化。</summary>
        private static Dictionary<string, object?> ComboRow(FundReportDto report, string group, FundReportRowDto row)
        {
            var primary = report.PrimaryText ?? "收支类别";
            var secondary = report.SecondaryText ?? "项目";
            return new Dictionary<string, object?>
            {
                ["分组"] = group,
                [primary] = row.Name,
                [secondary] = row.SecondaryName ?? string.Empty,
                ["收入总额"] = Amount(row.TotalIncome),
                ["支出总额"] = Amount(row.TotalExpense),
                ["收入笔数"] = row.IncomeCount,
                ["支出笔数"] = row.ExpenseCount,
            };
        }

        private static Dictionary<string, object?> ToRow(FundReportDto report, string group, FundReportRowDto row)
        {
            return report.Dimension switch
            {
                FundReportDimensions.Account => new Dictionary<string, object?>
                {
                    ["分组"] = group,
                    ["编码"] = row.Code ?? string.Empty,
                    ["账户"] = row.Name,
                    ["期初余额"] = Amount(row.OpeningBalance),
                    ["收入总额"] = Amount(row.TotalIncome),
                    ["支出总额"] = Amount(row.TotalExpense),
                    ["余额"] = Amount(row.Balance),
                    ["收入笔数"] = row.IncomeCount,
                    ["支出笔数"] = row.ExpenseCount,
                },
                FundReportDimensions.Counterparty => new Dictionary<string, object?>
                {
                    ["分组"] = group,
                    ["往来单位"] = row.Name,
                    ["收入总额"] = Amount(row.TotalIncome),
                    ["支出总额"] = Amount(row.TotalExpense),
                    ["收入笔数"] = row.IncomeCount,
                    ["支出笔数"] = row.ExpenseCount,
                },
                FundReportDimensions.Project => new Dictionary<string, object?>
                {
                    ["分组"] = group,
                    ["编码"] = row.Code ?? string.Empty,
                    ["项目"] = row.Name,
                    ["收入总额"] = Amount(row.TotalIncome),
                    ["支出总额"] = Amount(row.TotalExpense),
                    ["收入笔数"] = row.IncomeCount,
                    ["支出笔数"] = row.ExpenseCount,
                },
                FundReportDimensions.Department => new Dictionary<string, object?>
                {
                    ["分组"] = group,
                    ["编码"] = row.Code ?? string.Empty,
                    ["部门"] = row.Name,
                    ["收入总额"] = Amount(row.TotalIncome),
                    ["支出总额"] = Amount(row.TotalExpense),
                    ["收入笔数"] = row.IncomeCount,
                    ["支出笔数"] = row.ExpenseCount,
                },
                FundReportDimensions.Combo => ComboRow(report, group, row),
                _ => new Dictionary<string, object?>
                {
                    ["分组"] = group,
                    ["编码"] = row.Code ?? string.Empty,
                    ["类别"] = row.Name,
                    ["收入总额"] = Amount(row.TotalIncome),
                    ["支出总额"] = Amount(row.TotalExpense),
                    ["收入笔数"] = row.IncomeCount,
                    ["支出笔数"] = row.ExpenseCount,
                },
            };
        }

        /// <summary>空值导出为空单元格，数值保留两位小数（与页面展示口径一致）。</summary>
        private static string Amount(decimal? value)
            => value is null ? string.Empty : value.Value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
