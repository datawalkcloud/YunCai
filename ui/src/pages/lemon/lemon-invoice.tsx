/**
 * 复刻区发票模块（对应柠檬云 /Invoice/*）：
 * 一键取票（引导页，建设中）、销项发票、进项发票（期间可切换、批量认证已接后端单条认证端点）、
 * 费用单据（建设中）、发票资金一览表（已接后端）、发票风险分析（建设中）。
 * 商品明细页签把发票列表接口返回的行明细直接摊平展示（真实数据）；
 * 暂不入账页签走发票列表接口 status=3（暂不入账），「入账」经状态变更端点恢复发票进入账流程。
 * 视觉遵循全站「云财蓝」设计语言：主色 #2f6bff、圆角 rounded-lg/2xl、白卡 + slate 灰阶文字，
 * 页头统一由 Page 基元的渐变横幅渲染。
 */
import { useCallback, useEffect, useState } from "react";
import type { ReactNode } from "react";
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
    Page,
    Pager,
    PeriodBlock,
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
import { InvoicesApi, type InvoiceDto, type InvoiceFundOverviewRowDto, type InvoiceSummaryDto } from "@/apis-gen";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { shiftPeriod, useDefaultAccountingPeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
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

/* ---------- 一键取票（引导页，建设中） ---------- */

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
                <div className="flex w-[860px] items-center gap-10">
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
            <Btn variant="primary">{children}</Btn>
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
            setAddForm({ invoiceType: "增值税专用发票", issueDate: "", invoiceNo: "", partnerName: "", goodsName: "", amount: "", taxRate: "13" });
            setAddToast("发票已登记");
            await data.reload();
        } catch (e) {
            setAddToast(await apiErrorMessage(e, "登记失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 批量认证：后端仅提供单条认证端点，这里对勾选行逐张调用（认证日期取期间末、税款所属期取当前期间）。 */
    const batchAuth = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (selected.size === 0) {
            setAddToast("请先勾选要认证的进项发票");
            return;
        }
        setBusy(true);
        const api = new InvoicesApi(apiConfig(token));
        let ok = 0;
        let fail = 0;
        for (const id of selected) {
            try {
                await api.accountsetsAccountSetIdInvoicesIdAuthenticatePost({
                    accountSetId,
                    id,
                    authenticateInvoiceRequest: { authDate: new Date(period.endDate), taxPeriod: period.period },
                });
                ok += 1;
            } catch {
                fail += 1;
            }
        }
        setSelected(new Set());
        setBusy(false);
        setAddToast(fail > 0 ? `认证成功 ${ok} 张，失败 ${fail} 张（仅进项发票可认证）` : `已认证 ${ok} 张发票`);
        await data.reload();
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
                        <WipBtn dropdown title="后端未提供发票导出端点，建设中">导出</WipBtn>
                        <WipBtn dropdown title="后端未提供发票导入端点，建设中">导入</WipBtn>
                        <Btn dropdown onClick={() => window.print()}>打印列表</Btn>
                        {kind === "purchase" && (
                            <Btn dropdown disabled={busy} title="对勾选的进项发票逐张调用认证端点" onClick={() => void batchAuth()}>批量认证</Btn>
                        )}
                        <WipBtn dropdown title="批量操作建设中，后端未提供端点">批量操作</WipBtn>
                        <WipBtn dropdown title="后端未提供发票生成凭证端点，建设中">生成凭证</WipBtn>
                        <span className="cursor-pointer" onClick={() => void data.reload()}>
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <div className="flex-1">
                {error2(data.error)}
                {data.loading && <div className="px-3 py-2 text-[13px] text-slate-500">加载中…</div>}
                <Table
                    cols={cols}
                    rows={[
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
                            __style: { background: "var(--muted)", fontWeight: 500 },
                        } as Row,
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
                />
                {data.invoices.length === 0 && !data.loading && <EmptyTip />}
            </div>
            {/* 真实数据不足一页时隐藏分页控件，不显示假页码 */}
            {data.invoices.length > 20 && <Pager total={data.invoices.length} count={1} />}
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
                            ["发票种类：", <Select key="t" className="w-[220px]" value={addForm.invoiceType} onChange={(v) => setAddForm({ ...addForm, invoiceType: v })} options={["增值税专用发票", "增值税普通发票", "电子发票"]} />],
                            ["开票日期：", <DateInput key="d" className="w-[220px]" value={addForm.issueDate} onChange={(v) => setAddForm({ ...addForm, issueDate: v })} />],
                            ["发票号码：", <TextInput key="n" className="w-[220px]" value={addForm.invoiceNo} onChange={(v) => setAddForm({ ...addForm, invoiceNo: v })} />],
                            [kind === "sales" ? "客户名称：" : "供应商名称：", <TextInput key="p" className="w-[220px]" value={addForm.partnerName} onChange={(v) => setAddForm({ ...addForm, partnerName: v })} />],
                            ["商品名称：", <TextInput key="g" className="w-[220px]" value={addForm.goodsName} onChange={(v) => setAddForm({ ...addForm, goodsName: v })} />],
                            ["不含税金额：", <TextInput key="a" className="w-[220px] text-right tabular-nums" value={addForm.amount} onChange={(v) => setAddForm({ ...addForm, amount: v.replace(/[^\d.]/g, "") })} />],
                            ["税率(%)：", <TextInput key="r" className="w-[220px] text-right tabular-nums" value={addForm.taxRate} onChange={(v) => setAddForm({ ...addForm, taxRate: v.replace(/[^\d.]/g, "") })} />],
                        ] as Array<[string, ReactNode]>).map(([label, control]) => (
                            <label key={label} className="flex items-center gap-3">
                                <span className="w-[90px] text-right">{label}</span>
                                {control}
                            </label>
                        ))}
                    </div>
                </Dialog>
            )}
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

