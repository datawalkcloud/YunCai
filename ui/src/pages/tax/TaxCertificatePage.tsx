/**
 * 智能报税 · 税费缴纳（开具完税证明）。
 *
 * 页面结构与税局一致：开具格式、税款所属期区间、快捷入口、完税证明列表与开具 / 导出。
 * 完税证明按缴款记录开具，列表数据由服务器上的税局会话从「缴款信息查询」取回；
 * 登录态过期时弹窗补一次短信验证码，成功后自动重取。真正的开具动作仍在电子税务局办理。
 */
import { useCallback, useEffect, useRef, useState } from "react";
import type { Key } from "react";
import { App, Button, DatePicker, Pagination, Select, Table, Tooltip } from "antd";
import type { TableColumnsType } from "antd";
import { DownOutlined, LoginOutlined, QuestionCircleOutlined, ReloadOutlined } from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate, useParams } from "react-router";

import { useAuth } from "@/auth/AuthContext";
import { fetchTaxPayments, type TaxPaymentRow } from "@/lib/tax-portal";

import { TaxLoginPrompt } from "./TaxLoginPrompt";

type CertificateRow = TaxPaymentRow;

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const columns: TableColumnsType<CertificateRow> = [
    { title: "原凭证号", dataIndex: "voucherNo", key: "voucherNo", width: "36%" },
    { title: "税款所属期起", dataIndex: "periodStart", key: "periodStart", width: "14%" },
    { title: "税款所属期止", dataIndex: "periodEnd", key: "periodEnd", width: "14%" },
    { title: "入（退）库日期", dataIndex: "paidOn", key: "paidOn", width: "15%" },
    {
        title: "实缴（退）金额",
        dataIndex: "amount",
        key: "amount",
        width: "14%",
        align: "right",
        render: (value: number) => fmtMoney(value),
    },
    { title: "操作", key: "op", width: "7%", render: () => <span className="text-slate-400">--</span> },
];

