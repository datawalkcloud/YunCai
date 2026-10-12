/**
 * 柠檬云 J6 复刻区共享 UI 基元。
 * 视觉规格取自在线系统截图取证（见 docs/柠檬云J6功能调研.md）：
 * 主绿 #44b449、侧栏激活绿 #32d373、链接蓝 #3385ff、
 * 表头浅绿 #f0f7f0、斑马行 #fafafa、负数底 #ffeeef、边框 #e8e8e8。
 */
import type { ReactNode } from "react";
import { Calendar, ChevronDown, ChevronLeft, ChevronRight, RefreshCw, RotateCw, Search } from "lucide-react";

/* ---------------- 按钮 ---------------- */

type BtnVariant = "primary" | "plain" | "link" | "disabled";

/** 柠檬云按钮：绿实心 / 白底描边 / 蓝字链接 / 置灰。可选右侧 ▾ 分隔。 */
export function Btn({
    variant = "plain",
    children,
    dropdown,
    className = "",
    onClick,
}: {
    variant?: BtnVariant;
    children: ReactNode;
    dropdown?: boolean;
    className?: string;
    onClick?: () => void;
}) {
    const base =
        "inline-flex h-8 items-center justify-center gap-1 rounded-[2px] px-3 text-sm whitespace-nowrap select-none";
    const styles: Record<BtnVariant, string> = {
        primary: "bg-[#44b449] text-white hover:bg-[#3ca341]",
        plain: "border border-[#d9d9d9] bg-white text-[#333] hover:border-[#44b449] hover:text-[#44b449]",
        link: "text-[#3385ff] hover:underline",
        disabled: "bg-[#f5f5f5] text-[#bbb] cursor-not-allowed",
    };
    return (
        <span className={`${base} ${styles[variant]} ${className}`} onClick={onClick}>
            {children}
            {dropdown && <ChevronDown className="h-3.5 w-3.5 opacity-70" />}
        </span>
    );
}

/** 主色下拉分裂按钮（如「生成凭证 ▾」）。 */
export function SplitBtn({ children }: { children: ReactNode }) {
    return (
        <span className="inline-flex h-8 select-none text-white">
            <span className="flex items-center rounded-l-[2px] bg-[#44b449] px-3 text-sm">{children}</span>
            <span className="flex items-center rounded-r-[2px] border-l border-white/40 bg-[#44b449] px-1.5">
                <ChevronDown className="h-3.5 w-3.5" />
            </span>
        </span>
    );
}

/* ---------------- 期间选择 ---------------- */

/** 绿色期间块（月：「2026年9月」/ 区间：「2026-07-01 至 2026-07-31」）。 */
export function PeriodBlock({ text, range }: { text?: string; range?: [string, string] }) {
    return (
        <span className="inline-flex h-8 select-none items-center gap-1.5 rounded-[2px] bg-[#44b449] px-2.5 text-sm text-white">
            {range ? (
                <>
                    <span>{range[0]}</span>
                    <span className="opacity-80">至</span>
                    <span>{range[1]}</span>
                </>
            ) : (
                <span>{text}</span>
            )}
            <Calendar className="h-3.5 w-3.5 opacity-90" />
        </span>
    );
}

/** 期间步进器（◀ 2026年9月 ▶，报表页用）。 */
export function PeriodStepper({ text }: { text: string }) {
    return (
        <span className="inline-flex h-8 select-none items-center rounded-[2px] bg-[#44b449] text-sm text-white">
            <span className="flex h-full items-center px-1.5 opacity-90">
                <ChevronLeft className="h-4 w-4" />
            </span>
            <span className="px-1">{text}</span>
            <span className="flex h-full items-center px-1.5 opacity-90">
                <ChevronRight className="h-4 w-4" />
            </span>
        </span>
    );
}

/* ---------------- 表单控件（静态展示） ---------------- */

