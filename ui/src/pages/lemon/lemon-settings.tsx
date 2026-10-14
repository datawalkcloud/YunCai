/**
 * 复刻区设置模块（对应柠檬云 /Settings/* 等）：
 * - 币别 / 凭证字 / 凭证模板：已接真实后端（CRUD，弹窗为真输入控件）。
 * - 操作日志：已接查询接口（日期/关键字筛选生效）。重新初始化：已接清空接口（高危，勾选风险确认后按账套名执行）。
 * - 权限设置 / 老板看账 / 备份恢复 / 关联云发票 / 关联进销存 / 旧账导入：后端暂无对应能力，
 *   页面保留界面复刻并统一挂 WipNotice 建设中提示，假数据与死按钮已清除。
 * - 账套 / 科目 / 期初 / 辅助核算：能力引导页（真实管理走既有页面或后续迭代）。
 * 视觉遵循全站统一「云财蓝」：主色 #2f6bff、slate 灰阶文字、白卡 + 渐变页头横幅，控件一律用 ./components/ui 基元。
 */
import { Fragment, useCallback, useEffect, useState } from "react";
import {
    Boxes,
    Briefcase,
    CircleDollarSign,
    DatabaseBackup,
    HelpCircle,
    LayoutTemplate,
    Receipt,
    RotateCcw,
    ScrollText,
    ShieldCheck,
    Stamp,
    Upload,
} from "lucide-react";

import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { useCurrentAccountSet, useDefaultAccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import {
    AccountsApi,
    CurrenciesApi,
    OperationLogsApi,
    ReinitApi,
    VoucherTemplatesApi,
    VoucherWordsApi,
    type AccountDto,
    type CurrencyDto,
    type OperationLogDto,
    type SaveCurrencyRequest,
    type SaveVoucherTemplateLineRequest,
    type SaveVoucherWordRequest,
    type VoucherTemplateDto,
    type VoucherWordDto,
} from "@/apis-gen";
import { StatusPill } from "@/components/page/status-pill";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";

import {
    Btn,
    CheckBox,
    Dialog,
    EmptyTip,
    FakeInput,
    FakeSelect,
    Page,
    Pager,
    RefreshIcon,
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

/** 账套上下文：id / token。 */
function useAccountContext() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    return { accountSetId, token };
}

/* ---------- 币别（已接后端） ---------- */

export function LemonCurrency() {
    const { accountSetId, token } = useAccountContext();
    const [rows, setRows] = useState<CurrencyDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [editing, setEditing] = useState<CurrencyDto | null>(null);
    const [formOpen, setFormOpen] = useState(false);
    const [form, setForm] = useState<SaveCurrencyRequest>({ code: "", name: "", rate: 1, isBase: false });

    const load = useCallback(async () => {
        if (!accountSetId) return;
        setLoading(true);
        setError(null);
        try {
            setRows(await new CurrenciesApi(apiConfig(token)).accountsetsAccountSetIdCurrenciesGet({ accountSetId }));
        } catch (err) {
            setError(await apiErrorMessage(err, "加载币别失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void load();
    }, [load]);

    function openCreate() {
        setEditing(null);
        setForm({ code: "", name: "", rate: 1, isBase: false });
        setFormOpen(true);
    }

    function openEdit(c: CurrencyDto) {
        setEditing(c);
        setForm({ code: c.code, name: c.name, rate: c.rate, isBase: c.isBase });
        setFormOpen(true);
    }

    async function submit() {
        if (!(form.code ?? "").trim() || !(form.name ?? "").trim()) {
            setToast("币别代码与名称不能为空。");
            return;
        }
        try {
            const api = new CurrenciesApi(apiConfig(token));
            if (editing) await api.accountsetsAccountSetIdCurrenciesIdPut({ accountSetId, id: editing.id ?? "", saveCurrencyRequest: form });
            else await api.accountsetsAccountSetIdCurrenciesPost({ accountSetId, saveCurrencyRequest: form });
            setFormOpen(false);
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "保存币别失败。"));
        }
    }

    async function remove(c: CurrencyDto) {
        if (!window.confirm(`确定删除币别「${c.name}」吗？`)) return;
        try {
            await new CurrenciesApi(apiConfig(token)).accountsetsAccountSetIdCurrenciesIdDelete({ accountSetId, id: c.id ?? "" });
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "删除币别失败。"));
        }
    }

    const cols: Col[] = [
        { key: "code", title: "币别", width: "120px" },
        { key: "name", title: "币别名称", width: "160px" },
        { key: "convert", title: "折算方式", width: "180px", render: () => "原币*汇率=本位币" },
        { key: "rate", title: "汇率", width: "100px", align: "right" },
        { key: "local", title: "是否本位币", width: "120px", render: (_, i) => (rows[i]?.isBase ? "是" : "否") },
        {
            key: "ops",
            title: "操作",
            width: "120px",
            render: (_, i) => {
                const c = rows[i];
                return (
                    <span className="flex gap-2">
                        <span className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline" onClick={() => openEdit(c)}>编辑</span>
                        <span className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline" onClick={() => void remove(c)}>删除</span>
                    </span>
                );
            },
        },
    ];
    return (
        <Page title="币别" subtitle="维护账套使用的外币种类与本位币汇率" icon={CircleDollarSign}>
            <div className="mx-auto w-[1090px]">
                <ToolRow left={<Btn variant="primary" onClick={openCreate}>新增</Btn>} right={<RefreshIcon />} />
                {error && <div className="px-3 py-2 text-[13px] text-[#ef4444]">{error}</div>}
                <Table cols={cols} rows={rows as unknown as Row[]} empty={loading ? undefined : <EmptyTip />} />
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            {formOpen && (
                <Dialog
                    title={editing ? "修改币别" : "新增币别"}
                    onClose={() => setFormOpen(false)}
                    footer={
                        <>
                            <Btn variant="primary" className="!px-6" onClick={() => void submit()}>保存</Btn>
                            <Btn className="!px-6" onClick={() => setFormOpen(false)}>取消</Btn>
                        </>
                    }
                >
                    <div className="flex flex-col gap-4">
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 币别代码：</span>
                            <TextInput value={form.code ?? ""} onChange={(v) => setForm({ ...form, code: v.toUpperCase() })} placeholder="如 CNY、USD" className="w-[230px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 币别名称：</span>
                            <TextInput value={form.name ?? ""} onChange={(v) => setForm({ ...form, name: v })} placeholder="如 人民币" className="w-[230px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-sm text-slate-700">汇率：</span>
                            <TextInput
                                type="number"
                                value={String(form.rate ?? 1)}
                                onChange={(v) => setForm({ ...form, rate: Number(v) || 0 })}
                                className="w-[230px]"
                            />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px]" />
                            <CheckBox label="设为本位币" checked={!!form.isBase} onChange={(v) => setForm({ ...form, isBase: v })} />
                        </div>
                    </div>
                </Dialog>
            )}
        </Page>
    );
}

