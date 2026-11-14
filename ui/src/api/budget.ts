/**
 * 预算管理的手写 API 封装（对应 Modules/Budgets）。
 *
 * DTO 为新增契约，apis-gen 尚未重新生成，页面直接走 fetch；
 * 后端契约改动时同步维护这里的类型。
 */
import { apiRequest } from "@/api/request"

export interface BudgetSummaryDto {
  id: string
  year: number
  name: string
  note?: string | null
  lineCount: number
  total: number
  createdBy: string
  createdAt: string
  updatedAt: string
}

export interface BudgetLineDto {
  accountId: string
  accountCode: string
  accountName: string
  isLeaf: boolean
  /** 12 个月金额，下标 0 为 1 月。 */
  months: number[]
  total: number
}

export interface BudgetDto {
  id: string
  year: number
  name: string
  note?: string | null
  createdBy: string
  createdAt: string
  updatedAt: string
  lines: BudgetLineDto[]
  total: number
}

export interface BudgetExecutionRowDto {
  accountId: string
  accountCode: string
  accountName: string
  /** 1 借 / 2 贷。 */
  direction: number
  budgetMonths: number[]
  actualMonths: number[]
  budgetToDate: number
  actualToDate: number
  /** 实际 - 预算；正数表示超出预算（收入类为超额完成）。 */
  variance: number
  rate?: number | null
}

export interface BudgetExecutionDto {
  budgetId: string
  year: number
  name: string
  note?: string | null
  throughMonth: number
  rows: BudgetExecutionRowDto[]
  budgetMonths: number[]
  actualMonths: number[]
  budgetToDate: number
  actualToDate: number
  variance: number
  rate?: number | null
}

export interface SaveBudgetLinePayload {
  accountId: string
  months: number[]
}

export interface SaveBudgetPayload {
  year: number
  name: string
  note?: string | null
  lines: SaveBudgetLinePayload[]
}

function budgetPath(accountSetId: string, suffix = ""): string {
  return `/accountsets/${accountSetId}/budgets${suffix}`
}

export function listBudgets(accountSetId: string, year: number, token?: string | null): Promise<BudgetSummaryDto[]> {
  return apiRequest(budgetPath(accountSetId, `?year=${year}`), token)
}

export function getBudget(accountSetId: string, id: string, token?: string | null): Promise<BudgetDto> {
  return apiRequest(budgetPath(accountSetId, `/${id}`), token)
}

export function createBudget(
  accountSetId: string,
  payload: SaveBudgetPayload,
  token?: string | null,
): Promise<BudgetDto> {
  return apiRequest(budgetPath(accountSetId), token, { method: "POST", body: JSON.stringify(payload) })
}

export function updateBudget(
  accountSetId: string,
  id: string,
  payload: SaveBudgetPayload,
  token?: string | null,
): Promise<BudgetDto> {
  return apiRequest(budgetPath(accountSetId, `/${id}`), token, {
    method: "PUT",
    body: JSON.stringify(payload),
  })
}

export function deleteBudget(accountSetId: string, id: string, token?: string | null): Promise<void> {
  return apiRequest(budgetPath(accountSetId, `/${id}`), token, { method: "DELETE" })
}

export function getBudgetExecution(
  accountSetId: string,
  id: string,
  throughMonth: number,
  token?: string | null,
): Promise<BudgetExecutionDto> {
  return apiRequest(budgetPath(accountSetId, `/${id}/execution?throughMonth=${throughMonth}`), token)
}
