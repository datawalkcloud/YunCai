import { useCallback, useState, type ReactNode } from "react"

import { ConfirmDialog } from "@/components/ConfirmDialog"

export interface ConfirmOptions {
    title?: string
    confirmText?: string
    cancelText?: string
}

export interface ConfirmHandle {
    /** 替代 window.confirm：弹窗打开时挂起，用户选择后 resolve。 */
    confirm: (message: string, options?: ConfirmOptions) => Promise<boolean>
    /** 待渲染的确认对话框，页面需在自身 JSX 末尾放置一次。 */
    confirmDialog: ReactNode
}

/** 页面级确认框：用法 `if (!(await confirm("确定删除…吗？"))) return`。 */
export function useConfirm(): ConfirmHandle {
    const [pending, setPending] = useState<
        { message: string; options: ConfirmOptions; resolve: (ok: boolean) => void } | null
    >(null)

    const confirm = useCallback(
        (message: string, options: ConfirmOptions = {}) =>
            new Promise<boolean>((resolve) => {
                setPending({ message, options, resolve })
            }),
        [],
    )

    const settle = (ok: boolean) => {
        pending?.resolve(ok)
        setPending(null)
    }

    return {
        confirm,
        confirmDialog: pending ? (
            <ConfirmDialog
                open
                message={pending.message}
                title={pending.options.title}
                confirmText={pending.options.confirmText}
                cancelText={pending.options.cancelText}
                onConfirm={() => settle(true)}
                onCancel={() => settle(false)}
            />
        ) : null,
    }
}
