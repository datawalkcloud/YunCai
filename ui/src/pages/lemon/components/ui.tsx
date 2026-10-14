/**
 * 柠檬云菜单区页面的共享 UI 基元。
 * 视觉遵循全站统一风格（云财蓝）：主色 #2f6bff、圆角 rounded-lg/2xl、
 * 白卡 + slate 灰阶文字，与 @/components/page 的既有实页同一套色板。
 */
import type { ReactNode } from "react";
import { Calendar, ChevronDown, ChevronLeft, ChevronRight, RefreshCw, RotateCw, Search } from "lucide-react";

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
    const base =
        "inline-flex h-8 cursor-pointer items-center justify-center gap-1.5 rounded-lg px-3 text-[13px] font-medium whitespace-nowrap transition-colors select-none";
    const styles: Record<BtnVariant, string> = {
        primary: "bg-[#2f6bff] text-white shadow-[0_1px_2px_rgba(47,107,255,0.22)] hover:bg-[#255ce0]",
        plain: "border border-slate-200 bg-white text-slate-600 hover:bg-[#f6f8fb] hover:text-slate-800",
        link: "text-[#2f6bff] hover:bg-[#eef4ff]",
        disabled: "cursor-not-allowed bg-slate-100 text-slate-400",
    };
    const v: BtnVariant = disabled ? "disabled" : variant;
    return (
        <span
            className={`${base} ${styles[v]} ${className}`}
            title={title}
            onClick={disabled ? undefined : onClick}
        >
            {children}
            {dropdown && <ChevronDown className="h-3.5 w-3.5 opacity-70" />}
        </span>
    );
}

/** 主色分裂按钮（如「生成凭证 ▾」）。 */
export function SplitBtn({ children }: { children: ReactNode }) {
    return (
        <span className="inline-flex h-8 cursor-pointer select-none rounded-lg bg-[#2f6bff] text-[13px] font-medium text-white shadow-[0_1px_2px_rgba(47,107,255,0.22)] transition-colors hover:bg-[#255ce0]">
            <span className="flex items-center px-3">{children}</span>
            <span className="flex items-center border-l border-white/30 px-1.5">
                <ChevronDown className="h-3.5 w-3.5" />
            </span>
        </span>
    );
}

/* ---------------- 期间选择 ---------------- */

/** 期间胶囊（日历图标）。传 onClick 后可点开选择器。 */
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
        <span
            className={`inline-flex h-8 items-center gap-1.5 rounded-lg bg-[#2f6bff] px-2.5 text-[13px] text-white shadow-[0_1px_2px_rgba(47,107,255,0.22)] ${onClick ? "cursor-pointer hover:bg-[#255ce0]" : "select-none"}`}
            onClick={onClick}
        >
            {range ? (
                <>
                    <span>{range[0]}</span>
                    <span className="opacity-70">至</span>
                    <span>{range[1]}</span>
                </>
            ) : (
                <span>{text}</span>
            )}
            <Calendar className="h-3.5 w-3.5 opacity-90" />
        </span>
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
    const cell = (handler?: () => void) =>
        `flex h-full items-center px-1.5 ${handler ? "cursor-pointer opacity-90 hover:opacity-100" : "opacity-90"}`;
    return (
        <span className="inline-flex h-8 items-center rounded-lg bg-[#2f6bff] text-[13px] text-white shadow-[0_1px_2px_rgba(47,107,255,0.22)]">
            <span className={cell(onPrev)} onClick={onPrev}>
                <ChevronLeft className="h-4 w-4" />
            </span>
            <span className="px-1 font-medium">{text}</span>
            <span className={cell(onNext)} onClick={onNext}>
                <ChevronRight className="h-4 w-4" />
            </span>
        </span>
    );
}

/* ---------------- 表单控件 ---------------- */

/**
 * 真输入框（受控）。外观与 FakeInput 一致，凡是需要用户填写的对话框都用它，
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
        <input
            type={type}
            value={value}
            disabled={disabled}
            placeholder={placeholder}
            onChange={(e) => onChange(e.target.value)}
            className={`h-8 rounded-lg border border-slate-200 bg-white px-2.5 text-[13px] text-slate-700 shadow-none outline-none placeholder:text-slate-400 focus:border-[#2f6bff] focus:ring-2 focus:ring-[#2f6bff]/15 disabled:bg-slate-100 disabled:text-slate-400 ${className}`}
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
        <select
            value={value}
            onChange={(e) => onChange(e.target.value)}
            className={`h-8 rounded-lg border border-slate-200 bg-white px-2 text-[13px] text-slate-700 shadow-none outline-none focus:border-[#2f6bff] focus:ring-2 focus:ring-[#2f6bff]/15 ${className}`}
        >
            {options.map((o) => {
                const v = typeof o === "string" ? o : o.value;
                return (
                    <option key={v} value={v}>
                        {typeof o === "string" ? o : o.label}
                    </option>
                );
            })}
        </select>
    );
}

/** 静态下拉框外观。 */
export function FakeSelect({ text, className = "" }: { text: string; className?: string }) {
    return (
        <span
            className={`inline-flex h-8 select-none items-center justify-between gap-2 rounded-lg border border-slate-200 bg-white px-2.5 text-[13px] text-slate-700 ${className}`}
        >
            <span className="truncate">{text}</span>
            <ChevronDown className="h-3.5 w-3.5 shrink-0 text-slate-400" />
        </span>
    );
}

