/**
 * 复刻区发票模块（对应柠檬云 /Invoice/*）：
 * 一键取票（引导页，建设中）、销项发票、进项发票（期间可切换、批量认证已接后端单条认证端点、
 * 生成凭证已接后端单张 / 批量端点，科目可留空由后端按方向取默认）、
 * 费用单据（已接后端）、发票资金一览表（已接后端）、发票风险分析（建设中）。
 * 商品明细页签把发票列表接口返回的行明细直接摊平展示（真实数据）；
 * 暂不入账页签走发票列表接口 status=3（暂不入账），「入账」经状态变更端点恢复发票进入账流程。
 * 视觉遵循全站「云财蓝」设计语言：主色 #2f6bff、直角卡片、白卡 + slate 灰阶文字，
 * 页头统一由 Page 基元的渐变横幅渲染。
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Button, Dropdown, Modal } from "antd";
import {
    AlertOutlined,
    BankOutlined,
    FileDoneOutlined,
    FileTextOutlined,
    KeyOutlined,
    ProfileOutlined,
    QuestionCircleOutlined,
    ReloadOutlined,
    WalletOutlined,
} from "@ant-design/icons";

import { ICON_STYLE, type PillTone } from "@/components/page/styles";

import {
    Btn,
    CheckBox,
    Dialog,
    EmptyTip,
    FakeInput,
    FakeSelect,
    DateInput,
    HintBox,
    LinkAction,
    Page,
    Pager,
    PeriodStepper,
    RefreshIcon,
    Select,
    SubTabs,
    Table,
    TextInput,
    Toast,
    ToolRow,
    WipBtn,
    WipNotice,
    WipTag,
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { StatusPill } from "@/components/page/status-pill";
import { AccountsApi, ExpensesApi, InvoicesApi, type AccountDto, type ExpenseBillDto, type ExpenseImportResultDto, type InvoiceDto, type InvoiceFundOverviewRowDto, type InvoiceImportResult, type InvoiceSummaryDto } from "@/apis-gen";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { makePeriod, shiftPeriod, useDefaultAccountingPeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";

/** 后端日期字段运行时是 Date，统一按 yyyy-MM-dd 展示。 */
const fmtDate = (v: Date | string | null | undefined): string => (v ? new Date(v).toISOString().substring(0, 10) : "");
/** 税款所属期 yyyyMM → yyyy-MM。 */
const fmtTaxPeriod = (p: number | null | undefined): string => (p ? `${String(p).slice(0, 4)}-${String(p).slice(4)}` : "");
/** 税率兼容小数（0.13）与百分数（13）两种口径。 */
const fmtRate = (v: number | null | undefined): string => (v == null ? "" : `${v > 0 && v < 1 ? Math.round(v * 100) : v}%`);

/** 发票数据 Hook（按方向 / 区间 / 入账状态取列表与合计）。 */
function useInvoiceData(direction: 1 | 2, startDate: string, endDate: string, status?: number) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [invoices, setInvoices] = useState<InvoiceDto[]>([]);
    const [summary, setSummary] = useState<InvoiceSummaryDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
        const api = new InvoicesApi(apiConfig(token));
        const range = { startDate: new Date(startDate), endDate: new Date(endDate) };
        try {
            setInvoices(await api.accountsetsAccountSetIdInvoicesGet({ accountSetId, direction, status, ...range }));
            setSummary(await api.accountsetsAccountSetIdInvoicesSummaryGet({ accountSetId, direction, status, ...range }));
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载发票失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, direction, status, startDate, endDate]);

    useEffect(() => {
        void reload();
    }, [reload]);

    return { invoices, summary, loading, error, reload };
}

