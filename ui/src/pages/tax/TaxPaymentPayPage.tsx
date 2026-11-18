/**
 * 智能报税 · 税费缴纳（缴税）。
 *
 * 按税款所属期列出当期应缴的税款，支持勾选批量缴款与跳转税局缴款。
 * 数据取自税局申报记录的应补（退）税额，缴款状态与滞纳金以电子税务局为准。
 * 登录态过期时弹窗补一次短信验证码，成功后自动重取。
 * 在线扣款仍需三方协议，批量缴款只做校验提示，不产生真实扣款。
 */
import { useCallback, useEffect, useMemo, useState, type Key } from "react";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { Card, App, Button, DatePicker, Select, Table } from "antd";
import type { TableColumnsType } from "antd";
import {
    CalendarOutlined,
    DownOutlined,
    ExclamationCircleFilled,
    InfoCircleOutlined,
    LoginOutlined,
} from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { useNavigate } from "react-router";

import { useTaxPeriod } from "./tax-period";
import { useAuth } from "@/auth/AuthContext";
import { fetchTaxPayables, type TaxPayableRow } from "@/lib/tax-portal";

import { TaxLoginPrompt } from "./TaxLoginPrompt";
import { setPath } from "@/accountSets/url-id";

type PayRow = TaxPayableRow;

