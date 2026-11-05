/**
 * 页面级视觉常量：与首页（pages/dashboard）同一套色板。
 * 页头按钮、行内图标按钮、状态标签在这里统一，避免各页各写一份。
 */

/**
 * 表单栅格列宽。
 *
 * antd 的 token 只覆盖颜色 / 字体 / 字号 / 圆角 / 控件高度，没有宽度 token，
 * 宽度属于布局层，所以集中在这里定义：全站标准表单的控件列统一一个宽度，
 * 不再各页各写一份 w-[220px] / w-[240px] / w-[260px]。
 *
 * 标签列宽度不统一收口：标签文字长度差异大（"统一社会信用代码：" 与 "类别名称："），
 * 固定窄列会导致换行，故各表单按自身文案长度保留，只把最常见的 110px 提为常量。
 */
export const FORM_LABEL_W = "w-[110px]";
export const FORM_CONTROL_W = "w-[240px]";

/** 页头操作按钮：白底直角按钮。 */
export const PAGE_BTN_GHOST =
    "h-9 rounded-xl border-white/80 bg-white/85 px-3.5 text-[13px] text-slate-600 shadow-[0_2px_8px_rgba(16,24,40,0.05)] hover:bg-white hover:text-slate-800"

/** 页头操作按钮：主色实底按钮，用于本页主动作。 */
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

/** 状态标签色调：浅底 + 同色系文字（antd Tag 用内联样式上色，故取色值而非 class）。 */
export const PILL: Record<PillTone, { bg: string; fg: string }> = {
    blue: { bg: "#dbeafe", fg: "#3b82f6" },
    green: { bg: "#d1fae5", fg: "#10b981" },
    purple: { bg: "#ede9fe", fg: "#8b5cf6" },
    amber: { bg: "#fef3c7", fg: "#f59e0b" },
    red: { bg: "#fee2e2", fg: "#ef4444" },
    gray: { bg: "#eef1f6", fg: "#64748b" },
}

/** 凭证状态 → 标签档位：已过账绿、草稿蓝；借贷不平衡一律红。 */
export function voucherStatusTone(status?: number, isBalanced?: boolean): PillTone {
    if (isBalanced === false) return "red"
    switch (status) {
        case 3:
            return "green"
        default:
            return "blue"
    }
}

/** 瓦片与标签共用的强调色，取值与首页统计卡一致。 */
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

/**
 * antd 图标着色：antd 注入的 `.anticon { color: inherit }` 是无层级声明，压过 Tailwind `@layer utilities`
 * 里的颜色工具类，写在图标 className 上的 text-slate-400、text-[#ef4444] 之类全部失效；
 * 颜色一律用这里的内联样式，字号与间距仍走类名。
 */
export const ICON_STYLE = {
    brand: { color: "#2f6bff" },
    ok: { color: "#22c55e" },
    warn: { color: "#f59e0b" },
    danger: { color: "#ef4444" },
    muted: { color: "#94a3b8" },
    placeholder: { color: "#cbd5e1" },
} as const

/**
 * antd 表格金额列公共属性：align 让表头与单元格一起右对齐（rc-table 只把 className 发给单元格），
 * 等宽数字靠 className。账簿、资金、报表各表共用，不再各自判定「哪些列是金额列」。
 */
export const AMOUNT_COL = { align: "right", className: "text-right tabular-nums" } as const
