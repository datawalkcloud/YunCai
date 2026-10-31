/**
 * 复刻区资金模块（对应柠檬云 /Cashier/*）：
 * 现金/银行日记账（行内编辑网格 + 生成凭证交互链路）、票据管理（按期间查询 / 新增 / 删除已接后端）、
 * 资金报表（六页签汇总）、核对总账、收支类别（收入/支出 + 新增表单）、账户设置（现金/银行存款，启停用/编辑/删除/新增已接后端）。
 * 视觉遵循全站统一风格（云财蓝）：主色 #2f6bff、白卡 + slate 灰阶文字、收入 #10b981 / 支出与错误 #ef4444。
 */
import { type ReactNode, useCallback, useEffect, useRef, useState } from "react";
import { WalletOutlined } from "@ant-design/icons";
import { Alert, Popconfirm } from "antd";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { JournalLedgerPage } from "@/pages/funds/journal-ledger";
import { BillLedgerPage } from "@/pages/funds/bill-ledger";
import {
    Btn,
    Dialog,
    EmptyTip,
    FakeInput,
    LinkAction,
    Page,
    Pager,
    RefreshIcon,
    Select,
    StateBar,
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
import { JournalsApi, AccountsApi, type JournalAccountImportResultDto, type JournalListItemDto, type AccountDto } from "@/apis-gen";

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

/* ---------- 现金/银行日记账：账页见 @/pages/funds/journal-ledger（与现金日记账共用） ---------- */

/** 银行日记账。journalType=2 是本页的固定语义（2 = 银行存款类日记账）。 */
export function LemonDepositJournal() {
    return <JournalLedgerPage journalType={2} seqPrefix="2-001" />;
}


/* ---------- 票据管理：账页见 @/pages/funds/bill-ledger（对齐柠檬云「票据台账」） ---------- */

/** 票据管理（票据台账）。 */
export function LemonDraft() {
    return <BillLedgerPage />;
}

/* ---------- 账户设置（现金/银行存款，启停用/编辑/删除/新增已接后端） ---------- */

const CURRENCIES = ["人民币", "美元", "欧元", "港币", "日元", "英镑"];

type AccountFormState = {
    code: string;
    name: string;
    currency: string;
    accountId: string;
    openingBalance: string;
    bankName: string;
    bankAccountNo: string;
};

/** 二进制下载：fetch + blob + a[download]，返回错误文案（成功返回 null）。 */
async function fundsDownload(url: string, token: string, fallbackName: string): Promise<string | null> {
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

/** 按账户类型筛选可绑定的末级货币资金科目（1=现金 1001*，2=银行存款 1002*）。 */
function subjectOptionsOf(accounts: AccountDto[], type: number) {
    const prefix = type === 2 ? "1002" : "1001";
    return accounts
        .filter((a) => a.code?.startsWith(prefix))
        .map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` }));
}

/** 表单行：左 80px 右对齐标签 + 右侧自适应控件（本页 Dialog 表单共用）。 */
function LabelRow({ label, children }: { label: string; children: ReactNode }) {
    return (
        <div className="flex items-center gap-3">
            <span className="w-[80px] text-right">{label}</span>
            {children}
        </div>
    );
}

/** 账户新增/编辑共用的表单字段（编码 / 名称 / 币别 / 会计科目 / 期初余额 / 开户行·账号）。 */
function AccountFormFields({
    form,
    setForm,
    accounts,
    type,
    hideBank,
    accountReadOnly,
}: {
    form: AccountFormState;
    setForm: (f: AccountFormState) => void;
    accounts: AccountDto[];
    type: number;
    hideBank?: boolean;
    accountReadOnly?: boolean;
}) {
    const currentSubject = accounts.find((a) => a.id === form.accountId);
    return (
        <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
            <LabelRow label="编码：">
                <TextInput className="flex-1" placeholder="留空自动生成" value={form.code} onChange={(v) => setForm({ ...form, code: v })} />
            </LabelRow>
            <LabelRow label="名称：">
                <TextInput className="flex-1" value={form.name} onChange={(v) => setForm({ ...form, name: v })} />
            </LabelRow>
            <LabelRow label="币别：">
                <Select className="flex-1" value={form.currency} onChange={(v) => setForm({ ...form, currency: v })} options={CURRENCIES} />
            </LabelRow>
            <LabelRow label="会计科目：">
                {accountReadOnly ? (
                    <FakeInput className="flex-1" text={currentSubject ? `${currentSubject.code} ${currentSubject.name}` : "—"} />
                ) : (
                    <Select
                        className="flex-1"
                        value={form.accountId}
                        onChange={(v) => setForm({ ...form, accountId: v })}
                        options={[{ value: "", label: "（不指定，自动建明细科目）" }, ...subjectOptionsOf(accounts, type)]}
                    />
                )}
            </LabelRow>
            <LabelRow label="期初余额：">
                <TextInput
                    className="flex-1"
                    type="number"
                    value={form.openingBalance}
                    onChange={(v) => setForm({ ...form, openingBalance: v.replace(/[^\d.]/g, "") })}
                />
            </LabelRow>
            {!hideBank && (
                <>
                    <LabelRow label="开户行：">
                        <TextInput className="flex-1" value={form.bankName} onChange={(v) => setForm({ ...form, bankName: v })} />
                    </LabelRow>
                    <LabelRow label="银行账号：">
                        <TextInput className="flex-1" value={form.bankAccountNo} onChange={(v) => setForm({ ...form, bankAccountNo: v })} />
                    </LabelRow>
                </>
            )}
        </div>
    );
}


export function LemonAccountSettings() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [tab, setTab] = useState(0);
    const { journals, loading, error, reload } = useJournals();
    const [toast, setToast] = useState<string | null>(null);
    const [keyword, setKeyword] = useState("");
    const [applied, setApplied] = useState("");
    const [editTarget, setEditTarget] = useState<JournalListItemDto | null>(null);
    const [editForm, setEditForm] = useState({ code: "", name: "", currency: "人民币", accountId: "", openingBalance: "", bankName: "", bankAccountNo: "" });
    const [showAdd, setShowAdd] = useState(false);
    const [addForm, setAddForm] = useState({ code: "", name: "", currency: "人民币", accountId: "", openingBalance: "", bankName: "", bankAccountNo: "" });
    const [accounts, setAccounts] = useState<AccountDto[]>([]);
    const accountImportRef = useRef<HTMLInputElement>(null);
    const [importing, setImporting] = useState(false);
    const [importResult, setImportResult] = useState<JournalAccountImportResultDto | null>(null);

    /** 下载资金账户导入模板。 */
    const doImportTemplate = async () => {
        if (!token || !accountSetId) return;
        const err = await fundsDownload(`/api/accountsets/${accountSetId}/journals/accounts/import-template`, token, "资金账户导入模板.xlsx");
        if (err) setToast(err);
    };

    /** 资金账户批量导入：同名同类型更新，否则新建。 */
    const doImportAccounts = async (file: File) => {
        if (!token || !accountSetId) return;
        setImporting(true);
        try {
            const form = new FormData();
            form.append("file", file);
            const res = await fetch(`/api/accountsets/${accountSetId}/journals/accounts/import`, {
                method: "POST",
                headers: { Authorization: `Bearer ${token}` },
                body: form,
            });
            const body = await res.json().catch(() => null);
            if (!res.ok) {
                const message = body && typeof body === "object" && "message" in body ? String((body as { message?: unknown }).message ?? "") : "";
                setToast(message || `导入失败（HTTP ${res.status}）`);
                return;
            }
            setImportResult(body as JournalAccountImportResultDto);
            setToast(`导入完成：新增 ${body.imported ?? 0} 个，更新 ${body.updated ?? 0} 个`);
            await reload();
        } catch {
            setToast("导入失败，请检查文件格式。");
        } finally {
            setImporting(false);
        }
    };

    // 加载末级货币资金科目（1001 现金 / 1002 银行）供「会计科目」下拉选择。
    useEffect(() => {
        if (!accountSetId) return;
        new AccountsApi(apiConfig(token))
            .accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true })
            .then((list) => setAccounts(list.filter((a) => a.code?.startsWith("1001") || a.code?.startsWith("1002"))))
            .catch(() => setAccounts([]));
    }, [accountSetId, token]);

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
            code: j.code ?? "",
            name: j.name ?? "",
            currency: j.currency ?? "人民币",
            accountId: j.accountId ?? "",
            openingBalance: j.openingBalance != null ? String(j.openingBalance) : "",
            bankName: j.bankName ?? "",
            bankAccountNo: j.bankAccountNo ?? "",
        });
    };

    /** 保存编辑（编码/名称/币别/科目/期初/开户行/银行账号）：PUT /journals/{id}。 */
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
                    code: editForm.code.trim() || null,
                    currency: editForm.currency.trim() || null,
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
                    code: addForm.code.trim() || null,
                    currency: addForm.currency.trim() || null,
                    accountId: addForm.accountId ? addForm.accountId : null,
                    openingBalance: Number(addForm.openingBalance) || 0,
                    bankName: tab === 1 ? addForm.bankName.trim() || null : null,
                    bankAccountNo: tab === 1 ? addForm.bankAccountNo.trim() || null : null,
                },
            });
            setShowAdd(false);
            setAddForm({ code: "", name: "", currency: "人民币", accountId: "", openingBalance: "", bankName: "", bankAccountNo: "" });
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
    const rows: Row[] = accountRows.map((j) =>
        tab === 0
            ? {
                  __j: j,
                  code: j.code || "",
                  name: j.name,
                  currency: j.currency || "人民币",
                  opening: j.openingBalance != null ? String(j.openingBalance) : "0",
                  subject: `${j.accountCode ?? ""} ${j.accountName ?? ""}`.trim(),
              }
            : {
                  __j: j,
                  code: j.code || "",
                  name: j.name,
                  bank: j.bankName ?? "",
                  bankNo: j.bankAccountNo ?? "",
                  currency: j.currency || "人民币",
                  opening: j.openingBalance != null ? String(j.openingBalance) : "0",
                  subject: `${j.accountCode ?? ""} ${j.accountName ?? ""}`.trim(),
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
                    <LinkAction onClick={() => openEdit(j)}>编辑</LinkAction>
                    <Popconfirm
                        title={`确定删除账户「${j.name}」吗？`}
                        description="删除后该账户的期初余额与既有流水将一并清除，且不可恢复。"
                        okText="确定"
                        cancelText="取消"
                        onConfirm={() => void removeJournal(j)}
                    >
                        <LinkAction danger>删除</LinkAction>
                    </Popconfirm>
                </span>
            );
        },
    };
    const cols: Col[] =
        tab === 0
            ? [
                  { key: "code", title: "编码", width: "90px" },
                  { key: "name", title: "账户名称", width: "200px" },
                  { key: "currency", title: "币别", width: "110px" },
                  { key: "subject", title: "会计科目", width: "200px" },
                  statusCol,
                  opsCol,
              ]
            : [
                  { key: "code", title: "编码", width: "70px" },
                  { key: "name", title: "账户名称", width: "180px" },
                  { key: "bank", title: "开户行", width: "150px" },
                  { key: "bankNo", title: "银行账号", width: "170px" },
                  { key: "currency", title: "币别", width: "90px" },
                  { key: "subject", title: "会计科目", width: "170px" },
                  statusCol,
                  opsCol,
              ];
    return (
        <Page title="账户设置" subtitle="现金与银行存款资金账户的启停用、编辑与新增" icon={WalletOutlined}>
            <SubTabs tabs={["现金", "银行存款"]} active={tab} onChange={setTab} />
            {/* 银企互联属外部银行对接能力，不在账套复刻范围内；账户核心管理（编码/币别/期初/科目/启停用/增删改/查询）已全部接真实数据。 */}
            <WipNotice text="银企互联（绑定银行自动获取流水）为外部银行对接能力，未纳入账套复刻；账户设置的核心功能（编码 / 币别 / 期初余额 / 会计科目 / 启用停用 / 新增编辑删除 / 查询 / 模板下载与批量导入）均已接真实数据。" />
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
                        <Btn onClick={() => void doImportTemplate()}>模板</Btn>
                        <Btn onClick={() => accountImportRef.current?.click()} disabled={importing}>{importing ? "导入中…" : "导入"}</Btn>
                        <input
                            ref={accountImportRef}
                            type="file"
                            accept=".xlsx"
                            className="hidden"
                            onChange={(e) => {
                                const f = e.target.files?.[0];
                                e.target.value = "";
                                if (f) void doImportAccounts(f);
                            }}
                        />
                        <Btn variant="primary" onClick={() => setShowAdd(true)}>新增</Btn>
                        <RefreshIcon onClick={() => void reload()} />
                    </>
                }
            />
            <StateBar loading={loading} error={error} />
            <div className="flex-1 px-4">
                {importResult && (importResult.failures ?? []).length > 0 && (
                    <div className="mb-2">
                        <Alert
                            type="error"
                            showIcon
                            title={`有 ${importResult.failures?.length} 行未导入`}
                            description={
                                <div className="text-[12.5px] leading-5">
                                    {(importResult.failures ?? []).slice(0, 8).map((f, i) => (
                                        <div key={i}>· 第 {f.row} 行「{f.name || "—"}」：{f.reason}</div>
                                    ))}
                                </div>
                            }
                        />
                    </div>
                )}
                <Table cols={cols} rows={rows} empty={<EmptyTip />} />
            </div>
            <Pager total={rows.length} />
            {/* 编辑账户弹窗 */}
            {editTarget && (
                <Dialog
                    title={`编辑账户（${editTarget.type === 2 ? "银行存款" : "现金"}）`}
                    width={520}
                    onClose={() => setEditTarget(null)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void saveEdit()}>保存</Btn>
                            <Btn onClick={() => setEditTarget(null)}>取消</Btn>
                        </div>
                    }
                >
                    <AccountFormFields form={editForm} setForm={setEditForm} accounts={accounts} type={editTarget.type ?? 1} hideBank={editTarget.type !== 2} accountReadOnly />
                </Dialog>
            )}
            {/* 新增账户弹窗 */}
            {showAdd && (
                <Dialog
                    title={`新增账户（${tab === 0 ? "现金" : "银行存款"}）`}
                    width={520}
                    onClose={() => setShowAdd(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void createAccount()}>保存</Btn>
                            <Btn onClick={() => setShowAdd(false)}>取消</Btn>
                        </div>
                    }
                >
                    <AccountFormFields form={addForm} setForm={setAddForm} accounts={accounts} type={tab === 0 ? 1 : 2} hideBank={tab !== 1} />
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