/* ---------- 凭证字（已接后端） ---------- */

export function LemonVoucherWord() {
    const { accountSetId, token } = useAccountContext();
    const [rows, setRows] = useState<VoucherWordDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [editing, setEditing] = useState<VoucherWordDto | null>(null);
    const [formOpen, setFormOpen] = useState(false);
    const [form, setForm] = useState<SaveVoucherWordRequest>({ word: "", printTitle: "", isDefault: false });

    const load = useCallback(async () => {
        if (!accountSetId) return;
        setLoading(true);
        setError(null);
        try {
            setRows(await new VoucherWordsApi(apiConfig(token)).accountsetsAccountSetIdVoucherwordsGet({ accountSetId }));
        } catch (err) {
            setError(await apiErrorMessage(err, "加载凭证字失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void load();
    }, [load]);

    function openCreate() {
        setEditing(null);
        setForm({ word: "", printTitle: "", isDefault: false });
        setFormOpen(true);
    }

    function openEdit(w: VoucherWordDto) {
        setEditing(w);
        setForm({ word: w.word, printTitle: w.printTitle, isDefault: w.isDefault });
        setFormOpen(true);
    }

    async function submit() {
        if (!(form.word ?? "").trim()) {
            setToast("凭证字不能为空。");
            return;
        }
        try {
            const api = new VoucherWordsApi(apiConfig(token));
            if (editing) await api.accountsetsAccountSetIdVoucherwordsIdPut({ accountSetId, id: editing.id ?? "", saveVoucherWordRequest: form });
            else await api.accountsetsAccountSetIdVoucherwordsPost({ accountSetId, saveVoucherWordRequest: form });
            setFormOpen(false);
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "保存凭证字失败。"));
        }
    }

    async function remove(w: VoucherWordDto) {
        if (!window.confirm(`确定删除凭证字「${w.word}」吗？`)) return;
        try {
            await new VoucherWordsApi(apiConfig(token)).accountsetsAccountSetIdVoucherwordsIdDelete({ accountSetId, id: w.id ?? "" });
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "删除凭证字失败。"));
        }
    }

    const cols: Col[] = [
        { key: "word", title: "凭证字", width: "120px" },
        { key: "title", title: "打印标题", width: "160px" },
        { key: "def", title: "是否默认", width: "120px" },
        {
            key: "ops",
            title: "操作",
            width: "120px",
            render: (_, i) => {
                const w = rows[i];
                return (
                    <span className="flex gap-2">
                        <span className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline" onClick={() => openEdit(w)}>编辑</span>
                        <span className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline" onClick={() => void remove(w)}>删除</span>
                    </span>
                );
            },
        },
    ];
    return (
        <Page title="凭证字" subtitle="设置凭证编号使用的文字与打印标题" icon={Stamp}>
            <div className="mx-auto w-[1090px]">
                <ToolRow left={<Btn variant="primary" onClick={openCreate}>新增</Btn>} right={<RefreshIcon />} />
                {error && <div className="px-3 py-2 text-[13px] text-[#ef4444]">{error}</div>}
                <Table
                    cols={cols}
                    rows={rows.map((w) => ({ word: w.word, title: w.printTitle, def: w.isDefault ? "是" : "否" }))}
                    empty={loading ? undefined : <EmptyTip />}
                />
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            {formOpen && (
                <Dialog
                    title={editing ? "修改凭证字" : "新增凭证字"}
                    onClose={() => setFormOpen(false)}
                    footer={
                        <>
                            <Btn variant="primary" className="!px-6" onClick={() => void submit()}>保存</Btn>
                            <Btn className="!px-6" onClick={() => setFormOpen(false)}>取消</Btn>
                        </>
                    }
                >
                    <div className="flex flex-col gap-4">
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 凭证字：</span>
                            <TextInput value={form.word ?? ""} onChange={(v) => setForm({ ...form, word: v })} placeholder="如 记、收、付" className="w-[230px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-sm text-slate-700"><span className="text-[#ef4444]">*</span> 打印标题：</span>
                            <TextInput value={form.printTitle ?? ""} onChange={(v) => setForm({ ...form, printTitle: v })} placeholder="打印凭证时显示的标题" className="w-[230px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px]" />
                            <CheckBox label="设为默认凭证字" checked={!!form.isDefault} onChange={(v) => setForm({ ...form, isDefault: v })} />
                        </div>
                    </div>
                </Dialog>
            )}
        </Page>
    );
}

