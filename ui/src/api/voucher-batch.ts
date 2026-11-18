/**
 * 凭证批量能力的手写 API 封装：合并暂存凭证、批量修改摘要 / 辅助核算。
 * 对应 Modules/Vouchers 的 merge 与 batch-update 端点，apis-gen 尚未重新生成。
 */
import { apiRequest } from "@/api/request"

export interface MergeVouchersResult {
  id: string
  voucherNo: string
}

export interface BatchAssistUpdatePayload {
  accountId: string
  /** 辅助核算类别（1 客户 / 2 供应商 / 3 职员 / 4 部门 / 5 项目 / 6 存货 / 7 现金流）。 */
  type: number
  /** 档案标识；不传表示清除该类别取值。 */
  itemId?: string | null
}

export interface BatchUpdateVouchersPayload {
  ids: string[]
  summary?: string
  assist?: BatchAssistUpdatePayload
}

/** 合并暂存凭证：返回合并后的凭证。需二次确认（源凭证被删除）。 */
export function mergeVouchers(
  accountSetId: string,
  ids: string[],
  token?: string | null,
  extraHeaders?: Record<string, string>,
): Promise<MergeVouchersResult> {
  return apiRequest(
    `/accountsets/${accountSetId}/vouchers/merge`,
    token,
    {
      method: "POST",
      body: JSON.stringify({ ids }),
    },
    extraHeaders,
  )
}

/** 批量修改暂存凭证，返回实际修改的凭证张数。 */
export function batchUpdateVouchers(
  accountSetId: string,
  payload: BatchUpdateVouchersPayload,
  token?: string | null,
): Promise<{ updated: number }> {
  return apiRequest(`/accountsets/${accountSetId}/vouchers/batch-update`, token, {
    method: "POST",
    body: JSON.stringify(payload),
  })
}
