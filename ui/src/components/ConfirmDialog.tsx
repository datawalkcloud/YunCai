import { useState } from "react"
import { ExclamationCircleFilled } from "@ant-design/icons"
import { Input, Modal } from "antd"

export interface ConfirmDialogProps {
    open: boolean
    /** 整句确认文案，即原 window.confirm 的提示。 */
    message: string
    title?: string
    confirmText?: string
    cancelText?: string
    /** 高危操作要求用户原样输入的文本（如账套名称）；不传则无需输入。 */
    requireText?: string
    onConfirm: () => void
    onCancel: () => void
}

/**
 * window.confirm 的替身：待确认操作一律不可逆，故确认按钮取危险色。
 * 用 hooks/useConfirm 驱动，页面只需在末尾渲染一次 confirmDialog。
 */
export function ConfirmDialog({
    open,
    message,
    title = "操作确认",
    confirmText = "确定",
    cancelText = "取消",
    requireText,
    onConfirm,
    onCancel,
}: ConfirmDialogProps) {
    const [typed, setTyped] = useState("")
    const matched = !requireText || typed.trim() === requireText

    return (
        <Modal
            open={open}
            width={400}
            title={
                <span className="flex items-center gap-2">
                    <ExclamationCircleFilled style={{ color: "var(--tone-red)" }} />
                    {title}
                </span>
            }
            okText={confirmText}
            cancelText={cancelText}
            okButtonProps={{ danger: true, disabled: !matched }}
            onOk={onConfirm}
            onCancel={onCancel}
        >
            <div className="flex flex-col gap-3">
                <span>{message}</span>
                {requireText && (
                    <Input
                        autoFocus
                        value={typed}
                        onChange={(e) => setTyped(e.target.value)}
                        onPressEnter={() => {
                            if (matched) onConfirm()
                        }}
                        placeholder={`请输入「${requireText}」`}
                        aria-label="确认文本"
                    />
                )}
            </div>
        </Modal>
    )
}
