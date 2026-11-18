/**
 * 全站统一的「筛选 / 搜索展开」触发按钮。
 *
 * 视觉基线 = antd 主按钮（type="primary"）：实底、hover、高度、内边距、圆角、字号
 * 一律由 ConfigProvider 的 colorPrimary / borderRadius / compact token 决定，调用方不得再覆盖。
 * 组件内只用 em 相对单位排「期间摘要 │ 漏斗 (条件数)」，竖线与徽标颜色同样取自 token，
 * 因此明暗主题 / 换主色时，触发按钮与筛选面板里的「确定」始终是同一颗主色。
 */
import { FilterFilled } from "@ant-design/icons"
import { Button, theme } from "antd"
import type { ComponentPropsWithRef, ReactNode } from "react"

type AntdButtonProps = ComponentPropsWithRef<typeof Button>

export interface FilterTriggerButtonProps extends Omit<AntdButtonProps, "type" | "children"> {
    /** 按钮上的期间 / 条件摘要，如「2026-11-01 至 2026-11-30」。 */
    label: ReactNode
    /** 已生效的筛选条件数；大于 0 时在漏斗后显示徽标。 */
    count?: number
}

export function FilterTriggerButton({ label, count = 0, title = "高级筛选", ...rest }: FilterTriggerButtonProps) {
    const { token } = theme.useToken()

    // 只给 Button 一个 child：避免 antd 自身的图标间距介入，内部排布统一由这里控制。
    return (
        <Button type="primary" title={title} {...rest}>
            <span className="inline-flex items-center gap-[0.4em]">
                <span className="tabular-nums">{label}</span>
                <span aria-hidden className="h-[1em] w-px shrink-0 bg-current opacity-30" />
                <FilterFilled />
                {count > 0 ? (
                    <span
                        className="px-[0.35em] text-[0.8em] font-semibold leading-[1.5]"
                        style={{
                            background: token.colorTextLightSolid,
                            color: token.colorPrimary,
                            borderRadius: token.borderRadius,
                        }}
                    >
                        {count}
                    </span>
                ) : null}
            </span>
        </Button>
    )
}
