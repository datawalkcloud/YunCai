/**
 * 期末结转 / 结账的手写 API 封装。
 *
 * 这些接口的 DTO 是本期新增的（apis-gen 尚未重新生成），为避免与生成代码互相覆盖，
 * 页面直接走 fetch；后端契约改动时同步维护这里的类型。
 */
const API_BASE = "/api";

export interface ClosingPeriodDto {
  year: number;
  month: number;
  periodText: string;
  /** NotCarried（未结转、未结账）/ Carried（已结转、未结账）/ Closed（已结转、已结账）。 */
  state: string;
  voucherCount: number;
  unpostedCount: number;
}

export interface ClosingStatusDto {
  startPeriod: string;
  carriedPeriod?: string | null;
  closedPeriod?: string | null;
  nextCarryYear: number;
  nextCarryMonth: number;
  nextCloseYear: number;
  nextCloseMonth: number;
  canReopen: boolean;
  canCancelCarry: boolean;
  periods: ClosingPeriodDto[];
}

export interface ClosingCheckItemDto {
  key: string;
  title: string;
  /** ok / warn / error。 */
  status: string;
  message: string;
  /** blocking（影响结账）/ info（仅提示）。 */
  group: string;
  actionKey?: string | null;
  actionText?: string | null;
}

export interface ClosingCheckResultDto {
  year: number;
  month: number;
  periodText: string;
  canCarry: boolean;
  canClose: boolean;
  items: ClosingCheckItemDto[];
}

export interface PreCloseItemDto {
  key: string;
  title: string;
  amount: number;
  available: boolean;
  /** ledger（本模块生成）/ check（已入账回显）/ salary（工资模块）/ template（自定义结转模板）/ unsupported。 */
  source: string;
  hint: string;
  generated: boolean;
  voucherLabel?: string | null;
  /** 已生成凭证的标识（用于跳转打印）。 */
  voucherId?: string | null;
}

export interface PreCloseListDto {
  year: number;
  month: number;
  periodText: string;
  items: PreCloseItemDto[];
}

export interface CarryVoucherRefDto {
  id: string;
  word: string;
  number: number;
  label: string;
  summary: string;
  kind: string;
}

export interface GeneratePreCloseResultDto {
  message: string;
  voucher?: CarryVoucherRefDto | null;
}

export interface CarryPreviewLineDto {
  summary: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  /** 该行过账后的科目余额（按科目余额方向取正）。 */
  balance: number;
}

export interface CarryPreviewVoucherDto {
  kind: string;
  word: string;
  number: number;
  label: string;
  date: string;
  summary: string;
  totalDebit: number;
  totalCredit: number;
  lines: CarryPreviewLineDto[];
}

export interface CarryForwardPreviewDto {
  year: number;
  month: number;
  periodText: string;
  alreadyCarried: boolean;
  needsCarry: boolean;
  profitAmount: number;
  /** 年末「本年利润」余额（正=盈利/贷方余额，负=亏损），仅已结转的 12 月有值。 */
  retainedProfitAmount: number;
  /** 本期是否已生成「结转未分配利润」凭证。 */
  hasRetainedVoucher: boolean;
  /** 年末待生成的「本年利润 → 未分配利润」凭证预览；已生成或无需结转时为空。 */
  retainedVoucher?: CarryPreviewVoucherDto | null;
  vouchers: CarryPreviewVoucherDto[];
  warnings: string[];
}

export interface TransferRetainedResultDto {
  message: string;
  voucher?: CarryVoucherRefDto | null;
}

export interface CarryForwardResultDto {
  periodText: string;
  vouchers: CarryVoucherRefDto[];
  profitAmount: number;
  message: string;
}

export interface CloseBatchResultDto {
  message: string;
  closedPeriods: string[];
  status: ClosingStatusDto;
}

export interface CarryBatchResultDto {
  message: string;
  markedPeriods: string[];
  status: ClosingStatusDto;
}

export interface ClosingTemplateOptionDto {
  id: string;
  name: string;
  isEnabled: boolean;
}

export interface CarryForwardOptions {
  year: number;
  month: number;
  splitIncomeCost: boolean;
  transferToRetainedEarnings: boolean;
  templateIds: string[];
}

export interface PreCloseGenerateRequest {
  year: number;
  month: number;
  key: string;
}

function headers(token?: string | null): Headers {
  const result = new Headers({ "Content-Type": "application/json" });
  if (token) {
    result.set("Authorization", `Bearer ${token}`);
  }
  return result;
}

