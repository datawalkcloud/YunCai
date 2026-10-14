import { TriangleAlert } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "@/components/ui/dialog"

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
 * window.confirm 的声明式替身：待确认操作一律不可逆，故确认按钮取危险色。
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
        <Dialog
            open={open}
            onOpenChange={(next) => {
                if (!next) onCancel()
            }}
        >
            <DialogContent showCloseButton={false} className="sm:max-w-md">
                <DialogHeader>
                    <DialogTitle className="flex items-center gap-2">
                        <TriangleAlert className="size-4 text-[#ef4444]" />
                        {title}
                    </DialogTitle>
                    <DialogDescription>{message}</DialogDescription>
                </DialogHeader>
                <DialogFooter>
                    <Button variant="outline" onClick={onCancel}>
                        {cancelText}
                    </Button>
                    <Button variant="destructive" onClick={onConfirm}>
                        {confirmText}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}
