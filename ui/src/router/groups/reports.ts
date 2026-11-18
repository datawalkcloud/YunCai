/**
 * 报表域 chunk：资产负债表、利润表（既有实页）与全部报表页
 * （季报/现金流量表三种口径/部门与项目利润/数据透视/费用明细/权益变动/纳税统计/财务概要）。
 * 仅供 router 动态 import，禁止静态引入。
 */
export { default as BalanceSheet } from "@/pages/reports/balance-sheet";
export { default as IncomeStatement } from "@/pages/reports/income-statement";
export {
    IncomeQuarter,
    CashFlow,
    CashFlowQuarter,
    StandardCashFlow,
    DepartmentIncome,
    ProjectIncome,
    Pivot,
    ExpenseDetail,
    EquityChange,
    TaxReport,
    FinancialSummary,
} from "@/pages/business/reports";
