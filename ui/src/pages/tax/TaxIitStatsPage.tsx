/**
 * 智能报税 · 个税申报 · 查询统计。
 *
 * 两个页签都按电子税务局的口径展示：
 * 「申报记录查询」走个税（自然人电子税务局）通道，一次把所属期区间的申报记录取回来，未登录或查不到数据就弹个税登录框；
 * 「缴税记录查询」同样走个税扣缴端，按缴税日期或税款所属期过滤。注意它是个税扣缴端按人按次的缴款流水，
 * 与「税费缴纳 › 缴款信息查询」（企业端数字账户 DescribeJkxxcx）不是同一份数据，不能互相替代。
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { Card, App, Button, DatePicker, Select, Table, Tooltip } from "antd";
import type { TableColumnsType } from "antd";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate } from "react-router";

import { useTaxPeriod } from "./tax-period";
import { useAuth } from "@/auth/AuthContext";
import { downloadCsv } from "@/lib/tax-csv";
import { EMPTY_CELL, pad } from "@/lib/tax-format";
import {
    fetchIitFilings,
    fetchIitPayments,
    type TaxIitFilingRow,
    type TaxIitPaymentDateField,
    type TaxIitPaymentRow,
} from "@/lib/tax-iit";

import { IitLoginModal } from "./IitLoginModal";
import { setPath } from "@/accountSets/url-id";

type TabKey = "declarations" | "payments";

/** 取数进度：idle 还没查过，页面进来会自动查一次。 */
type FetchState = "idle" | "loading" | "loaded" | "failed";

const isBlank = (value: string) => !value || value === EMPTY_CELL || value === "-";

const TEXT_DASH = <span className="text-(--muted-foreground)">{EMPTY_CELL}</span>;

const textCell = (value: string) => (isBlank(value) ? TEXT_DASH : value);

const numberCell = (value: string) =>
    isBlank(value) ? TEXT_DASH : <span className="tabular-nums">{value}</span>;

