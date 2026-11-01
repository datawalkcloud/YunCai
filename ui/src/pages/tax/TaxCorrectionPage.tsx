/**
 * 智能报税 · 作废与更正。
 *
 * 按申报日期查询已申报的申报表，作废后可重新申报。申报记录由税务助手登录电子税务局取回，
 * 当前尚未接入，因此查询与列表保持空态；搜索先给出提示，避免被误解为已接通税局。
 */
import { useState } from "react";
import { App, Button, DatePicker, Pagination, Table, Tooltip } from "antd";
import type { TableColumnsType } from "antd";
import { DownOutlined, ExclamationCircleFilled, ReloadOutlined } from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";

interface CorrectionRow {
    key: string;
    /** 申报表单，如增值税及附加税费申报表（一般纳税人适用） */
    form: string;
    periodStart: string;
    periodEnd: string;
    declareDate: string;
    /** 正数为应补，负数为应退 */
    payable: number;
    status: string;
}

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const columns: TableColumnsType<CorrectionRow> = [
    { title: "申报表单", dataIndex: "form", key: "form", width: "38%" },
    { title: "所属期起", dataIndex: "periodStart", key: "periodStart", width: "12%" },
    { title: "所属期止", dataIndex: "periodEnd", key: "periodEnd", width: "12%" },
    { title: "申报日期", dataIndex: "declareDate", key: "declareDate", width: "12%" },
    {
        title: "应补（退）税额",
        dataIndex: "payable",
        key: "payable",
        width: "12%",
        align: "right",
        render: (value: number) => fmtMoney(value),
    },
    { title: "申报状态", dataIndex: "status", key: "status", width: "9%" },
    { title: "操作", key: "op", width: "5%", render: () => <span className="text-slate-400">--</span> },
];

export function TaxCorrectionPage() {
    const { message } = App.useApp();

    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("day"), dayjs().startOf("day")]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);

    /** 税局取数未接入前列表恒为空；接入后按申报日期区间查询。 */
    const rows: CorrectionRow[] = [];

    const total = rows.length;
    const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
    const to = Math.min(page * pageSize, total);

    const notConnected = () =>
        message.warning("申报记录需税务助手登录电子税务局后取回，请先前往【一键登录税局】。");

    return (
        <div className="flex h-full flex-col px-6 pt-4">
            <div className="flex flex-wrap items-center gap-3" style={{ marginBottom: 16 }}>
                <span className="flex items-center gap-1.5 text-[13px] text-slate-600">
                    申报日期：
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
                </span>
                <Button type="primary" onClick={notConnected}>
                    搜索
                </Button>
                <span className="flex items-center gap-1.5 text-[12.5px] text-[#e6a23c]">
                    <ExclamationCircleFilled className="text-[#faad14]" />
                    作废成功后，请您及时重新申报，避免造成逾期未申报，被税务局处罚！
                </span>
            </div>

            <div className="min-h-0 flex-1">
                <Table<CorrectionRow>
                    columns={columns}
                    dataSource={rows}
                    rowKey="key"
                    tableLayout="fixed"
                    pagination={false}
                    locale={{
                        emptyText: <span className="block py-10 text-[13px] text-slate-400">暂无数据</span>,
                    }}
                />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 pb-3 text-[12.5px] text-slate-500" style={{ marginTop: 16 }}>
                <span>{`显示 ${from} 到 ${to}，共 ${total} 记录`}</span>
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
                申报记录需税务助手登录电子税务局后取回，当前尚未接入；作废与更正请先在电子税务局办理。
            </p>
        </div>
    );
}
