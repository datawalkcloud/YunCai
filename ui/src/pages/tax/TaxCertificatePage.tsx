/**
 * 智能报税 · 税费缴纳（开具完税证明）。
 *
 * 页面结构与税局一致：开具格式、税款所属期区间、快捷入口、完税证明列表与开具 / 导出。
 * 完税证明按缴款记录开具，数据需税务助手登录电子税务局后取回，当前尚未接入，
 * 列表保持空态；开具 / 导出列表先给出提示，避免被误解为已经接通税局。
 */
import { useState } from "react";
import type { Key } from "react";
import { App, Button, DatePicker, Pagination, Select, Table, Tooltip } from "antd";
import type { TableColumnsType } from "antd";
import { DownOutlined, QuestionCircleOutlined, ReloadOutlined } from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate, useParams } from "react-router";

interface CertificateRow {
    key: string;
    /** 原凭证号（税局缴款书号） */
    voucherNo: string;
    periodStart: string;
    periodEnd: string;
    /** 入（退）库日期 */
    paidOn: string;
    /** 正数为实缴，负数为退还 */
    amount: number;
}

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

    const [format, setFormat] = useState<"table" | "document">("table");
    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("year"), dayjs().startOf("day")]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [selectedRowKeys, setSelectedRowKeys] = useState<Key[]>([]);

    /** 税局取数未接入前列表恒为空；接入后按缴款日期区间查询。 */
    const rows: CertificateRow[] = [];

    const total = rows.length;
    const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
    const to = Math.min(page * pageSize, total);

    const notConnected = () =>
        message.warning("完税证明数据需税务助手登录电子税务局后取回，请先前往【一键登录税局】。");

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
                <span className="inline-flex items-center rounded-[2px] border border-[#2f6bff] bg-white pr-1">
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
                    <Button type="primary" onClick={notConnected}>
                        开具
                    </Button>
                    <Button onClick={notConnected}>导出列表</Button>
                </div>
            </div>

            <div className="min-h-0 flex-1">
                <Table<CertificateRow>
                    columns={columns}
                    dataSource={rows}
                    rowKey="key"
                    tableLayout="fixed"
                    rowSelection={{ selectedRowKeys, onChange: setSelectedRowKeys }}
                    pagination={false}
                    locale={{
                        emptyText: <span className="block py-10 text-[13px] text-slate-400">暂无数据</span>,
                    }}
                />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 pb-3 text-[12.5px] text-slate-500" style={{ marginTop: 16 }}>
                <span>{`显示 ${from} 到 ${to}，共 ${total} 条记录`}</span>
                <span className="flex items-center gap-1">
                    <Pagination
                        current={page}
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
                            onClick={notConnected}
                        />
                    </Tooltip>
                </span>
            </div>

            <p className="pb-5 text-[12.5px] leading-5 text-slate-400">
                完税证明按缴款记录开具，支持表格式与文书式；数据需税务助手登录电子税务局后取回，当前尚未接入。
            </p>
        </div>
    );
}
