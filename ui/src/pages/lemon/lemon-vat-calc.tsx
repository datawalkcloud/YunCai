/**
 * 增值税测算（对应柠檬云 /Invoice/TaxCalculation）：
 * 按税款所属期汇总销项税额、进项税额（本期认证）与期初进项留抵税额，算出预计应交增值税，
 * 并给出「销项 vs 进项」对比与全年趋势。金额全部来自已录入的发票（销项 / 进项），实时汇总。
 */
import { useCallback, useEffect, useMemo, useState } from "react";

import { Btn, EmptyTip, Page, PeriodStepper, RefreshIcon, Table, ToolRow, WipBtn } from "./components/ui";
import type { Col, Row } from "./components/ui";
import { InvoicesApi, type InvoiceDto } from "@/apis-gen";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { shiftPeriod, useDefaultAccountingPeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { apiConfig } from "@/lib/api-client";

const money = (v: number): string =>
    v.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** 发票日期（Date | string）→ yyyy-MM。 */
const monthOf = (v: Date | string | null | undefined): string =>
    v ? new Date(v).toISOString().substring(0, 7) : "";

export function LemonVatCalc() {
    const defaultPeriod = useDefaultAccountingPeriod();
    const [userPeriod, setUserPeriod] = useState<AccountingPeriod | null>(null);
    const period = userPeriod ?? defaultPeriod;
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";

    const [sales, setSales] = useState<InvoiceDto[]>([]);
    const [purchases, setPurchases] = useState<InvoiceDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        if (!token || !accountSetId) return;
        setLoading(true);
        setError(null);
        try {
            const api = new InvoicesApi(apiConfig(token));
            const startDate = new Date(`${period.year}-01-01`);
            const endDate = new Date(`${period.year}-12-31`);
            const [s, p] = await Promise.all([
                api.accountsetsAccountSetIdInvoicesGet({ accountSetId, direction: 1, startDate, endDate }),
                api.accountsetsAccountSetIdInvoicesGet({ accountSetId, direction: 2, startDate, endDate }),
            ]);
            setSales(s);
            setPurchases(p);
        } catch (e) {
            setError(e instanceof Error ? e.message : "增值税测算加载失败");
        } finally {
            setLoading(false);
        }
    }, [token, accountSetId, period.year]);

    useEffect(() => {
        void load();
    }, [load]);

    const periodKey = `${period.year}-${String(period.month).padStart(2, "0")}`;
    const inPeriod = (inv: InvoiceDto) => monthOf(inv.issueDate) === periodKey;

    const outputTax = useMemo(
        () => sales.filter(inPeriod).reduce((sum, inv) => sum + (inv.totalTax ?? 0), 0),
        // eslint-disable-next-line react-hooks/exhaustive-deps
        [sales, periodKey],
    );
    const inputTax = useMemo(
        () => purchases.filter(inPeriod).reduce((sum, inv) => sum + (inv.totalTax ?? 0), 0),
        // eslint-disable-next-line react-hooks/exhaustive-deps
        [purchases, periodKey],
    );
    /** 期初进项留抵税额：当前数据模型没有留抵台账，固定 0。 */
    const carryOver = 0;
    const payable = outputTax - inputTax - carryOver;

    const rows: Row[] = [
        { item: "销项税额", amount: money(outputTax) },
        { item: "进项税额（本期认证）", amount: money(inputTax) },
        { item: "期初进项留抵税额", amount: money(carryOver) },
    ];

    /** 全年应交增值税趋势（按开票月份汇总销项 - 进项）。 */
    const trend = useMemo(() => {
        const months = Array.from({ length: 12 }, (_, i) => `${period.year}-${String(i + 1).padStart(2, "0")}`);
        return months.map((m) => {
            const out = sales.filter((inv) => monthOf(inv.issueDate) === m).reduce((s, inv) => s + (inv.totalTax ?? 0), 0);
            const inn = purchases.filter((inv) => monthOf(inv.issueDate) === m).reduce((s, inv) => s + (inv.totalTax ?? 0), 0);
            return { month: m, value: out - inn };
        });
    }, [sales, purchases, period.year]);
    const trendMax = Math.max(1, ...trend.map((t) => Math.abs(t.value)));

    return (
        <Page>
            <ToolRow
                left={
                    <>
                        <span className="text-[13px] text-(--muted-foreground)">税款所属期：</span>
                        <PeriodStepper
                            text={`${period.year}年${period.month}月`}
                            onPrev={() => setUserPeriod(shiftPeriod(period, -1))}
                            onNext={() => setUserPeriod(shiftPeriod(period, 1))}
                        />
                        <Btn variant="primary" title="按当前税款所属期重新测算" onClick={() => void load()}>
                            查询
                        </Btn>
                    </>
                }
                right={
                    <>
                        <WipBtn title="同步认证状态需对接电子税务局，暂未实现">同步认证状态</WipBtn>
                        <RefreshIcon onClick={() => void load()} />
                    </>
                }
            />
            <div className="flex-1 px-4 pb-4">
                {error && <div className="py-2 text-[13px] text-(--danger)">{error}</div>}
                {loading && <div className="px-1 py-2 text-[13px] text-(--muted-foreground)">加载中…</div>}
                <Table
                    cols={[
                        { key: "item", title: "项目" },
                        { key: "amount", title: "金额", align: "right" },
                    ] as Col[]}
                    rows={rows}
                    empty={rows.length === 0 ? <EmptyTip /> : undefined}
                />

                <div className="mt-5 grid grid-cols-1 gap-5 lg:grid-cols-2">
                    {/* 销项 vs 进项 对比 */}
                    <div className="rounded border border-(--border) bg-(--card) p-5">
                        <div className="mb-3 text-[13px] text-(--muted-foreground)">本期销项与进项对比</div>
                        <div className="flex items-end justify-center gap-10">
                            <div className="flex flex-col items-center">
                                <div
                                    className="w-16 bg-(--warn-icon)"
                                    style={{ height: `${Math.max(8, (outputTax / Math.max(1, Math.max(outputTax, inputTax))) * 120)}px` }}
                                />
                                <div className="mt-2 text-[13px] text-(--foreground)">销项税</div>
                                <div className="text-[13px] font-medium tabular-nums">{money(outputTax)}</div>
                                <div className="text-[12px] text-(--muted-foreground)">（本期销项）</div>
                            </div>
                            <div className="flex flex-col items-center">
                                <div
                                    className="w-16 bg-(--brand-fill)"
                                    style={{ height: `${Math.max(8, (inputTax / Math.max(1, Math.max(outputTax, inputTax))) * 120)}px` }}
                                />
                                <div className="mt-2 text-[13px] text-(--foreground)">进项税</div>
                                <div className="text-[13px] font-medium tabular-nums">{money(inputTax)}</div>
                                <div className="text-[12px] text-(--muted-foreground)">（本期进项+期初留抵）</div>
                            </div>
                        </div>
                    </div>

                    {/* 公式说明 */}
                    <div className="rounded border border-(--border) bg-(--card) p-5 text-[13px] leading-7 text-(--foreground)">
                        <div className="mb-2 font-medium text-(--foreground)">增值税测算公式：</div>
                        <div className="tabular-nums">
                            预计应交增值税 = 销项税额 - 进项税额（本期认证） - 期初进项留抵税额
                        </div>
                        <div className="tabular-nums text-(--muted-foreground)">
                            = {money(outputTax)} - {money(inputTax)} - {money(carryOver)}
                        </div>
                        <div className="tabular-nums font-medium text-(--foreground)">= {money(payable)}</div>
                        <div className="mt-2 text-[12.5px] text-(--muted-foreground)">
                            {payable <= 0
                                ? "*本期抵扣的进项税等于或大于销项税，无需缴纳增值税。"
                                : "*测算结果仅供参考，实际申报以税务机关核定为准。"}
                        </div>
                    </div>
                </div>

                {/* 趋势图 */}
                <div className="mt-5 rounded border border-(--border) bg-(--card) p-5">
                    <div className="mb-4 text-[13px] font-medium text-(--foreground)">
                        应交增值税趋势图（{period.year}年）
                    </div>
                    <div className="flex h-40 items-end gap-2">
                        {trend.map((t) => (
                            <div key={t.month} className="flex flex-1 flex-col items-center justify-end gap-1">
                                <div
                                    className={t.value >= 0 ? "w-full bg-(--brand-fill)" : "w-full bg-(--success)"}
                                    style={{ height: `${Math.max(2, (Math.abs(t.value) / trendMax) * 120)}px` }}
                                    title={`${t.month}：${money(t.value)}`}
                                />
                                <span className="text-[11px] text-(--muted-foreground)">{Number(t.month.slice(5))}月</span>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </Page>
    );
}
