/**
 * 智能报税 · 税费缴纳（下载完税证明）。
 *
 * 页面结构与税局一致：按开具日期查询已开具的完税证明，勾选后打包下载。
 * 列表数据由服务器上的税局会话直连数字账户「证明信息查询」接口取回；登录态过期时弹窗补一次短信验证码，
 * 成功后自动重取。证明原文的下载仍在电子税务局办理，「下载」只做勾选校验。
 */
import { useCallback, useEffect, useRef, useState } from "react";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import type { Key } from "react";
import { Card, App, Button, DatePicker, InputNumber, Select, Table } from "antd";
import type { TableColumnsType } from "antd";
import {
    LeftOutlined,
    LoginOutlined,
    ReloadOutlined,
    RightOutlined,
    StepBackwardOutlined,
    StepForwardOutlined,
} from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate } from "react-router";

import { useAuth } from "@/auth/AuthContext";
import { fetchTaxCertificates, type TaxCertificateRow } from "@/lib/tax-portal";

import { TaxLoginPrompt } from "./TaxLoginPrompt";
import { setPath } from "@/accountSets/url-id";

type CertificateRow = TaxCertificateRow;

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const columns: TableColumnsType<CertificateRow> = [
    { title: "证明名称", dataIndex: "name", key: "name", width: "18%" },
    { title: "开具日期", dataIndex: "issuedOn", key: "issuedOn", width: "11%" },
    {
        title: "开具金额",
        dataIndex: "amount",
        key: "amount",
        width: "11%",
        align: "right",
        render: (value: number) => fmtMoney(value),
    },
    { title: "证明样式", dataIndex: "format", key: "format", width: "18%" },
    { title: "票证号码", dataIndex: "voucherNo", key: "voucherNo", width: "18%" },
    { title: "开具税务机关", dataIndex: "authority", key: "authority", width: "24%" },
];

export function TaxCertificateDownloadPage() {
    const accountSetId = useCurrentAccountSetId() ?? "";
    const navigate = useNavigate();
    const { message } = App.useApp();
    const { token } = useAuth();

    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().startOf("year"), dayjs().startOf("day")]);
    const [rows, setRows] = useState<CertificateRow[]>([]);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [querying, setQuerying] = useState(false);
    const [selectedRowKeys, setSelectedRowKeys] = useState<Key[]>([]);
    const [needLogin, setNeedLogin] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    const [hint, setHint] = useState<string | null>(null);

    const requestId = useRef(0);

    const load = useCallback(
        async (target: [Dayjs, Dayjs], opts: { promptLogin?: boolean } = {}) => {
            if (!accountSetId) return;
            const id = ++requestId.current;
            setQuerying(true);
            try {
                const result = await fetchTaxCertificates(
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
                setHint(list.length === 0 ? result.message ?? "您所选的期间内查无数据。" : null);
                setPage(1);
                setSelectedRowKeys([]);
            } catch (err) {
                if (id !== requestId.current) return;
                setRows([]);
                setHint(err instanceof Error && err.message ? err.message : "读取税局完税证明失败，请稍后重试。");
            } finally {
                if (id === requestId.current) setQuerying(false);
            }
        },
        [accountSetId, token],
    );

    // 起止日期是两个独立选择器，改日期不立刻取数，等用户点「查询」再跑一次。
    useEffect(() => {
        void load([dayjs().startOf("year"), dayjs().startOf("day")]);
    }, [load]);

    const total = rows.length;
    const totalPages = Math.max(1, Math.ceil(total / pageSize));
    const current = Math.min(page, totalPages);
    const paged = rows.slice((current - 1) * pageSize, current * pageSize);
    const from = total === 0 ? 0 : (current - 1) * pageSize + 1;
    const to = Math.min(current * pageSize, total);

    function download() {
        if (selectedRowKeys.length === 0) {
            message.warning("请先勾选需要下载的完税证明。");
            return;
        }
        message.info("已勾选完税证明；原文下载需在电子税务局「证明信息查询」中办理。");
    }

    return (
        <Card>
            <div className="flex flex-wrap items-center gap-3" style={{ marginBottom: 16 }}>
                <span className="text-[13px] text-(--foreground)">开具日期：</span>
                <DatePicker
                    allowClear={false}
                    value={range[0]}
                    format="YYYY-MM-DD"
                    className="w-[132px]"
                    onChange={(value) => {
                        if (value) setRange([value.startOf("day"), range[1]]);
                    }}
                />
                <span className="text-[13px] text-(--muted-foreground)">至</span>
                <DatePicker
                    allowClear={false}
                    value={range[1]}
                    format="YYYY-MM-DD"
                    className="w-[132px]"
                    onChange={(value) => {
                        if (value) setRange([range[0], value.startOf("day")]);
                    }}
                />
                <Button
                    type="primary"
                    autoInsertSpace={false}
                    loading={querying}
                    onClick={() => void load(range, { promptLogin: true })}
                >
                    查询
                </Button>
                <div className="ml-auto flex items-center gap-2">
                    {needLogin && (
                        <Button icon={<LoginOutlined />} onClick={() => setLoginOpen(true)}>
                            登录电子税务局
                        </Button>
                    )}
                    <Button type="primary" autoInsertSpace={false} onClick={download}>
                        下载
                    </Button>
                </div>
            </div>

            <div>
                <Table<CertificateRow>
                    columns={columns}
                    dataSource={paged}
                    rowKey="key"
                    tableLayout="fixed"
                    rowSelection={{ selectedRowKeys, onChange: setSelectedRowKeys }}
                    loading={querying}
                    pagination={false}
                    locale={{
                        emptyText: (
                            <div className="flex min-h-[calc(100vh-300px)] flex-col items-center justify-center gap-1 text-[13px] leading-6 text-(--muted-foreground)">
                                <div>{hint ?? "您所选的期间内查无数据。"}</div>
                                <div>
                                    如需开具完税证明，请前往{" "}
                                    <Button type="link" onClick={() => navigate(setPath(accountSetId, "/tax/certificate"))}>
                                        开具完税证明
                                    </Button>{" "}
                                    。
                                </div>
                            </div>
                        ),
                    }}
                />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 pb-2.5 text-[12.5px] text-(--muted-foreground)" style={{ marginTop: 16 }}>
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
                        disabled={current <= 1}
                        onClick={() => setPage(1)}
                    />
                    <Button
                        type="text"
                        icon={<LeftOutlined />}
                        aria-label="上一页"
                        disabled={current <= 1}
                        onClick={() => setPage(Math.max(1, current - 1))}
                    />
                    <span className="px-1">第</span>
                    <InputNumber
                        controls={false}
                        min={1}
                        max={totalPages}
                        value={current}
                        className="w-[40px]"
                        onChange={(value) => setPage(Math.min(Math.max(1, Number(value) || 1), totalPages))}
                    />
                    <span className="px-1">页</span>
                    <span className="px-1">共{totalPages}页</span>
                    <Button
                        type="text"
                        icon={<RightOutlined />}
                        aria-label="下一页"
                        disabled={current >= totalPages}
                        onClick={() => setPage(Math.min(totalPages, current + 1))}
                    />
                    <Button
                        type="text"
                        icon={<StepForwardOutlined />}
                        aria-label="末页"
                        disabled={current >= totalPages}
                        onClick={() => setPage(totalPages)}
                    />
                    <Button
                        type="text"
                        icon={<ReloadOutlined />}
                        aria-label="刷新"
                        loading={querying}
                        onClick={() => void load(range, { promptLogin: true })}
                    />
                </div>
            </div>

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
