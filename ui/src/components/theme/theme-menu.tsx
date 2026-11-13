import {
    BgColorsOutlined,
    CheckOutlined,
    CompressOutlined,
    MoonOutlined,
    SunOutlined,
    SyncOutlined,
} from "@ant-design/icons"
import { Button, ColorPicker, Dropdown, Segmented, Switch, Tooltip } from "antd"

import { DEFAULT_PRIMARY, RADIUS_STEPS, useTheme, type Radius, type Theme } from "./theme-provider"

/** 预设主题色：默认云财蓝，其余按色相均匀排开，供不同业务外壳挑选。 */
const PRIMARY_PRESETS: { color: string; label: string }[] = [
    { color: DEFAULT_PRIMARY, label: "云财蓝" },
    { color: "#1677ff", label: "天蓝" },
    { color: "#0fa3a3", label: "青碧" },
    { color: "#2e9e5b", label: "松绿" },
    { color: "#6d5bd0", label: "紫檀" },
    { color: "#e8801a", label: "橘橙" },
    { color: "#d6407a", label: "玫红" },
    { color: "#3b4148", label: "石墨" },
]

/** 圆角档位名：0 即全站直角，其余为 antd borderRadius 与 rounded-* 的基准值（px）。 */
const RADIUS_LABELS: Record<Radius, string> = { 0: "直角", 4: "小", 8: "中", 12: "大" }

/**
 * 顶栏右上角的主题设置：明暗三选一 + 紧凑密度 + 圆角档位 + 主题色。
 * 四项都存 localStorage，改动即时生效，无需刷新。
 */
export function ThemeMenu() {
    return (
        <Dropdown trigger={["click"]} placement="bottomRight" popupRender={() => <ThemePanel />}>
            <Tooltip title="主题设置" placement="bottom">
                <Button type="text" icon={<BgColorsOutlined />} aria-label="主题设置" />
            </Tooltip>
        </Dropdown>
    )
}

function ThemePanel() {
    const { theme, setTheme, density, setDensity, radius, setRadius, primary, setPrimary } = useTheme()

    return (
        <div className="w-[300px] rounded-xl border border-border bg-popover p-4 shadow-lg">
            <Segmented
                block
                value={theme}
                onChange={(value) => setTheme(value as Theme)}
                options={[
                    { value: "system", label: "系统", icon: <SyncOutlined /> },
                    { value: "light", label: "浅色", icon: <SunOutlined /> },
                    { value: "dark", label: "暗黑", icon: <MoonOutlined /> },
                ]}
            />

            <div className="mt-4 flex items-center justify-between">
                <span className="flex items-center gap-2 text-sm text-muted-foreground">
                    <CompressOutlined />
                    紧凑主题
                </span>
                <Switch
                    checked={density === "compact"}
                    onChange={(checked) => setDensity(checked ? "compact" : "default")}
                />
            </div>

            <SectionLabel>圆角</SectionLabel>
            <div className="flex gap-1.5">
                {RADIUS_STEPS.map((step) => (
                    <RadiusChip key={step} step={step} active={radius === step} onClick={() => setRadius(step)} />
                ))}
            </div>

            <SectionLabel>主题色</SectionLabel>
            <div className="flex flex-wrap items-center gap-1.5">
                {PRIMARY_PRESETS.map((preset) => (
                    <ColorSwatch
                        key={preset.color}
                        color={preset.color}
                        label={preset.label}
                        active={primary.toLowerCase() === preset.color.toLowerCase()}
                        onClick={() => setPrimary(preset.color)}
                    />
                ))}
                <ColorPicker
                    value={primary}
                    format="hex"
                    disabledAlpha
                    onChangeComplete={(color) => setPrimary(color.toHexString())}
                >
                    <Tooltip title="自定义颜色">
                        <span className="flex size-5 cursor-pointer items-center justify-center rounded-full border border-(--border) text-(--muted-foreground)">
                            <BgColorsOutlined style={{ fontSize: 12 }} />
                        </span>
                    </Tooltip>
                </ColorPicker>
            </div>
        </div>
    )
}

function SectionLabel({ children }: { children: string }) {
    return <div className="mb-1.5 mt-4 text-sm text-muted-foreground">{children}</div>
}

/** 圆角预览：用一块小方块按档位画出实际圆角，选择时看得见效果。 */
function RadiusChip({ step, active, onClick }: { step: Radius; active: boolean; onClick: () => void }) {
    return (
        <button
            type="button"
            onClick={onClick}
            title={RADIUS_LABELS[step]}
            className="flex flex-1 flex-col items-center gap-1 rounded border px-1 py-1.5"
            style={active ? { borderColor: "var(--brand)", color: "var(--brand)" } : undefined}
        >
            <span
                className="h-3.5 w-full bg-current opacity-25"
                style={{ borderRadius: `${step}px` }}
                aria-hidden
            />
            <span className="text-[13px] leading-none">{RADIUS_LABELS[step]}</span>
        </button>
    )
}

function ColorSwatch({
    color,
    label,
    active,
    onClick,
}: {
    color: string
    label: string
    active: boolean
    onClick: () => void
}) {
    return (
        <button
            type="button"
            title={label}
            aria-label={label}
            aria-pressed={active}
            onClick={onClick}
            className="flex size-5 items-center justify-center rounded-full text-white"
            style={{ background: color, outline: active ? "2px solid var(--brand)" : "none", outlineOffset: 1 }}
        >
            {active && <CheckOutlined style={{ fontSize: 11 }} />}
        </button>
    )
}