/** 静态输入框外观（仅用于无交互的展示位，需要输入请改用 TextInput）。 */
export function FakeInput({ text = "", className = "" }: { text?: string; className?: string }) {
    return (
        <span
            className={`inline-flex h-8 select-none items-center rounded-lg border border-slate-200 bg-white px-2.5 text-[13px] text-slate-700 ${className}`}
        >
            {text || <span className="w-0">&nbsp;</span>}
        </span>
    );
}

/** 搜索框（设置页用）。 */
export function SearchBox({ placeholder }: { placeholder: string }) {
    return (
        <span className="inline-flex h-8 w-[260px] items-center rounded-lg border border-slate-200 bg-white px-2.5 text-[13px] text-slate-400">
            <span className="flex-1 truncate">{placeholder}</span>
            <Search className="h-3.5 w-3.5 text-slate-400" />
        </span>
    );
}

/** 复选框。传 onChange 即为可交互的受控复选框，不传则退化为纯展示。 */
export function CheckBox({
    label,
    checked,
    onChange,
}: {
    label: React.ReactNode;
    checked?: boolean;
    onChange?: (checked: boolean) => void;
}) {
    return (
        <span
            className={`inline-flex items-center gap-1.5 text-[13px] text-slate-600 ${onChange ? "cursor-pointer" : "select-none"}`}
            onClick={onChange ? () => onChange(!checked) : undefined}
        >
            <span
                className={`flex h-3.5 w-3.5 items-center justify-center rounded-[3px] border ${checked ? "border-[#2f6bff] bg-[#2f6bff]" : "border-slate-300 bg-white"}`}
            >
                {checked && (
                    <svg viewBox="0 0 10 8" className="h-2 w-2.5 fill-none stroke-white stroke-[2]">
                        <path d="M1 4l2.5 2.5L9 1" />
                    </svg>
                )}
            </span>
            {label}
        </span>
    );
}

/* ---------------- 表格 ---------------- */

export type Col =
    | {
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
      }
    | {
          key: string;
          title: ReactNode;
          align?: "left" | "center" | "right";
          width?: string;
          render?: (row: Record<string, unknown>, i: number) => ReactNode;
          group?: string;
          neg?: boolean;
      };

export type Row = Record<string, unknown> & { __cls?: string };

