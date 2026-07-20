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

/**
 * 从生成客户端抛出的错误中提取可展示的提示文案。
 * 后端业务错误统一返回 { message }，这里优先解析该字段，其次回退到响应文本与通用兜底。
 */
export async function apiErrorMessage(err: unknown, fallback: string): Promise<string> {
    if (err instanceof ResponseError) {
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
