/**
 * 智能报税 · 个税申报 · 申报缴款。
 *
 * 按税局口径，一个所属期在个税端只有一张「综合所得预扣预缴申报表-正常工资薪金所得」，
 * 所以本页是一行汇总：人数、本期收入与应补（退）税额取自「报表填写」同一套工资表算法，
 * 点「填写」回到报表填写页。申报状态、申报日期与申报渠道取自个税系统的申报记录
 * （查询统计 → 申报记录查询），本地只负责推算有没有填过；正式报送仍在税局侧。
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Card, App, Button, Modal, Table, Tooltip } from "antd";
import type { TableColumnsType } from "antd";
import { BookOutlined } from "@ant-design/icons";
import { useNavigate, useParams } from "react-router";

import { useTaxPeriod } from "./tax-period";
import { useAuth } from "@/auth/AuthContext";
import { downloadCsv } from "@/lib/tax-csv";
import { EMPTY_CELL, filingDeadline, formatAmount, formatDate, pad, periodKey } from "@/lib/tax-format";
import { fetchIitFilings, type TaxIitFilingRow } from "@/lib/tax-iit";
import { fetchIitReport } from "@/lib/tax-portal";

import { IitLoginModal } from "./IitLoginModal";

/** 个税扣缴端在电子税务局里的申报表名称。 */
const FORM_NAME = "综合所得预扣预缴申报表-正常工资薪金所得";

/** 税局申报记录的取数状态；只有 loaded 才敢把「没有记录」当作「未申报」。 */
type FilingsState = "idle" | "loading" | "loaded" | "failed";

interface DeclareRow {
    key: string;
    /** 申报表名称 */
    formName: string;
    /** 税款所属期 yyyyMM */
    period: string;
    /** 申报期限：所属期次月 15 日，遇周末顺延 */
    deadline: string;
    /** 未填写时人数与金额一律留空 */
    personCount: number | null;
    income: number | null;
    /** 操作列：跳到报表填写页 */
    filled: boolean;
    /** 税局有本期的申报记录 */
    declared: boolean;
    status: string;
    refundable: number | null;
    /** 以下三项取自税局的申报记录，未取回时留空。 */
    declaredOn: string | null;
    channel: string | null;
    feedback: string | null;
}

const HELP_NOTES = [
    `「${FORM_NAME}」的人数、本期收入与应补（退）税额来自「报表填写」，与工资表同源。`,
    "申报状态、申报日期与申报渠道取自个税系统的申报记录；未登录或取数失败时，状态只反映本地有没有填写。",
    "申报期限按所属期次月 15 日推算，遇周末顺延到下一个工作日；法定节假日调整以税局公告为准。",
    "正式申报、更正与缴款都在电子税务局完成，本页不代替报送。",
    "完税证明由电子税务局开具，尚未接入。",
];

const moneyCell = (value: number | null) =>
    value === null ? (
        <span className="text-(--muted-foreground)">{EMPTY_CELL}</span>
    ) : (
        <span className="tabular-nums">{formatAmount(value)}</span>
    );

/** 税局的申报记录行按「年 + 月」匹配：所属期可能是 2026-10、2026年10月或 2026-10-01至2026-10-31。 */
function periodMatches(value: string, year: number, month: number): boolean {
    const match = /(\d{4})\D{0,4}(\d{1,2})/.exec(value ?? "");
    return match !== null && Number(match[1]) === year && Number(match[2]) === month;
}

/** 本页只关心工资薪金那张表，同月的奖金等其他申报表不算。 */
function formMatches(name: string): boolean {
    return !name || name.includes("工资薪金") || name.includes("预扣预缴");
}

/** 一个所属期可能先正常申报再补正，取申报时间最晚的那条。 */
function pickFiling(rows: TaxIitFilingRow[], year: number, month: number): TaxIitFilingRow | null {
    const matches = rows.filter((row) => periodMatches(row.period, year, month) && formMatches(row.formName));
    if (matches.length === 0) {
        return null;
    }
    return [...matches].sort((a, b) => (b.declareTime ?? "").localeCompare(a.declareTime ?? ""))[0];
}

function filingStatus(row: TaxIitFilingRow): string {
    const type = row.declareType ?? "";
    if (type.includes("作废")) return "已作废";
    if (type.includes("更正")) return "已更正";
    return "已申报";
}

