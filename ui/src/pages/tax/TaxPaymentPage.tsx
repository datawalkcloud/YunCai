/**
 * 智能报税 · 税费缴纳（缴款信息查询）。
 *
 * 表格字段与税局「缴款信息查询」一致。数据由服务器上的税局会话按缴款日期区间
 * 直连数字账户接口取回，登录态过期时弹窗补一次短信验证码，成功后自动重取。
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { App, Button, ConfigProvider, DatePicker, InputNumber, Select, Table } from "antd";
import type { TableColumnsType } from "antd";
import {
    DownOutlined,
    InfoCircleOutlined,
    LeftOutlined,
    LoginOutlined,
    ReloadOutlined,
    RightOutlined,
    VerticalLeftOutlined,
    VerticalRightOutlined,
} from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate, useParams } from "react-router";

import { useAuth } from "@/auth/AuthContext";
import { fetchTaxPayments, type TaxPaymentRow } from "@/lib/tax-portal";
import { TAX_THEME } from "@/lib/tax-theme";

import { TaxLoginPrompt } from "./TaxLoginPrompt";

type PaymentRow = TaxPaymentRow;

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const PAGE_SIZES = [20, 50, 100];

export function TaxPaymentPage() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { message } = App.useApp();
    const { token } = useAuth();

    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("year"), dayjs().startOf("day")]);
    const [rows, setRows] = useState<PaymentRow[]>([]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [loading, setLoading] = useState(false);
    const [needLogin, setNeedLogin] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    const [hint, setHint] = useState<string | null>(null);

    /** 改区间会连着发几次请求，只认最后发起的那一次。 */
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
                setHint(list.length === 0 ? result.message ?? "所选缴款日期区间没有缴款记录。" : result.message ?? null);
                setPage(1);
            } catch (err) {
                if (id !== requestId.current) return;
                setRows([]);
                setHint(err instanceof Error && err.message ? err.message : "读取税局缴款信息失败，请稍后重试。");
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
    const from = rows.length === 0 ? 0 : (page - 1) * pageSize + 1;
    const to = Math.min(page * pageSize, rows.length);

    async function refreshPayments() {
        await load(range, { promptLogin: true });
        setPage(1);
    }

    function exportCsv() {
        if (rows.length === 0) {
            message.warning("当前查询区间没有可导出的缴款记录。");
            return;
        }
        const header = [
            "序号",
            "征收项目",
            "征收品目",
            "税款所属期起",
            "税款所属期止",
            "实缴金额",
            "税款种类",
            "缴款日期",
            "税费缴纳方式",
            "票证种类",
            "票证号码",
            "电子税票号码",
        ];
        const body = rows.map((row, index) => [
            index + 1,
            row.project,
            row.item,
            row.periodStart,
            row.periodEnd,
            fmtMoney(row.amount),
            row.taxKind,
            row.paidOn,
            row.method,
            row.voucherKind,
            row.voucherNo,
            row.eTicketNo,
        ]);
        const csv = [header, ...body]
            .map((line) => line.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(","))
            .join("\r\n");
        const blob = new Blob(["\ufeff", csv], { type: "text/csv;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = `缴款信息_${range[0].format("YYYYMMDD")}-${range[1].format("YYYYMMDD")}.csv`;
        link.click();
        URL.revokeObjectURL(url);
    }

    const columns = useMemo<TableColumnsType<PaymentRow>>(() => {
        const serial: TableColumnsType<PaymentRow>[number] = {
            title: "序号",
            key: "serial",
            width: 56,
            align: "center",
            render: (_, __, index) => (page - 1) * pageSize + index + 1,
        };
        return [
            serial,
            { title: "征收项目", dataIndex: "project", key: "project", width: 110 },
            { title: "征收品目", dataIndex: "item", key: "item", width: 170 },
            { title: "税款所属期起", dataIndex: "periodStart", key: "periodStart", width: 112 },
            { title: "税款所属期止", dataIndex: "periodEnd", key: "periodEnd", width: 112 },
            {
                title: "实缴金额",
                dataIndex: "amount",
                key: "amount",
                width: 108,
                align: "right",
                render: (value: number) => fmtMoney(value),
            },
            { title: "税款种类", dataIndex: "taxKind", key: "taxKind", width: 80 },
            { title: "缴款日期", dataIndex: "paidOn", key: "paidOn", width: 112 },
            { title: "税费缴纳方式", dataIndex: "method", key: "method", width: 118 },
            { title: "票证种类", dataIndex: "voucherKind", key: "voucherKind", width: 108 },
            { title: "票证号码", dataIndex: "voucherNo", key: "voucherNo", width: 150 },
            { title: "电子税票号码", dataIndex: "eTicketNo", key: "eTicketNo", width: 164 },
        ];
    }, [page, pageSize]);

    return (
        <div className="flex h-full flex-col px-6 pt-4">
            <div className="flex flex-wrap items-center gap-3" style={{ marginBottom: 16 }}>
                <span className="inline-flex items-center rounded-md bg-[#eef4ff] pr-1 text-[13px] text-[#2f6bff]">
                    <DatePicker.RangePicker
                        variant="borderless"
                        allowClear={false}
                        value={range}
                        format="YYYY-MM-DD"
                        separator="至"
                        suffixIcon={<DownOutlined className="text-[10px]" />}
                        className="[&_input]:!text-[#2f6bff]"
                        onChange={(value) => {
                            if (!value?.[0] || !value[1]) return;
                            setRange([value[0].startOf("day"), value[1].startOf("day")]);
                            setPage(1);
                        }}
                    />
                </span>
                <span className="flex items-center gap-1.5 text-[12.5px] text-slate-500">
                    <InfoCircleOutlined className="text-[#faad14]" />
                    温馨提示：如无数据，请点击【更新缴款信息】按钮进行更新
                </span>
                <div className="ml-auto flex items-center gap-2">
                    {needLogin && (
                        <Button icon={<LoginOutlined />} onClick={() => setLoginOpen(true)}>
                            登录电子税务局
                        </Button>
                    )}
                    <Button loading={loading} onClick={() => void refreshPayments()}>
                        更新缴款信息
                    </Button>
                    <Button type="primary" onClick={exportCsv}>
                        导出列表
                    </Button>
                </div>
            </div>

            <div>
                <ConfigProvider theme={TAX_THEME}>
                    <Table<PaymentRow>
                        columns={columns}
                        dataSource={rows.slice((page - 1) * pageSize, page * pageSize)}
                        rowKey="key"
                        tableLayout="fixed"
                        scroll={{ x: 1400 }}
                        loading={loading}
                        locale={{
                            emptyText: (
                                <span className="block py-10 text-[13px] text-slate-400">{hint ?? "暂无数据"}</span>
                            ),
                        }}
                        pagination={false}
                        className="[&_.ant-table-placeholder>.ant-table-cell]:!h-[calc(100vh_-_370px)]"
                    />
                </ConfigProvider>
            </div>

            <div className="mt-auto flex flex-wrap items-center justify-between gap-2 py-3 text-[12.5px] text-slate-500">
                <span>
                    显示 {from} 到 {to}，共 {rows.length} 条记录
                </span>
                <div className="flex items-center gap-1.5">
                    <Select
                        value={pageSize}
                        options={PAGE_SIZES.map((size) => ({ value: size, label: String(size) }))}
                        onChange={(value) => {
                            setPageSize(value);
                            setPage(1);
                        }}
                        className="w-[72px]"
                    />
                    <Button
                        title="首页"
                        aria-label="首页"
                        disabled={page <= 1}
                        onClick={() => setPage(1)}
                        className="!text-slate-500 enabled:hover:!border-[#2f6bff] enabled:hover:!text-[#2f6bff] disabled:!text-slate-300"
                        icon={<VerticalRightOutlined />}
                    />
                    <Button
                        title="上一页"
                        aria-label="上一页"
                        disabled={page <= 1}
                        onClick={() => setPage(page - 1)}
                        className="!text-slate-500 enabled:hover:!border-[#2f6bff] enabled:hover:!text-[#2f6bff] disabled:!text-slate-300"
                        icon={<LeftOutlined />}
                    />
                    <span className="pl-1">第</span>
                    <InputNumber
                        min={1}
                        max={Math.max(pageCount, 1)}
                        controls={false}
                        value={page}
                        onChange={(value) => {
                            if (typeof value === "number") setPage(Math.min(Math.max(1, value), Math.max(pageCount, 1)));
                        }}
                        className="w-[44px] [&_input]:text-center"
                    />
                    <span>共 {pageCount} 页</span>
                    <Button
                        title="下一页"
                        aria-label="下一页"
                        disabled={page >= pageCount}
                        onClick={() => setPage(page + 1)}
                        className="!text-slate-500 enabled:hover:!border-[#2f6bff] enabled:hover:!text-[#2f6bff] disabled:!text-slate-300"
                        icon={<RightOutlined />}
                    />
                    <Button
                        title="末页"
                        aria-label="末页"
                        disabled={page >= pageCount}
                        onClick={() => setPage(pageCount)}
                        className="!text-slate-500 enabled:hover:!border-[#2f6bff] enabled:hover:!text-[#2f6bff] disabled:!text-slate-300"
                        icon={<VerticalLeftOutlined />}
                    />
                    <Button
                        type="text"
                        icon={<ReloadOutlined />}
                        aria-label="刷新"
                        title="刷新"
                        loading={loading}
                        onClick={() => void refreshPayments()}
                    />
                </div>
            </div>

            <div className="flex flex-wrap items-center gap-2 py-3 text-[13px] text-slate-500">
                快捷入口：
                <Button
                    type="link"
                    onClick={() => navigate(`/${accountSetId}/tax/login`)}
                >
                    缴款信息查询
                </Button>
            </div>
            <p className="pb-5 text-[12.5px] leading-5 text-slate-400">
                缴款记录取自电子税务局「缴款信息查询」，按缴款日期区间过滤。
                {needLogin ? "税局登录态已过期，登录后点击「更新缴款信息」重新取数。" : ""}
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
