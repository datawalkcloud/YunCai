/**
 * 智能报税 · 报表查询。
 *
 * 版式对齐税局客户端「报表查询」：查询区间、温馨提示、更新/打印/批量导出，
 * 下方为申报表列表与分页，底部为快捷入口。
 * 申报表由服务器上的税局会话从电子税务局「申报信息查询」按申报日期区间取回：
 * 未登录或登录态过期时弹出补一次短信验证码，登录成功后自动重取。
 * 税局的申报查询接口不给缴款日期，该列固定显示「-」，缴款情况到税费缴纳侧核对。
 */
import { useCallback, useEffect, useRef, useState, type Key } from "react";
import { Card, App, Button, DatePicker, Dropdown, Pagination, Table, Tooltip } from "antd";
import type { MenuProps, TableColumnsType } from "antd";
import {
    DownOutlined,
    DownloadOutlined,
    ExclamationCircleFilled,
    LoginOutlined,
    PrinterOutlined,
    ReloadOutlined,
    RightOutlined,
} from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate, useParams } from "react-router";

import { useAuth } from "@/auth/AuthContext";
import { fetchTaxReports, type TaxDeclarationRow } from "@/lib/tax-portal";
import { cn } from "@/lib/utils";

import { TaxLoginPrompt } from "./TaxLoginPrompt";

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function TaxReportQueryPage() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { message } = App.useApp();
    const { token } = useAuth();

    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("year"), dayjs().startOf("day")]);
    const [rows, setRows] = useState<TaxDeclarationRow[]>([]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [selectedKeys, setSelectedKeys] = useState<Key[]>([]);
    const [expandedKeys, setExpandedKeys] = useState<readonly Key[]>([]);
    const [loading, setLoading] = useState(false);
    const [needLogin, setNeedLogin] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    /** 税局或服务器给的一句话：没取到数据时直接显示在表格里，不拿「暂无数据」糊过去。 */
    const [hint, setHint] = useState<string | null>(null);
    const [fetchedAt, setFetchedAt] = useState<string | null>(null);

    /** 改区间会连着发几次请求，只认最后发起的那一次。 */
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
                    // 用户主动点「更新报表」时直接补登录，登录成功后自动再取一次数。
                    if (opts.promptLogin) setLoginOpen(true);
                    return;
                }

                const list = result.rows ?? [];
                setRows(list);
                setNeedLogin(false);
                setHint(list.length === 0 ? result.message ?? "当前查询区间没有申报表。" : null);
                setFetchedAt(dayjs().format("YYYY-MM-DD HH:mm"));
                setPage(1);
                setSelectedKeys([]);
                setExpandedKeys([]);
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

    useEffect(() => {
        void load(range);
    }, [load, range]);

    const pageCount = Math.max(1, Math.ceil(rows.length / pageSize));
    const current = Math.min(page, pageCount);
    const paged = rows.slice((current - 1) * pageSize, current * pageSize);
    const from = rows.length === 0 ? 0 : (current - 1) * pageSize + 1;
    const to = Math.min(current * pageSize, rows.length);

    function exportCsv(onlySelected: boolean) {
        const target = onlySelected ? rows.filter((row) => selectedKeys.includes(row.key)) : rows;
        if (target.length === 0) {
            message.warning(onlySelected ? "请先勾选需要导出的申报表。" : "当前查询区间没有可导出的申报表。");
            return;
        }
        const header = ["申报表", "所属期", "申报状态", "申报日期", "缴款日期", "应补（退）税额"];
        const body = target.map((row) => [
            row.declareKind,
            `${row.periodStart}至${row.periodEnd}`,
            row.status,
            row.declareDate,
            "-",
            fmtMoney(row.payable),
        ]);
        const csv = [header, ...body]
            .map((line) => line.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(","))
            .join("\r\n");
        const blob = new Blob(["\ufeff", csv], { type: "text/csv;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = `申报表_${range[0].format("YYYYMMDD")}-${range[1].format("YYYYMMDD")}.csv`;
        link.click();
        URL.revokeObjectURL(url);
        message.success(`已导出 ${target.length} 条申报表记录。`);
    }

    const exportItems: MenuProps["items"] = [
        { key: "all", label: "导出当前列表" },
        { key: "selected", label: "导出勾选记录", disabled: selectedKeys.length === 0 },
    ];

    const columns: TableColumnsType<TaxDeclarationRow> = [
        {
            title: "申报表",
            dataIndex: "declareKind",
            key: "declareKind",
            render: (name: string, row) => {
                const expanded = expandedKeys.includes(row.key);
                return (
                    <span className="flex items-center gap-1.5">
                        <Button
                            type="text"
                            aria-label={expanded ? "收起申报表" : "展开申报表"}
                            onClick={() =>
                                setExpandedKeys(
                                    expanded
                                        ? expandedKeys.filter((key) => key !== row.key)
                                        : [...expandedKeys, row.key],
                                )
                            }
                            icon={
                                <RightOutlined
                                    className={cn(
                                        "text-[11px] transition-transform",
                                        expanded && "rotate-90",
                                    )}
                                />
                            }
                        />
                        <span className="truncate" title={name}>
                            《{name}》
                        </span>
                    </span>
                );
            },
        },
        {
            title: "所属期",
            key: "period",
            width: 190,
            render: (_, row) => `${row.periodStart}至${row.periodEnd}`,
        },
        { title: "申报状态", dataIndex: "status", key: "status", width: 120 },
        { title: "申报日期", dataIndex: "declareDate", key: "declareDate", width: 140 },
        { title: "缴款日期", key: "paidOn", width: 140, render: () => "-" },
        {
            title: "应补（退）税额",
            dataIndex: "payable",
            key: "payable",
            width: 280,
            align: "right",
            render: (value: number) => fmtMoney(value),
        },
    ];

    // min-h-full 而非 h-full：h-full 会把表格区锁在一屏内，长列表溢出后压住下方的分页栏。
    return (
        <Card>
            <div className="flex flex-wrap items-start gap-3" style={{ marginBottom: 16 }}>
                <span className="shrink-0">
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

                <div className="flex min-w-[320px] flex-1 items-start gap-2 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-2 text-[13px] leading-[22px] text-(--warn-text)">
                    <ExclamationCircleFilled className="mt-[4px] shrink-0 text-[14px] text-(--warn-icon)" />
                    <div>
                        <div>温馨提示：1、申报表数据不全或有变更，可以点击【更新报表】按钮进行更新</div>
                        <div className="pl-[76px]">
                            2、更新完成后，未能获取到申报表，可能是税局数据存在延迟，请稍后重试
                        </div>
                    </div>
                </div>

                <div className="flex shrink-0 flex-wrap items-center gap-2">
                    {needLogin && (
                        <Button icon={<LoginOutlined />} onClick={() => setLoginOpen(true)}>
                            登录电子税务局
                        </Button>
                    )}
                    <Button loading={loading} onClick={() => void load(range, { promptLogin: true })}>
                        更新报表
                    </Button>
                    <Button icon={<PrinterOutlined />} onClick={() => message.info("打印封面需要税局回传申报表版式，暂未接入。")}>打印封面</Button>
                    <Button
                        icon={<PrinterOutlined />}
                        onClick={() =>
                            selectedKeys.length === 0
                                ? message.warning("请先勾选需要打印的申报表。")
                                : message.info("批量打印需要税局回传申报表版式，暂未接入。")
                        }
                    >
                        批量打印
                    </Button>
                    <Dropdown menu={{ items: exportItems, onClick: ({ key }) => exportCsv(key === "selected") }}>
                        <Button type="primary" icon={<DownloadOutlined />}>
                            批量导出 <DownOutlined className="text-[9px]" />
                        </Button>
                    </Dropdown>
                </div>
            </div>

            <div className="min-h-0 flex-1">
                <Table<TaxDeclarationRow>
                    columns={columns}
                    dataSource={paged}
                    rowKey="key"
                    tableLayout="fixed"
                    loading={loading}
                    pagination={false}
                    scroll={{ x: 1180 }}
                    rowSelection={{ selectedRowKeys: selectedKeys, onChange: setSelectedKeys }}
                    expandable={{
                        showExpandColumn: false,
                        expandedRowKeys: expandedKeys,
                        onExpandedRowsChange: setExpandedKeys,
                        expandedRowRender: (row) => (
                            <div className="grid grid-cols-1 gap-x-10 gap-y-1 rounded-lg bg-(--brand-soft) px-4 py-2 text-[12.5px] text-(--foreground) md:grid-cols-2">
                                <span>申报表名称：《{row.declareKind}》</span>
                                <span>
                                    税款所属期：{row.periodStart} 至 {row.periodEnd}
                                </span>
                                <span>申报状态：{row.status}</span>
                                <span>申报日期：{row.declareDate}</span>
                                <span>应补（退）税额：{fmtMoney(row.payable)}</span>
                                {row.feedback && <span>反馈：{row.feedback}</span>}
                            </div>
                        ),
                    }}
                    locale={{
                        emptyText: (
                            <span className="block py-10 text-[13px] text-(--muted-foreground)">
                                {hint ?? "当前查询区间没有申报表"}
                            </span>
                        ),
                    }}
                />
            </div>

            <div className="flex flex-wrap items-center gap-2 pb-2.5 text-[12.5px] text-(--muted-foreground)" style={{ marginTop: 16 }}>
                <span>
                    显示 {from} 到 {to}，共 {rows.length} 条记录
                </span>
                <div className="ml-auto flex items-center gap-1.5">
                    <Pagination
                        current={current}
                        pageSize={pageSize}
                        total={rows.length}
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
                            aria-label="刷新"
                            onClick={() => void load(range, { promptLogin: true })}
                            icon={<ReloadOutlined />}
                        />
                    </Tooltip>
                </div>
            </div>

            <div className="flex flex-wrap items-center gap-3 border-t border-(--border) py-2.5 text-[13px] text-(--muted-foreground)">
                <span>快捷入口：</span>
                <Button
                    type="link"
                    onClick={() => navigate(`/${accountSetId}/tax/login`)}
                >
                    申报信息查询
                </Button>
                <Button
                    type="link"
                    onClick={() => navigate(`/${accountSetId}/tax/login`)}
                >
                    财务报表申报信息查询
                </Button>
            </div>

            <p className="pb-5 text-[12.5px] leading-5 text-(--muted-foreground)">
                申报表取自电子税务局「申报信息查询」，按申报日期区间过滤
                {fetchedAt ? `；最近更新 ${fetchedAt}` : ""}。税局该接口不返回缴款日期，缴款情况请到【税费缴纳】核对。
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
