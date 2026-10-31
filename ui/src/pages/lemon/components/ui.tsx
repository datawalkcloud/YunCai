/**
 * 柠檬云菜单区页面的共享 UI 基元。
 * 视觉遵循全站统一风格（云财蓝）：主色 #2f6bff、直角、白卡 + slate 灰阶文字，
 * 与 @/components/page 的既有实页同一套色板；交互基元一律用 antd 组件实现。
 */
import type { CSSProperties, ReactNode } from "react";

import dayjs from "dayjs";

import {
    Alert,
    Button,
    Card,
    Checkbox,
    ConfigProvider,
    DatePicker,
    Input,
    Modal,
    Select as SelectInner,
    Switch,
    Table as AntTable,
    Tabs,
} from "antd";
import type { TableColumnsType } from "antd";
import {
    CalendarOutlined,
    DownOutlined,
    LeftOutlined,
    ReloadOutlined,
    RightOutlined,
    SearchOutlined,
} from "@ant-design/icons";

import { PageHeader } from "@/components/page/section";
import { ICON_STYLE } from "@/components/page/styles";
import { cn } from "@/lib/utils";

/* ---------------- 按钮 ---------------- */

type BtnVariant = "primary" | "plain" | "link" | "disabled";

/** 页面按钮：主色实底 / 白底描边 / 链接 / 置灰。可选右侧 ▾ 分隔。 */
export function Btn({
    variant = "plain",
    children,
    dropdown,
    className = "",
    disabled,
    title,
    onClick,
}: {
    variant?: BtnVariant;
    children: ReactNode;
    dropdown?: boolean;
    className?: string;
    disabled?: boolean;
    title?: string;
    onClick?: () => void;
}) {
    return (
        <Button
            className={className}
            title={title}
            disabled={disabled || variant === "disabled"}
            type={variant === "primary" ? "primary" : variant === "link" ? "link" : "default"}
            onClick={onClick}
        >
            {children}
            {dropdown && <DownOutlined className="ml-1 opacity-70" />}
        </Button>
    );
}

/** 主色分裂按钮（如「生成凭证 ▾」）。 */
export function SplitBtn({ children }: { children: ReactNode }) {
    return (
        <Button type="primary">
            {children}
            <span className="ml-2 border-l border-white/40 pl-2">
                <DownOutlined className="text-[11px]" />
            </span>
        </Button>
    );
}

/* ---------------- 期间选择 ---------------- */

/** 期间按钮（日历图标）。传 onClick 后可点开选择器。 */
export function PeriodBlock({
    text,
    range,
    onClick,
}: {
    text?: string;
    range?: [string, string];
    onClick?: () => void;
}) {
    return (
        <Button type="primary" icon={<CalendarOutlined />} iconPosition="end" onClick={onClick}>
            {range ? (
                <>
                    {range[0]}
                    <span className="mx-1.5 opacity-70">至</span>
                    {range[1]}
                </>
            ) : (
                text
            )}
        </Button>
    );
}

/** 期间步进器（◀ 2026年9月 ▶）。传 onPrev/onNext 即为可切换的真实控件。 */
export function PeriodStepper({
    text,
    onPrev,
    onNext,
}: {
    text: string;
    onPrev?: () => void;
    onNext?: () => void;
}) {
    return (
        // 期间名称用 span 展示（不是按钮），避免被当成可点击动作；两侧箭头才是按钮
        <span className="inline-flex items-center overflow-hidden rounded border border-[#2f6bff]">
            <button
                type="button"
                aria-label="上一期"
                className="flex h-8 w-8 items-center justify-center bg-[#2f6bff] text-white disabled:opacity-60"
                disabled={!onPrev}
                onClick={onPrev}
            >
                <LeftOutlined />
            </button>
            <span className="inline-flex h-8 items-center bg-[#2f6bff] px-3 text-[13px] font-medium tabular-nums text-white">
                {text}
            </span>
            <button
                type="button"
                aria-label="下一期"
                className="flex h-8 w-8 items-center justify-center bg-[#2f6bff] text-white disabled:opacity-60"
                disabled={!onNext}
                onClick={onNext}
            >
                <RightOutlined />
            </button>
        </span>
    );
}

/* ---------------- 表单控件 ---------------- */

/**
 * 真输入框（受控）。凡是需要用户填写的对话框都用它，
 * FakeInput 只保留给纯装饰的静态展示位。
 */
export function TextInput({
    value,
    onChange,
    placeholder,
    type = "text",
    disabled,
    className = "",
}: {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
    type?: "text" | "number" | "date";
    disabled?: boolean;
    className?: string;
}) {
    return (
        <Input
            className={className}
            type={type}
            value={value}
            disabled={disabled}
            placeholder={placeholder}
            onChange={(e) => onChange(e.target.value)}
        />
    );
}

