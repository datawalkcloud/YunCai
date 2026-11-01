/**
 * 智能报税 · 报税设置。
 *
 * 版式对齐税局客户端「报税设置」：增值税取数说明、印花税取数方式与系数、财务报表填表口径、
 * 企业所得税季报取数优先级（三个页签各自可排序），底部保存。
 * 税局取数尚未接入，配置先按账套保存在本地 localStorage，接入税务助手后改由后端账套设置持久化。
 */
import { useEffect, useState, type ReactNode } from "react";
import {
    App,
    Button,
    Checkbox,
    ConfigProvider,
    InputNumber,
    Radio,
    Select,
    Table,
    Tooltip,
} from "antd";
import type { TableColumnsType } from "antd";
import { QuestionCircleOutlined } from "@ant-design/icons";
import { useParams } from "react-router";

import { cn } from "@/lib/utils";
import { FORM_CONTROL_W } from "@/components/page/styles";

type StampDutyMode = "manual" | "auto";
type UnclosedMode = "latest" | "latestClosed" | "none";
type CashFlowSource = "ledger" | "none";
type OpeningSource = "declared" | "ledger";
type EitTabKey = "ytd" | "costSalary" | "paidSalary";
type EitSourceKey = "declared" | "ledger" | "none";

interface SettingsState {
    stampDutyMode: StampDutyMode;
    stampDutyBase: string;
    stampDutyCoefficient: number;
    unclosedMode: UnclosedMode;
    reclassEnabled: boolean;
    reclassTaxPayable: boolean;
    cashFlowSource: CashFlowSource;
    openingSource: OpeningSource;
    eitOrders: Record<EitTabKey, EitSourceKey[]>;
}

const EIT_TABS: Array<{ key: EitTabKey; label: string }> = [
    { key: "ytd", label: "本年累计金额" },
    { key: "costSalary", label: "已计入成本费用的职工薪酬" },
    { key: "paidSalary", label: "实际支付给职工的应付职工薪酬" },
];

const EIT_SOURCES: Record<EitSourceKey, { label: string; description: string; tip?: string }> = {
    declared: {
        label: "已申报财报的利润表",
        description: "取已上报的财务报表的利润表对应项目的本年累计数",
    },
    ledger: {
        label: "财务账套的利润表",
        description: "取当期财务系统财务报表的利润表对应项目的本年累计数",
        tip: "取当期财务系统财务报表的利润表对应项目的本年累计数。",
    },
    none: {
        label: "不取数",
        description: "全部按0填写，需手动编辑修改",
    },
};

const EIT_SOURCE_KEYS: EitSourceKey[] = ["declared", "ledger", "none"];

const DEFAULT_SETTINGS: SettingsState = {
    stampDutyMode: "manual",
    stampDutyBase: "purchase-sales-exclusive",
    stampDutyCoefficient: 1,
    unclosedMode: "latest",
    reclassEnabled: false,
    reclassTaxPayable: false,
    cashFlowSource: "none",
    openingSource: "declared",
    eitOrders: {
        ytd: [...EIT_SOURCE_KEYS],
        costSalary: [...EIT_SOURCE_KEYS],
        paidSalary: [...EIT_SOURCE_KEYS],
    },
};

const storageKey = (accountSetId: string) => `kuaiji.tax.settings.${accountSetId}`;

function loadSettings(accountSetId: string): SettingsState {
    try {
        const raw = window.localStorage.getItem(storageKey(accountSetId));
        if (!raw) return DEFAULT_SETTINGS;
        const saved = JSON.parse(raw) as Partial<SettingsState>;

        const eitOrders = { ...DEFAULT_SETTINGS.eitOrders };
        for (const tab of EIT_TABS) {
            const order = saved.eitOrders?.[tab.key];
            if (Array.isArray(order) && order.length === EIT_SOURCE_KEYS.length) {
                eitOrders[tab.key] = order.filter((key) => EIT_SOURCE_KEYS.includes(key));
            }
        }

        return { ...DEFAULT_SETTINGS, ...saved, eitOrders };
    } catch {
        return DEFAULT_SETTINGS;
    }
}

