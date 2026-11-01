/**
 * 智能报税 · 报表查询。
 *
 * 版式对齐税局客户端「报表查询」：查询区间、温馨提示、更新/打印/批量导出，
 * 下方为申报表列表与分页，底部为快捷入口。
 * 申报表需要税务助手登录电子税务局后取回，当前展示本地演示数据；接入前
 * 不要把列表内容当作税局真实申报结果。
 */
import { useMemo, useState, type Key, type ReactNode } from "react";
import { App, Button, ConfigProvider, DatePicker, Dropdown, Input, Select, Table } from "antd";
import type { MenuProps, TableColumnsType } from "antd";
import {
    DownOutlined,
    ExclamationCircleFilled,
    LeftOutlined,
    ReloadOutlined,
    RightOutlined,
    StepBackwardOutlined,
    StepForwardOutlined,
} from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate, useParams } from "react-router";

import { cn } from "@/lib/utils";

interface ReportRow {
    key: string;
    /** 申报表名称 */
    name: string;
    periodStart: string;
    periodEnd: string;
    status: string;
    /** 申报日期，查询区间按该字段过滤 */
    declaredOn: string;
    paidOn?: string;
    amount: number;
}

/** 演示数据：与税局客户端一致的一条残保金申报表记录。 */
const MOCK_ROWS: ReportRow[] = [
    {
        key: "demo-1",
        name: "《残疾人就业保障金缴费申报表》",
        periodStart: "2025-01-01",
        periodEnd: "2025-12-31",
        status: "申报成功",
        declaredOn: "2026-09-14",
        amount: 0,
    },
];

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function TaxReportQueryPage() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { message } = App.useApp();

    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("year"), dayjs().startOf("day")]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [selectedKeys, setSelectedKeys] = useState<Key[]>([]);
    const [expandedKeys, setExpandedKeys] = useState<readonly Key[]>([]);
    const [refreshing, setRefreshing] = useState(false);

    const rows = useMemo(() => {
        const start = range[0].format("YYYY-MM-DD");
        const end = range[1].format("YYYY-MM-DD");
        return MOCK_ROWS.filter((row) => row.declaredOn >= start && row.declaredOn <= end);
    }, [range]);

    const pageCount = Math.max(1, Math.ceil(rows.length / pageSize));
    const current = Math.min(page, pageCount);
    const paged = rows.slice((current - 1) * pageSize, current * pageSize);
    const from = rows.length === 0 ? 0 : (current - 1) * pageSize + 1;
    const to = Math.min(current * pageSize, rows.length);

    async function refresh() {
        setRefreshing(true);
        // 模拟税局取数耗时；接入真实接口后替换为一次请求。
        await new Promise((resolve) => setTimeout(resolve, 500));
        setRefreshing(false);
        setPage(1);
        setSelectedKeys([]);
        setExpandedKeys([]);
        message.success(`报表已更新，共 ${rows.length} 条记录（演示数据）。`);
    }

    function exportCsv(onlySelected: boolean) {
        const target = onlySelected ? rows.filter((row) => selectedKeys.includes(row.key)) : rows;
        if (target.length === 0) {
            message.warning(onlySelected ? "请先勾选需要导出的申报表。" : "当前查询区间没有可导出的申报表。");
            return;
        }
        const header = ["申报表", "所属期", "申报状态", "申报日期", "缴款日期", "应补（退）税额"];
        const body = target.map((row) => [
            row.name,
            `${row.periodStart}至${row.periodEnd}`,
            row.status,
            row.declaredOn,
            row.paidOn ?? "-",
            fmtMoney(row.amount),
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

    const columns: TableColumnsType<ReportRow> = [
        {
            title: "申报表",
            dataIndex: "name",
            key: "name",
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
                                        "text-[11px] text-[#2f6bff] transition-transform",
                                        expanded && "rotate-90",
                                    )}
                                />
                            }
                        />
                        <span className="truncate">{name}</span>
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
        { title: "申报日期", dataIndex: "declaredOn", key: "declaredOn", width: 140 },
        {
            title: "缴款日期",
            dataIndex: "paidOn",
            key: "paidOn",
            width: 140,
            render: (value: string | undefined) => value || "-",
        },
        {
            title: "应补（退）税额",
            dataIndex: "amount",
            key: "amount",
            width: 280,
            align: "right",
            render: (value: number) => fmtMoney(value),
        },
    ];

    return (
        <div className="flex h-full flex-col px-6 pt-4">
            <div className="flex flex-wrap items-start gap-3" style={{ marginBottom: 16 }}>
                <span className="inline-flex h-8 shrink-0 items-center rounded-[2px] bg-[#eef4ff] pr-1 text-[13px] text-[#2f6bff]">
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
                            setSelectedKeys([]);
                        }}
                    />
                </span>

                <div className="flex min-w-[320px] flex-1 items-start gap-2 rounded-[2px] border border-[#f0d7a8] bg-[#fff9ef] px-3 py-2 text-[13px] leading-[22px] text-[#8a6a35]">
                    <ExclamationCircleFilled className="mt-[4px] shrink-0 text-[14px] text-[#e6a23c]" />
                    <div>
                        <div>温馨提示：1、申报表数据不全或有变更，可以点击【更新报表】按钮进行更新</div>
                        <div className="pl-[76px]">
                            2、更新完成后，未能获取到申报表，可能是税局数据存在延迟，请稍后重试
                        </div>
                    </div>
                </div>

                <div className="flex shrink-0 flex-wrap items-center gap-2">
                    <Button loading={refreshing} onClick={() => void refresh()}>
                        更新报表
                    </Button>
                    <Button onClick={() => message.info("打印封面需先从电子税务局调取申报表。")}>打印封面</Button>
                    <Button
                        onClick={() =>
                            selectedKeys.length === 0
                                ? message.warning("请先勾选需要打印的申报表。")
                                : message.info("批量打印需先从电子税务局调取申报表。")
                        }
                    >
                        批量打印
                    </Button>
                    <Dropdown menu={{ items: exportItems, onClick: ({ key }) => exportCsv(key === "selected") }}>
                        <Button type="primary">
                            批量导出 <DownOutlined className="text-[9px]" />
                        </Button>
                    </Dropdown>
                </div>
            </div>

            <div className="min-h-0 flex-1">
                <ConfigProvider
                    theme={{
                        components: {
                            Table: {
                                headerBg: "#f4f7ff",
                                headerColor: "#475569",
                                borderColor: "#eef2f7",
                                rowHoverBg: "#f6f9ff",
                            },
                        },
                    }}
                >
                    <Table<ReportRow>
                        columns={columns}
                        dataSource={paged}
                        rowKey="key"
                        tableLayout="fixed"
                        pagination={false}
                        scroll={{ x: 1180 }}
                        rowSelection={{ selectedRowKeys: selectedKeys, onChange: setSelectedKeys }}
                        expandable={{
                            showExpandColumn: false,
                            expandedRowKeys: expandedKeys,
                            onExpandedRowsChange: setExpandedKeys,
                            expandedRowRender: (row) => (
                                <div className="grid grid-cols-1 gap-x-10 gap-y-1 rounded-[2px] bg-[#f8faff] px-4 py-2 text-[12.5px] text-[#555] md:grid-cols-2">
                                    <span>申报表名称：{row.name}</span>
                                    <span>
                                        税款所属期：{row.periodStart} 至 {row.periodEnd}
                                    </span>
                                    <span>申报状态：{row.status}</span>
                                    <span>申报日期：{row.declaredOn}</span>
                                    <span>缴款日期：{row.paidOn || "-"}</span>
                                    <span>应补（退）税额：{fmtMoney(row.amount)}</span>
                                </div>
                            ),
                        }}
                        locale={{
                            emptyText: (
                                <span className="block py-10 text-[13px] text-slate-400">当前查询区间没有申报表</span>
                            ),
                        }}
                    />
                </ConfigProvider>
            </div>

            <div className="flex flex-wrap items-center gap-2 pb-2.5 text-[12.5px] text-slate-500" style={{ marginTop: 16 }}>
                <span>
                    显示 {from} 到 {to}，共 {rows.length} 条记录
                </span>
                <div className="ml-auto flex items-center gap-1.5">
                    <Select
                        value={pageSize}
                        options={[20, 50, 100].map((value) => ({ value, label: value }))}
                        onChange={(value) => {
                            setPageSize(value);
                            setPage(1);
                        }}
                    />
                    <PagerButton title="首页" disabled={current <= 1} onClick={() => setPage(1)}>
                        <StepBackwardOutlined />
                    </PagerButton>
                    <PagerButton title="上一页" disabled={current <= 1} onClick={() => setPage(current - 1)}>
                        <LeftOutlined />
                    </PagerButton>
                    <span className="ml-1 flex items-center gap-1 text-slate-500">
                        第
                        <Input
                            value={String(current)}
                            onChange={(event) => {
                                const digits = event.target.value.replace(/\D/g, "");
                                if (!digits) return;
                                setPage(Math.min(Math.max(1, Number(digits)), pageCount));
                            }}
                            className="text-center"
                        />
                        页 共{pageCount}页
                    </span>
                    <PagerButton
                        title="下一页"
                        disabled={current >= pageCount}
                        onClick={() => setPage(current + 1)}
                    >
                        <RightOutlined />
                    </PagerButton>
                    <PagerButton
                        title="末页"
                        disabled={current >= pageCount}
                        onClick={() => setPage(pageCount)}
                    >
                        <StepForwardOutlined />
                    </PagerButton>
                    <Button
                        type="text"
                        className="ml-1 !text-slate-400 hover:!text-[#2f6bff]"
                        title="刷新"
                        aria-label="刷新"
                        onClick={() => void refresh()}
                        icon={<ReloadOutlined />}
                    />
                </div>
            </div>

            <div className="flex flex-wrap items-center gap-3 border-t border-slate-200 py-2.5 text-[13px] text-slate-500">
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

            <p className="pb-5 text-[12.5px] leading-5 text-slate-400">
                当前为本地演示数据；接入电子税务局取数后，此处展示按申报日期查询到的真实申报表。
            </p>
        </div>
    );
}

/** 分页图标按钮：无内容可翻时置灰，与税局的翻页控件一致。 */
function PagerButton({
    title,
    disabled,
    children,
    onClick,
}: {
    title: string;
    disabled: boolean;
    children: ReactNode;
    onClick: () => void;
}) {
    return (
        <Button
            type="text"
            title={title}
            disabled={disabled}
            onClick={onClick}
            className={cn(
                "text-[11px]",
                disabled ? "!text-slate-200" : "!text-slate-400 hover:!text-[#2f6bff]",
            )}
        >
            {children}
        </Button>
    );
}
