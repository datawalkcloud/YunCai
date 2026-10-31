/**
 * 柠檬云报表模块（对应 /Statements/*，已接真实后端 ReportsApi/CustomReportsApi）：
 * 利润表季报、现金流量表（月/季/标准）、部门/项目利润表、
 * 数据透视表（自定义报表）、费用明细表、所有者权益变动表、纳税统计表、财务概要信息。
 * 资产负债表与利润表（月报）与既有实页重叠，复刻版已下线（见 router）。
 *
 * 期间口径：各页期间初始跟随账套当前工作期间（useDefaultAccountingPeriod），
 * 步进器为真实控件——onPrev/onNext 切换期间并携带 year/month（季报为 year/quarter、
 * 权益变动表为 year）重新请求后端；表头显示的期间恒等于实际请求的期间。
 * 报表与账簿只统计已过账凭证。
 */
import { Fragment, useCallback, useEffect, useRef, useState } from "react";
import {
    AppstoreOutlined,
    BarChartOutlined,
    BankOutlined,
    CheckCircleOutlined,
    InfoCircleOutlined,
    LineChartOutlined,
    PieChartOutlined,
    ProjectOutlined,
    ReconciliationOutlined,
    RiseOutlined,
    TeamOutlined,
} from "@ant-design/icons";

import {
    Btn,
    CheckBox,
    Dialog,
    EmptyTip,
    FakeSelect,
    Page,
    Pager,
    PeriodStepper,
    RefreshIcon,
    Select,
    Table,
    TextInput,
    Toast,
    ToolRow,
    WipBtn,
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { shiftPeriod, useDefaultAccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import type { AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { downloadReportExport } from "@/lib/report-download";
import {
    CustomReportsApi,
    ReportsApi,
    type CashFlowDto,
    type CashFlowOpeningDto,
    type CashFlowQuarterDto,
    type CashFlowSupplementDto,
    type CashFlowWorkpaperDto,
    type CustomReportDto,
    type CustomReportRenderDto,
    type EquityChangeRowDto,
    type FinancialSummaryRowDto,
} from "@/apis-gen";

/* ---------- 共用 ---------- */

/** 报表页期间状态：初始跟随账套当前期间，步进后覆盖；stepMonths 为步进月数（月报 1 / 季报 3 / 年度 12）。 */
function useReportPeriod(stepMonths: number) {
    const base = useDefaultAccountingPeriod();
    const [override, setOverride] = useState<AccountingPeriod | null>(null);
    const period = override ?? base;
    const shift = useCallback(
        (dir: number) => setOverride(shiftPeriod(period, dir * stepMonths)),
        [period, stepMonths],
    );
    return { period, onPrev: () => shift(-1), onNext: () => shift(1) };
}

/** 月度展示文案，如「2026年10月」。 */
const monthText = (p: AccountingPeriod) => `${p.year}年${p.month}月`;

/** 期间所属季度（1-4）。 */
const quarterOf = (p: AccountingPeriod) => Math.ceil(p.month / 3);

/** 季度展示文案，如「2026年第4季度」。 */
const quarterText = (p: AccountingPeriod) => `${p.year}年第${quarterOf(p)}季度`;

/** 报表取数：随期间 / 账套 / 手动刷新重新请求，暴露 data、loading、error、reload。 */
function useReportData<T>(
    period: AccountingPeriod,
    errorText: string,
    load: (api: ReportsApi, accountSetId: string) => Promise<T>,
) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [data, setData] = useState<T | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [tick, setTick] = useState(0);
    // 取数闭包每渲染都新建，用最新引用读取，避免把 load 放进依赖数组造成反复请求。
    const loadRef = useRef<{ run: typeof load; errorText: string }>({ run: load, errorText });
    useEffect(() => {
        loadRef.current = { run: load, errorText };
    });

    const reload = useCallback(() => setTick((t) => t + 1), []);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        let alive = true;
        setLoading(true);
        setError(null);
        loadRef.current.run(new ReportsApi(apiConfig(token)), accountSetId)
            .then((d) => {
                if (alive) {
                    setData(d);
                }
            })
            .catch(async (e) => {
                if (alive) {
                    setError(await apiErrorMessage(e, loadRef.current.errorText));
                }
            })
            .finally(() => {
                if (alive) {
                    setLoading(false);
                }
            });
        return () => {
            alive = false;
        };
    }, [token, accountSetId, period.period, tick]);
    return { data, loading, error, reload };
}

/**
 * 弹窗场景的按需取数：show() 打开并请求一次（随当前期间刷新），hide() 关闭但保留上次结果。
 * 取数闭包每渲染都新建，用 ref 读取最新引用，避免放进依赖数组造成反复请求。
 */
function useLazyReport<T>(
    errorText: string,
    load: (api: ReportsApi, accountSetId: string) => Promise<T>,
) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [open, setOpen] = useState(false);
    const [data, setData] = useState<T | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const loadRef = useRef(load);
    useEffect(() => {
        loadRef.current = load;
    });

    const show = useCallback(async () => {
        setOpen(true);
        if (!token || !accountSetId) {
            return;
        }
        setLoading(true);
        setError(null);
        try {
            setData(await loadRef.current(new ReportsApi(apiConfig(token)), accountSetId));
        } catch (e) {
            setError(await apiErrorMessage(e, errorText));
        } finally {
            setLoading(false);
        }
    }, [token, accountSetId, errorText]);

    const hide = useCallback(() => setOpen(false), []);
    return { open, show, hide, data, loading, error };
}

/** 报表工具行：左期间步进器（真实可切换）、右按钮组。 */
function ReportTool({
    text,
    onPrev,
    onNext,
    right,
}: {
    text: string;
    onPrev: () => void;
    onNext: () => void;
    right: React.ReactNode;
}) {
    return <ToolRow left={<PeriodStepper text={text} onPrev={onPrev} onNext={onNext} />} right={right} />;
}

/** 顶部加载中文案。 */
function LoadTip({ text = "加载中…" }: { text?: string }) {
    return <div className="px-4 pb-2 text-[13px] text-slate-400">{text}</div>;
}

