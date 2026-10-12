/**
 * 复刻区资金模块（对应柠檬云 /Cashier/*）：
 * 现金/银行日记账（行内编辑网格 + 生成凭证交互链路）、内部转账、票据管理、
 * 资金报表（六页签汇总）、核对总账、收支类别（收入/支出 + 新增表单）、账户设置（现金/银行存款）。
 */
import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { ChevronDown, Info, Settings2 } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { AccountsApi } from "@/apis-gen";


import {
    Btn,
    CheckBox,
    Dialog,
    EmptyTip,
    FakeInput,
    Page,
    Pager,
    PeriodBlock,
    RefreshIcon,
    SplitBtn,
    SubTabs,
    Table,
    Toast,
    Toggle,
    ToolRow,
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { BillsApi, JournalsApi, type AccountDto, type BillDto, type JournalDetailDto, type JournalListItemDto } from "@/apis-gen";

/** 资金账户 / 票据数据 Hook。 */
function useFundData() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [journals, setJournals] = useState<JournalListItemDto[]>([]);
    const [bills, setBills] = useState<BillDto[]>([]);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) return;
        try {
            setJournals(await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsGet({ accountSetId }));
            setBills(await new BillsApi(apiConfig(token)).accountsetsAccountSetIdBillsGet({ accountSetId }));
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载资金数据失败。"));
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void reload();
    }, [reload]);

    return { journals, bills, error, reload };
}

/* ---------- 共用小件 ---------- */

/** 操作列蓝色链接组。 */
function Ops({ items }: { items: string[] }) {
    return (
        <span className="flex items-center gap-2 whitespace-nowrap">
            {items.map((t) => (
                <span key={t} className="cursor-pointer text-[13px] text-[#3385ff] hover:underline">
                    {t}
                </span>
            ))}
        </span>
    );
}

/* ---------- 现金/银行日记账（已接后端） ---------- */

const jpEdit = "h-7 w-full rounded-[2px] border border-[#d9d9d9] px-1.5 text-[13px] text-[#333] outline-none focus:border-[#44b449]";