/** 静态下拉框外观。 */
export function FakeSelect({ text, className = "" }: { text: string; className?: string }) {
    return (
        <span
            className={`inline-flex h-8 select-none items-center justify-between gap-2 rounded-[2px] border border-[#d9d9d9] bg-white px-2.5 text-sm text-[#333] ${className}`}
        >
            <span className="truncate">{text}</span>
            <ChevronDown className="h-3.5 w-3.5 shrink-0 text-[#999]" />
        </span>
    );
}

/** 静态输入框外观。 */
export function FakeInput({ text = "", className = "" }: { text?: string; className?: string }) {
    return (
        <span
            className={`inline-flex h-8 select-none items-center rounded-[2px] border border-[#d9d9d9] bg-white px-2.5 text-sm text-[#333] ${className}`}
        >
            {text || <span className="w-0">&nbsp;</span>}
        </span>
    );
}

/** 搜索框（设置页用）。 */
export function SearchBox({ placeholder }: { placeholder: string }) {
    return (
        <span className="inline-flex h-8 w-[260px] items-center rounded-[2px] border border-[#d9d9d9] bg-white px-2.5 text-sm text-[#999]">
            <span className="flex-1 truncate">{placeholder}</span>
            <Search className="h-3.5 w-3.5 text-[#999]" />
        </span>
    );
}

