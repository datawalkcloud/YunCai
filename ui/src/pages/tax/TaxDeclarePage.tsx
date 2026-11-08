/**
 * 智能报税 · 税种申报。
 *
 * 页面结构与税局一致：申报期限提示条、申报结果表格、税局直达专区。
 * 表格数据由服务器上的税局会话读取「申报信息查询」里的已申报记录，按所选税款所属期过滤；
 * 取数必须先有税局登录态：点「更新申报结果」时若未登录或登录态过期，直接弹出登录框补一次短信验证码，
 * 成功后自动重新取数。真实申报仍需到电子税务局办理。
 */
import { useCallback, useEffect, useState, type ReactNode } from "react";
import { Card, App, Button, Select, Table, Tag } from "antd";
import type { TableColumnsType } from "antd";
import {
    AppstoreOutlined,
    BookOutlined,
    ContainerOutlined,
    FileTextOutlined,
    LoginOutlined,
    ProfileOutlined,
} from "@ant-design/icons";
import { useNavigate, useParams } from "react-router";

import { useTaxPeriod } from "./tax-period";
import { useAuth } from "@/auth/AuthContext";
import { fetchTaxDeclarations, type TaxDeclarationRow } from "@/lib/tax-portal";
import { deadlineFor, filingDeadline, formatDate, formatAmount, pad } from "@/lib/tax-format";

import { TaxLoginPrompt } from "./TaxLoginPrompt";

interface DeclareRow extends TaxDeclarationRow {
    /** 申报期限：所属期止次月 15 日，遇周末顺延到下一个工作日 */
    deadline: string;
}

const STATUS_COLORS: Record<string, string> = {
    已申报: "green",
    已更正: "gold",
    已作废: "default",
};

/** 税局直达专区的入口；当前只能经税务助手打开电子税务局，无法直达具体业务页。 */
const DIRECT_GROUPS: Array<{
    key: string;
    title: string;
    color: string;
    icon: ReactNode;
    items: string[];
}> = [
    {
        key: "quick",
        title: "快捷入口",
        color: "var(--warn-icon)",
        icon: <AppstoreOutlined />,
        items: ["电子税务局", "税种核定（应申报税费种信息报告）", "征纳互动", "账户中心", "申报智能自检"],
    },
    {
        key: "common",
        title: "常办业务",
        color: "var(--brand)",
        icon: <ProfileOutlined />,
        items: ["社保业务", "财产和行为税合并申报", "办税进度及结果查询", "出口退税申报"],
    },
    {
        key: "einvoice",
        title: "数电票业务",
        color: "var(--success)",
        icon: <FileTextOutlined />,
        items: ["开票业务", "票种核定（发票用票需求申请）", "税务数字账户"],
    },
];

const columns: TableColumnsType<DeclareRow> = [
    { title: "序号", key: "index", width: 56, render: (_, __, index) => index + 1 },
    { title: "申报种类", dataIndex: "declareKind", key: "declareKind", width: 320 },
    { title: "所属期起", dataIndex: "periodStart", key: "periodStart", width: 110 },
    { title: "所属期止", dataIndex: "periodEnd", key: "periodEnd", width: 110 },
    { title: "申报期限", dataIndex: "deadline", key: "deadline", width: 110 },
    { title: "操作", key: "op", width: 80, render: () => <span className="text-(--muted-foreground)">—</span> },
    {
        title: "申报状态",
        dataIndex: "status",
        key: "status",
        width: 110,
        render: (status: string) => <Tag color={STATUS_COLORS[status] ?? "blue"}>{status}</Tag>,
    },
    {
        title: "应补(退)税额",
        dataIndex: "payable",
        key: "payable",
        width: 140,
        align: "right",
        render: (payable: number) => <span className="tabular-nums">{formatAmount(payable)}</span>,
    },
    {
        title: "申报日期",
        dataIndex: "declareDate",
        key: "declareDate",
        width: 110,
        render: (declareDate: string) => declareDate || "-",
    },
    {
        title: "反馈",
        dataIndex: "feedback",
        key: "feedback",
        render: (feedback: string) => feedback || "-",
    },
];

