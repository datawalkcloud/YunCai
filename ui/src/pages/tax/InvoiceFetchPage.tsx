/**
 * 智能报税 · 发票获取。
 *
 * 销项 / 进项 / 进项统计表三个页签。发票种类按税局的标准清单铺满：
 * 销项按开票日期归属期间，统计正常与红冲份数；进项按认证时登记的税款所属期
 * （未登记则按认证日期，税局取票的发票按开票日期）归属期间，展示认证份数 / 金额 / 有效抵扣税额，
 * 有效抵扣税额取发票合计税额。进项统计表与进项页签同源，按附表二抵扣类型归集份数 / 金额 / 税额。
 * 状态条跟随服务器上的税局登录态：登录有效显示「已登录」，失效时可就地补一次短信验证码。
 * 「一键取票」由服务器上的无头浏览器登录税局查询发票并落库，页面只展示取票结果；
 * 未登录时点击会先弹登录提示。
 */
import { useCallback, useEffect, useMemo, useState } from "react";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { Alert, App, Button, Card, Modal, Table, Tooltip, Typography } from "antd";
import type { TableColumnsType } from "antd";
import { QuestionCircleOutlined, ReloadOutlined } from "@ant-design/icons";
import { useNavigate } from "react-router";

import type { InvoiceDto } from "@/apis-gen";
import { InvoicesApi } from "@/apis-gen";
import type { AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { useAuth } from "@/auth/AuthContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { resumeTaxPortalLogin, syncTaxInvoices } from "@/lib/tax-portal";
import { AMOUNT_COL } from "@/components/page/styles";
import { cn } from "@/lib/utils";
import { TaxLoginPrompt } from "./TaxLoginPrompt";
import { useTaxPeriod } from "./tax-period";
import { setPath } from "@/accountSets/url-id";

type TabKey = "sales" | "purchase" | "purchase-stats";

/** 一键取票落库时写入的来源；本页只展示这个来源的发票，手工录入 / 导入的不混进来。 */
const TAX_SOURCE = "一键取票";

const TABS: Array<{ key: TabKey; label: string }> = [
    { key: "sales", label: "销项发票" },
    { key: "purchase", label: "进项发票" },
    { key: "purchase-stats", label: "进项统计表" },
];

interface KindDef {
    key: string;
    label: string;
}

/** 销项发票种类（顺序与税局取票清单一致）。 */
const SALES_KINDS: KindDef[] = [
    { key: "digital-special", label: "数电发票（增值税专用发票）" },
    { key: "special", label: "增值税专用发票" },
    { key: "digital-normal", label: "数电发票（普通发票）" },
    { key: "electronic", label: "增值税电子普通发票" },
    { key: "normal", label: "增值税普通发票" },
    { key: "digital-vehicle", label: "数电发票（机动车销售统一发票）" },
    { key: "vehicle", label: "机动车销售统一发票" },
    { key: "digital-used-car", label: "数电发票（二手车销售统一发票）" },
    { key: "used-car", label: "二手车销售统一发票" },
    { key: "vehicle-tax", label: "税控机动车专用发票" },
    { key: "toll", label: "道路通行费电子普通发票" },
    { key: "digital-toll", label: "数电发票（通行费发票）" },
    { key: "air", label: "数电发票（航空运输电子客票行程单）" },
    { key: "rail", label: "数电发票（铁路电子客票）" },
    { key: "sales-unbilled", label: "无票收入（未开具发票）" },
    { key: "sales-adjust", label: "纳税检查调整" },
    { key: "other", label: "其他发票" },
];

/** 进项发票种类：顺序与税局取票清单一致（机动车在前、普通发票在后），末端补进项专有凭证。 */
const PURCHASE_KINDS: KindDef[] = [
    { key: "digital-special", label: "数电发票（增值税专用发票）" },
    { key: "special", label: "增值税专用发票" },
    { key: "digital-vehicle", label: "数电发票（机动车销售统一发票）" },
    { key: "vehicle", label: "机动车销售统一发票" },
    { key: "vehicle-tax", label: "税控机动车专用发票" },
    { key: "toll", label: "道路通行费电子普通发票" },
    { key: "digital-toll", label: "数电发票（通行费发票）" },
    { key: "air", label: "数电发票（航空运输电子客票行程单）" },
    { key: "rail", label: "数电发票（铁路电子客票）" },
    { key: "digital-normal", label: "数电发票（普通发票）" },
    { key: "electronic", label: "增值税电子普通发票" },
    { key: "normal", label: "增值税普通发票" },
    { key: "digital-used-car", label: "数电发票（二手车销售统一发票）" },
    { key: "used-car", label: "二手车销售统一发票" },
    { key: "customs", label: "海关进口增值税专用缴款书" },
    { key: "wht", label: "代扣代缴税收缴款凭证" },
    { key: "agri", label: "农产品收购发票" },
    { key: "other", label: "其他发票" },
];

const DIGITAL_INVOICE = /数电|全电/;

/** 把本系统录入 / 导入的发票类型归到标准票种；认不出的落「其他发票」。 */
function classify(rawType: string | undefined, direction: "sales" | "purchase"): string {
    const type = (rawType ?? "").replace(/\s+/g, "");
    const digital = DIGITAL_INVOICE.test(type);

    if (direction === "purchase") {
        if (type.includes("海关") || type.includes("进口")) return "customs";
        if (type.includes("代扣代缴")) return "wht";
        if (type.includes("农产品") || type.includes("收购")) return "agri";
    } else {
        if (type.includes("无票") || type.includes("未开具")) return "sales-unbilled";
        if (type.includes("纳税检查")) return "sales-adjust";
    }

    if (type.includes("二手车")) return digital ? "digital-used-car" : "used-car";
    if (type.includes("机动车")) {
        if (digital) return "digital-vehicle";
        return type.includes("专用") ? "vehicle-tax" : "vehicle";
    }
    if (type.includes("通行费")) return digital ? "digital-toll" : "toll";
    if (type.includes("航空") || type.includes("行程单")) return "air";
    if (type.includes("铁路") || type.includes("火车")) return "rail";
    if (type.includes("专用")) return digital ? "digital-special" : "special";
    if (type.includes("电子普通") || type.includes("电子发票")) return "electronic";
    if (type.includes("普通")) return digital ? "digital-normal" : "normal";
    return "other";
}

interface KindRow {
    key: string;
    label: string;
    normal: number;
    red: number;
    amount: number;
    tax: number;
    total: number;
    invoices: InvoiceDto[];
}

interface DisplayKindRow extends KindRow {
    isTotal?: boolean;
}

function buildKindRows(kinds: KindDef[], invoices: InvoiceDto[], direction: "sales" | "purchase"): KindRow[] {
    const buckets = new Map<string, InvoiceDto[]>();
    for (const invoice of invoices) {
        const key = classify(invoice.invoiceType, direction);
        buckets.set(key, [...(buckets.get(key) ?? []), invoice]);
    }

    return kinds.map((kind) => {
        const list = buckets.get(kind.key) ?? [];
        let normal = 0;
        let red = 0;
        let amount = 0;
        let tax = 0;
        let total = 0;
        for (const invoice of list) {
            const withTax = invoice.totalWithTax ?? 0;
            if (withTax < 0) red += 1;
            else normal += 1;
            amount += invoice.totalAmount ?? 0;
            tax += invoice.totalTax ?? 0;
            total += withTax;
        }
        return { key: kind.key, label: kind.label, normal, red, amount, tax, total, invoices: list };
    });
}

/** 合计行：份数与金额逐列累加，操作列显示「--」。 */
function buildTotalRow(rows: KindRow[]): DisplayKindRow {
    return rows.reduce<DisplayKindRow>(
        (acc, row) => ({
            ...acc,
            normal: acc.normal + row.normal,
            red: acc.red + row.red,
            amount: acc.amount + row.amount,
            tax: acc.tax + row.tax,
            total: acc.total + row.total,
        }),
        { key: "total", label: "合计", normal: 0, red: 0, amount: 0, tax: 0, total: 0, invoices: [], isTotal: true },
    );
}

interface DeductionRow {
    key: string;
    label: string;
    hint?: string;
    count: number;
    amount: number;
    tax: number;
    /** 金额列不适用（海关缴款书按税局附表二口径只统计税额）。 */
    amountNotApplicable?: boolean;
}

/** 进项抵扣类型（对应增值税申报表附表二项目），顺序与税局统计表一致。 */
const DEDUCTION_KINDS: Array<{
    key: string;
    label: string;
    kinds: string[];
    hint?: string;
    amountNotApplicable?: boolean;
}> = [
    {
        key: "special",
        label: "认证相符的增值税专用发票",
        kinds: ["digital-special", "special", "digital-vehicle", "vehicle", "vehicle-tax"],
        hint: "本期已认证的增值税专用发票，含数电专票、机动车销售统一发票和税控机动车专用发票。",
    },
    { key: "customs", label: "海关进口增值税缴款书", kinds: ["customs"], amountNotApplicable: true },
    { key: "agri", label: "农产品收购发票或者销售发票", kinds: ["agri"] },
    { key: "wht", label: "代扣代缴税收缴款凭证", kinds: ["wht"] },
    { key: "agri-extra", label: "加计扣除农产品进项税额", kinds: [] },
    { key: "export", label: "外贸企业进项税额抵扣证明", kinds: [] },
    {
        key: "other",
        label: "其他",
        kinds: ["digital-normal", "electronic", "normal", "toll", "digital-toll", "air", "rail", "digital-used-car", "used-car", "other"],
        hint: "除专用发票、海关缴款书、农产品收购 / 销售发票、代扣代缴凭证以外的已认证发票，如通行费、旅客运输和普通发票。",
    },
];

/** 按附表二抵扣类型归集已认证进项发票；一份发票只会落在一个类型下。 */
function buildDeductionRows(invoices: InvoiceDto[]): DeductionRow[] {
    const byKind = new Map<string, InvoiceDto[]>();
    for (const invoice of invoices) {
        const kind = classify(invoice.invoiceType, "purchase");
        byKind.set(kind, [...(byKind.get(kind) ?? []), invoice]);
    }

    return DEDUCTION_KINDS.map((category) => {
        const list = category.kinds.flatMap((kind) => byKind.get(kind) ?? []);
        let amount = 0;
        let tax = 0;
        for (const invoice of list) {
            amount += invoice.totalAmount ?? 0;
            tax += invoice.totalTax ?? 0;
        }
        return {
            key: category.key,
            label: category.label,
            hint: category.hint,
            count: list.length,
            amount,
            tax,
            amountNotApplicable: category.amountNotApplicable,
        };
    });
}

const fmtMoney = (value: number) =>
    value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** 后端日期字段运行时是 Date，统一按 yyyy-MM-dd 展示。 */
const dayOf = (value: Date | string | null | undefined) =>
    value ? new Date(value).toISOString().substring(0, 10) : "";

const fmtDate = (value: Date | string | null | undefined) => dayOf(value) || "—";

/** 日期是否落在会计期间内（yyyy-MM-dd 字符串可直接比较）。 */
const inPeriod = (value: Date | string | null | undefined, period: AccountingPeriod) => {
    const day = dayOf(value);
    return day !== "" && day >= period.startDate && day <= period.endDate;
};

/** 进项发票的抵扣归属期：优先取认证时登记的税款所属期，未登记时退回认证日期；
 *  税局一键取票的发票没有认证信息，按开票日期归属。 */
const belongsToPeriod = (invoice: InvoiceDto, period: AccountingPeriod) => {
    if (invoice.taxPeriod != null) return invoice.taxPeriod === period.period;
    return inPeriod(invoice.authDate ?? invoice.issueDate, period);
};

export function InvoiceFetchPage() {
    const period = useTaxPeriod();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const navigate = useNavigate();
    const { token } = useAuth();
    const { message } = App.useApp();

    const [tab, setTab] = useState<TabKey>("sales");
    const [sales, setSales] = useState<InvoiceDto[]>([]);
    const [purchase, setPurchase] = useState<InvoiceDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [detail, setDetail] = useState<KindRow | null>(null);
    /** 税局登录态由服务器持有：进入本页先确认一次，失效时就地补一次短信验证码。 */
    const [portalState, setPortalState] = useState<"checking" | "valid" | "expired">("checking");
    const [loginOpen, setLoginOpen] = useState(false);
    const [syncing, setSyncing] = useState(false);

    useEffect(() => {
        if (!accountSetId || !token) return;
        let cancelled = false;
        void resumeTaxPortalLogin(accountSetId, token)
            .then((result) => {
                if (!cancelled) setPortalState(result.state === "success" ? "valid" : "expired");
            })
            .catch(() => {
                if (!cancelled) setPortalState("expired");
            });
        return () => {
            cancelled = true;
        };
    }, [accountSetId, token]);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
        const api = new InvoicesApi(apiConfig(token));
        const range = { startDate: new Date(period.startDate), endDate: new Date(period.endDate) };
        try {
            const [salesInvoices, purchaseInvoices] = await Promise.all([
                api.accountsetsAccountSetIdInvoicesGet({ accountSetId, direction: 1, ...range }),
                // 进项按认证归属期统计：本期认证的发票可能是往月开具的，不能按开票日期截取。
                api.accountsetsAccountSetIdInvoicesGet({ accountSetId, direction: 2 }),
            ]);
            setSales(salesInvoices.filter((invoice) => invoice.source === TAX_SOURCE));
            setPurchase(purchaseInvoices.filter((invoice) => invoice.source === TAX_SOURCE));
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载发票失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, period.startDate, period.endDate]);

    useEffect(() => {
        void reload();
    }, [reload]);

    const salesRows = useMemo(() => buildKindRows(SALES_KINDS, sales, "sales"), [sales]);
    /** 本期认证的进项发票：认证时登记了税款所属期的按所属期，未登记的按认证日期。 */
    const certifiedPurchases = useMemo(
        () => purchase.filter((invoice) => belongsToPeriod(invoice, period)),
        [purchase, period],
    );
    const purchaseRows = useMemo(
        () => buildKindRows(PURCHASE_KINDS, certifiedPurchases, "purchase"),
        [certifiedPurchases],
    );
    /** 进项统计表与「进项发票」页签同源：只统计已认证发票。 */
    const deductionRows = useMemo(() => buildDeductionRows(certifiedPurchases), [certifiedPurchases]);

    const kindTableRows = useMemo(() => {
        const rows = tab === "sales" ? salesRows : purchaseRows;
        return [...rows, buildTotalRow(rows)];
    }, [tab, salesRows, purchaseRows]);

    /** 合计行只标出状态，底色与加粗由 index.css 的 .pk-row-total 统一给出。 */
    const stripe = (row: unknown) => ((row as { isTotal?: boolean }).isTotal ? { className: "pk-row-total" } : {});

    /** 票种名：本期没有发票的行压一档灰，有数据的行才加粗。 */
    const kindLabel = (value: string, count: number) => (
        <span className={count === 0 ? "text-(--pk-text-2)" : "font-medium text-(--pk-text)"}>{value}</span>
    );

    /** 金额单元格：0 表示该类本期没有票，不该和真实金额一样抢眼。 */
    const money = (value: number) => (
        <span className={value === 0 ? "tabular-nums text-(--pk-text-2) opacity-60" : "tabular-nums"}>
            {fmtMoney(value)}
        </span>
    );

    const opColumn: TableColumnsType<DisplayKindRow>[number] = {
        title: "功能",
        key: "op",
        width: "9%",
        onCell: stripe,
        render: (_, row) =>
            row.isTotal ? (
                <span className="text-(--pk-text-2) opacity-60">—</span>
            ) : (
                <Button type="link" onClick={() => setDetail(row)}>
                    操作
                </Button>
            ),
    };

    const salesKindColumns: TableColumnsType<DisplayKindRow> = [
        {
            title: "发票种类",
            dataIndex: "label",
            key: "label",
            width: "27%",
            onCell: stripe,
            render: (value: string, row) => kindLabel(value, row.normal + row.red),
        },
        {
            title: "发票份数",
            key: "count",
            width: "17%",
            onCell: stripe,
            render: (_, row) => {
                const empty = row.normal + row.red === 0;
                return (
                    <span className={cn("text-[12.5px] tabular-nums", empty && "text-(--pk-text-2) opacity-60")}>
                        正常{row.normal}份，红冲{row.red}份
                    </span>
                );
            },
        },
        { title: "金额", dataIndex: "amount", key: "amount", width: "15%", onCell: stripe, ...AMOUNT_COL, render: money },
        { title: "税额", dataIndex: "tax", key: "tax", width: "14%", onCell: stripe, ...AMOUNT_COL, render: money },
        { title: "价税合计", dataIndex: "total", key: "total", width: "15%", onCell: stripe, ...AMOUNT_COL, render: money },
        opColumn,
    ];

    const purchaseKindColumns: TableColumnsType<DisplayKindRow> = [
        {
            title: "发票种类",
            dataIndex: "label",
            key: "label",
            width: "32%",
            onCell: stripe,
            render: (value: string, row) => kindLabel(value, row.normal + row.red),
        },
        {
            title: "认证发票份数",
            key: "count",
            width: "14%",
            align: "center",
            onCell: stripe,
            render: (_, row) => {
                const count = row.normal + row.red;
                return (
                    <span className={cn("tabular-nums", count === 0 && "text-(--pk-text-2) opacity-60")}>{count}</span>
                );
            },
        },
        {
            title: "认证发票金额",
            dataIndex: "amount",
            key: "amount",
            width: "18%",
            onCell: stripe,
            ...AMOUNT_COL,
            render: money,
        },
        {
            title: "认证发票有效抵扣税额",
            dataIndex: "tax",
            key: "tax",
            width: "26%",
            onCell: stripe,
            ...AMOUNT_COL,
            render: money,
        },
        opColumn,
    ];

    const kindColumns = tab === "purchase" ? purchaseKindColumns : salesKindColumns;

    const deductionColumns: TableColumnsType<DeductionRow> = [
        {
            title: "进项抵扣类型",
            dataIndex: "label",
            key: "label",
            width: "36%",
            onCell: stripe,
            render: (value: string, row) => (
                <span
                    className={cn(
                        "flex items-center gap-1",
                        row.count === 0 ? "text-(--pk-text-2)" : "font-medium text-(--pk-text)",
                    )}
                >
                    {value}
                    {row.hint && (
                        <Tooltip title={row.hint}>
                            <QuestionCircleOutlined className="text-[12px] opacity-50" />
                        </Tooltip>
                    )}
                </span>
            ),
        },
        {
            title: "份数",
            dataIndex: "count",
            key: "count",
            width: "14%",
            onCell: stripe,
            render: (value: number) => (
                <span className={cn("tabular-nums", value === 0 && "text-(--pk-text-2) opacity-60")}>{value}</span>
            ),
        },
        {
            title: "金额",
            dataIndex: "amount",
            key: "amount",
            width: "22%",
            onCell: stripe,
            ...AMOUNT_COL,
            render: (value: number, row) =>
                row.amountNotApplicable && value === 0 ? (
                    <span className="text-(--pk-text-2) opacity-60">—</span>
                ) : (
                    money(value)
                ),
        },
        { title: "税额", dataIndex: "tax", key: "tax", width: "22%", onCell: stripe, ...AMOUNT_COL, render: money },
    ];

    const detailColumns = useMemo<TableColumnsType<InvoiceDto>>(() => {
        const columns: TableColumnsType<InvoiceDto> = [
            {
                title: "发票号码",
                dataIndex: "invoiceNo",
                key: "invoiceNo",
                width: 150,
                render: (value: string | null | undefined) => <span className="tabular-nums">{value ?? "—"}</span>,
            },
            {
                title: "开票日期",
                dataIndex: "issueDate",
                key: "issueDate",
                width: 100,
                render: (value) => <span className="tabular-nums">{fmtDate(value)}</span>,
            },
            {
                title: tab === "purchase" ? "供应商名称" : "客户名称",
                dataIndex: "partnerName",
                key: "partnerName",
                width: 180,
            },
            { title: "金额", dataIndex: "totalAmount", key: "totalAmount", width: 110, ...AMOUNT_COL, render: (value: number) => fmtMoney(value ?? 0) },
            { title: "税额", dataIndex: "totalTax", key: "totalTax", width: 100, ...AMOUNT_COL, render: (value: number) => fmtMoney(value ?? 0) },
            {
                title: "价税合计",
                dataIndex: "totalWithTax",
                key: "totalWithTax",
                width: 120,
                ...AMOUNT_COL,
                render: (value: number) => fmtMoney(value ?? 0),
            },
        ];
        if (tab === "purchase") {
            columns.push(
                {
                    title: "认证日期",
                    dataIndex: "authDate",
                    key: "authDate",
                    width: 100,
                    render: (value) => <span className="tabular-nums">{fmtDate(value)}</span>,
                },
                {
                    title: "税款所属期",
                    dataIndex: "taxPeriod",
                    key: "taxPeriod",
                    width: 100,
                    render: (value: number | null | undefined) =>
                        value ? <span className="tabular-nums">{`${Math.floor(value / 100)}-${String(value % 100).padStart(2, "0")}`}</span> : "—",
                },
            );
        }
        columns.push(
            { title: "入账状态", dataIndex: "statusText", key: "statusText", width: 90 },
            {
                title: "关联凭证",
                dataIndex: "voucherText",
                key: "voucherText",
                width: 120,
                render: (value: string | null | undefined) => value ?? "—",
            },
        );
        return columns;
    }, [tab]);

    /**
     * 一键取票：未登录先弹登录提示；已登录时提交后台取票任务，等税局查询与落库完成再刷新列表。
     * 登录态在取票途中失效时，服务器会回 needLogin，这里把状态条切回未登录并重新弹登录框。
     */
    const handleFetchInvoices = async () => {
        if (portalState !== "valid") {
            setLoginOpen(true);
            return;
        }
        if (syncing || !accountSetId) return;

        setSyncing(true);
        try {
            const job = await syncTaxInvoices(accountSetId, token, period.startDate, period.endDate);
            if (job.needLogin) {
                setPortalState("expired");
                setLoginOpen(true);
                return;
            }

            if (job.status !== "success") {
                message.error(job.message ?? "一键取票失败，请稍后重试。");
                return;
            }

            const result = job.data;
            if (!result) {
                message.success("取票完成，请刷新列表查看。");
                return;
            }

            if (result.fetched === 0) {
                // 税局明确回 0 条：正常空结果，销项 / 进项保持空表，不写库。
                message.info("税局本期没有返回发票，销项 / 进项保持为空。");
            } else {
                const skipped = result.skipped > 0 ? `，跳过 ${result.skipped} 张` : "";
                message.success(`已取回 ${result.fetched} 张发票，新增 ${result.imported} 张${skipped}。`);
                if (result.warnings?.length) {
                    message.warning(result.warnings.join("；"));
                }
            }
            await reload();
        } catch (err) {
            message.error(err instanceof Error ? err.message : "一键取票失败，请稍后重试。");
        } finally {
            setSyncing(false);
        }
    };

    return (
        <>
            <Card>
                <div className="flex shrink-0 items-center gap-6 border-b border-(--pk-border)">
                    {TABS.map((item) => (
                        <button
                            key={item.key}
                            type="button"
                            onClick={() => {
                                setTab(item.key);
                                setDetail(null);
                            }}
                            className={cn(
                                "relative h-[42px] text-[13px] transition-colors",
                                tab === item.key
                                    ? "font-medium text-(--pk-text)"
                                    : "text-(--pk-text-2) hover:text-(--pk-text)",
                            )}
                        >
                            {item.label}
                            {tab === item.key && (
                                <span className="absolute inset-x-0 bottom-0 h-[2px] bg-(--pk-brand-text)" />
                            )}
                        </button>
                    ))}
                </div>

                <div className="flex shrink-0 flex-wrap items-center gap-2 py-3">
                    <PortalState state={portalState} onVerify={() => setLoginOpen(true)} />
                    <div className="ml-auto flex flex-wrap items-center gap-2">
                        <Button
                            onClick={() => navigate(setPath(accountSetId, `/invoices/${tab === "sales" ? "sales" : "purchase"}`))}
                        >
                            {tab === "sales" ? "记账销项" : "记账进项"}
                        </Button>
                        <Tooltip
                            title={portalState === "valid" ? "从电子税务局获取本期的销项与进项发票" : "请先登录电子税务局"}
                        >
                            <Button type="primary" loading={syncing} onClick={() => void handleFetchInvoices()}>
                                一键取票
                            </Button>
                        </Tooltip>
                        <Button disabled title="生成申报表需税局取数完成，当前尚未接入">
                            生成申报表
                        </Button>
                        <Tooltip title="刷新">
                            <Button type="text" icon={<ReloadOutlined />} aria-label="刷新" onClick={() => void reload()} />
                        </Tooltip>
                    </div>
                </div>

                {error && <Alert type="error" showIcon title={error} className="mb-3" />}

                {tab === "purchase-stats" ? (
                    <>
                        <Table<DeductionRow>
                            columns={deductionColumns}
                            dataSource={deductionRows}
                            rowKey="key"
                            pagination={false}
                            tableLayout="fixed"
                            loading={loading}
                            sticky
                        />
                        <div className="mb-4 mt-4 rounded-md bg-(--pk-gray-bg) px-4 py-3 text-[12.5px] leading-6 text-(--pk-text-2)">
                            <div className="mb-1 font-medium text-(--pk-text)">说明：</div>
                            <div>1、以上数据源自【发票获取--进项发票】已认证发票统计得出；</div>
                            <div>2、生成申报表时，附表二对应项目按此填充；</div>
                            <div>
                                3、系统暂不支持农产品、海关缴款书、代扣代缴完税凭证等场景的智能取数填表，请手工核对。
                            </div>
                        </div>
                    </>
                ) : (
                    <Table<DisplayKindRow>
                        columns={kindColumns}
                        dataSource={kindTableRows}
                        rowKey="key"
                        pagination={false}
                        tableLayout="fixed"
                        loading={loading}
                        sticky
                    />
                )}

                <p className="pt-3 pb-4 text-[12.5px] leading-5 text-(--pk-text-2)">
                    {tab !== "purchase-stats" && (
                        <>
                            销项按开票日期归属本期；进项按认证时登记的税款所属期（未登记则按认证日期，税局取票的发票按开票日期）归属本期。
                            本页只展示「一键取票」从电子税务局取回并落库的发票，手工录入 / 导入的发票请到「记账销项 / 记账进项」查看。
                        </>
                    )}
                    快捷入口：
                    <Typography.Link onClick={() => navigate(setPath(accountSetId, "/tax/login"))}>
                        税务数字账户
                    </Typography.Link>
                </p>
            </Card>

            <TaxLoginPrompt
                open={loginOpen}
                accountSetId={accountSetId}
                reason="取票前需要先登录电子税务局：服务器会用账套里保存的税局资料登录，只有税局发来短信时才需要你填写验证码。"
                onClose={() => setLoginOpen(false)}
                onSuccess={() => {
                    setLoginOpen(false);
                    setPortalState("valid");
                }}
            />

            <Modal
                open={detail !== null}
                title={detail ? `${detail.label}（${period.label}${tab === "purchase" ? " 已认证" : ""}）` : ""}
                width={tab === "purchase" ? 1240 : 1000}
                onCancel={() => setDetail(null)}
                footer={<Button onClick={() => setDetail(null)}>关闭</Button>}
            >
                {detail && detail.invoices.length > 0 ? (
                    <Table<InvoiceDto>
                        columns={detailColumns}
                        dataSource={detail.invoices}
                        rowKey={(row) => row.id ?? row.invoiceNo ?? ""}
                        pagination={false}
                        scroll={{ x: "max-content" }}
                    />
                ) : (
                    <div className="py-8 text-center text-[13px] text-(--pk-text-2)">{tab === "purchase" ? "本期该类发票没有已认证记录" : "本期该类发票没有记录"}</div>
                )}
            </Modal>
        </>
    );
}

/** 税局登录态：一个圆点 + 结论，失效时才多给一个认证入口。 */
function PortalState({ state, onVerify }: { state: "checking" | "valid" | "expired"; onVerify: () => void }) {
    if (state === "checking") {
        return (
            <span className="flex items-center gap-2 text-[12.5px] text-(--pk-text-2)">
                <span className="size-1.5 rounded-full bg-(--pk-text-2) opacity-40" />
                正在确认税局登录态…
            </span>
        );
    }
    if (state === "valid") {
        return (
            <span className="flex items-center gap-2 text-[12.5px] text-(--pk-green)">
                <span className="size-1.5 rounded-full bg-(--pk-green)" />
                税局已登录
            </span>
        );
    }
    return (
        <span className="flex items-center gap-2 text-[12.5px] text-(--pk-red)">
            <span className="size-1.5 rounded-full bg-(--pk-red)" />
            税局未登录
            <Typography.Link onClick={onVerify}>前往认证</Typography.Link>
        </span>
    );
}
