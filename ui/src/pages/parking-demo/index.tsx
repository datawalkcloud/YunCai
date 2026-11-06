import {
    AppstoreOutlined,
    AuditOutlined,
    BarChartOutlined,
    CarOutlined,
    ClockCircleOutlined,
    CopyOutlined,
    FileDoneOutlined,
    FileTextOutlined,
    GiftOutlined,
    IdcardOutlined,
    LineChartOutlined,
    MonitorOutlined,
    PayCircleOutlined,
    RollbackOutlined,
    SafetyCertificateOutlined,
    SearchOutlined,
    StarFilled,
    SwapOutlined,
    ThunderboltOutlined,
    VideoCameraOutlined,
    WalletOutlined,
} from "@ant-design/icons";
import type { ReactNode } from "react";

import ParkingLayout from "@/components/layout/parking/ParkingLayout";
import type { ParkingMenu, ParkingSidebarSection, ParkingTab, ParkingUtilityApp } from "@/components/layout/parking/ParkingLayout";

const topMenus: ParkingMenu[] = [
    { key: "remote", label: "远程管控平台" },
    { key: "console", label: "控制台" },
    { key: "config", label: "配置中心" },
    { key: "ops", label: "运营管理" },
    { key: "biz", label: "业务办理" },
    { key: "finance", label: "财务管理" },
    { key: "maintain", label: "运维监测" },
    { key: "vas", label: "增值服务" },
];

const utilityApps: ParkingUtilityApp[] = [
    { key: "share", label: "分润平台", icon: <PayCircleOutlined /> },
    { key: "booth", label: "云岗亭", icon: <MonitorOutlined /> },
    { key: "coupon", label: "发券平台", icon: <GiftOutlined /> },
    { key: "visual", label: "数据可视平台", icon: <BarChartOutlined /> },
];

const sidebarSections: ParkingSidebarSection[] = [
    {
        key: "data",
        label: "",
        items: [
            { key: "revenue", label: "收益与车流分析", icon: <LineChartOutlined /> },
            { key: "strategy", label: "运营策略与风控分析", icon: <SafetyCertificateOutlined /> },
        ],
    },
    {
        key: "common",
        label: "常用功能",
        icon: <AppstoreOutlined />,
        items: [
            { key: "in-lot", label: "场内订单", icon: <FileTextOutlined /> },
            { key: "exit-order", label: "出场订单", icon: <FileDoneOutlined /> },
            { key: "arrears", label: "欠费订单", icon: <ThunderboltOutlined /> },
            { key: "daily-fee", label: "收费日报表", icon: <BarChartOutlined /> },
            { key: "card-renew", label: "车主办卡/续费", icon: <IdcardOutlined /> },
            { key: "card-audit", label: "办卡记录/审核", icon: <AuditOutlined /> },
            { key: "vehicle-change", label: "车辆变更审核记录", icon: <SwapOutlined /> },
            { key: "stored-merchant", label: "储值商户", icon: <WalletOutlined /> },
            { key: "coupon-merchant", label: "买券商户", icon: <GiftOutlined /> },
            { key: "refund-record", label: "退款记录", icon: <RollbackOutlined /> },
            { key: "settlement", label: "结算付款记录", icon: <PayCircleOutlined /> },
            { key: "device-monitor", label: "设备运行实时监测", icon: <MonitorOutlined /> },
        ],
    },
];

const tabs: ParkingTab[] = [
    { key: "t1", label: "运营策略与风控分析" },
    { key: "t2", label: "运营策略与风控分析" },
    { key: "t3", label: "收益与车流分析", active: true },
    { key: "t4", label: "运营策略与风控分析" },
    { key: "t5", label: "运营策略与风控分析" },
    { key: "t6", label: "运营策略与风控分析" },
];

export default function ParkingDemo() {
    return (
        <ParkingLayout
            menus={topMenus}
            activeMenu="ops"
            pillMenu="remote"
            sections={sidebarSections}
            selectedKey="exit-order"
            tabs={tabs}
            utilityApps={utilityApps}
            userName="严伟洋"
            noticeCount={2}
            aside={<RightPanel />}
        >
            <ExitOrderDetail />
        </ParkingLayout>
    );
}

/* ------------------------------------------------------------------ */
/*  主内容区                                                           */
/* ------------------------------------------------------------------ */

