/**
 * 复刻区发票模块（对应柠檬云 /Invoice/*）：
 * 一键取票（引导页）、销项发票、进项发票、费用单据、发票资金一览表、发票风险分析。
 */
import { useCallback, useEffect, useState } from "react";
import { CalendarDays, ChevronDown, CircleHelp, HelpCircle, MoveRight } from "lucide-react";

import {
    Btn,
    CheckBox,
    Dialog,
    EmptyTip,
    FakeInput,
    FakeSelect,
    Page,
    Pager,
    PeriodBlock,
    RefreshIcon,
    SplitBtn,
    SubTabs,
    Toast,
    ToolRow,
} from "./components/ui";
import type { Col } from "./components/ui";
import { InvoicesApi, type InvoiceDto, type InvoiceFundOverviewRowDto, type InvoiceSummaryDto } from "@/apis-gen";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";

/** 发票数据 Hook（按方向取列表与合计）。 */
function useInvoiceData(direction: 1 | 2) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [invoices, setInvoices] = useState<InvoiceDto[]>([]);
    const [summary, setSummary] = useState<InvoiceSummaryDto | null>(null);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) return;
        const api = new InvoicesApi(apiConfig(token));
        try {
            setInvoices(await api.accountsetsAccountSetIdInvoicesGet({ accountSetId, direction, startDate: new Date("2026-10-01"), endDate: new Date("2026-10-31") }));
            setSummary(await api.accountsetsAccountSetIdInvoicesSummaryGet({ accountSetId, direction, startDate: new Date("2026-10-01"), endDate: new Date("2026-10-31") }));
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载发票失败。"));
        }
    }, [accountSetId, token, direction]);

    useEffect(() => {
        void reload();
    }, [reload]);

    return { invoices, summary, error, reload };
}

/** 发票资金一览表数据 Hook。 */
function useFundOverview() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [rows, setRows] = useState<InvoiceFundOverviewRowDto[]>([]);
    const reload = useCallback(async () => {
        if (!accountSetId) return;
        try {
            setRows(await new InvoicesApi(apiConfig(token)).accountsetsAccountSetIdInvoicesFundOverviewGet({
                accountSetId, startDate: new Date("2026-10-01"), endDate: new Date("2026-10-31"),
            }));
        } catch {
            setRows([]);
        }
    }, [accountSetId, token]);
    useEffect(() => {
        void reload();
    }, [reload]);
    return rows;
}

/* ---------- 一键取票（引导页） ---------- */

const FETCH_POINTS = [
    { title: "智能获取发票数据", desc: "一键获取进销项发票，轻松获取1万+发票" },
    { title: "数电发票电子文件采集", desc: "智能获取数电发票电子文件，自动关联发票数据" },
    { title: "数据安全保障", desc: "单向式获取发票数据，数据安全有保障" },
];

