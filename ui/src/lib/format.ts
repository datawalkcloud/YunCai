/**
 * 全站统一的金额与日期显示口径：账页、报表、资金页与凭证页共用，各页不再自写格式化。
 */

const pad = (n: number) => String(n).padStart(2, "0")

/**
 * 金额格式化：0 显示为空（避免表格被 0.00 填满），其余按千分位保留两位小数；
 * force 为真时 0 也列示，用于合计行。
 */
export function fmtAmount(value: number | null | undefined, force = false): string {
    if ((value ?? 0) === 0 && !force) return ""
    return (value ?? 0).toLocaleString("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })
}

/** 金额 + 人民币符号：资金页统计卡与余额列用，数字与负号口径与 fmtAmount 一致。 */
export function fmtMoney(value: number | null | undefined): string {
    const v = value ?? 0
    return `${v < 0 ? "-" : ""}¥${fmtAmount(Math.abs(v), true)}`
}

/**
 * 日期 → yyyy-MM-dd。
 * 字符串分支直接取 ISO 前缀（后端 DateOnly 序列化就是 yyyy-MM-dd，不经过 Date 解析便没有时区偏移）；
 * Date 分支取本地日历日，不走 toISOString() 的 UTC 口径（那会让东八区的本地今天少一天）。
 */
export function toISODate(value?: Date | string | null): string {
    if (!value) return ""
    if (typeof value === "string") return value.slice(0, 10)
    if (Number.isNaN(value.getTime())) return ""
    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`
}