/* ---------- 凭证模板（已接后端） ---------- */

export function LemonVoucherTemplate() {
    const { accountSetId, token } = useAccountContext();
    const [templates, setTemplates] = useState<VoucherTemplateDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    const [creating, setCreating] = useState(false);
    const [name, setName] = useState("");
    const [subjectAccounts, setSubjectAccounts] = useState<AccountDto[]>([]);

    const load = useCallback(async () => {
        if (!accountSetId) return;
        setLoading(true);
        try {
            setTemplates(await new VoucherTemplatesApi(apiConfig(token)).accountsetsAccountSetIdVouchertemplatesGet({ accountSetId }));
        } catch (err) {
            setToast(await apiErrorMessage(err, "加载凭证模板失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void load();
    }, [load]);

    async function openCreate() {
        setName("");
        setSubjectAccounts(
            await new AccountsApi(apiConfig(token))
                .accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true })
                .catch(() => []),
        );
        setCreating(true);
    }

    async function submit() {
        const first = subjectAccounts[0];
        const lines: SaveVoucherTemplateLineRequest[] = first
            ? [{ summary: name || "模板分录", accountId: first.id ?? "", isDebit: true }]
            : [];
        try {
            await new VoucherTemplatesApi(apiConfig(token)).accountsetsAccountSetIdVouchertemplatesPost({
                accountSetId,
                saveVoucherTemplateRequest: { name: name || "未命名模板", type: 1, lines },
            });
            setCreating(false);
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "保存凭证模板失败。"));
        }
    }

    async function remove(t: VoucherTemplateDto) {
        if (!window.confirm(`确定删除凭证模板「${t.name}」吗？`)) return;
        try {
            await new VoucherTemplatesApi(apiConfig(token)).accountsetsAccountSetIdVouchertemplatesIdDelete({ accountSetId, id: t.id ?? "" });
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "删除凭证模板失败。"));
        }
    }

    const th = "border-b border-slate-200 bg-muted px-3 py-2 text-[13px] font-normal text-slate-500";
    const td = "border-b border-slate-100 px-3 py-2.5 text-[13px] text-slate-600 tabular-nums";
    if (creating) {
        return (
            <Page title="新建凭证模板" subtitle="按当月常用分录沉淀为可复用的凭证模板" icon={LayoutTemplate}>
                <ToolRow
                    left={
                        <>
                            <span className="text-sm text-slate-700">模板名称：</span>
                            <TextInput value={name} onChange={setName} placeholder="请输入模板名称" className="w-[150px]" />
                            <span className="text-sm text-slate-700">模板类型：</span>
                            <FakeSelect text="日常支出" className="w-[130px]" />
                        </>
                    }
                    right={
                        <>
                            <Btn variant="primary" onClick={() => void submit()}>保存</Btn>
                            <Btn onClick={() => setCreating(false)}>取消</Btn>
                        </>
                    }
                />
                <div className="mx-auto w-[1050px] flex-1 py-4">
                    <table className="w-full border-collapse text-sm">
                        <thead>
                            <tr className="bg-muted text-[13px] text-slate-600">
                                <th rowSpan={2} className="w-[50px] border border-slate-300 px-1 py-1.5 font-normal">序号</th>
                                <th rowSpan={2} className="w-[220px] border border-slate-300 px-2 py-1.5 font-normal">摘要</th>
                                <th rowSpan={2} className="border border-slate-300 px-2 py-1.5 font-normal">会计科目</th>
                                <th colSpan={11} className="border border-slate-300 px-2 py-1 font-normal">借方金额</th>
                                <th colSpan={11} className="border border-slate-300 px-2 py-1 font-normal">贷方金额</th>
                            </tr>
                            <tr className="bg-muted text-[10px] text-slate-500">
                                {["亿", "千", "百", "十", "万", "千", "百", "十", "元", "角", "分"].map((d, i) => (
                                    <th key={`j${i}`} className="w-[20px] border border-slate-300 py-0.5 font-normal">{d}</th>
                                ))}
                                {["亿", "千", "百", "十", "万", "千", "百", "十", "元", "角", "分"].map((d, i) => (
                                    <th key={`d${i}`} className="w-[20px] border border-slate-300 py-0.5 font-normal">{d}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {[1, 2, 3, 4].map((n) => (
                                <tr key={n} className="h-[52px]">
                                    <td className="border border-slate-300 text-center text-[13px] text-slate-600 tabular-nums">{n}</td>
                                    <td className="border border-slate-300 px-2"><span className="block h-7 w-full rounded-lg border border-slate-300" /></td>
                                    <td className="border border-slate-300 px-2">
                                        {n === 1 && subjectAccounts.length > 0 ? (
                                            <span className="block h-7 w-full truncate rounded-lg border border-slate-300 px-2 text-[13px] leading-7 text-slate-700">
                                                {subjectAccounts[0].code} {subjectAccounts[0].name}
                                            </span>
                                        ) : (
                                            <span className="block h-7 w-full rounded-lg border border-slate-300" />
                                        )}
                                    </td>
                                    {Array.from({ length: 11 }).map((_, i) => (
                                        <td key={`j${i}`} className="border border-slate-300" />
                                    ))}
                                    {Array.from({ length: 11 }).map((_, i) => (
                                        <td key={`d${i}`} className="border border-slate-300" />
                                    ))}
                                </tr>
                            ))}
                            <tr className="h-[44px]">
                                <td colSpan={3} className="border border-slate-300 px-3 text-sm text-slate-700">合计：零元整</td>
                                <td colSpan={22} className="border border-slate-300 bg-[#fffbeb]" />
                            </tr>
                        </tbody>
                    </table>
                    <div className="py-4 text-[12px] leading-5 text-slate-400">
                        提示：新建默认生成「日常支出」类型、首行取第一个末级科目的模板；行编辑与金额栏随迭代开放。
                    </div>
                </div>
            </Page>
        );
    }
    return (
        <Page title="凭证模板" subtitle="沉淀常用分录，制单时一键套用" icon={LayoutTemplate}>
            <ToolRow
                left={
                    <>
                        <Btn variant="primary" onClick={() => void openCreate()}>新建</Btn>
                        <WipBtn>导入</WipBtn>
                        <WipBtn>导出</WipBtn>
                    </>
                }
                right={<RefreshIcon />}
            />
            <div className="flex-1 px-4">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr>
                            <th className={`${th} text-left`}>摘要</th>
                            <th className={`${th} text-left`}>科目</th>
                            <th className={`${th} text-center`}>借贷方向</th>
                            <th className={`${th} text-right`}>借方金额</th>
                            <th className={`${th} text-right`}>贷方金额</th>
                        </tr>
                    </thead>
                    <tbody>
                        {templates.flatMap((t) => [
                            <Fragment key={t.id + "-head"}>
                                <tr className="bg-[#f6f9ff]">
                                    <td colSpan={5} className="px-3 py-2 text-[13px] text-slate-700">
                                        <span className="font-bold">模板名称:</span> {t.name}
                                        <span className="ml-6 font-bold">模板类型:</span> {t.typeText}
                                        <span
                                            className="ml-6 cursor-pointer text-[13px] text-[#2f6bff] hover:underline"
                                            onClick={() => void remove(t)}
                                        >
                                            删除
                                        </span>
                                    </td>
                                </tr>
                                {(t.lines ?? []).map((l, i) => (
                                    <Fragment key={l.id}>
                                        <tr className="border-b border-slate-100">
                                            <td className={td}>{l.summary}</td>
                                            <td className={td}>{l.accountCode} {l.accountName}</td>
                                            <td className={`${td} text-center`}>{l.directionText}</td>
                                            <td className={`${td} text-right`} />
                                            <td className={`${td} text-right`} />
                                        </tr>
                                        {i === (t.lines ?? []).length - 1 && (
                                            <tr className="border-b border-slate-100 bg-[#fafbfd]">
                                                <td className={td}>合计</td>
                                                <td className={td}>零元整</td>
                                                <td />
                                                <td />
                                                <td />
                                            </tr>
                                        )}
                                    </Fragment>
                                ))}
                            </Fragment>
                        ])}
                        {templates.length === 0 && !loading && (
                            <tr>
                                <td colSpan={5} className="py-10 text-center text-[13px] text-slate-400">暂无数据</td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>
            <Pager total={templates.length} count={templates.length} />
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 权限设置（建设中：后端暂无成员/权限管理能力） ---------- */

export function LemonPermission() {
    const accountSet = useCurrentAccountSet();
    return (
        <Page
            title="权限设置"
            subtitle={accountSet?.companyName || accountSet?.name || "账套"}
            icon={ShieldCheck}
            actions={
                <>
                    <WipBtn>+新增成员</WipBtn>
                    <RefreshIcon />
                </>
            }
        >
            <WipNotice text="权限设置界面复刻，尚未接入后端：成员与权限管理请待后续迭代。" />
            <div className="mx-auto w-[980px] pb-2">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr>
                            <th className="border-b border-slate-200 bg-muted px-3 py-2.5 text-left text-[13px] font-normal text-slate-500">用户</th>
                            <th className="border-b border-slate-200 bg-muted px-3 py-2.5 text-left text-[13px] font-normal text-slate-500">手机号</th>
                            <th className="border-b border-slate-200 bg-muted px-3 py-2.5 text-left text-[13px] font-normal text-slate-500">权限</th>
                            <th className="border-b border-slate-200 bg-muted px-3 py-2.5 text-left text-[13px] font-normal text-slate-500">操作</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td colSpan={4} className="px-3 py-10 text-center text-[13px] text-slate-400">暂无数据</td>
                        </tr>
                    </tbody>
                </table>
                <div className="mt-6 flex items-center gap-2 rounded-xl bg-[#fafbfd] px-4 py-3 text-[13px] text-slate-500">
                    <span className="text-[#f59e0b]">⚠</span>
                    免费版仅限账套管理员一人使用，多人协作的专业版能力尚未建设 <WipTag />
                </div>
            </div>
        </Page>
    );
}

/* ---------- 老板看账（建设中：后端暂无智能报表授权能力） ---------- */

export function LemonBoss() {
    return (
        <Page title="老板看账" subtitle="智能生成老板看得懂的财务报表" icon={Briefcase}>
            <WipNotice text="老板看账界面复刻，尚未接入后端：智能财务分析报告能力待建设。" />
            <div className="flex flex-1 flex-col items-center pt-6">
                <div className="text-[24px] font-bold text-slate-800">帮您智能生成一份老板都看得懂的报表</div>
                <div className="mt-2 text-[13px] text-slate-400">智能财务分析报告，助您轻松和老板汇报</div>
                <span className="mt-6">
                    <WipBtn>立即授权</WipBtn>
                </span>
                <div className="mt-10 grid w-[1000px] grid-cols-2 gap-6">
                    <div className="flex rounded-2xl bg-[#fafbfd] p-6">
                        <div className="flex-1">
                            <div className="text-[15px] font-bold text-slate-800">老板看账亮点:</div>
                            <div className="mt-4 flex flex-col gap-3 text-[13px] text-slate-700">
                                {["财务报告自动生成", "财务数据通俗易懂", "收入费用一目了然"].map((t) => (
                                    <span key={t} className="flex items-center gap-2">
                                        <span className="text-[#10b981]">✓</span> {t}
                                    </span>
                                ))}
                            </div>
                        </div>
                        <div className="flex h-[140px] w-[90px] items-center justify-center rounded-lg border border-slate-200 bg-white text-[12px] text-slate-400">
                            手机报表
                        </div>
                    </div>
                    <div className="flex rounded-2xl bg-[#fafbfd] p-6">
                        <div className="flex-1">
                            <div className="text-[15px] font-bold text-slate-800">【老板看账】公众号</div>
                            <div className="mt-4 flex flex-col gap-2 text-[13px] text-slate-500">
                                <span>微信扫码关注，并推送给老板</span>
                                <span>轻松向老板汇报企业经营情况~</span>
                            </div>
                        </div>
                        <div className="flex h-[120px] w-[120px] items-center justify-center rounded-lg border border-slate-200 bg-white text-[12px] text-slate-400">
                            二维码
                        </div>
                    </div>
                </div>
            </div>
        </Page>
    );
}

/* ---------- 备份恢复（建设中：后端暂无备份/归档能力） ---------- */

const BACKUP_TABS = ["备份数据包", "归档Excel", "归档PDF", "文件下载中心", "审计对接"];

export function LemonBackup() {
    const [tab, setTab] = useState(0);
    const period = useDefaultAccountingPeriod();
    const th = "border-b border-slate-200 bg-muted px-3 py-2.5 text-left text-[13px] font-normal text-slate-500";
    const isDownload = tab === 3;
    const isAudit = tab === 4;
    return (
        <Page title="备份恢复" subtitle="账套数据包备份、归档导出与审计取数" icon={DatabaseBackup}>
            <WipNotice text="备份恢复界面复刻，尚未接入后端：备份数据包 / 归档 / 审计取数能力待建设。" />
            <SubTabs tabs={BACKUP_TABS} active={tab} onChange={setTab} />
            {!isAudit && (
                <ToolRow
                    left={<WipBtn>{isDownload ? "删除" : "开始归档"}</WipBtn>}
                    right={
                        <>
                            <span className="flex items-center gap-2 text-[13px] text-slate-500">
                                在线备份与扩容能力建设中 <WipTag />
                            </span>
                            <RefreshIcon />
                        </>
                    }
                />
            )}
            {!isAudit && (
                <div className="mx-auto w-[980px] flex-1">
                    <table className="w-full border-collapse text-sm">
                        <thead>
                            {isDownload ? (
                                <tr>
                                    {["", "文件名称", "来源类型", "来源位置", "日期", "文件大小", "操作人", "操作"].map((t) => (
                                        <th key={t} className={th}>{t}</th>
                                    ))}
                                </tr>
                            ) : (
                                <tr>
                                    {["备份名称", "日期", "文件大小", "操作人", "操作"].map((t) => (
                                        <th key={t} className={th}>{t}</th>
                                    ))}
                                </tr>
                            )}
                        </thead>
                    </table>
                    <EmptyTip />
                    <div className="mt-8 flex flex-col gap-2 pb-8 text-[12px] leading-5 text-slate-400">
                        <span>备份数据包、归档Excel、归档PDF有什么区别?</span>
                        <span>备份数据包 → “备份”整个账套的数据（✅支持下载到本地和数据恢复，⚪不支持在线预览 ）</span>
                        <span>归档Excel和归档PDF → 将整个账套的数据归档成Excel或PDF文件（✅支持下载到本地，⚪不支持在线预览和数据恢复 ）</span>
                    </div>
                </div>
            )}
            {isAudit && (
                <div className="mx-auto mt-6 w-[1000px]">
                    <div className="flex h-[300px] items-center justify-between rounded-2xl bg-gradient-to-r from-[#0b1e3f] to-[#123a6e] px-12">
                        <div>
                            <div className="text-[22px] font-bold text-white">审计无忧，一键提取</div>
                            <div className="mt-4 flex items-center gap-2">
                                <span className="text-[13px] text-[#cdd8ea]">审计软件：</span>
                                <FakeInput text="鼎信诺" className="w-[160px]" />
                            </div>
                            <div className="mt-2 flex items-center gap-2">
                                <span className="text-[13px] text-[#cdd8ea]">审计期间：</span>
                                <FakeInput text={`${period.year}年${String(period.month).padStart(2, "0")}月`} className="w-[110px]" />
                                <span className="text-[#cdd8ea]">至</span>
                                <FakeInput className="w-[110px]" />
                            </div>
                            <div className="mt-5">
                                <WipBtn>一键提取</WipBtn>
                            </div>
                        </div>
                        <div className="flex flex-col gap-2 text-right text-[13px] text-[#cdd8ea]">
                            <span>安全授权，取数方便</span>
                            <span>满足企业审计取款的需求</span>
                            <span>随时随地管理审计文件数据</span>
                        </div>
                    </div>
                </div>
            )}
        </Page>
    );
}

/* ---------- 操作日志（已接后端） ---------- */

export function LemonLog() {
    const { accountSetId, token } = useAccountContext();
    const period = useDefaultAccountingPeriod();
    const [rows, setRows] = useState<OperationLogDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [start, setStart] = useState(period.startDate);
    const [end, setEnd] = useState(period.endDate);
    const [keyword, setKeyword] = useState("");

    const load = useCallback(async () => {
        if (!accountSetId) return;
        setLoading(true);
        setError(null);
        try {
            setRows(
                await new OperationLogsApi(apiConfig(token)).accountsetsAccountSetIdOperationlogsGet({
                    accountSetId,
                    // 后端为 DateTime 参数（存 UTC 时间戳）：按本地时区取当日 0 点至 23:59:59 再序列化。
                    start: start ? new Date(`${start}T00:00:00`) : undefined,
                    end: end ? new Date(`${end}T23:59:59`) : undefined,
                    keyword: keyword || undefined,
                }),
            );
        } catch (err) {
            setRows([]);
            setError(await apiErrorMessage(err, "加载操作日志失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, start, end, keyword]);

    useEffect(() => {
        void load();
    }, [load]);

    const cols: Col[] = [
        { key: "uid", title: "用户ID", width: "110px", align: "right" },
        { key: "uname", title: "用户姓名", width: "120px" },
        { key: "type", title: "日志类型", width: "120px" },
        { key: "module", title: "模块", width: "140px" },
        { key: "log", title: "日志" },
        { key: "time", title: "操作时间", width: "170px" },
    ];
    return (
        <Page title="操作日志" subtitle="按日期与关键字检索账套内的关键操作记录" icon={ScrollText}>
            <ToolRow
                left={
                    <>
                        <TextInput type="date" value={start} onChange={setStart} className="w-[150px]" />
                        <span className="text-slate-400">至</span>
                        <TextInput type="date" value={end} onChange={setEnd} className="w-[150px]" />
                        <TextInput value={keyword} onChange={setKeyword} placeholder="请输入日志的关键字搜索" className="w-[260px]" />
                        <Btn variant="primary" onClick={() => void load()}>查询</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            {error && <div className="px-4 text-[13px] text-[#ef4444]">{error}</div>}
            <div className="flex-1 px-4">
                <Table
                    cols={cols}
                    rows={rows.map((l) => ({
                        uid: l.userId,
                        uname: l.userName,
                        type: l.type,
                        module: l.module,
                        log: l.log,
                        time: l.time ? new Date(l.time).toLocaleString("zh-CN") : "",
                    }))}
                    empty={loading ? <EmptyTip text="加载中…" /> : <EmptyTip />}
                />
            </div>
            <Pager total={rows.length} count={rows.length} />
        </Page>
    );
}

/* ---------- 关联云发票 / 关联进销存（能力引导页） ---------- */

/* ---------- 关联云发票 / 关联进销存（建设中：后端暂无外部系统关联能力） ---------- */

function RelationTable({ cols2 }: { cols2: string }) {
    const accountSet = useCurrentAccountSet();
    const th = "border-b border-slate-200 bg-muted px-3 py-2.5 text-[13px] font-normal text-slate-500";
    const td = "border-b border-slate-100 px-3 py-2.5 text-[13px] text-slate-600 tabular-nums";
    return (
        <table className="w-full border-collapse text-sm">
            <thead>
                <tr>
                    <th className={`${th} w-[130px] text-left`}>关联状态</th>
                    <th className={`${th} w-[240px] text-left`}>财务软件账套</th>
                    <th className={`${th} text-left`}>{cols2}</th>
                    <th className={`${th} w-[100px] text-left`}>操作</th>
                </tr>
            </thead>
            <tbody>
                <tr>
                    <td className={td}>
                        <StatusPill tone="gray">未关联</StatusPill>
                    </td>
                    <td className={td}>{accountSet?.companyName || accountSet?.name || "账套"}</td>
                    <td className={td} />
                    <td className={`${td} text-[12px] text-slate-400`}>
                        关联功能建设中 <WipTag />
                    </td>
                </tr>
            </tbody>
        </table>
    );
}

function StepCard({ title, desc }: { title: string; desc: string }) {
    return (
        <div className="flex-1 rounded-2xl border border-slate-200 bg-white p-5 text-center">
            <div className="mx-auto flex h-10 w-10 items-center justify-center rounded-full bg-[#eef4ff] text-[18px] text-[#2f6bff]">✓</div>
            <div className="mt-3 text-[14px] font-bold text-slate-800">{title}</div>
            <div className="mt-2 text-[12px] leading-5 text-slate-400">{desc}</div>
        </div>
    );
}

export function LemonEisRelation() {
    return (
        <Page title="关联云发票" subtitle="打通云发票，开票数据自动生成凭证" icon={Receipt}>
            <WipNotice text="关联云发票界面复刻，尚未接入后端：发票关联与自动凭证能力待建设。" />
            <ToolRow
                left={<span className="text-[13px] text-slate-400">查看帮助（建设中）</span>}
                right={<RefreshIcon />}
            />
            <div className="mx-auto w-[980px] pb-8">
                <RelationTable cols2="云发票企业" />
                <div className="mt-8 mb-3 text-[14px] font-bold text-slate-800">操作指引:</div>
                <div className="flex items-center gap-2">
                    <StepCard title="关联云发票企业" desc="拥有财务软件的关联云发票权限，且是云发票的系统管理员才有权限设置关联" />
                    <span className="text-slate-300">›</span>
                    <StepCard title="开具发票" desc="云发票支持立即开票、批量开票、单据开票、复制开票等多种开票方式" />
                    <span className="text-slate-300">›</span>
                    <StepCard title="自动生成凭证" desc="选择发票数据生成凭证" />
                </div>
                <div className="mt-8 mb-6 flex h-[120px] items-center justify-between rounded-2xl bg-[#eef4ff] px-10">
                    <span className="text-[22px] font-bold text-[#2f6bff]">柠檬云发票</span>
                    <span className="text-[13px] text-slate-500">一站式数电发票服务平台</span>
                    <WipBtn>立即使用 <WipTag /></WipBtn>
                </div>
            </div>
        </Page>
    );
}

export function LemonScmRelation() {
    return (
        <Page title="关联进销存" subtitle="业财一体，进销存单据自动生成凭证" icon={Boxes}>
            <WipNotice text="关联进销存界面复刻，尚未接入后端：业财关联与凭证自动生成能力待建设。" />
            <ToolRow
                left={<span className="text-[13px] text-slate-400">进销存+财务管理操作视频（建设中）</span>}
                right={<RefreshIcon />}
            />
            <div className="mx-auto w-[980px] pb-8">
                <RelationTable cols2="进销存账套" />
                <div className="mt-8 mb-3 text-[14px] font-bold text-slate-800">操作指引:</div>
                <div className="flex items-center gap-2">
                    <StepCard title="关联进销存账套" desc="只有同时拥有财务软件和进销存的账套管理员才有权限设置关联" />
                    <span className="text-slate-300">›</span>
                    <StepCard title="设置进销存核算参数" desc="检查确定进销存核算参数的设置" />
                    <span className="text-slate-300">›</span>
                    <StepCard title="完善进销存基础资料" desc="设置进销存基础资料与财务软件账套的会计科目之间的对应关系" />
                    <span className="text-slate-300">›</span>
                    <StepCard title="自动生成凭证" desc="选择进销存单据生成凭证" />
                </div>
                <div className="mt-8 mb-6 flex h-[120px] items-center gap-8 rounded-2xl bg-[#eef4ff] px-10">
                    <span className="flex gap-2">
                        {["采购", "销售", "库存", "财务"].map((t) => (
                            <span key={t} className="rounded-full bg-white px-3 py-1 text-[12px] text-[#2f6bff]">{t}</span>
                        ))}
                    </span>
                    <span className="flex-1 text-[13px] leading-5 text-slate-500">
                        财务与业务数据不同步，核对数据费事费力，还总出错？您只需一个：柠檬云业财
                        <br />
                        业财税完美融合，全面理顺企业内外部流程
                        <br />
                        做更懂中国企业的业财税一体化系统
                    </span>
                    <WipBtn>前往业财体验 <WipTag /></WipBtn>
                </div>
            </div>
        </Page>
    );
}

/* ---------- 旧账导入（建设中：后端暂无旧账迁移能力） ---------- */

export function LemonImportOld() {
    return (
        <Page title="旧账导入" subtitle="在线导入、导账工具或科目余额表迁移历史账套" icon={Upload}>
            <WipNotice text="旧账导入界面复刻，尚未接入后端：在线导账 / 工具导入 / 科目余额表导入能力待建设。" />
            <div className="mx-auto mt-6 mb-10 w-[1000px] rounded-2xl border border-slate-200 p-10">
                <div className="mb-10 flex items-center justify-center">
                    {["选择软件", "输入账号", "导入旧账数据"].map((t, i) => (
                        <div key={t} className="flex items-center">
                            <span className="flex items-center gap-2">
                                <span className={`flex h-7 w-7 items-center justify-center rounded-full text-[13px] ${i === 0 ? "bg-[#2f6bff] text-white" : "bg-slate-100 text-slate-400"}`}>
                                    {i + 1}
                                </span>
                                <span className={`text-[13px] ${i === 0 ? "text-[#2f6bff]" : "text-slate-400"}`}>{t}</span>
                            </span>
                            {i < 2 && <span className="mx-4 h-px w-24 bg-slate-200" />}
                        </div>
                    ))}
                </div>
                <div className="flex items-center justify-center gap-3">
                    <span className="text-[#ef4444]">*</span>
                    <span className="text-sm text-slate-700">选择软件厂商:</span>
                    <FakeSelect text="--请选择--" className="w-[280px]" />
                    <HelpCircle className="h-4 w-4 text-slate-400" />
                </div>
                <div className="mt-8 flex justify-center">
                    <WipBtn>下一步</WipBtn>
                </div>
                <div className="mt-10 flex justify-end gap-4 text-[13px] text-slate-400">
                    <span>导账历史记录（建设中）</span>
                    <span>导账工具一键导入（建设中）</span>
                    <span>科目余额表导账（建设中）</span>
                </div>
            </div>
            <div className="mx-auto mt-8 w-[440px] rounded-2xl bg-[#fafbfd] p-5 text-[13px] leading-7 text-slate-700">
                <div className="font-bold">旧账导入支持以下方式（均为建设中）:</div>
                <div>① 在线导入旧账数据</div>
                <div>② 下载导账工具进行旧账导入</div>
                <div>③ 使用科目余额表进行导入</div>
                <div>④ 扫码添加客服，免费为您迁移账套</div>
            </div>
        </Page>
    );
}

/* ---------- 重新初始化（已接后端） ---------- */

export function LemonReinit() {
    const { accountSetId, token } = useAccountContext();
    const { user } = useAuth();
    const accountSet = useCurrentAccountSet();
    const [checked, setChecked] = useState(false);
    const [busy, setBusy] = useState(false);
    const [result, setResult] = useState<string | null>(null);
    const [toast, setToast] = useState<string | null>(null);

    async function doReinit() {
        if (!checked) {
            setToast("请先勾选风险确认。");
            return;
        }
        if (!accountSet?.name) {
            setToast("尚未加载到账套信息，请稍后重试。");
            return;
        }

        if (!window.confirm("重新初始化将清空账套全部业务数据且不可恢复，确定继续吗？")) return;
        setBusy(true);
        try {
            const res = await new ReinitApi(apiConfig(token)).accountsetsAccountSetIdReinitPost({
                accountSetId,
                wipeRequest: {
                    // 确认口令 = 账套名称（后端要求与账套名完全一致）
                    confirmName: accountSet.name,
                    operatorUserId: user?.id ?? "",
                    operatorUserName: user?.displayName ?? user?.username ?? "",
                },
            });
            const summary = Object.entries(res.wiped ?? {})
                .filter(([, n]) => n > 0)
                .map(([k, n]) => `${k} ${n} 条`)
                .join("、");
            setResult(summary ? `已清空：${summary}。结转/结账期间已退回启用期间。` : "账套已清空，期间已退回启用期间。");
        } catch (err) {
            setToast(await apiErrorMessage(err, "重新初始化失败。"));
        } finally {
            setBusy(false);
        }
    }

    return (
        <Page title="重新初始化" subtitle="清空账套全部业务数据并退回启用期间（不可恢复）" icon={RotateCcw}>
            <div className="mx-auto mt-6 mb-10 w-[1000px] rounded-2xl border border-slate-200 p-10 text-center">
                <div className="text-[14px] font-bold text-[#ef4444]">
                    重新初始化将彻底清空您在账套中录入的所有数据，并不可恢复，请慎重！！
                </div>
                <div className="mt-5 text-[14px] font-bold text-slate-800">
                    您将要重新初始化账套：{accountSet?.name ?? "（加载中…）"}，以下数据将被永久删除:
                </div>
                <div className="mt-4 flex flex-col gap-2 text-left text-[13px] leading-6 text-slate-700">
                    <span>1、新增的所有会计科目、辅助核算等基础设置</span>
                    <span>2、所有已录入的凭证、发票、日记账、资产及初始化数据</span>
                    <span>3、已保存的凭证模板、数据透视表模板等个性化配置模板</span>
                    <span>4、如果您已结账，重新初始化后会退回到账套启用期间</span>
                </div>
                <div className="mt-6 flex justify-center">
                    <CheckBox
                        label="我已清楚重新初始化将会清空所有数据，并知晓操作风险"
                        checked={checked}
                        onChange={setChecked}
                    />
                </div>
                <div className="mt-6 flex justify-center">
                    <Btn variant={checked && !busy ? "primary" : "disabled"} className="!px-8" disabled={!checked || busy} onClick={() => void doReinit()}>
                        {busy ? "处理中…" : "重新初始化"}
                    </Btn>
                </div>
                {result && <div className="mt-5 text-[13px] leading-6 text-[#10b981]">{result}</div>}
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