/** 发票资金一览表数据 Hook（含加载与错误状态）。 */
function useFundOverview(startDate: string, endDate: string) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [rows, setRows] = useState<InvoiceFundOverviewRowDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const reload = useCallback(async () => {
        if (!accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
        try {
            setRows(await new InvoicesApi(apiConfig(token)).accountsetsAccountSetIdInvoicesFundOverviewGet({
                accountSetId,
                startDate: new Date(startDate),
                endDate: new Date(endDate),
            }));
            setError(null);
        } catch (err) {
            setRows([]);
            setError(await apiErrorMessage(err, "加载发票资金一览表失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, startDate, endDate]);
    useEffect(() => {
        void reload();
    }, [reload]);
    return { rows, loading, error, reload };
}

/* ---------- 一键取票（引导页，需税局取票服务，外部对接未接入） ---------- */

const FETCH_POINTS = [
    { title: "智能获取发票数据", desc: "一键获取进销项发票，轻松获取1万+发票" },
    { title: "数电发票电子文件采集", desc: "智能获取数电发票电子文件，自动关联发票数据" },
    { title: "数据安全保障", desc: "单向式获取发票数据，数据安全有保障" },
];

export function LemonInvoiceFetch() {
    return (
        <Page
            title="一键取票"
            subtitle="只需一次维护，即可直接获取进销项发票数据、数电发票电子文件，实现发票智能化"
            icon={ReloadOutlined}
        >
            <WipNotice text="一键取票需对接税局发票取得服务，后端尚未提供端点，本页为功能介绍（建设中）。" />
            <div className="flex flex-1 flex-col items-center justify-center py-16">
                <div className="flex w-[860px] max-w-full items-center gap-10">
                    {/* 左侧显示器插画（简化为示意框） */}
                    <div className="flex h-[220px] w-[300px] shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-[#fafbfd] text-[13px] text-slate-400">
                        取票界面示意
                    </div>
                    <div className="flex flex-1 flex-col gap-6">
                        {FETCH_POINTS.map((p, i) => (
                            <div key={p.title} className="flex items-start gap-3">
                                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[#2f6bff] text-[13px] text-white">
                                    {i + 1}
                                </span>
                                <div>
                                    <div className="text-[15px] font-bold text-slate-800">{p.title}</div>
                                    <div className="mt-1 text-[13px] text-slate-500">{p.desc}</div>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
                <div className="mt-12">
                    <WipBtn title="一键取票建设中，后端未提供取票端点">立即取票</WipBtn>
                </div>
            </div>
        </Page>
    );
}

/* ---------- 销项/进项发票列表 ---------- */

function HotBadge() {
    return (
        <span className="absolute -top-2 -right-3 rounded-full bg-[#ef4444] px-1 text-[10px] leading-[14px] text-white">
            Hot
        </span>
    );
}

function HotBtn({ children }: { children: string }) {
    return (
        <span className="relative inline-flex">
            <WipBtn title={`${children}建设中，后端未提供端点`}>{children}</WipBtn>
            <HotBadge />
        </span>
    );
}

/** 发票状态文案 → 状态胶囊色调（纯视觉口径：完成类绿、暂不入账类橙、其余蓝，空值灰）。 */
function invoiceStatusTone(text?: string | null): PillTone {
    if (!text) return "gray";
    if (text.includes("暂不")) return "amber";
    if (text.startsWith("已")) return "green";
    return "blue";
}

/** 发票列表页（销项/进项共用骨架；新增发票与进项批量认证已接后端）。 */
function InvoiceListPage({ kind }: { kind: "sales" | "purchase" }) {
    const [tab, setTab] = useState(0);
    const defaultPeriod = useDefaultAccountingPeriod();
    const [userPeriod, setUserPeriod] = useState<AccountingPeriod | null>(null);
    const period = userPeriod ?? defaultPeriod;
    const stepPeriod = (d: number) => setUserPeriod(shiftPeriod(period, d));
    const data = useInvoiceData(kind === "sales" ? 1 : 2, period.startDate, period.endDate);
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [showAdd, setShowAdd] = useState(false);
    const [addForm, setAddForm] = useState({ invoiceType: "增值税专用发票", issueDate: "", invoiceNo: "", partnerName: "", goodsName: "", amount: "", taxRate: "13" });
    const [addToast, setAddToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [showVoucher, setShowVoucher] = useState(false);
    /** 生成凭证弹窗：三个科目留空（"auto"）时由后端按方向取默认科目；凭证字默认「记」，日期默认取开票日期。 */
    const [voucherForm, setVoucherForm] = useState({ counterAccountId: "auto", subjectAccountId: "auto", taxAccountId: "auto", word: "记", date: "" });
    const [accounts, setAccounts] = useState<AccountDto[]>([]);
    const [showImport, setShowImport] = useState(false);
    const [importFile, setImportFile] = useState<File | null>(null);
    const [importResult, setImportResult] = useState<InvoiceImportResult | null>(null);
    const importInputRef = useRef<HTMLInputElement>(null);

    const toggleSel = (id: string) =>
        setSelected((prev) => {
            const next = new Set(prev);
            if (next.has(id)) {
                next.delete(id);
            } else {
                next.add(id);
            }
            return next;
        });
    const allSelected = data.invoices.length > 0 && data.invoices.every((inv) => selected.has(inv.id ?? ""));
    const toggleAll = () =>
        setSelected(allSelected ? new Set() : new Set(data.invoices.map((inv) => inv.id ?? "")));

    const createInvoice = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (!addForm.invoiceNo.trim() || !addForm.partnerName.trim() || !addForm.issueDate) {
            setAddToast("开票日期、发票号码、购销方名称必填");
            return;
        }
        setBusy(true);
        try {
            const amount = Number(addForm.amount) || 0;
            const rate = Number(addForm.taxRate) || 0;
            await new InvoicesApi(apiConfig(token!)).accountsetsAccountSetIdInvoicesPost({
                accountSetId,
                direction: kind === "sales" ? 1 : 2,
                saveInvoiceRequest: {
                    invoiceType: addForm.invoiceType,
                    inputDate: new Date(),
                    issueDate: new Date(addForm.issueDate),
                    invoiceNo: addForm.invoiceNo.trim(),
                    partnerName: addForm.partnerName.trim(),
                    lines: [
                        {
                            goodsName: addForm.goodsName.trim() || addForm.invoiceType,
                            amount,
                            taxRate: rate,
                            taxAmount: Math.round(amount * rate) / 100,
                        },
                    ],
                },
            });
            setShowAdd(false);
            // 开票日期若落在当前期间之外（例如账套工作期间是去年，登记本月发票），
            // 自动把期间切到开票月份，避免「提示已登记却在列表看不见」。
            const issued = addForm.issueDate;
            if (issued && (issued < period.startDate || issued > period.endDate)) {
                const [y, m] = issued.split("-").map(Number);
                if (y && m) setUserPeriod(makePeriod(y, m));
            }
            setAddForm({ invoiceType: "增值税专用发票", issueDate: "", invoiceNo: "", partnerName: "", goodsName: "", amount: "", taxRate: "13" });
            setAddToast("发票已登记");
            await data.reload();
        } catch (e) {
            setAddToast(await apiErrorMessage(e, "登记失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 二进制下载：后端返回 application/octet-stream 的 xlsx，用 blob + a[download] 落盘。 */
    const downloadBlob = async (url: string, fallbackName: string) => {
        const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
        if (!res.ok) {
            setAddToast(await apiErrorMessage(await res.json().catch(() => null), "下载失败，请重试。"));
            return;
        }
        const disposition = res.headers.get("Content-Disposition") ?? "";
        const star = /filename\*=UTF-8''([^;\n]+)/i.exec(disposition);
        const plain = /filename="?([^";\n]+)"?/i.exec(disposition);
        const name = star ? decodeURIComponent(star[1]) : plain?.[1] ?? fallbackName;
        const url2 = URL.createObjectURL(await res.blob());
        const anchor = document.createElement("a");
        anchor.href = url2;
        anchor.download = name;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url2);
    };

    /** 导出当前期间发票；文件名由后端按方向与期间生成。 */
    const exportInvoices = async () => {
        if (!token || !accountSetId) {
            return;
        }
        await downloadBlob(
            `/api/accountsets/${accountSetId}/invoices/export?direction=${kind === "sales" ? 1 : 2}` +
                `&startDate=${period.startDate}&endDate=${period.endDate}&period=${period.year}-${String(period.month).padStart(2, "0")}`,
            `${kind === "sales" ? "销项" : "进项"}发票.xlsx`,
        );
        setAddToast("导出已开始下载");
    };

    /** 下载导入模板（与导出同列，仅含表头）。 */
    const downloadTemplate = async () => {
        if (!token || !accountSetId) {
            return;
        }
        await downloadBlob(
            `/api/accountsets/${accountSetId}/invoices/import-template?direction=${kind === "sales" ? 1 : 2}`,
            `${kind === "sales" ? "销项" : "进项"}发票导入模板.xlsx`,
        );
    };

    /** 导入发票：选中的 .xlsx 走批量导入端点，按发票号码聚合明细行。 */
    const submitImport = async () => {
        if (!token || !accountSetId || !importFile) {
            setAddToast("请选择要导入的 .xlsx 文件");
            return;
        }
        setBusy(true);
        try {
            const body = new FormData();
            body.append("file", importFile);
            const res = await fetch(
                `/api/accountsets/${accountSetId}/invoices/import?direction=${kind === "sales" ? 1 : 2}`,
                { method: "POST", headers: { Authorization: `Bearer ${token}` }, body },
            );
            if (!res.ok) {
                setAddToast(await apiErrorMessage(await res.json().catch(() => null), "导入失败"));
                return;
            }
            const result = (await res.json()) as InvoiceImportResult;
            setImportResult(result);
            setImportFile(null);
            setShowImport(false);
            await data.reload();
            const warn = result.warnings?.length ? `，${result.warnings[0]}` : "";
            setAddToast(`导入完成：成功 ${result.imported ?? 0} 张，跳过 ${result.skipped ?? 0} 行${warn}`);
        } finally {
            setBusy(false);
        }
    };

    /** 批量操作：删除 / 置暂不入账 / 恢复未入账，均作用于勾选行。 */
    const runBulk = async (action: "delete" | "suspend" | "pending") => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (selected.size === 0) {
            setAddToast("请先勾选要操作的发票");
            return;
        }
        const ids = [...selected];
        const label = action === "delete" ? "删除" : action === "suspend" ? "置为暂不入账" : "恢复为未入账";
        if (action === "delete" && !window.confirm(`确定删除已勾选的 ${ids.length} 张发票吗？`)) {
            return;
        }
        setBusy(true);
        const api = new InvoicesApi(apiConfig(token));
        try {
            const res = action === "delete"
                ? await api.accountsetsAccountSetIdInvoicesBulkDeletePost({ accountSetId, bulkInvoiceIdsRequest: { ids } })
                : await api.accountsetsAccountSetIdInvoicesBulkStatusPost({
                    accountSetId,
                    bulkInvoiceStatusRequest: { ids, status: action === "suspend" ? 3 : 1 },
                });
            setSelected(new Set());
            setAddToast(`已${label} ${res.affected ?? 0} 张${(res.skipped ?? 0) > 0 ? `，跳过 ${res.skipped} 张` : ""}`);
            await data.reload();
        } catch (e) {
            setAddToast(await apiErrorMessage(e, `${label}失败`));
        } finally {
            setBusy(false);
        }
    };

    /** 批量认证：走批量端点，销项发票由后端跳过而不是整批失败。 */
    const bulkAuthenticate = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (selected.size === 0) {
            setAddToast("请先勾选要认证的进项发票");
            return;
        }
        setBusy(true);
        try {
            const res = await new InvoicesApi(apiConfig(token)).accountsetsAccountSetIdInvoicesBulkAuthenticatePost({
                accountSetId,
                bulkAuthenticateRequest: { ids: [...selected], authDate: new Date(period.endDate), taxPeriod: period.period },
            });
            setSelected(new Set());
            setAddToast(
                (res.skipped ?? 0) > 0
                    ? `已认证 ${res.affected ?? 0} 张，跳过 ${res.skipped} 张（仅进项发票可认证）`
                    : `已认证 ${res.affected ?? 0} 张发票`,
            );
            await data.reload();
        } catch (e) {
            setAddToast(await apiErrorMessage(e, "认证失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 生成凭证弹窗按需加载末级科目；已加载过则不再重复请求。 */
    const ensureAccounts = async () => {
        if (!token || !accountSetId || accounts.length > 0) {
            return;
        }
        try {
            setAccounts(
                await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({
                    accountSetId,
                    leafOnly: true,
                }),
            );
        } catch {
            // 科目加载失败不阻断弹窗：留空由后端按编码取默认科目。
        }
    };

    const openVoucherDialog = () => {
        void ensureAccounts();
        setShowVoucher(true);
    };

    /**
     * 批量生成凭证：把勾选的发票一次性提交给后端批量端点。
     * 后端逐张出票，已入账 / 暂不入账的计入跳过，单条失败不影响其余；
     * 科目留空（"auto"）时不传该字段，由后端按方向取默认科目。
     */
    const generateVouchers = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (selected.size === 0) {
            setAddToast("请先勾选要生成凭证的发票");
            return;
        }
        setBusy(true);
        const pick = (v: string) => (v && v !== "auto" ? v : undefined);
        try {
            const res = await new InvoicesApi(apiConfig(token)).accountsetsAccountSetIdInvoicesVouchersPost({
                accountSetId,
                generateInvoiceVouchersRequest: {
                    ids: [...selected],
                    options: {
                        counterAccountId: pick(voucherForm.counterAccountId),
                        subjectAccountId: pick(voucherForm.subjectAccountId),
                        taxAccountId: pick(voucherForm.taxAccountId),
                        word: voucherForm.word.trim() || undefined,
                        date: voucherForm.date ? new Date(voucherForm.date) : undefined,
                    },
                },
            });
            setShowVoucher(false);
            const skipped = res.skipped ?? 0;
            const failures = res.failures ?? [];
            const parts = [`已生成 ${res.created ?? 0} 张凭证`];
            if (skipped > 0) {
                parts.push(`跳过 ${skipped} 张（已入账 / 暂不入账）`);
            }
            if (failures.length > 0) {
                parts.push(`失败 ${failures.length} 张：${failures[0].reason}`);
            }
            setAddToast(parts.join("，"));
            setSelected(new Set());
            await data.reload();
        } catch (e) {
            setAddToast(await apiErrorMessage(e, "生成凭证失败"));
        } finally {
            setBusy(false);
        }
    };

    if (tab === 1) {
        return <InvoiceGoodsTable kind={kind} period={period} invoices={data.invoices} loading={data.loading} error={data.error} onTab={setTab} onStep={stepPeriod} />;
    }
    if (tab === 2) {
        return <InvoicePendingTable period={period} onTab={setTab} onStep={stepPeriod} />;
    }
    const cols: Col[] = [
        { key: "ck", title: <CheckBox label="" checked={allSelected} onChange={toggleAll} />, width: "36px" },
        { key: "type", title: "发票种类", width: "100px" },
        { key: "inputDate", title: "录入日期", width: "100px" },
        { key: "issueDate", title: "开票日期", width: "100px" },
        { key: "no", title: "发票号码", width: "150px" },
        { key: "partner", title: kind === "sales" ? "客户名称" : "供应商名称", width: "180px" },
        ...(kind === "purchase"
            ? ([{ key: "authDate", title: "认证日期", width: "100px" }, { key: "period", title: "税款所属期", width: "100px" }] as Col[])
            : []),
        { key: "biz", title: "业务类型", width: "100px" },
        { key: "goods", title: "商品名称", width: "150px" },
        { key: "amount", title: "合计金额", align: "right", width: "110px" },
        { key: "rate", title: "税率", width: "70px" },
        { key: "tax", title: "合计税额", align: "right", width: "100px" },
        { key: "total", title: "价税合计", align: "right", width: "110px" },
        { key: "voucher", title: "关联凭证", width: "110px" },
        { key: "source", title: "发票来源", width: "110px" },
        { key: "status", title: "状态", width: "80px" },
    ];
    const tabs = kind === "sales" ? ["销项发票", "商品明细"] : ["进项发票", "商品明细", "暂不入账发票"];
    return (
        <Page title={kind === "sales" ? "销项发票" : "进项发票"} subtitle={kind === "sales" ? "销项发票的登记、开具与入账情况" : "进项发票的登记、认证与抵扣情况"} icon={kind === "sales" ? FileTextOutlined : FileDoneOutlined}>
            <SubTabs tabs={tabs} active={tab} onChange={setTab} />
            <ToolRow
                left={
                    <PeriodStepper
                        text={`${period.year}年${period.month}月`}
                        onPrev={() => stepPeriod(-1)}
                        onNext={() => stepPeriod(1)}
                    />
                }
                right={
                    <>
                        <CheckBox label="显示所有信息" />
                        {kind === "sales" && <HotBtn>开具发票</HotBtn>}
                        <HotBtn>一键取票</HotBtn>
                        <Btn dropdown onClick={() => setShowAdd(true)}>新增发票</Btn>
                        <Btn dropdown disabled={busy} title="导出当前期间的发票为 .xlsx" onClick={() => void exportInvoices()}>导出</Btn>
                        <Btn dropdown title="从 .xlsx 导入发票" onClick={() => setShowImport(true)}>导入</Btn>
                        <Btn dropdown onClick={() => window.print()}>打印列表</Btn>
                        {kind === "purchase" && (
                            <Btn dropdown disabled={busy} title="对勾选的进项发票调用批量认证端点" onClick={() => void bulkAuthenticate()}>批量认证</Btn>
                        )}
                        <Dropdown
                            trigger={["click"]}
                            menu={{
                                items: [
                                    { key: "suspend", label: "批量置为暂不入账" },
                                    { key: "pending", label: "批量恢复为未入账" },
                                    { key: "delete", label: "批量删除" },
                                ],
                                onClick: ({ key }) => void runBulk(key as "delete" | "suspend" | "pending"),
                            }}
                        >
                            <Btn dropdown disabled={busy} title="对勾选发票执行批量操作">批量操作</Btn>
                        </Dropdown>
                        <Btn dropdown disabled={busy} title="对勾选的未入账发票逐张生成凭证" onClick={() => openVoucherDialog()}>生成凭证</Btn>
                        <RefreshIcon onClick={() => void data.reload()} />
                    </>
                }
            />
            <div className="flex-1">
                {error2(data.error)}
                {data.loading && <div className="px-3 py-2 text-[13px] text-slate-500">加载中…</div>}
                <Table
                    cols={cols}
                    rows={[
                        // 空数据时不渲染合计行，空态交给 Table 的 empty
                        ...(data.invoices.length
                            ? [
                                  {
                                      ck: "",
                                      type: "",
                                      inputDate: "",
                                      issueDate: "",
                                      no: "",
                                      partner: <span className="font-bold text-slate-800">合计</span>,
                                      authDate: "",
                                      period: "",
                                      biz: "",
                                      goods: "",
                                      amount: (data.summary?.totalAmount ?? 0).toFixed(2),
                                      rate: "",
                                      tax: (data.summary?.totalTax ?? 0).toFixed(2),
                                      total: (data.summary?.totalWithTax ?? 0).toFixed(2),
                                      voucher: "",
                                      source: "",
                                      status: "",
                                      __style: { background: "#fafbfd", fontWeight: 500 },
                                  } as Row,
                              ]
                            : []),
                        ...data.invoices.map(
                            (inv) =>
                                ({
                                    ck: <CheckBox label="" checked={selected.has(inv.id ?? "")} onChange={() => toggleSel(inv.id ?? "")} />,
                                    type: inv.invoiceType,
                                    inputDate: fmtDate(inv.inputDate),
                                    issueDate: fmtDate(inv.issueDate),
                                    no: inv.invoiceNo,
                                    partner: inv.partnerName,
                                    authDate: fmtDate(inv.authDate),
                                    period: fmtTaxPeriod(inv.taxPeriod),
                                    biz: inv.bizType,
                                    goods: inv.lines?.[0]?.goodsName ?? "",
                                    amount: (inv.totalAmount ?? 0).toFixed(2),
                                    rate: fmtRate(inv.lines?.[0]?.taxRate),
                                    tax: (inv.totalTax ?? 0).toFixed(2),
                                    total: (inv.totalWithTax ?? 0).toFixed(2),
                                    voucher: inv.voucherText,
                                    source: inv.source,
                                    status: <StatusPill tone={invoiceStatusTone(inv.statusText)}>{inv.statusText}</StatusPill>,
                                    __style: selected.has(inv.id ?? "") ? { background: "#eef4ff" } : undefined,
                                }) as Row
                        ),
                    ]}
                    empty={data.invoices.length === 0 && !data.loading ? <EmptyTip /> : undefined}
                />
            </div>
            {/* 真实数据不足一页时隐藏分页控件，不显示假页码 */}
            <Pager total={data.invoices.length} />
            {/* 新增发票弹窗 */}
            {showAdd && (
                <Dialog
                    title={kind === "sales" ? "新增销项发票" : "新增进项发票"}
                    onClose={() => setShowAdd(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void createInvoice()}>
                                保存
                            </Btn>
                            <Btn onClick={() => setShowAdd(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        {([
                            ["发票种类：", <Select key="t" className="w-[240px]" value={addForm.invoiceType} onChange={(v) => setAddForm({ ...addForm, invoiceType: v })} options={["增值税专用发票", "增值税普通发票", "电子发票"]} />],
                            ["开票日期：", <DateInput key="d" className="w-[240px]" value={addForm.issueDate} onChange={(v) => setAddForm({ ...addForm, issueDate: v })} />],
                            ["发票号码：", <TextInput key="n" className="w-[240px]" value={addForm.invoiceNo} onChange={(v) => setAddForm({ ...addForm, invoiceNo: v })} />],
                            [kind === "sales" ? "客户名称：" : "供应商名称：", <TextInput key="p" className="w-[240px]" value={addForm.partnerName} onChange={(v) => setAddForm({ ...addForm, partnerName: v })} />],
                            ["商品名称：", <TextInput key="g" className="w-[240px]" value={addForm.goodsName} onChange={(v) => setAddForm({ ...addForm, goodsName: v })} />],
                            ["不含税金额：", <TextInput key="a" className="w-[240px] text-right tabular-nums" value={addForm.amount} onChange={(v) => setAddForm({ ...addForm, amount: v.replace(/[^\d.]/g, "") })} />],
                            ["税率(%)：", <TextInput key="r" className="w-[240px] text-right tabular-nums" value={addForm.taxRate} onChange={(v) => setAddForm({ ...addForm, taxRate: v.replace(/[^\d.]/g, "") })} />],
                        ] as Array<[string, ReactNode]>).map(([label, control]) => (
                            <label key={label} className="flex items-center gap-3">
                                <span className="w-[110px] text-right">{label}</span>
                                {control}
                            </label>
                        ))}
                    </div>
                </Dialog>
            )}
            {/* 导入发票弹窗 */}
            {showImport && (
                <Dialog
                    title={kind === "sales" ? "导入销项发票" : "导入进项发票"}
                    onClose={() => setShowImport(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void submitImport()}>
                                导入
                            </Btn>
                            <Btn onClick={() => setShowImport(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        <HintBox>
                            支持 .xlsx；同一发票号码的多行商品会合并为一张发票的多条明细。已存在的发票号会被跳过。
                        </HintBox>
                        <div className="flex items-center gap-3">
                            <span className="w-[110px] text-right">选择文件：</span>
                            <input
                                ref={importInputRef}
                                type="file"
                                accept=".xlsx"
                                className="hidden"
                                onChange={(e) => setImportFile(e.target.files?.[0] ?? null)}
                            />
                            <Btn onClick={() => importInputRef.current?.click()}>选择文件</Btn>
                            <span className="text-[12px] text-slate-500">{importFile ? importFile.name : "未选择文件"}</span>
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[110px] text-right" />
                            <LinkAction onClick={() => void downloadTemplate()}>下载导入模板</LinkAction>
                        </div>
                    </div>
                </Dialog>
            )}
            {/* 生成凭证弹窗：科目留空由后端按发票方向取默认科目 */}
            {showVoucher && (
                <Dialog
                    title="生成凭证"
                    width={520}
                    onClose={() => setShowVoucher(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void generateVouchers()}>
                                确定
                            </Btn>
                            <Btn onClick={() => setShowVoucher(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        <HintBox>
                            已勾选 {selected.size} 张发票。销项生成「借 应收账款 价税合计 / 贷 收入 金额、贷 销项税额 税额」，进项生成「借 商品 金额、借 进项税额 税额 / 贷 应付账款 价税合计」。
                        </HintBox>
                        {([
                            [
                                "对方科目：",
                                <Select
                                    key="c"
                                    className="w-[240px]"
                                    value={voucherForm.counterAccountId}
                                    onChange={(v) => setVoucherForm({ ...voucherForm, counterAccountId: v })}
                                    options={[
                                        { value: "auto", label: kind === "sales" ? "自动：应收账款 1122" : "自动：应付账款 2202" },
                                        ...accounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
                                    ]}
                                />,
                            ],
                            [
                                kind === "sales" ? "收入科目：" : "商品科目：",
                                <Select
                                    key="s"
                                    className="w-[240px]"
                                    value={voucherForm.subjectAccountId}
                                    onChange={(v) => setVoucherForm({ ...voucherForm, subjectAccountId: v })}
                                    options={[
                                        { value: "auto", label: kind === "sales" ? "自动：主营业务收入 5001" : "自动：库存商品 1405" },
                                        ...accounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
                                    ]}
                                />,
                            ],
                            [
                                "税额科目：",
                                <Select
                                    key="t"
                                    className="w-[240px]"
                                    value={voucherForm.taxAccountId}
                                    onChange={(v) => setVoucherForm({ ...voucherForm, taxAccountId: v })}
                                    options={[
                                        { value: "auto", label: kind === "sales" ? "自动：销项税额 222100105" : "自动：进项税额 222100101" },
                                        ...accounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
                                    ]}
                                />,
                            ],
                            [
                                "凭证字：",
                                <TextInput key="w" className="w-[240px]" value={voucherForm.word} onChange={(v) => setVoucherForm({ ...voucherForm, word: v })} />,
                            ],
                            [
                                "凭证日期：",
                                <DateInput key="d" className="w-[240px]" value={voucherForm.date} onChange={(v) => setVoucherForm({ ...voucherForm, date: v })} />,
                            ],
                        ] as Array<[string, ReactNode]>).map(([label, control]) => (
                            <label key={label} className="flex items-center gap-3">
                                <span className="w-[110px] text-right">{label}</span>
                                {control}
                            </label>
                        ))}
                        <div className="pl-[122px] text-[12px] text-slate-400">凭证日期留空时取发票开票日期。</div>
                    </div>
                </Dialog>
            )}
            {/* 导入结果：成功 / 跳过张数与告警明细 */}
            <Modal
                open={importResult !== null}
                title="导入结果"
                width={520}
                onCancel={() => setImportResult(null)}
                onOk={() => setImportResult(null)}
                okText="确定"
                cancelText="关闭"
            >
                {importResult && (
                    <div className="flex flex-col gap-2 py-1 text-[13px] text-slate-700">
                        <div>
                            解析 {importResult.totalRows ?? 0} 行，成功导入 <b>{importResult.imported ?? 0}</b> 张发票，
                            跳过 <b>{importResult.skipped ?? 0}</b> 行。
                        </div>
                        {(importResult.warnings?.length ?? 0) > 0 && (
                            <div className="flex flex-col gap-1">
                                <span className="text-slate-500">告警：</span>
                                <ul className="ml-4 list-disc text-[12.5px] text-slate-600">
                                    {(importResult.warnings ?? []).map((w, i) => (
                                        <li key={i}>{w}</li>
                                    ))}
                                </ul>
                            </div>
                        )}
                    </div>
                )}
            </Modal>
            {addToast && <Toast text={addToast} onClose={() => setAddToast(null)} />}
        </Page>
    );
}

/** 错误可见化小件：有错误时渲染红色提示行，无错误渲染 null。 */
function error2(err: string | null) {
    return err ? <div className="px-3 py-2 text-[13px] text-[#ef4444]">{err}</div> : null;
}

/** 销项发票。 */
export function LemonSalesInvoice() {
    return <InvoiceListPage kind="sales" />;
}

/** 进项发票。 */
export function LemonPurchaseInvoice() {
    return <InvoiceListPage kind="purchase" />;
}

/* ---------- 费用单据（已接后端） ---------- */

/** 费用单据接口前缀。 */
const expenseBase = (accountSetId: string) => `/api/accountsets/${accountSetId}/expensebills`;

/** 结算方式候选项（与柠檬云一致）。 */
const SETTLE_METHODS = ["银行转账", "现金", "支付宝", "微信支付", "其他"];

/** 费用单据表单（金额保留字符串，提交时再转数字）。 */
type ExpenseFormState = {
    billNo: string;
    billDate: string;
    reason: string;
    expenseType: string;
    payee: string;
    settleMethod: string;
    amount: string;
    deductibleTax: string;
};

/** 空表单：编号留空由后端按 FY-0001 规则生成。 */
const emptyExpenseForm = (date: string): ExpenseFormState => ({
    billNo: "",
    billDate: date,
    reason: "",
    expenseType: "办公费",
    payee: "",
    settleMethod: "银行转账",
    amount: "",
    deductibleTax: "",
});

/** 二进制下载（xlsx）：fetch + blob + a[download]，返回错误文案（成功返回 null）。 */
async function expenseDownload(url: string, token: string, fallbackName: string): Promise<string | null> {
    const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
    if (!res.ok) {
        const body = await res.json().catch(() => null);
        const message = body && typeof body === "object" && "message" in body ? String((body as { message?: unknown }).message ?? "") : "";
        return message || `下载失败（HTTP ${res.status}）`;
    }
    const disposition = res.headers.get("Content-Disposition") ?? "";
    const star = /filename\*=UTF-8''([^;\n]+)/i.exec(disposition);
    const plain = /filename="?([^";\n]+)"?/i.exec(disposition);
    const name = star ? decodeURIComponent(star[1]) : plain?.[1] ?? fallbackName;
    const href = URL.createObjectURL(await res.blob());
    const a = document.createElement("a");
    a.href = href;
    a.download = name;
    a.click();
    URL.revokeObjectURL(href);
    return null;
}

/** 上传 .xlsx 到导入端点，返回解析结果或错误文案。 */
async function expenseUpload<T>(url: string, token: string, file: File): Promise<{ data?: T; error?: string }> {
    const form = new FormData();
    form.append("file", file);
    const res = await fetch(url, { method: "POST", headers: { Authorization: `Bearer ${token}` }, body: form });
    const body = await res.json().catch(() => null);
    if (!res.ok) {
        const message = body && typeof body === "object" && "message" in body ? String((body as { message?: unknown }).message ?? "") : "";
        return { error: message || `导入失败（HTTP ${res.status}）` };
    }
    return { data: body as T };
}

/**
 * 费用单据页：登记 / 修改 / 删除 / 批量删除 / 导入导出 / 生成凭证。
 * 凭证口径与后端一致：借 费用科目（不含税）+ 借 进项税额（可抵扣税额）/ 贷 对方科目（含税）。
 */
export function LemonExpenseBill() {
    const defaultPeriod = useDefaultAccountingPeriod();
    const [userPeriod, setUserPeriod] = useState<AccountingPeriod | null>(null);
    const period = userPeriod ?? defaultPeriod;
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";

    const [bills, setBills] = useState<ExpenseBillDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [showAll, setShowAll] = useState(false);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [busy, setBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    const [accounts, setAccounts] = useState<AccountDto[]>([]);
    const [form, setForm] = useState<ExpenseFormState | null>(null);
    const [editId, setEditId] = useState<string | null>(null);
    const [voucherIds, setVoucherIds] = useState<string[]>([]);
    const [showVoucher, setShowVoucher] = useState(false);
    const [voucherForm, setVoucherForm] = useState({ expense: "", counter: "", tax: "", word: "记" });
    const [importResult, setImportResult] = useState<ExpenseImportResultDto | null>(null);
    const [confirmDelete, setConfirmDelete] = useState<{ ids: string[]; label: string } | null>(null);
    const importRef = useRef<HTMLInputElement>(null);

    const stepPeriod = (delta: number) => setUserPeriod(shiftPeriod(period, delta));

    const reload = useCallback(async () => {
        if (!accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
        try {
            const api = new ExpensesApi(apiConfig(token));
            setBills(await api.accountsetsAccountSetIdExpensebillsGet({ accountSetId }));
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载费用单据失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void reload();
    }, [reload]);

    /** 期间内单据（勾选「显示全部」时不按期间过滤），按单据日期倒序。 */
    const visible = useMemo(() => {
        const list = showAll
            ? bills
            : bills.filter((b) => {
                  const date = fmtDate(b.billDate);
                  return date >= period.startDate && date <= period.endDate;
              });
        return [...list].sort((a, b) => {
            const d = fmtDate(b.billDate).localeCompare(fmtDate(a.billDate));
            return d !== 0 ? d : (b.billNo ?? "").localeCompare(a.billNo ?? "");
        });
    }, [bills, showAll, period.startDate, period.endDate]);

    const totalAmount = visible.reduce((s, b) => s + (b.amount ?? 0), 0);
    const totalTax = visible.reduce((s, b) => s + (b.deductibleTax ?? 0), 0);
    const selectableIds = visible.map((b) => b.id ?? "").filter(Boolean);
    const allSelected = selectableIds.length > 0 && selectableIds.every((id) => selected.has(id));

    const toggleSel = (id: string) =>
        setSelected((prev) => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id);
            else next.add(id);
            return next;
        });

    const toggleAll = () => setSelected(allSelected ? new Set() : new Set(selectableIds));

    /** 打开新增 / 编辑弹窗；编辑时回填单据要素。 */
    const openEdit = (bill?: ExpenseBillDto) => {
        setEditId(bill?.id ?? null);
        setForm(
            bill
                ? {
                      billNo: bill.billNo ?? "",
                      billDate: fmtDate(bill.billDate),
                      reason: bill.reason ?? "",
                      expenseType: bill.expenseType ?? "",
                      payee: bill.payee ?? "",
                      settleMethod: bill.settleMethod ?? "",
                      amount: String(bill.amount ?? ""),
                      deductibleTax: String(bill.deductibleTax ?? ""),
                  }
                : emptyExpenseForm(period.startDate),
        );
    };

    const submitForm = async () => {
        if (!token || !accountSetId || !form || busy) {
            return;
        }
        const amount = Number(form.amount || 0);
        const tax = Number(form.deductibleTax || 0);
        if (!form.reason.trim()) {
            setToast("请填写费用事由");
            return;
        }
        if (!form.expenseType.trim()) {
            setToast("请填写费用类型");
            return;
        }
        if (!(amount > 0)) {
            setToast("单据金额需大于 0");
            return;
        }
        if (!(tax >= 0) || tax >= amount) {
            setToast("可抵扣税额需大于等于 0 且小于单据金额");
            return;
        }
        setBusy(true);
        const body = {
            billNo: form.billNo.trim() || undefined,
            billDate: new Date(form.billDate),
            reason: form.reason.trim(),
            expenseType: form.expenseType.trim(),
            payee: form.payee.trim() || undefined,
            settleMethod: form.settleMethod || undefined,
            amount,
            deductibleTax: tax,
        };
        try {
            const api = new ExpensesApi(apiConfig(token));
            if (editId) {
                await api.accountsetsAccountSetIdExpensebillsIdPut({ accountSetId, id: editId, saveExpenseBillRequest: body });
            } else {
                await api.accountsetsAccountSetIdExpensebillsPost({ accountSetId, saveExpenseBillRequest: body });
            }
            setToast(editId ? "已保存修改" : "已新增费用单据");
            setForm(null);
            setEditId(null);
            await reload();
        } catch (err) {
            setToast(await apiErrorMessage(err, "保存失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 删除（单张）与批量删除共用：已入账单据由后端跳过并给出原因。 */
    const runDelete = async () => {
        if (!token || !accountSetId || !confirmDelete || busy) {
            return;
        }
        setBusy(true);
        const ids = confirmDelete.ids;
        try {
            const api = new ExpensesApi(apiConfig(token));
            if (ids.length === 1) {
                await api.accountsetsAccountSetIdExpensebillsIdDelete({ accountSetId, id: ids[0] });
                setToast("已删除 1 张费用单据");
            } else {
                const res = await api.accountsetsAccountSetIdExpensebillsBulkDeletePost({
                    accountSetId,
                    expenseBulkIdsRequest: { ids },
                });
                setToast(
                    `已删除 ${res.affected ?? 0} 张费用单据` +
                        ((res.skipped ?? 0) > 0 ? `，跳过 ${res.skipped} 张（已生成凭证的单据需先删除凭证）` : ""),
                );
            }
            setSelected(new Set());
            setConfirmDelete(null);
            await reload();
        } catch (err) {
            setToast(await apiErrorMessage(err, "删除失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 生成凭证前按需加载末级科目，并预选常见科目（管理费用 / 银行存款 / 进项税额）。 */
    const openVoucherDialog = async () => {
        if (selected.size === 0) {
            setToast("请先勾选要生成凭证的费用单据");
            return;
        }
        setVoucherIds([...selected]);
        if (accounts.length === 0 && token && accountSetId) {
            try {
                const list = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({
                    accountSetId,
                    leafOnly: true,
                });
                setAccounts(list);
                const pick = (pred: (a: AccountDto) => boolean) => list.find(pred)?.id ?? "";
                setVoucherForm((prev) => ({
                    ...prev,
                    expense: prev.expense || pick((a) => (a.code ?? "").startsWith("5602") || (a.code ?? "").startsWith("5601")),
                    counter: prev.counter || pick((a) => (a.code ?? "").startsWith("1002")),
                    tax: prev.tax || pick((a) => (a.code ?? "").startsWith("2221") && (a.name ?? "").includes("进项税额")),
                }));
            } catch {
                // 科目加载失败不阻断弹窗：用户可手动选择。
            }
        }
        setShowVoucher(true);
    };

    const generateVouchers = async () => {
        if (!token || !accountSetId || busy || voucherIds.length === 0) {
            return;
        }
        if (!voucherForm.expense || !voucherForm.counter) {
            setToast("请选择费用科目与对方科目");
            return;
        }
        setBusy(true);
        const word = voucherForm.word.trim() || undefined;
        const taxAccountId = voucherForm.tax || undefined;
        try {
            const api = new ExpensesApi(apiConfig(token));
            if (voucherIds.length === 1) {
                const res = await api.accountsetsAccountSetIdExpensebillsIdVoucherPost({
                    accountSetId,
                    id: voucherIds[0],
                    generateExpenseVoucherRequest: {
                        expenseAccountId: voucherForm.expense,
                        counterAccountId: voucherForm.counter,
                        taxAccountId,
                        word,
                    },
                });
                setToast(`已生成凭证 ${res.voucherText ?? ""}`);
            } else {
                const res = await api.accountsetsAccountSetIdExpensebillsVouchersPost({
                    accountSetId,
                    generateExpenseVouchersRequest: {
                        billIds: voucherIds,
                        expenseAccountId: voucherForm.expense,
                        counterAccountId: voucherForm.counter,
                        taxAccountId,
                        word,
                    },
                });
                const failures = res.failures ?? [];
                setToast(
                    `已生成 ${res.generated ?? 0} 张凭证` +
                        ((res.skipped ?? 0) > 0 ? `，跳过 ${res.skipped} 张` : "") +
                        (failures.length > 0 ? `，失败 ${failures.length} 张：${failures[0].reason ?? ""}` : ""),
                );
            }
            setShowVoucher(false);
            setSelected(new Set());
            await reload();
        } catch (err) {
            setToast(await apiErrorMessage(err, "生成凭证失败"));
        } finally {
            setBusy(false);
        }
    };

    const exportBills = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await expenseDownload(`${expenseBase(accountSetId)}/export`, token, "费用单据.xlsx");
        if (err) {
            setToast(err);
        }
    };

    const downloadTemplate = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await expenseDownload(`${expenseBase(accountSetId)}/import-template`, token, "费用单据导入模板.xlsx");
        if (err) {
            setToast(err);
        }
    };

    const submitImport = async (file: File) => {
        if (!token || !accountSetId) {
            return;
        }
        setBusy(true);
        try {
            const res = await expenseUpload<ExpenseImportResultDto>(`${expenseBase(accountSetId)}/import`, token, file);
            if (res.error) {
                setToast(res.error);
            } else {
                setImportResult(res.data ?? null);
                await reload();
            }
        } finally {
            setBusy(false);
        }
    };

    const cols: Col[] = [
        { key: "ck", title: <CheckBox label="" checked={allSelected} onChange={toggleAll} />, width: "36px" },
        { key: "date", title: "单据日期", width: "110px" },
        { key: "no", title: "单据编号", width: "130px" },
        { key: "type", title: "费用类型", width: "110px" },
        { key: "reason", title: "费用事由" },
        { key: "settle", title: "结算方式", width: "110px" },
        { key: "payee", title: "收款人", width: "100px" },
        { key: "deductTax", title: "可抵扣税额", align: "right", width: "120px" },
        { key: "amount", title: "单据金额（含税）", align: "right", width: "120px" },
        { key: "voucher", title: "关联凭证", width: "110px" },
        { key: "status", title: "状态", width: "90px" },
        { key: "ops", title: "操作", width: "120px" },
    ];

    return (
        <Page title="费用单据" subtitle="费用单据登记、导入导出与生成凭证" icon={ProfileOutlined}>
            <ToolRow
                left={
                    <>
                        <PeriodStepper
                            text={`${period.year}年${period.month}月`}
                            onPrev={() => stepPeriod(-1)}
                            onNext={() => stepPeriod(1)}
                        />
                        <CheckBox label="显示全部" checked={showAll} onChange={() => setShowAll((v) => !v)} />
                    </>
                }
                right={
                    <>
                        <Btn dropdown onClick={() => openEdit()}>新增</Btn>
                        <Btn dropdown title="从 .xlsx 导入费用单据（按单据编号更新已存在单据）" onClick={() => importRef.current?.click()}>
                            导入
                        </Btn>
                        <Btn dropdown disabled={busy} title="导出全部费用单据为 .xlsx" onClick={() => void exportBills()}>
                            导出
                        </Btn>
                        <Dropdown
                            trigger={["click"]}
                            menu={{
                                items: [{ key: "delete", label: "批量删除" }],
                                onClick: () =>
                                    selected.size === 0 ? setToast("请先勾选要删除的费用单据") : setConfirmDelete({ ids: [...selected], label: `${selected.size} 张单据` }),
                            }}
                        >
                            <Btn dropdown disabled={busy} title="对勾选单据执行批量操作">批量操作</Btn>
                        </Dropdown>
                        <Btn dropdown disabled={busy} title="对勾选的未入账单据生成凭证" onClick={() => void openVoucherDialog()}>
                            生成凭证
                        </Btn>
                        <RefreshIcon onClick={() => void reload()} />
                    </>
                }
            />
            <input
                ref={importRef}
                type="file"
                accept=".xlsx"
                className="hidden"
                onChange={(e) => {
                    const file = e.target.files?.[0];
                    e.target.value = "";
                    if (file) {
                        void submitImport(file);
                    }
                }}
            />
            <div className="flex-1">
                {error2(error)}
                {loading && <div className="px-3 py-2 text-[13px] text-slate-500">加载中…</div>}
                <Table
                    cols={cols}
                    rows={[
                        // 空数据时不渲染合计行，空态交给 Table 的 empty
                        ...(visible.length
                            ? [
                                  {
                                      ck: "",
                                      date: "",
                                      no: "",
                                      type: "",
                                      reason: <span className="font-bold text-slate-800">合计（{visible.length} 张）</span>,
                                      settle: "",
                                      payee: "",
                                      deductTax: totalTax.toFixed(2),
                                      amount: totalAmount.toFixed(2),
                                      voucher: "",
                                      status: "",
                                      ops: "",
                                      __style: { background: "#fafbfd", fontWeight: 500 },
                                  } as Row,
                              ]
                            : []),
                        ...visible.map(
                            (b) =>
                                ({
                                    ck: <CheckBox label="" checked={selected.has(b.id ?? "")} onChange={() => toggleSel(b.id ?? "")} />,
                                    date: fmtDate(b.billDate),
                                    no: b.billNo,
                                    type: b.expenseType,
                                    reason: b.reason,
                                    settle: b.settleMethod ?? "",
                                    payee: b.payee ?? "",
                                    deductTax: (b.deductibleTax ?? 0).toFixed(2),
                                    amount: (b.amount ?? 0).toFixed(2),
                                    voucher: b.voucherText ?? "",
                                    status: <StatusPill tone={b.voucherText ? "green" : "gray"}>{b.statusText}</StatusPill>,
                                    ops: (
                                        <span className="flex items-center gap-3">
                                            <LinkAction disabled={Boolean(b.voucherText)} onClick={() => openEdit(b)}>
                                                编辑
                                            </LinkAction>
                                            <LinkAction danger disabled={Boolean(b.voucherText)} onClick={() => setConfirmDelete({ ids: [b.id ?? ""], label: b.billNo ?? "" })}>
                                                删除
                                            </LinkAction>
                                        </span>
                                    ),
                                    __style: selected.has(b.id ?? "") ? { background: "#eef4ff" } : undefined,
                                }) as Row
                        ),
                    ]}
                    empty={visible.length === 0 && !loading ? <EmptyTip text="本期暂无费用单据" /> : undefined}
                />
            </div>
            <Pager total={visible.length} />
            {/* 新增 / 编辑单据 */}
            {form && (
                <Dialog
                    title={editId ? `编辑费用单据 ${form.billNo}` : "新增费用单据"}
                    width={520}
                    onClose={() => {
                        setForm(null);
                        setEditId(null);
                    }}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" disabled={busy} onClick={() => void submitForm()}>
                                保存
                            </Btn>
                            <Btn
                                onClick={() => {
                                    setForm(null);
                                    setEditId(null);
                                }}
                            >
                                取消
                            </Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        <HintBox>
                            单据编号留空时自动生成（FY-0001）。单据金额为含税总额，可抵扣税额需小于单据金额。
                        </HintBox>
                        {([
                            ["单据编号：", <TextInput key="no" className="w-[240px]" value={form.billNo} placeholder="留空自动生成" onChange={(v) => setForm({ ...form, billNo: v })} />],
                            ["单据日期：", <DateInput key="d" className="w-[240px]" value={form.billDate} onChange={(v) => setForm({ ...form, billDate: v })} />],
                            ["费用事由：", <TextInput key="r" className="w-[240px]" value={form.reason} onChange={(v) => setForm({ ...form, reason: v })} />],
                            ["费用类型：", <Select key="t" className="w-[240px]" value={form.expenseType} onChange={(v) => setForm({ ...form, expenseType: v })} options={["办公费", "差旅费", "业务招待费", "水电费", "房租", "通讯费", "运输费", "其他"]} />],
                            ["收款人：", <TextInput key="p" className="w-[240px]" value={form.payee} onChange={(v) => setForm({ ...form, payee: v })} />],
                            ["结算方式：", <Select key="s" className="w-[240px]" value={form.settleMethod} onChange={(v) => setForm({ ...form, settleMethod: v })} options={SETTLE_METHODS} />],
                            ["单据金额：", <TextInput key="a" className="w-[240px] text-right tabular-nums" value={form.amount} placeholder="含税总额" onChange={(v) => setForm({ ...form, amount: v.replace(/[^\d.]/g, "") })} />],
                            ["可抵扣税额：", <TextInput key="x" className="w-[240px] text-right tabular-nums" value={form.deductibleTax} placeholder="无进项税额填 0" onChange={(v) => setForm({ ...form, deductibleTax: v.replace(/[^\d.]/g, "") })} />],
                        ] as Array<[string, ReactNode]>).map(([label, control]) => (
                            <label key={label} className="flex items-center gap-3">
                                <span className="w-[110px] text-right">{label}</span>
                                {control}
                            </label>
                        ))}
                        <div className="pl-[122px] text-[12px] text-slate-400">
                            不含税金额 {(Number(form.amount || 0) - Number(form.deductibleTax || 0)).toFixed(2)} 元
                        </div>
                    </div>
                </Dialog>
            )}
            {/* 生成凭证 */}
            {showVoucher && (
                <Dialog
                    title="生成凭证"
                    width={520}
                    onClose={() => setShowVoucher(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" disabled={busy} onClick={() => void generateVouchers()}>
                                确定
                            </Btn>
                            <Btn onClick={() => setShowVoucher(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        <HintBox>
                            已勾选 {voucherIds.length} 张单据，将逐张生成「借 费用科目（不含税）+ 借 进项税额（可抵扣税额）/ 贷 对方科目（含税）」。
                            税额科目留空时自动取 2221 下的「进项税额」。
                        </HintBox>
                        {([
                            [
                                "费用科目：",
                                <Select
                                    key="e"
                                    className="w-[240px]"
                                    value={voucherForm.expense}
                                    onChange={(v) => setVoucherForm({ ...voucherForm, expense: v })}
                                    options={accounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` }))}
                                />,
                            ],
                            [
                                "对方科目：",
                                <Select
                                    key="c"
                                    className="w-[240px]"
                                    value={voucherForm.counter}
                                    onChange={(v) => setVoucherForm({ ...voucherForm, counter: v })}
                                    options={accounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` }))}
                                />,
                            ],
                            [
                                "税额科目：",
                                <Select
                                    key="t"
                                    className="w-[240px]"
                                    value={voucherForm.tax}
                                    onChange={(v) => setVoucherForm({ ...voucherForm, tax: v })}
                                    options={[
                                        { value: "", label: "自动：应交税费-进项税额" },
                                        ...accounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
                                    ]}
                                />,
                            ],
                            [
                                "凭证字：",
                                <TextInput key="w" className="w-[240px]" value={voucherForm.word} onChange={(v) => setVoucherForm({ ...voucherForm, word: v })} />,
                            ],
                        ] as Array<[string, ReactNode]>).map(([label, control]) => (
                            <label key={label} className="flex items-center gap-3">
                                <span className="w-[110px] text-right">{label}</span>
                                {control}
                            </label>
                        ))}
                    </div>
                </Dialog>
            )}
            {/* 删除确认 */}
            {confirmDelete && (
                <Dialog
                    title="确认删除"
                    onClose={() => setConfirmDelete(null)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" disabled={busy} onClick={() => void runDelete()}>
                                确定删除
                            </Btn>
                            <Btn onClick={() => setConfirmDelete(null)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="px-1 py-2 text-[13px] text-slate-700">
                        将删除{confirmDelete.label || "所选费用单据"}，删除后不可恢复。已生成凭证的单据会被跳过。
                    </div>
                </Dialog>
            )}
            {/* 导入结果 */}
            {importResult && (
                <Dialog
                    title="导入结果"
                    width={520}
                    onClose={() => setImportResult(null)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => setImportResult(null)}>
                                确定
                            </Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-2 px-1 py-1 text-[13px] text-slate-700">
                        <div>
                            新增 <b>{importResult.imported ?? 0}</b> 张，更新 <b>{importResult.updated ?? 0}</b> 张。
                        </div>
                        {(importResult.failures?.length ?? 0) > 0 && (
                            <div className="flex flex-col gap-1">
                                <span className="text-slate-500">失败明细：</span>
                                <ul className="ml-4 max-h-[220px] list-disc overflow-auto text-[12.5px] text-slate-600">
                                    {(importResult.failures ?? []).map((f, i) => (
                                        <li key={i}>
                                            第 {f.row} 行：{f.reason}
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        )}
                        <LinkAction onClick={() => void downloadTemplate()}>下载导入模板</LinkAction>
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 发票资金一览表（已接后端） ---------- */

export function LemonInvoiceSummary() {
    const defaultPeriod = useDefaultAccountingPeriod();
    const [userPeriod, setUserPeriod] = useState<AccountingPeriod | null>(null);
    const period = userPeriod ?? defaultPeriod;
    const { rows, loading, error, reload } = useFundOverview(period.startDate, period.endDate);
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";

    /** 导出资金一览表（按往来单位的票款差异）。 */
    const exportOverview = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const res = await fetch(
            `/api/accountsets/${accountSetId}/invoices/fund-overview/export` +
                `?startDate=${period.startDate}&endDate=${period.endDate}&period=${period.year}-${String(period.month).padStart(2, "0")}`,
            { headers: { Authorization: `Bearer ${token}` } },
        );
        if (!res.ok) {
            return;
        }
        const disposition = res.headers.get("Content-Disposition") ?? "";
        const star = /filename\*=UTF-8''([^;\n]+)/i.exec(disposition);
        const plain = /filename="?([^";\n]+)"?/i.exec(disposition);
        const name = star ? decodeURIComponent(star[1]) : plain?.[1] ?? "发票资金一览表.xlsx";
        const url = URL.createObjectURL(await res.blob());
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = name;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
    };
    const cols: Col[] = [
        { key: "partner", title: "往来单位", width: "200px" },
        { key: "sales", title: "销项发票金额（含税）", align: "right", group: "发票" },
        { key: "received", title: "收款金额", align: "right", group: "资金" },
        { key: "notInvoiced", title: "未开发票金额（含税）", align: "right", group: "差异" },
        { key: "notReceived", title: "未收款金额", align: "right", group: "差异" },
        { key: "ops", title: "操作", width: "110px" },
    ];
    const tableRows: Row[] = rows.map((r) => ({
        partner: <span className="text-[#2f6bff]">{r.partnerName}</span>,
        sales: (r.salesWithTax ?? 0).toFixed(2),
        received: (r.received ?? 0).toFixed(2),
        notInvoiced: (r.notInvoiced ?? 0).toFixed(2),
        notReceived: (r.notReceived ?? 0).toFixed(2),
        // 逐户核对后端未提供端点
        ops: <WipTag text="核对建设中" />,
    }));
    return (
        <Page title="发票资金一览表" subtitle="按往来单位比对发票与收付款差异" icon={WalletOutlined}>
            <ToolRow
                left={
                    <PeriodStepper
                        text={`${period.year}年${period.month}月`}
                        onPrev={() => setUserPeriod(shiftPeriod(period, -1))}
                        onNext={() => setUserPeriod(shiftPeriod(period, 1))}
                    />
                }
                right={
                    <>
                        <Btn title="导出当前期间的发票资金一览表" onClick={() => void exportOverview()}>导出</Btn>
                        <RefreshIcon onClick={() => void reload()} />
                    </>
                }
            />
            <div className="flex-1">
                {error2(error)}
                {loading && <div className="px-3 py-2 text-[13px] text-slate-500">加载中…</div>}
                <Table cols={cols} rows={tableRows} empty={rows.length === 0 && !loading && !error ? <EmptyTip /> : undefined} />
            </div>
            {/* 真实数据不足一页时隐藏分页控件，不显示假页码 */}
            <Pager total={rows.length} />
        </Page>
    );
}

/* ---------- 发票风险分析（引导页，建设中） ---------- */

const RISK_POINTS = [
    {
        color: "#10b981",
        text: "发票风险分析数据来源于企业通过一键取票从税局获取的发票数据，通过获取的发票数据，依据相关政策规定为您公司检测可能存在的发票风险及销售采购资金分析等",
    },
    {
        color: "#f59e0b",
        text: "进销项对比、虚开发票、采购虚假发票、销售采购资金分析、销售收入分析等多维度分析发票潜在风险",
    },
    {
        color: "#ef4444",
        text: "通过发票风险分析，可了解企业当前开票是否合规，客户及供应商情况，商品明细信息，规避企业潜在的发票风险",
    },
];

export function LemonInvoiceRisk() {
    return (
        <Page title="发票风险分析" subtitle="多维度检测发票潜在风险与销售采购资金情况" icon={AlertOutlined}>
            <WipNotice text="发票风险分析依赖一键取票取得的税局发票数据，后端尚未提供分析端点，本页为功能介绍（建设中）。" />
            <div className="flex flex-1 items-center justify-center py-10">
                <div className="flex w-[1100px] max-w-full items-center gap-10 rounded-2xl border border-slate-100 bg-white px-10 py-12 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
                    <div className="flex h-[240px] w-[320px] shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-[#fafbfd] text-[13px] text-slate-400">
                        风险分析插画
                    </div>
                    <div className="flex flex-1 flex-col items-center">
                        <div className="text-[22px] font-bold text-slate-800">发票风险分析</div>
                        <div className="mt-6 flex flex-col gap-4">
                            {RISK_POINTS.map((p, i) => (
                                <div key={i} className="flex items-start gap-2 text-[13px] text-slate-500">
                                    <span
                                        className="mt-1.5 h-2 w-2 shrink-0 rounded-full"
                                        style={{ background: p.color }}
                                    />
                                    {p.text}
                                </div>
                            ))}
                        </div>
                        <div className="mt-8">
                            <WipBtn title="发票风险分析建设中，后端未提供端点">立即分析</WipBtn>
                        </div>
                    </div>
                </div>
            </div>
        </Page>
    );
}

/** 商品明细页签：把发票列表接口返回的行明细按行摊开，均为真实数据（无独立后端端点）。 */
function InvoiceGoodsTable({
    kind,
    period,
    invoices,
    loading,
    error,
    onTab,
    onStep,
}: {
    kind: "sales" | "purchase";
    period: AccountingPeriod;
    invoices: InvoiceDto[];
    loading: boolean;
    error: string | null;
    onTab: (i: number) => void;
    onStep: (d: number) => void;
}) {
    const cols: Col[] = [
        { key: "no", title: "发票号码", width: "150px" },
        { key: "date", title: "开票日期", width: "100px" },
        { key: "type", title: "发票种类", width: "100px" },
        ...(kind === "purchase" ? ([{ key: "period", title: "税款所属期", width: "100px" }] as Col[]) : []),
        { key: "partner", title: kind === "sales" ? "客户名称" : "供应商名称", width: "160px" },
        { key: "goods", title: "商品名称", width: "150px" },
        { key: "amount", title: "金额", align: "right", width: "100px" },
        { key: "rate", title: "税率", width: "70px" },
        { key: "tax", title: "税额", align: "right", width: "90px" },
        { key: "total", title: "价税合计", align: "right", width: "100px" },
    ];
    // 行明细摊平：一张发票多行商品时逐行展示
    const flat: Row[] = [];
    invoices.forEach((inv) => {
        (inv.lines ?? []).forEach((ln, i) => {
            const amount = ln.amount ?? 0;
            const tax = ln.taxAmount ?? 0;
            flat.push({
                no: `${inv.invoiceNo ?? ""}${(inv.lines ?? []).length > 1 ? `-${i + 1}` : ""}`,
                date: fmtDate(inv.issueDate),
                type: inv.invoiceType,
                period: fmtTaxPeriod(inv.taxPeriod),
                partner: inv.partnerName,
                goods: ln.goodsName,
                amount: amount.toFixed(2),
                rate: fmtRate(ln.taxRate),
                tax: tax.toFixed(2),
                total: (ln.totalWithTax ?? amount + tax).toFixed(2),
            });
        });
    });
    const sum = (key: string) => flat.reduce((a, r) => a + Number(r[key] ?? 0), 0);
    const rows: Row[] = flat.length
        ? [
              {
                  no: "合计",
                  date: "",
                  type: "",
                  period: "",
                  partner: "",
                  goods: "",
                  amount: sum("amount").toFixed(2),
                  rate: "",
                  tax: sum("tax").toFixed(2),
                  total: sum("total").toFixed(2),
                  __style: { background: "#fafbfd", fontWeight: 700, color: "#1e293b" },
              },
              ...flat,
          ]
        : [];
    return (
        <Page title={kind === "sales" ? "销项发票" : "进项发票"} subtitle="发票商品明细逐行摊示" icon={kind === "sales" ? FileTextOutlined : FileDoneOutlined}>
            <SubTabs tabs={kind === "sales" ? ["销项发票", "商品明细"] : ["进项发票", "商品明细", "暂不入账发票"]} active={1} onChange={onTab} />
            <ToolRow
                left={
                    <PeriodStepper
                        text={`${period.year}年${period.month}月`}
                        onPrev={() => onStep(-1)}
                        onNext={() => onStep(1)}
                    />
                }
                right={
                    <>
                        <WipBtn dropdown title="批量操作建设中，后端未提供端点">批量操作</WipBtn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1">
                {error2(error)}
                {loading && <div className="px-3 py-2 text-[13px] text-slate-500">加载中…</div>}
                <Table cols={cols} rows={rows} empty={<EmptyTip />} />
            </div>
        </Page>
    );
}

/** 暂不入账发票页签：真实数据（发票列表接口 status=3 暂不入账），「入账」走状态变更端点恢复发票进入账流程。 */
function InvoicePendingTable({ period, onTab, onStep }: { period: AccountingPeriod; onTab: (i: number) => void; onStep: (d: number) => void }) {
    const data = useInvoiceData(2, period.startDate, period.endDate, 3);
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    /** 入账：把暂不入账发票状态恢复为「未入账」，使其重新回到入账流程（凭证在销项 / 进项发票页勾选后生成）。 */
    const postNow = async (ids: string[]) => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (ids.length === 0) {
            setToast("请先勾选要入账的发票");
            return;
        }
        setBusy(true);
        try {
            const res = await new InvoicesApi(apiConfig(token)).accountsetsAccountSetIdInvoicesBulkStatusPost({
                accountSetId,
                bulkInvoiceStatusRequest: { ids, status: 1 },
            });
            setToast(`已恢复 ${res.affected ?? 0} 张发票至入账流程`);
        } catch (e) {
            setToast(await apiErrorMessage(e, "入账失败"));
        } finally {
            setSelected(new Set());
            setBusy(false);
            await data.reload();
        }
    };

    /** 导出当前期间的暂不入账发票。 */
    const exportPending = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const res = await fetch(
            `/api/accountsets/${accountSetId}/invoices/export?direction=2&status=3` +
                `&startDate=${period.startDate}&endDate=${period.endDate}&period=${period.year}-${String(period.month).padStart(2, "0")}`,
            { headers: { Authorization: `Bearer ${token}` } },
        );
        if (!res.ok) {
            setToast("导出失败，请重试。");
            return;
        }
        const disposition = res.headers.get("Content-Disposition") ?? "";
        const star = /filename\*=UTF-8''([^;\n]+)/i.exec(disposition);
        const plain = /filename="?([^";\n]+)"?/i.exec(disposition);
        const url = URL.createObjectURL(await res.blob());
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = star ? decodeURIComponent(star[1]) : plain?.[1] ?? "暂不入账发票.xlsx";
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
    };

    /** 批量删除暂不入账发票。 */
    const deletePending = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (selected.size === 0) {
            setToast("请先勾选要删除的发票");
            return;
        }
        if (!window.confirm(`确定删除已勾选的 ${selected.size} 张暂不入账发票吗？`)) {
            return;
        }
        setBusy(true);
        try {
            const res = await new InvoicesApi(apiConfig(token)).accountsetsAccountSetIdInvoicesBulkDeletePost({
                accountSetId,
                bulkInvoiceIdsRequest: { ids: [...selected] },
            });
            setToast(`已删除 ${res.affected ?? 0} 张发票`);
        } catch (e) {
            setToast(await apiErrorMessage(e, "删除失败"));
        } finally {
            setSelected(new Set());
            setBusy(false);
            await data.reload();
        }
    };

    const toggleSel = (id: string) =>
        setSelected((prev) => {
            const next = new Set(prev);
            if (next.has(id)) {
                next.delete(id);
            } else {
                next.add(id);
            }
            return next;
        });

    const cols: Col[] = [
        { key: "ck", title: <CheckBox label="" />, width: "36px" },
        { key: "type", title: "发票种类", width: "100px" },
        { key: "inputDate", title: "录入日期", width: "100px" },
        { key: "issueDate", title: "开票日期", width: "100px" },
        { key: "no", title: "发票号码", width: "150px" },
        { key: "status", title: "发票状态", width: "90px" },
        { key: "partner", title: "供应商名称", width: "160px" },
        { key: "goods", title: "商品名称", width: "150px" },
        { key: "amount", title: "合计金额", align: "right", width: "110px" },
        { key: "rate", title: "税率", width: "70px" },
        { key: "tax", title: "合计税额", align: "right", width: "100px" },
        { key: "total", title: "价税合计", align: "right", width: "110px" },
        { key: "source", title: "发票来源", width: "100px" },
        { key: "ops", title: "操作", width: "80px" },
    ];
    const rows: Row[] = [
        ...(data.invoices.length
            ? [
                  {
                      ck: "",
                      type: "",
                      inputDate: "",
                      issueDate: "",
                      no: "",
                      status: "",
                      partner: <span className="font-bold">合计</span>,
                      goods: "",
                      amount: (data.summary?.totalAmount ?? 0).toFixed(2),
                      rate: "",
                      tax: (data.summary?.totalTax ?? 0).toFixed(2),
                      total: (data.summary?.totalWithTax ?? 0).toFixed(2),
                      source: "",
                      ops: "",
                      __style: { background: "#fafbfd" },
                  } as Row,
              ]
            : []),
        ...data.invoices.map(
            (inv) =>
                ({
                    ck: <CheckBox label="" checked={selected.has(inv.id ?? "")} onChange={() => toggleSel(inv.id ?? "")} />,
                    type: inv.invoiceType,
                    inputDate: fmtDate(inv.inputDate),
                    issueDate: fmtDate(inv.issueDate),
                    no: inv.invoiceNo,
                    status: <StatusPill tone={invoiceStatusTone(inv.statusText)}>{inv.statusText}</StatusPill>,
                    partner: inv.partnerName,
                    goods: inv.lines?.[0]?.goodsName ?? "",
                    amount: (inv.totalAmount ?? 0).toFixed(2),
                    rate: fmtRate(inv.lines?.[0]?.taxRate),
                    tax: (inv.totalTax ?? 0).toFixed(2),
                    total: (inv.totalWithTax ?? 0).toFixed(2),
                    source: inv.source,
                    ops: <LinkAction onClick={() => void postNow([inv.id ?? ""])}>入账</LinkAction>,
                }) as Row,
        ),
    ];
    return (
        <Page title="进项发票" subtitle="暂不入账发票可恢复入账流程" icon={FileDoneOutlined}>
            <SubTabs tabs={["进项发票", "商品明细", "暂不入账发票"]} active={2} onChange={onTab} />
            <WipNotice text="「入账」把发票恢复为「未入账」状态，恢复后需回到销项 / 进项发票页勾选并生成凭证。" />
            <ToolRow
                left={
                    <PeriodStepper
                        text={`${period.year}年${period.month}月`}
                        onPrev={() => onStep(-1)}
                        onNext={() => onStep(1)}
                    />
                }
                right={
                    <>
                        <CheckBox label="显示所有信息" />
                        <Btn title="导出当前期间的暂不入账发票" onClick={() => void exportPending()}>导出</Btn>
                        <Dropdown
                            trigger={["click"]}
                            menu={{
                                items: [{ key: "delete", label: "批量删除" }],
                                onClick: () => void deletePending(),
                            }}
                        >
                            <Btn dropdown disabled={busy} title="批量删除勾选的暂不入账发票">批量操作</Btn>
                        </Dropdown>
                        <Btn variant="primary" disabled={busy} title="把勾选的暂不入账发票恢复为未入账" onClick={() => void postNow([...selected])}>入账</Btn>
                        <RefreshIcon onClick={() => void data.reload()} />
                    </>
                }
            />
            <div className="flex-1">
                {error2(data.error)}
                {data.loading && <div className="px-3 py-2 text-[13px] text-slate-500">加载中…</div>}
                <Table cols={cols} rows={rows} empty={<EmptyTip />} />
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/** 收票宝落地页：与柠檬云一致，只放介绍与授权入口；授权依赖微信小程序服务。 */
export function LemonSpb() {
    const [authOpen, setAuthOpen] = useState(false);
    return (
        <Page title="收票宝" subtitle="员工收票交票，财务审批归档，企业电子档案管理好帮手" icon={ProfileOutlined}>
            <div className="flex flex-1 flex-col items-center pt-10">
                <div className="text-[16px] text-slate-500">帮您智能收取票据，一键提交财务，解放双手之神器</div>
                <div className="mt-8">
                    <Button type="primary" size="large" onClick={() => setAuthOpen(true)}>
                        立即授权
                    </Button>
                </div>
                <div className="mt-10 flex w-[900px] max-w-full items-center gap-8 rounded-2xl bg-[#fafbfd] p-8">
                    <div className="flex-1">
                        <div className="text-[15px] font-bold text-slate-800">收票宝亮点：</div>
                        <div className="mt-4 flex flex-col gap-3 text-[13px] text-slate-700">
                            {["多方案采集，自动核验", "一键交票，告别粘贴", "关联凭证，高效利用", "智能归档，无忧存储"].map((t) => (
                                <span key={t} className="flex items-center gap-2">
                                    <span className="flex h-3.5 w-3.5 items-center justify-center rounded-md bg-[#10b981] text-[10px] text-white">✓</span>
                                    {t}
                                </span>
                            ))}
                        </div>
                    </div>
                    <div className="flex gap-4">
                        <PhoneMock title="收票宝" rows={["差旅费 · 电子发票", "餐饮 · 增值税专票", "打车 · 电子普通发票"]} />
                        <PhoneMock title="待审批" rows={["张伟提交的报销单", "李娜提交的发票", "王强提交的差旅单"]} />
                    </div>
                </div>
            </div>
            <Modal
                open={authOpen}
                title="收票宝授权"
                okText="知道了"
                cancelButtonProps={{ style: { display: "none" } }}
                onOk={() => setAuthOpen(false)}
                onCancel={() => setAuthOpen(false)}
            >
                <div className="text-[13px] leading-6 text-slate-600">
                    收票宝通过微信小程序收票，需要先在小程序内完成企业绑定后才能授权到本账套。
                    <br />
                    当前环境未接入微信小程序服务，暂时无法生成授权小程序码。
                </div>
            </Modal>
        </Page>
    );
}

/** 手机样机：展示收票宝小程序的样子（纯静态示意）。 */
function PhoneMock({ title, rows }: { title: string; rows: string[] }) {
    return (
        <div className="h-[200px] w-[104px] overflow-hidden rounded-[14px] border-4 border-slate-800 bg-white">
            <div className="bg-[#10b981] px-2 py-1 text-center text-[10px] text-white">{title}</div>
            <div className="flex flex-col gap-1.5 p-1.5">
                {rows.map((r) => (
                    <div key={r} className="rounded bg-slate-100 p-1 text-[8px] leading-3 text-slate-500">
                        {r}
                    </div>
                ))}
            </div>
        </div>
    );
}

/** 银企互联落地页（建设中：银行页签可切换，表单为示意，不接后端）。 */
const BANK_TABS = ["工商银行", "中国银行", "招商银行", "邮储银行", "交通银行", "中信银行", "浦发银行", "微众银行", "平安银行", "光大银行", "民生银行", "广发银行"];

export function LemonBankLink() {
    const [bank, setBank] = useState(1);
    return (
        <Page title="银企互联" subtitle="实时同步对账单，自动生成凭证，银行级加密，安全可靠" icon={BankOutlined}>
            <WipNotice text="银企互联需对接银行直连服务，后端尚未提供端点，本页为签约流程示意（建设中），表单内容不会提交。" />
            <SubTabs tabs={BANK_TABS} active={bank} onChange={setBank} />
            <div className="mx-auto my-6 w-[1220px] max-w-full rounded-2xl border border-slate-200 bg-white p-8">
                <div className="flex justify-end gap-1 text-[13px]">
                    <span className="flex h-5 w-5 items-center justify-center rounded-full bg-[#2f6bff] text-[11px] text-white">?</span>
                    <span className="cursor-pointer text-[#2f6bff]">点此查看操作手册</span>
                </div>
                <div className="mt-4 flex gap-10">
                    <div className="flex flex-1 flex-col gap-5">
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-[13px] text-slate-700"><span className="text-[#ef4444]">*</span> 公司名称：</span>
                            <FakeInput className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-[13px] text-slate-700"><span className="text-[#ef4444]">*</span> 统一社会信用代码：</span>
                            <FakeInput className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-[13px] text-slate-700"><span className="text-[#ef4444]">*</span> 银行账户：</span>
                            <FakeSelect text="" className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-[13px] text-slate-700"><span className="text-[#ef4444]">*</span> 银行账号：</span>
                            <FakeInput className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-[13px] text-slate-700">企业客户号：</span>
                            <span className="inline-flex h-8 w-[320px] items-center rounded-lg border border-slate-200 px-2.5 text-[13px] text-slate-400">
                                若集团账号签约，请输入企业客户号
                                <QuestionCircleOutlined className="ml-auto text-[14px]" style={ICON_STYLE.muted} />
                            </span>
                        </div>
                    </div>
                    <div className="w-[370px] shrink-0 self-start rounded-xl bg-[#fafbfd] p-5 text-[13px] leading-6">
                        <div className="font-medium text-[#ef4444]">提示：</div>
                        <div className="text-slate-600">1、请确认公司名称和统一社会信用代码</div>
                        <div className="text-slate-600">2、选择对应的银行账户，并确认银行账号和名称正确</div>
                        <div className="text-slate-600">3、在电脑上插入“{BANK_TABS[bank]}飞盾”</div>
                        <div className="text-slate-600">4、点击立即授权</div>
                    </div>
                </div>
                <div className="mt-8 flex justify-center">
                    <WipBtn title="银企互联建设中，后端未提供授权端点">立即授权</WipBtn>
                </div>
                <div className="mt-5 text-center text-[13px] text-slate-500">
                    如有疑问，请 <span className="cursor-pointer text-[#2f6bff]">咨询客服</span> ，咨询时间：周一至周日 8:30-12:00,13:30-20:30
                </div>
            </div>
            <div className="mx-auto mb-8 mt-8 flex h-[85px] w-[990px] max-w-full items-center justify-between rounded-2xl bg-[#eef4ff] px-10">
                <span className="text-[20px] font-bold">
                    <span className="text-slate-800">云财与各大银行 </span>
                    <span className="text-[#2f6bff]">携手研发</span>
                </span>
                <span className="text-[13px] text-slate-500">实时同步对账单，自动生成凭证，银行级加密，安全可靠</span>
                <span className="text-[26px]">🏦</span>
            </div>
        </Page>
    );
}

/** 一键登录税局落地页（建设中：引导弹窗为示意，不提交任何数据）。 */
export function LemonTaxLogin() {
    /** 弹层默认不渲染，点「税局登录」打开、可关闭（修复无条件渲染导致遮罩常驻挡页）。 */
    const [dialogOpen, setDialogOpen] = useState(false);
    return (
        <Page
            title="一键登录税局"
            subtitle="只需一次维护，即可直接登录电子税务局，进行取票、开票、报税、社保业务、征纳互动等操作"
            icon={KeyOutlined}
        >
            <WipNotice text="一键登录税局需对接电子税务局免密登录服务，后端尚未提供端点，本页与弹窗均为流程示意（建设中）。" />
            <div className="px-4 pb-8">
                <Btn variant="primary" onClick={() => setDialogOpen(true)}>
                    税局登录
                </Btn>
            </div>
            {dialogOpen && (
                <Dialog
                    title="一键登录电子税局"
                    width={900}
                    onClose={() => setDialogOpen(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn onClick={() => setDialogOpen(false)}>取消</Btn>
                            <WipBtn title="一键登录税局建设中，后端未提供端点">下一步</WipBtn>
                        </div>
                    }
                >
                    <div className="rounded-xl bg-[#fafbfd] p-4 text-[13px] leading-6">
                        <div className="flex items-start gap-2">
                            <span className="text-[#b45309]">⚠</span>
                            <div className="text-slate-600">
                                <div>1.一键自动跳转登录电子税局，方便且高效</div>
                                <div>
                                    2.首次使用或<span className="text-[#ef4444]">税务信息填写不准确</span>，均需要认证税局登录账号后，才能使用一键登录电子税局功能；税务信息填写完成后，可前往
                                    <span className="cursor-pointer text-[#2f6bff]">【设置-账套-税务信息】</span>中再次查看和修改
                                </div>
                                <div>
                                    3.如您在使用过程中有任何疑问，请查看 <span className="cursor-pointer text-[#2f6bff]">帮助文档</span> 或联系
                                    <span className="ml-1 inline-flex h-6 items-center rounded-md bg-[#2f6bff] px-2 text-[12px] text-white">在线客服</span>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div className="mt-5 flex items-center gap-3 text-[13px]">
                        <span className="flex items-center gap-1.5">
                            <span className="flex h-6 w-6 items-center justify-center rounded-full bg-[#2f6bff] text-[12px] text-white">1</span>
                            <span className="text-[#2f6bff]">确认公司信息</span>
                        </span>
                        <span className="h-px w-16 bg-slate-200" />
                        <span className="flex items-center gap-1.5">
                            <span className="flex h-6 w-6 items-center justify-center rounded-full border border-slate-300 text-[12px] text-slate-400">2</span>
                            <span className="text-slate-400">填写税局登录信息</span>
                        </span>
                    </div>
                    <div className="mt-5 flex flex-col gap-4">
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-[13px] text-slate-700"><span className="text-[#ef4444]">*</span> 公司名称：</span>
                            <FakeInput className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-[13px] text-slate-700"><span className="text-[#ef4444]">*</span> 统一社会信用代码：</span>
                            <FakeInput className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-[13px] text-slate-700"><span className="text-[#ef4444]">*</span> 报税地区：</span>
                            <FakeSelect text="" className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px]" />
                            <CheckBox label={<>我已阅读并同意 <span className="text-[#2f6bff]">《税局登录业务隐私政策》</span></>} />
                        </div>
                    </div>
                </Dialog>
            )}
        </Page>
    );
}
