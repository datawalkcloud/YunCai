using KuaiJi.Modules.Salary.Domain;
using KuaiJi.Modules.Tax.Domain;

namespace KuaiJi.Modules.Tax.Application
{
    /// <summary>
    /// 个税「报表填写」取数：把本系统工资模块的月度工资单按累计预扣法算成综合所得申报表。
    /// 只申报本期有工资的员工；累计列取本年 1 月至所属期，已缴税额取 1 月至上月。
    /// </summary>
    public sealed class IitReportService
    {
        private readonly ISalaryRepository _salary;

        public IitReportService(ISalaryRepository salary)
        {
            _salary = salary;
        }

        public async Task<TaxIitReportResultDto> BuildAsync(
            Guid accountSetId,
            int year,
            int month,
            CancellationToken cancellationToken)
        {
            var employees = new Dictionary<Guid, Employee>();
            foreach (var employee in await _salary.GetEmployeesAsync(accountSetId, cancellationToken))
            {
                employees[employee.Id] = employee;
            }

            // 员工 → 月份 → 工资单；只取本年 1 月到所属期。
            var bills = new Dictionary<Guid, Dictionary<int, SalaryBill>>();
            for (var m = 1; m <= month; m++)
            {
                foreach (var bill in await _salary.GetBillsAsync(accountSetId, year * 100 + m, cancellationToken))
                {
                    if (!bills.TryGetValue(bill.EmployeeId, out var perMonth))
                    {
                        perMonth = new Dictionary<int, SalaryBill>();
                        bills[bill.EmployeeId] = perMonth;
                    }

                    perMonth[m] = bill;
                }
            }

            var rows = new List<TaxIitReportRowDto>();
            foreach (var (employeeId, perMonth) in bills)
            {
                if (!perMonth.TryGetValue(month, out var current) || !employees.TryGetValue(employeeId, out var employee))
                {
                    continue;
                }

                rows.Add(BuildRow(year, month, employee, current, perMonth));
            }

            rows.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));

            var totalIncome = Round(rows.Sum(r => r.CurrentIncome));
            var totalRefundable = Round(rows.Sum(r => r.Refundable));
            return new TaxIitReportResultDto
            {
                Year = year,
                Month = month,
                Rows = rows,
                PersonCount = rows.Count,
                TotalIncome = totalIncome,
                TotalRefundable = totalRefundable,
                Message = rows.Count == 0
                    ? $"{year}年{month}月还没有工资表数据，请先到工资模块登记当期工资。"
                    : null,
            };
        }

        private static TaxIitReportRowDto BuildRow(
            int year,
            int month,
            Employee employee,
            SalaryBill current,
            Dictionary<int, SalaryBill> perMonth)
        {
            var cumulativeIncome = 0m;
            var cumulativeSpecialDeduction = 0m;
            var paidTax = 0m;
            for (var m = 1; m <= month; m++)
            {
                if (!perMonth.TryGetValue(m, out var bill))
                {
                    continue;
                }

                cumulativeIncome += bill.Payable;
                cumulativeSpecialDeduction += bill.PersonalDeduction;
                if (m < month)
                {
                    paidTax += bill.Tax;
                }
            }

            var standard = current.TaxThreshold;
            var months = DeductionMonths(year, month, employee.HireDate);
            var taxable = Round(
                cumulativeIncome
                - standard * months
                - cumulativeSpecialDeduction);
            if (taxable < 0m)
            {
                taxable = 0m;
            }

            var (rate, quickDeduction) = IitWithholdingTable.Lookup(taxable);
            var cumulativeTax = Round(taxable * rate - quickDeduction);
            if (cumulativeTax < 0m)
            {
                cumulativeTax = 0m;
            }

            return new TaxIitReportRowDto
            {
                Key = employee.Id.ToString("N"),
                Code = employee.Code,
                Name = employee.Name,
                IdNo = employee.IdNo ?? string.Empty,
                CurrentIncome = Round(current.Payable),
                StandardDeduction = standard,
                CumulativeIncome = Round(cumulativeIncome),
                CumulativeStandardDeduction = Round(standard * months),
                CumulativeSpecialDeduction = Round(cumulativeSpecialDeduction),
                Remark = employee.Remark ?? string.Empty,
                TaxableIncome = taxable,
                Rate = rate,
                QuickDeduction = quickDeduction,
                CumulativeTax = cumulativeTax,
                PaidTax = Round(paidTax),
                Refundable = Round(cumulativeTax - paidTax),
            };
        }

        /// <summary>本年 1 月至所属期的任职受雇月份数：当年入职的从入职月起算。</summary>
        private static int DeductionMonths(int year, int month, DateOnly? hireDate)
        {
            if (hireDate is { } hire && hire.Year == year)
            {
                return Math.Clamp(month - hire.Month + 1, 0, 12);
            }

            return month;
        }

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
