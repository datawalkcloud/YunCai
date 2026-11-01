/**
 * 智能报税 · 税费缴纳（下载完税证明）。
 *
 * 页面结构与税局一致：按开具日期查询已开具的完税证明，勾选后打包下载。
 * 完税证明由税局开具后返回，数据需税务助手登录电子税务局取回，当前尚未接入，
 * 列表保持空态；「下载」只做勾选校验，不产生真实文件。
 */
import { useState } from "react";
import type { Key } from "react";
import { App, Button, ConfigProvider, DatePicker, InputNumber, Select, Table } from "antd";
import type { TableColumnsType } from "antd";
import {
    LeftOutlined,
    ReloadOutlined,
    RightOutlined,
    StepBackwardOutlined,
    StepForwardOutlined,
} from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";

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
    /** 开具日期 */
    issuedOn: string;
    /** 开具格式：表格式 / 文书式 */
    format: string;
}

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const columns: TableColumnsType<CertificateRow> = [
    { title: "原凭证号", dataIndex: "voucherNo", key: "voucherNo", width: "34%" },
    { title: "税款所属期起", dataIndex: "periodStart", key: "periodStart", width: "10%" },
    { title: "税款所属期止", dataIndex: "periodEnd", key: "periodEnd", width: "10%" },
    { title: "入（退）库日期", dataIndex: "paidOn", key: "paidOn", width: "11%" },
    {
        title: "实缴（退）金额",
        dataIndex: "amount",
        key: "amount",
        width: "11%",
        align: "right",
        render: (value: number) => fmtMoney(value),
    },
    { title: "开具日期", dataIndex: "issuedOn", key: "issuedOn", width: "10%" },
    { title: "开具格式", dataIndex: "format", key: "format", width: "14%" },
];

export function TaxCertificateDownloadPage({ onOpenCertificate }: { onOpenCertificate: () => void }) {
    const { message } = App.useApp();

    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("year"), dayjs().startOf("day")]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [querying, setQuerying] = useState(false);
    const [selectedRowKeys, setSelectedRowKeys] = useState<Key[]>([]);

    /** 税局取数未接入前列表恒为空；接入后按开具日期区间查询。 */
    const rows: CertificateRow[] = [];

    const total = rows.length;
    const totalPages = Math.ceil(total / pageSize);
    const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
    const to = Math.min(page * pageSize, total);

    /** 税局取数耗时以一次短暂 loading 呈现，接入后替换为真实请求。 */
    async function query() {
        setQuerying(true);
        await new Promise((resolve) => setTimeout(resolve, 300));
        setQuerying(false);
        setPage(1);
    }

    function download() {
        if (selectedRowKeys.length === 0) {
            message.warning("请先勾选需要下载的完税证明。");
            return;
        }
        message.warning("完税证明原文需税务助手登录电子税务局后取回，当前尚未接入。");
    }

    return (
        <div className="flex h-full flex-col px-6 pt-4">
            <div className="flex flex-wrap items-center gap-3" style={{ marginBottom: 16 }}>
                <span className="text-[13px] text-slate-600">开具日期：</span>
                <DatePicker
                    allowClear={false}
                    value={range[0]}
                    format="YYYY-MM-DD"
                    className="w-[132px]"
                    onChange={(value) => {
                        if (value) setRange([value.startOf("day"), range[1]]);
                    }}
                />
                <span className="text-[13px] text-slate-500">至</span>
                <DatePicker
                    allowClear={false}
                    value={range[1]}
                    format="YYYY-MM-DD"
                    className="w-[132px]"
                    onChange={(value) => {
                        if (value) setRange([range[0], value.startOf("day")]);
                    }}
                />
                <Button type="primary" autoInsertSpace={false} loading={querying} onClick={() => void query()}>
                    查询
                </Button>
                <div className="ml-auto">
                    <Button type="primary" autoInsertSpace={false} onClick={download}>
                        下载
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
                    <Table<CertificateRow>
                        columns={columns}
                        dataSource={rows}
                        rowKey="key"
                        tableLayout="fixed"
                        rowSelection={{ selectedRowKeys, onChange: setSelectedRowKeys }}
                        pagination={false}
                        locale={{
                            emptyText: (
                                <div className="flex min-h-[calc(100vh-300px)] flex-col items-center justify-center gap-1 text-[13px] leading-6 text-slate-500">
                                    <div>您所选的期间内查无数据。</div>
                                    <div>
                                        如需开具完税证明，请前往{" "}
                                        <Button type="link" onClick={onOpenCertificate}>
                                            开具完税证明
                                        </Button>{" "}
                                        。
                                    </div>
                                </div>
                            ),
                        }}
                    />
                </ConfigProvider>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 pb-2.5 text-[12.5px] text-slate-500" style={{ marginTop: 16 }}>
                <span>{`显示 ${from} 到 ${to}, 共 ${total} 条记录`}</span>
                <div className="flex items-center gap-1">
                    <Select
                        value={pageSize}
                        className="w-[62px]"
                        options={[20, 50, 100].map((value) => ({ value, label: `${value}` }))}
                        onChange={(value) => {
                            setPageSize(value);
                            setPage(1);
                        }}
                    />
                    <Button
                        type="text"
                        icon={<StepBackwardOutlined />}
                        aria-label="首页"
                        disabled
                        onClick={() => setPage(1)}
                    />
                    <Button
                        type="text"
                        icon={<LeftOutlined />}
                        aria-label="上一页"
                        disabled
                        onClick={() => setPage((current) => Math.max(1, current - 1))}
                    />
                    <span className="px-1">第</span>
                    <InputNumber
                        controls={false}
                        min={1}
                        value={page}
                        className="w-[40px]"
                        onChange={(value) => setPage(Number(value) || 1)}
                    />
                    <span className="px-1">页</span>
                    <span className="px-1">共{totalPages}页</span>
                    <Button
                        type="text"
                        icon={<RightOutlined />}
                        aria-label="下一页"
                        disabled
                        onClick={() => setPage((current) => current + 1)}
                    />
                    <Button
                        type="text"
                        icon={<StepForwardOutlined />}
                        aria-label="末页"
                        disabled
                        onClick={() => setPage(Math.max(1, totalPages))}
                    />
                    <Button
                        type="text"
                        icon={<ReloadOutlined />}
                        aria-label="刷新"
                        onClick={() => void query()}
                    />
                </div>
            </div>
        </div>
    );
}