const STATUS_OPTIONS = [
    { value: "all", label: "全部" },
    { value: "unpaid", label: "未缴款" },
    { value: "paid", label: "已缴款" },
];

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function TaxPaymentPayPage() {
    const period = useTaxPeriod();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const navigate = useNavigate();
    const { message } = App.useApp();
    const { token } = useAuth();

    const [rows, setRows] = useState<PayRow[]>([]);
    const [month, setMonth] = useState<Dayjs>(() => dayjs(period.startDate));
    const [status, setStatus] = useState("all");
    const [selectedKeys, setSelectedKeys] = useState<Key[]>([]);
    const [loading, setLoading] = useState(false);
    const [needLogin, setNeedLogin] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    const [hint, setHint] = useState<string | null>(null);

    const monthKey = month.format("YYYYMM");

    const load = useCallback(
        async (targetMonth: string, opts: { promptLogin?: boolean } = {}) => {
            if (!accountSetId) return;
            setLoading(true);
            try {
                const result = await fetchTaxPayables(accountSetId, token, targetMonth);
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
                // 取自申报记录时税局不说缴款状态，这句提示要留在页面上。
                setHint(result.message ?? null);
                setSelectedKeys([]);
            } catch (err) {
                setRows([]);
                setHint(err instanceof Error && err.message ? err.message : "读取税局应缴税款失败，请稍后重试。");
            } finally {
                setLoading(false);
            }
        },
        [accountSetId, token],
    );

    useEffect(() => {
        void load(monthKey);
    }, [load, monthKey]);

    const filtered = useMemo(
        () =>
            rows.filter((row) => {
                if (status === "all") return true;
                const paid = row.status.includes("已缴");
                // 状态为空的记录取自申报表，缴款与否税局没给，按「未缴款」展示。
                return status === "paid" ? paid : !paid;
            }),
        [rows, status],
    );

    const toTaxLogin = () => navigate(setPath(accountSetId, "/tax/login"));

    function search() {
        setSelectedKeys([]);
        void load(monthKey, { promptLogin: true });
    }

    function paySelected() {
        if (selectedKeys.length === 0) {
            message.warning("请先勾选需要缴款的申报记录。");
            return;
        }
        message.info("在线缴款需先开通三方协议，当前请点击「税局缴款」前往电子税务局办理。");
    }

    const columns: TableColumnsType<PayRow> = [
        {
            title: "序号",
            key: "serial",
            width: 60,
            align: "center",
            render: (_, __, index) => index + 1,
        },
        { title: "申报种类", dataIndex: "kind", key: "kind", width: 260 },
        { title: "所属期起", dataIndex: "periodStart", key: "periodStart", width: 110 },
        { title: "所属期止", dataIndex: "periodEnd", key: "periodEnd", width: 110 },
        { title: "申报期限", dataIndex: "deadline", key: "deadline", width: 110 },
        {
            title: "应补（退）税额",
            dataIndex: "refund",
            key: "refund",
            width: 130,
            align: "right",
            render: (value: number) => fmtMoney(value),
        },
        {
            title: "应缴税额",
            dataIndex: "payable",
            key: "payable",
            width: 130,
            align: "right",
            render: (value: number) => fmtMoney(value),
        },
        {
            title: "滞纳金、利息、违约金",
            dataIndex: "surcharge",
            key: "surcharge",
            width: 170,
            align: "right",
            render: (value: number) => fmtMoney(value),
        },
        {
            title: "缴税状态",
            dataIndex: "status",
            key: "status",
            width: 100,
            render: (value: string) => value || "未确认",
        },
        {
            title: "操作",
            key: "op",
            width: 90,
            render: (_, row) => (
                <Button
                    type="link"
                    disabled={row.status.includes("已缴")}
                    onClick={() => {
                        setSelectedKeys([row.key]);
                        paySelected();
                    }}
                >
                    缴款
                </Button>
            ),
        },
    ];

    return (
        <Card>
            <div className="flex flex-wrap items-center gap-x-5 gap-y-3" style={{ marginBottom: 16 }}>
                <span className="flex items-center gap-2 text-[13px] text-(--foreground)">
                    税款所属期：
                    <DatePicker
                        picker="month"
                        allowClear={false}
                        value={month}
                        format="YYYY-MM"
                        suffixIcon={<CalendarOutlined />}
                        className="w-[130px]"
                        onChange={(value) => {
                            if (value) {
                                setMonth(value);
                                setSelectedKeys([]);
                            }
                        }}
                    />
                </span>
                <span className="flex items-center gap-2 text-[13px] text-(--foreground)">
                    缴税状态：
                    <Select
                        value={status}
                        options={STATUS_OPTIONS}
                        suffixIcon={<DownOutlined className="text-[10px]" />}
                        className="w-[200px]"
                        onChange={(value) => {
                            setStatus(value);
                            setSelectedKeys([]);
                        }}
                    />
                </span>
                {needLogin && (
                    <Button icon={<LoginOutlined />} onClick={() => setLoginOpen(true)}>
                        登录电子税务局
                    </Button>
                )}
                <Button type="primary" loading={loading} onClick={search}>
                    搜索
                </Button>
                <span className="flex items-center gap-1.5 rounded-md border border-(--border) px-3 py-1 text-[12.5px] text-(--muted-foreground)">
                    <ExclamationCircleFilled className="text-(--warn-icon)" />
                    仅支持当期税种进行缴税，往期或未开通三方协议的请前往税局缴款
                </span>
                <div className="ml-auto flex items-center gap-2">
                    <Button type="primary" onClick={paySelected}>
                        批量缴款
                    </Button>
                    <Button onClick={toTaxLogin}>税局缴款</Button>
                </div>
            </div>

            {hint && rows.length > 0 && (
                <div className="mb-2 flex items-center gap-1.5 text-[12.5px] text-(--muted-foreground)">
                    <InfoCircleOutlined className="text-(--brand)" />
                    {hint}
                </div>
            )}

            <div className="min-h-0 flex-1">
                <Table<PayRow>
                    rowSelection={{
                        selectedRowKeys: selectedKeys,
                        onChange: (keys) => setSelectedKeys(keys),
                    }}
                    columns={columns}
                    dataSource={filtered}
                    rowKey="key"
                    tableLayout="fixed"
                    pagination={false}
                    scroll={{ x: 1330 }}
                    loading={loading}
                    locale={{
                        emptyText: (
                            <span className="block py-16 text-[13px] text-(--muted-foreground)">{hint ?? "暂无数据"}</span>
                        ),
                    }}
                />
            </div>

            <TaxLoginPrompt
                open={loginOpen}
                accountSetId={accountSetId}
                reason={hint ?? undefined}
                onClose={() => setLoginOpen(false)}
                onSuccess={() => {
                    setLoginOpen(false);
                    void load(monthKey);
                }}
            />
        </Card>
    );
}
