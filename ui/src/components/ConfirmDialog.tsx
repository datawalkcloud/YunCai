import { ExclamationCircleFilled } from "@ant-design/icons"
import { Modal } from "antd"

export interface ConfirmDialogProps {
    open: boolean
    /** 整句确认文案，即原 window.confirm 的提示。 */
    message: string
    title?: string
    confirmText?: string
    cancelText?: string
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
    onConfirm,
    onCancel,
}: ConfirmDialogProps) {
    return (
        <Modal
            open={open}
            width={400}
            title={
                <span className="flex items-center gap-2">
                    <ExclamationCircleFilled style={{ color: "#ef4444" }} />
                    {title}
                </span>
            }
            okText={confirmText}
            cancelText={cancelText}
            okButtonProps={{ danger: true }}
            onOk={onConfirm}
            onCancel={onCancel}
        >
            {message}
        </Modal>
    )
}
