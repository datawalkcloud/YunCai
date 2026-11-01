/**
 * 智能报税 · 税费缴纳（缴款信息查询）。
 *
 * 表格字段与税局「缴款信息查询」一致；当前数据为本地模拟（buildMockPayments），
 * 仅用于页面演示与视觉验收。真实缴款信息需要税务助手登录电子税务局取回，
 * 接入前端到真实税局接口前，不要据此做账或对外导出。
 */
import { useMemo, useState } from "react";
import { App, Button, ConfigProvider, DatePicker, InputNumber, Select, Table } from "antd";
import type { TableColumnsType } from "antd";
import {
    DownOutlined,
    InfoCircleOutlined,
    LeftOutlined,
    ReloadOutlined,
    RightOutlined,
    VerticalLeftOutlined,
    VerticalRightOutlined,
} from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate, useParams } from "react-router";

interface PaymentRow {
    key: string;
    /** 征收项目，如增值税 */
    project: string;
    /** 征收品目，如销售货物(13%) */
    item: string;
    periodStart: string;
    periodEnd: string;
    amount: number;
    /** 正税 / 滞纳金 / 罚款 */
    taxKind: string;
    paidOn: string;
    method: string;
    voucherKind: string;
    voucherNo: string;
    eTicketNo: string;
}

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const PAGE_SIZES = [20, 50, 100];

const pad2 = (value: number) => String(value).padStart(2, "0");

/** 模拟缴款记录：本年 1 月起，按月生成增值税及附加，按季生成印花税与企业所得税。 */
function buildMockPayments(year: number): PaymentRow[] {
    const rows: PaymentRow[] = [];
    let seq = 0;

    const push = (
        periodMonth: number,
        paidOn: Dayjs,
        project: string,
        item: string,
        amount: number,
        taxKind = "正税",
    ) => {
        seq += 1;
        const periodStart = dayjs(`${year}-${pad2(periodMonth)}-01`);
        rows.push({
            key: `mock-${seq}`,
            project,
            item,
            periodStart: periodStart.format("YYYY-MM-DD"),
            periodEnd: periodStart.endOf("month").format("YYYY-MM-DD"),
            amount: Math.round(amount * 100) / 100,
            taxKind,
            paidOn: paidOn.format("YYYY-MM-DD"),
            method: seq % 3 === 0 ? "银行端查询缴税" : "三方协议",
            voucherKind: "电子缴款书",
            voucherNo: `${year}${pad2(periodMonth)}${String(seq).padStart(6, "0")}`,
            eTicketNo: `${year}${pad2(periodMonth)}${paidOn.format("MMDD")}${String(100000 + seq)}`,
        });
    };

    // 缴款日期取所属期次月 15 日；晚于查询截止日的记录会被默认区间（年初至今天）过滤掉。
    for (let month = 1; month <= 12; month += 1) {
        const paidOn = dayjs(`${year}-${pad2(month)}-01`).add(1, "month").date(15);
        const vat = 12800 + month * 1375.5;
        push(month, paidOn, "增值税", month % 2 === 1 ? "销售货物(13%)" : "提供劳务(6%)", vat);
        push(month, paidOn, "城市维护建设税", "市区(7%)", vat * 0.07);
        push(month, paidOn, "教育费附加", "教育费附加(3%)", vat * 0.03);
        push(month, paidOn, "地方教育附加", "地方教育附加(2%)", vat * 0.02);

        if (month % 3 === 0) {
            const quarterStart = month - 2;
            push(quarterStart, paidOn, "印花税", "购销合同(0.03%)", 1860 + month * 42.5);
            push(quarterStart, paidOn, "企业所得税", "应纳税所得额(25%)", 18600 + month * 640);
        }
    }

    return rows.sort((a, b) => a.paidOn.localeCompare(b.paidOn));
}

export function TaxPaymentPage() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { message } = App.useApp();

    const paymentRows = useMemo(() => buildMockPayments(dayjs().year()), []);
    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("year"), dayjs().startOf("day")]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [refreshing, setRefreshing] = useState(false);

    const rows = useMemo(() => {
        const start = range[0].format("YYYY-MM-DD");
        const end = range[1].format("YYYY-MM-DD");
        return paymentRows.filter((row) => row.paidOn >= start && row.paidOn <= end);
    }, [paymentRows, range]);

    const pageCount = Math.ceil(rows.length / pageSize);
    const from = rows.length === 0 ? 0 : (page - 1) * pageSize + 1;
    const to = Math.min(page * pageSize, rows.length);

    async function refreshPayments() {
        setRefreshing(true);
        // 模拟税局取数耗时；接入真实接口后替换为一次请求。
        await new Promise((resolve) => setTimeout(resolve, 400));
        setRefreshing(false);
        setPage(1);
        message.success(`已更新缴款信息，共 ${rows.length} 条（演示数据）。`);
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
                <span className="inline-flex items-center rounded-[2px] bg-[#eef4ff] pr-1 text-[13px] text-[#2f6bff]">
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
                    <Button loading={refreshing} onClick={() => void refreshPayments()}>
                        更新缴款信息
                    </Button>
                    <Button type="primary" onClick={exportCsv}>
                        导出列表
                    </Button>
                </div>
            </div>

            <div>
                <ConfigProvider
                    theme={{
                        components: {
                            Table: { headerBg: "#f4f7ff", headerColor: "#475569", borderColor: "#eef2f7", rowHoverBg: "#f6f9ff" },
                        },
                    }}
                >
                    <Table<PaymentRow>
                        columns={columns}
                        dataSource={rows.slice((page - 1) * pageSize, page * pageSize)}
                        rowKey="key"
                        tableLayout="fixed"
                        scroll={{ x: 1400 }}
                        locale={{ emptyText: <span className="block py-10 text-[13px] text-slate-400">暂无数据</span> }}
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
                        loading={refreshing}
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
                当前为本地演示数据；接入电子税务局取数后，此处展示按缴款日期查询到的真实缴款记录。
            </p>
        </div>
    );
}