export function TaxCertificatePage() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { message } = App.useApp();
    const { token } = useAuth();

    const [format, setFormat] = useState<"table" | "document">("table");
    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("year"), dayjs().startOf("day")]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [selectedRowKeys, setSelectedRowKeys] = useState<Key[]>([]);
    const [rows, setRows] = useState<CertificateRow[]>([]);
    const [loading, setLoading] = useState(false);
    const [needLogin, setNeedLogin] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    const [hint, setHint] = useState<string | null>(null);

    const requestId = useRef(0);

    const load = useCallback(
        async (target: [Dayjs, Dayjs], opts: { promptLogin?: boolean } = {}) => {
            if (!accountSetId) return;
            const id = ++requestId.current;
            setLoading(true);
            try {
                const result = await fetchTaxPayments(
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
                    if (opts.promptLogin) setLoginOpen(true);
                    return;
                }

                const list = result.rows ?? [];
                setRows(list);
                setNeedLogin(false);
                setHint(list.length === 0 ? result.message ?? "所选缴款日期区间没有可开具的缴款记录。" : null);
                setPage(1);
                setSelectedRowKeys([]);
            } catch (err) {
                if (id !== requestId.current) return;
                setRows([]);
                setHint(err instanceof Error && err.message ? err.message : "读取税局缴款记录失败，请稍后重试。");
            } finally {
                if (id === requestId.current) setLoading(false);
            }
        },
        [accountSetId, token],
    );

    useEffect(() => {
        void load(range);
    }, [load, range]);

    const total = rows.length;
    const pageCount = Math.max(1, Math.ceil(total / pageSize));
    const current = Math.min(page, pageCount);
    const paged = rows.slice((current - 1) * pageSize, current * pageSize);
    const from = total === 0 ? 0 : (current - 1) * pageSize + 1;
    const to = Math.min(current * pageSize, total);

    function issue() {
        const picked = selectedRowKeys.length;
        message.info(
            picked > 0
                ? `已勾选 ${picked} 条缴款记录；在线开具需在电子税务局办理，请点击「开具税收完税证明」进入税局。`
                : "请先勾选需要开具完税证明的缴款记录，或点击「开具税收完税证明」进入电子税务局。",
        );
    }

    function exportCsv() {
        const target = selectedRowKeys.length > 0
            ? rows.filter((row) => selectedRowKeys.includes(row.key))
            : rows;
        if (target.length === 0) {
            message.warning("当前没有可导出的缴款记录。");
            return;
        }
        const header = ["原凭证号", "税款所属期起", "税款所属期止", "入（退）库日期", "实缴（退）金额"];
        const body = target.map((row) => [
            row.voucherNo,
            row.periodStart,
            row.periodEnd,
            row.paidOn,
            fmtMoney(row.amount),
        ]);
        const csv = [header, ...body]
            .map((line) => line.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(","))
            .join("\r\n");
        const blob = new Blob(["\ufeff", csv], { type: "text/csv;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = `完税证明_缴款记录_${range[0].format("YYYYMMDD")}-${range[1].format("YYYYMMDD")}.csv`;
        link.click();
        URL.revokeObjectURL(url);
    }

    return (
        <div className="flex h-full flex-col px-6 pt-4">
            <div className="flex flex-wrap items-center gap-3" style={{ marginBottom: 16 }}>
                <span className="flex items-center text-[13px] text-slate-600">
                    开具格式
                    <Tooltip title="表格式按缴款明细逐笔开具；文书式按查询区间汇总开具。">
                        <QuestionCircleOutlined className="mx-1 text-[12px] text-slate-400" />
                    </Tooltip>
                    ：
                </span>
                <Select
                    value={format}
                    className="w-[136px]"
                    options={[
                        { value: "table", label: "表格式" },
                        { value: "document", label: "文书式" },
                    ]}
                    onChange={setFormat}
                />
                <span className="inline-flex items-center rounded-md border border-[#2f6bff] bg-white pr-1">
                    <DatePicker.RangePicker
                        variant="borderless"
                        allowClear={false}
                        value={range}
                        format="YYYY-MM-DD"
                        separator="至"
                        suffixIcon={<DownOutlined className="text-[10px] text-[#2f6bff]" />}
                        className="[&_input]:!text-[#2f6bff]"
                        onChange={(value) => {
                            if (!value?.[0] || !value[1]) return;
                            setRange([value[0].startOf("day"), value[1].startOf("day")]);
                            setPage(1);
                        }}
                    />
                </span>
                <span className="flex items-center gap-1 text-[13px] text-slate-500">
                    快捷入口：
                    <Button
                        type="link"
                        onClick={() => navigate(`/${accountSetId}/tax/login`)}
                    >
                        开具税收完税证明
                    </Button>
                </span>
                <div className="ml-auto flex items-center gap-2">
                    {needLogin && (
                        <Button icon={<LoginOutlined />} onClick={() => setLoginOpen(true)}>
                            登录电子税务局
                        </Button>
                    )}
                    <Button type="primary" onClick={issue}>
                        开具
                    </Button>
                    <Button onClick={exportCsv}>导出列表</Button>
                </div>
            </div>

            <div className="min-h-0 flex-1">
                <Table<CertificateRow>
                    columns={columns}
                    dataSource={paged}
                    rowKey="key"
                    tableLayout="fixed"
                    rowSelection={{ selectedRowKeys, onChange: setSelectedRowKeys }}
                    loading={loading}
                    pagination={false}
                    locale={{
                        emptyText: (
                            <span className="block py-10 text-[13px] text-slate-400">{hint ?? "暂无数据"}</span>
                        ),
                    }}
                />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 pb-3 text-[12.5px] text-slate-500" style={{ marginTop: 16 }}>
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
                            setPage(nextPage);
                            setPageSize(nextPageSize);
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

            <p className="pb-5 text-[12.5px] leading-5 text-slate-400">
                完税证明按缴款记录开具，支持表格式与文书式；列表取自电子税务局「缴款信息查询」，
                开具与打印请在电子税务局办理。
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
        </div>
    );
}
