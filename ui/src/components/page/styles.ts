/**
 * 页面级视觉常量：与首页（pages/dashboard）同一套色板。
 * 页头按钮、行内图标按钮、状态胶囊在这里统一，避免各页各写一份。
 */

/** 页头操作按钮：白底胶囊。 */
export const PAGE_BTN_GHOST =
    "h-9 rounded-xl border-white/80 bg-white/85 px-3.5 text-[13px] text-slate-600 shadow-[0_2px_8px_rgba(16,24,40,0.05)] hover:bg-white hover:text-slate-800"

/** 页头操作按钮：主色实底胶囊，用于本页主动作。 */
export const PAGE_BTN_PRIMARY =
    "h-9 rounded-xl bg-[#2f6bff] px-4 text-[13px] text-white shadow-[0_1px_2px_rgba(47,107,255,0.25)] hover:bg-[#255ce0]"

/** 卡片标题行右侧操作按钮：页头按钮的紧凑版，用于一屏放不下的多按钮场景。 */
export const CARD_BTN =
    "h-8 rounded-lg border-slate-200 bg-white px-3 text-[12.5px] text-slate-600 shadow-none hover:bg-[#f6f8fb] hover:text-slate-800"
export const CARD_BTN_PRIMARY =
    "h-8 rounded-lg bg-[#2f6bff] px-3 text-[12.5px] text-white shadow-[0_1px_2px_rgba(47,107,255,0.22)] hover:bg-[#255ce0]"
export const CARD_BTN_DANGER =
    "h-8 rounded-lg border-slate-200 bg-white px-3 text-[12.5px] text-[#ef4444] shadow-none hover:border-[#fecaca] hover:bg-[#fef2f2]"

/** 行内图标按钮柔和底色，按语义区分主色 / 成功 / 危险。 */
export const ICON_BTN =
    "size-7 rounded-lg text-slate-400 hover:bg-[#eef4ff] hover:text-[#2f6bff] disabled:opacity-40"
export const ICON_BTN_SUCCESS =
    "size-7 rounded-lg text-slate-400 hover:bg-[#d1fae5] hover:text-[#10b981] disabled:opacity-40"
export const ICON_BTN_DANGER =
    "size-7 rounded-lg text-slate-400 hover:bg-[#fee2e2] hover:text-[#ef4444] disabled:opacity-40"

export type PillTone = "blue" | "green" | "purple" | "amber" | "red" | "gray"

/** 状态胶囊色调：浅底 + 同色系文字。 */
export const PILL: Record<PillTone, string> = {
    blue: "bg-[#dbeafe] text-[#3b82f6]",
    green: "bg-[#d1fae5] text-[#10b981]",
    purple: "bg-[#ede9fe] text-[#8b5cf6]",
    amber: "bg-[#fef3c7] text-[#f59e0b]",
    red: "bg-[#fee2e2] text-[#ef4444]",
    gray: "bg-[#eef1f6] text-[#64748b]",
}

/** 表单控件（Input / SelectTrigger）在筛选区里的小号浅灰样式。 */
export const CONTROL = "h-8 rounded-lg border-slate-200 bg-white text-[13px] shadow-none"

/** 选择器触发按钮（PickerTrigger 的第一个按钮）的小号浅灰样式。 */
const TRIGGER_BTN = [
    "[&>button:first-of-type]:h-8 [&>button:first-of-type]:rounded-lg [&>button:first-of-type]:border-slate-200",
    "[&>button:first-of-type]:bg-white [&>button:first-of-type]:px-2.5 [&>button:first-of-type]:text-[12px]",
    "[&>button:first-of-type]:font-normal [&>button:first-of-type]:text-slate-500 [&>button:first-of-type]:shadow-none",
    "[&>button:first-of-type:hover]:bg-slate-50",
].join(" ")

/** 单个日期 / 期间选择器在筛选区里的样式。 */
export const PERIOD_FIELD = TRIGGER_BTN

/** 日期区间选择器：主按钮 + 尾部清除按钮。 */
export const DATE_FIELD = [
    TRIGGER_BTN,
    "[&>button:last-of-type]:text-slate-400 [&>button:last-of-type:hover]:bg-slate-100 [&>button:last-of-type:hover]:text-slate-600",
].join(" ")

/** 凭证状态 → 胶囊档位：已过账绿、已审核紫、草稿蓝；借贷不平衡一律红。 */
export function voucherStatusTone(status?: number, isBalanced?: boolean): PillTone {
    if (isBalanced === false) return "red"
    switch (status) {
        case 3:
            return "green"
        case 2:
            return "purple"
        default:
            return "blue"
    }
}

/** 瓦片与胶囊共用的强调色，取值与首页统计卡一致。 */
export const ACCENT = {
    blue: "#3b82f6",
    green: "#10b981",
    purple: "#8b5cf6",
    amber: "#f59e0b",
    red: "#ef4444",
    gray: "#64748b",
} as const

/**
 * 瓦片底色：对应 ACCENT 的浅色版。
 * 页面底色为 #f4f6fb，白卡与它的相对亮度差只有 0.079，瓦片要比这个门槛更明显才不"融"进背景，
 * 故六档统一取 ΔL≈0.12（约为白卡分离度的 1.5 倍），彩度不同但分离度一致。
 */
export const ACCENT_BG = {
    blue: "#dbe8fd",
    green: "#caefe3",
    purple: "#ebe4fd",
    amber: "#fce4bc",
    red: "#fce1e1",
    gray: "#e5e8eb",
} as const

/** 白卡内部的浅灰区块（筛选条、汇总条、表头带）：底色 --muted，描边需深一档才看得见。 */
export const SOFT_PANEL = "rounded-xl border border-slate-200 bg-muted"
