/**
 * 智能报税 · 税费种管理。
 *
 * 与税局「税费种管理」页面同构：认定提示条 + 页头征收类型 + 更新 / 新增 / 删除 + 征收项目 × 申报种类表格。
 * 认定信息由服务器的税局会话读取（需先在「一键登录税局」完成登录），页面不再使用演示数据；
 * 新增 / 删除只改本页内存，刷新后以税局核定为准。
 */
import { useCallback, useEffect, useState } from "react";
import { App, Button, ConfigProvider, DatePicker, Form, Input, Modal, Select, Table, Tooltip } from "antd";
import type { TableColumnsType } from "antd";
import { BookOutlined, ExclamationCircleFilled, LoginOutlined, QuestionCircleOutlined } from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { useAuth } from "@/auth/AuthContext";
import { fetchTaxKinds, type TaxKindRow as ApiTaxKindRow } from "@/lib/tax-portal";
import { TAX_THEME } from "@/lib/tax-theme";

import { TaxLoginPrompt } from "./TaxLoginPrompt";

interface TaxItem {
    key: string;
    /** 征收品目，如销售货物 */
    name: string;
    /** 税率 / 征收率，如 3% */
    rate: string;
    validFrom: string;
    validTo: string;
    source: string;
}

interface TaxKindRow {
    key: string;
    /** 征收项目，如增值税 */
    project: string;
    /** 征收品目：仅税局已核定的项目可下钻 */
    items?: TaxItem[];
    /** 申报种类，如增值税及附加税费申报（小规模纳税人） */
    declareKind: string;
    /** 申报期限：月 / 季 / 年 / 次 */
    period: string;
    /** 申报选项：申报 / 代扣代缴 / 代收代缴 */
    option: string;
    validFrom: string;
    validTo: string;
    /** 数据来源：税局获取 / 手动新增 */
    source: string;
}

interface AddFormValues {
    project: string;
    declareKind: string;
    period: string;
    option: string;
    validFrom: Dayjs;
    validTo: Dayjs;
}

const TAX_PERIODS = ["月", "季", "年", "次"].map((value) => ({ value, label: value }));
const TAX_OPTIONS = ["申报", "代扣代缴", "代收代缴"].map((value) => ({ value, label: value }));

/** 接口行转表格行：补上 antd 需要的 key，品目明细也各自带 key。 */
function toLocalRows(apiRows: ApiTaxKindRow[]): TaxKindRow[] {
    return apiRows.map((row, index) => ({
        key: row.key || `tax-${index}`,
        project: row.project,
        declareKind: row.declareKind,
        period: row.period,
        option: row.option,
        validFrom: row.validFrom,
        validTo: row.validTo,
        source: row.source || "税局获取",
        items: row.items?.map((item, itemIndex) => ({
            key: `${row.key || index}-${itemIndex}`,
            name: item.name,
            rate: item.rate,
            validFrom: item.validFrom,
            validTo: item.validTo,
            source: item.source || "税局获取",
        })),
    }));
}

const itemColumns: TableColumnsType<TaxItem> = [
    { title: "征收品目", dataIndex: "name", key: "name" },
    { title: "税率/征收率", dataIndex: "rate", key: "rate", width: 130 },
    { title: "认定有效期起", dataIndex: "validFrom", key: "validFrom", width: 150 },
    { title: "认定有效期止", dataIndex: "validTo", key: "validTo", width: 150 },
    { title: "数据来源", dataIndex: "source", key: "source", width: 120 },
];

