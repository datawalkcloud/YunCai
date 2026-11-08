/**
 * 页面级视觉常量：色值一律引用 index.css 的语义变量（--tone-* / --brand / --warn-* 等），
 * 这里只做「语义 → 用法」的映射，不出现具体颜色。
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

export type PillTone = "blue" | "green" | "purple" | "amber" | "red" | "gray"

/** 状态标签色调：浅底 + 同色系文字（antd Tag 用内联样式上色，故取色值而非 class）。 */
export const PILL: Record<PillTone, { bg: string; fg: string }> = {
    blue: { bg: "var(--tone-blue-soft)", fg: "var(--tone-blue)" },
    green: { bg: "var(--tone-green-soft)", fg: "var(--tone-green)" },
    purple: { bg: "var(--tone-purple-soft)", fg: "var(--tone-purple)" },
    amber: { bg: "var(--tone-amber-soft)", fg: "var(--tone-amber)" },
    red: { bg: "var(--tone-red-soft)", fg: "var(--tone-red)" },
    gray: { bg: "var(--tone-gray-soft)", fg: "var(--tone-gray)" },
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
    blue: "var(--tone-blue)",
    green: "var(--tone-green)",
    purple: "var(--tone-purple)",
    amber: "var(--tone-amber)",
    red: "var(--tone-red)",
    gray: "var(--tone-gray)",
} as const

/**
 * 瓦片底色：对应 ACCENT 的浅色版。
 * 页面底色为 --background，白卡与它的相对亮度差只有 0.079，瓦片要比这个门槛更明显才不"融"进背景，
 * 故六档统一取 ΔL≈0.12（约为白卡分离度的 1.5 倍），彩度不同但分离度一致。
 */
export const ACCENT_BG = {
    blue: "var(--tone-blue-bg)",
    green: "var(--tone-green-bg)",
    purple: "var(--tone-purple-bg)",
    amber: "var(--tone-amber-bg)",
    red: "var(--tone-red-bg)",
    gray: "var(--tone-gray-bg)",
} as const

/** 白卡内部的浅灰区块（筛选条、汇总条、表头带）：底色 --muted，描边比 --border 深一档才看得见。 */
export const SOFT_PANEL = "rounded-xl border border-(--input) bg-muted"

/**
 * antd 图标着色：antd 注入的 `.anticon { color: inherit }` 是无层级声明，压过 Tailwind `@layer utilities`
 * 里的颜色工具类，写在图标 className 上的 text-slate-400、text-[#ef4444] 之类全部失效；
 * 颜色一律用这里的内联样式，字号与间距仍走类名。
 */
export const ICON_STYLE = {
    brand: { color: "var(--brand)" },
    ok: { color: "var(--tone-green)" },
    warn: { color: "var(--warn-icon)" },
    danger: { color: "var(--tone-red)" },
    muted: { color: "var(--icon-muted)" },
    placeholder: { color: "var(--icon-placeholder)" },
} as const

/**
 * antd 表格金额列公共属性：align 让表头与单元格一起右对齐（rc-table 只把 className 发给单元格），
 * 等宽数字靠 className。账簿、资金、报表各表共用，不再各自判定「哪些列是金额列」。
 */
export const AMOUNT_COL = { align: "right", className: "text-right tabular-nums" } as const