/** 卡片小标题：主色竖条 + 标题，与税局客户端的板块样式一致。 */
function SettingsCard({ title, children }: { title: string; children: ReactNode }) {
    return (
        <section className="rounded-[2px] border border-[#eef2f7] bg-[#fcfcfc] px-5 py-5">
            <h3 className="flex items-center gap-2 text-[14px] font-semibold text-[#333]">
                <span className="h-3.5 w-[3px] shrink-0 bg-[#2f6bff]" />
                {title}
            </h3>
            <div className="mt-5">{children}</div>
        </section>
    );
}

function HintIcon({ title }: { title: string }) {
    return (
        <Tooltip title={title}>
            <QuestionCircleOutlined className="cursor-help text-[12px] text-[#999]" />
        </Tooltip>
    );
}

export function TaxSettingsPage() {
    const { accountSetId = "" } = useParams();
    const { message } = App.useApp();

    const [settings, setSettings] = useState<SettingsState>(() => loadSettings(accountSetId));
    const [eitTab, setEitTab] = useState<EitTabKey>("ytd");

    // 工作台内切换账套时停在当前页，设置要跟着账套重新读取。
    useEffect(() => {
        setSettings(loadSettings(accountSetId));
    }, [accountSetId]);

    const patch = (next: Partial<SettingsState>) => setSettings((prev) => ({ ...prev, ...next }));

    const autoStampDuty = settings.stampDutyMode === "auto";
    const eitOrder = settings.eitOrders[eitTab];

    const setEitOrder = (order: EitSourceKey[]) =>
        patch({ eitOrders: { ...settings.eitOrders, [eitTab]: order } });

    const moveEitSource = (index: number, offset: number) => {
        const target = index + offset;
        if (target < 0 || target >= eitOrder.length) return;
        const next = [...eitOrder];
        [next[index], next[target]] = [next[target], next[index]];
        setEitOrder(next);
    };

    const save = () => {
        window.localStorage.setItem(storageKey(accountSetId), JSON.stringify(settings));
        message.success("报税设置已保存。");
    };

    const eitRows = eitOrder.map((key, index) => ({
        key,
        rank: index + 1,
        ...EIT_SOURCES[key],
    }));

    const eitColumns: TableColumnsType<(typeof eitRows)[number]> = [
        {
            title: "优先级",
            dataIndex: "rank",
            width: 80,
            render: (rank: number) => <span className="block text-center">{rank}</span>,
        },
        {
            title: "取数来源",
            dataIndex: "label",
            width: 194,
            render: (label: string, row) => (
                <span className="inline-flex items-center gap-1">
                    {label}
                    {row.tip && <HintIcon title={row.tip} />}
                </span>
            ),
        },
        { title: "应用说明", dataIndex: "description", width: 455 },
        {
            title: "操作",
            key: "op",
            width: 205,
            render: (_, __, index) => (
                <span className="flex items-center gap-3">
                    <Button
                        type="link"
                        disabled={index === 0}
                        onClick={() => moveEitSource(index, -1)}
                        className="!text-[#2f6bff] hover:!underline disabled:!text-[#c9c9c9]"
                    >
                        上移
                    </Button>
                    <Button
                        type="link"
                        disabled={index === eitOrder.length - 1}
                        onClick={() => moveEitSource(index, 1)}
                        className="!text-[#2f6bff] hover:!underline disabled:!text-[#c9c9c9]"
                    >
                        下移
                    </Button>
                </span>
            ),
        },
    ];

    return (
        <ConfigProvider
            theme={{
                token: { colorPrimary: "#2f6bff", borderRadius: 2 },
                components: {
                    Table: {
                        headerBg: "#f4f7ff",
                        headerColor: "#475569",
                        borderColor: "#eef2f7",
                        rowHoverBg: "#f6f9ff",
                        cellPaddingBlock: 9,
                        cellPaddingInline: 16,
                    },
                },
            }}
        >
            <div className="flex min-h-full flex-col bg-white">
                <div className="flex h-11 shrink-0 items-center gap-2 border-b border-[#e5e5e5] px-4">
                    <span className="h-3.5 w-[3px] shrink-0 bg-[#2f6bff]" />
                    <span className="text-[14px] font-semibold text-[#333]">报税设置</span>
                </div>

                <div className="space-y-3 px-4 pb-8 pt-3.5">
                    <SettingsCard title="增值税">
                        <div className="space-y-3.5 text-[13px] leading-[22px] text-[#444]">
                            <p>
                                销项：取财务系统销项发票数据，包括未开具发票、纳税调整和其他发票类型（请先进行【一键取票】）
                            </p>
                            <p>
                                一般纳税人进项：取财务系统进项发票已认证的税款所属期一致的数据（请先进行【一键取票】）
                            </p>
                        </div>
                    </SettingsCard>

                    <SettingsCard title="印花税">
                        <div className="flex flex-wrap items-center gap-x-5 gap-y-3 text-[13px] text-[#444]">
                            <span>印花税取数：</span>
                            <Radio.Group
                                value={settings.stampDutyMode}
                                onChange={(event) => patch({ stampDutyMode: event.target.value })}
                                className="!flex items-center gap-6 [&_.ant-radio-wrapper]:!mr-0"
                            >
                                <Radio value="manual">手动填写</Radio>
                                <Radio value="auto">自动取数</Radio>
                            </Radio.Group>
                            <Select
                                value={settings.stampDutyBase}
                                disabled={!autoStampDuty}
                                onChange={(value) => patch({ stampDutyBase: value })}
                                className={FORM_CONTROL_W}
                                options={[{ value: "purchase-sales-exclusive", label: "进销项发票（不含税）" }]}
                            />
                            <span className="inline-flex items-center gap-0.5">
                                系数
                                <HintIcon title="按进销项发票金额（不含税）乘以系数，作为印花税计税依据。" />：
                            </span>
                            <span
                                className={cn(
                                    "inline-flex h-7 items-stretch overflow-hidden rounded-[2px] border border-[#dcdfe6] bg-[#fafafa]",
                                    !autoStampDuty && "cursor-not-allowed",
                                )}
                            >
                                <Button
                                    type="text"
                                    disabled={!autoStampDuty}
                                    aria-label="减小系数"
                                    onClick={() =>
                                        patch({
                                            stampDutyCoefficient: Math.max(
                                                0,
                                                Math.round((settings.stampDutyCoefficient - 0.01) * 100) / 100,
                                            ),
                                        })
                                    }
                                    className="!w-7 !border-r border-[#e8eaee] !text-[#999] hover:!bg-[#f0f0f0]"
                                >
                                    −
                                </Button>
                                <InputNumber
                                    variant="borderless"
                                    controls={false}
                                    disabled={!autoStampDuty}
                                    min={0}
                                    step={0.01}
                                    precision={2}
                                    value={settings.stampDutyCoefficient}
                                    onChange={(value) => patch({ stampDutyCoefficient: value ?? 0 })}
                                    className="[&_input]:!text-center"
                                />
                                <Button
                                    type="text"
                                    disabled={!autoStampDuty}
                                    aria-label="增大系数"
                                    onClick={() =>
                                        patch({
                                            stampDutyCoefficient:
                                                Math.round((settings.stampDutyCoefficient + 0.01) * 100) / 100,
                                        })
                                    }
                                    className="!w-7 !border-l border-[#e8eaee] !text-[#999] hover:!bg-[#f0f0f0]"
                                >
                                    +
                                </Button>
                            </span>
                        </div>
                    </SettingsCard>

                    <SettingsCard title="财务报表">
                        <div className="space-y-3 text-[13px] leading-[22px] text-[#444]">
                            <p>财报填表说明内容：</p>
                            <p>1、账套已结账到当期：取账套内财务报表数据填表</p>
                            <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
                                <span>2、账套未结账到当期：</span>
                                <Radio.Group
                                    value={settings.unclosedMode}
                                    onChange={(event) => patch({ unclosedMode: event.target.value })}
                                    className="!flex items-center gap-6 [&_.ant-radio-wrapper]:!mr-0"
                                >
                                    <Radio value="latest">取最近账期填表</Radio>
                                    <Radio value="latestClosed">取最近已结账账期填表</Radio>
                                    <Radio value="none">不取数填表</Radio>
                                </Radio.Group>
                            </div>
                            <div className="flex flex-wrap items-center gap-x-8 gap-y-2">
                                <span>资产负债表重分类：</span>
                                <span className="inline-flex items-center gap-1">
                                    <Checkbox
                                        checked={settings.reclassEnabled}
                                        onChange={(event) => patch({ reclassEnabled: event.target.checked })}
                                    >
                                        开启重分类
                                    </Checkbox>
                                    <HintIcon title="按科目余额方向重分类往来科目，避免资产、负债项目出现负数。" />
                                </span>
                                <span className="inline-flex items-center gap-1">
                                    <Checkbox
                                        checked={settings.reclassTaxPayable}
                                        onChange={(event) => patch({ reclassTaxPayable: event.target.checked })}
                                    >
                                        应交税金重分类
                                    </Checkbox>
                                    <HintIcon title="应交税费借方余额重分类至其他流动资产。" />
                                </span>
                            </div>
                            <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
                                <span className="inline-flex items-center gap-0.5">
                                    现金流量表取数来源
                                    <HintIcon title="取财务账套数据时按账套现金流量项目填表；不取数则留空手工填写。" />：
                                </span>
                                <Radio.Group
                                    value={settings.cashFlowSource}
                                    onChange={(event) => patch({ cashFlowSource: event.target.value })}
                                    className="!flex items-center gap-6 [&_.ant-radio-wrapper]:!mr-0"
                                >
                                    <Radio value="ledger">取财务账套数据</Radio>
                                    <Radio value="none">不取数填表</Radio>
                                </Radio.Group>
                            </div>
                            <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
                                <span className="inline-flex items-center gap-0.5">
                                    年初数（上年年末数） 取值来源
                                    <HintIcon title="申报表数据取上期已申报报表的期末数；财务数据取财务系统报表的期末数。" />：
                                </span>
                                <Radio.Group
                                    value={settings.openingSource}
                                    onChange={(event) => patch({ openingSource: event.target.value })}
                                    className="!flex items-center gap-6 [&_.ant-radio-wrapper]:!mr-0"
                                >
                                    <Radio value="declared">申报表数据（上期已申报）</Radio>
                                    <Radio value="ledger">财务数据（财务系统/报表）</Radio>
                                </Radio.Group>
                            </div>
                        </div>
                    </SettingsCard>

                    <SettingsCard title="企业所得税">
                        <div className="text-[13px] text-[#444]">企业所得税季报取数优先级：</div>
                        <div className="mt-4 w-full">
                            <div className="flex items-center gap-10 border-b border-[#e5e5e5] text-[13px]">
                                {EIT_TABS.map((tab) => {
                                    const active = tab.key === eitTab;
                                    return (
                                        <button
                                            key={tab.key}
                                            type="button"
                                            onClick={() => setEitTab(tab.key)}
                                            className={cn(
                                                "relative -mb-px pb-2 transition-colors",
                                                active
                                                    ? "font-semibold text-[#2f6bff]"
                                                    : "text-[#333] hover:text-[#2f6bff]",
                                            )}
                                        >
                                            {tab.label}
                                            {active && (
                                                <span className="absolute inset-x-0 -bottom-px h-[2px] bg-[#2f6bff]" />
                                            )}
                                        </button>
                                    );
                                })}
                            </div>
                            <Table
                                columns={eitColumns}
                                dataSource={eitRows}
                                rowKey="key"
                                pagination={false}
                                tableLayout="fixed"
                                rowClassName={(_, index) => (index % 2 === 1 ? "bg-[#f8fafc]" : "")}
                                className="mt-3"
                            />
                        </div>
                    </SettingsCard>

                    <div className="pt-0.5">
                        <Button
                            type="primary"
                            onClick={save}
                            className="!bg-[#2f6bff] hover:!bg-[#255ce0]"
                        >
                            保存
                        </Button>
                    </div>
                </div>
            </div>
        </ConfigProvider>
    );
}