const actionButtons = [
    { key: "refund", label: "退款", kind: "red" },
    { key: "intercept", label: "拦截记录", kind: "red-outline" },
    { key: "pass", label: "通行记录", kind: "blue" },
    { key: "recognize", label: "识别记录", kind: "blue" },
    { key: "video-in", label: "进场视频播放", kind: "blue" },
    { key: "video-out", label: "出场视频播放", kind: "blue" },
    { key: "owner-month", label: "车主月租详情", kind: "blue" },
    { key: "month-detail", label: "月卡生效明细", kind: "blue" },
    { key: "cross-trace", label: "区域穿行轨迹", kind: "blue" },
] as const;

const behaviorEvents = [
    { label: "逃费单", active: false },
    { label: "重复进场", active: false },
    { label: "自动清理场内车", active: true },
    { label: "无记录放行", active: false },
    { label: "手动抬杆进场", active: false },
    { label: "手动抬杆出场", active: false },
    { label: "疑似假车牌", active: false },
    { label: "车辆滞留", active: false },
    { label: "折返行为", active: false },
    { label: "撤销欠费单", active: false },
    { label: "充电减免", active: false },
    { label: "优惠券减免", active: false },
    { label: "军警消车辆", active: false },
    { label: "预约车辆", active: false },
    { label: "无感支付", active: false },
    { label: "内场被拦截", active: false },
    { label: "月卡过期", active: false },
    { label: "分组计费", active: false },
    { label: "区域计费", active: false },
    { label: "模糊匹配", active: false },
    { label: "白名单", active: false },
    { label: "匹配上单", active: false },
    { label: "固定收费", active: false },
    { label: "车辆月卡场内切换", active: false },
];

const reductions = [
    { label: "电子优惠券减免", amount: "¥2.00" },
    { label: "抬杆放行减免", amount: "¥0.00" },
    { label: "访客预约减免", amount: "¥0.00" },
    { label: "岗亭减免", amount: "¥0.00" },
    { label: "三方平台减免", amount: "¥0.00" },
    { label: "其他减免", amount: "¥0.00" },
];