/* ---------- 费用单据（建设中，后端未提供端点） ---------- */

export function LemonExpenseBill() {
    const period = useDefaultAccountingPeriod();
    const cols: Col[] = [
        { key: "ck", title: "", width: "36px" },
        { key: "date", title: "单据日期", width: "110px" },
        { key: "reason", title: "费用事由" },
        { key: "type", title: "费用类型", width: "110px" },
        { key: "payee", title: "收款人", width: "100px" },
        { key: "settle", title: "结算方式", width: "110px" },
        { key: "deductTax", title: "可抵扣税额", align: "right", width: "120px" },
        { key: "amount", title: "单据金额（含税）", align: "right", width: "120px" },
        { key: "voucher", title: "关联凭证", width: "110px" },
        { key: "ops", title: "操作", width: "100px" },
        { key: "no", title: "单据编号", width: "130px" },
    ];
    return (
        <Page title="费用单据" subtitle="费用单据登记与生成凭证（建设中，暂无真实数据）" icon={ProfileOutlined}>
            <WipNotice text="费用单据后端未提供数据端点，本页为界面复刻占位（建设中），无真实数据。" />
            <ToolRow
                left={
                    <>
                        <PeriodBlock range={[period.startDate, period.endDate]} />
                        <CheckBox label="显示全部" />
                    </>
                }
                right={
                    <>
                        <WipBtn dropdown title="后端未提供费用单据导入端点">导入</WipBtn>
                        <WipBtn dropdown title="后端未提供费用单据导出端点">导出</WipBtn>
                        <WipBtn dropdown title="批量操作建设中">批量操作</WipBtn>
                        <WipBtn dropdown title="后端未提供费用单据生成凭证端点">生成凭证</WipBtn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1">
                <Table cols={cols} rows={[]} empty={<EmptyTip />} />
            </div>
        </Page>
    );
}

/* ---------- 发票资金一览表（已接后端） ---------- */

export function LemonInvoiceSummary() {
    const defaultPeriod = useDefaultAccountingPeriod();
    const [userPeriod, setUserPeriod] = useState<AccountingPeriod | null>(null);
    const period = userPeriod ?? defaultPeriod;
    const { rows, loading, error, reload } = useFundOverview(period.startDate, period.endDate);
    const cols: Col[] = [
        { key: "partner", title: "往来单位", width: "200px" },
        { key: "sales", title: "销项发票金额（含税）", align: "right", group: "发票" },
        { key: "received", title: "收款金额", align: "right", group: "资金" },
        { key: "notInvoiced", title: "未开票金额（含税）", align: "right", group: "差异" },
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
                        <WipBtn title="后端未提供资金一览导出端点，建设中">导出</WipBtn>
                        <span className="cursor-pointer" onClick={() => void reload()}>
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <div className="flex-1">
                {error2(error)}
                {loading && <div className="px-3 py-2 text-[13px] text-slate-500">加载中…</div>}
                <Table cols={cols} rows={tableRows} empty={rows.length === 0 && !loading && !error ? <EmptyTip /> : undefined} />
            </div>
            {/* 真实数据不足一页时隐藏分页控件，不显示假页码 */}
            {rows.length > 20 && <Pager total={rows.length} count={1} />}
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
                <div className="flex w-[1100px] items-center gap-10 rounded-2xl border border-slate-100 bg-white px-10 py-12 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
                    <div className="flex h-[240px] w-[320px] shrink-0 items-center justify-center rounded-xl bg-[#fafbfd] text-[13px] text-slate-400">
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
                  __style: { background: "var(--muted)", fontWeight: 700, color: "#1e293b" },
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

    /** 入账：后端未提供发票生成凭证端点，这里把暂不入账发票状态恢复为「未入账」，重新回到入账流程。 */
    const postNow = async (ids: string[]) => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (ids.length === 0) {
            setToast("请先勾选要入账的发票");
            return;
        }
        setBusy(true);
        const api = new InvoicesApi(apiConfig(token));
        let ok = 0;
        for (const id of ids) {
            try {
                await api.accountsetsAccountSetIdInvoicesIdStatusPost({ accountSetId, id, changeInvoiceStatusRequest: { status: 1 } });
                ok += 1;
            } catch {
                // 单张失败不中断批量
            }
        }
        setSelected(new Set());
        setBusy(false);
        setToast(`已恢复 ${ok} 张发票至入账流程`);
        await data.reload();
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
                      __style: { background: "var(--muted)" },
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
                    ops: (
                        <span className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline" onClick={() => void postNow([inv.id ?? ""])}>
                            入账
                        </span>
                    ),
                }) as Row,
        ),
    ];
    return (
        <Page title="进项发票" subtitle="暂不入账发票可恢复入账流程" icon={FileDoneOutlined}>
            <SubTabs tabs={["进项发票", "商品明细", "暂不入账发票"]} active={2} onChange={onTab} />
            <WipNotice text="「入账」仅把发票恢复为未入账状态；发票生成凭证端点后端尚未提供，仍属建设中。导出与批量操作建设中。" />
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
                        <WipBtn title="后端未提供发票导出端点，建设中">导出</WipBtn>
                        <WipBtn dropdown title="批量操作建设中，后端未提供端点">批量操作</WipBtn>
                        <Btn variant="primary" disabled={busy} title="把勾选的暂不入账发票恢复为未入账" onClick={() => void postNow([...selected])}>入账</Btn>
                        <span className="cursor-pointer" onClick={() => void data.reload()}>
                            <RefreshIcon />
                        </span>
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

/** 收票宝落地页（建设中）。 */
export function LemonSpb() {
    return (
        <Page title="收票宝" subtitle="员工收票交票，财务审批归档，企业电子档案管理好帮手" icon={ProfileOutlined}>
            <WipNotice text="收票宝需对接员工收票 / 电子档案服务，后端尚未提供端点，本页为功能介绍（建设中）。" />
            <div className="flex flex-1 flex-col items-center pt-10">
                <div className="text-[16px] text-slate-500">帮您智能收取票据，一键提交财务，解放双手之神器</div>
                <div className="mt-8">
                    <WipBtn title="收票宝建设中，后端未提供授权端点">立即授权</WipBtn>
                </div>
                <div className="mt-10 flex w-[900px] items-center gap-8 rounded-2xl bg-[#fafbfd] p-8">
                    <div className="flex-1">
                        <div className="text-[15px] font-bold text-slate-800">收票宝亮点：</div>
                        <div className="mt-4 flex flex-col gap-3 text-[13px] text-slate-700">
                            {["多方采集，自动核验", "一键交票，告别粘贴", "关联凭证，高效利用", "智能归档，无忧存储"].map((t) => (
                                <span key={t} className="flex items-center gap-2">
                                    <span className="flex h-3.5 w-3.5 items-center justify-center rounded-md bg-[#2f6bff] text-[10px] text-white">✓</span>
                                    {t}
                                </span>
                            ))}
                        </div>
                    </div>
                    <div className="flex gap-4">
                        <div className="flex h-[180px] w-[90px] items-center justify-center rounded-lg border border-slate-200 bg-white text-[12px] text-slate-400">手机样机1</div>
                        <div className="flex h-[180px] w-[90px] items-center justify-center rounded-lg border border-slate-200 bg-white text-[12px] text-slate-400">手机样机2</div>
                    </div>
                </div>
            </div>
        </Page>
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
                            <span className="w-[170px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 公司名称：</span>
                            <FakeInput className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 统一社会信用代码：</span>
                            <FakeInput className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 银行账户：</span>
                            <FakeSelect text="" className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 银行账号：</span>
                            <FakeInput className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-sm text-slate-700">企业客户号：</span>
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
                    <span className="text-slate-800">柠檬云与各大银行 </span>
                    <span className="text-[#f59e0b]">携手研发</span>
                </span>
                <span className="text-[13px] text-slate-500">实时同步对账单，自动生成凭证，银行级加密，安全可靠</span>
                <span className="text-[26px]">🏦</span>
            </div>
        </Page>
    );
}

/** 一键登录税局落地页（建设中：第一步引导弹窗为示意，不提交任何数据）。 */
export function LemonTaxLogin() {
    return (
        <Page
            title="一键登录税局"
            subtitle="只需一次维护，即可直接登录电子税务局，进行取票、开票、报税、社保业务、征纳互动等操作"
            icon={KeyOutlined}
        >
            <WipNotice text="一键登录税局需对接电子税务局免密登录服务，后端尚未提供端点，本页与弹窗均为流程示意（建设中）。" />
            <Dialog
                title="一键登录电子税局"
                width={900}
                footer={
                    <div className="flex justify-center gap-3">
                        <WipBtn title="一键登录税局建设中，后端未提供端点">取消</WipBtn>
                        <WipBtn title="一键登录税局建设中，后端未提供端点">下一步</WipBtn>
                    </div>
                }
            >
                <div className="rounded-xl bg-[#fafbfd] p-4 text-[13px] leading-6">
                    <div className="flex items-start gap-2">
                        <span className="text-[#f59e0b]">⚠</span>
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
                        <span className="w-[190px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 公司名称：</span>
                        <FakeInput className="w-[320px]" />
                    </div>
                    <div className="flex items-center">
                        <span className="w-[190px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 统一社会信用代码：</span>
                        <FakeInput className="w-[320px]" />
                    </div>
                    <div className="flex items-center">
                        <span className="w-[190px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 报税地区：</span>
                        <FakeSelect text="" className="w-[320px]" />
                    </div>
                    <div className="flex items-center">
                        <span className="w-[190px]" />
                        <CheckBox label={<>我已阅读并同意 <span className="text-[#2f6bff]">《税局登录业务隐私政策》</span></>} />
                    </div>
                </div>
            </Dialog>
        </Page>
    );
}