/** 错误提示行（加载中与失败均可见，不再吞错）。 */
function ErrorTip({ text }: { text: string }) {
    return <div className="px-4 pb-2 text-[13px] text-[#ef4444]">{text}</div>;
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

/** 行项目构造器：indent 缩进、check 成功绿勾（#10b981）、neg 负数红字由列渲染处理。 */
type StmtRow = {
    item: React.ReactNode;
    seq: string;
    ytd?: string;
    cur?: string;
    ly?: string;
    ytdYoy?: string;
    curYoy?: string;
    curMom?: string;
};

function item(
    label: string,
    seq: string,
    ytd = "",
    cur = "",
    opts: { check?: boolean; indent?: boolean } = {},
    ly = "",
): StmtRow {
    return {
        item: (
            <span className={opts.indent ? "pl-6" : ""}>
                {opts.check && <CheckCircleOutlined className="mr-1 text-[14px]" style={{ color: "#10b981" }} />}
                {label}
            </span>
        ),
        seq,
        ytd,
        cur,
        ly,
    };
}

/** 行次列：浅灰弱化数字，避免与金额抢视线。 */
const SEQ_COL: Col = {
    key: "seq",
    title: "行次",
    width: "70px",
    render: (row) => <span className="text-slate-400">{String(row.seq ?? "")}</span>,
};

const INCOME_COLS = (curTitle: string, withMark = true, withLastYear = false): Col[] => [
    { key: "item", title: "项目" },
    SEQ_COL,
    ...(withMark ? [{ key: "mark", title: "≈", width: "65px" } as Col] : []),
    { key: "ytd", title: "本年累计金额", align: "right", width: "160px", neg: true },
    ...(withLastYear ? [{ key: "ly", title: "上年累计金额", align: "right", width: "160px", neg: true } as Col] : []),
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

type IncomeLineValues = { current: number; yearToDate: number; lastYear: number };

/** 由后端明细行组装利润表展示行（含营业利润/利润总额/净利润计算行），行次沿用柠檬云编号。 */
function buildIncomeRows(
    lines?: Array<{ line?: number; current?: number; yearToDate?: number; lastYear?: number }>,
): StmtRow[] {
    const byLine = new Map<number, IncomeLineValues>();
    for (const l of lines ?? []) {
        if (l?.line != null) {
            byLine.set(l.line, {
                current: l.current ?? 0,
                yearToDate: l.yearToDate ?? 0,
                lastYear: l.lastYear ?? 0,
            });
        }
    }
    const v = (line: number, key: keyof IncomeLineValues): number => byLine.get(line)?.[key] ?? 0;
    const s = (line: number, key: keyof IncomeLineValues): string => nf(byLine.get(line)?.[key]);
    const ly = (line: number): string => nf(byLine.get(line)?.lastYear);
    const sum = (keys: number[], key: keyof IncomeLineValues): number => keys.reduce((acc, n) => acc + v(n, key), 0);

    const revenueLines = [1, 8, 9, 11, 14];
    const expenseLines = [2, 3, 4, 5, 6, 7, 12, 13];
    const rows: StmtRow[] = [
        item("一、营业收入", "1", s(1, "yearToDate"), s(1, "current"), {}, ly(1)),
        item("减：营业成本", "2", s(2, "yearToDate"), s(2, "current"), { indent: true }, ly(2)),
        item("税金及附加", "3", s(3, "yearToDate"), s(3, "current"), { check: true, indent: true }, ly(3)),
        item("销售费用", "11", s(4, "yearToDate"), s(4, "current"), { check: true, indent: true }, ly(4)),
        item("管理费用", "14", s(5, "yearToDate"), s(5, "current"), { check: true, indent: true }, ly(5)),
        item("财务费用", "18", s(6, "yearToDate"), s(6, "current"), { check: true, indent: true }, ly(6)),
        item("加：投资收益（亏损以“-”号填列）", "20", s(9, "yearToDate"), s(9, "current"), { indent: true }, ly(9)),
    ];
    const netOf = (key: keyof IncomeLineValues) => sum(revenueLines, key) - sum(expenseLines, key);
    rows.push(
        item(
            "二、营业利润（亏损以“-”号填列）",
            "21",
            nf(netOf("yearToDate")),
            nf(netOf("current")),
            {},
            nf(netOf("lastYear")),
        ),
    );
    rows.push(item("加：营业外收入", "22", s(16, "yearToDate"), s(16, "current"), { check: true, indent: true }, ly(16)));
    rows.push(item("减：营业外支出", "24", s(17, "yearToDate"), s(17, "current"), { check: true, indent: true }, ly(17)));
    const totalOf = (key: keyof IncomeLineValues) => netOf(key) + v(16, key) - v(17, key);
    rows.push(
        item(
            "三、利润总额（亏损总额以“-”号填列）",
            "30",
            nf(totalOf("yearToDate")),
            nf(totalOf("current")),
            {},
            nf(totalOf("lastYear")),
        ),
    );
    rows.push(item("减：所得税费用", "31", s(19, "yearToDate"), s(19, "current"), { indent: true }, ly(19)));
    rows.push(
        item(
            "四、净利润（净亏损以“-”号填列）",
            "32",
            nf(totalOf("yearToDate") - v(19, "yearToDate")),
            nf(totalOf("current") - v(19, "current")),
            {},
            nf(totalOf("lastYear") - v(19, "lastYear")),
        ),
    );
    return rows;
}

export function LemonIncomeQuarter() {
    const { period, onPrev, onNext } = useReportPeriod(3);
    const exp = useReportExport();
    const [showLastYear, setShowLastYear] = useState(false);
    const { data, loading, error, reload } = useReportData(
        period,
        "利润表季报加载失败",
        (api, accountSetId) =>
            api.accountsetsAccountSetIdReportsIncomeQuarterGet({
                accountSetId,
                year: period.year,
                quarter: quarterOf(period),
            }),
    );

    return (
        <Page title="利润表季报" subtitle="按季度汇总收入、费用与利润，仅统计已过账凭证。" icon={BarChartOutlined}>
            <ReportTool
                text={quarterText(period)}
                onPrev={onPrev}
                onNext={onNext}
                right={
                    <>
                        <CheckBox label="显示本年所有季度" />
                        <CheckBox
                            label="显示上年累计金额"
                            checked={showLastYear}
                            onChange={setShowLastYear}
                        />
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <Btn dropdown onClick={() => void exp.run("income-quarter", { year: period.year, quarter: quarterOf(period) })}>导出</Btn>
                        <WipBtn>微信分享</WipBtn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {loading && <LoadTip />}
            {error && <ErrorTip text={error} />}
            <div className="flex-1 px-4">
                <Table cols={INCOME_COLS("本季金额", false, showLastYear)} rows={buildIncomeRows(data?.lines)} zebra={false} />
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

/* ---------- 现金流量表（月/季/标准） ---------- */

/**
 * 现金流量表行次：与企业会计准则主表（财政部 2007 版式）一一对应，序号即后端标准行次，
 * 与柠檬云「现金流量表」完全一致（35 个行次 + 3 个活动小标题 = 38 行）。
 */
const CF_LINE_MAP: Record<string, number> = Object.fromEntries(
    Array.from({ length: 35 }, (_, i) => [String(i + 1), i + 1]),
);

function CashFlowPage({
    variant,
}: {
    variant: "month" | "quarter" | "standard";
}) {
    const { period, onPrev, onNext } = useReportPeriod(variant === "quarter" ? 3 : 1);
    const exp = useReportExport();
    /** 直接法（主表）/ 公式法（补充资料间接法）切换；季报不提供该切换。 */
    const [method, setMethod] = useState<"direct" | "indirect">("direct");
    const [showLastYear, setShowLastYear] = useState(false);
    // 月报与标准表请求同一端点 /reports/cash-flow（后端未提供独立标准口径），季报走 /reports/cash-flow-quarter。
    const { data, loading, error, reload } = useReportData<CashFlowDto | CashFlowQuarterDto>(
        period,
        "现金流量表加载失败",
        variant === "quarter"
            ? (api, accountSetId) =>
                  api.accountsetsAccountSetIdReportsCashFlowQuarterGet({
                      accountSetId,
                      year: period.year,
                      quarter: quarterOf(period),
                  })
            : (api, accountSetId) =>
                  api.accountsetsAccountSetIdReportsCashFlowGet({
                      accountSetId,
                      year: period.year,
                      month: period.month,
                  }),
    );

    // 补充资料只有月度口径（季报页不取数）。
    const {
        data: supplement,
        error: supplementError,
    } = useReportData<CashFlowSupplementDto | null>(
        period,
        "现金流量表补充资料加载失败",
        variant === "quarter"
            ? async () => null
            : (api, accountSetId) =>
                  api.accountsetsAccountSetIdReportsCashFlowSupplementGet({
                      accountSetId,
                      year: period.year,
                      month: period.month,
                  }),
    );

    // 报表底稿 / 期初余额为弹窗场景：打开时才取数，避免每次翻期间都多打请求。
    const workpaper = useLazyReport<CashFlowWorkpaperDto>("现金流量表底稿加载失败", (api, accountSetId) =>
        api.accountsetsAccountSetIdReportsCashFlowWorkpaperGet({
            accountSetId,
            year: period.year,
            month: period.month,
        }),
    );
    const opening = useLazyReport<CashFlowOpeningDto>("期初现金余额加载失败", (api, accountSetId) =>
        api.accountsetsAccountSetIdReportsCashFlowOpeningGet({
            accountSetId,
            year: period.year,
            month: period.month,
        }),
    );
    const wp = workpaper.data;

    const lines = data?.lines ?? [];
    const byLine = new Map<number, { current: number; yearToDate: number; lastYear: number }>();
    for (const l of lines) {
        if (l?.line != null) {
            byLine.set(l.line, {
                current: l.current ?? 0,
                yearToDate: l.yearToDate ?? 0,
                lastYear: l.lastYear ?? 0,
            });
        }
    }
    const cell = (seq: string, key: "current" | "yearToDate" | "lastYear"): string => {
        const std = CF_LINE_MAP[seq];
        return std == null ? "" : nf(byLine.get(std)?.[key]);
    };

    const mk = (label: string, seq: string) =>
        item(label, seq, cell(seq, "yearToDate"), cell(seq, "current"), { indent: true }, cell(seq, "lastYear"));
    // 行次与文案严格对齐柠檬云「现金流量表」（企业会计准则 2007 版式，38 行）
    const rows: StmtRow[] = [
        item("一、经营活动产生的现金流量：", ""),
        mk("销售商品、提供劳务收到的现金", "1"),
        mk("收到的税费返还", "2"),
        mk("收到其他与经营活动有关的现金", "3"),
        mk("经营活动现金流入小计", "4"),
        mk("购买商品、接受劳务支付的现金", "5"),
        mk("支付给职工以及为职工支付的现金", "6"),
        mk("支付的各项税费", "7"),
        mk("支付其他与经营活动有关的现金", "8"),
        mk("经营活动现金流出小计", "9"),
        mk("经营活动产生的现金流量净额", "10"),
        item("二、投资活动产生的现金流量：", ""),
        mk("收回投资收到的现金", "11"),
        mk("取得投资收益收到的现金", "12"),
        mk("处置固定资产、无形资产和其他长期资产收回的现金净额", "13"),
        mk("处置子公司及其他营业单位收到的现金净额", "14"),
        mk("收到其他与投资活动有关的现金", "15"),
        mk("投资活动现金流入小计", "16"),
        mk("购建固定资产、无形资产和其他长期资产支付的现金", "17"),
        mk("投资支付的现金", "18"),
        mk("取得子公司及其他营业单位支付的现金净额", "19"),
        mk("支付其他与投资活动有关的现金", "20"),
        mk("投资活动现金流出小计", "21"),
        mk("投资活动产生的现金流量净额", "22"),
        item("三、筹资活动产生的现金流量：", ""),
        mk("吸收投资收到的现金", "23"),
        mk("取得借款收到的现金", "24"),
        mk("收到其他与筹资活动有关的现金", "25"),
        mk("筹资活动现金流入小计", "26"),
        mk("偿还债务支付的现金", "27"),
        mk("分配股利、利润或偿付利息支付的现金", "28"),
        mk("支付其他与筹资活动有关的现金", "29"),
        mk("筹资活动现金流出小计", "30"),
        mk("筹资活动产生的现金流量净额", "31"),
        mk("四、汇率变动对现金及现金等价物的影响", "32"),
        mk("五、现金及现金等价物净增加额", "33"),
        mk("加：期初现金及现金等价物的余额", "34"),
        mk("六、期末现金及现金等价物余额", "35"),
    ];

    const cols: Col[] = [
        { key: "item", title: "项目" },
        SEQ_COL,
        { key: "ytd", title: "本年累计金额", align: "right", width: "160px", neg: true },
        ...(showLastYear
            ? [{ key: "ly", title: "上年累计金额", align: "right", width: "160px", neg: true } as Col]
            : []),
        { key: "cur", title: variant === "quarter" ? "本季金额" : "本期金额", align: "right", width: "140px", neg: true },
    ];

    /* 补充资料（间接法，仅标准现金流量表展示）：按段落插入小标题行，合计行加底色。 */
    const SUPPLEMENT_GROUPS: Record<number, string> = {
        1: "1.将净利润调节为经营活动现金流量：",
        2: "2.不涉及现金收支的重大投资和筹资活动",
        3: "3.现金及现金等价物净变动情况",
    };
    const supplementRows: Row[] = [];
    let lastGroup = 0;
    if (variant === "standard" && (supplement?.lines ?? []).length > 0) {
        supplementRows.push({ item: <span className="font-medium">补充资料</span>, seq: "" });
    }
    for (const l of variant === "standard" ? supplement?.lines ?? [] : []) {
        const group = l.group ?? 1;
        if (group !== lastGroup) {
            supplementRows.push({
                item: <span className="font-medium text-slate-600">{SUPPLEMENT_GROUPS[group] ?? ""}</span>,
                seq: "",
            });
            lastGroup = group;
        }
        supplementRows.push({
            item: <span className={l.isTotal ? "font-medium" : "pl-6"}>{l.name ?? ""}</span>,
            seq: String(l.line ?? ""),
            ytd: nf(l.yearToDate),
            cur: nf(l.current),
            __style: l.isTotal ? { background: "#f8fafc", fontWeight: 600 } : undefined,
        });
    }
    const supplementCols: Col[] = [
        { key: "item", title: "项目" },
        SEQ_COL,
        { key: "ytd", title: "本年累计金额", align: "right", width: "160px", neg: true },
        { key: "cur", title: "本期金额", align: "right", width: "140px", neg: true },
    ];
    const supplementNet = supplement?.lines?.find((l) => l.line === 15)?.current ?? 0;
    const reconciled = (supplement?.difference ?? 0) === 0;

    /* 报表底稿：逐凭证分摊结果，用于解释主表行次金额的来源。 */
    const wpRows: Row[] = (wp?.rows ?? []).map((r, i) => ({
        idx: String(i + 1),
        date: r.date ? new Date(r.date).toISOString().substring(0, 10) : "",
        voucher: r.voucherText ?? "",
        summary: r.summary ?? "",
        account: `${r.accountCode ?? ""} ${r.accountName ?? ""}`.trim(),
        line: r.line == null ? "" : `${r.line} ${r.lineName ?? ""}`,
        dir: r.inflow ? "流入" : "流出",
        amount: nf(r.amount),
    }));
    const wpCols: Col[] = [
        { key: "idx", title: "序号", width: "64px" },
        { key: "date", title: "日期", width: "100px" },
        { key: "voucher", title: "凭证字号", width: "100px" },
        { key: "summary", title: "摘要", width: "180px" },
        { key: "account", title: "对手方科目" },
        { key: "line", title: "归入行次", width: "200px" },
        { key: "dir", title: "方向", width: "64px" },
        { key: "amount", title: "金额", align: "right", width: "120px" },
    ];
    const wpNet = (wp?.inflowTotal ?? 0) - (wp?.outflowTotal ?? 0);

    /* 期初余额：按现金科目列示期初/期末，合计与主表期初、期末两行一致。 */
    const openingRows: Row[] = (opening.data?.rows ?? []).map((r) => ({
        item: `${r.accountCode ?? ""} ${r.accountName ?? ""}`.trim(),
        open: nf(r.opening),
        close: nf(r.closing),
    }));
    if (opening.data) {
        openingRows.push({
            item: <span className="font-medium">合计</span>,
            open: nf(opening.data.openingTotal),
            close: nf(opening.data.closingTotal),
            __style: { background: "#f8fafc", fontWeight: 600 },
        });
    }
    const openingCols: Col[] = [
        { key: "item", title: "现金科目" },
        { key: "open", title: "期初余额", align: "right", width: "160px", neg: true },
        { key: "close", title: "期末余额", align: "right", width: "160px", neg: true },
    ];
    return (
        <Page
            title={variant === "standard" ? "标准现金流量表" : variant === "quarter" ? "现金流量表季报" : "现金流量表"}
            subtitle="按直接法简化工作底稿归集现金收付，仅统计已过账凭证。"
            icon={LineChartOutlined}
        >
            <ToolRow
                left={
                    <>
                        {variant === "standard" && <FakeSelect text="月报" className="w-[90px]" />}
                        <PeriodStepper text={variant === "quarter" ? quarterText(period) : monthText(period)} onPrev={onPrev} onNext={onNext} />
                        {variant === "month" && (
                            <Select
                                value={method}
                                onChange={(v) => setMethod(v === "indirect" ? "indirect" : "direct")}
                                options={[
                                    { value: "direct", label: "直接法" },
                                    { value: "indirect", label: "公式法" },
                                ]}
                                className="w-[110px]"
                            />
                        )}
                        {variant === "month" && (
                            <span
                                className="flex items-center gap-1 text-[13px] text-slate-400"
                                title="直接法/公式法口径与取数说明见本页表格上方文案"
                            >
                                <InfoCircleOutlined className="text-[14px]" /> 帮助
                            </span>
                        )}
                        {variant === "standard" && (
                            <span
                                className="flex items-center gap-1 text-[13px] text-slate-400"
                                title="标准表与「现金流量表」读取同一接口，差异仅在展示行次，详见表格上方口径说明"
                            >
                                <InfoCircleOutlined className="text-[14px]" /> 报表说明
                            </span>
                        )}
                    </>
                }
                right={
                    <>
                        <CheckBox
                            label="显示上年累计金额"
                            checked={showLastYear}
                            onChange={setShowLastYear}
                        />
                        {variant !== "quarter" && <Btn onClick={() => void opening.show()}>期初</Btn>}
                        {variant === "month" && <Btn onClick={() => void workpaper.show()}>报表底稿</Btn>}
                        {variant === "standard" && <WipBtn>现金流量调整</WipBtn>}
                        {variant === "standard" ? (
                            <>
                                <Btn dropdown onClick={() => void exp.run("cash-flow", { year: period.year, month: period.month })}>导出</Btn>
                                <Btn dropdown onClick={() => window.print()}>打印</Btn>
                                <WipBtn>分享</WipBtn>
                            </>
                        ) : (
                            <>
                                <Btn dropdown onClick={() => window.print()}>打印</Btn>
                                <Btn
                                    dropdown
                                    onClick={() =>
                                        void exp.run(
                                            variant === "quarter" ? "cash-flow-quarter" : "cash-flow",
                                            variant === "quarter"
                                                ? { year: period.year, quarter: quarterOf(period) }
                                                : { year: period.year, month: period.month },
                                        )
                                    }
                                >
                                    导出
                                </Btn>
                                <WipBtn>微信分享</WipBtn>
                            </>
                        )}
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {/* 口径说明：月报与标准表同源，差异仅在展示行次，如实标注来源 */}
            {variant !== "quarter" && (
                <div className="px-4 pb-2 text-[12.5px] leading-5 text-slate-400">
                    {variant === "standard"
                        ? "口径说明：本表与「现金流量表」读取同一后端接口 /reports/cash-flow（直接法简化工作底稿，仅统计已过账凭证），按企业会计准则主表行次 1-35 排列；表下「补充资料」走间接法（/reports/cash-flow-supplement），行次 36-60。"
                        : "口径说明：主表取 /reports/cash-flow 直接法简化工作底稿（仅统计已过账凭证），按企业会计准则主表行次 1-35 展示（与柠檬云一致）；「报表底稿」逐凭证列出分摊明细，可核对主表行次金额的来源。"}
                </div>
            )}
            {loading && <LoadTip />}
            {error && <ErrorTip text={error} />}
            {supplementError && <ErrorTip text={supplementError} />}
            <div className="flex-1 overflow-auto px-4 pb-4">
                {method === "direct" && <Table cols={cols} rows={rows} zebra={false} />}
                {variant === "standard" && (
                    <div className={method === "direct" ? "mt-6" : ""}>
                        <div className="mb-2 flex items-center gap-2 text-[13px] font-medium text-slate-600">
                            补充资料
                            <span className="text-[12.5px] font-normal text-slate-400">
                                （间接法：将净利润调节为经营活动现金流量）
                            </span>
                        </div>
                        <Table cols={supplementCols} rows={supplementRows} zebra={false} />
                        <div className={`py-2 text-[12.5px] ${reconciled ? "text-slate-400" : "text-amber-600"}`}>
                            勾稽：补充资料调节净额 {nf(supplementNet)} 元，主表「经营活动产生的现金流量净额」{" "}
                            {nf(supplement?.mainTableOperatingNet ?? 0)} 元，差异 {nf(supplement?.difference ?? 0)} 元
                            {reconciled ? "（完全勾稽）。" : "（口径差异，多因收支归类不同，可结合明细核对）。"}
                        </div>
                    </div>
                )}
            </div>
            {workpaper.open && (
                <Dialog
                    title={`现金流量表底稿（${wp?.periodText ?? monthText(period)}）`}
                    width={1000}
                    onClose={workpaper.hide}
                    footer={
                        <Btn variant="primary" className="w-[96px]" onClick={workpaper.hide}>
                            关闭
                        </Btn>
                    }
                >
                    <div className="mb-2 flex flex-wrap gap-x-6 gap-y-1 text-[12.5px] text-slate-500">
                        <span>分摊行数 {wp?.rows?.length ?? 0}</span>
                        <span>流入合计 {nf(wp?.inflowTotal ?? 0)} 元</span>
                        <span>流出合计 {nf(wp?.outflowTotal ?? 0)} 元</span>
                        <span>净额 {nf(wpNet)} 元</span>
                    </div>
                    {workpaper.loading && <LoadTip />}
                    {workpaper.error && <ErrorTip text={workpaper.error} />}
                    <div className="max-h-[420px] overflow-auto">
                        <Table
                            cols={wpCols}
                            rows={wpRows}
                            zebra={false}
                            empty={<EmptyTip text={workpaper.loading ? "加载中…" : "本期没有现金收付凭证"} />}
                        />
                    </div>
                    <div className="mt-3 text-[12.5px] leading-5 text-slate-400">
                        口径：现金科目取 1001 库存现金 / 1002 银行存款 / 1012 其他货币资金；每张凭证的现金净收付按非现金分录金额比例分摊到对手方科目，
                        再按对手方科目归类到标准主表行次（仅统计已过账凭证）；底稿小计与主表「经营活动产生的现金流量净额」同源。
                    </div>
                </Dialog>
            )}
            {opening.open && (
                <Dialog
                    title={`期初现金余额（${opening.data?.periodText ?? monthText(period)}）`}
                    width={620}
                    onClose={opening.hide}
                    footer={
                        <Btn variant="primary" className="w-[96px]" onClick={opening.hide}>
                            关闭
                        </Btn>
                    }
                >
                    {opening.loading && <LoadTip />}
                    {opening.error && <ErrorTip text={opening.error} />}
                    <Table
                        cols={openingCols}
                        rows={openingRows}
                        zebra={false}
                        empty={<EmptyTip text={opening.loading ? "加载中…" : "暂无现金科目余额"} />}
                    />
                    <div className="mt-3 text-[12.5px] leading-5 text-slate-400">
                        口径：期初余额为期间起始日前的累计余额（含科目启用期初），期末余额为期初加本期净增加额；
                        合计分别对应主表「期初现金及现金等价物余额」「期末现金及现金等价物余额」两行。
                    </div>
                </Dialog>
            )}
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
    const { period, onPrev, onNext } = useReportPeriod(1);
    const exp = useReportExport();
    const { data, loading, error, reload } = useReportData(
        period,
        "部门利润表加载失败",
        (api, accountSetId) =>
            api.accountsetsAccountSetIdReportsDepartmentIncomeGet({
                accountSetId,
                year: period.year,
                month: period.month,
            }),
    );

    const th = "border-b border-slate-200 bg-[#f8fafc] px-3 py-2 text-[13px] font-medium text-slate-500";
    const td = "border-b border-slate-100 px-3 py-2.5 text-[13px] text-slate-700";
    const departments = data?.departments ?? [];
    const rows = data?.rows ?? [];
    return (
        <Page title="部门利润表" subtitle="按部门辅助核算归集收入与费用，仅统计已过账凭证。" icon={TeamOutlined}>
            <ToolRow
                left={
                    <>
                        <PeriodStepper text={monthText(period)} onPrev={onPrev} onNext={onNext} />
                        <span
                            className="flex items-center gap-1 text-[13px] text-slate-400"
                            title="按部门辅助核算归集收入与费用，仅统计已过账凭证；完整口径见导出报表"
                        >
                            <InfoCircleOutlined className="text-[14px]" /> 帮助
                        </span>
                    </>
                }
                right={
                    <>
                        <Btn dropdown onClick={() => void exp.run("department-income", { year: period.year, month: period.month })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {loading && <LoadTip />}
            {error && <ErrorTip text={error} />}
            <div className="flex-1 overflow-x-auto px-4">
                <table className="border-collapse text-[13px] whitespace-nowrap">
                    <thead>
                        <tr>
                            <th rowSpan={2} className={`${th} text-left`}>
                                项目
                            </th>
                            <th rowSpan={2} className={`${th} w-[60px] text-center`}>
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
                        {rows.length === 0 && !loading && !error && (
                            <tr>
                                <td colSpan={2 + departments.length * 2} className="py-10 text-center text-[13px] text-slate-400">
                                    暂无数据
                                </td>
                            </tr>
                        )}
                        {rows.map((r) => (
                            <tr key={r.line}>
                                <td className={td}>{r.item}</td>
                                <td className={`${td} text-center text-slate-400`}>{r.line}</td>
                                {departments.map((d, i) => (
                                    <Fragment key={d.id}>
                                        <td className={`${td} text-right tabular-nums`}>{nf(r.amounts?.[i]?.current)}</td>
                                        <td className={`${td} text-right tabular-nums`}>{nf(r.amounts?.[i]?.yearToDate)}</td>
                                    </Fragment>
                                ))}
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
            <Pager total={rows.length} />
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

/* ---------- 项目利润表 ---------- */

export function LemonProjectIncome() {
    const { period, onPrev, onNext } = useReportPeriod(1);
    const exp = useReportExport();
    const { data, loading, error, reload } = useReportData(
        period,
        "项目利润表加载失败",
        (api, accountSetId) =>
            api.accountsetsAccountSetIdReportsProjectIncomeGet({
                accountSetId,
                year: period.year,
                month: period.month,
            }),
    );
    const [tipClosed, setTipClosed] = useState(false);

    const rows = (data?.rows ?? []).map((r) => ({
        item: r.name ?? "",
        seq: String(r.line ?? ""),
        cur: nf(r.current),
        ytd: nf(r.yearToDate),
    }));
    const cols: Col[] = [
        { key: "item", title: "项目" },
        SEQ_COL,
        { key: "cur", title: "本期金额", align: "right", width: "160px", neg: true },
        { key: "ytd", title: "本年累计金额", align: "right", width: "160px", neg: true },
    ];
    const showTip = rows.length === 0 && !loading && !error && !tipClosed;
    return (
        <Page title="项目利润表" subtitle="按项目辅助核算归集收入与费用，仅统计已过账凭证。" icon={ProjectOutlined}>
            <ReportTool
                text={monthText(period)}
                onPrev={onPrev}
                onNext={onNext}
                right={
                    <>
                        <Btn dropdown onClick={() => void exp.run("project-income", { year: period.year, month: period.month })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {loading && <LoadTip />}
            {error && <ErrorTip text={error} />}
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} zebra={false} empty={<EmptyTip />} />
            </div>
            <Pager total={rows.length} />
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
            {/* 提示弹窗：无项目档案时提示 */}
            {showTip && (
                <Dialog
                    title="提示"
                    onClose={() => setTipClosed(true)}
                    footer={
                        <Btn variant="primary" className="w-[96px]" onClick={() => setTipClosed(true)}>
                            确定
                        </Btn>
                    }
                >
                    <div className="py-6 text-center">暂无项目信息，请先设置项目！</div>
                </Dialog>
            )}
        </Page>
    );
}

/* ---------- 数据透视表（自定义报表） ---------- */

export function LemonPivot() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const defaultPeriod = useDefaultAccountingPeriod();
    const [reports, setReports] = useState<CustomReportDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [checked, setChecked] = useState<Set<string>>(new Set());
    // 介绍弹窗默认不弹，只在点击工具行「说明」时打开。
    const [guideOpen, setGuideOpen] = useState(false);
    const [creating, setCreating] = useState(false);
    const [form, setForm] = useState({ code: "", name: "", valueType: 1 });
    // 渲染目标（报表 id + 年度）与渲染结果；年度步进会重新请求 /render（后端支持 year 参数）。
    const [renderTarget, setRenderTarget] = useState<{ id: string; year: number } | null>(null);
    const [rendered, setRendered] = useState<CustomReportRenderDto | null>(null);
    const [renderLoading, setRenderLoading] = useState(false);
    const [renderError, setRenderError] = useState<string | null>(null);
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

    useEffect(() => {
        if (!renderTarget || !token || !accountSetId) {
            return;
        }
        let alive = true;
        setRenderLoading(true);
        setRenderError(null);
        new CustomReportsApi(apiConfig(token))
            .accountsetsAccountSetIdCustomReportsIdRenderGet({ accountSetId, id: renderTarget.id, year: renderTarget.year })
            .then((d) => {
                if (alive) {
                    setRendered(d);
                }
            })
            .catch(async (e) => {
                if (alive) {
                    setRenderError(await apiErrorMessage(e, "渲染失败"));
                }
            })
            .finally(() => {
                if (alive) {
                    setRenderLoading(false);
                }
            });
        return () => {
            alive = false;
        };
    }, [renderTarget, token, accountSetId]);

    // 操作反馈轻提示 2.4s 自动消失（沿用原 ToastBar 行为）。
    useEffect(() => {
        if (!toast) {
            return;
        }
        const timer = setTimeout(() => setToast(null), 2400);
        return () => clearTimeout(timer);
    }, [toast]);

    if (!token || !accountSetId) {
        return (
            <Page title="数据透视表" subtitle="自定义报表：按年度查看科目发生额或余额。" icon={AppstoreOutlined}>
                <ErrorTip text="缺少登录态或账套上下文，无法加载数据透视表。" />
            </Page>
        );
    }

    const create = async () => {
        try {
            await new CustomReportsApi(apiConfig(token)).accountsetsAccountSetIdCustomReportsPost({
                accountSetId,
                saveCustomReportRequest: { code: form.code, name: form.name, valueType: form.valueType },
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
        const api = new CustomReportsApi(apiConfig(token));
        const results = await Promise.allSettled(ids.map((id) => api.accountsetsAccountSetIdCustomReportsIdDelete({ accountSetId, id })));
        const failed = results.filter((r) => r.status === "rejected").length;
        setChecked(new Set());
        setToast(failed === 0 ? "删除成功" : `${failed} 个删除失败`);
        await reload();
    };

    const copy = async (r: CustomReportDto) => {
        try {
            await new CustomReportsApi(apiConfig(token)).accountsetsAccountSetIdCustomReportsPost({
                accountSetId,
                saveCustomReportRequest: {
                    code: `${r.code ?? "REPORT"}-C${reports.length + 1}`.slice(0, 50),
                    name: `${r.name ?? "报表"}（副本）`,
                    valueType: r.valueType ?? 1,
                },
            });
            setToast("复制成功");
            await reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "复制失败"));
        }
    };

    const view = (r: CustomReportDto) => {
        setRendered(null);
        setRenderTarget({ id: r.id ?? "", year: defaultPeriod.year });
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
                <span className="flex gap-2 text-[13px] text-[#2f6bff]">
                    <span
                        className="cursor-pointer hover:underline"
                        onClick={() => {
                            const r = reports.find((x) => x.id === row.id);
                            if (r) {
                                view(r);
                            }
                        }}
                    >
                        查看
                    </span>
                    <span className="cursor-pointer hover:underline" onClick={() => void del([String(row.id)])}>
                        删除
                    </span>
                </span>
            ),
        },
    ];
    const renderName = reports.find((r) => r.id === renderTarget?.id)?.name ?? rendered?.name ?? "";
    return (
        <Page title="数据透视表" subtitle="自定义报表：将科目、辅助核算等字段按取值口径逐月透视，年度步进实时取数。" icon={AppstoreOutlined}>
            <ToolRow
                left={
                    <>
                        <Btn variant="primary" onClick={() => setCreating(true)}>
                            新增
                        </Btn>
                        <Btn variant="link" onClick={() => setGuideOpen(true)}>
                            说明
                        </Btn>
                        <Btn
                            onClick={() => {
                                if (checked.size !== 1) {
                                    setToast("请先勾选恰好一个报表再复制");
                                    return;
                                }
                                const r = reports.find((x) => x.id === [...checked][0]);
                                if (r) {
                                    void copy(r);
                                }
                            }}
                        >
                            复制
                        </Btn>
                        <Btn onClick={() => checked.size > 0 && void del([...checked])}>批量删除</Btn>
                    </>
                }
                right={<ReloadButton onClick={() => void reload()} />}
            />
            {loading && <LoadTip />}
            {error && <ErrorTip text={error} />}
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
                    empty={<EmptyTip text={loading ? "加载中…" : "暂无数据"} />}
                />
            </div>
            <Pager total={reports.length} />
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            {/* 介绍弹窗：默认不弹，点工具行「说明」打开 */}
            {guideOpen && (
                <Dialog
                    title="数据透视表"
                    width={640}
                    onClose={() => setGuideOpen(false)}
                    footer={
                        <Btn variant="primary" className="w-[110px]" onClick={() => setGuideOpen(false)}>
                            去看看
                        </Btn>
                    }
                >
                    <div className="flex items-center gap-8">
                        <div className="flex h-[160px] w-[240px] shrink-0 items-center justify-center rounded-lg bg-[#fafbfd] text-[13px] text-slate-400">
                            插画
                        </div>
                        <div className="flex flex-col gap-3 leading-6 text-slate-500">
                            <span>
                                您可以通过数据透视表将会计科目、辅助核算类别、取值金额等字段通过拖拽方式，自定义会计区间来查看不同级别的明细数据；
                            </span>
                            <span>
                                根据你自己的需求对报表数据进行自定义排列组合布局，生成属于您的专属数据报表
                            </span>
                        </div>
                    </div>
                </Dialog>
            )}
            {/* 新增弹窗 */}
            {creating && (
                <Dialog
                    title="新增数据透视表"
                    onClose={() => setCreating(false)}
                    footer={
                        <>
                            <Btn variant="primary" className="w-[96px]" onClick={() => void create()}>
                                保存
                            </Btn>
                            <Btn className="w-[96px]" onClick={() => setCreating(false)}>
                                取消
                            </Btn>
                        </>
                    }
                >
                    <div className="flex flex-col gap-4">
                        <label className="flex items-center gap-3">
                            <span className="w-[70px] text-right">报表编码</span>
                            <TextInput
                                className="w-[220px]"
                                value={form.code}
                                onChange={(v) => setForm({ ...form, code: v })}
                                placeholder="如 ZCZK2026"
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[70px] text-right">报表名称</span>
                            <TextInput
                                className="w-[220px]"
                                value={form.name}
                                onChange={(v) => setForm({ ...form, name: v })}
                                placeholder="如 资产负债透视"
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[70px] text-right">取值口径</span>
                            <Select
                                className="w-[220px]"
                                value={String(form.valueType)}
                                onChange={(v) => setForm({ ...form, valueType: Number(v) })}
                                options={[
                                    { value: "1", label: "期末余额" },
                                    { value: "2", label: "期间发生额" },
                                ]}
                            />
                        </label>
                    </div>
                </Dialog>
            )}
            {/* 渲染结果弹窗：科目 × 12 个月，年度步进真实传给 /render */}
            {renderTarget && (
                <Dialog
                    title={`${renderName}（${renderTarget.year} 年 · ${rendered?.valueType === 2 ? "期间发生额" : "期末余额"}）`}
                    width={1000}
                    onClose={() => setRenderTarget(null)}
                    footer={
                        <Btn variant="primary" className="w-[96px]" onClick={() => setRenderTarget(null)}>
                            关闭
                        </Btn>
                    }
                >
                    <div className="mb-3 flex justify-end">
                        <PeriodStepper
                            text={`${renderTarget.year}年`}
                            onPrev={() => setRenderTarget({ ...renderTarget, year: renderTarget.year - 1 })}
                            onNext={() => setRenderTarget({ ...renderTarget, year: renderTarget.year + 1 })}
                        />
                    </div>
                    {renderLoading && <LoadTip />}
                    {renderError && <ErrorTip text={renderError} />}
                    <div className="max-h-[56vh] overflow-auto">
                        <table className="w-full border-collapse text-[13px] whitespace-nowrap">
                            <thead>
                                <tr>
                                    <th className="border border-slate-200 bg-[#f8fafc] px-2 py-1.5 text-left font-medium text-slate-500">科目编码</th>
                                    <th className="border border-slate-200 bg-[#f8fafc] px-2 py-1.5 text-left font-medium text-slate-500">科目名称</th>
                                    {Array.from({ length: 12 }, (_, m) => (
                                        <th key={m} className="border border-slate-200 bg-[#f8fafc] px-2 py-1.5 text-right font-medium text-slate-500">
                                            {m + 1}月
                                        </th>
                                    ))}
                                    <th className="border border-slate-200 bg-[#f8fafc] px-2 py-1.5 text-right font-medium text-slate-500">合计</th>
                                </tr>
                            </thead>
                            <tbody>
                                {(rendered?.rows ?? []).map((r, i) => (
                                    <tr key={r.code} style={i % 2 === 1 ? { background: "#fafbfd" } : undefined}>
                                        <td className="border border-slate-100 px-2 py-1.5 text-slate-700">{r.code}</td>
                                        <td className="border border-slate-100 px-2 py-1.5 text-slate-700">{r.name}</td>
                                        {(r.months ?? []).map((v, m) => (
                                            <td key={m} className={`border border-slate-100 px-2 py-1.5 text-right tabular-nums ${v < 0 ? "text-[#ef4444]" : "text-slate-700"}`}>
                                                {nf(v)}
                                            </td>
                                        ))}
                                        <td className={`border border-slate-100 px-2 py-1.5 text-right font-medium tabular-nums ${(r.total ?? 0) < 0 ? "text-[#ef4444]" : "text-slate-800"}`}>
                                            {nf(r.total)}
                                        </td>
                                    </tr>
                                ))}
                                {rendered != null && (rendered.rows ?? []).length === 0 && (
                                    <tr>
                                        <td colSpan={14} className="py-8 text-center text-[13px] text-slate-400">
                                            {`${renderTarget.year} 年无数据`}
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                </Dialog>
            )}
        </Page>
    );
}

/* ---------- 费用明细表 ---------- */

export function LemonExpenseDetail() {
    const { period, onPrev, onNext } = useReportPeriod(1);
    const exp = useReportExport();
    const { data, loading, error, reload } = useReportData(
        period,
        "费用明细表加载失败",
        (api, accountSetId) =>
            api.accountsetsAccountSetIdReportsExpenseDetailGet({
                accountSetId,
                year: period.year,
                month: period.month,
            }),
    );
    const [expandAll, setExpandAll] = useState(true);

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
            __style: { fontWeight: 700 },
        },
    ];
    const cols: Col[] = [
        { key: "code", title: "科目编码", width: "140px" },
        { key: "name", title: "科目名称" },
        { key: "cur", title: `${monthText(period)}金额`, align: "right", width: "160px", neg: true },
        { key: "total", title: "合计", align: "right", width: "160px", neg: true },
    ];
    return (
        <Page title="费用明细表" subtitle="按科目层级归集本期费用明细，仅统计已过账凭证。" icon={PieChartOutlined}>
            <ToolRow
                left={
                    <>
                        <PeriodStepper text={monthText(period)} onPrev={onPrev} onNext={onNext} />
                        <span className="cursor-pointer" onClick={() => setExpandAll((v) => !v)}>
                            <CheckBox label="展开所有级次" checked={expandAll} />
                        </span>
                    </>
                }
                right={
                    <>
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <Btn onClick={() => void exp.run("expense-detail", { year: period.year, month: period.month })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {loading && <LoadTip />}
            {error && <ErrorTip text={error} />}
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
    // 后端端点仅按年度取数（equity-change?year=），步进器按年切换。
    const { period, onPrev, onNext } = useReportPeriod(12);
    const exp = useReportExport();
    const { data, loading, error, reload } = useReportData(
        period,
        "所有者权益变动表加载失败",
        (api, accountSetId) => api.accountsetsAccountSetIdReportsEquityChangeGet({ accountSetId, year: period.year }),
    );

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

    const th = "border-b border-slate-200 bg-[#f8fafc] px-3 py-2 text-[13px] font-medium text-slate-500";
    const td = "border-b border-slate-100 px-3 py-2.5 text-[13px] text-slate-700";
    return (
        <Page title="所有者权益变动表" subtitle="按年度列示实收资本、资本公积、盈余公积与未分配利润的期初期末及变动。" icon={ReconciliationOutlined}>
            <ToolRow
                left={
                    <>
                        <span className="text-[13px] text-slate-500">会计年度：</span>
                        <PeriodStepper text={`${period.year}年`} onPrev={onPrev} onNext={onNext} />
                        <span
                            className="flex items-center gap-1 text-[13px] text-slate-400"
                            title="按年度列示实收资本、资本公积、盈余公积与未分配利润的期初期末及变动，仅统计已过账凭证"
                        >
                            <InfoCircleOutlined className="text-[14px]" /> 报表说明
                        </span>
                        {!balanced && !loading && !error && (
                            <span className="text-[13px] text-[#ef4444]">
                                报表不平，（未分配利润）本年年初余额+本年增加变动金额不等于本年年末...
                            </span>
                        )}
                    </>
                }
                right={
                    <>
                        <WipBtn title="报表公式编辑尚未接入后端，建设中">编辑公式</WipBtn>
                        <Btn onClick={() => void exp.run("equity-change", { year: period.year })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {loading && <LoadTip />}
            {error && <ErrorTip text={error} />}
            <div className="flex-1 overflow-x-auto px-4">
                <table className="border-collapse text-[13px] whitespace-nowrap">
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
                                    <td key={j} className={`${td} text-right tabular-nums`}>
                                        {r.v(j)}
                                    </td>
                                ))}
                                {EQ_COLS.map((_, j) => (
                                    <td key={`o${j}`} className={`${td} text-right tabular-nums`}>
                                        {r.pv(j)}
                                    </td>
                                ))}
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

/* ---------- 纳税统计表 ---------- */

export function LemonTaxReport() {
    const { period, onPrev, onNext } = useReportPeriod(1);
    const exp = useReportExport();
    const { data, loading, error, reload } = useReportData(
        period,
        "纳税统计表加载失败",
        (api, accountSetId) => api.accountsetsAccountSetIdReportsTaxGet({ accountSetId, year: period.year, month: period.month }),
    );

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
        SEQ_COL,
        { key: "ytd", title: "金额", group: "本年累计金额", align: "right", neg: true },
        { key: "ytdYoy", title: "同比变化", group: "本年累计金额", align: "right" },
        { key: "cur", title: "金额", group: "本期金额", align: "right", neg: true },
        { key: "curYoy", title: "同比变化", group: "本期金额", align: "right" },
    ];
    return (
        <Page title="纳税统计表" subtitle="统计各税种应交与已交金额及同比变化，仅统计已过账凭证。" icon={BankOutlined}>
            <ReportTool
                text={monthText(period)}
                onPrev={onPrev}
                onNext={onNext}
                right={
                    <>
                        <Btn onClick={() => void exp.run("tax", { year: period.year, month: period.month })}>导出</Btn>
                        <Btn onClick={() => window.print()}>打印</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {loading && <LoadTip />}
            {error && <ErrorTip text={error} />}
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} zebra={false} />
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}

/* ---------- 财务概要信息 ---------- */

export function LemonFinancialSummary() {
    const { period, onPrev, onNext } = useReportPeriod(1);
    const exp = useReportExport();
    const { data, loading, error, reload } = useReportData(
        period,
        "财务概要加载失败",
        (api, accountSetId) =>
            api.accountsetsAccountSetIdReportsFinancialSummaryGet({ accountSetId, year: period.year, month: period.month }),
    );

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
        SEQ_COL,
        { key: "ytd", title: "金额", group: "本年累计金额", align: "right", neg: true },
        { key: "ytdYoy", title: "同比变化", group: "本年累计金额", align: "right" },
        { key: "cur", title: "金额", group: "本期金额", align: "right", neg: true },
        { key: "curYoy", title: "同比变化", group: "本期金额", align: "right" },
        { key: "curMom", title: "环比变化", group: "本期金额", align: "right" },
    ];
    return (
        <Page title="财务概要信息" subtitle="盈利、三项费用、应收应付、资金与税金的关键指标一览。" icon={RiseOutlined}>
            <ReportTool
                text={monthText(period)}
                onPrev={onPrev}
                onNext={onNext}
                right={
                    <>
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <Btn onClick={() => void exp.run("financial-summary", { year: period.year, month: period.month })}>导出</Btn>
                        <ReloadButton onClick={reload} />
                    </>
                }
            />
            {loading && <LoadTip />}
            {error && <ErrorTip text={error} />}
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} zebra={false} />
            </div>
            {exp.toast && <Toast text={exp.toast} onClose={exp.clearToast} />}
        </Page>
    );
}