/** 日期选择（antd DatePicker）。值沿用页面的 yyyy-MM-dd 字符串，页面状态口径不变。 */
export function DateInput({
    value,
    onChange,
    disabled,
    className = "",
}: {
    value: string;
    onChange: (value: string) => void;
    disabled?: boolean;
    className?: string;
}) {
    const parsed = value ? dayjs(value) : null;
    return (
        <DatePicker
            className={className}
            disabled={disabled}
            allowClear={false}
            value={parsed?.isValid() ? parsed : null}
            onChange={(_, text) => onChange(text ?? "")}
        />
    );
}

/** 真下拉框（受控），选项可以是字符串或 { value, label }。 */
export function Select({
    value,
    onChange,
    options,
    className = "",
}: {
    value: string;
    onChange: (value: string) => void;
    options: Array<string | { value: string; label: string }>;
    className?: string;
}) {
    return (
        <SelectInner
            className={className}
            value={value}
            onChange={(v) => onChange(v)}
            // 科目等长列表动辄上百项，允许输入编码或名称过滤；不输入时行为与原来一致。
            showSearch
            optionFilterProp="label"
            options={options.map((o) => (typeof o === "string" ? { value: o, label: o } : o))}
        />
    );
}

/** 静态下拉框外观。 */
export function FakeSelect({ text, className = "" }: { text: string; className?: string }) {
    return (
        <Input
            className={className}
            readOnly
            value={text}
            suffix={<DownOutlined style={ICON_STYLE.muted} />}
        />
    );
}

/** 静态输入框外观（仅用于无交互的展示位，需要输入请改用 TextInput）。 */
export function FakeInput({ text = "", className = "" }: { text?: string; className?: string }) {
    return <Input className={className} readOnly value={text} />;
}

/** 搜索框（设置页用）。 */
export function SearchBox({ placeholder }: { placeholder: string }) {
    return (
        <Input
            className="w-[260px]"
            readOnly
            placeholder={placeholder}
            prefix={<SearchOutlined style={ICON_STYLE.muted} />}
        />
    );
}

/** 复选框。传 onChange 即为可交互的受控复选框，不传则退化为纯展示。 */
export function CheckBox({
    label,
    checked,
    onChange,
}: {
    label: ReactNode;
    checked?: boolean;
    onChange?: (checked: boolean) => void;
}) {
    return (
        <Checkbox
            className={cn("text-[13px]", !onChange && "pointer-events-none")}
            checked={checked}
            onChange={onChange ? (e) => onChange(e.target.checked) : undefined}
        >
            {label}
        </Checkbox>
    );
}

/* ---------------- 表格 ---------------- */

export type Col = {
    key: string;
    title: ReactNode;
    align?: "left" | "center" | "right";
    width?: string;
    /** 自定义单元格 */
    render?: (row: Record<string, unknown>, i: number) => ReactNode;
    /** 二级表头分组（组下列） */
    group?: string;
    /** 负数红底单元格 */
    neg?: boolean;
};

/** __style 用于合计行/强调行：斑马与底色在 antd 单元格里只能靠内联样式生效。 */
export type Row = Record<string, unknown> & { __style?: CSSProperties };

/**
 * 通用表：两级表头（group 分组）、数字右对齐、斑马纹、合计行、负数红底。
 * 负数红底与合计行底色是业务信号，保留原配色；表头/悬停交还给 antd 默认口径。
 */
export function Table({
    cols,
    rows,
    zebra = true,
    empty,
}: {
    cols: Col[];
    rows: Row[];
    zebra?: boolean;
    empty?: ReactNode;
}) {
    const toColumn = (c: Col): TableColumnsType<Row>[number] => ({
        key: c.key,
        title: c.title,
        align: c.align ?? "left",
        width: c.width,
        onCell: (row, index) => {
            const v = row[c.key];
            const isNeg = c.neg && typeof v === "string" && v.startsWith("-");
            const style: CSSProperties = { ...(row.__style ?? {}) };
            if (zebra && index !== undefined && index % 2 === 1 && style.background === undefined) style.background = "#fafbfd";
            if (isNeg) {
                style.background = "#fef2f2";
                style.color = "#ef4444";
            }
            return { style };
        },
        render: (_, row, i) => (c.render ? c.render(row, i) : ((row[c.key] as ReactNode) ?? "")),
    });

    /** 同组列合并成 antd 的二级表头；无组列保持平铺。 */
    const columns: TableColumnsType<Row> = [];
    for (const c of cols) {
        if (!c.group) {
            columns.push(toColumn(c));
            continue;
        }
        const last = columns[columns.length - 1] as { title?: ReactNode; children?: unknown[] } | undefined;
        if (last && last.title === c.group && Array.isArray(last.children)) {
            last.children.push(toColumn(c));
        } else {
            columns.push({ key: `group-${c.group}`, title: c.group, children: [toColumn(c)] } as TableColumnsType<Row>[number]);
        }
    }

    const dataRows = rows.map((r, i) => ({ ...r, __key: String(i) } as Row & { __key: string }));

    return (
        <ConfigProvider theme={{ components: { Table: { rowHoverBg: "#f6f9ff" } } }}>
            <AntTable<Row & { __key: string }>
                columns={columns as TableColumnsType<Row & { __key: string }>}
                dataSource={dataRows}
                rowKey={(r) => r.__key}
                pagination={false}
                size="small"
                scroll={{ x: "max-content" }}
                rowClassName={(row) => row.__style ? "" : "transition-colors"}
                locale={empty === undefined ? undefined : { emptyText: <div className="py-6">{empty}</div> }}
            />
        </ConfigProvider>
    );
}

