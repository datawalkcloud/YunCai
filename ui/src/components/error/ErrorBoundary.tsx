import { Component, type ErrorInfo, type ReactNode } from "react"
import { WarningFilled } from "@ant-design/icons"
import { Button } from "antd"
import { isRouteErrorResponse, useRouteError } from "react-router"

import { cn } from "@/lib/utils"

/**
 * 统一的错误兜底界面：中文说明 + 「重试 / 返回首页」两个动作。
 * 异常详情只写进控制台，绝不把堆栈渲染给用户；跳转用原生 <a>，
 * 因为最外层边界可能渲染在 Router 上下文之外，整页刷新也能拿到干净状态。
 * fullPage=false 用于账套外壳内容区：撑满内容区而不是整屏，否则兜底页自己会多出滚动条。
 */
function ErrorFallback({ description, fullPage = true }: { description: string; fullPage?: boolean }) {
    return (
        <div
            className={cn(
                "flex flex-col items-center justify-center gap-2 p-6 text-center",
                fullPage ? "min-h-svh" : "min-h-full",
            )}
        >
            <WarningFilled className="mb-1 text-[32px] text-(--danger)" />
            <h2 className="text-base font-medium text-(--foreground)">页面出错了</h2>
            <p className="max-w-md text-sm text-(--muted-foreground)">{description}</p>
            <p className="text-xs text-(--muted-foreground)">详细信息已输出到浏览器控制台，可反馈给技术支持。</p>
            <div className="mt-3 flex items-center gap-2">
                <Button onClick={() => window.location.reload()}>重试</Button>
                <Button href="/app">返回首页</Button>
            </div>
        </div>
    )
}

interface ErrorBoundaryProps {
    children: ReactNode
}

interface ErrorBoundaryState {
    failed: boolean
}

/**
 * React 错误边界：兜住渲染期异常，避免整页白屏。
 * 用在两处——RouterProvider 外层（全站最后一道防线）与受保护路由区外层。
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
    state: ErrorBoundaryState = { failed: false }

    static getDerivedStateFromError(): ErrorBoundaryState {
        return { failed: true }
    }

    componentDidCatch(error: Error, info: ErrorInfo) {
        console.error("[ErrorBoundary] 渲染异常：", error, info.componentStack)
    }

    render() {
        if (this.state.failed) {
            return <ErrorFallback description="页面渲染时发生异常，请重试或返回首页。" />
        }
        return this.props.children
    }
}

/**
 * route 级错误兜底的文案归一：区分「路由报错（带状态码）」「chunk 拉取失败」「其它异常」。
 */
function describeRouteError(error: unknown) {
    if (isRouteErrorResponse(error)) {
        return `页面加载失败（${error.status}），请重试或返回首页。`
    }
    if (
        error instanceof Error &&
        /dynamically imported module|Importing a module script failed|Failed to fetch/i.test(error.message)
    ) {
        return "页面资源加载失败，可能是系统刚更新过，请重试。"
    }
    return "页面加载失败，请重试或返回首页。"
}

/**
 * route 级错误兜底：作为各路由的 errorElement，捕获该路由子树的渲染异常与
 * 懒加载 chunk 拉取失败（常见于站点刚发版、用户手里还是旧 index.html）。
 */
export function RouteErrorElement() {
    const error = useRouteError()
    console.error("[router] 路由异常：", error)

    return <ErrorFallback description={describeRouteError(error)} />
}

/** 内容区级错误兜底：账套外壳下的叶子路由出错时只替换内容区，撑满内容区高度。 */
export function ContentErrorElement() {
    const error = useRouteError()
    console.error("[router] 路由异常：", error)

    return <ErrorFallback fullPage={false} description={describeRouteError(error)} />
}
