/**
 * 柠檬云菜单区页面的共享 UI 基元。
 * 视觉遵循全站统一风格（云财蓝）：主色 #273cf3、圆角、白卡 + slate 灰阶文字，
 * 与 @/components/page 的既有实页同一套色板；交互基元一律用 antd 组件实现。
 */
import { useEffect, useRef, useState } from "react";
import type { CSSProperties, ReactNode, Ref } from "react";

import dayjs, { type Dayjs } from "dayjs";

import {
    Alert,
    AutoComplete,
    Button,
    Card,
    Checkbox,
    DatePicker,
    Input,
    Modal,
    Select as SelectInner,
    Switch,
    Table as AntTable,
    Tabs,
    theme,
} from "antd";
import type { ButtonProps, TableColumnsType } from "antd";
import {
    CalendarOutlined,
    DownOutlined,
    LeftOutlined,
    ReloadOutlined,
    RightOutlined,
    SearchOutlined,
} from "@ant-design/icons";

import { ICON_STYLE } from "@/components/page/styles";
import { makePeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { cn } from "@/lib/utils";

/* ---------------- 按钮 ---------------- */

type BtnVariant = "primary" | "plain" | "link" | "disabled";

/**
 * variant/type/onClick 由本组件自己定义（antd 的 variant 是 outlined/filled 那套语义，
 * onClick 需要事件参数交给触发器，本组件对外只暴露无参回调），其余沿用 antd Button 属性。
 */
type BtnProps = Omit<ButtonProps, "type" | "variant" | "children" | "onClick"> & {
    variant?: BtnVariant;
    children: ReactNode;
    dropdown?: boolean;
    onClick?: () => void;
    /**
     * React 19 里 ref 是普通属性。Dropdown / Tooltip / Popconfirm 这类触发器通过
     * cloneElement 注入 ref 来拿到真实 DOM 节点做弹层对齐，这里必须转发下去，
     * 否则弹层找不到锚点，会停在页面左上角（rc-trigger 的 -1000vw/-1000vh 占位）。
     */
    ref?: Ref<HTMLButtonElement>;
};

/** 页面按钮：主色实底 / 白底描边 / 链接 / 置灰。可选右侧 ▾ 分隔。 */
export function Btn({
    variant = "plain",
    children,
    dropdown,
    className = "",
    disabled,
    title,
    onClick,
    ref,
    ...rest
}: BtnProps) {
    return (
        <Button
            {...rest}
            ref={ref}
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

/** 期间面板的粒度：月报 month、季报 quarter、年报 year。 */
export type PeriodPicker = "month" | "quarter" | "year";

/** 面板选中的日期换算回期间：季度落到首月，年度沿用当前月份（与按年步进同口径）。 */
function pickedPeriod(
    picker: PeriodPicker,
    day: Dayjs,
    current?: { year: number; month: number } | null,
): AccountingPeriod {
    if (picker === "year") return makePeriod(day.year(), current?.month ?? 12);
    if (picker === "quarter") return makePeriod(day.year(), Math.floor(day.month() / 3) * 3 + 1);
    return makePeriod(day.year(), day.month() + 1);
}

/**
 * 期间步进器（◀ 2026年9月 ▶）。传 onPrev/onNext 即为可切换的真实控件；
 * 再传 onPick（配 value）则中间文字悬浮即弹出年/季/月面板，直接跳到任意期间——
 * 逐期快进走箭头，隔得远走面板。
 */
export interface PeriodStepperProps {
    text: string;
    onPrev?: () => void;
    onNext?: () => void;
    /** 覆盖在期间文字上的附加层。 */
    children?: ReactNode;
    /** 点中间文字弹出的期间面板粒度，需与 text 的展示口径一致。 */
    picker?: PeriodPicker;
    /** 当前期间，面板据此高亮。 */
    value?: { year: number; month: number } | null;
    /** 面板选中后的回调；不传则中间文字保持纯展示、悬浮也不弹面板。 */
    onPick?: (period: AccountingPeriod) => void;
    /** 可选期间下界（含），如账套启用期间。 */
    min?: Dayjs;
    /** 可选期间上界（含），如当前自然月。 */
    max?: Dayjs;
    disabled?: boolean;
}

export function PeriodStepper({
    text,
    onPrev,
    onNext,
    children,
    picker = "month",
    value,
    onPick,
    min,
    max,
    disabled,
}: PeriodStepperProps) {
    // 高度取自 antd token（compact 下即 28px），使自写控件与 antd 输入框/按钮等高、视觉统一。
    const { token } = theme.useToken();
    const h = token.controlHeight;
    const unit = picker === "year" ? "year" : "month";
    // 悬浮弹面板后，指针要从文字跨到面板上：离开文字先宽限一小段，面板接手则继续展开，否则收起。
    const [open, setOpen] = useState(false);
    const linger = useRef(0);
    const hold = () => window.clearTimeout(linger.current);
    const drop = () => {
        hold();
        linger.current = window.setTimeout(() => setOpen(false), 200);
    };
    useEffect(() => () => window.clearTimeout(linger.current), []);
    return (
        // 期间名称用 span 展示（不是按钮），避免被当成可点击动作；两侧箭头才是按钮
        <span className="inline-flex items-center overflow-hidden rounded-md border border-(--brand)">
            <button
                type="button"
                aria-label="上一期"
                style={{ height: h, width: h }}
                className="flex items-center justify-center bg-(--brand-fill) text-white disabled:opacity-60"
                disabled={!onPrev}
                onClick={onPrev}
            >
                <LeftOutlined />
            </button>
            <span
                style={{ height: h }}
                className="relative inline-flex items-center bg-(--brand-fill) px-3 text-[13px] font-medium tabular-nums text-white"
                onMouseEnter={onPick && !disabled ? () => { hold(); setOpen(true); } : undefined}
                onMouseLeave={onPick ? drop : undefined}
            >
                {text}
                {children}
                {onPick && (
                    // 透明选择器盖在期间文字上：悬浮即弹面板，逐期快进仍走两侧箭头。
                    <DatePicker
                        picker={picker}
                        open={open}
                        onOpenChange={(o) => {
                            hold();
                            setOpen(o);
                        }}
                        panelRender={(node) => (
                            <div onMouseEnter={hold} onMouseLeave={drop}>
                                {node}
                            </div>
                        )}
                        value={value ? dayjs(`${value.year}-${String(value.month).padStart(2, "0")}-01`) : null}
                        onChange={(d) => d && onPick(pickedPeriod(picker, d, value))}
                        disabledDate={(d) =>
                            (min != null && d.isBefore(min, unit)) || (max != null && d.isAfter(max, unit))
                        }
                        allowClear={false}
                        inputReadOnly
                        disabled={disabled}
                        style={{
                            position: "absolute",
                            inset: 0,
                            width: "100%",
                            height: "100%",
                            padding: 0,
                            opacity: 0,
                        }}
                    />
                )}
            </span>
            <button
                type="button"
                aria-label="下一期"
                style={{ height: h, width: h }}
                className="flex items-center justify-center bg-(--brand-fill) text-white disabled:opacity-60"
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
    style,
}: {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
    type?: "text" | "number" | "date";
    disabled?: boolean;
    className?: string;
    style?: CSSProperties;
}) {
    return (
        <Input
            className={className}
            style={style}
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

/**
 * 组合框：既能从下拉里选，也能直接录入下拉之外的值（如自定义币别代码）。
 * 输入内容按 value 或 label 做包含匹配，label 一般写成「编码（名称）」方便按任一项检索。
 */
export function ComboBox({
    value,
    onChange,
    options,
    placeholder,
    className = "",
}: {
    value: string;
    onChange: (value: string) => void;
    options: Array<{ value: string; label: string }>;
    placeholder?: string;
    className?: string;
}) {
    return (
        <AutoComplete
            className={className}
            value={value}
            placeholder={placeholder}
            options={options}
            onChange={onChange}
            filterOption={(input, option) => (option?.label ?? "").toLowerCase().includes(input.trim().toLowerCase())}
        />
    );
}

/** 静态下拉框外观。 */
export function FakeSelect({ text, className = "", style }: { text: string; className?: string; style?: CSSProperties }) {
    return (
        <Input
            className={className}
            style={style}
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
            className={cn("text-[13px]", !onChange && "opacity-60")}
            checked={checked}
            disabled={!onChange}
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
    scrollY,
}: {
    cols: Col[];
    rows: Row[];
    zebra?: boolean;
    empty?: ReactNode;
    /**
     * 纵向滚动高度（数字像素或 CSS 值如 "56vh"）。传了就让 antd 固定表头、表体内部滚动，
     * 调用方不必再在外面包 `max-h-[…] overflow-auto`（外层 overflow 会连带算出 overflow-y，多出滚动条）。
     */
    scrollY?: number | string;
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
            if (zebra && index !== undefined && index % 2 === 1 && style.background === undefined) style.background = "var(--table-zebra-bg)";
            if (isNeg) {
                style.background = "var(--table-neg-bg)";
                style.color = "var(--tone-red)";
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
        <AntTable<Row & { __key: string }>
            columns={columns as TableColumnsType<Row & { __key: string }>}
            dataSource={dataRows}
            rowKey={(r) => r.__key}
            pagination={false}
            scroll={{ x: "max-content", ...(scrollY === undefined ? {} : { y: scrollY }) }}
            rowClassName={(row) => row.__style ? "" : "transition-colors"}
            locale={empty === undefined ? undefined : { emptyText: <div className="py-6">{empty}</div> }}
        />
    );
}

/* ---------------- 分页页脚 ---------------- */

/** 页脚：只报记录总数。此前画了一组翻不动的页码，且总是写「1 到 20」，与实际渲染的全量行数不符。 */
export function Pager({ total = 0, count }: { total?: number; count?: number }) {
    // 兼容两种历史传参（total/count 都传记录数、或 count 固定 1），一律取较大者作为记录总数。
    const records = Math.max(total, count ?? 0);
    return (
        <div className="flex items-center px-4 py-3 text-[12.5px] text-(--muted-foreground)">
            共 {records} 条
        </div>
    );
}

/* ---------------- 页面骨架 ---------------- */

/** 内容页工具行：左筛选、右按钮组。 */
export function ToolRow({ left, right }: { left?: ReactNode; right?: ReactNode }) {
    return (
        <div className="flex min-h-[56px] flex-wrap items-center justify-between gap-3 px-4 py-2.5">
            <div className="flex flex-wrap items-center gap-3">{left}</div>
            <div className="flex flex-wrap items-center gap-2">{right}</div>
        </div>
    );
}

/** 页面容器：白色卡片（工具行 + 表格 + 页脚同属一张卡；描边、圆角与投影由全站主题统一控制）。 */
export function Page({ children, className = "" }: { children: ReactNode; className?: string }) {
    return (
        <Card className={className}>
            {children}
        </Card>
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
            checked={on}
            className={onClick ? "cursor-pointer" : "pointer-events-none"}
            onChange={() => onClick?.()}
        />
    );
}

/** 空表提示。 */
export function EmptyTip({ text = "暂无数据" }: { text?: string }) {
    return <span className="block text-center text-[13px] text-(--muted-foreground)">{text}</span>;
}

/* ---------------- 行内链接 / 说明块 / 取数状态 ---------------- */

/** 表格操作列的行内链接按钮；danger 用于「删除」等危险操作（标准红）。 */
export function LinkAction({
    children,
    danger,
    disabled,
    title,
    onClick,
    ref,
    ...rest
}: Omit<ButtonProps, "type" | "children" | "onClick"> & {
    children: ReactNode;
    danger?: boolean;
    disabled?: boolean;
    title?: string;
    onClick?: () => void;
    /** 同 Btn：Popconfirm / Tooltip 一类触发器靠 ref 拿真实 DOM 节点做弹层对齐，必须转发。 */
    ref?: Ref<HTMLButtonElement>;
}) {
    return (
        <Button
            {...rest}
            ref={ref}
            type="link"
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
        <div className={cn("rounded-lg bg-(--warn-bg) px-3 py-2 text-[12.5px] leading-5 text-(--warn-text)", className)}>
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
        return <div className="px-4 py-6 text-center text-[13px] text-(--muted-foreground)">加载中…</div>;
    }
    return null;
}

/* ---------------- 建设中（界面已复刻、后端未接入） ---------------- */

/** 行内「建设中」小标：挂在尚未接后端的按钮/字段旁，避免用户误认为可用。 */
export function WipTag({ text = "建设中", className = "" }: { text?: string; className?: string }) {
    return (
        <span className={`rounded bg-(--warn-bg) px-2 text-[11px] font-medium leading-5 text-(--warn-text) ${className}`}>
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