/** 通用表：两级表头（group 分组）、数字右对齐、斑马纹、合计行、负数红底。 */
export function Table({
    cols,
    rows,
    zebra = true,
    headerCls = "bg-muted",
    empty,
}: {
    cols: Col[];
    rows: Row[];
    zebra?: boolean;
    headerCls?: string;
    empty?: ReactNode;
}) {
    const hasGroup = cols.some((c) => c.group);
    const th = "border-b border-slate-200 px-3 py-2.5 text-[12.5px] font-medium text-slate-500";
    const alignCls = (a?: string) => (a === "right" ? "text-right" : a === "center" ? "text-center" : "text-left");
    return (
        <div className="w-full overflow-x-auto">
            <table className="w-full border-collapse text-[13px]">
            <thead>
                {hasGroup ? (
                    <>
                        <tr className={headerCls}>
                            {cols
                                .filter((c) => !c.group)
                                .map((c) => (
                                    <th key={c.key} rowSpan={2} className={`${th} ${alignCls(c.align)}`} style={{ width: c.width }}>
                                        {c.title}
                                    </th>
                                ))}
                            {/* 分组表头跨列 */}
                            {[
                                ...new Set(cols.filter((c) => c.group).map((c) => c.group as string)),
                            ].map((g) => (
                                <th
                                    key={g}
                                    colSpan={cols.filter((c) => c.group === g).length}
                                    className={`${th} text-center`}
                                >
                                    {g}
                                </th>
                            ))}
                        </tr>
                        <tr className={headerCls}>
                            {cols.map((c) =>
                                c.group ? (
                                    <th key={c.key} className={`${th} ${alignCls(c.align)}`} style={{ width: c.width }}>
                                        {c.title}
                                    </th>
                                ) : null,
                            )}
                        </tr>
                    </>
                ) : (
                    <tr className={headerCls}>
                        {cols.map((c) => (
                            <th key={c.key} className={`${th} ${alignCls(c.align)}`} style={{ width: c.width }}>
                                {c.title}
                            </th>
                        ))}
                    </tr>
                )}
            </thead>
            <tbody>
                {rows.length === 0 && empty ? (
                    <tr>
                        <td colSpan={cols.length} className="px-3 py-10 text-center text-slate-400">
                            {empty}
                        </td>
                    </tr>
                ) : (
                    rows.map((r, i) => (
                        <tr
                            key={i}
                            className={`border-b border-slate-100 transition-colors hover:bg-[#f6f9ff] ${r.__cls ?? ""} ${zebra && i % 2 === 1 ? "bg-[#fafbfd]" : "bg-white"}`}
                        >
                            {cols.map((c) => {
                                const v = r[c.key];
                                const neg = c.neg && typeof v === "string" && v.startsWith("-");
                                return (
                                    <td
                                        key={c.key}
                                        className={`px-3 py-2.5 text-[13px] text-slate-600 tabular-nums ${alignCls(c.align)} ${neg ? "bg-[#fef2f2] text-[#ef4444]" : ""}`}
                                        style={{ width: c.width }}
                                    >
                                        {c.render ? c.render(r, i) : (v as ReactNode) ?? ""}
                                    </td>
                                );
                            })}
                        </tr>
                    ))
                )}
            </tbody>
            </table>
        </div>
    );
}

/* ---------------- 分页页脚 ---------------- */

/** 页脚：左「显示 N 到 M，共 X 条记录」，右页码组。 */
export function Pager({ total = 1, count = 1 }: { total?: number; count?: number }) {
    return (
        <div className="flex items-center justify-between border-t border-slate-100 px-4 py-3 text-[12.5px] text-slate-500">
            <span>
                显示 1 到 {total}，共 {count} 条记录
            </span>
            <span className="flex items-center gap-2">
                <span className="inline-flex h-7 items-center rounded-lg border border-slate-200 px-2">
                    20 <ChevronDown className="ml-1 h-3 w-3 text-slate-400" />
                </span>
                <ChevronLeft className="h-4 w-4 text-slate-300" />
                <span>第</span>
                <span className="inline-flex h-7 w-10 items-center justify-center rounded-lg border border-slate-200 font-medium text-[#2f6bff]">
                    1
                </span>
                <span>共{count}页</span>
                <ChevronRight className="h-4 w-4 text-slate-500" />
                <RotateCw className="h-3.5 w-3.5 text-slate-400" />
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
 * 页面容器：白色圆角卡片（工具行 + 表格 + 页脚同属一张卡）。
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
                    <PageHeaderBanner icon={Icon} title={title} subtitle={subtitle} actions={actions} />
                ) : (
                    <div className="rounded-2xl bg-gradient-to-r from-[#e7f0ff] via-[#eef4ff] to-[#f6f9ff] px-6 py-5">
                        <div className="flex flex-wrap items-center gap-4">
                            <div className="min-w-0">
                                <h1 className="text-[20px] font-bold text-slate-800">{title}</h1>
                                {subtitle && <p className="mt-0.5 text-[12.5px] text-slate-500">{subtitle}</p>}
                            </div>
                            {actions && <div className="ml-auto flex flex-wrap items-center gap-2">{actions}</div>}
                        </div>
                    </div>
                ))}
            <div
                className={`overflow-hidden rounded-2xl border border-slate-100 bg-white shadow-[0_1px_2px_rgba(16,24,40,0.04)] ${className}`}
            >
                {children}
            </div>
        </div>
    );
}

