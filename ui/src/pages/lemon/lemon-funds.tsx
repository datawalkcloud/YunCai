/**
 * 复刻区资金模块（对应柠檬云 /Cashier/*）：
 * 现金/银行日记账（行内编辑网格 + 生成凭证交互链路）、票据管理（按期间查询 / 新增 / 删除已接后端）、
 * 资金报表（六页签汇总）、核对总账、收支类别（收入/支出 + 新增表单）、账户设置（现金/银行存款，启停用/编辑/删除/新增已接后端）。
 * 视觉遵循全站统一风格（云财蓝）：主色 #2f6bff、白卡 + slate 灰阶文字、收入 #10b981 / 支出与错误 #ef4444。
 */
import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import {
    DownOutlined,
    InfoCircleOutlined,
    BankOutlined,
    MoneyCollectOutlined,
    ProfileOutlined,
    SettingOutlined,
    WalletOutlined,
} from "@ant-design/icons";
import { Input } from "antd";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { shiftPeriod, useDefaultAccountingPeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { ICON_STYLE } from "@/components/page/styles";
import { AccountsApi } from "@/apis-gen";


import {
    Btn,
    CheckBox,
    DateInput,
    Dialog,
    EmptyTip,
    Page,
    Pager,
    PeriodBlock,
    PeriodStepper,
    RefreshIcon,
    Select,
    SplitBtn,
    SubTabs,
    Table,
    TextInput,
    Toast,
    Toggle,
    ToolRow,
    WipBtn,
    WipNotice,
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { BillsApi, JournalsApi, type AccountDto, type BillDto, type JournalDetailDto, type JournalListItemDto } from "@/apis-gen";

/** 后端日期字段运行时是 Date，统一按 yyyy-MM-dd 展示。 */
const fmtDate = (v: Date | string | null | undefined): string =>
    v ? new Date(v).toISOString().substring(0, 10) : "";

/** 资金账户（日记账）列表数据 Hook（供账户设置页使用）。 */
function useJournals() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [journals, setJournals] = useState<JournalListItemDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
        try {
            setJournals(await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsGet({ accountSetId }));
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载资金账户失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void reload();
    }, [reload]);

    return { journals, loading, error, reload };
}

/* ---------- 现金/银行日记账（已接后端） ---------- */

