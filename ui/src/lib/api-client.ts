import { Configuration, ResponseError } from "@/apis-gen"

/**
 * 构造调用生成的 API 客户端（apis-gen）所需的配置：
 * - basePath 指向 Vite 代理前缀 /api（转发到后端时会去掉 /api）。
 * - 存在登录令牌时注入 Bearer 认证头。
 */
export function apiConfig(token?: string | null): Configuration {
    return new Configuration({
        basePath: "/api",
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    })
}

/** 带附加请求头的配置（高危操作的 X-Step-Up-Token 等）。 */
export function apiConfigWithHeaders(token?: string | null, extra?: Record<string, string>): Configuration {
    const headers: Record<string, string> = { ...extra }
    if (token) headers.Authorization = `Bearer ${token}`
    return new Configuration({ basePath: "/api", headers })
}

/** 401（登录态失效）的处理回调，由登录态模块注册。 */
export type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null
/** 上一次触发回调的时间戳，用于并发请求同时 401 时去重。 */
let lastUnauthorizedAt = 0
/** 去抖窗口：同一轮失效往往有多个请求同时返回 401，只交给处理器一次。 */
const UNAUTHORIZED_WINDOW_MS = 3000

/**
 * 注册（或传 null 注销）401 的统一处理回调。
 * 页面代码无需感知：各页 catch 里照常显示后端文案，跳登录页由这里集中完成。
 * 重新注册（如重新登录）会重置去抖窗口，保证下一轮失效仍能跳出。
 */
export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
    unauthorizedHandler = handler
    if (handler) lastUnauthorizedAt = 0
}

/**
 * 把 401 事件转交给已注册的回调；未注册（如登录页自身的 401）则什么都不做，
 * 去抖窗口内的重复事件直接丢弃，避免并发 401 触发多次跳转。
 */
function notifyUnauthorized(): void {
    if (!unauthorizedHandler) return
    const now = Date.now()
    if (now - lastUnauthorizedAt < UNAUTHORIZED_WINDOW_MS) return
    lastUnauthorizedAt = now
    try {
        unauthorizedHandler()
    } catch (cause) {
        console.error("[api] 401 处理回调执行失败", cause)
    }
}

/**
 * 取生成客户端抛错里的 Response。生成代码与业务代码可能拿到不同模块实例，
 * instanceof ResponseError 不能单独依赖，补一层按结构的判定。
 */
function errorResponse(err: unknown): Response | null {
    if (err instanceof ResponseError) return err.response
    if (err && typeof err === "object" && "response" in err) {
        const value = (err as { response?: unknown }).response
        if (value instanceof Response) return value
    }
    return null
}

/** 代理 / 网关返回的技术文本，不适合直接展示给用户。 */
const INFRA_NOISE = /upstream|bad gateway|gateway timeout|econnrefused|<html|<!doctype|nginx|response returned an error code/i

/** 生成客户端自带的英文内部文案，同样不能透出到界面。 */
const GENERATOR_NOISE = /^(Response returned an error code|The request failed and the interceptors did not return an alternative response|Required parameter|fetch failed|Failed to fetch|NetworkError)/i

/** 响应体没有可用业务文案时，按状态码给出可操作的中文提示。 */
function statusMessage(status: number): string | null {
    switch (status) {
        case 400: return "请求参数有误（HTTP 400），请检查后重试。"
        case 401: return "登录已失效，请重新登录。"
        case 403: return "没有权限执行此操作（HTTP 403）。"
        case 404: return "请求的接口不存在（HTTP 404），请确认后端服务版本。"
        case 409: return "数据冲突（HTTP 409），请刷新后重试。"
        case 500: return "服务处理失败（HTTP 500），请稍后重试或查看服务日志。"
        case 502:
        case 503:
        case 504: return `后端服务暂时不可用（HTTP ${status}），请确认服务已启动后重试。`
        default: return status >= 500 ? `服务异常（HTTP ${status}），请稍后重试。` : null
    }
}

/**
 * 从生成客户端抛出的错误中提取可展示的中文提示。
 * 优先透出后端的 { message } / Problem Details { detail | title }，其次是后端自己的纯文本；
 * 代理错误页、生成器英文文案一律不外泄，改按状态码或调用方兜底文案展示。
 * 识别到 401 时先触发统一回调（清登录态、跳登录页），再按原样返回文案，调用方行为不变。
 */
export async function apiErrorMessage(err: unknown, fallback: string): Promise<string> {
    const response = errorResponse(err)
    if (response) {
        if (response.status === 401) notifyUnauthorized()
        try {
            const data = await response.clone().json()
            if (data && typeof data === "object") {
                const { message, detail, title } = data as { message?: unknown; detail?: unknown; title?: unknown }
                // 业务错误约定为 { message }；未捕获异常走 Problem Details，透出 detail 首段即可。
                const text = typeof message === "string" && message ? message
                    : typeof detail === "string" && detail ? detail
                        : typeof title === "string" && title ? title : ""
                if (text) return text.slice(0, 200)
            }
        } catch {
            // 响应体不是 JSON，继续尝试读取纯文本。
        }
        try {
            const text = (await response.clone().text()).trim()
            if (text && !INFRA_NOISE.test(text)) return text.slice(0, 200)
        } catch {
            // 读取失败则按状态码给提示。
        }
        return statusMessage(response.status) ?? fallback
    }
    // fetch 本身失败（服务未启动 / 断网）：给明确的中文提示，而不是 "Failed to fetch"。
    if (err instanceof TypeError) return "无法连接后端服务，请确认服务已启动且网络正常。"
    if (err instanceof Error && err.message && !GENERATOR_NOISE.test(err.message)) return err.message
    return fallback
}

let fetchGuardInstalled = false

/**
 * 兜住不走 apiErrorMessage 的取数：报表/日记账导出、.xls 导入、下载等场景用的是裸 fetch，
 * 令牌过期时同样要集中登出。只读响应状态、不碰响应体，因此不影响调用方自己的处理。
 * 与 apiErrorMessage 共用同一份去抖窗口，并发 401 仍只触发一次跳转。
 */
export function installUnauthorizedFetchGuard(): void {
    if (fetchGuardInstalled || typeof globalThis.fetch !== "function") return
    fetchGuardInstalled = true
    const originalFetch = globalThis.fetch
    globalThis.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
        const response = await originalFetch(input, init)
        if (response.status === 401) notifyUnauthorized()
        return response
    }
}