function PageHeaderBanner({
    icon: Icon,
    title,
    subtitle,
    actions,
}: {
    icon: React.ComponentType<{ className?: string }>;
    title: ReactNode;
    subtitle?: ReactNode;
    actions?: ReactNode;
}) {
    return (
        <section className="rounded-2xl bg-gradient-to-r from-[#e7f0ff] via-[#eef4ff] to-[#f6f9ff] px-6 py-5">
            <div className="flex flex-wrap items-center gap-4">
                <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-[#5b9bff] to-[#2f6bff] text-white shadow-[0_6px_16px_rgba(47,107,255,0.28)]">
                    <Icon className="size-5" />
                </span>
                <div className="min-w-0">
                    <h1 className="text-[20px] font-bold text-slate-800">{title}</h1>
                    {subtitle && <p className="mt-0.5 text-[12.5px] text-slate-500">{subtitle}</p>}
                </div>
                {actions && <div className="ml-auto flex flex-wrap items-center gap-2">{actions}</div>}
            </div>
        </section>
    );
}

/** 刷新图标按钮。 */
export function RefreshIcon() {
    return <RefreshCw className="h-4 w-4 text-slate-400" />;
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
    return (
        <div className="flex items-center gap-8 border-b border-slate-100 px-4">
            {tabs.map((t, i) => {
                const on = active !== undefined ? active === i : i === 0;
                return (
                    <span
                        key={t}
                        onClick={() => onChange?.(i)}
                        className={`-mb-px cursor-pointer py-3 text-[13px] font-medium select-none ${on ? "border-b-2 border-[#2f6bff] text-[#2f6bff]" : "border-b-2 border-transparent text-slate-500 hover:text-slate-700"}`}
                    >
                        {t}
                    </span>
                );
            })}
        </div>
    );
}

/** 右下角提示 toast（橙色 ! 圆标 + 标题「提示」+ 文案）。 */
export function Toast({ text, onClose }: { text: string; onClose?: () => void }) {
    return (
        <div className="fixed right-4 bottom-5 z-50 flex w-[330px] items-start gap-3 rounded-xl border border-slate-100 bg-white p-4 shadow-[0_8px_24px_rgba(16,24,40,0.14)]">
            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[#f59e0b] text-[15px] font-bold text-white">
                !
            </span>
            <div className="min-w-0 flex-1">
                <div className="text-[15px] font-bold text-slate-800">提示</div>
                <div className="mt-1 text-[13px] text-slate-500">{text}</div>
            </div>
            <span className="cursor-pointer text-slate-400 hover:text-slate-600" onClick={onClose}>
                ×
            </span>
        </div>
    );
}

/** 通用居中对话框（白底圆角卡片 + 标题 + × + 底部按钮）。 */
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
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4">
            <div className="w-full overflow-hidden rounded-2xl bg-white shadow-[0_16px_48px_rgba(16,24,40,0.2)]" style={{ maxWidth: width }}>
                <div className="relative px-6 py-4 text-[15px] font-semibold text-slate-800">
                    {title}
                    <span
                        className="absolute top-3.5 right-4 cursor-pointer text-[16px] text-slate-400 hover:text-slate-600"
                        onClick={onClose}
                    >
                        ×
                    </span>
                </div>
                <div className="border-t border-slate-100 px-6 py-6 text-[13px] text-slate-600">{children}</div>
                {footer && (
                    <div className="flex justify-end gap-2.5 border-t border-slate-100 bg-[#fafbfd] px-6 py-4">
                        {footer}
                    </div>
                )}
            </div>
        </div>
    );
}

/** 启用状态开关：开启用主色。传 onClick 即为可切换开关。 */
export function Toggle({ on = true, onClick }: { on?: boolean; onClick?: () => void }) {
    return (
        <span
            className={`relative inline-block h-5 w-9 rounded-full align-middle transition-colors ${on ? "bg-[#2f6bff]" : "bg-slate-300"} ${onClick ? "cursor-pointer" : ""}`}
            onClick={onClick}
        >
            <span
                className={`absolute top-0.5 h-4 w-4 rounded-full bg-white shadow ${on ? "right-0.5" : "left-0.5"}`}
            />
        </span>
    );
}

/** 空表提示。 */
export function EmptyTip({ text = "暂无数据" }: { text?: string }) {
    return <span className="block py-14 text-center text-[13px] text-slate-400">{text}</span>;
}

/* ---------------- 建设中（界面已复刻、后端未接入） ---------------- */

/** 行内「建设中」小标：挂在尚未接后端的按钮/字段旁，避免用户误认为可用。 */
export function WipTag({ text = "建设中", className = "" }: { text?: string; className?: string }) {
    return (
        <span className={`rounded-full bg-[#fef3c7] px-2 text-[11px] font-medium leading-5 text-[#b45309] ${className}`}>
            {text}
        </span>
    );
}

/** 页面/区块级建设中提示条，用在不接后端就整块无意义的区域。 */
export function WipNotice({ text = "以下为界面复刻的展示占位，尚未接入后端数据。" }: { text?: string }) {
    return (
        <div className="mx-4 my-3 flex items-center gap-2 rounded-xl border border-[#fde68a] bg-[#fffbeb] px-3 py-2 text-[12.5px] text-[#92640b]">
            <span className="flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-[#f59e0b] text-[11px] font-bold text-white">
                !
            </span>
            {text}
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
