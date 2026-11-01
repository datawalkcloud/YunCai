/**
 * 智能报税 · 税种申报。
 *
 * 页面结构与税局一致：申报期限提示条、申报结果表格、税局直达专区。
 * 税种信息要登录电子税务局后由助手取回，当前尚未接入，因此表格为空并引导完成初始化；
 * 申报 / 更新申报结果按钮先给出提示，避免被误解为已经接通税局。
 */
import { useState, type ReactNode } from "react";
import { App, Button, Select, Table } from "antd";
import type { TableColumnsType } from "antd";
import {
    AppstoreOutlined,
    BookOutlined,
    ContainerOutlined,
    FileTextOutlined,
    ProfileOutlined,
} from "@ant-design/icons";
import { useNavigate, useParams } from "react-router";

import type { AccountingPeriod } from "@/accountSets/useCurrentAccountSet";

interface DeclareRow {
    key: string;
    kind: string;
    start: string;
    end: string;
    deadline: string;
    status: string;
    payable: number;
    declareDate?: string;
    feedback?: string;
}

const pad = (value: number) => String(value).padStart(2, "0");

/** 申报截止日按次月 15 日推算，遇周末顺延到下一个工作日；法定节假日调整以税局公告为准。 */
function filingDeadline(period: AccountingPeriod): Date {
    const deadline = new Date(period.year, period.month, 15);
    while (deadline.getDay() === 0 || deadline.getDay() === 6) {
        deadline.setDate(deadline.getDate() + 1);
    }
    return deadline;
}

/** 税局直达专区的入口；当前只能经税务助手打开电子税务局，无法直达具体业务页。 */
const DIRECT_GROUPS: Array<{
    key: string;
    title: string;
    color: string;
    icon: ReactNode;
    items: string[];
}> = [
    {
        key: "quick",
        title: "快捷入口",
        color: "#f59e0b",
        icon: <AppstoreOutlined />,
        items: ["电子税务局", "税种核定（应申报税费种信息报告）", "征纳互动", "账户中心", "申报智能自检"],
    },
    {
        key: "common",
        title: "常办业务",
        color: "#2f6bff",
        icon: <ProfileOutlined />,
        items: ["社保业务", "财产和行为税合并申报", "办税进度及结果查询", "出口退税申报"],
    },
    {
        key: "einvoice",
        title: "数电票业务",
        color: "#10b981",
        icon: <FileTextOutlined />,
        items: ["开票业务", "票种核定（发票用票需求申请）", "税务数字账户"],
    },
];

const columns: TableColumnsType<DeclareRow> = [
    { title: "序号", key: "index", width: 56, render: (_, __, index) => index + 1 },
    { title: "申报种类", dataIndex: "kind", key: "kind", width: 300 },
    { title: "所属期起", dataIndex: "start", key: "start", width: 110 },
    { title: "所属期止", dataIndex: "end", key: "end", width: 110 },
    { title: "申报期限", dataIndex: "deadline", key: "deadline", width: 110 },
    { title: "操作", key: "op", width: 100 },
    { title: "申报状态", dataIndex: "status", key: "status", width: 180 },
    {
        title: "应补(退)税额",
        dataIndex: "payable",
        key: "payable",
        width: 140,
        align: "right",
    },
    { title: "申报日期", dataIndex: "declareDate", key: "declareDate", width: 110 },
    { title: "反馈", dataIndex: "feedback", key: "feedback" },
];

