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
 * 从生成客户端抛出的错误中提取可展示的提示文案。
 * 后端业务错误统一返回 { message }，这里优先解析该字段，其次回退到响应文本与通用兜底。
 * 识别到 401 时先触发统一回调（清登录态、跳登录页），再按原样返回文案，调用方行为不变。
 */
export async function apiErrorMessage(err: unknown, fallback: string): Promise<string> {
    if (err instanceof ResponseError) {
        if (err.response.status === 401) notifyUnauthorized()
        try {
            const data = await err.response.clone().json()
            if (data && typeof data === "object" && "message" in data) {
                const message = (data as { message?: unknown }).message
                if (typeof message === "string" && message) return message
            }
        } catch {
            // 响应体不是 JSON，继续尝试读取纯文本。
        }
        try {
            const text = await err.response.clone().text()
            if (text) return text
        } catch {
            // 读取失败则回退到兜底文案。
        }
    }
    if (err instanceof Error && err.message) return err.message
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