/** 日记账页面：行来自后端流水；勾选行→生成凭证走「未选提示 toast / 未设科目引导弹窗 / 批量出票」三条链路。 */
function JournalPage({
    journalType,
    seqPrefix,
    bank,
}: {
    journalType: number;
    seqPrefix: string;
    bank?: boolean;
}) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const navigate = useNavigate();
    const [journals, setJournals] = useState<JournalListItemDto[]>([]);
    const [journalId, setJournalId] = useState<string>("");
    const [detail, setDetail] = useState<JournalDetailDto | null>(null);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [toast, setToast] = useState<string | null>(null);
    const [showGuide, setShowGuide] = useState(false);
    const [draft, setDraft] = useState({ date: "", summary: "", category: "", counterparty: "", income: "", expense: "" });
    const [error, setError] = useState<string | null>(null);
    const [tick, setTick] = useState(0);
    const [showAdd, setShowAdd] = useState(false);
    const [accounts, setAccounts] = useState<AccountDto[]>([]);
    const [addForm, setAddForm] = useState({ name: "", type: 1, accountId: "", openingBalance: "", bankName: "", bankAccountNo: "" });
    const importRef = useRef<HTMLInputElement>(null);

    const journal = journals.find((j) => j.id === journalId) ?? null;
    const reload = useCallback(async () => {
        if (!token || !accountSetId) {
            return;
        }
        setError(null);
        try {
            const api = new JournalsApi(apiConfig(token));
            const list = await api.accountsetsAccountSetIdJournalsGet({ accountSetId });
            const sameType = (list ?? []).filter((j) => j.type === journalType);
            setJournals(sameType);
            const target = journalId || sameType[0]?.id || "";
            setJournalId(target);
            setDetail(target ? await api.accountsetsAccountSetIdJournalsIdGet({ accountSetId, id: target }) : null);
            setSelected(new Set());
        } catch (e) {
            setError(await apiErrorMessage(e, "加载日记账失败。"));
        }
    }, [token, accountSetId, journalType, journalId]);
    useEffect(() => {
        void reload();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [token, accountSetId, journalType, tick]);

    const fmtDate = (v: Date | string | null | undefined): string =>
        v ? new Date(v).toISOString().substring(0, 10) : "";
    const nf = (v: number | null | undefined): string =>
        v == null ? "" : v.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    const switchJournal = async (id: string) => {
        setJournalId(id);
        setSelected(new Set());
        if (token && accountSetId && id) {
            setDetail(await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdGet({ accountSetId, id }));
        }
    };

    /** 打开新增日记账弹窗（首次加载明细科目下拉）。 */
    const openAdd = async () => {
        setShowAdd(true);
        if (token && accountSetId && accounts.length === 0) {
            try {
                setAccounts(await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true }));
            } catch {
                setToast(await apiErrorMessage(new Error("加载科目失败"), "加载科目失败"));
            }
        }
    };

    const createJournal = async () => {
        if (!token || !accountSetId || !addForm.name.trim()) {
            setToast("请填写日记账名称");
            return;
        }
        try {
            await new JournalsApi(apiConfig(token!)).accountsetsAccountSetIdJournalsPost({
                accountSetId,
                createJournalRequest: {
                    name: addForm.name.trim(),
                    type: addForm.type,
                    accountId: addForm.accountId || null,
                    openingBalance: Number(addForm.openingBalance) || 0,
                    bankName: addForm.type === 2 ? addForm.bankName.trim() || null : null,
                    bankAccountNo: addForm.type === 2 ? addForm.bankAccountNo.trim() || null : null,
                },
            });
            setShowAdd(false);
            setAddForm({ name: "", type: 1, accountId: "", openingBalance: "", bankName: "", bankAccountNo: "" });
            setToast("日记账已创建");
            setTick((t) => t + 1);
        } catch (e) {
            setToast(await apiErrorMessage(e, "创建失败"));
        }
    };

    const deleteEntry = async (entryId: string) => {
        if (!token || !accountSetId || !journalId) {
            return;
        }
        try {
            await new JournalsApi(apiConfig(token!)).accountsetsAccountSetIdJournalsIdEntriesEntryIdDelete({
                accountSetId,
                id: journalId,
                entryId,
            });
            setToast("流水已删除");
            setTick((t) => t + 1);
        } catch (e) {
            setToast(await apiErrorMessage(e, "删除失败"));
        }
    };

    /** 导出当前日记账为后端生成的表格文件。 */
    const exportJournal = async () => {
        if (!token || !accountSetId || !journalId) {
            return;
        }
        try {
            const res = await fetch(`/api/accountsets/${accountSetId}/journals/${journalId}/export`, {
                headers: { Authorization: `Bearer ${token}` },
            });
            if (!res.ok) {
                throw new Error("导出失败");
            }

            const blob = await res.blob();
            const url = URL.createObjectURL(blob);
            const anchor = document.createElement("a");
            anchor.href = url;
            anchor.download = `${detail?.name ?? "日记账"}.xlsx`;
            document.body.appendChild(anchor);
            anchor.click();
            anchor.remove();
            URL.revokeObjectURL(url);
        } catch (e) {
            setToast(e instanceof Error ? e.message : "导出失败");
        }
    };

    const importFile = async (file: File) => {
        if (!token || !accountSetId || !journalId || !file) {
            return;
        }
        try {
            const result = await new JournalsApi(apiConfig(token!)).accountsetsAccountSetIdJournalsIdImportPost({
                accountSetId,
                id: journalId,
                file,
            });
            setToast(`导入完成：成功 ${result.importedEntries ?? 0} / 共 ${result.totalRows ?? 0} 行`);
            setTick((t) => t + 1);
        } catch (e) {
            setToast(await apiErrorMessage(e, "导入失败"));
        } finally {
            if (importRef.current) {
                importRef.current.value = "";
            }
        }
    };

    const addEntry = async () => {
        if (!token || !accountSetId || !journalId) {
            return;
        }
        if (!draft.date || !draft.summary.trim()) {
            setToast("请填写日期与摘要");
            return;
        }
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEntriesPost({
                accountSetId,
                id: journalId,
                journalEntryRequest: {
                    date: new Date(draft.date),
                    summary: draft.summary.trim(),
                    category: draft.category.trim() || null,
                    counterparty: draft.counterparty.trim() || null,
                    income: Number(draft.income) || 0,
                    expense: Number(draft.expense) || 0,
                },
            });
            setDraft({ date: "", summary: "", category: "", counterparty: "", income: "", expense: "" });
            setToast("流水已登记");
            setTick((t) => t + 1);
        } catch (e) {
            setToast(await apiErrorMessage(e, "登记失败"));
        }
    };

    const genVoucher = async () => {
        if (selected.size === 0) {
            setToast("请选择凭证后生成凭证!");
            return;
        }
        if (!journal?.accountId) {
            setShowGuide(true);
            return;
        }
        try {
            const result = await new JournalsApi(apiConfig(token!)).accountsetsAccountSetIdJournalsIdEntriesVouchersPost({
                accountSetId,
                id: journalId,
                generateVouchersBatchRequest: { entryIds: [...selected], mergeBy: 1 },
            });
            setToast(`已生成 ${result.entryCount ?? 0} 张凭证`);
            setTick((t) => t + 1);
        } catch (e) {
            setToast(await apiErrorMessage(e, "生成凭证失败"));
        }
    };

    const entries = detail?.entries ?? [];
    const cols = [
        { key: "ck", title: <CheckBox label="" />, width: "36px" },
        { key: "date", title: "日期", width: "110px" },
        { key: "summary", title: "摘要" },
        { key: "ieType", title: "收支类别" },
        { key: "partner", title: "往来单位" },
        { key: "income", title: "收入 (借方)", align: "right" as const, width: "100px" },
        { key: "expense", title: "支出 (贷方)", align: "right" as const, width: "100px" },
        { key: "balance", title: "余额", align: "right" as const, width: "110px" },
        { key: "voucher", title: "关联凭证", width: "110px" },
        { key: "seq", title: "日记账序号", width: "150px" },
        {
            key: "ops",
            title: (
                <span className="inline-flex items-center gap-1">
                    操作 <Settings2 className="h-3 w-3 text-[#999]" />
                </span>
            ),
            width: "170px",
        },
    ];
    return (
        <Page>
            <ToolRow
                left={
                    <>
                        <select className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] bg-white px-2 text-sm text-[#333] outline-none" value={journalId} onChange={(e) => void switchJournal(e.target.value)}>
                            {journals.length === 0 && <option value="">（暂无日记账）</option>}
                            {journals.map((j) => (
                                <option key={j.id} value={j.id ?? ""}>
                                    {j.name ?? ""}
                                </option>
                            ))}
                        </select>
                        <PeriodBlock
                            range={
                                entries.length
                                    ? [fmtDate(entries[0]?.date), fmtDate(entries[entries.length - 1]?.date)]
                                    : undefined
                            }
                            text={detail?.name ?? ""}
                        />
                        <CheckBox label="显示全部" />
                    </>
                }
                right={
                    <>
                        {bank && (
                            <span className="inline-flex h-8 items-center gap-1 rounded-[2px] border border-[#44b449] px-3 text-sm text-[#44b449]">
                                银企互联 <Info className="h-3.5 w-3.5" />
                            </span>
                        )}
                        <Btn onClick={() => void openAdd()}>新增日记账</Btn>
                        <input
                            ref={importRef}
                            type="file"
                            accept=".xls,.xlsx"
                            className="hidden"
                            onChange={(e) => void importFile(e.target.files?.[0] as File)}
                        />
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <Btn dropdown onClick={() => importRef.current?.click()}>导入</Btn>
                        <Btn dropdown onClick={() => void exportJournal()}>导出</Btn>
                        <Btn dropdown>批量操作</Btn>
                        <Btn dropdown>关联凭证</Btn>
                        <SplitBtn>
                            <span onClick={() => void genVoucher()}>生成凭证</span>
                        </SplitBtn>
                        <span className="cursor-pointer" onClick={() => setTick((t) => t + 1)}>
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#f5222d]">{error}</div>}
            <div className="flex-1 px-4">
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
                                    {["summary", "ieType", "partner"].includes(String(c.key)) && (
                                        <ChevronDown className="ml-1 inline h-3 w-3 text-[#999]" />
                                    )}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {/* 期初行 */}
                        <tr className="border-b border-[#f0f0f0]">
                            <td />
                            <td />
                            <td className="px-3 py-2 text-[13px]">初始化余额</td>
                            <td />
                            <td />
                            <td />
                            <td />
                            <td className="px-2 py-1.5">
                                <span className="block h-7 border border-[#d9d9d9] px-2 text-right text-[13px] leading-7">
                                    {nf(detail?.openingBalance)}
                                </span>
                            </td>
                            <td />
                            <td />
                            <td />
                        </tr>
                        {/* 已有流水行（可勾选） */}
                        {entries.map((e) => (
                            <tr key={e.id} className={`border-b border-[#f0f0f0] ${selected.has(e.id ?? "") ? "bg-[#f0f9f0]" : ""}`}>
                                <td
                                    className="cursor-pointer px-3 py-2"
                                    onClick={() =>
                                        setSelected((prev) => {
                                            const key = e.id ?? "";
                                            const next = new Set([...prev].filter((k) => k !== key));
                                            if (!prev.has(key)) {
                                                next.add(key);
                                            }
                                            return next;
                                        })
                                    }
                                >
                                    <CheckBox label="" checked={selected.has(e.id ?? "")} />
                                </td>
                                <td className="px-3 py-2 text-[13px]">{fmtDate(e.date)}</td>
                                <td className="px-3 py-2 text-[13px]">{e.summary ?? ""}</td>
                                <td className="px-3 py-2 text-[13px]">{e.category ?? ""}</td>
                                <td className="px-3 py-2 text-[13px]">{e.counterparty ?? ""}</td>
                                <td className="px-3 py-2 text-right text-[13px]">{nf(e.income)}</td>
                                <td className="px-3 py-2 text-right text-[13px]">{nf(e.expense)}</td>
                                <td className="px-3 py-2 text-right text-[13px]">{nf(e.balance)}</td>
                                <td className="px-3 py-2 text-[13px]">{e.voucherNo ?? ""}</td>
                                <td className="px-3 py-2 text-[13px]">
                                    {`${seqPrefix}-${fmtDate(e.date).replaceAll("-", "")}-${e.sequence ?? ""}`}
                                </td>
                                <td className="px-3 py-2">
                                    <span className="flex items-center gap-2 whitespace-nowrap text-[13px] text-[#3385ff]">
                                        <span className="cursor-pointer">插入</span>
                                        <span className="cursor-pointer hover:underline" onClick={() => void deleteEntry(e.id ?? "")}>
                                            删除
                                        </span>
                                        <span className="cursor-pointer" onClick={() => window.print()}>打印</span>
                                        <span className="cursor-pointer">附件</span>
                                        <span className="cursor-pointer">复制</span>
                                    </span>
                                </td>
                            </tr>
                        ))}
                        {/* 行内新增编辑行 */}
                        <tr className="border-b border-[#f0f0f0]">
                            <td />
                            <td className="px-2 py-1.5">
                                <input type="date" className={jpEdit} value={draft.date} onChange={(ev) => setDraft({ ...draft, date: ev.target.value })} />
                            </td>
                            <td className="px-2 py-1.5">
                                <input className={`${jpEdit} border-[#44b449]`} value={draft.summary} onChange={(ev) => setDraft({ ...draft, summary: ev.target.value })} />
                            </td>
                            <td className="px-2 py-1.5">
                                <input className={jpEdit} value={draft.category} onChange={(ev) => setDraft({ ...draft, category: ev.target.value })} />
                            </td>
                            <td className="px-2 py-1.5">
                                <input className={jpEdit} value={draft.counterparty} onChange={(ev) => setDraft({ ...draft, counterparty: ev.target.value })} />
                            </td>
                            <td className="px-2 py-1.5">
                                <input className={`${jpEdit} text-right`} value={draft.income} onChange={(ev) => setDraft({ ...draft, income: ev.target.value.replace(/[^\d.]/g, "") })} />
                            </td>
                            <td className="px-2 py-1.5">
                                <input className={`${jpEdit} text-right`} value={draft.expense} onChange={(ev) => setDraft({ ...draft, expense: ev.target.value.replace(/[^\d.]/g, "") })} />
                            </td>
                            <td className="px-3 py-2 text-right text-[13px]" />
                            <td />
                            <td className="px-3 py-2 text-[13px]">{`${seqPrefix}-${draft.date.replaceAll("-", "") || "NEW"}`}</td>
                            <td className="px-3 py-2">
                                <Btn variant="primary" className="!h-7 !px-2 text-[12px]" onClick={() => void addEntry()}>
                                    保存
                                </Btn>
                            </td>
                        </tr>
                        {/* 合计行 */}
                        <tr className="border-b border-[#f0f0f0] bg-[#fafafa]">
                            <td />
                            <td />
                            <td className="px-3 py-2 text-[13px] font-bold">合计</td>
                            <td />
                            <td />
                            <td className="px-3 py-2 text-right text-[13px]">{nf(detail?.totalIncome)}</td>
                            <td className="px-3 py-2 text-right text-[13px]">{nf(detail?.totalExpense)}</td>
                            <td className="px-3 py-2 text-right text-[13px]">{nf(detail?.closingBalance)}</td>
                            <td colSpan={3} />
                        </tr>
                        {entries.length === 0 && !error && (
                            <tr>
                                <td colSpan={cols.length} className="py-10 text-center text-[13px] text-[#999]">
                                    本账户暂无流水，请在下方行内登记。
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>
            <Pager total={entries.length} count={entries.length} />
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            {/* 未设会计科目引导弹窗 */}
            {showGuide && (
                <Dialog
                    title="提示"
                    onClose={() => setShowGuide(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn
                                variant="primary"
                                onClick={() => {
                                    setShowGuide(false);
                                    navigate(`/${accountSetId}/settings/accounts`);
                                }}
                            >
                                前往处理
                            </Btn>
                            <Btn onClick={() => setShowGuide(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="text-center text-sm leading-6 text-[#333]">
                        请先设置资金账户{journal?.name ?? ""} 的会计科目，否则不能生成凭证，
                        <br />
                        是否立即前往处理？
                    </div>
                </Dialog>
            )}
            {/* 新增日记账弹窗 */}
            {showAdd && (
                <Dialog
                    title="新增日记账"
                    onClose={() => setShowAdd(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void createJournal()}>
                                保存
                            </Btn>
                            <Btn onClick={() => setShowAdd(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-[#333]">
                        <label className="flex items-center gap-3">
                            <span className="w-[90px] text-right">账簿类型：</span>
                            <select
                                className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] bg-white px-2 outline-none focus:border-[#44b449]"
                                value={addForm.type}
                                onChange={(e) => setAddForm({ ...addForm, type: Number(e.target.value) })}
                            >
                                <option value={1}>现金日记账</option>
                                <option value={2}>银行存款日记账</option>
                            </select>
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[90px] text-right">名称：</span>
                            <input
                                className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]"
                                value={addForm.name}
                                onChange={(e) => setAddForm({ ...addForm, name: e.target.value })}
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[90px] text-right">关联科目：</span>
                            <select
                                className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] bg-white px-2 outline-none focus:border-[#44b449]"
                                value={addForm.accountId}
                                onChange={(e) => setAddForm({ ...addForm, accountId: e.target.value })}
                            >
                                <option value="">（暂不关联）</option>
                                {(accounts ?? []).map((a) => (
                                    <option key={a.id} value={a.id}>
                                        {`${a.code ?? ""} ${a.name ?? ""}`}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[90px] text-right">期初余额：</span>
                            <input
                                className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 text-right outline-none focus:border-[#44b449]"
                                value={addForm.openingBalance}
                                onChange={(e) => setAddForm({ ...addForm, openingBalance: e.target.value.replace(/[^\d.]/g, "") })}
                            />
                        </label>
                        {addForm.type === 2 && (
                            <>
                                <label className="flex items-center gap-3">
                                    <span className="w-[90px] text-right">开户行：</span>
                                    <input
                                        className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]"
                                        value={addForm.bankName}
                                        onChange={(e) => setAddForm({ ...addForm, bankName: e.target.value })}
                                    />
                                </label>
                                <label className="flex items-center gap-3">
                                    <span className="w-[90px] text-right">银行账号：</span>
                                    <input
                                        className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]"
                                        value={addForm.bankAccountNo}
                                        onChange={(e) => setAddForm({ ...addForm, bankAccountNo: e.target.value })}
                                    />
                                </label>
                            </>
                        )}
                    </div>
                </Dialog>
            )}
        </Page>
    );
}

/** 银行日记账。 */
export function LemonDepositJournal() {
    return <JournalPage journalType={2} seqPrefix="2-001" bank />;
}
/* ---------- 票据管理 ---------- */

export function LemonDraft() {
    const data = useFundData();
    return (
        <Page>
            <SubTabs tabs={["票据台账"]} />
            <ToolRow
                left={
                    <>
                        <PeriodBlock range={["2026-10-05", "2026-10-12"]} />
                        <span className="flex items-center gap-1 text-[13px] text-[#3385ff]">
                            如何管理票据？ <Info className="h-3.5 w-3.5" />
                        </span>
                        <CheckBox label="显示所有信息" />
                    </>
                }
                right={
                    <>
                        <Btn>新增</Btn>
                        <Btn dropdown>生成凭证</Btn>
                        <Btn dropdown>导入</Btn>
                        <Btn>删除</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                            {["", "收支类别", "出票日", "票据号", "票据金额", "票据种类", "汇票到期日", "出票人", "承兑人", "票据状态", "能否转让", "操作"].map(
                                (t, i) => (
                                    <th
                                        key={i}
                                        className="border-b border-[#e8e8e8] px-3 py-2.5 text-left font-normal"
                                        style={{ width: i === 4 ? "110px" : undefined }}
                                    >
                                        {t}
                                    </th>
                                ),
                            )}
                        </tr>
                    </thead>
                </table>
                {data.bills.length === 0 ? (
                    <EmptyTip />
                ) : (
                    <table className="w-full border-collapse text-sm">
                        <tbody>
                            {data.bills.map((b) => (
                                <tr key={b.id} className="border-b border-[#f0f0f0]">
                                    <td className="px-3 py-2"><CheckBox label="" /></td>
                                    <td className="px-3 py-2.5 text-[13px]">{b.category}</td>
                                    <td className="px-3 py-2.5 text-[13px]">{b.issueDate ? new Date(b.issueDate).toISOString().substring(0, 10) : ""}</td>
                                    <td className="px-3 py-2.5 text-[13px]">{b.billNo}</td>
                                    <td className="px-3 py-2.5 text-right text-[13px]">{(b.amount ?? 0).toFixed(2)}</td>
                                    <td className="px-3 py-2.5 text-[13px]">{b.kindText}</td>
                                    <td className="px-3 py-2.5 text-[13px]">{b.dueDate ? new Date(b.dueDate).toISOString().substring(0, 10) : ""}</td>
                                    <td className="px-3 py-2.5 text-[13px]">{b.drawer}</td>
                                    <td className="px-3 py-2.5 text-[13px]">{b.acceptor}</td>
                                    <td className="px-3 py-2.5 text-[13px]">{b.statusText}</td>
                                    <td className="px-3 py-2.5 text-[13px]">{b.transferable ? "是" : "否"}</td>
                                    <td />
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </div>
            <Pager total={data.bills.length} count={data.bills.length} />
        </Page>
    );
}

/* ---------- 账户设置（现金/银行存款） ---------- */

export function LemonAccountSettings() {
    const [tab, setTab] = useState(0);
    const data = useFundData();
    const cols: Col[] =
        tab === 0
            ? [
                  { key: "code", title: "编码", width: "110px" },
                  { key: "name", title: "账户名称", width: "220px" },
                  { key: "currency", title: "币别", width: "120px" },
                  { key: "subject", title: "会计科目" },
                  { key: "status", title: "启用状态", width: "110px", render: () => <Toggle /> },
                  { key: "ops", title: "操作", width: "160px", render: () => <Ops items={["编辑", "删除"]} /> },
              ]
            : [
                  { key: "code", title: "编码", width: "70px" },
                  { key: "name", title: "账户名称", width: "180px" },
                  { key: "bank", title: "银行", width: "140px" },
                  { key: "bankNo", title: "银行账号", width: "180px" },
                  { key: "currency", title: "币别", width: "90px" },
                  { key: "subject", title: "会计科目", width: "140px" },
                  { key: "bind", title: "绑定银企互联", width: "120px" },
                  { key: "bindDate", title: "绑定日期", width: "100px" },
                  { key: "expire", title: "到期日期", width: "100px" },
                  { key: "status", title: "启用状态", width: "100px", render: () => <Toggle /> },
                  { key: "ops", title: "操作", width: "170px", render: () => <Ops items={["去绑定", "编辑", "删除"]} /> },
              ];
    const accountRows = data.journals.filter((j) => (tab === 0 ? j.type === 1 : j.type === 2));
    const rows: Row[] = accountRows.map((j, i) =>
        tab === 0
            ? { code: String(i + 1).padStart(3, "0"), name: j.name, currency: "人民币", subject: `${j.accountCode} ${j.accountName}` }
            : {
                  code: String(i + 1).padStart(3, "0"),
                  name: j.name,
                  bank: j.name,
                  bankNo: "",
                  currency: "人民币",
                  subject: `${j.accountCode} ${j.accountName}`,
                  bind: "未绑定",
              },
    );
    return (
        <Page>
            <SubTabs tabs={["现金", "银行存款"]} active={tab} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <span className="text-[13px] text-[#666]">输入 编码或名称：</span>
                        <FakeInput className="w-[180px]" />
                        <Btn variant="primary">查询</Btn>
                    </>
                }
                right={
                    <>
                        {tab === 1 && <Btn>预约开户</Btn>}
                        <Btn>导入</Btn>
                        <Btn variant="primary">新增</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                {data.error && <div className="px-3 py-2 text-[13px] text-[#f5222d]">{data.error}</div>}
                <Table cols={cols} rows={rows} empty={<EmptyTip />} />
            </div>
            <Pager total={rows.length} count={rows.length} />
        </Page>
    );
}