export function TaxDeclarePage({
    period,
    onOpenTaxKinds,
}: {
    period: AccountingPeriod;
    onOpenTaxKinds: () => void;
}) {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { message } = App.useApp();

    const toTaxLogin = () => navigate(`/${accountSetId}/tax/login`);

    const deadline = filingDeadline(period);
    const deadlineText = `${deadline.getFullYear()}-${pad(deadline.getMonth() + 1)}-${pad(deadline.getDate())}`;
    // 进页面时取一次当天日期给提示条用，避免在渲染里读系统时间。
    const [today] = useState(() => {
        const date = new Date();
        date.setHours(0, 0, 0, 0);
        return date;
    });
    const remaining = Math.round((deadline.getTime() - today.getTime()) / 86_400_000);

    const notInitialized = () =>
        message.warning("税务初始化尚未完成，请先完成【初始化】并登录电子税务局。");

    return (
        <div className="px-6 py-4">
            <div className="flex flex-wrap items-start justify-between gap-4" style={{ marginBottom: 16 }}>
                <div className="flex max-w-[860px] items-start gap-2 rounded-[2px] border border-[#f0d7a8] bg-[#fff9ef] px-4 py-3 text-[13px] leading-6 text-[#8a6a35]">
                    <ContainerOutlined className="mt-1 shrink-0 text-[15px] text-[#e6a23c]" />
                    <div>
                        <div>
                            温馨提示：1、当期税款所属期的申报时间在{" "}
                            <span className="font-medium text-[#2f6bff]">{deadlineText}</span> 截止，
                            {remaining > 0 ? (
                                <>
                                    还有 <span className="font-medium text-[#e6a23c]">{remaining}</span> 天，
                                    请及时进行申报
                                </>
                            ) : remaining === 0 ? (
                                "今天是申报截止日，请及时进行申报"
                            ) : (
                                <span className="text-[#ef4444]">已超期 {-remaining} 天，请尽快办理</span>
                            )}
                        </div>
                        <div>
                            2、如需增加或删除【税种申报】下的税种信息，可在{" "}
                            <Button
                                type="link"
                                className="!text-[#2f6bff]"
                                onClick={onOpenTaxKinds}
                            >
                                【税费种管理】
                            </Button>{" "}
                            中进行调整
                        </div>
                        <div>
                            3、系统报税或更新数据过程中{" "}
                            <span className="text-[#ef4444]">请勿登录电子税务局</span>
                        </div>
                    </div>
                </div>

                <div className="flex shrink-0 flex-wrap items-center gap-3">
                    <Button
                        type="text"
                        onClick={() => message.info("申报前请先在【税费种管理】确认税费种核定，报税过程中请勿登录电子税务局。")}
                        className="!text-slate-600 hover:!text-[#2f6bff]"
                        icon={
                            <span className="flex h-4 w-4 items-center justify-center rounded-[2px] bg-[#10b981] text-[10px] text-white">
                                <BookOutlined />
                            </span>
                        }
                    >
                        帮助手册
                    </Button>
                    <span className="flex items-center gap-1 text-[13px] text-slate-500">
                        征收类型：
                        <Select disabled className="w-[120px]" placeholder="未初始化" />
                    </span>
                    <Button onClick={notInitialized}>更新申报结果</Button>
                    <Button type="primary" onClick={notInitialized}>
                        申报
                    </Button>
                </div>
            </div>

            <div>
                <Table<DeclareRow>
                    columns={columns}
                    dataSource={[]}
                    rowKey="key"
                    pagination={false}
                    tableLayout="fixed"
                    scroll={{ x: 1200 }}
                    locale={{
                        emptyText: (
                            <div className="py-24 text-center text-[13px] text-slate-500">
                                未完成税务初始化，请点击完成
                                <Button
                                    type="link"
                                    onClick={toTaxLogin}
                                >
                                    【初始化】
                                </Button>
                            </div>
                        ),
                    }}
                />
            </div>

            <section className="mt-4 rounded-[2px] border border-slate-200">
                <div className="flex items-center gap-2 px-5 pb-1 pt-4">
                    <span className="text-[14px] font-medium text-slate-800">税局直达专区</span>
                    <span className="rounded-[2px] bg-[#ef4444] px-1.5 py-[1px] text-[10px] font-medium leading-4 text-white">
                        HOT
                    </span>
                </div>
                <div className="grid grid-cols-1 gap-6 px-5 py-4 md:grid-cols-3">
                    {DIRECT_GROUPS.map((group) => (
                        <div key={group.key}>
                            <div className="mb-2 flex items-center gap-2 text-[13.5px] font-medium text-slate-700">
                                <span style={{ color: group.color }}>{group.icon}</span>
                                {group.title}
                            </div>
                            <ul className="space-y-1.5">
                                {group.items.map((item) => (
                                    <li key={item}>
                                        <Button
                                            type="link"
                                            title="前往一键登录税局后办理"
                                            onClick={toTaxLogin}
                                            className="!text-left hover:!underline"
                                        >
                                            {item} <span className="text-[#2f6bff]/70">›</span>
                                        </Button>
                                    </li>
                                ))}
                            </ul>
                        </div>
                    ))}
                </div>
            </section>
        </div>
    );
}