/** 行内编辑单元格控件尺寸：antd 的 .ant-input 是无层级声明，Tailwind 的 h-/text- 压不过，只能内联。 */
const jpEdit = { height: 28, fontSize: 13 } as const;

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
    const [loading, setLoading] = useState(true);
    const [tick, setTick] = useState(0);
    const [showAdd, setShowAdd] = useState(false);
    const [accounts, setAccounts] = useState<AccountDto[]>([]);
    const [addForm, setAddForm] = useState({ name: "", type: 1, accountId: "", openingBalance: "", bankName: "", bankAccountNo: "" });
    const importRef = useRef<HTMLInputElement>(null);

    const journal = journals.find((j) => j.id === journalId) ?? null;
    const reload = useCallback(async () => {
        if (!token || !accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
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
        } finally {
            setLoading(false);
        }
    }, [token, accountSetId, journalType, journalId]);
    useEffect(() => {
        void reload();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [token, accountSetId, journalType, tick]);

    const nf = (v: number | null | undefined): string =>
        v == null ? "" : v.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    const switchJournal = async (id: string) => {
        setJournalId(id);
        setSelected(new Set());
        if (token && accountSetId && id) {
            try {
                setDetail(await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdGet({ accountSetId, id }));
            } catch (e) {
                setError(await apiErrorMessage(e, "加载日记账明细失败。"));
            }
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
                    操作 <SettingOutlined className="text-[12px]" style={ICON_STYLE.muted} />
                </span>
            ),
            width: "230px",
        },
    ];
    return (
        <Page title={bank ? "银行日记账" : "现金日记账"} icon={bank ? BankOutlined : MoneyCollectOutlined}>
            <ToolRow
                left={
                    <>
                        <Select
                            className="w-[220px]"
                            value={journalId}
                            onChange={(v) => void switchJournal(v)}
                            options={
                                journals.length === 0
                                    ? [{ value: "", label: "（暂无日记账）" }]
                                    : journals.map((j) => ({ value: j.id ?? "", label: j.name ?? "" }))
                            }
                        />
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
                            <span className="inline-flex h-8 items-center gap-1 rounded-lg border border-[#2f6bff]/40 bg-[#eef4ff] px-3 text-sm text-[#2f6bff]">
                                银企互联 <InfoCircleOutlined className="text-[14px]" />
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
                        <WipBtn dropdown title="批量操作建设中，后端未提供端点">批量操作</WipBtn>
                        <WipBtn dropdown title="关联凭证查看建设中，后端未提供端点">关联凭证</WipBtn>
                        <SplitBtn>
                            <span onClick={() => void genVoucher()}>生成凭证</span>
                        </SplitBtn>
                        <span className="cursor-pointer" onClick={() => setTick((t) => t + 1)}>
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#ef4444]">{error}</div>}
            {loading && <div className="px-4 pb-2 text-[13px] text-slate-500">加载中…</div>}
            <div className="flex-1 px-4">
                <table className="w-full border-collapse text-sm tabular-nums">
                    <thead>
                        <tr className="bg-muted text-[13px] text-slate-700">
                            {cols.map((c) => (
                                <th
                                    key={c.key}
                                    className={`border-b border-slate-200 px-3 py-2.5 font-normal ${c.align === "right" ? "text-right" : "text-left"}`}
                                    style={{ width: c.width }}
                                >
                                    {c.title}
                                    {["summary", "ieType", "partner"].includes(String(c.key)) && (
                                        <DownOutlined className="ml-1 text-[12px]" style={ICON_STYLE.muted} />
                                    )}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {/* 期初行 */}
                        <tr className="border-b border-slate-100">
                            <td />
                            <td />
                            <td className="px-3 py-2 text-[13px]">初始化余额</td>
                            <td />
                            <td />
                            <td />
                            <td />
                            <td className="px-2 py-1.5">
                                <span className="block h-7 rounded-lg border border-slate-200 px-2 text-right text-[13px] leading-7 text-slate-700">
                                    {nf(detail?.openingBalance)}
                                </span>
                            </td>
                            <td />
                            <td />
                            <td />
                        </tr>
                        {/* 已有流水行（可勾选） */}
                        {entries.map((e) => (
                            <tr key={e.id} className={`border-b border-slate-100 ${selected.has(e.id ?? "") ? "bg-[#eef4ff]" : ""}`}>
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
                                <td className="px-3 py-2 text-right text-[13px] text-[#10b981]">{nf(e.income)}</td>
                                <td className="px-3 py-2 text-right text-[13px] text-[#ef4444]">{nf(e.expense)}</td>
                                <td className="px-3 py-2 text-right text-[13px]">{nf(e.balance)}</td>
                                <td className="px-3 py-2 text-[13px]">{e.voucherNo ?? ""}</td>
                                <td className="px-3 py-2 text-[13px]">
                                    {`${seqPrefix}-${fmtDate(e.date).replaceAll("-", "")}-${e.sequence ?? ""}`}
                                </td>
                                <td className="px-3 py-2">
                                    <span className="flex items-center gap-1 whitespace-nowrap text-[13px] text-[#2f6bff]">
                                        {/* 插入行/附件/复制后端未提供端点，先置灰防误操作 */}
                                        <Btn disabled className="!h-6 !px-1.5 text-[12px]" title="插入行建设中，后端未提供端点">插入</Btn>
                                        <span className="cursor-pointer hover:underline" onClick={() => void deleteEntry(e.id ?? "")}>
                                            删除
                                        </span>
                                        <span className="cursor-pointer hover:underline" onClick={() => window.print()}>打印</span>
                                        <Btn disabled className="!h-6 !px-1.5 text-[12px]" title="附件管理建设中，后端未提供端点">附件</Btn>
                                        <Btn disabled className="!h-6 !px-1.5 text-[12px]" title="复制流水建设中，后端未提供端点">复制</Btn>
                                    </span>
                                </td>
                            </tr>
                        ))}
                        {/* 行内新增编辑行 */}
                        <tr className="border-b border-slate-100">
                            <td />
                            <td className="px-2 py-1.5">
                                <DateInput className="w-full" value={draft.date} onChange={(v) => setDraft({ ...draft, date: v })} />
                            </td>
                            <td className="px-2 py-1.5">
                                <Input
                                    className="w-full"
                                    style={{ ...jpEdit, borderColor: "#2f6bff" }}
                                    value={draft.summary}
                                    onChange={(ev) => setDraft({ ...draft, summary: ev.target.value })}
                                />
                            </td>
                            <td className="px-2 py-1.5">
                                <Input
                                    className="w-full"
                                    style={jpEdit}
                                    value={draft.category}
                                    onChange={(ev) => setDraft({ ...draft, category: ev.target.value })}
                                />
                            </td>
                            <td className="px-2 py-1.5">
                                <Input
                                    className="w-full"
                                    style={jpEdit}
                                    value={draft.counterparty}
                                    onChange={(ev) => setDraft({ ...draft, counterparty: ev.target.value })}
                                />
                            </td>
                            <td className="px-2 py-1.5">
                                <Input
                                    className="w-full"
                                    style={{ ...jpEdit, textAlign: "right" }}
                                    value={draft.income}
                                    onChange={(ev) => setDraft({ ...draft, income: ev.target.value.replace(/[^\d.]/g, "") })}
                                />
                            </td>
                            <td className="px-2 py-1.5">
                                <Input
                                    className="w-full"
                                    style={{ ...jpEdit, textAlign: "right" }}
                                    value={draft.expense}
                                    onChange={(ev) => setDraft({ ...draft, expense: ev.target.value.replace(/[^\d.]/g, "") })}
                                />
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
                        <tr className="border-b border-slate-100 bg-[#fafbfd]">
                            <td />
                            <td />
                            <td className="px-3 py-2 text-[13px] font-bold text-slate-800">合计</td>
                            <td />
                            <td />
                            <td className="px-3 py-2 text-right text-[13px] text-[#10b981]">{nf(detail?.totalIncome)}</td>
                            <td className="px-3 py-2 text-right text-[13px] text-[#ef4444]">{nf(detail?.totalExpense)}</td>
                            <td className="px-3 py-2 text-right text-[13px] text-slate-700">{nf(detail?.closingBalance)}</td>
                            <td colSpan={3} />
                        </tr>
                        {entries.length === 0 && !loading && !error && (
                            <tr>
                                <td colSpan={cols.length} className="py-10 text-center text-[13px] text-slate-400">
                                    本账户暂无流水，请在下方行内登记。
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>
            {/* 真实数据不足一页（20 条）时隐藏分页控件，不显示假页码 */}
            {entries.length > 20 && <Pager total={entries.length} count={1} />}
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
                    <div className="text-center text-sm leading-6 text-slate-700">
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
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        <label className="flex items-center gap-3">
                            <span className="w-[90px] text-right">账簿类型：</span>
                            <Select
                                className="w-[220px]"
                                value={String(addForm.type)}
                                onChange={(v) => setAddForm({ ...addForm, type: Number(v) })}
                                options={[
                                    { value: "1", label: "现金日记账" },
                                    { value: "2", label: "银行存款日记账" },
                                ]}
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[90px] text-right">名称：</span>
                            <TextInput className="w-[220px]" value={addForm.name} onChange={(v) => setAddForm({ ...addForm, name: v })} />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[90px] text-right">关联科目：</span>
                            <Select
                                className="w-[220px]"
                                value={addForm.accountId}
                                onChange={(v) => setAddForm({ ...addForm, accountId: v })}
                                options={[
                                    { value: "", label: "（暂不关联）" },
                                    ...(accounts ?? []).map((a) => ({ value: a.id ?? "", label: `${a.code ?? ""} ${a.name ?? ""}` })),
                                ]}
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[90px] text-right">期初余额：</span>
                            <TextInput
                                className="w-[220px] text-right"
                                value={addForm.openingBalance}
                                onChange={(v) => setAddForm({ ...addForm, openingBalance: v.replace(/[^\d.]/g, "") })}
                            />
                        </label>
                        {addForm.type === 2 && (
                            <>
                                <label className="flex items-center gap-3">
                                    <span className="w-[90px] text-right">开户行：</span>
                                    <TextInput className="w-[220px]" value={addForm.bankName} onChange={(v) => setAddForm({ ...addForm, bankName: v })} />
                                </label>
                                <label className="flex items-center gap-3">
                                    <span className="w-[90px] text-right">银行账号：</span>
                                    <TextInput className="w-[220px]" value={addForm.bankAccountNo} onChange={(v) => setAddForm({ ...addForm, bankAccountNo: v })} />
                                </label>
                            </>
                        )}
                    </div>
                </Dialog>
            )}
        </Page>
    );
}

/**
 * 银行日记账。journalType=2 是本页的固定语义：
 * 2 = 银行存款类日记账（后端 JournalType），本页只看银行账户，现金日记账由独立入口以 type=1 渲染。
 */
export function LemonDepositJournal() {
    return <JournalPage journalType={2} seqPrefix="2-001" bank />;
}
/* ---------- 票据管理（期间查询 / 新增 / 删除已接后端） ---------- */

/** 票据种类，与后端 BillKind 枚举一致。 */
const BILL_KIND_OPTIONS = [
    { value: "1", label: "银行承兑汇票" },
    { value: "2", label: "商业承兑汇票" },
    { value: "3", label: "支票" },
    { value: "4", label: "其他" },
];

const emptyBillForm = { category: "", issueDate: "", billNo: "", amount: "", kind: "1", dueDate: "", drawer: "", acceptor: "", transferable: true };

export function LemonDraft() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const defaultPeriod = useDefaultAccountingPeriod();
    const [userPeriod, setUserPeriod] = useState<AccountingPeriod | null>(null);
    const period = userPeriod ?? defaultPeriod;
    const [bills, setBills] = useState<BillDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [toast, setToast] = useState<string | null>(null);
    const [showAdd, setShowAdd] = useState(false);
    const [form, setForm] = useState(emptyBillForm);
    const [busy, setBusy] = useState(false);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
        try {
            setBills(
                await new BillsApi(apiConfig(token)).accountsetsAccountSetIdBillsGet({
                    accountSetId,
                    startDate: new Date(period.startDate),
                    endDate: new Date(period.endDate),
                }),
            );
            setSelected(new Set());
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载票据失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, period]);

    useEffect(() => {
        void reload();
    }, [reload]);

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

    /** 新增票据：走 POST /accountsets/{id}/bills。 */
    const createBill = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (!form.billNo.trim() || !form.issueDate) {
            setToast("票据号与出票日必填");
            return;
        }
        const amount = Number(form.amount);
        if (!(amount > 0)) {
            setToast("请填写正确的票据金额");
            return;
        }
        setBusy(true);
        try {
            await new BillsApi(apiConfig(token)).accountsetsAccountSetIdBillsPost({
                accountSetId,
                saveBillRequest: {
                    category: form.category.trim(),
                    issueDate: new Date(form.issueDate),
                    billNo: form.billNo.trim(),
                    amount,
                    kind: Number(form.kind),
                    dueDate: form.dueDate ? new Date(form.dueDate) : null,
                    drawer: form.drawer.trim() || null,
                    acceptor: form.acceptor.trim() || null,
                    transferable: form.transferable,
                },
            });
            setShowAdd(false);
            setForm(emptyBillForm);
            setToast("票据已新增");
            await reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "新增票据失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 删除票据：后端仅提供单条 DELETE 端点，勾选行逐条调用。 */
    const deleteBills = async (ids: string[]) => {
        if (!token || !accountSetId || busy || ids.length === 0) {
            if (ids.length === 0) {
                setToast("请先勾选要删除的票据");
            }
            return;
        }
        setBusy(true);
        const api = new BillsApi(apiConfig(token));
        let failMsg = "";
        for (const id of ids) {
            try {
                await api.accountsetsAccountSetIdBillsIdDelete({ accountSetId, id });
            } catch (e) {
                failMsg = await apiErrorMessage(e, "删除失败");
            }
        }
        setBusy(false);
        setToast(failMsg || "票据已删除");
        await reload();
    };

    return (
        <Page title="票据管理" subtitle="按期间登记与查询应收票据" icon={ProfileOutlined}>
            <SubTabs tabs={["票据台账"]} />
            <ToolRow
                left={
                    <>
                        <PeriodStepper
                            text={`${period.year}年${period.month}月`}
                            onPrev={() => setUserPeriod(shiftPeriod(period, -1))}
                            onNext={() => setUserPeriod(shiftPeriod(period, 1))}
                        />
                        <span className="flex items-center gap-1 text-[13px] text-[#2f6bff]">
                            如何管理票据？ <InfoCircleOutlined className="text-[14px]" />
                        </span>
                        <CheckBox label="显示所有信息" />
                    </>
                }
                right={
                    <>
                        <Btn onClick={() => setShowAdd(true)}>新增</Btn>
                        <WipBtn dropdown title="后端未提供票据生成凭证端点，建设中">生成凭证</WipBtn>
                        <WipBtn dropdown title="后端未提供票据导入端点，建设中">导入</WipBtn>
                        <Btn disabled={busy || selected.size === 0} title="删除勾选的票据" onClick={() => void deleteBills([...selected])}>删除</Btn>
                        <span className="cursor-pointer" onClick={() => void reload()}>
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            {error && <div className="px-4 pb-2 text-[13px] text-[#ef4444]">{error}</div>}
            {loading && <div className="px-4 pb-2 text-[13px] text-slate-500">加载中…</div>}
            <div className="flex-1 px-4">
                <table className="w-full border-collapse text-sm tabular-nums">
                    <thead>
                        <tr className="bg-muted text-[13px] text-slate-700">
                            {["", "收支类别", "出票日", "票据号", "票据金额", "票据种类", "汇票到期日", "出票人", "承兑人", "票据状态", "能否转让", "操作"].map(
                                (t, i) => (
                                    <th
                                        key={i}
                                        className={`border-b border-slate-200 px-3 py-2.5 font-normal ${i === 4 ? "text-right" : "text-left"}`}
                                        style={{ width: i === 4 ? "110px" : undefined }}
                                    >
                                        {t}
                                    </th>
                                ),
                            )}
                        </tr>
                    </thead>
                    <tbody>
                        {bills.map((b) => (
                            <tr key={b.id} className={`border-b border-slate-100 ${selected.has(b.id ?? "") ? "bg-[#eef4ff]" : ""}`}>
                                <td className="px-3 py-2">
                                    <CheckBox label="" checked={selected.has(b.id ?? "")} onChange={() => toggleSel(b.id ?? "")} />
                                </td>
                                <td className="px-3 py-2.5 text-[13px]">{b.category}</td>
                                <td className="px-3 py-2.5 text-[13px]">{fmtDate(b.issueDate)}</td>
                                <td className="px-3 py-2.5 text-[13px]">{b.billNo}</td>
                                <td className="px-3 py-2.5 text-right text-[13px] text-slate-700">{(b.amount ?? 0).toFixed(2)}</td>
                                <td className="px-3 py-2.5 text-[13px]">{b.kindText}</td>
                                <td className="px-3 py-2.5 text-[13px]">{fmtDate(b.dueDate)}</td>
                                <td className="px-3 py-2.5 text-[13px]">{b.drawer}</td>
                                <td className="px-3 py-2.5 text-[13px]">{b.acceptor}</td>
                                <td className="px-3 py-2.5 text-[13px]">{b.statusText}</td>
                                <td className="px-3 py-2.5 text-[13px]">{b.transferable ? "是" : "否"}</td>
                                <td className="px-3 py-2.5">
                                    <span
                                        className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline"
                                        onClick={() => void deleteBills([b.id ?? ""])}
                                    >
                                        删除
                                    </span>
                                </td>
                            </tr>
                        ))}
                        {bills.length === 0 && !loading && (
                            <tr>
                                <td colSpan={12}>
                                    <EmptyTip />
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>
            {/* 真实数据不足一页时隐藏分页控件，不显示假页码 */}
            {bills.length > 20 && <Pager total={bills.length} count={1} />}
            {/* 新增票据弹窗 */}
            {showAdd && (
                <Dialog
                    title="新增票据"
                    width={520}
                    onClose={() => setShowAdd(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" disabled={busy} onClick={() => void createBill()}>
                                保存
                            </Btn>
                            <Btn onClick={() => setShowAdd(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        <div className="flex items-center gap-3">
                            <span className="w-[90px] text-right">票据号：</span>
                            <TextInput className="flex-1" value={form.billNo} onChange={(v) => setForm({ ...form, billNo: v })} placeholder="必填" />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[90px] text-right">票据金额：</span>
                            <TextInput className="flex-1" type="number" value={form.amount} onChange={(v) => setForm({ ...form, amount: v.replace(/[^\d.]/g, "") })} placeholder="必填" />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[90px] text-right">票据种类：</span>
                            <Select className="flex-1" value={form.kind} onChange={(v) => setForm({ ...form, kind: v })} options={BILL_KIND_OPTIONS} />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[90px] text-right">出票日：</span>
                            <DateInput className="flex-1" value={form.issueDate} onChange={(v) => setForm({ ...form, issueDate: v })} />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[90px] text-right">汇票到期日：</span>
                            <DateInput className="flex-1" value={form.dueDate} onChange={(v) => setForm({ ...form, dueDate: v })} />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[90px] text-right">收支类别：</span>
                            <TextInput className="flex-1" value={form.category} onChange={(v) => setForm({ ...form, category: v })} />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[90px] text-right">出票人：</span>
                            <TextInput className="flex-1" value={form.drawer} onChange={(v) => setForm({ ...form, drawer: v })} />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[90px] text-right">承兑人：</span>
                            <TextInput className="flex-1" value={form.acceptor} onChange={(v) => setForm({ ...form, acceptor: v })} />
                        </div>
                        <div className="flex items-center gap-3 pl-[102px]">
                            <CheckBox label="能否转让" checked={form.transferable} onChange={(v) => setForm({ ...form, transferable: v })} />
                        </div>
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 账户设置（现金/银行存款，启停用/编辑/删除/新增已接后端） ---------- */

export function LemonAccountSettings() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [tab, setTab] = useState(0);
    const { journals, loading, error, reload } = useJournals();
    const [toast, setToast] = useState<string | null>(null);
    const [keyword, setKeyword] = useState("");
    const [applied, setApplied] = useState("");
    const [editTarget, setEditTarget] = useState<JournalListItemDto | null>(null);
    const [editForm, setEditForm] = useState({ name: "", openingBalance: "", bankName: "", bankAccountNo: "" });
    const [showAdd, setShowAdd] = useState(false);
    const [addForm, setAddForm] = useState({ name: "", openingBalance: "", bankName: "", bankAccountNo: "" });

    const asJournal = (row: Record<string, unknown>) => row.__j as JournalListItemDto;

    /** 启用 / 停用：PATCH /journals/{id}/enabled。 */
    const toggleEnabled = async (j: JournalListItemDto, enabled: boolean) => {
        if (!token || !accountSetId) return;
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEnabledPatch({
                accountSetId,
                id: j.id ?? "",
                setJournalEnabledRequest: { enabled },
            });
            setToast(enabled ? "账户已启用" : "账户已停用（仅拦截新流水）");
            await reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "操作失败"));
        }
    };

    /** 删除资金账户：DELETE /journals/{id}。 */
    const removeJournal = async (j: JournalListItemDto) => {
        if (!token || !accountSetId) return;
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdDelete({ accountSetId, id: j.id ?? "" });
            setToast("账户已删除");
            await reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "删除失败"));
        }
    };

    const openEdit = (j: JournalListItemDto) => {
        setEditTarget(j);
        setEditForm({
            name: j.name ?? "",
            openingBalance: j.openingBalance != null ? String(j.openingBalance) : "",
            bankName: j.bankName ?? "",
            bankAccountNo: j.bankAccountNo ?? "",
        });
    };

    /** 保存编辑（名称/期初/开户行/银行账号）：PUT /journals/{id}。 */
    const saveEdit = async () => {
        if (!token || !accountSetId || !editTarget) return;
        if (!editForm.name.trim()) {
            setToast("请填写账户名称");
            return;
        }
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdPut({
                accountSetId,
                id: editTarget.id ?? "",
                updateJournalRequest: {
                    name: editForm.name.trim(),
                    openingBalance: Number(editForm.openingBalance) || 0,
                    bankName: editTarget.type === 2 ? editForm.bankName.trim() || null : null,
                    bankAccountNo: editTarget.type === 2 ? editForm.bankAccountNo.trim() || null : null,
                },
            });
            setEditTarget(null);
            setToast("账户已更新");
            await reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "更新失败"));
        }
    };

    /** 新增账户：POST /journals，类型跟随当前页签（现金=1 / 银行=2）。 */
    const createAccount = async () => {
        if (!token || !accountSetId) return;
        if (!addForm.name.trim()) {
            setToast("请填写账户名称");
            return;
        }
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsPost({
                accountSetId,
                createJournalRequest: {
                    name: addForm.name.trim(),
                    type: tab === 0 ? 1 : 2,
                    accountId: null,
                    openingBalance: Number(addForm.openingBalance) || 0,
                    bankName: tab === 1 ? addForm.bankName.trim() || null : null,
                    bankAccountNo: tab === 1 ? addForm.bankAccountNo.trim() || null : null,
                },
            });
            setShowAdd(false);
            setAddForm({ name: "", openingBalance: "", bankName: "", bankAccountNo: "" });
            setToast("账户已新增");
            await reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "新增失败"));
        }
    };

    const kw = applied.trim();
    const accountRows = journals
        .filter((j) => (tab === 0 ? j.type === 1 : j.type === 2))
        .filter((j) => !kw || (j.name ?? "").includes(kw) || (j.accountCode ?? "").includes(kw) || (j.accountName ?? "").includes(kw));
    const rows: Row[] = accountRows.map((j, i) =>
        tab === 0
            ? { __j: j, code: String(i + 1).padStart(3, "0"), name: j.name, currency: "人民币", subject: `${j.accountCode ?? ""} ${j.accountName ?? ""}` }
            : {
                  __j: j,
                  code: String(i + 1).padStart(3, "0"),
                  name: j.name,
                  bank: j.bankName ?? "",
                  bankNo: j.bankAccountNo ?? "",
                  currency: "人民币",
                  subject: `${j.accountCode ?? ""} ${j.accountName ?? ""}`,
              },
    );

    const statusCol: Col = {
        key: "status",
        title: "启用状态",
        width: "110px",
        render: (row) => {
            const j = asJournal(row);
            return <Toggle on={j.isEnabled !== false} onClick={() => void toggleEnabled(j, j.isEnabled === false)} />;
        },
    };
    const opsCol: Col = {
        key: "ops",
        title: "操作",
        width: "150px",
        render: (row) => {
            const j = asJournal(row);
            return (
                <span className="flex items-center gap-2 whitespace-nowrap">
                    <span className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline" onClick={() => openEdit(j)}>编辑</span>
                    <span className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline" onClick={() => void removeJournal(j)}>删除</span>
                </span>
            );
        },
    };
    const cols: Col[] =
        tab === 0
            ? [
                  { key: "code", title: "编码", width: "110px" },
                  { key: "name", title: "账户名称", width: "220px" },
                  { key: "currency", title: "币别", width: "120px" },
                  { key: "subject", title: "会计科目" },
                  statusCol,
                  opsCol,
              ]
            : [
                  { key: "code", title: "编码", width: "70px" },
                  { key: "name", title: "账户名称", width: "180px" },
                  { key: "bank", title: "开户行", width: "160px" },
                  { key: "bankNo", title: "银行账号", width: "180px" },
                  { key: "currency", title: "币别", width: "90px" },
                  { key: "subject", title: "会计科目", width: "140px" },
                  statusCol,
                  opsCol,
              ];
    return (
        <Page title="账户设置" subtitle="现金与银行存款资金账户的启停用、编辑与新增" icon={WalletOutlined}>
            <SubTabs tabs={["现金", "银行存款"]} active={tab} onChange={setTab} />
            {/* 银企互联列已移除：后端未提供绑定端点，功能建设中 */}
            <WipNotice text="银企互联（绑定银行 / 预约开户）建设中，后端未提供端点，列表暂不展示绑定相关列；启停用、编辑、删除与新增均已接真实数据。" />
            <ToolRow
                left={
                    <>
                        <span className="text-[13px] text-slate-500">输入 编码或名称：</span>
                        <TextInput value={keyword} onChange={setKeyword} placeholder="编码 / 名称" className="w-[180px]" />
                        <Btn variant="primary" onClick={() => setApplied(keyword)}>查询</Btn>
                    </>
                }
                right={
                    <>
                        {tab === 1 && <WipBtn title="预约开户建设中，后端未提供端点">预约开户</WipBtn>}
                        <WipBtn title="账户批量导入建设中，后端未提供端点">导入</WipBtn>
                        <Btn variant="primary" onClick={() => setShowAdd(true)}>新增</Btn>
                        <span className="cursor-pointer" onClick={() => void reload()}>
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <div className="flex-1 px-4">
                {error && <div className="px-3 py-2 text-[13px] text-[#ef4444]">{error}</div>}
                {loading && <div className="px-3 py-2 text-[13px] text-slate-500">加载中…</div>}
                <Table cols={cols} rows={rows} empty={<EmptyTip />} />
            </div>
            {/* 真实数据不足一页时隐藏分页控件，不显示假页码 */}
            {rows.length > 20 && <Pager total={rows.length} count={1} />}
            {/* 编辑账户弹窗 */}
            {editTarget && (
                <Dialog
                    title={`编辑账户（${editTarget.type === 2 ? "银行存款" : "现金"}）`}
                    width={460}
                    onClose={() => setEditTarget(null)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void saveEdit()}>保存</Btn>
                            <Btn onClick={() => setEditTarget(null)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">名称：</span>
                            <TextInput className="flex-1" value={editForm.name} onChange={(v) => setEditForm({ ...editForm, name: v })} />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">期初余额：</span>
                            <TextInput className="flex-1" type="number" value={editForm.openingBalance} onChange={(v) => setEditForm({ ...editForm, openingBalance: v.replace(/[^\d.]/g, "") })} />
                        </div>
                        {editTarget.type === 2 && (
                            <>
                                <div className="flex items-center gap-3">
                                    <span className="w-[80px] text-right">开户行：</span>
                                    <TextInput className="flex-1" value={editForm.bankName} onChange={(v) => setEditForm({ ...editForm, bankName: v })} />
                                </div>
                                <div className="flex items-center gap-3">
                                    <span className="w-[80px] text-right">银行账号：</span>
                                    <TextInput className="flex-1" value={editForm.bankAccountNo} onChange={(v) => setEditForm({ ...editForm, bankAccountNo: v })} />
                                </div>
                            </>
                        )}
                    </div>
                </Dialog>
            )}
            {/* 新增账户弹窗 */}
            {showAdd && (
                <Dialog
                    title={`新增账户（${tab === 0 ? "现金" : "银行存款"}）`}
                    width={460}
                    onClose={() => setShowAdd(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void createAccount()}>保存</Btn>
                            <Btn onClick={() => setShowAdd(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">名称：</span>
                            <TextInput className="flex-1" value={addForm.name} onChange={(v) => setAddForm({ ...addForm, name: v })} />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">期初余额：</span>
                            <TextInput className="flex-1" type="number" value={addForm.openingBalance} onChange={(v) => setAddForm({ ...addForm, openingBalance: v.replace(/[^\d.]/g, "") })} />
                        </div>
                        {tab === 1 && (
                            <>
                                <div className="flex items-center gap-3">
                                    <span className="w-[80px] text-right">开户行：</span>
                                    <TextInput className="flex-1" value={addForm.bankName} onChange={(v) => setAddForm({ ...addForm, bankName: v })} />
                                </div>
                                <div className="flex items-center gap-3">
                                    <span className="w-[80px] text-right">银行账号：</span>
                                    <TextInput className="flex-1" value={addForm.bankAccountNo} onChange={(v) => setAddForm({ ...addForm, bankAccountNo: v })} />
                                </div>
                            </>
                        )}
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
