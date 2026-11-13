using System.Globalization;
using YunCai.Modules.Salary.Application;
using MiniExcelLibs;

namespace YunCai.Modules.Salary.Infrastructure
{
    /// <summary>
    /// 工资模块导出生成器（MiniExcel）：
    /// 员工名册、员工导入模板、工资统计表、当期工资表。
    /// 列名与导入解析一一对应，导出的文件可直接改完再导回。
    /// </summary>
    public static class SalaryExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>员工名册导出（与导入模板同列，便于回填后原样导入）。</summary>
        public static byte[] BuildEmployees(IReadOnlyList<EmployeeDto> employees)
        {
            var rows = employees.Select(e => new Dictionary<string, object?>
            {
                ["编号"] = e.Code,
                ["姓名"] = e.Name,
                ["性别"] = e.GenderText,
                ["证件类型"] = e.IdTypeText,
                ["证件号码"] = e.IdNo ?? string.Empty,
                ["部门"] = e.Dept,
                ["手机"] = e.Phone ?? string.Empty,
                ["邮箱"] = e.Email ?? string.Empty,
                ["入职日期"] = e.HireDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                ["计提工资科目编码"] = CodeOf(e.SubjectText),
                ["计提工资科目名称"] = NameOf(e.SubjectText),
                ["员工状态"] = e.StatusText,
                ["备注"] = e.Remark ?? string.Empty,
            }).ToList();

            if (rows.Count == 0)
            {
                rows.Add(EmptyEmployeeRow());
            }

            return Save(rows, "员工");
        }

        /// <summary>员工导入模板（仅表头 + 一行示例说明之外的空行）。</summary>
        public static byte[] BuildEmployeeTemplate()
            => Save([EmptyEmployeeRow()], "员工");

        /// <summary>工资统计表导出（按部门汇总，末行为合计）。</summary>
        public static byte[] BuildSummary(IReadOnlyList<SalarySummaryRowDto> rows)
        {
            var data = rows.Select(r => new Dictionary<string, object?>
            {
                ["部门"] = r.Dept,
                ["人数"] = r.EmployeeCount,
                ["应发工资"] = r.Payable,
                ["代扣个人款项"] = r.PersonalDeduction,
                ["公司承担款项"] = r.CompanyPart,
                ["实发工资"] = r.Net,
                ["员工成本"] = r.Cost,
            }).ToList();

            data.Add(new Dictionary<string, object?>
            {
                ["部门"] = "合计",
                ["人数"] = rows.Sum(r => r.EmployeeCount),
                ["应发工资"] = rows.Sum(r => r.Payable),
                ["代扣个人款项"] = rows.Sum(r => r.PersonalDeduction),
                ["公司承担款项"] = rows.Sum(r => r.CompanyPart),
                ["实发工资"] = rows.Sum(r => r.Net),
                ["员工成本"] = rows.Sum(r => r.Cost),
            });

            return Save(data, "工资统计表");
        }

        /// <summary>当期工资表导出（一行一员工，末行合计）。</summary>
        public static byte[] BuildBills(IReadOnlyList<SalaryBillDto> bills, int period)
        {
            var data = bills.Select(b => new Dictionary<string, object?>
            {
                ["期间"] = PeriodText(period),
                ["编号"] = b.EmployeeCode,
                ["姓名"] = b.EmployeeName,
                ["部门"] = b.Dept,
                ["工资总额"] = b.Gross,
                ["应扣工资"] = b.Deductible,
                ["应发工资"] = b.Payable,
                ["代扣个人款项"] = b.PersonalDeduction,
                ["个税免征额"] = b.TaxThreshold,
                ["本月应缴个税"] = b.Tax,
                ["实发工资调整"] = b.Adjust,
                ["实发工资"] = b.Net,
                ["公司承担款项"] = b.CompanyPart,
                ["员工成本"] = b.Cost,
                ["计提凭证"] = b.AccruedVoucherText ?? string.Empty,
                ["发放凭证"] = b.PaidVoucherText ?? string.Empty,
            }).ToList();

            if (data.Count == 0)
            {
                data.Add(new Dictionary<string, object?>
                {
                    ["期间"] = PeriodText(period),
                    ["编号"] = string.Empty,
                    ["姓名"] = string.Empty,
                    ["部门"] = string.Empty,
                    ["工资总额"] = 0m,
                    ["应扣工资"] = 0m,
                    ["应发工资"] = 0m,
                    ["代扣个人款项"] = 0m,
                    ["个税免征额"] = 0m,
                    ["本月应缴个税"] = 0m,
                    ["实发工资调整"] = 0m,
                    ["实发工资"] = 0m,
                    ["公司承担款项"] = 0m,
                    ["员工成本"] = 0m,
                    ["计提凭证"] = string.Empty,
                    ["发放凭证"] = string.Empty,
                });
            }

            return Save(data, "工资表");
        }

        /// <summary>期间 yyyyMM → yyyy-MM。</summary>
        public static string PeriodText(int period) => $"{period / 100:D4}-{period % 100:D2}";

        private static Dictionary<string, object?> EmptyEmployeeRow() => new()
        {
            ["编号"] = string.Empty,
            ["姓名"] = string.Empty,
            ["性别"] = string.Empty,
            ["证件类型"] = string.Empty,
            ["证件号码"] = string.Empty,
            ["部门"] = string.Empty,
            ["手机"] = string.Empty,
            ["邮箱"] = string.Empty,
            ["入职日期"] = string.Empty,
            ["计提工资科目编码"] = string.Empty,
            ["计提工资科目名称"] = string.Empty,
            ["员工状态"] = string.Empty,
            ["备注"] = string.Empty,
        };

        private static byte[] Save(List<Dictionary<string, object?>> rows, string sheetName)
        {
            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, new Dictionary<string, object> { [sheetName] = rows }, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        /// <summary>「5602001 管理人员职工薪酬」→ 5602001。</summary>
        private static string CodeOf(string? subjectText)
        {
            if (string.IsNullOrWhiteSpace(subjectText))
            {
                return string.Empty;
            }

            var index = subjectText.IndexOf(' ', StringComparison.Ordinal);
            return index < 0 ? subjectText.Trim() : subjectText[..index].Trim();
        }

        /// <summary>「5602001 管理人员职工薪酬」→ 管理人员职工薪酬。</summary>
        private static string NameOf(string? subjectText)
        {
            if (string.IsNullOrWhiteSpace(subjectText))
            {
                return string.Empty;
            }

            var index = subjectText.IndexOf(' ', StringComparison.Ordinal);
            return index < 0 ? string.Empty : subjectText[(index + 1)..].Trim();
        }
    }
}