/* ---------------- 分页页脚 ---------------- */

/** 页脚：左「显示 N 到 M，共 X 条」，右页码组（当前为静态展示，后端分页未接入）。 */
export function Pager({ total = 0, count }: { total?: number; count?: number }) {
    // 兼容两种历史传参（total/count 都传记录数、或 count 固定 1），一律取较大者作为记录总数。
    const records = Math.max(total, count ?? 0);
    const pages = Math.max(1, Math.ceil(records / 20));
    return (
        <div className="flex items-center justify-between border-t border-slate-100 px-4 py-3 text-[12.5px] text-slate-500">
            <span>
                显示 {records ? 1 : 0} 到 {Math.min(records, 20)}，共 {records} 条
            </span>
            <span className="flex items-center gap-2">
                <span className="inline-flex h-7 items-center rounded-lg border border-slate-200 px-2">
                    20 <DownOutlined className="ml-1 text-[11px]" style={ICON_STYLE.muted} />
                </span>
                <LeftOutlined className="text-[13px]" style={ICON_STYLE.placeholder} />
                <span>第</span>
                <span className="inline-flex h-7 w-10 items-center justify-center rounded-lg border border-slate-200 font-medium text-[#2f6bff]">
                    1
                </span>
                <span>共{pages}页</span>
                <RightOutlined className="text-[13px]" style={ICON_STYLE.muted} />
            </span>
        </div>
    );
}

/* ---------------- 页面骨架 ---------------- */

/** 内容页工具行：左筛选、右按钮组。 */
export function ToolRow({ left, right }: { left?: ReactNode; right?: ReactNode }) {
    return (
        <div className="flex min-h-[56px] flex-wrap items-center justify-between gap-3 border-b border-slate-100 px-4 py-2.5">
            <div className="flex flex-wrap items-center gap-3">{left}</div>
            <div className="flex flex-wrap items-center gap-2">{right}</div>
        </div>
    );
}

/**
 * 页面容器：白色卡片（工具行 + 表格 + 页脚同属一张卡；圆角由 antd 全局主题统一控制）。
 * 传 title / icon 时额外渲染与既有实页一致的页头横幅。
 */
export function Page({
    children,
    className = "",
    title,
    subtitle,
    icon: Icon,
    actions,
}: {
    children: ReactNode;
    className?: string;
    title?: ReactNode;
    subtitle?: ReactNode;
    icon?: React.ComponentType<{ className?: string }>;
    actions?: ReactNode;
}) {
    return (
        <div className="flex min-h-full flex-col gap-4">
            {title != null &&
                (Icon ? (
                    <PageHeader icon={Icon} title={title} subtitle={subtitle} actions={actions} />
                ) : (
                    <section className="rounded-2xl bg-gradient-to-r from-[#e7f0ff] via-[#eef4ff] to-[#f6f9ff] px-6 py-5">
                        <div className="flex flex-wrap items-center gap-4">
                            <div className="min-w-0">
                                <h1 className="text-[20px] font-bold text-slate-800">{title}</h1>
                                {subtitle && <p className="mt-0.5 text-[12.5px] text-slate-500">{subtitle}</p>}
                            </div>
                            {actions && <div className="ml-auto flex flex-wrap items-center gap-2">{actions}</div>}
                        </div>
                    </section>
                ))}
            <Card
                className={className}
                variant="borderless"
                styles={{ root: { boxShadow: "0 1px 2px rgba(16,24,40,0.04)" }, body: { padding: 0 } }}
            >
                {children}
            </Card>
        </div>
    );
}

/** 刷新图标按钮。 */
export function RefreshIcon({ onClick }: { onClick?: () => void } = {}) {
    return <ReloadOutlined className={`text-[15px] ${onClick ? "cursor-pointer" : ""}`} style={ICON_STYLE.muted} onClick={onClick} />;
}