/** 复选框（静态）。label 支持传入节点（如带链接的协议文案）。 */
export function CheckBox({ label, checked }: { label: React.ReactNode; checked?: boolean }) {
    return (
        <span className="inline-flex select-none items-center gap-1.5 text-sm text-[#333]">
            <span
                className={`flex h-3.5 w-3.5 items-center justify-center rounded-[2px] border ${checked ? "border-[#44b449] bg-[#44b449]" : "border-[#d9d9d9] bg-white"}`}
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
    headerCls = "bg-[#f0f7f0]",
    empty,
}: {
    cols: Col[];
    rows: Row[];
    zebra?: boolean;
    headerCls?: string;
    empty?: ReactNode;
}) {
    const hasGroup = cols.some((c) => c.group);
    const th = "border-b border-[#e8e8e8] px-3 py-2 text-[13px] font-normal text-[#333]";
    const alignCls = (a?: string) => (a === "right" ? "text-right" : a === "center" ? "text-center" : "text-left");
    return (
        <table className="w-full border-collapse text-sm">
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
                        <td colSpan={cols.length} className="px-3 py-10 text-center text-[#999]">
                            {empty}
                        </td>
                    </tr>
                ) : (
                    rows.map((r, i) => (
                        <tr
                            key={i}
                            className={`border-b border-[#f0f0f0] ${r.__cls ?? ""} ${zebra && i % 2 === 1 ? "bg-[#fafafa]" : "bg-white"}`}
                        >
                            {cols.map((c) => {
                                const v = r[c.key];
                                const neg = c.neg && typeof v === "string" && v.startsWith("-");
                                return (
                                    <td
                                        key={c.key}
                                        className={`px-3 py-2.5 text-[13px] text-[#333] ${alignCls(c.align)} ${neg ? "bg-[#ffeeef] text-[#e0454f]" : ""}`}
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
    );
}

/* ---------------- 分页页脚 ---------------- */

/** 页脚：左「显示 N 到 M，共 X 条记录」，右页码组。 */
export function Pager({ total = 1, count = 1 }: { total?: number; count?: number }) {
    return (
        <div className="flex items-center justify-between px-4 py-3 text-[13px] text-[#666]">
            <span>
                显示 1 到 {total}，共 {count} 条记录
            </span>
            <span className="flex items-center gap-2">
                <span className="inline-flex h-7 items-center rounded-[2px] border border-[#d9d9d9] px-2">
                    20 <ChevronDown className="ml-1 h-3 w-3 text-[#999]" />
                </span>
                <ChevronLeft className="h-4 w-4 text-[#999]" />
                <span className="text-[#666]">第</span>
                <span className="inline-flex h-7 w-10 items-center justify-center rounded-[2px] border border-[#d9d9d9]">
                    1
                </span>
                <span>共{count}页</span>
                <ChevronRight className="h-4 w-4 text-[#999]" />
                <RotateCw className="h-3.5 w-3.5 text-[#999]" />
            </span>
        </div>
    );
}

/* ---------------- 页面骨架 ---------------- */

/** 内容页工具行：左筛选、右按钮组。 */
export function ToolRow({ left, right }: { left?: ReactNode; right?: ReactNode }) {
    return (
        <div className="flex min-h-[56px] flex-wrap items-center justify-between gap-3 px-4 py-2">
            <div className="flex flex-wrap items-center gap-3">{left}</div>
            <div className="flex flex-wrap items-center gap-2.5">{right}</div>
        </div>
    );
}

/** 页面容器（白底 + 可选内边距）。 */
export function Page({ children, className = "" }: { children: ReactNode; className?: string }) {
    return <div className={`flex min-h-full flex-col bg-white ${className}`}>{children}</div>;
}

/** 刷新图标按钮。 */
export function RefreshIcon() {
    return <RefreshCw className="h-4 w-4 text-[#999]" />;
}

/** 页内子页签（绿字+绿下划线激活态）。传 active/on.onChange 即为受控模式。 */
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
        <div className="flex items-center gap-8 border-b border-[#e8e8e8] px-4">
            {tabs.map((t, i) => {
                const on = active !== undefined ? active === i : i === 0;
                return (
                    <span
                        key={t}
                        onClick={() => onChange?.(i)}
                        className={`-mb-px cursor-pointer py-3 text-sm select-none ${on ? "border-b-2 border-[#44b449] text-[#44b449]" : "border-b-2 border-transparent text-[#333]"}`}
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
        <div className="fixed right-4 bottom-5 z-50 flex w-[330px] items-start gap-3 rounded-md bg-white p-4 shadow-[0_4px_16px_rgba(0,0,0,0.18)]">
            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[#ff9f2e] text-[15px] font-bold text-white">
                !
            </span>
            <div className="min-w-0 flex-1">
                <div className="text-[15px] font-bold text-[#333]">提示</div>
                <div className="mt-1 text-sm text-[#555]">{text}</div>
            </div>
            <span className="cursor-pointer text-[#999]" onClick={onClose}>
                ×
            </span>
        </div>
    );
}

/** 通用居中对话框骨架（白底直角细边框 + 标题 + × + 底部按钮）。 */
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
        <div className="fixed inset-0 z-50 flex items-center justify-center">
            <div className="w-full bg-white shadow-[0_6px_24px_rgba(0,0,0,0.2)]" style={{ maxWidth: width }}>
                <div className="relative border-b border-[#e8e8e8] py-3 text-center text-[15px] font-bold text-[#333]">
                    {title}
                    <span className="absolute top-2.5 right-4 cursor-pointer text-[16px] text-[#999]" onClick={onClose}>
                        ×
                    </span>
                </div>
                <div className="px-6 py-6">{children}</div>
                {footer && <div className="flex justify-center gap-3 border-t border-[#e8e8e8] px-6 py-4">{footer}</div>}
            </div>
        </div>
    );
}

/** 启用状态绿色开关（开态）。 */
export function Toggle({ on = true }: { on?: boolean }) {
    return (
        <span
            className={`relative inline-block h-5 w-9 rounded-full align-middle ${on ? "bg-[#44b449]" : "bg-[#d9d9d9]"}`}
        >
            <span
                className={`absolute top-0.5 h-4 w-4 rounded-full bg-white shadow ${on ? "right-0.5" : "left-0.5"}`}
            />
        </span>
    );
}

/** 空表提示。 */
export function EmptyTip({ text = "暂无数据" }: { text?: string }) {
    return <span className="block py-14 text-center text-[13px] text-[#999]">{text}</span>;
}
