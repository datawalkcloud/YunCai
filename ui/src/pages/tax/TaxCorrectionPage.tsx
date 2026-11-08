/**
 * 智能报税 · 作废与更正。
 *
 * 按申报日期区间从电子税务局「申报信息查询」取回申报表，列出每张表的申报状态；
 * 作废与更正由税局侧完成，本系统不代发，点操作后带着服务器上的登录态进电子税务局办理。
 * 未登录或登录态过期时弹出补一次短信验证码，登录成功后自动重取。
 */
import { useCallback, useEffect, useRef, useState } from "react";
import { Card, App, Button, DatePicker, Pagination, Table, Tooltip } from "antd";
import type { TableColumnsType } from "antd";
import { DownOutlined, ExclamationCircleFilled, LoginOutlined, ReloadOutlined } from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate, useParams } from "react-router";

import { useAuth } from "@/auth/AuthContext";
import { fetchTaxReports, type TaxDeclarationRow } from "@/lib/tax-portal";

import { TaxLoginPrompt } from "./TaxLoginPrompt";

type CorrectionAction = "作废" | "更正";

/** 两种操作在税局侧的后果不同，弹窗里说清楚再让用户进税局。 */
const ACTION_HINT: Record<CorrectionAction, string> = {
    作废: "作废后这张申报表按未申报处理，请及时重新申报，避免逾期。",
    更正: "更正需在电子税务局重新填报本张申报表，提交后原申报数据即被替换。",
};

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function TaxCorrectionPage() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { modal } = App.useApp();
    const { token } = useAuth();

    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("year"), dayjs().startOf("day")]);
    const [rows, setRows] = useState<TaxDeclarationRow[]>([]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [loading, setLoading] = useState(false);
    const [needLogin, setNeedLogin] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    /** 税局或服务器给的一句话：没取到数据时直接显示在表格里，不拿「暂无数据」糊过去。 */
    const [hint, setHint] = useState<string | null>(null);
    const [fetchedAt, setFetchedAt] = useState<string | null>(null);

    /** 一次查询要在税局侧分段取数，只认最后发起的那一次。 */
    const requestId = useRef(0);

    const load = useCallback(
        async (target: [Dayjs, Dayjs], opts: { promptLogin?: boolean } = {}) => {
            if (!accountSetId) return;
            const id = ++requestId.current;
            setLoading(true);
            try {
                const result = await fetchTaxReports(
                    accountSetId,
                    token,
                    target[0].format("YYYY-MM-DD"),
                    target[1].format("YYYY-MM-DD"),
                );
                if (id !== requestId.current) return;

                if (result.needLogin) {
                    setRows([]);
                    setNeedLogin(true);
                    setHint(result.message ?? "税局登录状态已过期，请重新登录电子税务局。");
                    // 用户主动点「搜索」时直接补登录，登录成功后自动再取一次数。
                    if (opts.promptLogin) setLoginOpen(true);
                    return;
                }

                const list = result.rows ?? [];
                setRows(list);
                setNeedLogin(false);
                setHint(list.length === 0 ? result.message ?? "当前查询区间没有申报表。" : null);
                setFetchedAt(dayjs().format("YYYY-MM-DD HH:mm"));
                setPage(1);
            } catch (err) {
                if (id !== requestId.current) return;
                setRows([]);
                setHint(err instanceof Error && err.message ? err.message : "读取税局申报表失败，请稍后重试。");
            } finally {
                if (id === requestId.current) setLoading(false);
            }
        },
        [accountSetId, token],
    );

    // 与【报表查询】一致：改区间立刻按新申报日期重取，【搜索】只作为手动刷新与补登录入口。
    useEffect(() => {
        void load(range);
    }, [load, range]);

    /** 税局里已作废的申报表既不能再作废，也不作为更正的对象。 */
    function askVoidOrCorrect(action: CorrectionAction, row: TaxDeclarationRow) {
        modal.confirm({
            title: `${action}申报表`,
            width: 520,
            content: (
                <div className="flex flex-col gap-1.5 py-1 text-[13px] leading-6">
                    <span>
                        《{row.declareKind}》所属期 {row.periodStart} 至 {row.periodEnd}，申报日期 {row.declareDate}，
                        应补（退）税额 {fmtMoney(row.payable)}。
                    </span>
                    <span>{ACTION_HINT[action]}</span>
                    <span className="text-(--muted-foreground)">
                        {action}在电子税务局完成，本系统不代发；进税局后在「申报信息查询」里选中本张申报表办理，
                        办完回到本页点【搜索】即可看到状态更新。
                    </span>
                </div>
            ),
            okText: "前往电子税务局",
            cancelText: "取消",
            onOk: () => navigate(`/${accountSetId}/tax/login`),
        });
    }

    const columns: TableColumnsType<TaxDeclarationRow> = [
        {
            title: "申报表单",
            dataIndex: "declareKind",
            key: "declareKind",
            width: "34%",
            render: (name: string) => (
                <span className="block truncate" title={name}>
                    《{name}》
                </span>
            ),
        },
        { title: "所属期起", dataIndex: "periodStart", key: "periodStart", width: "11%" },
        { title: "所属期止", dataIndex: "periodEnd", key: "periodEnd", width: "11%" },
        { title: "申报日期", dataIndex: "declareDate", key: "declareDate", width: "12%" },
        {
            title: "应补（退）税额",
            dataIndex: "payable",
            key: "payable",
            width: "12%",
            align: "right",
            render: (value: number) => fmtMoney(value),
        },
        { title: "申报状态", dataIndex: "status", key: "status", width: "8%" },
        {
            title: "操作",
            key: "op",
            width: "12%",
            render: (_, row) =>
                row.status === "已作废" ? (
                    <span className="text-(--muted-foreground)">--</span>
                ) : (
                    <span className="flex items-center gap-1">
                        {(["作废", "更正"] as const).map((action) => (
                            <Button
                                key={action}
                                type="link"
                                onClick={() => askVoidOrCorrect(action, row)}
                            >
                                {action}
                            </Button>
                        ))}
                    </span>
                ),
        },
    ];

    const total = rows.length;
    const pageCount = Math.max(1, Math.ceil(total / pageSize));
    const current = Math.min(page, pageCount);
    const from = total === 0 ? 0 : (current - 1) * pageSize + 1;
    const to = Math.min(current * pageSize, total);

    // min-h-full 而非 h-full：h-full 会把表格区锁在一屏内，长列表溢出后压住下方的分页栏。
    return (
        <Card>
            <div className="flex flex-wrap items-center gap-3" style={{ marginBottom: 16 }}>
                <span className="flex items-center gap-1.5 text-[13px] text-(--foreground)">
                    申报日期：
                    <span>
                        <DatePicker.RangePicker
                            allowClear={false}
                            value={range}
                            format="YYYY-MM-DD"
                            separator="至"
                            suffixIcon={<DownOutlined />}
                            onChange={(value) => {
                                if (!value?.[0] || !value[1]) return;
                                setRange([value[0].startOf("day"), value[1].startOf("day")]);
                            }}
                        />
                    </span>
                </span>
                <Button type="primary" loading={loading} onClick={() => void load(range, { promptLogin: true })}>
                    搜索
                </Button>
                {needLogin && (
                    <Button icon={<LoginOutlined />} onClick={() => setLoginOpen(true)}>
                        登录电子税务局
                    </Button>
                )}
                <span className="flex items-center gap-1.5 text-[12.5px] text-(--warn-icon)">
                    <ExclamationCircleFilled className="text-(--warn-icon)" />
                    作废成功后，请您及时重新申报，避免造成逾期未申报，被税务局处罚！
                </span>
            </div>

            <div className="min-h-0 flex-1">
                <Table<TaxDeclarationRow>
                    columns={columns}
                    dataSource={rows.slice((current - 1) * pageSize, current * pageSize)}
                    rowKey="key"
                    tableLayout="fixed"
                    loading={loading}
                    pagination={false}
                    locale={{
                        emptyText: (
                            <span className="block py-10 text-[13px] text-(--muted-foreground)">
                                {hint ?? "当前查询区间没有申报表"}
                            </span>
                        ),
                    }}
                />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 pb-3 text-[12.5px] text-(--muted-foreground)" style={{ marginTop: 16 }}>
                <span>{`显示 ${from} 到 ${to}，共 ${total} 条记录`}</span>
                <span className="flex items-center gap-1">
                    <Pagination
                        current={current}
                        pageSize={pageSize}
                        total={total}
                        pageSizeOptions={[20, 50, 100]}
                        showSizeChanger
                        showQuickJumper
                        onChange={(nextPage, nextPageSize) => {
                            setPageSize(nextPageSize);
                            setPage(nextPage);
                        }}
                    />
                    <Tooltip title="刷新">
                        <Button
                            type="text"
                            icon={<ReloadOutlined />}
                            aria-label="刷新"
                            loading={loading}
                            onClick={() => void load(range, { promptLogin: true })}
                        />
                    </Tooltip>
                </span>
            </div>

            <p className="pb-5 text-[12.5px] leading-5 text-(--muted-foreground)">
                申报表取自电子税务局「申报信息查询」，按申报日期区间过滤
                {fetchedAt ? `；最近更新 ${fetchedAt}` : ""}。作废与更正在电子税务局办理，已作废的申报表不再提供操作入口。
            </p>

            <TaxLoginPrompt
                open={loginOpen}
                accountSetId={accountSetId}
                reason={hint ?? undefined}
                onClose={() => setLoginOpen(false)}
                onSuccess={() => {
                    setLoginOpen(false);
                    void load(range);
                }}
            />
        </Card>
    );
}