/** 税局金额列带千分位或「-」占位，解析不出就回 null，交给本地的工资表口径兜底。 */
function parseAmount(value: string): number | null {
    const cleaned = (value ?? "").replace(/,/g, "").replace(/[^\d.-]/g, "");
    if (!/\d/.test(cleaned)) {
        return null;
    }
    const parsed = Number(cleaned);
    return Number.isFinite(parsed) ? parsed : null;
}

function parseCount(value: string): number | null {
    const match = /\d+/.exec(value ?? "");
    return match ? Number(match[0]) : null;
}

export function TaxIitDeclarePage() {
    const period = useTaxPeriod();
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { token } = useAuth();
    const { message, modal } = App.useApp();

    const [personCount, setPersonCount] = useState(0);
    const [income, setIncome] = useState(0);
    const [refundable, setRefundable] = useState(0);
    const [filled, setFilled] = useState(false);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const [filing, setFiling] = useState<TaxIitFilingRow | null>(null);
    const [filingsState, setFilingsState] = useState<FilingsState>("idle");
    const [filingsMessage, setFilingsMessage] = useState<string | null>(null);
    const [needLogin, setNeedLogin] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    const [helpOpen, setHelpOpen] = useState(false);

    /** 取数按所属期去重（StrictMode 会把挂载 effect 跑两遍），再用请求序号丢弃切期后才回来的旧结果。 */
    const filingsInFlight = useRef<string | null>(null);
    const filingsRequestId = useRef(0);
    /** 当前 filing 状态属于哪个所属期；切期时先清掉上期的记录，避免把上期的「已申报」显示到本期。 */
    const filingMonth = useRef<string | null>(null);

    /** 申报缴款与报表填写读同一份本地申报表，这里只取汇总口径。 */
    const reload = useCallback(async () => {
        if (!accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
        try {
            const result = await fetchIitReport(accountSetId, token, String(period.period));
            const rows = result.rows ?? [];
            setFilled(rows.length > 0);
            setPersonCount(result.personCount ?? 0);
            setIncome(result.totalIncome ?? 0);
            setRefundable(result.totalRefundable ?? 0);
            setError(null);
        } catch (err) {
            setError(err instanceof Error ? err.message : "读取本期申报表失败。");
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, period.period]);

    /** 从个税系统取本所属期的申报记录：有记录就是已申报，取不回来时状态按本地填写情况显示。 */
    const refreshFilings = useCallback(
        async (opts: { promptLogin?: boolean } = {}) => {
            if (!accountSetId) return;
            const month = `${period.year}-${pad(period.month)}`;
            if (filingsInFlight.current === month) return;
            filingsInFlight.current = month;
            if (filingMonth.current !== month) {
                filingMonth.current = month;
                setFiling(null);
                setFilingsMessage(null);
            }
            const requestId = ++filingsRequestId.current;
            setFilingsState("loading");
            setFilingsMessage(null);
            try {
                const result = await fetchIitFilings(accountSetId, token, month, month);
                if (filingsRequestId.current !== requestId) return;
                if (result.needLogin) {
                    setNeedLogin(true);
                    setFilingsState("failed");
                    setFilingsMessage("个税登录状态已失效，没有读取到税局的申报记录。");
                    if (opts.promptLogin) setLoginOpen(true);
                    return;
                }

                setNeedLogin(false);
                setFiling(pickFiling(result.rows ?? [], period.year, period.month));
                setFilingsState("loaded");
                if ((result.rows ?? []).length === 0) {
                    setFilingsMessage("没有从个税系统取回申报记录，申报状态按本地填写情况显示。");
                }
            } catch (err) {
                if (filingsRequestId.current !== requestId) return;
                setFilingsState("failed");
                setFilingsMessage(err instanceof Error ? err.message : "读取个税申报记录失败，请稍后重试。");
            } finally {
                if (filingsInFlight.current === month) {
                    filingsInFlight.current = null;
                }
            }
        },
        [accountSetId, token, period.year, period.month],
    );

    useEffect(() => {
        void reload();
    }, [reload]);

    useEffect(() => {
        void refreshFilings();
    }, [refreshFilings]);

    const toTaxLogin = () => navigate(`/${accountSetId}/tax/login`);

    const row = useMemo<DeclareRow>(() => {
        const declared = filing !== null;
        const status = declared
            ? filingStatus(filing)
            : filingsState === "loading" || filingsState === "idle"
              ? "查询中…"
              : filingsState === "loaded"
                ? filled
                    ? "已填写·未申报"
                    : "未填写"
                : "状态未知";

        return {
            key: "iit-salary",
            formName: FORM_NAME,
            period: periodKey(period.year, period.month),
            deadline: formatDate(filingDeadline(period.year, period.month)),
            // 已申报时优先用税局回的口径，税局格子是「-」时再回落到本地工资表算出的数。
            personCount: (declared ? parseCount(filing.headcount) : null) ?? (filled ? personCount : null),
            income: (declared ? parseAmount(filing.income) : null) ?? (filled ? income : null),
            filled,
            declared,
            status,
            refundable: (declared ? parseAmount(filing.taxDue) : null) ?? (filled ? refundable : null),
            declaredOn: filing?.declareTime || null,
            channel: filing?.channel || null,
            feedback: null,
        };
    }, [filing, filingsState, filled, income, personCount, period.month, period.year, refundable]);

    const rows = useMemo(() => [row], [row]);

    function exportList() {
        downloadCsv(`个税申报缴款_${row.period}.csv`, [
            ["申报表", "税款所属期", "申报期限", "申报人数", "本期收入", "申报状态", "应补（退）税额", "申报日期", "申报渠道", "反馈"],
            [
                row.formName,
                row.period,
                row.deadline,
                row.personCount ?? EMPTY_CELL,
                row.income ?? EMPTY_CELL,
                row.status,
                row.refundable ?? EMPTY_CELL,
                row.declaredOn ?? EMPTY_CELL,
                row.channel ?? EMPTY_CELL,
                row.feedback ?? "-",
            ],
        ]);
        message.success("已导出本期申报表。");
    }

    /** 报送由电子税务局完成，这里只把人送到登录入口。 */
    function openDeclare() {
        modal.confirm({
            title: "发送申报",
            content: `将向电子税务局申报 ${period.year}年${period.month}月 综合所得（工资薪金）${personCount} 人。申报由税局侧完成，当前需先登录电子税务局后在税局页面办理。`,
            okText: "前往登录税局",
            cancelText: "取消",
            onOk: toTaxLogin,
        });
    }

    const columns: TableColumnsType<DeclareRow> = [
        { title: "申报表", dataIndex: "formName", key: "formName", width: 300 },
        { title: "税款所属期", dataIndex: "period", key: "period", width: 120 },
        { title: "申报期限", dataIndex: "deadline", key: "deadline", width: 130 },
        {
            title: "申报人数",
            dataIndex: "personCount",
            key: "personCount",
            width: 120,
            align: "right",
            render: (value: number | null) =>
                value === null ? <span className="text-(--muted-foreground)">{EMPTY_CELL}</span> : `${value} 人`,
        },
        {
            title: "本期收入",
            dataIndex: "income",
            key: "income",
            width: 140,
            align: "right",
            render: moneyCell,
        },
        {
            title: "操作",
            key: "action",
            width: 90,
            render: () => (
                <Button type="link" size="small" onClick={() => navigate(`/${accountSetId}/tax/iit/report`)}>
                    填写
                </Button>
            ),
        },
        {
            title: "申报状态",
            dataIndex: "status",
            key: "status",
            width: 130,
            render: (value: string, record) => (
                <span
                    className={
                        record.declared && record.status !== "已作废"
                            ? "text-(--success)"
                            : record.status === "查询中…"
                              ? "text-(--muted-foreground)"
                              : "text-(--foreground)"
                    }
                >
                    {value}
                </span>
            ),
        },
        {
            title: "应补（退）税额",
            dataIndex: "refundable",
            key: "refundable",
            width: 140,
            align: "right",
            render: moneyCell,
        },
        {
            title: "申报日期",
            dataIndex: "declaredOn",
            key: "declaredOn",
            width: 170,
            render: (value: string | null) => value ?? <span className="text-(--muted-foreground)">{EMPTY_CELL}</span>,
        },
        {
            title: "申报渠道",
            dataIndex: "channel",
            key: "channel",
            width: 130,
            render: (value: string | null) => value ?? <span className="text-(--muted-foreground)">{EMPTY_CELL}</span>,
        },
        { title: "反馈", dataIndex: "feedback", key: "feedback", width: 90, render: (value: string | null) => value ?? "-" },
    ];

    return (
        <Card>
            <div className="flex shrink-0 flex-wrap items-start gap-4">
                <div className="flex w-full max-w-[820px] shrink-0 items-start gap-2 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-2 text-[12.5px] leading-5 text-(--warn-text)">
                    <span className="mt-0.5 flex h-[14px] w-[14px] shrink-0 items-center justify-center rounded-full bg-(--warn-icon) text-[10px] text-white">
                        !
                    </span>
                    <span>
                        温馨提示：1、申报渠道为其他渠道申报的数据将不允许进行作废、更正处理。
                        <br />
                        2、使用个税网页端——实名账号的登录方式进行申报或更新数据时，请勿登录自然人电子税局。
                    </span>
                </div>
                <div className="ml-auto flex flex-wrap items-center gap-2">
                    <Button type="text" icon={<BookOutlined className="text-(--brand)" />} onClick={() => setHelpOpen(true)}>
                        帮助手册
                    </Button>
                    <Tooltip title="完税证明由电子税务局开具，尚未接入">
                        <Button disabled>开具完税证明</Button>
                    </Tooltip>
                    <Button disabled={!filled && !row.declared} onClick={exportList}>
                        导出申报表
                    </Button>
                    <Button
                        type="primary"
                        loading={loading || filingsState === "loading"}
                        onClick={() => {
                            void reload();
                            void refreshFilings({ promptLogin: true });
                        }}
                    >
                        更新申报结果
                    </Button>
                    <Tooltip
                        title={
                            row.declared
                                ? "本期已在税局申报"
                                : filled
                                  ? ""
                                  : "请先到「报表填写」生成本期申报表"
                        }
                    >
                        <span>
                            <Button disabled={!filled || row.declared} onClick={openDeclare}>
                                申报
                            </Button>
                        </span>
                    </Tooltip>
                </div>
            </div>

            <div className="min-h-0 flex-1 pb-3 pt-3">
                {error && (
                    <div className="mb-3 rounded-md border border-(--warn-border) bg-(--table-neg-bg) px-3 py-1.5 text-[12.5px] text-(--danger)">
                        {error}
                    </div>
                )}
                {needLogin ? (
                    <div className="mb-3 flex items-center gap-3 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-1.5 text-[12.5px] text-(--warn-text)">
                        <span>{filingsMessage ?? "个税登录状态已失效，没有读取到税局的申报记录。"}</span>
                        <Button size="small" onClick={() => setLoginOpen(true)}>
                            登录个税
                        </Button>
                    </div>
                ) : (
                    filingsMessage && (
                        <div className="mb-3 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-1.5 text-[12.5px] text-(--warn-text)">
                            {filingsMessage}
                        </div>
                    )
                )}
                <Table<DeclareRow>
                    columns={columns}
                    dataSource={rows}
                    rowKey="key"
                    loading={loading}
                    pagination={false}
                    scroll={{ x: 1480 }}
                />
                <div className="mt-2 text-[12px] text-(--muted-foreground)">
                    申报状态、申报日期与申报渠道取自自然人电子税务局的申报记录（查询统计 → 申报记录查询）；未取回时，状态按本地填写情况显示。
                </div>
            </div>

            <Modal
                open={helpOpen}
                title="帮助手册"
                width={560}
                footer={<Button onClick={() => setHelpOpen(false)}>知道了</Button>}
                onCancel={() => setHelpOpen(false)}
            >
                <ul className="m-0 list-disc space-y-2 pl-5 text-[13px] leading-6 text-(--foreground)">
                    {HELP_NOTES.map((note) => (
                        <li key={note}>{note}</li>
                    ))}
                </ul>
            </Modal>

            <IitLoginModal
                open={loginOpen}
                accountSetId={accountSetId}
                onClose={() => setLoginOpen(false)}
                onSuccess={() => {
                    setLoginOpen(false);
                    void refreshFilings();
                }}
            />
        </Card>
    );
}