/** 页内子页签（主色字 + 主色下划线激活态）。传 active/on.onChange 即为受控模式。 */
export function SubTabs({
    tabs,
    active,
    onChange,
}: {
    tabs: string[];
    active?: number;
    onChange?: (i: number) => void;
}) {
    const controlled = active !== undefined && onChange !== undefined;
    return (
        <Tabs
            className="lemon-sub-tabs"
            activeKey={String(controlled ? active : 0)}
            onChange={(key) => onChange?.(Number(key))}
            items={tabs.map((t, i) => ({ key: String(i), label: t }))}
            tabBarStyle={{ margin: 0, paddingInline: 16 }}
        />
    );
}

/** 右下角提示 toast（警告条 + 标题「提示」+ 文案）。 */
export function Toast({ text, onClose }: { text: string; onClose?: () => void }) {
    return (
        <div className="fixed right-4 bottom-5 z-50 w-[330px]">
            <Alert
                type="warning"
                showIcon
                title="提示"
                description={text}
                closable={Boolean(onClose)}
                onClose={onClose}
            />
        </div>
    );
}

/** 通用对话框（antd Modal：标题 + × + 底部按钮）。 */
export function Dialog({
    title,
    width = 440,
    children,
    footer,
    onClose,
}: {
    title: string;
    width?: number;
    children: ReactNode;
    footer?: ReactNode;
    onClose?: () => void;
}) {
    return (
        <Modal open title={title} width={width} onCancel={onClose} footer={footer}>
            {children}
        </Modal>
    );
}

/** 启用状态开关：开启用主色。传 onClick 即为可切换开关。 */
export function Toggle({ on = true, onClick }: { on?: boolean; onClick?: () => void }) {
    return (
        <Switch
            size="small"
            checked={on}
            className={onClick ? "cursor-pointer" : "pointer-events-none"}
            onChange={() => onClick?.()}
        />
    );
}

/** 空表提示。 */
export function EmptyTip({ text = "暂无数据" }: { text?: string }) {
    return <span className="block text-center text-[13px] text-slate-400">{text}</span>;
}

/* ---------------- 行内链接 / 说明块 / 取数状态 ---------------- */

/** 表格操作列的行内链接按钮；danger 用于「删除」等危险操作（标准红）。 */
export function LinkAction({
    children,
    danger,
    disabled,
    title,
    onClick,
}: {
    children: ReactNode;
    danger?: boolean;
    disabled?: boolean;
    title?: string;
    onClick?: () => void;
}) {
    return (
        <Button
            className="!px-0 !text-[13px]"
            type="link"
            size="small"
            danger={danger}
            disabled={disabled}
            title={title}
            onClick={onClick}
        >
            {children}
        </Button>
    );
}

/** 浅色说明块：与 WipTag 同一琥珀色板，用于表单/表格上方的口径说明。 */
export function HintBox({ children, className = "" }: { children: ReactNode; className?: string }) {
    return (
        <div className={cn("rounded-lg bg-[#fffbeb] px-3 py-2 text-[12.5px] leading-5 text-[#b45309]", className)}>
            {children}
        </div>
    );
}

/** 取数状态条：错误用 antd Alert（文案请经 apiErrorMessage 收敛），加载中用灰字。 */
export function StateBar({ loading, error }: { loading?: boolean; error?: string | null }) {
    if (error) {
        return (
            <div className="px-4 pb-2">
                <Alert type="error" showIcon title={error} />
            </div>
        );
    }
    if (loading) {
        return <div className="px-4 py-6 text-center text-[13px] text-slate-400">加载中…</div>;
    }
    return null;
}

/* ---------------- 建设中（界面已复刻、后端未接入） ---------------- */

/** 行内「建设中」小标：挂在尚未接后端的按钮/字段旁，避免用户误认为可用。 */
export function WipTag({ text = "建设中", className = "" }: { text?: string; className?: string }) {
    return (
        <span className={`bg-[#fef3c7] px-2 text-[11px] font-medium leading-5 text-[#b45309] ${className}`}>
            {text}
        </span>
    );
}

/** 页面/区块级建设中提示条，用在不接后端就整块无意义的区域。 */
export function WipNotice({ text = "以下为界面复刻的展示占位，尚未接入后端数据。" }: { text?: string }) {
    return (
        <div className="mx-4 my-3">
            <Alert type="warning" showIcon title={text} />
        </div>
    );
}

/** 建设中按钮：外观保留、不可点击，悬停给出说明。 */
export function WipBtn({ children, dropdown, title = "该操作尚未接入后端，建设中" }: { children: ReactNode; dropdown?: boolean; title?: string }) {
    return (
        <Btn variant="disabled" dropdown={dropdown} title={title}>
            {children}
        </Btn>
    );
}