export function TaxKindsPage() {
    const { message, modal } = App.useApp();
    const accountSetId = useCurrentAccountSetId();
    const { token } = useAuth();
    const [rows, setRows] = useState<TaxKindRow[]>([]);
    const [levyType, setLevyType] = useState("");
    const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
    const [refreshing, setRefreshing] = useState(false);
    const [needLogin, setNeedLogin] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    const [detail, setDetail] = useState<TaxKindRow | null>(null);
    const [addOpen, setAddOpen] = useState(false);
    const [form] = Form.useForm<AddFormValues>();

    const refreshTaxKinds = useCallback(async (silent = false) => {
        if (!accountSetId) return;
        setRefreshing(true);
        try {
            const result = await fetchTaxKinds(accountSetId, token);
            if (result.needLogin) {
                // 税局登录态缺失或过期：直接弹出登录表单，用户补一次短信验证码后继续取数。
                setRows([]);
                setLevyType("");
                setSelectedKeys([]);
                setNeedLogin(true);
                setLoginOpen(true);
                return;
            }

            const next = toLocalRows(result.rows);
            setRows(next);
            setLevyType(result.levyType?.trim() ?? "");
            setSelectedKeys([]);
            setNeedLogin(false);
            if (next.length > 0) {
                message.success(`已更新税费种认定信息，共 ${next.length} 条。`);
            } else {
                message.info(result.message || "税局没有返回税费种认定信息。");
            }
        } catch (err) {
            const text = err instanceof Error && err.message ? err.message : "获取税费种认定信息失败，请稍后重试。";
            if (text.includes("尚未登录")) {
                setNeedLogin(true);
            } else if (!silent) {
                message.error(text);
            } else {
                message.warning(text);
            }
        } finally {
            setRefreshing(false);
        }
    }, [accountSetId, message, token]);

    useEffect(() => {
        void refreshTaxKinds(true);
    }, [refreshTaxKinds]);

    const handleLoginSuccess = useCallback(() => {
        setLoginOpen(false);
        void refreshTaxKinds(true);
    }, [refreshTaxKinds]);

    function removeSelected() {
        if (selectedKeys.length === 0) {
            message.warning("请先勾选需要删除的税费种。");
            return;
        }
        modal.confirm({
            title: "删除税费种认定",
            content: `确定删除选中的 ${selectedKeys.length} 条税费种认定信息吗？删除后如仍需申报，可点击【更新税费种认定】重新获取或手动新增。`,
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: () => {
                setRows((prev) => prev.filter((row) => !selectedKeys.includes(row.key)));
                setSelectedKeys([]);
                message.success("已删除选中的税费种。");
            },
        });
    }

    function submitAdd(values: AddFormValues) {
        const row: TaxKindRow = {
            key: `manual-${Date.now()}`,
            project: values.project.trim(),
            declareKind: values.declareKind.trim(),
            period: values.period,
            option: values.option,
            validFrom: values.validFrom.format("YYYY-MM-DD"),
            validTo: values.validTo.format("YYYY-MM-DD"),
            source: "手动新增",
        };
        setRows((prev) => [...prev, row]);
        setAddOpen(false);
        message.success("已新增税费种，请核对申报期限与认定有效期。");
    }

    const columns: TableColumnsType<TaxKindRow> = [
        {
            title: "征收项目",
            dataIndex: "project",
            key: "project",
            width: 180,
            render: (project: string, row) => (
                <span className="inline-flex items-center gap-2">
                    <span>{project}</span>
                    {row.items && (
                        <Button type="link" onClick={() => setDetail(row)}>
                            征收品目
                        </Button>
                    )}
                </span>
            ),
        },
        { title: "申报种类", dataIndex: "declareKind", key: "declareKind", width: 340 },
        { title: "申报期限", dataIndex: "period", key: "period", width: 120 },
        {
            title: (
                <span className="inline-flex items-center gap-1">
                    申报选项
                    <Tooltip title="申报选项用于区分自行申报、代扣代缴等申报方式，随税费种认定信息由税局下发。">
                        <QuestionCircleOutlined className="text-[12px] text-slate-400" />
                    </Tooltip>
                </span>
            ),
            dataIndex: "option",
            key: "option",
            width: 140,
        },
        { title: "认定有效期起", dataIndex: "validFrom", key: "validFrom", width: 150 },
        { title: "认定有效期止", dataIndex: "validTo", key: "validTo", width: 150 },
        { title: "数据来源", dataIndex: "source", key: "source", width: 120 },
    ];

    return (
        <ConfigProvider theme={TAX_THEME}>
            <div className="flex h-full flex-col px-6 pt-4">
                <div className="flex flex-wrap items-start justify-between gap-4" style={{ marginBottom: 16 }}>
                    <div className="flex max-w-[1180px] items-start gap-2 rounded-md border border-[#f0d7a8] bg-[#fff9ef] px-4 py-3 text-[13px] leading-6 text-[#8a6a35]">
                        <ExclamationCircleFilled className="mt-1 shrink-0 text-[14px] text-[#e6a23c]" />
                        <div>
                            温馨提示：1、若税费种认定信息或财务会计制度备案信息有
                            <span className="mx-1 font-medium text-[#e6a23c]">变更</span>，
                            可以点击
                            <Button
                                type="link"
                                loading={refreshing}
                                onClick={() => void refreshTaxKinds()}
                                className="!text-[#2f6bff]"
                            >
                                【更新税费种认定】
                            </Button>
                            按钮进行更新；2、若更新以后，税费种认定信息显示不全，可以手动新增！
                        </div>
                        <Tooltip title="税费种认定由电子税务局核定，本页仅展示与调整申报范围。">
                            <Button
                                type="text"
                                aria-label="税费种认定说明"
                                onClick={() => message.info("税费种认定以电子税务局核定为准；变更后请更新认定信息，取数不全时可手动新增。")}
                                className="!text-[#2f6bff] hover:!bg-[#fff1d6]"
                                icon={<BookOutlined className="text-[14px]" />}
                            />
                        </Tooltip>
                    </div>

                    <div className="flex shrink-0 flex-wrap items-center gap-2">
                        {levyType && (
                            <Tooltip title="征收类型由电子税务局按户认定（服务型 / 货物型等），决定税局给企业推送哪套申报表与征收品目；随税费种认定信息一并取回，税局没回时不显示。">
                                <span className="inline-flex h-8 cursor-help items-center gap-1 rounded-md border border-[#dfe5f0] bg-white px-3 text-[13px] text-slate-500">
                                    征收类型：
                                    <span className="font-medium text-slate-700">{levyType}</span>
                                </span>
                            </Tooltip>
                        )}
                        {needLogin && (
                            <Button icon={<LoginOutlined />} onClick={() => setLoginOpen(true)}>
                                登录电子税务局
                            </Button>
                        )}
                        <Button type="primary" loading={refreshing} onClick={() => void refreshTaxKinds()}>
                            更新税费种认定
                        </Button>
                        <Button onClick={() => setAddOpen(true)}>新增</Button>
                        <Button onClick={removeSelected}>删除</Button>
                    </div>
                </div>

                <div className="min-h-0 flex-1">
                    <Table<TaxKindRow>
                        columns={columns}
                        dataSource={rows}
                        rowKey="key"
                        tableLayout="fixed"
                        loading={refreshing}
                        pagination={false}
                        scroll={{ x: 1500 }}
                        rowSelection={{
                            selectedRowKeys: selectedKeys,
                            onChange: (keys) => setSelectedKeys(keys.map(String)),
                        }}
                        locale={{ emptyText: <span className="block py-10 text-[13px] text-slate-400">{needLogin ? "尚未登录电子税务局：请点击「登录电子税务局」补一次短信验证码，随后会自动更新。" : "暂无税费种认定信息"}</span> }}
                    />
                </div>

                <p className="pb-5 pt-3 text-[12.5px] leading-5 text-slate-400">
                    税费种认定信息来自电子税务局，需先完成税局登录；新增与删除仅在本页生效，刷新后以税局核定为准。
                </p>
            </div>

            <Modal
                open={detail !== null}
                title={detail ? `${detail.project} · 征收品目` : ""}
                width={780}
                onCancel={() => setDetail(null)}
                footer={<Button onClick={() => setDetail(null)}>关闭</Button>}
            >
                {detail?.items && detail.items.length > 0 ? (
                    <>
                        <Table<TaxItem>
                            columns={itemColumns}
                            dataSource={detail.items}
                            rowKey="key"
                            tableLayout="fixed"
                            pagination={false}
                            scroll={{ x: 720 }}
                        />
                        <p className="mt-3 text-[12.5px] leading-5 text-slate-400">
                            征收品目随税费种认定信息一并从税局获取。
                        </p>
                    </>
                ) : (
                    <div className="py-8 text-center text-[13px] text-slate-400">该征收项目没有可查看的征收品目。</div>
                )}
            </Modal>

            <Modal
                open={addOpen}
                title="新增税费种"
                width={560}
                okText="确定"
                cancelText="取消"
                onOk={() => void form.submit()}
                onCancel={() => setAddOpen(false)}
                destroyOnHidden
            >
                <div className="mb-3 rounded-lg bg-[#f6f9ff] px-3 py-2 text-[12.5px] leading-5 text-slate-500">
                    手动新增用于补全税局取数不全的税费种，仅保存在本页；实际申报仍以电子税务局核定为准。
                </div>
                <Form<AddFormValues>
                    form={form}
                    layout="vertical"
                    initialValues={{
                        period: "季",
                        option: "申报",
                        validFrom: dayjs("2025-10-01"),
                        validTo: dayjs("9999-12-31"),
                    }}
                    onFinish={submitAdd}
                >
                    <Form.Item
                        name="project"
                        label="征收项目"
                        rules={[{ required: true, whitespace: true, message: "请填写征收项目" }]}
                    >
                        <Input placeholder="如：增值税" maxLength={40} />
                    </Form.Item>
                    <Form.Item
                        name="declareKind"
                        label="申报种类"
                        rules={[{ required: true, whitespace: true, message: "请填写申报种类" }]}
                    >
                        <Input placeholder="如：增值税及附加税费申报（小规模纳税人）" maxLength={80} />
                    </Form.Item>
                    <div className="grid grid-cols-2 gap-3">
                        <Form.Item name="period" label="申报期限" rules={[{ required: true, message: "请选择申报期限" }]}>
                            <Select options={TAX_PERIODS} />
                        </Form.Item>
                        <Form.Item name="option" label="申报选项" rules={[{ required: true, message: "请选择申报选项" }]}>
                            <Select options={TAX_OPTIONS} />
                        </Form.Item>
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                        <Form.Item
                            name="validFrom"
                            label="认定有效期起"
                            rules={[{ required: true, message: "请选择认定有效期起" }]}
                        >
                            <DatePicker className="!w-full" format="YYYY-MM-DD" />
                        </Form.Item>
                        <Form.Item
                            name="validTo"
                            label="认定有效期止"
                            rules={[{ required: true, message: "请选择认定有效期止" }]}
                        >
                            <DatePicker className="!w-full" format="YYYY-MM-DD" />
                        </Form.Item>
                    </div>
                </Form>
            </Modal>

            {accountSetId && (
                <TaxLoginPrompt
                    open={loginOpen}
                    accountSetId={accountSetId}
                    reason="税费种认定需要读取电子税务局，当前的税局登录状态已失效。服务器会用账套里保存的税局资料重新登录，你只需要填一次短信验证码。"
                    onClose={() => setLoginOpen(false)}
                    onSuccess={handleLoginSuccess}
                />
            )}
        </ConfigProvider>
    );
}
