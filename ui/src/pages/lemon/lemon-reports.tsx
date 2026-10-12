/**
 * 柠檬云报表模块（对应 /Statements/*，已接真实后端 ReportsApi/CustomReportsApi）：
 * 利润表季报、现金流量表（月/季/标准）、部门/项目利润表、
 * 数据透视表（自定义报表）、费用明细表、所有者权益变动表、纳税统计表、财务概要信息。
 * 资产负债表与利润表（月报）与既有实页重叠，复刻版已下线（见 router）。
 */
import { Fragment, useCallback, useEffect, useState } from "react";
import { CalendarDays, CircleCheck, Info } from "lucide-react";

import {
    Btn,
    CheckBox,
    EmptyTip,
    FakeSelect,
    Page,
    Pager,
    PeriodStepper,
    RefreshIcon,
    Table,
    Toast,
    ToolRow,
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { downloadReportExport } from "@/lib/report-download";
import {
    CustomReportsApi,
    ReportsApi,
    type CashFlowDto,
    type CashFlowQuarterDto,
    type CustomReportDto,
    type CustomReportRenderDto,
    type DepartmentIncomeDto,
    type EquityChangeDto,
    type EquityChangeRowDto,
    type ExpenseDetailDto,
    type FinancialSummaryDto,
    type FinancialSummaryRowDto,
    type IncomeStatementQuarterDto,
    type ProjectIncomeDto,
    type TaxStatisticsDto,
} from "@/apis-gen";

/* ---------- 共用 ---------- */

/** 报表工具行：左期间步进器、右按钮组。 */
function ReportTool({ text, right }: { text: string; right: React.ReactNode }) {
    return <ToolRow left={<PeriodStepper text={text} />} right={right} />;
}

/** 金额格式化：千分位 + 两位小数；空值返回空串。 */
function nf(v: number | null | undefined): string {
    return v == null
        ? ""
        : v.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** 百分比格式化：后端已是百分数值（如 8.36 表示 8.36%）。 */
function pct(v: number | null | undefined): string {
    return v == null ? "" : `${nf(v)}%`;
}

/** 两个数值相除的百分比文本（分母为 0 时为空）。 */
function ratioPct(a: number | null | undefined, b: number | null | undefined): string {
    if (!a || !b) {
        return "";
    }
    return `${((a / b) * 100).toFixed(2)}%`;
}

/** 行项目构造器：indent 缩进、check 绿勾、neg 红字由列渲染处理。 */
type StmtRow = {
    item: React.ReactNode;
    seq: string;
    ytd?: string;
    cur?: string;
    ytdYoy?: string;
    curYoy?: string;
    curMom?: string;
};

function item(label: string, seq: string, ytd = "", cur = "", opts: { check?: boolean; indent?: boolean } = {}): StmtRow {
    return {
        item: (
            <span className={opts.indent ? "pl-6" : ""}>
                {opts.check && <CircleCheck className="mr-1 inline h-3.5 w-3.5 align-[-2px] text-[#44b449]" />}
                {label}
            </span>
        ),
        seq,
        ytd,
        cur,
    };
}

const INCOME_COLS = (curTitle: string, withMark = true): Col[] => [
    { key: "item", title: "项目" },
    { key: "seq", title: "行次", width: "70px" },
    ...(withMark ? [{ key: "mark", title: "≈", width: "65px" } as Col] : []),
    { key: "ytd", title: "本年累计金额", align: "right", width: "160px", neg: true },
    { key: "cur", title: curTitle, align: "right", width: "140px", neg: true },
];

/** 刷新按钮（可点击触发重载）。 */
function ReloadButton({ onClick }: { onClick: () => void }) {
    return (
        <span className="cursor-pointer" onClick={onClick} title="刷新">
            <RefreshIcon />
        </span>
    );
}

/** 报表导出：调用后端 /reports/export 下载 .xlsx；kind 与页面一一对应。 */
function useReportExport() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [exporting, setExporting] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    const run = useCallback(
        async (kind: string, params: { year?: number; month?: number; quarter?: number } = {}) => {
            if (!token || !accountSetId || exporting) {
                return;
            }
            setExporting(true);
            try {
                await downloadReportExport(token, accountSetId, kind, params);
            } catch (e) {
                setToast(e instanceof Error ? e.message : "导出失败");
            } finally {
                setExporting(false);
            }
        },
        [token, accountSetId, exporting],
    );
    return { run, exporting, toast, clearToast: () => setToast(null) };
}

/* ---------- 利润表季报 ---------- */

type IncomeLineValues = { current: number; yearToDate: number };

/** 由后端明细行组装利润表展示行（含营业利润/利润总额/净利润计算行），行次沿用柠檬云编号。 */
function buildIncomeRows(lines?: Array<{ line?: number; current?: number; yearToDate?: number }>): StmtRow[] {
    const byLine = new Map<number, IncomeLineValues>();
    for (const l of lines ?? []) {
        if (l?.line != null) {
            byLine.set(l.line, { current: l.current ?? 0, yearToDate: l.yearToDate ?? 0 });
        }
    }
    const v = (line: number, key: keyof IncomeLineValues): number => byLine.get(line)?.[key] ?? 0;
    const s = (line: number, key: keyof IncomeLineValues): string => nf(byLine.get(line)?.[key]);
    const sum = (keys: number[], key: keyof IncomeLineValues): number => keys.reduce((acc, l) => acc + v(l, key), 0);

    const revenueLines = [1, 8, 9, 11, 14];
    const expenseLines = [2, 3, 4, 5, 6, 7, 12, 13];
    const rows: StmtRow[] = [
        item("一、营业收入", "1", s(1, "yearToDate"), s(1, "current")),
        item("减：营业成本", "2", s(2, "yearToDate"), s(2, "current"), { indent: true }),
        item("税金及附加", "3", s(3, "yearToDate"), s(3, "current"), { check: true, indent: true }),
        item("销售费用", "11", s(4, "yearToDate"), s(4, "current"), { check: true, indent: true }),
        item("管理费用", "14", s(5, "yearToDate"), s(5, "current"), { check: true, indent: true }),
        item("财务费用", "18", s(6, "yearToDate"), s(6, "current"), { check: true, indent: true }),
        item("加：投资收益（亏损以“-”号填列）", "20", s(9, "yearToDate"), s(9, "current"), { indent: true }),
    ];
    const operatingYtd = sum(revenueLines, "yearToDate") - sum(expenseLines, "yearToDate");
    const operatingCur = sum(revenueLines, "current") - sum(expenseLines, "current");
    rows.push(item("二、营业利润（亏损以“-”号填列）", "21", nf(operatingYtd), nf(operatingCur)));
    rows.push(item("加：营业外收入", "22", s(16, "yearToDate"), s(16, "current"), { check: true, indent: true }));
    rows.push(item("减：营业外支出", "24", s(17, "yearToDate"), s(17, "current"), { check: true, indent: true }));
    const totalYtd = operatingYtd + v(16, "yearToDate") - v(17, "yearToDate");
    const totalCur = operatingCur + v(16, "current") - v(17, "current");
    rows.push(item("三、利润总额（亏损总额以“-”号填列）", "30", nf(totalYtd), nf(totalCur)));
    rows.push(item("减：所得税费用", "31", s(19, "yearToDate"), s(19, "current"), { indent: true }));
    rows.push(item("四、净利润（净亏损以“-”号填列）", "32", nf(totalYtd - v(19, "yearToDate")), nf(totalCur - v(19, "current"))));
    return rows;
}

export function LemonIncomeQuarter() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [data, setData] = useState<IncomeStatementQuarterDto | null>(null);
    const exp = useReportExport();
    const [error, setError] = useState<string | null>(null);
    const [tick, setTick] = useState(0);

    const reload = useCallback(() => setTick((t) => t + 1), []);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        let alive = true;
        setError(null);
        new ReportsApi(apiConfig(token))
            .accountsetsAccountSetIdReportsIncomeQuarterGet({ accountSetId })
            .then((d) => alive && setData(d))
            .catch(async (e) => alive && setError(await apiErrorMessage(e, "利润表季报加载失败")))
            .finally(() => alive && undefined);
        return () => {
            alive = false;
        };
    }, [token, accountSetId, tick]);

    return (
        <Page>
            <ReportTool
                text={data?.periodText ?? "加载中…"}
                right={
                    <>
                        <CheckBox label="显示本年所有季度" />
                        <CheckBox label="显示上年累计金额" />
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <Btn dropdown onClick={() => void exp.run("income-quarter", { year: data?.year, quarter: data?.quarter })}>导出</Btn>
                        <Btn>微信分享</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="flex-1 px-4">
                <Table cols={INCOME_COLS("本季金额", false)} rows={buildIncomeRows(data?.lines)} zebra={false} />
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

/* ---------- 现金流量表（月/季/标准） ---------- */

/** 小企业口径行次 → 标准主表行次映射（标准行 28 同时覆盖利息与利润分配，展示在 17 行）。 */
const CF_LINE_MAP: Record<string, number> = {
    "1": 1, "2": 3, "3": 5, "4": 6, "5": 7, "6": 8, "7": 10,
    "8": 11, "9": 12, "10": 13, "11": 18, "12": 17, "13": 22,
    "14": 24, "15": 23, "16": 27, "17": 28,
};

function CashFlowPage({
    variant,
}: {
    variant: "month" | "quarter" | "standard";
}) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [monthData, setMonthData] = useState<CashFlowDto | null>(null);
    const exp = useReportExport();
    const [quarterData, setQuarterData] = useState<CashFlowQuarterDto | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [tick, setTick] = useState(0);

    const reload = useCallback(() => setTick((t) => t + 1), []);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        let alive = true;
        setError(null);
        const api = new ReportsApi(apiConfig(token));
        const load = variant === "quarter"
            ? api.accountsetsAccountSetIdReportsCashFlowQuarterGet({ accountSetId }).then((d) => alive && setQuarterData(d))
            : api.accountsetsAccountSetIdReportsCashFlowGet({ accountSetId }).then((d) => alive && setMonthData(d));
        load.catch(async (e) => alive && setError(await apiErrorMessage(e, "现金流量表加载失败")));
        return () => {
            alive = false;
        };
    }, [token, accountSetId, tick, variant]);

    const periodText = variant === "quarter" ? quarterData?.periodText : monthData?.periodText;
    const lines = (variant === "quarter" ? quarterData?.lines : monthData?.lines) ?? [];
    const byLine = new Map<number, { current: number; yearToDate: number }>();
    for (const l of lines) {
        if (l?.line != null) {
            byLine.set(l.line, { current: l.current ?? 0, yearToDate: l.yearToDate ?? 0 });
        }
    }
    const cell = (seq: string, key: "current" | "yearToDate"): string => {
        const std = CF_LINE_MAP[seq];
        return std == null ? "" : nf(byLine.get(std)?.[key]);
    };

    const mk = (label: string, seq: string) =>
        item(label, seq, cell(seq, "yearToDate"), cell(seq, "current"), { indent: true });
    const rows: StmtRow[] = [
        item("一、经营活动产生的现金流量：", ""),
        mk("销售产成品、商品、提供劳务收到的现金", "1"),
        mk("收到其他与经营活动有关的现金", "2"),
        mk("购买原材料、商品、接受劳务支付的现金", "3"),
        mk("支付的职工薪酬", "4"),
        mk("支付的税费", "5"),
        mk("支付其他与经营活动有关的现金", "6"),
        mk("经营活动产生的现金流量净额", "7"),
        item("二、投资活动产生的现金流量：", ""),
        mk("收回短期投资、长期债券投资和长期股权投资收到的现金", "8"),
        mk("取得投资收益收到的现金", "9"),
        mk("处置固定资产、无形资产和其他非流动资产收回的现金净额", "10"),
        mk("短期投资、长期债券投资和长期股权投资支付的现金", "11"),
        mk("购建固定资产、无形资产和其他非流动资产支付的现金", "12"),
        mk("投资活动产生的现金流量净额", "13"),
        item("三、筹资活动产生的现金流量：", ""),
        mk("取得借款收到的现金", "14"),
        mk("吸收投资者投资收到的现金", "15"),
        mk("偿还借款本金支付的现金", "16"),
    ];
    if (variant !== "month") {
        rows.push(mk("偿还借款利息支付的现金", "17"), item("分配利润支付的现金", "18", "", "", { indent: true }));
    }

    const cols: Col[] = [
        { key: "item", title: "项目" },
        { key: "seq", title: "行次", width: "70px" },
        { key: "ytd", title: "本年累计金额", align: "right", width: "160px", neg: true },
        { key: "cur", title: variant === "quarter" ? "本季金额" : "本期金额", align: "right", width: "140px", neg: true },
    ];
    return (
        <Page>
            <ToolRow
                left={
                    <>
                        {variant === "standard" && <FakeSelect text="月报" className="w-[90px]" />}
                        <PeriodStepper text={periodText ?? "加载中…"} />
                        {variant === "month" && <FakeSelect text="公式法" className="w-[100px]" />}
                        {variant === "month" && (
                            <span className="flex items-center gap-1 text-[13px] text-[#3385ff]">
                                <Info className="h-3.5 w-3.5" /> 帮助
                            </span>
                        )}
                        {variant === "standard" && (
                            <span className="flex items-center gap-1 text-[13px] text-[#3385ff]">
                                <Info className="h-3.5 w-3.5" /> 报表说明
                            </span>
                        )}
                    </>
                }
                right={
                    <>
                        <CheckBox label="显示上年累计金额" />
                        {variant !== "quarter" && <Btn>期初</Btn>}
                        {variant === "month" && <Btn>报表底稿</Btn>}
                        {variant === "standard" && <Btn>现金流量调整</Btn>}
                        {variant === "standard" ? (
                            <>
                                <Btn dropdown onClick={() => void exp.run("cash-flow", { year: monthData?.year, month: monthData?.month })}>导出</Btn>
                                <Btn dropdown onClick={() => window.print()}>打印</Btn>
                                <Btn>分享</Btn>
                            </>
                        ) : (
                            <>
                                <Btn dropdown onClick={() => window.print()}>打印</Btn>
                                <Btn dropdown onClick={() => void exp.run(variant === "quarter" ? "cash-flow-quarter" : "cash-flow", variant === "quarter" ? {} : { year: monthData?.year, month: monthData?.month })}>导出</Btn>
                                <Btn>微信分享</Btn>
                            </>
                        )}
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} zebra={false} />
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

export function LemonCashFlow() {
    return <CashFlowPage variant="month" />;
}
export function LemonCashFlowQuarter() {
    return <CashFlowPage variant="quarter" />;
}
export function LemonStandardCashFlow() {
    return <CashFlowPage variant="standard" />;
}

/* ---------- 部门利润表 ---------- */

export function LemonDepartmentIncome() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [data, setData] = useState<DepartmentIncomeDto | null>(null);
    const exp = useReportExport();
    const [error, setError] = useState<string | null>(null);
    const [tick, setTick] = useState(0);

    const reload = useCallback(() => setTick((t) => t + 1), []);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        let alive = true;
        setError(null);
        new ReportsApi(apiConfig(token))
            .accountsetsAccountSetIdReportsDepartmentIncomeGet({ accountSetId })
            .then((d) => alive && setData(d))
            .catch(async (e) => alive && setError(await apiErrorMessage(e, "部门利润表加载失败")));
        return () => {
            alive = false;
        };
    }, [token, accountSetId, tick]);

    const th = "border-b border-[#e8e8e8] bg-[#f0f7f0] px-3 py-2 text-[13px] font-normal text-[#333]";
    const td = "border-b border-[#f0f0f0] px-3 py-2.5 text-[13px] text-[#333]";
    const departments = data?.departments ?? [];
    const rows = data?.rows ?? [];
    return (
        <Page>
            <ToolRow
                left={
                    <>
                        <span className="inline-flex h-8 items-center gap-1.5 rounded-[2px] bg-[#44b449] px-2.5 text-sm text-white">
                            {data?.periodText ?? "加载中…"} <CalendarDays className="h-3.5 w-3.5" />
                        </span>
                        <span className="flex items-center gap-1 text-[13px] text-[#3385ff]">
                            <Info className="h-3.5 w-3.5" /> 帮助
                        </span>
                    </>
                }
                right={
                    <>
                        <Btn dropdown onClick={() => void exp.run("department-income", { year: data?.year, month: data?.month })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="flex-1 overflow-x-auto px-4">
                <table className="border-collapse text-sm whitespace-nowrap">
                    <thead>
                        <tr>
                            <th rowSpan={2} className={`${th} text-left`}>
                                项目
                            </th>
                            <th rowSpan={2} className={`${th} w-[60px]`}>
                                行次
                            </th>
                            {departments.map((d) => (
                                <th key={d.id} colSpan={2} className={`${th} text-center`}>
                                    {d.name}
                                </th>
                            ))}
                        </tr>
                        <tr>
                            {departments.map((d) => (
                                <Fragment key={d.id}>
                                    <th className={`${th} text-right`}>本期金额</th>
                                    <th className={`${th} text-right`}>本年累计金额</th>
                                </Fragment>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {rows.length === 0 && !error && (
                            <tr>
                                <td colSpan={2 + departments.length * 2} className="py-10 text-center text-[13px] text-[#999]">
                                    暂无数据
                                </td>
                            </tr>
                        )}
                        {rows.map((r) => (
                            <tr key={r.line}>
                                <td className={td}>{r.item}</td>
                                <td className={td}>{r.line}</td>
                                {departments.map((d, i) => (
                                    <Fragment key={d.id}>
                                        <td className={`${td} text-right`}>{nf(r.amounts?.[i]?.current)}</td>
                                        <td className={`${td} text-right`}>{nf(r.amounts?.[i]?.yearToDate)}</td>
                                    </Fragment>
                                ))}
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
            <Pager total={rows.length} count={rows.length} />
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

/* ---------- 项目利润表 ---------- */

export function LemonProjectIncome() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [data, setData] = useState<ProjectIncomeDto | null>(null);
    const exp = useReportExport();
    const [error, setError] = useState<string | null>(null);
    const [tick, setTick] = useState(0);
    const [tipClosed, setTipClosed] = useState(false);

    const reload = useCallback(() => setTick((t) => t + 1), []);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        let alive = true;
        setError(null);
        new ReportsApi(apiConfig(token))
            .accountsetsAccountSetIdReportsProjectIncomeGet({ accountSetId })
            .then((d) => alive && setData(d))
            .catch(async (e) => alive && setError(await apiErrorMessage(e, "项目利润表加载失败")));
        return () => {
            alive = false;
        };
    }, [token, accountSetId, tick]);

    const rows = (data?.rows ?? []).map((r) => ({
        item: r.name ?? "",
        seq: String(r.line ?? ""),
        cur: nf(r.current),
        ytd: nf(r.yearToDate),
    }));
    const cols: Col[] = [
        { key: "item", title: "项目" },
        { key: "seq", title: "行次", width: "70px" },
        { key: "cur", title: "本期金额", align: "right", width: "160px", neg: true },
        { key: "ytd", title: "本年累计金额", align: "right", width: "160px", neg: true },
    ];
    const showTip = rows.length === 0 && !error && !tipClosed;
    return (
        <Page>
            <ReportTool
                text={data?.periodText ?? "加载中…"}
                right={
                    <>
                        <Btn dropdown onClick={() => void exp.run("project-income", { year: data?.year, month: data?.month })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} zebra={false} empty={<EmptyTip />} />
            </div>
            <Pager total={rows.length} count={rows.length} />
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
            {/* 提示弹窗：无项目档案时提示 */}
            {showTip && (
                <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/45">
                    <div className="w-[420px] rounded-md bg-white shadow-xl">
                        <div className="flex items-center justify-between px-5 pt-4 text-[15px] font-bold text-[#333]">
                            提示
                            <span className="cursor-pointer text-[#999]" onClick={() => setTipClosed(true)}>
                                ×
                            </span>
                        </div>
                        <div className="px-5 py-8 text-center text-sm text-[#333]">
                            暂无项目信息，请先设置项目！
                        </div>
                        <div className="flex justify-center pb-5">
                            <Btn variant="primary" className="w-[96px]" onClick={() => setTipClosed(true)}>
                                确定
                            </Btn>
                        </div>
                    </div>
                </div>
            )}
        </Page>
    );
}

/* ---------- 数据透视表（自定义报表） ---------- */

const inputCls = "h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2.5 text-sm text-[#333] outline-none focus:border-[#44b449]";

export function LemonPivot() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [reports, setReports] = useState<CustomReportDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [checked, setChecked] = useState<Set<string>>(new Set());
    const [guideOpen, setGuideOpen] = useState(true);
    const [creating, setCreating] = useState(false);
    const [form, setForm] = useState({ code: "", name: "", valueType: 1 });
    const [rendered, setRendered] = useState<CustomReportRenderDto | null>(null);
    const [toast, setToast] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!token || !accountSetId) {
            return;
        }
        setLoading(true);
        setError(null);
        try {
            setReports(await new CustomReportsApi(apiConfig(token)).accountsetsAccountSetIdCustomReportsGet({ accountSetId }));
        } catch (e) {
            setError(await apiErrorMessage(e, "自定义报表加载失败"));
        } finally {
            setLoading(false);
        }
    }, [token, accountSetId]);
    useEffect(() => {
        void reload();
    }, [reload]);

    const create = async () => {
        try {
            await new CustomReportsApi(apiConfig(token!)).accountsetsAccountSetIdCustomReportsPost({
                accountSetId,
                saveCustomReportRequest: { code:form.code, name: form.name, valueType: form.valueType },
            });
            setCreating(false);
            setForm({ code: "", name: "", valueType: 1 });
            setToast("创建成功");
            await reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "创建失败"));
        }
    };

    const del = async (ids: string[]) => {
        const api = new CustomReportsApi(apiConfig(token!));
        const results = await Promise.allSettled(ids.map((id) => api.accountsetsAccountSetIdCustomReportsIdDelete({ accountSetId, id })));
        const failed = results.filter((r) => r.status === "rejected").length;
        setChecked(new Set());
        setToast(failed === 0 ? "删除成功" : `${failed} 个删除失败`);
        await reload();
    };

    const copy = async (r: CustomReportDto) => {
        try {
            await new CustomReportsApi(apiConfig(token!)).accountsetsAccountSetIdCustomReportsPost({
                accountSetId,
                saveCustomReportRequest: { code:`${r.code ?? "REPORT"}-C${reports.length + 1}`.slice(0, 50), name: `${r.name ?? "报表"}（副本）`, valueType: r.valueType ?? 1 },
            });
            setToast("复制成功");
            await reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "复制失败"));
        }
    };

    const view = async (r: CustomReportDto) => {
        try {
            setRendered(await new CustomReportsApi(apiConfig(token!)).accountsetsAccountSetIdCustomReportsIdRenderGet({ accountSetId, id: r.id ?? "" }));
        } catch (e) {
            setToast(await apiErrorMessage(e, "渲染失败"));
        }
    };

    const fmtTime = (v: Date | string | null | undefined): string => (v ? new Date(v).toLocaleString("zh-CN", { hour12: false }) : "");
    const cols: Col[] = [
        {
            key: "ck",
            title: <CheckBox label="" />,
            width: "40px",
            render: (row: Row) => (
                <span
                    className="cursor-pointer"
                    onClick={() =>
                        setChecked((prev) => {
                            const next = new Set(prev);
                            if (next.has(String(row.id))) {
                                next.delete(String(row.id));
                            } else {
                                next.add(String(row.id));
                            }
                            return next;
                        })
                    }
                >
                    <CheckBox label="" checked={checked.has(String(row.id))} />
                </span>
            ),
        },
        { key: "code", title: "报表编码", width: "140px" },
        { key: "name", title: "报表名称" },
        { key: "creator", title: "创建人", width: "110px" },
        { key: "createTime", title: "创建时间", width: "170px" },
        { key: "modifier", title: "修改人", width: "110px" },
        { key: "modifyTime", title: "修改时间", width: "170px" },
        {
            key: "ops",
            title: "操作",
            width: "100px",
            render: (row: Row) => (
                <span className="flex gap-2 text-[13px] text-[#3385ff]">
                    <span className="cursor-pointer hover:underline" onClick={() => void view(reports.find((r) => r.id === row.id)!)}>
                        查看
                    </span>
                    <span className="cursor-pointer hover:underline" onClick={() => void del([String(row.id)])}>
                        删除
                    </span>
                </span>
            ),
        },
    ];
    return (
        <Page>
            <ToolRow
                left={
                    <>
                        <Btn variant="primary" onClick={() => setCreating(true)}>
                            新增
                        </Btn>
                        <Btn onClick={() => checked.size === 1 && void copy(reports.find((r) => checked.has(r.id ?? ""))!)}>复制</Btn>
                        <Btn onClick={() => checked.size > 0 && void del([...checked])}>批量删除</Btn>
                    </>
                }
                right={<ReloadButton onClick={() => void reload()} />}
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="flex-1 px-4">
                <Table
                    cols={cols}
                    rows={reports.map((r) => ({
                        id: r.id ?? "",
                        code: r.code ?? "",
                        name: r.name ?? "",
                        creator: r.createdBy ?? "",
                        createTime: fmtTime(r.createdAt),
                        modifier: r.modifiedBy ?? "",
                        modifyTime: fmtTime(r.modifiedAt),
                    }))}
                    zebra={false}
                    empty={loading ? <EmptyTip /> : <EmptyTip />}
                />
            </div>
            <Pager total={reports.length} count={reports.length} />
            {toast && <ToastBar text={toast} onClose={() => setToast(null)} />}
            {/* 引导弹窗：无报表时展示 */}
            {reports.length === 0 && guideOpen && !error && (
                <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/45">
                    <div className="w-[640px] rounded-md bg-white p-6 shadow-xl">
                        <div className="flex items-center justify-between text-[15px] font-bold text-[#333]">
                            数据透视表
                            <span className="cursor-pointer text-[#999]" onClick={() => setGuideOpen(false)}>
                                ×
                            </span>
                        </div>
                        <div className="mt-4 flex items-center gap-8">
                            <div className="flex h-[160px] w-[240px] shrink-0 items-center justify-center rounded bg-[#fafafa] text-[13px] text-[#bbb]">
                                插画
                            </div>
                            <div className="flex flex-col gap-3 text-[13px] leading-6 text-[#666]">
                                <span>
                                    您可以通过数据透视表将会计科目、辅助核算类别、取值金额等字段通过拖拽方式，自定义会计区间来查看不同级别的明细数据；
                                </span>
                                <span>
                                    根据你自己的需求对报表数据进行自定义排列组合布局，生成属于您的专属数据报表
                                </span>
                            </div>
                        </div>
                        <div className="mt-6 flex justify-center gap-3">
                            <Btn variant="primary" className="w-[110px]" onClick={() => setGuideOpen(false)}>
                                去看看
                            </Btn>
                        </div>
                    </div>
                </div>
            )}
            {/* 新增弹窗 */}
            {creating && (
                <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/45">
                    <div className="w-[420px] rounded-md bg-white shadow-xl">
                        <div className="flex items-center justify-between px-5 pt-4 text-[15px] font-bold text-[#333]">
                            新增数据透视表
                            <span className="cursor-pointer text-[#999]" onClick={() => setCreating(false)}>
                                ×
                            </span>
                        </div>
                        <div className="flex flex-col gap-4 px-5 py-6 text-[13px] text-[#333]">
                            <label className="flex items-center gap-3">
                                <span className="w-[70px] text-right">报表编码</span>
                                <input className={inputCls} value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} placeholder="如 ZCZK2026" />
                            </label>
                            <label className="flex items-center gap-3">
                                <span className="w-[70px] text-right">报表名称</span>
                                <input className={inputCls} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="如 资产负债透视" />
                            </label>
                            <label className="flex items-center gap-3">
                                <span className="w-[70px] text-right">取值口径</span>
                                <select
                                    className={inputCls}
                                    value={form.valueType}
                                    onChange={(e) => setForm({ ...form, valueType: Number(e.target.value) })}
                                >
                                    <option value={1}>期末余额</option>
                                    <option value={2}>期间发生额</option>
                                </select>
                            </label>
                        </div>
                        <div className="flex justify-center gap-3 pb-5">
                            <Btn variant="primary" className="w-[96px]" onClick={() => void create()}>
                                保存
                            </Btn>
                            <Btn className="w-[96px]" onClick={() => setCreating(false)}>
                                取消
                            </Btn>
                        </div>
                    </div>
                </div>
            )}
            {/* 渲染结果弹窗：科目 × 12 个月 */}
            {rendered && (
                <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/45 p-6">
                    <div className="max-h-[80vh] w-[980px] max-w-full overflow-auto rounded-md bg-white shadow-xl">
                        <div className="sticky top-0 flex items-center justify-between border-b border-[#eee] bg-white px-5 py-3 text-[15px] font-bold text-[#333]">
                            {`${rendered.name ?? ""}（${rendered.year ?? ""} 年 · ${rendered.valueType === 2 ? "期间发生额" : "期末余额"}）`}
                            <span className="cursor-pointer text-[#999]" onClick={() => setRendered(null)}>
                                ×
                            </span>
                        </div>
                        <div className="p-4">
                            <table className="w-full border-collapse text-sm whitespace-nowrap">
                                <thead>
                                    <tr>
                                        <th className="border border-[#e8e8e8] bg-[#f0f7f0] px-2 py-1.5 text-[12px] font-normal">科目编码</th>
                                        <th className="border border-[#e8e8e8] bg-[#f0f7f0] px-2 py-1.5 text-[12px] font-normal">科目名称</th>
                                        {Array.from({ length: 12 }, (_, m) => (
                                            <th key={m} className="border border-[#e8e8e8] bg-[#f0f7f0] px-2 py-1.5 text-right text-[12px] font-normal">
                                                {m + 1}月
                                            </th>
                                        ))}
                                        <th className="border border-[#e8e8e8] bg-[#f0f7f0] px-2 py-1.5 text-right text-[12px] font-normal">合计</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {(rendered.rows ?? []).map((r) => (
                                        <tr key={r.code}>
                                            <td className="border border-[#f0f0f0] px-2 py-1.5 text-[12px] text-[#333]">{r.code}</td>
                                            <td className="border border-[#f0f0f0] px-2 py-1.5 text-[12px] text-[#333]">{r.name}</td>
                                            {(r.months ?? []).map((v, m) => (
                                                <td key={m} className={`border border-[#f0f0f0] px-2 py-1.5 text-right text-[12px] ${v < 0 ? "text-[#f5222d]" : "text-[#333]"}`}>
                                                    {nf(v)}
                                                </td>
                                            ))}
                                            <td className={`border border-[#f0f0f0] px-2 py-1.5 text-right text-[12px] font-medium ${(r.total ?? 0) < 0 ? "text-[#f5222d]" : "text-[#333]"}`}>
                                                {nf(r.total)}
                                            </td>
                                        </tr>
                                    ))}
                                    {(rendered.rows ?? []).length === 0 && (
                                        <tr>
                                            <td colSpan={14} className="py-8 text-center text-[13px] text-[#999]">
                                                本年度无数据
                                            </td>
                                        </tr>
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            )}
        </Page>
    );
}