export function TaxIitStatsPage() {
    const period = useTaxPeriod();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const navigate = useNavigate();
    const { token } = useAuth();
    const { message } = App.useApp();

    const [tab, setTab] = useState<TabKey>("declarations");
    // 默认取近 12 个月（含当月）：申报记录按实际申报时间落在自然月里，不跟账套期间走。
    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => {
        const end = dayjs().startOf("month");
        return [end.subtract(11, "month"), end];
    });
    const [rows, setRows] = useState<TaxIitFilingRow[]>([]);
    const [state, setState] = useState<FetchState>("idle");
    const [error, setError] = useState<string | null>(null);
    const [needLogin, setNeedLogin] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    const [payDateField, setPayDateField] = useState<TaxIitPaymentDateField>("paidOn");
    const [payRange, setPayRange] = useState<[Dayjs, Dayjs]>([
        dayjs(`${period.year}-01-01`),
        dayjs(`${period.year}-${pad(period.month)}-01`).endOf("month"),
    ]);
    const [payRows, setPayRows] = useState<TaxIitPaymentRow[]>([]);
    const [payState, setPayState] = useState<FetchState>("idle");
    const [payError, setPayError] = useState<string | null>(null);
    const [payNeedLogin, setPayNeedLogin] = useState(false);

    const loading = state === "loading";
    const payLoading = payState === "loading";
    /** 开发模式的 StrictMode 会把挂载 effect 跑两遍，这里挡住第二次，免得对税局重复发一次取数。 */
    const inFlight = useRef(false);
    const payInFlight = useRef(false);

    const toTaxLogin = () => navigate(setPath(accountSetId, "/tax/login"));

    /**
     * 按所属期区间向个税系统取一次申报记录。
     * 未登录，或登录后一条都没取回，都当作「还没登录」处理：直接把登录框弹出来，省得用户自己找入口。
     */
    const queryFilings = useCallback(async () => {
        if (inFlight.current) return;
        if (range[1].isBefore(range[0])) {
            message.warning("税款所属期区间不正确，请重新选择。");
            return;
        }
        inFlight.current = true;
        setState("loading");
        setError(null);
        try {
            const result = await fetchIitFilings(
                accountSetId,
                token,
                range[0].format("YYYY-MM"),
                range[1].format("YYYY-MM"),
            );
            setNeedLogin(Boolean(result.needLogin));
            if (result.needLogin) {
                // 只有登录态真的没了才弹登录框：刚登录成功又被要求重登，比查不到数据更让人困惑。
                setState("failed");
                setError(result.message ?? "个税登录状态已失效，请重新登录后再查询申报记录。");
                setRows([]);
                setLoginOpen(true);
                return;
            }
            if (result.rows.length === 0) {
                setState("failed");
                setError("税局没有回这个区间的申报记录。若确实申报过，请到「任务中心」看这次取数的解决建议。");
                setRows([]);
                return;
            }
            setRows(result.rows);
            setState("loaded");
        } catch (err) {
            const text = err instanceof Error ? err.message : "读取个税申报记录失败，请稍后重试。";
            setState("failed");
            setError(text);
            setNeedLogin(text.includes("尚未登录"));
        } finally {
            inFlight.current = false;
        }
    }, [accountSetId, message, range, token]);

    // 进页面先自动查一次；失败后切页签回来会重置成 idle，于是再查一次、再弹一次登录框。
    useEffect(() => {
        if (tab === "declarations" && state === "idle") void queryFilings();
    }, [queryFilings, state, tab]);

    /** 按缴税日期（或税款所属期）向个税扣缴端取一段缴税记录；后端会按同一区间再过滤一次。 */
    const queryPayments = useCallback(async () => {
        if (payInFlight.current) return;
        if (payRange[1].isBefore(payRange[0])) {
            message.warning("查询区间不正确，请重新选择。");
            return;
        }
        payInFlight.current = true;
        setPayState("loading");
        setPayError(null);
        try {
            const byPeriod = payDateField === "period";
            const shape = byPeriod ? "YYYY-MM" : "YYYY-MM-DD";
            const result = await fetchIitPayments(
                accountSetId,
                token,
                payDateField,
                payRange[0].format(shape),
                payRange[1].format(shape),
            );
            setPayNeedLogin(Boolean(result.needLogin));
            if (result.needLogin) {
                setPayState("failed");
                setPayError(result.message ?? "个税登录状态已失效，请重新登录后再查询缴税记录。");
                setPayRows([]);
                setLoginOpen(true);
                return;
            }
            setPayRows(result.rows);
            if (result.rows.length === 0) {
                setPayState("failed");
                setPayError(result.message ?? "税局没有回这个区间的缴税记录。若确实缴过，请到「任务中心」看这次取数的解决建议。");
                return;
            }
            setPayState("loaded");
        } catch (err) {
            const text = err instanceof Error ? err.message : "读取个税缴税记录失败，请稍后重试。";
            setPayState("failed");
            setPayError(text);
            setPayNeedLogin(text.includes("尚未登录"));
        } finally {
            payInFlight.current = false;
        }
    }, [accountSetId, message, payDateField, payRange, token]);

    function exportDeclarations() {
        downloadCsv(`个税申报记录_${range[0].format("YYYYMM")}-${range[1].format("YYYYMM")}.csv`, [
            ["税款所属期", "申报表", "申报人次", "当期收入", "应补（退）税额", "申报类型", "申报渠道", "申报时间"],
            ...rows.map((row) => [
                row.period || EMPTY_CELL,
                row.formName || EMPTY_CELL,
                row.headcount || EMPTY_CELL,
                row.income || EMPTY_CELL,
                row.taxDue || EMPTY_CELL,
                row.declareType || EMPTY_CELL,
                row.channel || EMPTY_CELL,
                row.declareTime || EMPTY_CELL,
            ]),
        ]);
        message.success(`已导出 ${rows.length} 条申报记录。`);
    }

    const declarationColumns: TableColumnsType<TaxIitFilingRow> = [
        { title: "税款所属期", dataIndex: "period", key: "period", width: 180, render: textCell },
        { title: "申报表", dataIndex: "formName", key: "formName", width: 320, render: textCell },
        { title: "申报人次", dataIndex: "headcount", key: "headcount", width: 140, align: "right", render: numberCell },
        { title: "当期收入", dataIndex: "income", key: "income", width: 180, align: "right", render: numberCell },
        { title: "应补（退）税额", dataIndex: "taxDue", key: "taxDue", width: 180, align: "right", render: numberCell },
        { title: "申报类型", dataIndex: "declareType", key: "declareType", width: 180, render: textCell },
        { title: "申报渠道", dataIndex: "channel", key: "channel", width: 220, render: textCell },
        { title: "申报时间", dataIndex: "declareTime", key: "declareTime", width: 200, render: textCell },
    ];

    function exportPayments() {
        const byPeriod = payDateField === "period";
        const shape = byPeriod ? "YYYYMM" : "YYYYMMDD";
        downloadCsv(`个税缴税记录_${payRange[0].format(shape)}-${payRange[1].format(shape)}.csv`, [
            ["税款所属期", "报表类型", "电子税票号", "缴税金额", "缴税日期", "缴税种类", "缴税方式"],
            ...payRows.map((row) => [
                row.period || EMPTY_CELL,
                row.formName || EMPTY_CELL,
                row.eTicketNo || EMPTY_CELL,
                row.amount || EMPTY_CELL,
                row.paidOn || EMPTY_CELL,
                row.taxKind || EMPTY_CELL,
                row.method || EMPTY_CELL,
            ]),
        ]);
        message.success(`已导出 ${payRows.length} 条缴税记录。`);
    }

    const paymentColumns: TableColumnsType<TaxIitPaymentRow> = [
        { title: "税款所属期", dataIndex: "period", key: "period", width: 180, render: textCell },
        { title: "报表类型", dataIndex: "formName", key: "formName", width: 320, render: textCell },
        { title: "电子税票号", dataIndex: "eTicketNo", key: "eTicketNo", width: 260, render: textCell },
        { title: "缴税金额", dataIndex: "amount", key: "amount", width: 180, align: "right", render: numberCell },
        { title: "缴税日期", dataIndex: "paidOn", key: "paidOn", width: 200, render: textCell },
        { title: "缴税种类", dataIndex: "taxKind", key: "taxKind", width: 260, render: textCell },
        { title: "缴税方式", dataIndex: "method", key: "method", width: 260, render: textCell },
    ];

    const tabs = useMemo(
        () => [
            { key: "declarations" as TabKey, label: "申报记录查询" },
            { key: "payments" as TabKey, label: "缴税记录查询" },
        ],
        [],
    );

    return (
        <Card>
            <div className="flex shrink-0 items-center gap-6 border-b border-(--border)">
                {tabs.map((item) => (
                    <button
                        key={item.key}
                        type="button"
                        onClick={() => {
                            // 上次没取到（多半是个税没登录）时，再进这个页签要再查一次、再弹一次登录框。
                            if (item.key === "declarations" && state === "failed") setState("idle");
                            if (item.key === "payments" && (payState === "idle" || payState === "failed")) {
                                setPayState("idle");
                                void queryPayments();
                            }
                            setTab(item.key);
                        }}
                        className="relative h-[46px] text-[13px] font-medium text-(--foreground)"
                    >
                        {item.label}
                        {tab === item.key && <span className="absolute inset-x-0 bottom-0 h-[2px] bg-(--brand-fill)" />}
                    </button>
                ))}
            </div>

            {tab === "declarations" ? (
                <>
                    <div className="flex shrink-0 flex-wrap items-center gap-3 pt-3 text-[13px]">
                        <span className="text-(--muted-foreground)">税款所属期：</span>
                        <DatePicker
                            picker="month"
                            allowClear={false}
                            className="w-[140px]"
                            value={range[0]}
                            format="YYYY-MM"
                            onChange={(value) => value && setRange([value, range[1].isBefore(value) ? value : range[1]])}
                        />
                        <span className="text-(--muted-foreground)">至</span>
                        <DatePicker
                            picker="month"
                            allowClear={false}
                            className="w-[140px]"
                            value={range[1]}
                            format="YYYY-MM"
                            onChange={(value) => value && setRange([range[0].isAfter(value) ? value : range[0], value])}
                        />
                        <Button type="primary" loading={loading} onClick={() => void queryFilings()}>
                            查询
                        </Button>
                        <Button className="ml-auto" disabled={rows.length === 0} onClick={exportDeclarations}>
                            导出
                        </Button>
                    </div>

                    <div className="min-h-0 flex-1 pb-3 pt-3">
                        {error && (
                            <div className="mb-3 flex items-center gap-3 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-1.5 text-[12.5px] text-(--warn-text)">
                                <span>{error}</span>
                                <Button size="small" onClick={() => setLoginOpen(true)}>
                                    {needLogin ? "重新登录个税" : "登录个税"}
                                </Button>
                            </div>
                        )}
                        <Table<TaxIitFilingRow>
                            columns={declarationColumns}
                            dataSource={rows}
                            rowKey="key"
                            loading={loading}
                            scroll={{ x: 1660 }}
                            pagination={{
                                pageSize: 20,
                                showSizeChanger: true,
                                showTotal: (total) => `共 ${total} 条记录`,
                            }}
                            locale={{
                                emptyText: (
                                    <span className="block py-16 text-[13px] text-(--muted-foreground)">
                                        {loading ? "正在获取申报记录…" : "暂无数据"}
                                    </span>
                                ),
                            }}
                        />
                        <div className="mt-2 text-[12px] text-(--muted-foreground)">
                            申报记录来自自然人电子税务局的扣缴申报，未申报的所属期不会出现在这里。
                        </div>
                    </div>
                </>
            ) : (
                <>
                    <div className="flex shrink-0 flex-wrap items-center gap-3 pt-3 text-[13px]">
                        <Select
                            value={payDateField}
                            onChange={(value) => {
                                setPayDateField(value);
                                // 换了筛选列，旧行不再是同一口径：清空后由用户点「查询」重取。
                                setPayRows([]);
                                setPayError(null);
                            }}
                            className="w-[132px]"
                            options={[
                                { value: "paidOn", label: "缴税日期" },
                                { value: "period", label: "税款所属期" },
                            ]}
                        />
                        <DatePicker.RangePicker
                            allowClear={false}
                            className="w-[260px]"
                            picker={payDateField === "period" ? "month" : "date"}
                            format={payDateField === "period" ? "YYYY-MM" : "YYYY-MM-DD"}
                            value={payRange}
                            onChange={(value) => value && value[0] && value[1] && setPayRange([value[0], value[1]])}
                        />
                        <Button type="primary" loading={payLoading} onClick={() => void queryPayments()}>
                            查询
                        </Button>
                        <div className="ml-auto flex items-center gap-2">
                            <Button disabled={payRows.length === 0} onClick={exportPayments}>
                                导出
                            </Button>
                            <Tooltip title="完税证明由电子税务局开具，本页只提供缴税流水；开具请到「税费缴纳 › 开具完税证明」">
                                <span>
                                    <Button disabled>开具完税证明</Button>
                                </span>
                            </Tooltip>
                        </div>
                    </div>

                    <div className="min-h-0 flex-1 pb-3 pt-3">
                        {payError && (
                            <div className="mb-3 flex items-center gap-3 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-1.5 text-[12.5px] text-(--warn-text)">
                                <span>{payError}</span>
                                <Button size="small" onClick={() => setLoginOpen(true)}>
                                    {payNeedLogin ? "重新登录个税" : "登录个税"}
                                </Button>
                                <Button size="small" type="link" onClick={toTaxLogin}>
                                    前往一键登录税局
                                </Button>
                            </div>
                        )}
                        <Table<TaxIitPaymentRow>
                            columns={paymentColumns}
                            dataSource={payRows}
                            rowKey="key"
                            loading={payLoading}
                            scroll={{ x: 1660 }}
                            pagination={{
                                pageSize: 20,
                                showSizeChanger: true,
                                showTotal: (total) => `共 ${total} 条记录`,
                            }}
                            locale={{
                                emptyText: (
                                    <span className="block py-16 text-[13px] text-(--muted-foreground)">
                                        {payLoading ? "正在获取缴税记录…" : "暂无数据"}
                                    </span>
                                ),
                            }}
                        />
                        <div className="mt-2 text-[12px] text-(--muted-foreground)">
                            缴税记录取自个税扣缴端「缴税记录查询」，是扣缴端按人按次的缴款流水；企业端的缴款信息请到「税费缴纳 › 缴款信息查询」核对。
                        </div>
                    </div>
                </>
            )}

            <IitLoginModal
                open={loginOpen}
                accountSetId={accountSetId}
                onClose={() => setLoginOpen(false)}
                onSuccess={() => {
                    setLoginOpen(false);
                    if (tab === "payments") {
                        void queryPayments();
                    } else {
                        void queryFilings();
                    }
                }}
            />
        </Card>
    );
}