async function request<T>(
  path: string,
  token: string | null | undefined,
  init?: RequestInit,
  stepUpToken?: string,
): Promise<T> {
  const merged = headers(token);
  if (stepUpToken) {
    merged.set("X-Step-Up-Token", stepUpToken);
  }

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers: merged });
  if (!res.ok) {
    let message = `请求失败（${res.status}）`;
    let code: string | undefined;
    try {
      const body = await res.json();
      if (body && typeof body === "object") {
        if (typeof body.message === "string" && body.message) {
          message = body.message;
        }
        if (typeof body.code === "string") {
          code = body.code;
        }
      }
    } catch {
      // 非 JSON 响应体，保留状态码提示。
    }
    const error = new Error(message) as Error & { status?: number; code?: string };
    error.status = res.status;
    error.code = code;
    throw error;
  }
  if (res.status === 204) {
    return undefined as T;
  }
  return (await res.json()) as T;
}

function closingPath(accountSetId: string, suffix = ""): string {
  return `/accountsets/${accountSetId}/closing${suffix}`;
}

export function getClosingStatus(accountSetId: string, token?: string | null): Promise<ClosingStatusDto> {
  return request(closingPath(accountSetId), token);
}

export function getClosingChecks(
  accountSetId: string,
  year: number,
  month: number,
  token?: string | null,
): Promise<ClosingCheckResultDto> {
  return request(closingPath(accountSetId, `/checks?year=${year}&month=${month}`), token);
}

export function getPreCloseList(
  accountSetId: string,
  year: number,
  month: number,
  token?: string | null,
): Promise<PreCloseListDto> {
  return request(closingPath(accountSetId, `/pre-close?year=${year}&month=${month}`), token);
}

export function generatePreCloseVoucher(
  accountSetId: string,
  body: PreCloseGenerateRequest,
  token?: string | null,
): Promise<GeneratePreCloseResultDto> {
  return request(closingPath(accountSetId, "/pre-close/generate"), token, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function previewCarryForward(
  accountSetId: string,
  body: CarryForwardOptions,
  token?: string | null,
): Promise<CarryForwardPreviewDto> {
  return request(closingPath(accountSetId, "/carry-forward/preview"), token, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function carryForward(
  accountSetId: string,
  body: CarryForwardOptions,
  token?: string | null,
  stepUpToken?: string,
): Promise<CarryForwardResultDto> {
  return request(closingPath(accountSetId, "/carry-forward"), token, {
    method: "POST",
    body: JSON.stringify(body),
  }, stepUpToken);
}

export function transferRetainedEarnings(
  accountSetId: string,
  year: number,
  month: number,
  token?: string | null,
): Promise<TransferRetainedResultDto> {
  return request(closingPath(accountSetId, "/retained-earnings"), token, {
    method: "POST",
    body: JSON.stringify({ year, month }),
  });
}

export function closePeriod(
  accountSetId: string,
  year: number,
  month: number,
  token?: string | null,
  stepUpToken?: string,
): Promise<ClosingStatusDto> {
  return request(closingPath(accountSetId, "/close"), token, {
    method: "POST",
    body: JSON.stringify({ year, month }),
  }, stepUpToken);
}

export function closeBatch(
  accountSetId: string,
  token?: string | null,
  stepUpToken?: string,
): Promise<CloseBatchResultDto> {
  return request(closingPath(accountSetId, "/close-batch"), token, { method: "POST" }, stepUpToken);
}

/** 批量标记结转：把损益余额已为 0 的连续期间批量标记为已结转，不生成凭证。 */
export function carryBatch(
  accountSetId: string,
  token?: string | null,
  stepUpToken?: string,
): Promise<CarryBatchResultDto> {
  return request(closingPath(accountSetId, "/carry-batch"), token, { method: "POST" }, stepUpToken);
}

export function reopenPeriod(
  accountSetId: string,
  year: number,
  month: number,
  token?: string | null,
  stepUpToken?: string,
): Promise<ClosingStatusDto> {
  return request(closingPath(accountSetId, `/reopen?year=${year}&month=${month}`), token, {
    method: "POST",
  }, stepUpToken);
}

export function cancelCarry(
  accountSetId: string,
  year: number,
  month: number,
  token?: string | null,
  stepUpToken?: string,
): Promise<{ message: string }> {
  return request(closingPath(accountSetId, "/cancel-carry"), token, {
    method: "POST",
    body: JSON.stringify({ year, month }),
  }, stepUpToken);
}

export interface RenumberGapsResultDto {
  message: string;
  /** 本次实际调整凭证号的凭证张数。 */
  changed: number;
}

/** 整理断号：把本期存在断号的凭证字重新连续编号（仅改凭证号）。 */
export function renumberGaps(
  accountSetId: string,
  year: number,
  month: number,
  token?: string | null,
): Promise<RenumberGapsResultDto> {
  return request(closingPath(accountSetId, "/renumber"), token, {
    method: "POST",
    body: JSON.stringify({ year, month }),
  });
}

export function getClosingTemplates(accountSetId: string, token?: string | null): Promise<ClosingTemplateOptionDto[]> {
  return request(closingPath(accountSetId, "/templates"), token);
}