export function LemonInvoiceFetch() {
    return (
        <Page>
            <div className="flex flex-1 flex-col items-center justify-center py-16">
                <div className="flex items-center gap-4 text-[28px] font-bold text-[#333]">
                    <MoveRight className="h-6 w-6 text-[#44b449]" />
                    一键取票
                    <MoveRight className="h-6 w-6 rotate-180 text-[#44b449]" />
                </div>
                <div className="mt-3 text-[14px] text-[#666]">
                    只需一次维护，即可直接获取进销项发票数据、数电发票电子文件，实现发票智能化
                </div>
                <div className="mt-10 flex w-[860px] items-center gap-10">
                    {/* 左侧显示器插画（简化为示意框） */}
                    <div className="flex h-[220px] w-[300px] shrink-0 items-center justify-center rounded border border-[#e8e8e8] bg-[#fafafa] text-[13px] text-[#bbb]">
                        取票界面示意
                    </div>
                    <div className="flex flex-1 flex-col gap-6">
                        {FETCH_POINTS.map((p, i) => (
                            <div key={p.title} className="flex items-start gap-3">
                                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[#44b449] text-[13px] text-white">
                                    {i + 1}
                                </span>
                                <div>
                                    <div className="text-[15px] font-bold text-[#333]">{p.title}</div>
                                    <div className="mt-1 text-[13px] text-[#666]">{p.desc}</div>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
                <button className="mt-12 h-10 cursor-pointer rounded-[2px] bg-[#44b449] px-10 text-[15px] text-white">
                    立即取票
                </button>
            </div>
        </Page>
    );
}

/* ---------- 销项/进项发票列表 ---------- */

function HotBadge() {
    return (
        <span className="absolute -top-2 -right-3 rounded-[2px] bg-[#f5222d] px-0.5 text-[10px] leading-[14px] text-white">
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

/** 发票列表页（销项/进项共用骨架，含商品明细/暂不入账页签；新增发票已接后端）。 */
function InvoiceListPage({ kind }: { kind: "sales" | "purchase" }) {
    const [tab, setTab] = useState(0);
    const data = useInvoiceData(kind === "sales" ? 1 : 2);
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [showAdd, setShowAdd] = useState(false);
    const [addForm, setAddForm] = useState({ invoiceType: "增值税专用发票", issueDate: "", invoiceNo: "", partnerName: "", goodsName: "", amount: "", taxRate: "13" });
    const [addToast, setAddToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

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
    if (tab === 1) return <InvoiceGoodsTable kind={kind} />;
    if (tab === 2) return <InvoicePendingTable />;
    const cols: Col[] = [
        { key: "ck", title: <CheckBox label="" />, width: "36px" },
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
        { key: "ops", title: "操作", width: "80px" },
    ];
    const tabs = kind === "sales" ? ["销项发票", "商品明细"] : ["进项发票", "商品明细", "暂不入账发票"];
    return (
        <Page>
            <SubTabs tabs={tabs} active={tab} onChange={setTab} />
            <ToolRow
                left={<PeriodBlock text="2026年10月" />}
                right={
                    <>
                        <CheckBox label="显示所有信息" />
                        {kind === "sales" && <HotBtn>开具发票</HotBtn>}
                        <HotBtn>一键取票</HotBtn>
                        <Btn dropdown onClick={() => setShowAdd(true)}>新增发票</Btn>
                        <Btn dropdown>导出</Btn>
                        <Btn dropdown>导入</Btn>
                        <Btn dropdown onClick={() => window.print()}>打印列表</Btn>
                        {kind === "purchase" && <Btn dropdown>批量认证</Btn>}
                        <Btn dropdown>批量操作</Btn>
                        <SplitBtn>生成凭证</SplitBtn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                {data.error && <div className="px-3 py-2 text-[13px] text-[#f5222d]">{data.error}</div>}
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                            {cols.map((c) => (
                                <th
                                    key={c.key}
                                    className={`border-b border-[#e8e8e8] px-3 py-2.5 font-normal ${c.align === "right" ? "text-right" : "text-left"}`}
                                    style={{ width: c.width }}
                                >
                                    {c.title}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        <tr className="border-b border-[#f0f0f0]">
                            <td colSpan={7} className="px-3 py-2.5 text-[13px] text-[#333]">合计</td>
                            <td className="px-3 py-2.5 text-right text-[13px]">{(data.summary?.totalAmount ?? 0).toFixed(2)}</td>
                            <td />
                            <td className="px-3 py-2.5 text-right text-[13px]">{(data.summary?.totalTax ?? 0).toFixed(2)}</td>
                            <td className="px-3 py-2.5 text-right text-[13px]">{(data.summary?.totalWithTax ?? 0).toFixed(2)}</td>
                            <td />
                            <td />
                        </tr>
                        {data.invoices.map((inv) => (
                            <tr key={inv.id} className="border-b border-[#f0f0f0]">
                                <td className="px-3 py-2"><CheckBox label="" /></td>
                                <td className="px-3 py-2.5 text-[13px]">{inv.invoiceType}</td>
                                <td className="px-3 py-2.5 text-[13px]">{inv.inputDate ? new Date(inv.inputDate).toISOString().substring(0, 10) : ""}</td>
                                <td className="px-3 py-2.5 text-[13px]">{inv.issueDate ? new Date(inv.issueDate).toISOString().substring(0, 10) : ""}</td>
                                <td className="px-3 py-2.5 text-[13px]">{inv.invoiceNo}</td>
                                <td className="px-3 py-2.5 text-[13px]">{inv.partnerName}</td>
                                <td className="px-3 py-2.5 text-[13px]">{inv.bizType}</td>
                                <td className="px-3 py-2.5 text-right text-[13px]">{(inv.totalAmount ?? 0).toFixed(2)}</td>
                                <td className="px-3 py-2.5 text-[13px]" />
                                <td className="px-3 py-2.5 text-right text-[13px]">{(inv.totalTax ?? 0).toFixed(2)}</td>
                                <td className="px-3 py-2.5 text-right text-[13px]">{(inv.totalWithTax ?? 0).toFixed(2)}</td>
                                <td className="px-3 py-2.5 text-[13px]">{inv.voucherText}</td>
                                <td className="px-3 py-2.5 text-[13px]">{inv.source}</td>
                                <td className="px-3 py-2.5 text-[13px]">{inv.statusText}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {data.invoices.length === 0 && <EmptyTip />}
            </div>
            <Pager total={data.invoices.length} count={data.invoices.length} />
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
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-[#333]">
                        {[
                            ["发票种类：", <select key="t" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] bg-white px-2 outline-none focus:border-[#44b449]" value={addForm.invoiceType} onChange={(e) => setAddForm({ ...addForm, invoiceType: e.target.value })}>
                                <option>增值税专用发票</option>
                                <option>增值税普通发票</option>
                                <option>电子发票</option>
                            </select>],
                            ["开票日期：", <input key="d" type="date" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]" value={addForm.issueDate} onChange={(e) => setAddForm({ ...addForm, issueDate: e.target.value })} />],
                            ["发票号码：", <input key="n" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]" value={addForm.invoiceNo} onChange={(e) => setAddForm({ ...addForm, invoiceNo: e.target.value })} />],
                            [kind === "sales" ? "客户名称：" : "供应商名称：", <input key="p" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]" value={addForm.partnerName} onChange={(e) => setAddForm({ ...addForm, partnerName: e.target.value })} />],
                            ["商品名称：", <input key="g" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]" value={addForm.goodsName} onChange={(e) => setAddForm({ ...addForm, goodsName: e.target.value })} />],
                            ["不含税金额：", <input key="a" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 text-right outline-none focus:border-[#44b449]" value={addForm.amount} onChange={(e) => setAddForm({ ...addForm, amount: e.target.value.replace(/[^\d.]/g, "") })} />],
                            ["税率(%)：", <input key="r" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 text-right outline-none focus:border-[#44b449]" value={addForm.taxRate} onChange={(e) => setAddForm({ ...addForm, taxRate: e.target.value.replace(/[^\d.]/g, "") })} />],
                        ].map(([label, control]) => (
                            <label key={String(label)} className="flex items-center gap-3">
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

/** 销项发票。 */
export function LemonSalesInvoice() {
    return <InvoiceListPage kind="sales" />;
}

/** 进项发票。 */
export function LemonPurchaseInvoice() {
    return <InvoiceListPage kind="purchase" />;
}

/* ---------- 费用单据 ---------- */

export function LemonExpenseBill() {
    const cols = ["", "单据日期", "费用事由", "费用类型", "收款人", "结算方式", "可抵扣税额", "单据金额（含税）", "关联凭证", "操作", "单据编号"];
    return (
        <Page>
            <ToolRow
                left={
                    <>
                        <PeriodBlock range={["2026-10-01", "2026-10-31"]} />
                        <CheckBox label="显示全部" />
                    </>
                }
                right={
                    <>
                        <Btn dropdown>导入</Btn>
                        <Btn dropdown>导出</Btn>
                        <Btn dropdown>批量操作</Btn>
                        <SplitBtn>生成凭证</SplitBtn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                            {cols.map((t, i) => (
                                <th
                                    key={i}
                                    className="border-b border-[#e8e8e8] px-3 py-2.5 text-left font-normal"
                                    style={{ width: i === 6 || i === 7 ? "120px" : undefined }}
                                >
                                    {t}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        <tr className="border-b border-[#f0f0f0]">
                            <td className="px-3 py-2">
                                <CheckBox label="" />
                            </td>
                            <td className="px-2 py-1.5">
                                <span className="inline-flex h-7 w-full items-center justify-between rounded-[2px] border border-[#d9d9d9] px-1.5 text-[13px]">
                                    2026-10-12 <CalendarDays className="h-3 w-3 text-[#999]" />
                                </span>
                            </td>
                            <td className="px-2 py-1.5">
                                <span className="block h-7 rounded-[2px] border border-[#d9d9d9]" />
                            </td>
                            <td className="px-2 py-1.5">
                                <FakeSelect text="" className="h-7 w-full" />
                            </td>
                            <td className="px-2 py-1.5">
                                <FakeSelect text="" className="h-7 w-full" />
                            </td>
                            <td className="px-2 py-1.5">
                                <FakeSelect text="银行转账" className="h-7 w-full" />
                            </td>
                            <td className="px-2 py-1.5">
                                <span className="block h-7 rounded-[2px] border border-[#d9d9d9]" />
                            </td>
                            <td className="px-2 py-1.5">
                                <span className="block h-7 rounded-[2px] border border-[#d9d9d9]" />
                            </td>
                            <td />
                            <td />
                            <td className="px-3 py-2 text-[13px]">FY20261012095705025001</td>
                        </tr>
                        <tr className="border-b border-[#f0f0f0]">
                            <td />
                            <td />
                            <td className="px-3 py-2 text-[13px]">合计</td>
                            {cols.slice(3).map((_, i) => (
                                <td key={i} />
                            ))}
                        </tr>
                    </tbody>
                </table>
            </div>
            <Pager total={1} count={1} />
        </Page>
    );
}

/* ---------- 发票资金一览表 ---------- */

export function LemonInvoiceSummary() {
    const rows = useFundOverview();
    return (
        <Page>
            <ToolRow
                left={
                    <span className="inline-flex">
                        <PeriodBlock text="2026年10月" />
                        <span className="-ml-1 flex items-center rounded-r-[2px] border border-l-0 border-[#d9d9d9] bg-white px-1.5">
                            <ChevronDown className="h-3.5 w-3.5 text-[#999]" />
                        </span>
                    </span>
                }
                right={
                    <>
                        <Btn>导出</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                            <th rowSpan={2} className="border-b border-[#e8e8e8] px-3 py-2.5 font-normal">
                                往来单位
                            </th>
                            <th className="border-b border-[#e8e8e8] px-3 py-2.5 font-normal">发票</th>
                            <th className="border-b border-[#e8e8e8] px-3 py-2.5 font-normal">资金</th>
                            <th colSpan={2} className="border-b border-[#e8e8e8] px-3 py-2.5 font-normal">
                                差异
                            </th>
                            <th rowSpan={2} className="border-b border-[#e8e8e8] px-3 py-2.5 font-normal">
                                操作
                            </th>
                        </tr>
                        <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                            <th className="border-b border-[#e8e8e8] px-3 py-2 font-normal">销项发票金额（含税）</th>
                            <th className="border-b border-[#e8e8e8] px-3 py-2 font-normal">收款金额</th>
                            <th className="border-b border-[#e8e8e8] px-3 py-2 font-normal">未开票金额（含税）</th>
                            <th className="border-b border-[#e8e8e8] px-3 py-2 font-normal">未收款金额</th>
                        </tr>
                    </thead>
                    <tbody>
                        {rows.map((r) => (
                            <tr key={r.partnerName} className="border-b border-[#f0f0f0]">
                                <td className="px-3 py-2.5 text-[13px] text-[#3385ff]">{r.partnerName}</td>
                                <td className="px-3 py-2.5 text-right text-[13px]">{(r.salesWithTax ?? 0).toFixed(2)}</td>
                                <td className="px-3 py-2.5 text-right text-[13px]">{(r.received ?? 0).toFixed(2)}</td>
                                <td className="px-3 py-2.5 text-right text-[13px]">{(r.notInvoiced ?? 0).toFixed(2)}</td>
                                <td className="px-3 py-2.5 text-right text-[13px]">{(r.notReceived ?? 0).toFixed(2)}</td>
                                <td className="px-3 py-2.5 text-[13px] text-[#3385ff]">核对</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {rows.length === 0 && <EmptyTip />}
            </div>
            <Pager total={rows.length} count={rows.length} />
        </Page>
    );
}

/* ---------- 发票风险分析（引导页） ---------- */

const RISK_POINTS = [
    {
        color: "#2fbf6c",
        text: "发票风险分析数据来源于企业通过一键取票从税局获取的发票数据，通过获取的发票数据，依据相关政策规定为您公司检测可能存在的发票风险及销售采购资金分析等",
    },
    {
        color: "#fadb14",
        text: "进销项对比、虚开发票、采购虚假发票、销售采购资金分析、销售收入分析等多维度分析发票潜在风险",
    },
    {
        color: "#f5222d",
        text: "通过发票风险分析，可了解企业当前开票是否合规，客户及供应商情况，商品明细信息，规避企业潜在的发票风险",
    },
];

export function LemonInvoiceRisk() {
    return (
        <Page>
            <div className="flex items-center justify-center border-b border-[#e8e8e8] py-4 text-[17px] font-bold text-[#333]">
                发票风险分析
            </div>
            <div className="flex flex-1 items-center justify-center py-10">
                <div className="flex w-[1100px] items-center gap-10 rounded border border-[#e8e8e8] bg-white px-10 py-12">
                    <div className="flex h-[240px] w-[320px] shrink-0 items-center justify-center rounded bg-[#fafafa] text-[13px] text-[#bbb]">
                        风险分析插画
                    </div>
                    <div className="flex flex-1 flex-col items-center">
                        <div className="text-[22px] font-bold text-[#333]">发票风险分析</div>
                        <div className="mt-6 flex flex-col gap-4">
                            {RISK_POINTS.map((p, i) => (
                                <div key={i} className="flex items-start gap-2 text-[13px] text-[#666]">
                                    <span
                                        className="mt-1.5 h-2 w-2 shrink-0 rounded-full"
                                        style={{ background: p.color }}
                                    />
                                    {p.text}
                                </div>
                            ))}
                        </div>
                        <button className="mt-8 h-9 cursor-pointer rounded-[2px] bg-[#44b449] px-8 text-sm text-white">
                            立即分析
                        </button>
                    </div>
                </div>
            </div>
        </Page>
    );
}

/** 商品明细页签（销项/进项共用骨架）。 */
function InvoiceGoodsTable({ kind }: { kind: "sales" | "purchase" }) {
    const cols: Col[] = [
        { key: "ck", title: <CheckBox label="" />, width: "36px" },
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
        { key: "ops", title: "操作", width: "80px" },
    ];
    return (
        <Page>
            <SubTabs tabs={kind === "sales" ? ["销项发票", "商品明细"] : ["进项发票", "商品明细", "暂不入账发票"]} active={1} />
            <ToolRow
                left={<PeriodBlock text="2026年10月" />}
                right={
                    <>
                        <Btn dropdown>批量操作</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                            {cols.map((c) => (
                                <th key={c.key} className={`border-b border-[#e8e8e8] px-3 py-2.5 font-normal ${c.align === "right" ? "text-right" : "text-left"}`} style={{ width: c.width }}>
                                    {c.title}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        <tr className="border-b border-[#f0f0f0]">
                            <td colSpan={cols.length - 3} className="px-3 py-2.5 text-[13px] text-[#333]">合计</td>
                            <td className="px-3 py-2.5 text-right text-[13px]">0.00</td>
                            <td />
                            <td className="px-3 py-2.5 text-right text-[13px]">0.00</td>
                            <td className="px-3 py-2.5 text-right text-[13px]" />
                            <td />
                        </tr>
                    </tbody>
                </table>
                <EmptyTip />
            </div>
            <Pager total={0} count={0} />
        </Page>
    );
}

/** 暂不入账发票页签。 */
function InvoicePendingTable() {
    return (
        <Page>
            <SubTabs tabs={["进项发票", "商品明细", "暂不入账发票"]} active={2} />
            <ToolRow
                left={<PeriodBlock text="2026年10月" />}
                right={
                    <>
                        <CheckBox label="显示所有信息" />
                        <Btn>导出</Btn>
                        <Btn dropdown>批量操作</Btn>
                        <span className="relative inline-flex">
                            <Btn variant="primary">入账</Btn>
                            <CircleHelp className="absolute -right-4 top-2 h-3.5 w-3.5 text-[#999]" />
                        </span>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                            {["", "发票种类", "录入日期", "开票日期", "发票号码", "发票状态", "供应商名称", "商品名称", "合计金额", "税率", "合计税额", "价税合计", "发票来源", "操作"].map((t, i) => (
                                <th key={i} className={`border-b border-[#e8e8e8] px-3 py-2.5 text-left font-normal ${["合计金额", "合计税额", "价税合计"].includes(t) ? "text-right" : ""}`}>{t}</th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        <tr className="border-b border-[#f0f0f0]">
                            <td colSpan={9} className="px-3 py-2.5 text-[13px] text-[#333]">合计</td>
                            <td className="px-3 py-2.5 text-right text-[13px]">0.00</td>
                            <td />
                            <td className="px-3 py-2.5 text-right text-[13px]">0.00</td>
                            <td className="px-3 py-2.5 text-right text-[13px]" />
                            <td />
                        </tr>
                    </tbody>
                </table>
                <EmptyTip />
            </div>
            <Pager total={0} count={0} />
        </Page>
    );
}

/** 收票宝落地页。 */
export function LemonSpb() {
    return (
        <Page>
            <div className="flex flex-1 flex-col items-center pt-16">
                <div className="max-w-[760px] text-center text-[24px] font-bold leading-9 text-[#333]">
                    员工收票交票，财务审批归档，企业电子档案管理好帮手
                </div>
                <div className="mt-3 text-[16px] text-[#666]">帮您智能收取票据，一键提交财务，解放双手之神器</div>
                <Btn variant="primary" className="mt-8 !h-10 !px-10">立即授权</Btn>
                <div className="mt-10 flex w-[900px] items-center gap-8 rounded-md bg-[#fafafa] p-8">
                    <div className="flex-1">
                        <div className="text-[15px] font-bold text-[#333]">收票宝亮点：</div>
                        <div className="mt-4 flex flex-col gap-3 text-[13px] text-[#333]">
                            {["多方采集，自动核验", "一键交票，告别粘贴", "关联凭证，高效利用", "智能归档，无忧存储"].map((t) => (
                                <span key={t} className="flex items-center gap-2">
                                    <span className="flex h-3.5 w-3.5 items-center justify-center rounded-[2px] bg-[#44b449] text-[10px] text-white">✓</span>
                                    {t}
                                </span>
                            ))}
                        </div>
                    </div>
                    <div className="flex gap-4">
                        <div className="flex h-[180px] w-[90px] items-center justify-center rounded-lg border border-[#e8e8e8] bg-white text-[12px] text-[#bbb]">手机样机1</div>
                        <div className="flex h-[180px] w-[90px] items-center justify-center rounded-lg border border-[#e8e8e8] bg-white text-[12px] text-[#bbb]">手机样机2</div>
                    </div>
                </div>
            </div>
        </Page>
    );
}

/** 银企互联落地页（银行页签 + 授权表单卡 + 底部横幅）。 */
const BANK_TABS = ["工商银行", "中国银行", "招商银行", "邮储银行", "交通银行", "中信银行", "浦发银行", "微众银行", "平安银行", "光大银行", "民生银行", "广发银行"];

export function LemonBankLink() {
    const [bank, setBank] = useState(1);
    return (
        <Page>
            <div className="pt-5 text-center text-[16px] text-[#333]">银企互联</div>
            <div className="mt-3 flex flex-wrap items-center justify-center gap-6 px-6">
                {BANK_TABS.map((t, i) => (
                    <span key={t} onClick={() => setBank(i)} className={`cursor-pointer pb-1 text-sm select-none ${i === bank ? "border-b-2 border-[#44b449] text-[#44b449]" : "text-[#666]"}`}>
                        {t}
                    </span>
                ))}
            </div>
            <div className="mx-auto mt-6 w-[1220px] rounded-[2px] border border-[#e8e8e8] bg-white p-8">
                <div className="flex justify-end gap-1 text-[13px]">
                    <span className="flex h-5 w-5 items-center justify-center rounded-full bg-[#3385ff] text-[11px] text-white">?</span>
                    <span className="cursor-pointer text-[#3385ff]">点此查看操作手册</span>
                </div>
                <div className="mt-4 flex gap-10">
                    <div className="flex flex-1 flex-col gap-5">
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 公司名称：</span>
                            <FakeInput text="杭州数从科技有限公司" className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 统一社会信用代码：</span>
                            <FakeInput text="91330105MAG0R8L09G" className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 银行账户：</span>
                            <FakeSelect text="" className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 银行账号：</span>
                            <FakeInput className="w-[320px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[170px] text-right text-sm text-[#333]">企业客户号：</span>
                            <span className="inline-flex h-8 w-[320px] items-center rounded-[2px] border border-[#d9d9d9] px-2.5 text-[13px] text-[#999]">
                                若集团账号签约，请输入企业客户号
                                <HelpCircle className="ml-auto h-3.5 w-3.5 text-[#999]" />
                            </span>
                        </div>
                    </div>
                    <div className="w-[370px] shrink-0 self-start rounded-[2px] bg-[#fafafa] p-5 text-[13px] leading-6">
                        <div className="text-[#f5222d]">提示：</div>
                        <div className="text-[#333]">1、请确认公司名称和统一社会信用代码</div>
                        <div className="text-[#333]">2、选择对应的银行账户，并确认银行账号和名称正确</div>
                        <div className="text-[#333]">3、在电脑上插入“{BANK_TABS[bank]}飞盾”</div>
                        <div className="text-[#333]">4、点击立即授权</div>
                    </div>
                </div>
                <div className="mt-8 flex justify-center">
                    <Btn variant="primary" className="!px-8">立即授权</Btn>
                </div>
                <div className="mt-5 text-center text-[13px] text-[#666]">
                    如有疑问，请 <span className="cursor-pointer text-[#3385ff]">咨询客服</span> ，咨询时间：周一至周日 8:30-12:00,13:30-20:30
                </div>
            </div>
            <div className="mx-auto mb-8 mt-8 flex h-[85px] w-[990px] items-center justify-between rounded-md bg-[#eaf3ff] px-10">
                <span className="text-[20px] font-bold">
                    <span className="text-[#1c4d8f]">柠檬云与各大银行 </span>
                    <span className="text-[#f5a623]">携手研发</span>
                </span>
                <span className="text-[13px] text-[#666]">实时同步对账单，自动生成凭证，银行级加密，安全可靠</span>
                <span className="text-[26px]">🏦</span>
            </div>
        </Page>
    );
}

/** 一键登录税局落地页（含第一步引导弹窗）。 */
export function LemonTaxLogin() {
    return (
        <Page>
            <div className="flex flex-1 flex-col items-center pt-14">
                <div className="flex items-center gap-4 text-[24px] font-bold text-[#333]">
                    <MoveRight className="h-6 w-6 text-[#44b449]" />
                    一键登录税局
                    <MoveRight className="h-6 w-6 rotate-180 text-[#44b449]" />
                </div>
                <div className="mt-3 max-w-[720px] text-center text-[14px] text-[#666]">
                    只需一次维护，即可直接登录电子税务局，进行取票、开票、报税、社保业务、征纳互动等操作
                </div>
            </div>
            <Dialog
                title="一键登录电子税局"
                width={900}
                footer={
                    <>
                        <Btn className="!px-6">取消</Btn>
                        <Btn variant="primary" className="!px-6">下一步</Btn>
                    </>
                }
            >
                <div className="rounded-[2px] bg-[#fafafa] p-4 text-[13px] leading-6">
                    <div className="flex items-start gap-2">
                        <span className="text-[#ff9f2e]">⚠</span>
                        <div className="text-[#333]">
                            <div>1.一键自动跳转登录电子税局，方便且高效</div>
                            <div>
                                2.首次使用或<span className="text-[#f5222d]">税务信息填写不准确</span>，均需要认证税局登录账号后，才能使用一键登录电子税局功能；税务信息填写完成后，可前往
                                <span className="cursor-pointer text-[#3385ff]">【设置-账套-税务信息】</span>中再次查看和修改
                            </div>
                            <div>
                                3.如您在使用过程中有任何疑问，请查看 <span className="cursor-pointer text-[#3385ff]">帮助文档</span> 或联系
                                <span className="ml-1 inline-flex h-6 items-center rounded-[2px] bg-[#3385ff] px-2 text-[12px] text-white">在线客服</span>
                            </div>
                        </div>
                    </div>
                </div>
                <div className="mt-5 flex items-center gap-3 text-[13px]">
                    <span className="flex items-center gap-1.5">
                        <span className="flex h-6 w-6 items-center justify-center rounded-full bg-[#44b449] text-[12px] text-white">1</span>
                        <span className="text-[#44b449]">确认公司信息</span>
                    </span>
                    <span className="h-px w-16 bg-[#e8e8e8]" />
                    <span className="flex items-center gap-1.5">
                        <span className="flex h-6 w-6 items-center justify-center rounded-full border border-[#d9d9d9] text-[12px] text-[#999]">2</span>
                        <span className="text-[#999]">填写税局登录信息</span>
                    </span>
                </div>
                <div className="mt-5 flex flex-col gap-4">
                    <div className="flex items-center">
                        <span className="w-[190px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 公司名称：</span>
                        <FakeInput text="杭州数从科技有限公司" className="w-[320px]" />
                    </div>
                    <div className="flex items-center">
                        <span className="w-[190px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 统一社会信用代码：</span>
                        <FakeInput text="91330105MAG0R8L09G" className="w-[320px]" />
                    </div>
                    <div className="flex items-center">
                        <span className="w-[190px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 报税地区：</span>
                        <FakeSelect text="浙江省" className="w-[320px]" />
                    </div>
                    <div className="flex items-center">
                        <span className="w-[190px]" />
                        <CheckBox label={<>我已阅读并同意 <span className="text-[#3385ff]">《税局登录业务隐私政策》</span></>} />
                    </div>
                </div>
            </Dialog>
        </Page>
    );
}
