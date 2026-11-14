/**
 * 报表行下钻的手写 API 封装（对应 Reports 的 balance-sheet/income-statement drilldown 端点）。
 * apis-gen 尚未重新生成，页面直接走 fetch；后端契约改动时同步维护这里的类型。
 */
import { apiRequest } from "@/api/request"

/** 下钻明细的一个科目：金额已按报表行方向换算，可直接与报表行相加对照。 */
export interface ReportDrilldownAccountDto {
  accountId: string
  code: string
  name: string
  /** 主金额：资产负债表为期末余额、利润表为本期金额。 */
  current: number
  /** 参照金额：资产负债表为年初余额、利润表为本年累计金额。 */
  yearToDate: number
}

export interface ReportDrilldownDto {
  line: number
  year: number
  month: number
  periodText: string
  accounts: ReportDrilldownAccountDto[]
  totalCurrent: number
  totalYearToDate: number
}

export type ReportDrilldownKind = "balance-sheet" | "income-statement"

/** 拉取某报表行次的科目级拆解（合计与报表行同源同口径）。 */
export function getReportLineDrilldown(
  accountSetId: string,
  kind: ReportDrilldownKind,
  params: { year: number; month: number; line: number },
  token?: string | null,
): Promise<ReportDrilldownDto> {
  const query = new URLSearchParams({
    year: String(params.year),
    month: String(params.month),
    line: String(params.line),
  })
  return apiRequest(`/accountsets/${accountSetId}/Reports/${kind}/drilldown?${query.toString()}`, token)
}
