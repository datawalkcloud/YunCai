import { useCallback, useEffect, useState } from "react";

import type { PeriodPicker, PeriodStepperProps } from "./ui";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import {
    periodDiff,
    shiftPeriod,
    useDefaultAccountingPeriod,
    type AccountingPeriod,
} from "@/accountSets/useCurrentAccountSet";

/** 步进月数对应的面板粒度与文案：季报落季度首月，年报只显示年份。 */
function granularity(stepMonths: number): { picker: PeriodPicker; text: (p: AccountingPeriod) => string } {
    if (stepMonths >= 12) return { picker: "year", text: (p) => `${p.year}年` };
    if (stepMonths === 3)
        return { picker: "quarter", text: (p) => `${p.year}年第${Math.ceil(p.month / 3)}季度` };
    return { picker: "month", text: (p) => `${p.year}年${p.month}月` };
}

/**
 * 「◀ 期间 ▶」步进器的状态与入参：初始跟随账套当前期间，切换账套自动复位。
 * 两侧箭头按 stepMonths 逐期快进，点中间文字弹出面板跳到任意期间；
 * 页面把 stepper 直接展开给 PeriodStepper，不再各写一份文案与步进逻辑。
 * textOf 用于覆盖展示口径（如工资统计表按年初累计到当期）。
 */
export function usePeriodStepper(stepMonths = 1, textOf?: (p: AccountingPeriod) => string) {
    const accountSetId = useCurrentAccountSetId() ?? "";
    const base = useDefaultAccountingPeriod();
    const [override, setOverride] = useState<AccountingPeriod | null>(null);
    useEffect(() => setOverride(null), [accountSetId]);
    const period = override ?? base;
    const { picker, text } = granularity(stepMonths);
    const shift = useCallback(
        (dir: number) => setOverride(shiftPeriod(period, dir * stepMonths)),
        [period, stepMonths],
    );
    const stepper: PeriodStepperProps = {
        text: (textOf ?? text)(period),
        picker,
        value: period,
        onPrev: () => shift(-1),
        onNext: () => shift(1),
        onPick: setOverride,
    };
    return { period, setPeriod: setOverride, stepper };
}

/** 面板选中期间 → 相对当前期间的步进月数，供只持有 onStep 的子组件复用。 */
export function pickToStep(current: AccountingPeriod, onStep: (delta: number) => void) {
    return (picked: AccountingPeriod) => onStep(periodDiff(current, picked));
}