export function TaxDeclarePage() {
    const period = useTaxPeriod();
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { message } = App.useApp();
    const { token } = useAuth();

    const [rows, setRows] = useState<DeclareRow[]>([]);
    const [loading, setLoading] = useState(false);
    const [needLogin, setNeedLogin] = useState(false);
    const [emptyHint, setEmptyHint] = useState<string | null>(null);
    const [loginOpen, setLoginOpen] = useState(false);

    const periodKey = `${period.year}${pad(period.month)}`;

    const refresh = useCallback(
        async (opts: { silent?: boolean; promptLogin?: boolean } = {}) => {
            if (!accountSetId) return;
            setLoading(true);
            try {
                const result = await fetchTaxDeclarations(accountSetId, token, periodKey);
                if (result.needLogin) {
                    setRows([]);
                    setNeedLogin(true);
                    setEmptyHint(result.message ?? "税局登录状态已过期，请重新登录电子税务局。");
                    // 用户主动点「更新申报结果」时直接补登录，登录成功后会自动再取一次数。
                    if (opts.promptLogin) setLoginOpen(true);
                    return;
                }

                setRows(
                    (result.rows ?? []).map((row) => ({ ...row, deadline: deadlineFor(row.periodEnd) })),
                );
                setNeedLogin(false);
                setEmptyHint(result.message ?? null);
            } catch (err) {
                const text = err instanceof Error && err.message ? err.message : "读取税局申报记录失败，请稍后重试。";
                if (text.includes("尚未登录")) {
                    setNeedLogin(true);
                    if (opts.promptLogin) setLoginOpen(true);
                } else if (opts.silent) {
                    message.warning(text);
                } else {
                    message.error(text);
                }
            } finally {
                setLoading(false);
            }
        },
        [accountSetId, message, periodKey, token],
    );

    useEffect(() => {
        void refresh({ silent: true });
    }, [refresh]);

    const toTaxLogin = () => navigate(`/${accountSetId}/tax/login`);

    const deadline = filingDeadline(period.year, period.month);
    const deadlineText = formatDate(deadline);
    // 进页面时取一次当天日期给提示条用，避免在渲染里读系统时间。
    const [today] = useState(() => {
        const date = new Date();
        date.setHours(0, 0, 0, 0);
        return date;
    });
    const remaining = Math.round((deadline.getTime() - today.getTime()) / 86_400_000);

    const handleLoginSuccess = useCallback(() => {
        setLoginOpen(false);
        void refresh({ silent: true });
    }, [refresh]);

    return (
        <Card>
            <div className="flex flex-wrap items-start justify-between gap-4" style={{ marginBottom: 16 }}>
                <div className="flex max-w-[860px] items-start gap-2 rounded-md border border-(--warn-border) bg-(--warn-bg) px-4 py-3 text-[13px] leading-6 text-(--warn-text)">
                    <ContainerOutlined className="mt-1 shrink-0 text-[15px] text-(--warn-icon)" />
                    <div>
                        <div>
                            温馨提示：1、当期税款所属期的申报时间在{" "}
                            <span className="font-medium text-(--brand)">{deadlineText}</span> 截止，
                            {remaining > 0 ? (
                                <>
                                    还有 <span className="font-medium text-(--warn-icon)">{remaining}</span> 天，
                                    请及时进行申报
                                </>
                            ) : remaining === 0 ? (
                                "今天是申报截止日，请及时进行申报"
                            ) : (
                                <span className="text-(--danger)">已超期 {-remaining} 天，请尽快办理</span>
                            )}
                        </div>
                        <div>
                            2、如需增加或删除【税种申报】下的税种信息，可在{" "}
                            <Button
                                type="link"
                                onClick={() => navigate(`/${accountSetId}/tax/kinds`)}
                            >
                                【税费种管理】
                            </Button>{" "}
                            中进行调整
                        </div>
                        <div>
                            3、系统报税或更新数据过程中{" "}
                            <span className="text-(--danger)">请勿登录电子税务局</span>
                        </div>
                    </div>
                </div>

                <div className="flex shrink-0 flex-wrap items-center gap-3">
                    <Button
                        type="text"
                        onClick={() => message.info("申报前请先在【税费种管理】确认税费种核定，报税过程中请勿登录电子税务局。")}
                        icon={
                            <span className="flex h-4 w-4 items-center justify-center rounded-sm bg-(--success) text-[10px] text-white">
                                <BookOutlined />
                            </span>
                        }
                    >
                        帮助手册
                    </Button>
                    <span className="flex items-center gap-1 text-[13px] text-(--muted-foreground)">
                        征收类型：
                        <Select disabled className="w-[120px]" placeholder="未初始化" />
                    </span>
                    {needLogin && (
                        <Button icon={<LoginOutlined />} onClick={() => setLoginOpen(true)}>
                            登录电子税务局
                        </Button>
                    )}
                    <Button loading={loading} onClick={() => void refresh({ promptLogin: true })}>
                        更新申报结果
                    </Button>
                    <Button
                        type="primary"
                        onClick={() => message.info("申报办理请前往电子税务局；本页用于查看税局的申报记录。")}
                    >
                        申报
                    </Button>
                </div>
            </div>

            <Table<DeclareRow>
                columns={columns}
                dataSource={rows}
                rowKey="key"
                loading={loading}
                pagination={false}
                tableLayout="fixed"
                scroll={{ x: 1200 }}
                locale={{
                    emptyText: (
                        <div className="py-16 text-center text-[13px] text-(--muted-foreground)">
                            {needLogin ? (
                                <>
                                    尚未登录电子税务局，请先登录后再查看申报结果
                                    <Button
                                        type="link"
                                        onClick={() => setLoginOpen(true)}
                                    >
                                        【登录电子税务局】
                                    </Button>
                                </>
                            ) : (
                                emptyHint || "所选税款所属期暂无申报记录"
                            )}
                        </div>
                    ),
                }}
            />

            <section className="mt-4 rounded-lg border border-(--border)">
                <div className="flex items-center gap-2 px-5 pb-1 pt-4">
                    <span className="text-[14px] font-medium text-(--foreground)">税局直达专区</span>
                    <span className="rounded-sm bg-(--danger) px-1.5 py-[1px] text-[10px] font-medium leading-4 text-white">
                        HOT
                    </span>
                </div>
                <div className="grid grid-cols-1 gap-6 px-5 py-4 md:grid-cols-3">
                    {DIRECT_GROUPS.map((group) => (
                        <div key={group.key}>
                            <div className="mb-2 flex items-center gap-2 text-[13.5px] font-medium text-(--foreground)">
                                <span style={{ color: group.color }}>{group.icon}</span>
                                {group.title}
                            </div>
                            <ul className="space-y-1.5">
                                {group.items.map((item) => (
                                    <li key={item}>
                                        <Button
                                            type="link"
                                            title="前往一键登录税局后办理"
                                            onClick={toTaxLogin}
                                        >
                                            {item} <span className="text-(--brand)/70">›</span>
                                        </Button>
                                    </li>
                                ))}
                            </ul>
                        </div>
                    ))}
                </div>
            </section>

            {accountSetId && (
                <TaxLoginPrompt
                    open={loginOpen}
                    accountSetId={accountSetId}
                    reason="查看申报结果需要读取电子税务局，当前的税局登录状态已失效。服务器会用账套里保存的税局资料重新登录，你只需要填一次短信验证码。"
                    onClose={() => setLoginOpen(false)}
                    onSuccess={handleLoginSuccess}
                />
            )}
        </Card>
    );
}
