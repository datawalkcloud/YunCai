/**
 * 智能报税各页面共用的税期与金额格式化。
 */

export const pad = (value: number) => String(value).padStart(2, "0");

/** 所属期标识 yyyyMM。 */
export function periodKey(year: number, month: number): string {
    return `${year}${pad(month)}`;
}

/** 申报截止日按次月 15 日推算，遇周末顺延到下一个工作日；法定节假日调整以税局公告为准。 */
export function filingDeadline(year: number, month: number): Date {
    const deadline = new Date(year, month, 15);
    while (deadline.getDay() === 0 || deadline.getDay() === 6) {
        deadline.setDate(deadline.getDate() + 1);
    }
    return deadline;
}

export function formatDate(date: Date): string {
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** 由所属期止（yyyy-MM-dd 开头）推算申报期限；解析不出来时留空。 */
export function deadlineFor(periodEnd: string): string {
    const match = /^(\d{4})-(\d{2})/.exec(periodEnd);
    if (!match) return "-";
    return formatDate(filingDeadline(Number(match[1]), Number(match[2])));
}

/** 金额千分位、固定两位小数。 */
export function formatAmount(value: number): string {
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** 上一个完整季度的季末月：按季申报的所属期标识取季末月，与税局「按年月查申报记录」同口径。 */
function previousQuarter(now: Date): { year: number; month: number } {
    const quarter = Math.floor(now.getMonth() / 3);
    return quarter === 0
        ? { year: now.getFullYear() - 1, month: 12 }
        : { year: now.getFullYear(), month: quarter * 3 };
}

/**
 * 按税局的申报期限推算「当前应申报的所属期」，返回年 + 所属期标识月：
 * 按月申报取上月；按季申报取上一完整季度的季末月（税局按年月查申报记录就是这个口径）；
 * 按年申报取上年度 12 月；按次申报没有固定所属期，返回 null。
 */
export function filingPeriod(
    frequency: string,
    now: Date = new Date(),
): { year: number; month: number } | null {
    const year = now.getFullYear();
    const month = now.getMonth() + 1;
    if (frequency === "月") return month === 1 ? { year: year - 1, month: 12 } : { year, month: month - 1 };
    if (frequency === "季") return previousQuarter(now);
    if (frequency === "年") return { year: year - 1, month: 12 };
    return null;
}

/**
 * 报税工作台的默认所属期：按税费种认定里主税种（优先增值税）的申报期限推算——
 * 按季申报的户取上月是取不到申报记录的，税局根本没有那个月的表。
 * 认定还没从税局取回时按上一季度兜底：本系统接的多是小规模季报户，比按自然月猜更接近税局推送的表。
 */
export function defaultFilingPeriod(
    kinds: ReadonlyArray<{ project: string; period: string }>,
    now: Date = new Date(),
): { year: number; month: number } {
    const main =
        kinds.find((kind) => kind.project === "增值税" && filingPeriod(kind.period, now)) ??
        kinds.find((kind) => filingPeriod(kind.period, now));
    return (main ? filingPeriod(main.period, now) : null) ?? previousQuarter(now);
}

/** 占位符：税局侧尚未取回的数字用「--」，与原厂页面口径一致。 */
export const EMPTY_CELL = "--";