function ExitOrderDetail() {
    return (
        <div className="flex flex-col gap-3">
            {/* 操作按钮行 */}
            <div className="pk-card flex flex-wrap items-center gap-2 px-3 py-2.5">
                {actionButtons.map((btn) => (
                    <span
                        key={btn.key}
                        className={
                            btn.kind === "red"
                                ? "pk-btn pk-btn-red"
                                : btn.kind === "red-outline"
                                  ? "pk-btn pk-btn-red-outline"
                                  : "pk-btn"
                        }
                    >
                        {btn.label}
                    </span>
                ))}
            </div>

            {/* 订单信息 */}
            <div className="pk-card px-4 py-3">
                <div className="mb-3 flex items-center">
                    <span className="pk-title">订单信息</span>
                    <span className="ml-3 text-[12px] text-(--pk-text-2)">
                        订单号 1909447030315229184
                    </span>
                    <CopyOutlined className="ml-1 cursor-pointer text-[12px] text-(--pk-text-2)" />
                    <span className="pk-tag pk-tag-dark ml-auto">车辆已离场</span>
                </div>
                <div className="flex gap-6">
                    <div className="grid min-w-0 flex-1 grid-cols-2 gap-x-10 gap-y-2.5">
                        <Field label="停车场" value="文博金街文博金街停车场" />
                        <Field label="车牌号" value="鲁AV60Y2" copyable />
                        <Field label="车牌颜色" value="蓝色" />
                        <Field label="停放时长" value="2小时17分钟53秒" />
                        <Field label="进场时间" value="2025.02.28 09:34:22" />
                        <Field label="出场时间" value="2025.02.28 09:34:22" />
                        <Field label="进场车道" value="总进口" camera />
                        <Field label="出场车道" value="广场出口" camera />
                        <Field
                            label="进场车辆类型"
                            value={<span className="pk-tag pk-tag-blue">临停</span>}
                        />
                        <Field
                            label="出场车辆类型"
                            value={<span className="pk-tag pk-tag-blue">临停</span>}
                        />
                        <Field
                            label="进场状态"
                            value={<span className="pk-tag pk-tag-red">重复进场</span>}
                        />
                        <Field
                            label="出场状态"
                            value={<span className="pk-tag pk-tag-green">正常出场</span>}
                        />
                    </div>
                    <div className="grid w-44 shrink-0 grid-cols-2 gap-x-6 gap-y-2.5 border-l border-(--pk-border) pl-5">
                        <Money label="应收" value="¥16.00" className="text-[15px] font-semibold" />
                        <Money label="减免" value="¥16.00" className="text-[15px] font-semibold text-(--pk-orange)" />
                        <Money label="欠费" value="¥16.00" className="text-[15px] font-semibold text-(--pk-red)" />
                        <Money label="实收" value="¥16.00" className="text-[15px] font-semibold text-(--pk-green)" />
                    </div>
                </div>
            </div>

            {/* 行为事件 */}
            <div className="pk-card px-4 py-3">
                <div className="mb-3 flex items-center">
                    <span className="pk-title">行为事件</span>
                </div>
                <div className="flex flex-wrap gap-2">
                    {behaviorEvents.map((event) =>
                        event.active ? (
                            <span key={event.label} className="pk-tag pk-tag-solid-blue">
                                {event.label}
                            </span>
                        ) : (
                            <span key={event.label} className="pk-tag">
                                {event.label}
                            </span>
                        ),
                    )}
                </div>
            </div>

            {/* 减免金额 */}
            <div className="pk-card px-4 py-3">
                <div className="mb-3 flex items-center gap-3">
                    <span className="pk-title">减免金额</span>
                    <span className="text-[12px] text-(--pk-text-2)">减免总额</span>
                    <span className="text-[24px] leading-none font-semibold text-(--pk-text)">2.00</span>
                </div>
                <div className="grid grid-cols-3 gap-x-10 gap-y-2.5">
                    {reductions.map((item) => (
                        <div key={item.label} className="flex items-center justify-between border-b border-dashed border-(--pk-border) pb-1.5">
                            <span className="text-[12px] text-(--pk-text-2)">{item.label}</span>
                            <span className="text-[13px]">{item.amount}</span>
                        </div>
                    ))}
                </div>
            </div>

            {/* 支付明细 */}
            <div className="pk-card px-4 py-3">
                <div className="mb-3 flex items-center">
                    <span className="pk-title">支付明细</span>
                </div>
                <table className="pk-table">
                    <thead>
                        <tr>
                            <th>支付类型</th>
                            <th>使用金额</th>
                            <th>支付渠道</th>
                            <th>状态</th>
                            <th>支付场景</th>
                            <th>交易流水号</th>
                            <th>支付时间</th>
                            <th>过期时间</th>
                            <th>支付成功时间</th>
                            <th />
                        </tr>
                    </thead>
                    <tbody>
                        {[0, 1].map((i) => (
                            <tr key={i}>
                                <td>金额</td>
                                <td>¥2.00</td>
                                <td>支付宝小程序</td>
                                <td>
                                    <span className="inline-flex items-center gap-1 text-(--pk-green)">
                                        <span className="size-1.5 rounded-full bg-(--pk-green)" />
                                        付款成功
                                    </span>
                                </td>
                                <td>出口支付</td>
                                <td>
                                    1909447...
                                    <CopyOutlined className="ml-1 cursor-pointer text-(--pk-text-2)" />
                                </td>
                                <td>2025-04-09 10:15:03</td>
                                <td>2025-04-09 10:25:03</td>
                                <td>2025-04-09 10:15:03</td>
                                <td>
                                    {i === 0 && (
                                        <span className="cursor-pointer text-(--pk-brand) hover:underline">
                                            合并订单 &gt;
                                        </span>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {/* 月卡信息 */}
            <div className="pk-card px-4 py-3">
                <div className="mb-3 flex items-center">
                    <span className="pk-title">月卡信息</span>
                </div>
                <div className="mb-3 flex flex-wrap items-center gap-x-10 gap-y-2">
                    <div className="flex items-center gap-2">
                        <span className="text-[12px] text-(--pk-text-2)">车主名称</span>
                        <span className="text-[13px]">山海火塘酒馆</span>
                    </div>
                    <div className="flex items-center gap-2">
                        <span className="text-[12px] text-(--pk-text-2)">联系电话</span>
                        <span className="text-[13px]">136789034567</span>
                    </div>
                    <div className="flex items-center gap-2">
                        <span className="text-[12px] text-(--pk-text-2)">绑定车牌</span>
                        {["鲁ADJ9167", "鲁AVH777", "鲁AVH777", "鲁AVH777"].map((plate, i) => (
                            <span key={`${plate}-${i}`} className="pk-tag pk-tag-blue-soft">
                                {plate}
                            </span>
                        ))}
                    </div>
                </div>
                <table className="pk-table">
                    <thead>
                        <tr>
                            <th>月卡名称</th>
                            <th>状态</th>
                            <th>开始日期</th>
                            <th>结束日期</th>
                            <th>操作</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td>文博金街年租</td>
                            <td>
                                <span className="inline-flex items-center gap-1 text-(--pk-green)">
                                    <span className="size-1.5 rounded-full bg-(--pk-green)" />
                                    生效中
                                </span>
                            </td>
                            <td>2024-11-26</td>
                            <td>2025-11-25</td>
                            <td>
                                <span className="cursor-pointer text-(--pk-brand) hover:underline">月卡续费</span>
                                <span className="mx-2 text-(--pk-border)">|</span>
                                <span className="cursor-pointer text-(--pk-brand) hover:underline">使用记录</span>
                                <span className="mx-2 text-(--pk-border)">|</span>
                                <span className="cursor-pointer text-(--pk-brand) hover:underline">删除</span>
                            </td>
                        </tr>
                    </tbody>
                </table>
            </div>
        </div>
    );
}

function Field({
    label,
    value,
    copyable,
    camera,
}: {
    label: string;
    value: ReactNode;
    copyable?: boolean;
    camera?: boolean;
}) {
    return (
        <div className="flex items-center gap-2">
            <span className="w-20 shrink-0 text-[12px] text-(--pk-text-2)">{label}</span>
            <span className="flex min-w-0 items-center gap-1.5 text-[13px]">{value}</span>
            {copyable && <CopyOutlined className="cursor-pointer text-[12px] text-(--pk-text-2)" />}
            {camera && <VideoCameraOutlined className="cursor-pointer text-[13px] text-(--pk-text-2)" />}
        </div>
    );
}

function Money({ label, value, className }: { label: string; value: string; className?: string }) {
    return (
        <div>
            <div className="text-[12px] text-(--pk-text-2)">{label}</div>
            <div className={className}>{value}</div>
        </div>
    );
}

/* ------------------------------------------------------------------ */
/*  右侧面板：AI 分析 + 车辆轨迹                                        */
/* ------------------------------------------------------------------ */

function RightPanel() {
    return (
        <div className="flex flex-col gap-3">
            {/* AI 轨迹分析 */}
            <div className="pk-card px-4 py-3">
                <div className="flex items-center gap-2">
                    <span className="flex size-6 items-center justify-center rounded bg-(--pk-brand) text-[12px] text-white">
                        <CarOutlined />
                    </span>
                    <span className="text-[14px] font-semibold text-(--pk-brand)">AI轨迹分析 · 运维建议</span>
                </div>
                <div className="mt-1 pl-8 text-[12px] text-(--pk-text-2)">车辆轨迹全链路追踪分析</div>
                <AiSection
                    tag={<span className="pk-tag pk-tag-solid-purple">结果</span>}
                    text={
                        <>
                            车辆在（东进口）反复触发识别超过3次，疑似存在
                            <span className="text-(--pk-red)">利用计费漏洞逃费</span>
                            的行为，已生成逃费单。
                        </>
                    }
                />
                <AiSection
                    tag={<span className="pk-tag pk-tag-solid-purple">平台自动撤回逃费单</span>}
                    text="通过车辆本车场轨迹链路、跨车场数据对比及车主行为特征数据判定该车辆不存在逃费行为，自动执行欠费单撤回处理。"
                />
                <AiSection
                    tag={<span className="pk-tag pk-tag-solid-orange">拦截</span>}
                    text="车辆于2025年12月5日 22:45:32在（地上出口）滞留超过2分钟（车牌相似度匹配），已标记内场被拦截事件。"
                    last
                />
                <div className="mt-3 flex items-center gap-3 pl-8">
                    <span className="pk-tag pk-tag-blue-soft">首次到访</span>
                    <span className="text-[12px] text-(--pk-text-2)">车主评分</span>
                    <span className="text-[13px] font-semibold">5.0</span>
                    <span className="flex text-[11px] text-(--pk-orange)">
                        {Array.from({ length: 5 }).map((_, i) => (
                            <StarFilled key={i} />
                        ))}
                    </span>
                </div>
            </div>

            {/* 车辆轨迹 */}
            <div className="pk-card px-4 py-3">
                <div className="mb-3 flex items-center">
                    <span className="pk-title">车辆轨迹</span>
                    <span className="ml-auto text-[12px] text-(--pk-text-2)">通过时间 2025-04-09 09:43:17</span>
                </div>
                <div className="mb-3 flex items-center gap-2">
                    <span className="text-[12px] text-(--pk-text-2)">入口</span>
                    <span className="pk-tag pk-tag-blue-soft">总进口</span>
                </div>
                <div className="mb-3 flex items-start gap-3">
                    <span className="flex size-10 shrink-0 items-center justify-center rounded bg-(--pk-brand) text-[17px] text-white">
                        <CarOutlined />
                    </span>
                    <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-2">
                            <span className="text-[16px] font-semibold">鲁AV60Y2</span>
                            <span className="pk-tag pk-tag-blue">临停</span>
                        </div>
                        <div className="mt-1 text-[12px] leading-5 text-(--pk-text-2)">
                            车辆到达车道时是否触发拦截事件：
                            <span className="ml-1 text-(--pk-green)">允许通行</span>
                            <span className="pk-tag pk-tag-blue ml-2">自动抬杆</span>
                        </div>
                    </div>
                    <span className="relative size-[60px] shrink-0 overflow-hidden rounded bg-(--pk-gray-bg)">
                        <CarThumb />
                        <span className="absolute right-1 bottom-1 flex size-4 items-center justify-center rounded-full bg-(--pk-brand) text-[8px] text-white">
                            <SearchOutlined />
                        </span>
                    </span>
                </div>
                <div className="mb-2 text-[13px] font-medium text-(--pk-brand)">云平台处理事件</div>
                <Timeline />
            </div>
        </div>
    );
}

function AiSection({
    tag,
    text,
    last,
}: {
    tag: ReactNode;
    text: ReactNode;
    last?: boolean;
}) {
    return (
        <div className="mt-3 pl-8">
            <div className="mb-1.5 flex items-center gap-2">{tag}</div>
            <div className="text-[12px] leading-5 text-(--pk-text-2)">{text}</div>
            {!last && <div className="mt-3 border-t border-dashed border-(--pk-border)" />}
        </div>
    );
}

function CarThumb() {
    return (
        <svg viewBox="0 0 60 60" className="size-full" fill="none">
            <rect width="60" height="60" fill="#dbe2ee" />
            <path
                d="M30 17 42 24.5V40h-3v-3.5H21V40h-3V24.5L30 17Z"
                fill="#8fa3c4"
            />
            <rect x="20" y="25" width="20" height="9" rx="1.5" fill="#eef2f8" />
            <circle cx="25" cy="41" r="3.4" fill="#3c4a63" />
            <circle cx="35" cy="41" r="3.4" fill="#3c4a63" />
        </svg>
    );
}

const timelineEvents = [
    { text: "云平台收到识别结果" },
    { text: "发送到订单中心" },
    { text: "发送到订单中心-成功" },
    { text: "发送抬杆指令" },
    { text: "发送语音播报指令", extra: "欠费车辆；鲁JDP7757；临时车；19:01:21" },
    { text: "下发对讲立柱显示指令" },
    { text: "发送显示指令" },
];

function Timeline() {
    return (
        <div>
            {timelineEvents.map((event) => (
                <div key={event.text} className="pk-timeline-item">
                    <span className="pk-timeline-dot" />
                    <div className="min-w-0 flex-1">
                        <div className="text-[11px] text-(--pk-text-2)">02.28 09:34:22</div>
                        <div className="mt-0.5 text-[12px]">{event.text}</div>
                        {event.extra && (
                            <div className="mt-1.5 rounded border border-(--pk-brand-border) bg-(--pk-brand-soft) px-2 py-1.5 text-[12px] text-(--pk-brand)">
                                {event.extra}
                            </div>
                        )}
                    </div>
                </div>
            ))}
            <div className="pk-timeline-item">
                <span className="pk-timeline-dot" />
                <div className="min-w-0 flex-1">
                    <div className="text-[11px] text-(--pk-text-2)">02.28 09:34:22</div>
                    <div className="mt-0.5 flex items-center gap-1 text-[12px]">
                        <ClockCircleOutlined className="text-(--pk-text-2)" />
                        放行完成
                    </div>
                </div>
            </div>
        </div>
    );
}