/** 右下轻提示（数据透视表操作反馈）。 */
function ToastBar({ text, onClose }: { text: string; onClose: () => void }) {
    useEffect(() => {
        const timer = setTimeout(onClose, 2400);
        return () => clearTimeout(timer);
    }, [onClose, text]);
    return (
        <div className="fixed bottom-8 right-8 z-50 rounded bg-[#333]/90 px-4 py-2 text-[13px] text-white shadow-lg">{text}</div>
    );
}

/* ---------- 费用明细表 ---------- */

export function LemonExpenseDetail() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [data, setData] = useState<ExpenseDetailDto | null>(null);
    const exp = useReportExport();
    const [error, setError] = useState<string | null>(null);
    const [expandAll, setExpandAll] = useState(true);
    const [tick, setTick] = useState(0);

    const reload = useCallback(() => setTick((t) => t + 1), []);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        let alive = true;
        setError(null);
        new ReportsApi(apiConfig(token))
            .accountsetsAccountSetIdReportsExpenseDetailGet({ accountSetId })
            .then((d) => alive && setData(d))
            .catch(async (e) => alive && setError(await apiErrorMessage(e, "费用明细表加载失败")));
        return () => {
            alive = false;
        };
    }, [token, accountSetId, tick]);

    const all = data?.rows ?? [];
    const visible = expandAll ? all : all.filter((r) => (r.level ?? 1) === 1);
    const topRows = all.filter((r) => (r.level ?? 1) === 1);
    const rows: Row[] = [
        ...visible.map((r) => ({
            code: `${r.hasChildren ? "⌄ " : "　".repeat(Math.max((r.level ?? 1) - 1, 0))}${r.code ?? ""}`,
            name: r.name ?? "",
            cur: nf(r.current),
            total: nf(r.total),
        })),
        {
            code: "",
            name: "总计",
            cur: nf(topRows.reduce((s, r) => s + (r.current ?? 0), 0)),
            total: nf(topRows.reduce((s, r) => s + (r.total ?? 0), 0)),
            __cls: "font-bold",
        },
    ];
    const cols: Col[] = [
        { key: "code", title: "科目编码", width: "140px" },
        { key: "name", title: "科目名称" },
        { key: "cur", title: data?.periodText ?? "本期", align: "right", width: "160px", neg: true },
        { key: "total", title: "合计", align: "right", width: "160px", neg: true },
    ];
    return (
        <Page>
            <ToolRow
                left={
                    <>
                        <span className="inline-flex h-8 items-center gap-1.5 rounded-[2px] bg-[#44b449] px-2.5 text-sm text-white">
                            {data?.periodText ?? "加载中…"} <CalendarDays className="h-3.5 w-3.5" />
                        </span>
                <span className="cursor-pointer" onClick={() => setExpandAll((v) => !v)}>
                    <CheckBox label="展开所有级次" checked={expandAll} />
                </span>
                    </>
                }
                right={
                    <>
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <Btn onClick={() => void exp.run("expense-detail", { year: data?.year, month: data?.month })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} zebra={false} />
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

/* ---------- 所有者权益变动表 ---------- */

const EQ_COLS = ["实收资本", "优先股", "永续债", "其他", "资本公积", "减：库存股", "其他综合收益", "专项储备", "盈余公积", "未分配利润", "所有者权益合计"];

/** 列下标 → 后端权益桶（0 实收资本 / 1 资本公积 / 2 盈余公积 / 3 未分配利润 / 4 合计）。 */
const EQ_BUCKET_BY_COL: Record<number, number> = { 0: 0, 4: 1, 8: 2, 9: 3, 10: 4 };
const EQ_ITEMS = ["实收资本（或股本）", "资本公积", "盈余公积", "未分配利润", "所有者权益合计"];

export function LemonEquityChange() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [data, setData] = useState<EquityChangeDto | null>(null);
    const exp = useReportExport();
    const [error, setError] = useState<string | null>(null);
    const [tick, setTick] = useState(0);

    const reload = useCallback(() => setTick((t) => t + 1), []);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        let alive = true;
        setError(null);
        new ReportsApi(apiConfig(token))
            .accountsetsAccountSetIdReportsEquityChangeGet({ accountSetId })
            .then((d) => alive && setData(d))
            .catch(async (e) => alive && setError(await apiErrorMessage(e, "所有者权益变动表加载失败")));
        return () => {
            alive = false;
        };
    }, [token, accountSetId, tick]);

    const byName = new Map<string, EquityChangeRowDto>();
    for (const r of data?.rows ?? []) {
        if (r.item) {
            byName.set(r.item, r);
        }
    }
    /** 取某列（权益桶）在某字段的展示值。 */
    const val = (col: number, pick: (r: EquityChangeRowDto) => number | undefined): string => {
        const bucket = EQ_BUCKET_BY_COL[col];
        if (bucket == null) {
            return "";
        }
        const r = byName.get(EQ_ITEMS[bucket]);
        return r ? nf(pick(r)) : "";
    };
    const deltaOf = (r: EquityChangeRowDto, begin: "currentBegin" | "priorBegin", end: "currentEnd" | "priorEnd"): number =>
        (r[end] ?? 0) - (r[begin] ?? 0);

    const rows = [
        { label: "一、上年年末余额", v: (c: number) => val(c, (r) => r.priorEnd), pv: (c: number) => val(c, (r) => r.priorBegin) },
        { label: "  加：会计政策变更", v: () => "", pv: () => "" },
        { label: "  前期差错更正", v: () => "", pv: () => "" },
        { label: "  其他", v: () => "", pv: () => "" },
        { label: "二、本年年初余额", v: (c: number) => val(c, (r) => r.currentBegin), pv: (c: number) => val(c, (r) => r.priorBegin) },
        { label: "三、本年增减变动金额（减少以“-”号填列）", v: (c: number) => val(c, (r) => deltaOf(r, "currentBegin", "currentEnd")), pv: (c: number) => val(c, (r) => deltaOf(r, "priorBegin", "priorEnd")) },
        { label: "  （一）综合收益总额", v: () => "", pv: () => "" },
        { label: "  （二）所有者投入和减少资本", v: () => "", pv: () => "" },
        { label: "    1.所有者投入的普通股", v: () => "", pv: () => "" },
        { label: "    2.其他权益工具持有者投入资本", v: () => "", pv: () => "" },
        { label: "  （三）利润分配", v: () => "", pv: () => "" },
        { label: "    1.提取盈余公积", v: () => "", pv: () => "" },
        { label: "    2.对所有者（或股东）的分配", v: () => "", pv: () => "" },
        { label: "  （四）所有者权益内部结转", v: () => "", pv: () => "" },
        { label: "四、本年年末余额", v: (c: number) => val(c, (r) => r.currentEnd), pv: (c: number) => val(c, (r) => r.priorEnd) },
    ];

    const total = byName.get(EQ_ITEMS[4]);
    const balanced =
        total != null && Math.abs((total.currentBegin ?? 0) + ((total.currentEnd ?? 0) - (total.currentBegin ?? 0)) - (total.currentEnd ?? 0)) < 0.01;

    const th = "border-b border-[#e8e8e8] bg-[#f0f7f0] px-3 py-2 text-[13px] font-normal text-[#333]";
    const td = "border-b border-[#f0f0f0] px-3 py-2.5 text-[13px] text-[#333]";
    return (
        <Page>
            <ToolRow
                left={
                    <>
                        <span className="text-[13px] text-[#666]">会计年度：</span>
                        <FakeSelect text={data ? `${data.year}年` : "加载中…"} className="w-[110px]" />
                        <span className="flex items-center gap-1 text-[13px] text-[#3385ff]">
                            <Info className="h-3.5 w-3.5" /> 报表说明
                        </span>
                        {!balanced && (
                            <span className="text-[13px] text-[#f5222d]">
                                报表不平，（未分配利润）本年年初余额+本年增加变动金额不等于本年年末...
                            </span>
                        )}
                    </>
                }
                right={
                    <>
                        <Btn>编辑公式</Btn>
                        <Btn onClick={() => void exp.run("equity-change", { year: data?.year })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="relative flex-1 overflow-x-auto px-4">
                <table className="border-collapse text-sm whitespace-nowrap">
                    <thead>
                        <tr>
                            <th rowSpan={3} className={`${th} text-left`}>
                                项目
                            </th>
                            <th colSpan={11} className={`${th} text-center`}>
                                本年金额
                            </th>
                            <th colSpan={11} className={`${th} text-center`}>
                                上年金额
                            </th>
                        </tr>
                        <tr>
                            <th rowSpan={2} className={th}>
                                实收资本
                            </th>
                            <th colSpan={3} className={`${th} text-center`}>
                                其他权益工具
                            </th>
                            {EQ_COLS.slice(4).map((c) => (
                                <th key={`y${c}`} rowSpan={2} className={th}>
                                    {c}
                                </th>
                            ))}
                            <th rowSpan={2} className={th}>
                                实收资本
                            </th>
                            <th colSpan={3} className={`${th} text-center`}>
                                其他权益工具
                            </th>
                            {EQ_COLS.slice(4).map((c) => (
                                <th key={`o${c}`} rowSpan={2} className={th}>
                                    {c}
                                </th>
                            ))}
                        </tr>
                        <tr>
                            {["优先股", "永续债", "其他"].map((c) => (
                                <th key={`y${c}`} className={th}>
                                    {c}
                                </th>
                            ))}
                            {["优先股", "永续债", "其他"].map((c) => (
                                <th key={`o${c}`} className={th}>
                                    {c}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {rows.map((r, i) => (
                            <tr key={i}>
                                <td className={td}>{r.label}</td>
                                {EQ_COLS.map((_, j) => (
                                    <td key={j} className={`${td} text-right`}>
                                        {r.v(j)}
                                    </td>
                                ))}
                                {EQ_COLS.map((_, j) => (
                                    <td key={`o${j}`} className={`${td} text-right`}>
                                        {r.pv(j)}
                                    </td>
                                ))}
                            </tr>
                        ))}
                    </tbody>
                </table>
                {/* 专业版到期浮层 */}
                <div className="absolute bottom-8 left-1/2 z-10 w-[360px] -translate-x-1/2 rounded-lg bg-[#fdf6e3] p-4 text-center shadow-lg">
                    <div className="text-sm font-bold text-[#333]">您的专业版试用已到期</div>
                    <div className="mt-1 text-[13px] text-[#666]">升级专业版方可使用完整功能哦</div>
                    <button className="mt-3 h-8 cursor-pointer rounded-[2px] bg-gradient-to-r from-[#f7c948] to-[#f5a623] px-5 text-sm font-bold text-white">
                        升级专业版
                    </button>
                </div>
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

/* ---------- 纳税统计表 ---------- */

export function LemonTaxReport() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [data, setData] = useState<TaxStatisticsDto | null>(null);
    const exp = useReportExport();
    const [error, setError] = useState<string | null>(null);
    const [tick, setTick] = useState(0);

    const reload = useCallback(() => setTick((t) => t + 1), []);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        let alive = true;
        setError(null);
        new ReportsApi(apiConfig(token))
            .accountsetsAccountSetIdReportsTaxGet({ accountSetId })
            .then((d) => alive && setData(d))
            .catch(async (e) => alive && setError(await apiErrorMessage(e, "纳税统计表加载失败")));
        return () => {
            alive = false;
        };
    }, [token, accountSetId, tick]);

    const accounts = data?.rows ?? [];
    const groups: Array<[string, (name: string) => boolean]> = [
        ["(一) 增值税：", (n) => n.includes("增值税")],
        ["(二) 所得税：", (n) => n.includes("所得税")],
        ["(三) 消费税：", (n) => n.includes("消费税")],
        ["(四) 城建税：", (n) => n.includes("城建税") || n.includes("城市维护建设税")],
    ];
    const used = new Set<number>();
    const rows: Row[] = [];
    let seq = 0;
    let totalCur = 0;
    let totalYtd = 0;
    let totalCurPaid = 0;
    let totalYtdPaid = 0;
    for (const [sec, match] of groups) {
        rows.push({ item: sec, seq: "" });
        for (let i = 0; i < accounts.length; i++) {
            const a = accounts[i]!;
            if (!match(a.name ?? "")) {
                continue;
            }
            used.add(i);
            totalCur += a.currentAmount ?? 0;
            totalYtd += a.yearToDateAmount ?? 0;
            totalCurPaid += a.currentPaidAmount ?? 0;
            totalYtdPaid += a.yearToDatePaidAmount ?? 0;
            seq += 1;
            rows.push(item(`应交${a.name ?? ""}`, String(seq), nf(a.yearToDateAmount), nf(a.currentAmount), { indent: true }));
            rows[rows.length - 1] = {
                ...rows[rows.length - 1]!,
                ytdYoy: pct(a.yearToDateYoyPercent),
                curYoy: pct(a.currentYoyPercent),
            };
            seq += 1;
            rows.push({
                ...item(`已交${a.name ?? ""}`, String(seq), nf(a.yearToDatePaidAmount), nf(a.currentPaidAmount), { indent: true }),
                ytdYoy: "",
                curYoy: "",
            });
        }
    }
    rows.push({ item: "(五) 其他税费：", seq: "" });
    for (let i = 0; i < accounts.length; i++) {
        const a = accounts[i]!;
        if (used.has(i)) {
            continue;
        }
        totalCur += a.currentAmount ?? 0;
        totalYtd += a.yearToDateAmount ?? 0;
        totalCurPaid += a.currentPaidAmount ?? 0;
        totalYtdPaid += a.yearToDatePaidAmount ?? 0;
        seq += 1;
        rows.push(item(`应交${a.name ?? ""}`, String(seq), nf(a.yearToDateAmount), nf(a.currentAmount), { indent: true }));
        rows[rows.length - 1] = {
            ...rows[rows.length - 1]!,
            ytdYoy: pct(a.yearToDateYoyPercent),
            curYoy: pct(a.currentYoyPercent),
        };
        seq += 1;
        rows.push({
            ...item(`已交${a.name ?? ""}`, String(seq), nf(a.yearToDatePaidAmount), nf(a.currentPaidAmount), { indent: true }),
            ytdYoy: "",
            curYoy: "",
        });
    }
    rows.push({ item: "(六) 合计：", seq: "" });
    rows.push({ ...item("应交税费合计", String(seq + 1), nf(totalYtd), nf(totalCur), { indent: true }), ytdYoy: "", curYoy: "" });
    rows.push({ ...item("已交税费合计", String(seq + 2), nf(totalYtdPaid), nf(totalCurPaid), { indent: true }), ytdYoy: "", curYoy: "" });

    const cols: Col[] = [
        { key: "item", title: "项目" },
        { key: "seq", title: "行次", width: "70px" },
        { key: "ytd", title: "金额", group: "本年累计金额", align: "right", neg: true },
        { key: "ytdYoy", title: "同比变化", group: "本年累计金额", align: "right" },
        { key: "cur", title: "金额", group: "本期金额", align: "right", neg: true },
        { key: "curYoy", title: "同比变化", group: "本期金额", align: "right" },
    ];
    return (
        <Page>
            <ReportTool
                text={data?.periodText ?? "加载中…"}
                right={
                    <>
                        <Btn onClick={() => void exp.run("tax", { year: data?.year, month: data?.month })}>导出</Btn>
                        <Btn onClick={() => window.print()}>打印</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} zebra={false} />
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

/* ---------- 财务概要信息 ---------- */

export function LemonFinancialSummary() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [data, setData] = useState<FinancialSummaryDto | null>(null);
    const exp = useReportExport();
    const [error, setError] = useState<string | null>(null);
    const [tick, setTick] = useState(0);

    const reload = useCallback(() => setTick((t) => t + 1), []);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        let alive = true;
        setError(null);
        new ReportsApi(apiConfig(token))
            .accountsetsAccountSetIdReportsFinancialSummaryGet({ accountSetId })
            .then((d) => alive && setData(d))
            .catch(async (e) => alive && setError(await apiErrorMessage(e, "财务概要加载失败")));
        return () => {
            alive = false;
        };
    }, [token, accountSetId, tick]);

    const m = new Map<string, FinancialSummaryRowDto>();
    for (const r of data?.rows ?? []) {
        if (r.item) {
            m.set(r.item, r);
        }
    }
    const get = (name: string) => m.get(name);
    const fill = (name: string, seq: string, label?: string): StmtRow => {
        const r = get(name);
        return {
            ...item(label ?? name, seq, nf(r?.yearToDateAmount), nf(r?.currentAmount), { indent: true }),
            ytdYoy: pct(r?.yearToDateYoyPercent),
            curYoy: pct(r?.currentYoyPercent),
            curMom: pct(r?.currentMomPercent),
        };
    };

    const revenueYtd = get("营业收入")?.yearToDateAmount ?? 0;
    const revenueCur = get("营业收入")?.currentAmount ?? 0;
    const netYtd = get("净利润")?.yearToDateAmount ?? 0;
    const netCur = get("净利润")?.currentAmount ?? 0;
    const expenseYtd = ["管理费用", "销售费用", "财务费用"].reduce((s, n) => s + (get(n)?.yearToDateAmount ?? 0), 0);
    const expenseCur = ["管理费用", "销售费用", "财务费用"].reduce((s, n) => s + (get(n)?.currentAmount ?? 0), 0);
    const taxYtd = get("应交税费")?.yearToDateAmount ?? 0;
    const taxCur = get("应交税费")?.currentAmount ?? 0;

    const rows: Row[] = [
        { item: "(一) 盈利状况：", seq: "" },
        fill("营业收入", "1"),
        fill("净利润", "2"),
        { ...item("营业收入净利率", "3", ratioPct(netYtd, revenueYtd), ratioPct(netCur, revenueCur), { indent: true }), ytdYoy: "", curYoy: "", curMom: "" },
        { item: "(二) 三项费用：", seq: "" },
        fill("管理费用", "4"),
        fill("销售费用", "5"),
        fill("财务费用", "6"),
        { ...item("费用比率", "7", ratioPct(expenseYtd, revenueYtd), ratioPct(expenseCur, revenueCur), { indent: true }), ytdYoy: "", curYoy: "", curMom: "" },
        { item: "(三) 应收应付：", seq: "" },
        fill("应收款", "8"),
        fill("应付款", "9"),
        { item: "(四) 资金状况：", seq: "" },
        fill("资金收入", "10"),
        fill("资金支付", "11"),
        fill("资金收支", "12"),
        { item: "(五) 税金状况：", seq: "" },
        fill("应交税费", "13"),
        { ...item("综合税负率", "14", ratioPct(taxYtd, revenueYtd), ratioPct(taxCur, revenueCur), { indent: true }), ytdYoy: "", curYoy: "", curMom: "" },
    ];

    const cols: Col[] = [
        { key: "item", title: "项目" },
        { key: "seq", title: "行次", width: "70px" },
        { key: "ytd", title: "金额", group: "本年累计金额", align: "right", neg: true },
        { key: "ytdYoy", title: "同比变化", group: "本年累计金额", align: "right" },
        { key: "cur", title: "金额", group: "本期金额", align: "right", neg: true },
        { key: "curYoy", title: "同比变化", group: "本期金额", align: "right" },
        { key: "curMom", title: "环比变化", group: "本期金额", align: "right" },
    ];
    return (
        <Page>
            <ReportTool
                text={data?.periodText ?? "加载中…"}
                right={
                    <>
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <Btn onClick={() => void exp.run("financial-summary", { year: data?.year, month: data?.month })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} zebra={false} />
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}
