/**
 * 复刻区设置模块（对应柠檬云 /Settings/* 等）：
 * - 币别 / 凭证字 / 凭证模板：已接真实后端（CRUD，弹窗为真输入控件）。
 * - 操作日志：已接查询接口（日期/关键字筛选生效）。重新初始化：已接清空接口（高危，勾选风险确认后按账套名执行）。
 * - 权限设置：已接后端（账套成员与角色，创建者保护 / 启停用 / 增删改）。
 * - 老板看账：已接后端财务概要（本期 / 本年累计 / 同比 / 环比 + 经营解读）。
 * - 旧账导入：科目余额表导入已接后端（模板下载 / 借贷两列或单列余额 + 方向 / 试算平衡）。
 * - 关联云发票 / 关联进销存 / 在线导账 / 导账工具：外部平台对接能力，保留复刻占位。
 * - 备份恢复：备份数据包（整账套 JSON 导出 + 恢复）、归档Excel / 归档PDF（PaperSharp）、
 *   审计对接一键提取（多页签取数包）均已接后端；文件下载中心暂未接入文件存储，保留占位。
 * - 账套 / 科目 / 期初 / 辅助核算：能力引导页（真实管理走既有页面或后续迭代）。
 * 视觉遵循全站统一「云财蓝」：主色 #273cf3、slate 灰阶文字、白卡，控件一律用 ./components/ui 基元。
 */
import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { Alert, Radio, Table as AntTable } from "antd";
import { ExclamationCircleFilled } from "@ant-design/icons";

import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { useCurrentAccountSet, useDefaultAccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import {
    AccountsApi,
    CurrenciesApi,
    MembersApi,
    OperationLogsApi,
    ReinitApi,
    ReportsApi,
    RolesApi,
    VoucherTemplatesApi,
    VoucherWordsApi,
    type AccountDto,
    type AccountSetMemberDto,
    type AccountSetRoleDto,
    type BackupRestoreResultDto,
    type CurrencyDto,
    type FinancialSummaryDto,
    type OpeningBalanceImportResultDto,
    type OperationLogDto,
    type PermissionModuleDto,
    type SaveAccountSetMemberRequest,
    type SaveAccountSetRoleRequest,
    type SaveCurrencyRequest,
    type SaveVoucherTemplateLineRequest,
    type SaveVoucherWordRequest,
    type VoucherTemplateDto,
    type VoucherTemplateImportResultDto,
    type VoucherWordDto,
} from "@/apis-gen";
import { StatusPill } from "@/components/page/status-pill";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";

import {
    Btn,

    ExportBtn,
    CheckBox,
    ComboBox,
    DateInput,
    Dialog,
    EmptyTip,
    FakeSelect,
    LinkAction,
    Page,
    Pager,
    PeriodStepper,
    Select,
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
import { FORM_CONTROL_W } from "@/components/page/styles";

/** 账套上下文：id / token。 */
function useAccountContext() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    return { accountSetId, token };
}

/* ---------- 币别（已接后端） ---------- */

/** 常用币别字典：新增时从下拉里选，选中自动带出名称；也允许直接录入表外代码。 */
const CURRENCY_PRESETS: Array<{ code: string; name: string }> = [
    { code: "USD", name: "美元" },
    { code: "EUR", name: "欧元" },
    { code: "JPY", name: "日元" },
    { code: "HKD", name: "港币" },
    { code: "GBP", name: "英镑" },
    { code: "AUD", name: "澳大利亚元" },
    { code: "NZD", name: "新西兰元" },
    { code: "SGD", name: "新加坡元" },
    { code: "CHF", name: "瑞士法郎" },
    { code: "CAD", name: "加拿大元" },
    { code: "KRW", name: "韩元" },
    { code: "RUB", name: "俄罗斯卢布" },
    { code: "THB", name: "泰铢" },
    { code: "MYR", name: "马来西亚林吉特" },
    { code: "IDR", name: "印度尼西亚卢比" },
    { code: "INR", name: "印度卢比" },
    { code: "PHP", name: "菲律宾比索" },
    { code: "VND", name: "越南盾" },
    { code: "TWD", name: "新台币" },
    { code: "MOP", name: "澳门元" },
    { code: "SEK", name: "瑞典克朗" },
    { code: "DKK", name: "丹麦克朗" },
    { code: "NOK", name: "挪威克朗" },
    { code: "PLN", name: "波兰兹罗提" },
    { code: "HUF", name: "匈牙利福林" },
    { code: "TRY", name: "新土耳其里拉" },
    { code: "AED", name: "阿联酋迪拉姆" },
    { code: "SAR", name: "沙特里亚尔" },
    { code: "ZAR", name: "南非兰特" },
    { code: "BRL", name: "巴西雷亚尔" },
    { code: "MXN", name: "墨西哥比索" },
    { code: "EGP", name: "埃及镑" },
];

/** 折算方式取值，与后端 CurrencyConvertType 一致。 */
const CONVERT_TYPES = [
    { value: "1", label: "原币*汇率=本位币" },
    { value: "2", label: "原币/汇率=本位币" },
];

/** 汇率行情来源提示（弹窗底部）。 */
function RateSourceHint() {
    return (
        <div className="flex items-start gap-2 pt-1 text-[12.5px] leading-5 text-(--muted-foreground)">
            <ExclamationCircleFilled style={{ color: "var(--warn-icon)" }} className="mt-[3px] shrink-0 text-[13px]" />
            <span>
                1.中国外汇交易中心：
                <a
                    className="ml-1 text-(--brand) hover:underline"
                    href="https://www.chinamoney.com.cn/chinese/bkccpr/?tab=2"
                    target="_blank"
                    rel="noreferrer"
                >
                    https://www.chinamoney.com.cn/chinese/bkccpr/?tab=2
                </a>
            </span>
        </div>
    );
}

export function LemonCurrency() {
    const { accountSetId, token } = useAccountContext();
    const [rows, setRows] = useState<CurrencyDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [editing, setEditing] = useState<CurrencyDto | null>(null);
    const [formOpen, setFormOpen] = useState(false);
    const [form, setForm] = useState<SaveCurrencyRequest>({ code: "", name: "", rate: 1, convertType: 1, isBase: false });
    const [pendingDelete, setPendingDelete] = useState<CurrencyDto | null>(null);

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
        setForm({ code: "", name: "", rate: 1, convertType: 1, isBase: false });
        setFormOpen(true);
    }

    function openEdit(c: CurrencyDto) {
        setEditing(c);
        setForm({ code: c.code, name: c.name, rate: c.rate, convertType: c.convertType ?? 1, isBase: c.isBase });
        setFormOpen(true);
    }

    /** 选币别代码：命中字典时顺手带出中文名称，用户仍可再改。 */
    function setCode(code: string) {
        const preset = CURRENCY_PRESETS.find((p) => p.code === code.toUpperCase());
        setForm((prev) => ({ ...prev, code: code.toUpperCase(), name: preset && !prev.name ? preset.name : prev.name }));
    }

    async function submit() {
        if (!(form.code ?? "").trim() || !(form.name ?? "").trim()) {
            setToast("币别代码与名称不能为空。");
            return;
        }
        if (!(Number(form.rate) > 0)) {
            setToast("汇率必须大于零。");
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

    /** 删除前先弹确认对话框（替代原生 window.confirm），确认后才调用删除接口。 */
    function remove(c: CurrencyDto) {
        setPendingDelete(c);
    }

    async function confirmRemove() {
        if (!pendingDelete) return;
        try {
            await new CurrenciesApi(apiConfig(token)).accountsetsAccountSetIdCurrenciesIdDelete({ accountSetId, id: pendingDelete.id ?? "" });
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "删除币别失败。"));
        } finally {
            setPendingDelete(null);
        }
    }

    const cols: Col[] = [
        { key: "code", title: "币别", width: "120px" },
        { key: "name", title: "币别名称", width: "160px" },
        { key: "convertText", title: "折算方式", width: "180px", render: (r) => (r.convertText as string) || CONVERT_TYPES[0].label },
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
                        <span className="cursor-pointer text-[13px] text-(--brand) hover:underline" onClick={() => openEdit(c)}>编辑</span>
                        <LinkAction danger onClick={() => remove(c)}>删除</LinkAction>
                    </span>
                );
            },
        },
    ];
    return (
        <Page>
            <div className="mx-auto max-w-[1090px] w-full px-4">
                <ToolRow left={<Btn variant="primary" onClick={openCreate}>新增</Btn>} right={<RefreshIcon onClick={() => void load()} />} />
                {error && <div className="px-3 py-2 text-[13px] text-(--danger)">{error}</div>}
                <Table cols={cols} rows={rows as unknown as Row[]} empty={loading ? undefined : <EmptyTip />} />
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            {formOpen && (
                <Dialog
                    title={editing ? "修改币别" : "新增币别"}
                    width={560}
                    onClose={() => setFormOpen(false)}
                    footer={
                        <>
                            <Btn variant="primary" onClick={() => void submit()}>确定</Btn>
                            <Btn onClick={() => setFormOpen(false)}>取消</Btn>
                        </>
                    }
                >
                    <div className="flex flex-col gap-4 py-2">
                        <FormRow label="币别" required>
                            <ComboBox
                                value={form.code ?? ""}
                                onChange={setCode}
                                options={CURRENCY_PRESETS.map((p) => ({ value: p.code, label: `${p.code}（${p.name}）` }))}
                                placeholder="选择/录入币别编码"
                                className="flex-1"
                            />
                        </FormRow>
                        <FormRow label="名称" required>
                            <TextInput value={form.name ?? ""} onChange={(v) => setForm({ ...form, name: v })} placeholder="示例：美元" className="flex-1" />
                        </FormRow>
                        <FormRow label="折算方式">
                            <Select
                                value={String(form.convertType ?? 1)}
                                onChange={(v) => setForm({ ...form, convertType: Number(v) })}
                                options={CONVERT_TYPES}
                                className="flex-1"
                            />
                        </FormRow>
                        <FormRow label="汇率" required>
                            <TextInput
                                type="number"
                                value={String(form.rate ?? 1)}
                                onChange={(v) => setForm({ ...form, rate: Number(v) || 0 })}
                                placeholder="示例：7.1800"
                                className="flex-1"
                            />
                        </FormRow>
                        <FormRow label="">
                            <CheckBox label="设为本位币" checked={!!form.isBase} onChange={(v) => setForm({ ...form, isBase: v })} />
                        </FormRow>
                        <RateSourceHint />
                    </div>
                </Dialog>
            )}
            {pendingDelete && (
                <Dialog
                    title="删除币别"
                    onClose={() => setPendingDelete(null)}
                    footer={
                        <>
                            <Btn variant="primary" onClick={() => void confirmRemove()}>确定</Btn>
                            <Btn onClick={() => setPendingDelete(null)}>取消</Btn>
                        </>
                    }
                >
                    <div className="py-1 text-[13px] text-(--foreground)">确定删除币别「{pendingDelete.name}」吗？</div>
                </Dialog>
            )}
        </Page>
    );
}

/** 弹窗内的一行「标签 + 控件」：标签定宽右对齐，控件占满剩余宽度；label 留空即只放控件。 */
function FormRow({ label, required, children }: { label: string; required?: boolean; children: ReactNode }) {
    return (
        <div className="flex items-center gap-3">
            <span className="w-[80px] shrink-0 text-right text-[13px] text-(--foreground)">
                {label && (<>{required && <span className="text-(--danger)">*</span>} {label}：</>)}
            </span>
            {children}
        </div>
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
                        <span className="cursor-pointer text-[13px] text-(--brand) hover:underline" onClick={() => openEdit(w)}>编辑</span>
                        <LinkAction danger onClick={() => void remove(w)}>删除</LinkAction>
                    </span>
                );
            },
        },
    ];
    return (
        <Page>
            <div className="mx-auto max-w-[1090px] w-full px-4">
                <ToolRow left={<Btn variant="primary" onClick={openCreate}>新增</Btn>} right={<RefreshIcon />} />
                {error && <div className="px-3 py-2 text-[13px] text-(--danger)">{error}</div>}
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
                            <Btn variant="primary"  onClick={() => void submit()}>保存</Btn>
                            <Btn  onClick={() => setFormOpen(false)}>取消</Btn>
                        </>
                    }
                >
                    <div className="flex flex-col gap-4">
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-[13px] text-(--foreground)"><span className="text-(--danger)">*</span> 凭证字：</span>
                            <TextInput value={form.word ?? ""} onChange={(v) => setForm({ ...form, word: v })} placeholder="如 记、收、付" className={FORM_CONTROL_W} />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-[13px] text-(--foreground)"><span className="text-(--danger)">*</span> 打印标题：</span>
                            <TextInput value={form.printTitle ?? ""} onChange={(v) => setForm({ ...form, printTitle: v })} placeholder="打印凭证时显示的标题" className={FORM_CONTROL_W} />
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

/* ---------- 凭证模板（已接后端：CRUD + xlsx 导入导出） ---------- */

/** 金额分位刻度（亿…分），对应 11 位金额栏。 */
const AMOUNT_SCALE = ["亿", "千", "百", "十", "万", "千", "百", "十", "元", "角", "分"];

/** 11 位金额分位占位格（凭证模板预览用，金额录入尚未开放）。 */
function AmountSlots() {
    return (
        <div className="flex overflow-hidden rounded border border-(--border)">
            {AMOUNT_SCALE.map((_, i) => (
                <span key={i} className="h-[28px] w-[20px] border-r border-(--border) last:border-r-0" />
            ))}
        </div>
    );
}

/** 金额列表头：标题 + 11 位刻度。 */
function AmountHeader({ title }: { title: string }) {
    return (
        <span className="flex flex-col items-center leading-tight">
            <span>{title}</span>
            <span className="text-[10px] font-normal text-(--muted-foreground)">{AMOUNT_SCALE.join("")}</span>
        </span>
    );
}

/** 模板列表扁平化后的行模型：模板首行带模板名，末尾追加合计行。 */
type TemplateRow = {
    key: string;
    kind: "line" | "total";
    showTemplate: boolean;
    template: VoucherTemplateDto;
    templateName: string;
    templateType: string;
    summary: string;
    account: string;
    direction: string;
};

export function LemonVoucherTemplate() {
    const { accountSetId, token } = useAccountContext();
    const [templates, setTemplates] = useState<VoucherTemplateDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    const [creating, setCreating] = useState(false);
    const [name, setName] = useState("");
    const [subjectAccounts, setSubjectAccounts] = useState<AccountDto[]>([]);
    const [busy, setBusy] = useState(false);
    const [importResult, setImportResult] = useState<VoucherTemplateImportResultDto | null>(null);
    const importRef = useRef<HTMLInputElement>(null);

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

    /** 导出全部凭证模板（一模板多行，可改完再导回）。 */
    async function exportTemplates() {
        if (!token || !accountSetId || busy) return;
        setBusy(true);
        try {
            const err = await settingsDownload(`/api/accountsets/${accountSetId}/vouchertemplates/export`, token, "凭证模板.xlsx");
            if (err) setToast(err);
        } finally {
            setBusy(false);
        }
    }

    /** 导入凭证模板：按模板名称分组，同名跳过。 */
    async function submitImport(file: File) {
        if (!token || !accountSetId || busy) return;
        setBusy(true);
        try {
            const res = await settingsUpload<VoucherTemplateImportResultDto>(`/api/accountsets/${accountSetId}/vouchertemplates/import`, token, file);
            if (res.error) setToast(res.error);
            else {
                setImportResult(res.data ?? null);
                await load();
            }
        } finally {
            setBusy(false);
        }
    }

    /** 模板列表扁平化：每个模板首行显示模板名与删除操作，末尾追加「合计」行。 */
    const templateRows: TemplateRow[] = templates.flatMap((t) => {
        const lines = t.lines ?? [];
        const rows: TemplateRow[] = lines.map((l, i) => ({
            key: `${t.id ?? "t"}-${l.id ?? i}`,
            kind: "line",
            showTemplate: i === 0,
            template: t,
            templateName: t.name ?? "",
            templateType: t.typeText ?? "",
            summary: l.summary ?? "",
            account: `${l.accountCode ?? ""} ${l.accountName ?? ""}`.trim(),
            direction: l.directionText ?? "",
        }));
        if (rows.length > 0) {
            rows.push({
                key: `${t.id ?? "t"}-total`,
                kind: "total",
                showTemplate: false,
                template: t,
                templateName: t.name ?? "",
                templateType: t.typeText ?? "",
                summary: "合计",
                account: "零元整",
                direction: "",
            });
        }
        return rows;
    });
    if (creating) {
        return (
            <Page>
                <ToolRow
                    left={
                        <>
                            <span className="text-[13px] text-(--foreground)">模板名称：</span>
                            <TextInput value={name} onChange={setName} placeholder="请输入模板名称" className="w-[150px]" />
                            <span className="text-[13px] text-(--foreground)">模板类型：</span>
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
                <div className="mx-auto max-w-[1050px] w-full flex-1 px-4 py-4">
                    <AntTable
                        pagination={false}
                        rowKey={(r) => r.n}
                        scroll={{ x: "max-content" }}
                        dataSource={[1, 2, 3, 4].map((n) => ({
                            n,
                            account:
                                n === 1 && subjectAccounts.length > 0
                                    ? `${subjectAccounts[0].code} ${subjectAccounts[0].name}`
                                    : "",
                        }))}
                        columns={[
                            { title: "序号", width: 50, align: "center", render: (_, r) => r.n },
                            {
                                title: "摘要",
                                width: 220,
                                render: () => <span className="block h-7 w-full rounded border border-(--border)" />,
                            },
                            {
                                title: "会计科目",
                                render: (_, r) =>
                                    r.account ? (
                                        <span className="block h-7 w-full truncate rounded border border-(--border) px-2 text-[13px] leading-7 text-(--foreground)">
                                            {r.account}
                                        </span>
                                    ) : (
                                        <span className="block h-7 w-full rounded border border-(--border)" />
                                    ),
                            },
                            { title: <AmountHeader title="借方金额" />, render: () => <AmountSlots /> },
                            { title: <AmountHeader title="贷方金额" />, render: () => <AmountSlots /> },
                        ]}
                        summary={() => (
                            <AntTable.Summary.Row>
                                <AntTable.Summary.Cell index={0} colSpan={3} className="text-[13px] text-(--foreground)">
                                    合计：零元整
                                </AntTable.Summary.Cell>
                                <AntTable.Summary.Cell index={3} className="bg-(--table-zebra-bg)" />
                                <AntTable.Summary.Cell index={4} className="bg-(--table-zebra-bg)" />
                            </AntTable.Summary.Row>
                        )}
                    />
                    <div className="py-4 text-[12px] leading-5 text-(--muted-foreground)">
                        提示：新建默认生成「日常支出」类型、首行取第一个末级科目的模板；行编辑与金额栏随迭代开放。
                    </div>
                </div>
            </Page>
        );
    }
    return (
        <Page>
            <ToolRow
                left={
                    <>
                        <Btn variant="primary" onClick={() => void openCreate()}>新建</Btn>
                        <ExportBtn
                            disabled={busy}
                            title="导出全部凭证模板为 .xlsx（导出后可直接修改再导回）"
                            onClick={() => void exportTemplates()}
                        >
                            导出
                        </ExportBtn>
                        <Btn disabled={busy} title="从 .xlsx 导入凭证模板（同名模板跳过）" onClick={() => importRef.current?.click()}>
                            导入
                        </Btn>
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
                    </>
                }
                right={<RefreshIcon onClick={() => void load()} />}
            />
            <div className="flex-1 px-4">
                <AntTable
                    pagination={false}
                    rowKey={(r) => r.key}
                    dataSource={templateRows}
                    scroll={{ x: "max-content" }}
                    locale={{ emptyText: loading ? "加载中…" : "暂无数据" }}
                    rowClassName={(r) => (r.kind === "total" ? "bg-(--table-zebra-bg)" : "")}
                    columns={[
                        {
                            title: "模板名称",
                            render: (_, r) =>
                                r.showTemplate ? (
                                    <span className="text-[13px] text-(--foreground)">
                                        <span className="font-bold">{r.templateName}</span>
                                        <span className="ml-4 font-bold">模板类型:</span> {r.templateType}
                                        <span className="ml-4 inline-flex align-middle">
                                            <LinkAction danger onClick={() => void remove(r.template)}>删除</LinkAction>
                                        </span>
                                    </span>
                                ) : null,
                        },
                        { title: "摘要", render: (_, r) => r.summary },
                        { title: "科目", render: (_, r) => r.account },
                        { title: "借贷方向", align: "center", render: (_, r) => r.direction },
                        { title: "借方金额", align: "right", render: () => null },
                        { title: "贷方金额", align: "right", render: () => null },
                    ]}
                />
            </div>
            <Pager total={templates.length} />
            {/* 导入结果：成功 / 跳过模板数与失败明细 */}
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
                    <div className="flex flex-col gap-2 px-1 py-1 text-[13px] text-(--foreground)">
                        <div>
                            新增 <b>{importResult.imported ?? 0}</b> 个模板，跳过同名 <b>{importResult.skipped ?? 0}</b> 个。
                        </div>
                        {(importResult.failures?.length ?? 0) > 0 && (
                            <div className="flex flex-col gap-1">
                                <span className="text-(--muted-foreground)">失败明细：</span>
                                <ul className="ml-4 max-h-[220px] list-disc overflow-auto text-[12.5px] text-(--foreground)">
                                    {(importResult.failures ?? []).map((f, i) => (
                                        <li key={i}>
                                            第 {f.row} 行：{f.reason}
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        )}
                        <LinkAction
                            title="下载凭证模板导入模板 .xlsx"
                            onClick={() => void settingsDownload(`/api/accountsets/${accountSetId}/vouchertemplates/import-template`, token ?? "", "凭证模板导入模板.xlsx")}
                        >
                            下载导入模板
                        </LinkAction>
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 权限设置（已接后端：账套角色 + 成员） ---------- */

/** 核心业务模块：权限汇总时排在最前，只勾「查看」的合并成一段。 */
const CORE_MODULE_KEYS = new Set(["vouchers", "funds", "invoices", "salary", "assets", "tax", "closing", "ledgers", "reports"]);

/** 与后端 RolePermissionCatalog.BuildSummary 同口径，用于角色编辑弹窗的实时预览。 */
function summarizePermissions(modules: PermissionModuleDto[], permissions: string[]): string {
    const granted = new Set(permissions);
    if (granted.size === 0) return "无权限";

    const parts: string[] = [];
    const viewOnly: string[] = [];
    for (const module of modules.filter((m) => CORE_MODULE_KEYS.has(m.key ?? ""))) {
        const actions = (module.actions ?? []).filter((a) => granted.has(`${module.key}.${a.key}`));
        if (actions.length === 0) continue;
        if ((module.actions?.length ?? 0) > 1 && actions.length === 1 && actions[0].key === "view") {
            viewOnly.push(module.name ?? "");
        } else {
            parts.push(module.name ?? "");
        }
    }
    if (viewOnly.length > 0) {
        parts.push(
            viewOnly.length === 1
                ? `查看${viewOnly[0]}`
                : `查看${viewOnly.slice(0, -1).join("、")}和${viewOnly[viewOnly.length - 1]}`,
        );
    }
    for (const module of modules.filter((m) => !CORE_MODULE_KEYS.has(m.key ?? ""))) {
        if ((module.actions ?? []).some((a) => granted.has(`${module.key}.${a.key}`))) {
            parts.push(module.name ?? "");
        }
    }
    return parts.join(" | ");
}

type RoleDraft = { id: string; name: string; isSystem: boolean; isAdmin: boolean; permissions: string[] };

const emptyRoleDraft = (): RoleDraft => ({ id: "", name: "", isSystem: false, isAdmin: false, permissions: [] });

const emptyMemberForm = (roleId = ""): SaveAccountSetMemberRequest => ({ name: "", phone: "", email: "", roleId });

export function LemonPermission() {
    const { accountSetId, token } = useAccountContext();
    const accountSet = useCurrentAccountSet();
    const [roles, setRoles] = useState<AccountSetRoleDto[]>([]);
    const [catalog, setCatalog] = useState<PermissionModuleDto[]>([]);
    const [members, setMembers] = useState<AccountSetMemberDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    const [editing, setEditing] = useState<AccountSetMemberDto | null>(null);
    const [form, setForm] = useState<SaveAccountSetMemberRequest>(emptyMemberForm());
    const [saving, setSaving] = useState(false);
    const [roleDraft, setRoleDraft] = useState<RoleDraft | null>(null);
    const [roleSaving, setRoleSaving] = useState(false);

    const load = useCallback(async () => {
        if (!accountSetId) return;
        setLoading(true);
        try {
            const roleList = await new RolesApi(apiConfig(token)).accountsetsAccountSetIdRolesGet({ accountSetId });
            setRoles(roleList);
            setMembers(await new MembersApi(apiConfig(token)).accountsetsAccountSetIdMembersGet({ accountSetId }));
        } catch (err) {
            setToast(await apiErrorMessage(err, "加载成员与角色失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void load();
    }, [load]);

    useEffect(() => {
        if (!accountSetId) return;
        let cancelled = false;
        void (async () => {
            try {
                const list = await new RolesApi(apiConfig(token)).accountsetsAccountSetIdRolesCatalogGet({ accountSetId });
                if (!cancelled) setCatalog(list);
            } catch (err) {
                if (!cancelled) setToast(await apiErrorMessage(err, "加载权限目录失败。"));
            }
        })();
        return () => {
            cancelled = true;
        };
    }, [accountSetId, token]);

    // 首次加载或角色被删除后，保证表单里选中的角色仍然有效；新增成员默认给第一个非管理员角色。
    useEffect(() => {
        if (roles.length === 0) return;
        setForm((current) =>
            roles.some((r) => r.id === current.roleId)
                ? current
                : { ...current, roleId: roles.find((r) => !r.isAdmin)?.id ?? roles[0].id ?? "" },
        );
    }, [roles]);

    function resetForm() {
        setEditing(null);
        setForm(emptyMemberForm(roles.find((r) => !r.isAdmin)?.id ?? roles[0]?.id ?? ""));
    }

    function startEdit(m: AccountSetMemberDto) {
        setEditing(m);
        setForm({ name: m.name ?? "", phone: m.phone ?? "", email: m.email ?? "", roleId: m.roleId ?? "" });
    }

    async function save() {
        const name = (form.name ?? "").trim();
        const phone = (form.phone ?? "").trim();
        if (!name) {
            setToast("请填写成员姓名。");
            return;
        }
        if (!editing && !/^1\d{10}$/.test(phone)) {
            setToast("请填写 11 位手机号。");
            return;
        }
        if (phone && !/^1\d{10}$/.test(phone)) {
            setToast("手机号需为 11 位数字且以 1 开头。");
            return;
        }
        if (!form.roleId) {
            setToast("请选择角色。");
            return;
        }
        setSaving(true);
        try {
            const api = new MembersApi(apiConfig(token));
            const body: SaveAccountSetMemberRequest = { name, phone, email: (form.email ?? "").trim(), roleId: form.roleId };
            if (editing) {
                await api.accountsetsAccountSetIdMembersIdPut({ accountSetId, id: editing.id ?? "", saveAccountSetMemberRequest: body });
            } else {
                await api.accountsetsAccountSetIdMembersPost({ accountSetId, saveAccountSetMemberRequest: body });
            }
            resetForm();
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "保存成员失败。"));
        } finally {
            setSaving(false);
        }
    }

    async function toggle(m: AccountSetMemberDto) {
        try {
            await new MembersApi(apiConfig(token)).accountsetsAccountSetIdMembersIdEnabledPut({
                accountSetId,
                id: m.id ?? "",
                toggleMemberRequest: { enabled: !m.enabled },
            });
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "切换启用状态失败。"));
        }
    }

    async function remove(m: AccountSetMemberDto) {
        if (!window.confirm(`确定删除成员「${m.name}」吗？`)) return;
        try {
            await new MembersApi(apiConfig(token)).accountsetsAccountSetIdMembersIdDelete({ accountSetId, id: m.id ?? "" });
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "删除成员失败。"));
        }
    }

    function openRoleEditor(role: AccountSetRoleDto) {
        setRoleDraft({
            id: role.id ?? "",
            name: role.name ?? "",
            isSystem: role.isSystem ?? false,
            isAdmin: role.isAdmin ?? false,
            permissions: [...(role.permissions ?? [])],
        });
    }

    function togglePermission(key: string, checked: boolean) {
        setRoleDraft(
            (draft) =>
                draft && {
                    ...draft,
                    permissions: checked
                        ? Array.from(new Set([...draft.permissions, key]))
                        : draft.permissions.filter((k) => k !== key),
                },
        );
    }

    function toggleModule(module: PermissionModuleDto, checked: boolean) {
        const keys = (module.actions ?? []).map((a) => `${module.key}.${a.key}`);
        setRoleDraft(
            (draft) =>
                draft && {
                    ...draft,
                    permissions: checked
                        ? Array.from(new Set([...draft.permissions, ...keys]))
                        : draft.permissions.filter((k) => !keys.includes(k)),
                },
        );
    }

    async function saveRole() {
        if (!roleDraft) return;
        const name = roleDraft.name.trim();
        if (!name) {
            setToast("请填写角色名称。");
            return;
        }
        if (roleDraft.permissions.length === 0) {
            setToast("请至少勾选一项权限。");
            return;
        }
        setRoleSaving(true);
        try {
            const api = new RolesApi(apiConfig(token));
            const body: SaveAccountSetRoleRequest = { name, permissions: roleDraft.permissions };
            if (roleDraft.id) {
                await api.accountsetsAccountSetIdRolesIdPut({ accountSetId, id: roleDraft.id, saveAccountSetRoleRequest: body });
            } else {
                await api.accountsetsAccountSetIdRolesPost({ accountSetId, saveAccountSetRoleRequest: body });
            }
            setRoleDraft(null);
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "保存角色失败。"));
        } finally {
            setRoleSaving(false);
        }
    }

    async function removeRole() {
        if (!roleDraft?.id) return;
        if (!window.confirm(`确定删除角色「${roleDraft.name}」吗？`)) return;
        setRoleSaving(true);
        try {
            await new RolesApi(apiConfig(token)).accountsetsAccountSetIdRolesIdDelete({ accountSetId, id: roleDraft.id });
            setRoleDraft(null);
            await load();
        } catch (err) {
            setToast(await apiErrorMessage(err, "删除角色失败。"));
        } finally {
            setRoleSaving(false);
        }
    }

    const accountSetName = accountSet?.companyName || accountSet?.name || "—";

    return (
        <Page>
            <ToolRow right={<RefreshIcon onClick={() => void load()} />} />
            <div className="mx-auto w-full max-w-[900px] px-4 pb-4">
                <div className="rounded-xl border border-(--border) bg-(--card) px-6 py-5">
                    <div className="mb-5 flex items-center justify-between">
                        <span className="text-[13px] text-(--foreground)">
                            当前账套：<span className="text-(--foreground)">{accountSetName}</span>
                        </span>
                        <span className="text-[12.5px] text-(--muted-foreground)">
                            {editing ? `正在编辑：${editing.name ?? ""}` : "新增成员"}
                        </span>
                    </div>

                    <div className="flex flex-col gap-3">
                        <label className="flex items-center gap-3">
                            <span className="w-[88px] text-right text-[13px] text-(--muted-foreground)">成员姓名：</span>
                            <TextInput
                                value={form.name ?? ""}
                                onChange={(v) => setForm({ ...form, name: v })}
                                placeholder="请输入成员姓名"
                                className="flex-1"
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[88px] text-right text-[13px] text-(--muted-foreground)">
                                <span className="mr-0.5 text-(--danger)">*</span>手机用户：
                            </span>
                            <TextInput
                                value={form.phone ?? ""}
                                onChange={(v) => setForm({ ...form, phone: v })}
                                placeholder="请输入 11 位手机号"
                                className="flex-1"
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[88px] text-right text-[13px] text-(--muted-foreground)">邮箱：</span>
                            <TextInput
                                value={form.email ?? ""}
                                onChange={(v) => setForm({ ...form, email: v })}
                                placeholder="可留空"
                                className="flex-1"
                            />
                        </label>
                        <div className="flex items-start gap-3">
                            <span className="mt-2 w-[88px] shrink-0 text-right text-[13px] text-(--muted-foreground)">角色权限：</span>
                            <div className="min-w-0 flex-1">
                                {roles.length === 0 ? (
                                    <div className="py-2 text-[13px] text-(--muted-foreground)">{loading ? "加载中…" : "暂无角色"}</div>
                                ) : (
                                    <Radio.Group
                                        className="flex w-full flex-col gap-1"
                                        value={form.roleId}
                                        onChange={(e) => setForm({ ...form, roleId: String(e.target.value) })}
                                    >
                                        {roles.map((role) => (
                                            <div key={role.id} className="flex items-start gap-2">
                                                <Radio
                                                    value={role.id ?? ""}
                                                    disabled={Boolean(editing?.isOwner) && !role.isAdmin}
                                                >
                                                    <span className="text-[13px] text-(--foreground)">{role.name}</span>
                                                </Radio>
                                                <span className="min-w-0 flex-1 pt-[5px] text-[12.5px] leading-5 text-(--muted-foreground)">
                                                    {role.permissionText}
                                                </span>
                                                {!role.isAdmin && (
                                                    <LinkAction className="shrink-0" onClick={() => openRoleEditor(role)}>
                                                        编辑
                                                    </LinkAction>
                                                )}
                                            </div>
                                        ))}
                                    </Radio.Group>
                                )}
                                <div className="mt-1">
                                    <LinkAction onClick={() => setRoleDraft(emptyRoleDraft())}>+ 新增角色</LinkAction>
                                </div>
                                {editing?.isOwner && (
                                    <div className="mt-1 text-[12px] text-(--muted-foreground)">创建者固定为账套管理员，不能更换角色。</div>
                                )}
                            </div>
                        </div>
                    </div>

                    <div className="mt-5 flex items-center gap-3 pl-[100px]">
                        <Btn variant="primary" onClick={() => void save()} disabled={saving}>
                            {saving ? "保存中…" : "保存"}
                        </Btn>
                        <Btn onClick={resetForm}>取消</Btn>
                    </div>
                </div>

                <div className="mt-6 rounded-xl border border-(--border) bg-(--card) px-4 py-3">
                    <div className="mb-2 text-[13px] font-medium text-(--foreground)">已加入成员</div>
                    <AntTable<AccountSetMemberDto>
                        rowKey={(m) => m.id ?? ""}
                        pagination={false}
                        dataSource={members}
                        locale={{ emptyText: loading ? "加载中…" : "暂无数据" }}
                        columns={[
                            {
                                title: "用户",
                                width: 200,
                                render: (_, m) => (
                                    <span>
                                        <span className="font-medium text-(--foreground)">{m.name}</span>
                                        {m.isOwner && (
                                            <span className="ml-2 rounded bg-(--brand-soft) px-1.5 py-0.5 text-[11px] text-(--brand)">创建者</span>
                                        )}
                                        {m.email && <span className="ml-2 text-[12px] text-(--muted-foreground)">{m.email}</span>}
                                    </span>
                                ),
                            },
                            { title: "手机号", width: 110, render: (_, m) => m.phone || "—" },
                            {
                                title: "权限",
                                render: (_, m) => (
                                    <span>
                                        <span className="mr-2 text-(--foreground)">{m.roleName}</span>
                                        <span className="text-[12px] text-(--muted-foreground)">{m.permissionText}</span>
                                    </span>
                                ),
                            },
                            {
                                title: "状态",
                                width: 70,
                                render: (_, m) => (
                                    <span className={m.enabled ? "text-(--success)" : "text-(--muted-foreground)"}>
                                        {m.enabled ? "启用" : "停用"}
                                    </span>
                                ),
                            },
                            {
                                title: "操作",
                                width: 160,
                                render: (_, m) =>
                                    m.isOwner ? (
                                        <span className="text-[12px] text-(--muted-foreground)">不可停用 / 删除</span>
                                    ) : (
                                        <div className="flex items-center gap-3">
                                            <LinkAction onClick={() => startEdit(m)}>编辑</LinkAction>
                                            <LinkAction onClick={() => void toggle(m)}>{m.enabled ? "停用" : "启用"}</LinkAction>
                                            <LinkAction danger onClick={() => void remove(m)}>删除</LinkAction>
                                        </div>
                                    ),
                            },
                        ]}
                    />
                    <div className="mt-4 flex items-center gap-2 rounded-xl bg-(--table-zebra-bg) px-4 py-3 text-[13px] text-(--muted-foreground)">
                        <span className="text-(--warn-icon)">⚠</span>
                        成员按角色共享同一账套数据；创建者固定为账套管理员，不可停用或删除。
                    </div>
                </div>
            </div>

            {roleDraft && (
                <Dialog
                    title={roleDraft.id ? `编辑角色：${roleDraft.name}` : "新增角色"}
                    width={720}
                    onClose={() => setRoleDraft(null)}
                    footer={
                        <div className="flex items-center justify-between">
                            <span>
                                {roleDraft.id && !roleDraft.isSystem && (
                                    <LinkAction danger disabled={roleSaving} onClick={() => void removeRole()}>
                                        删除角色
                                    </LinkAction>
                                )}
                            </span>
                            <span className="flex items-center gap-2">
                                <Btn onClick={() => setRoleDraft(null)}>取消</Btn>
                                <Btn variant="primary" disabled={roleSaving} onClick={() => void saveRole()}>
                                    {roleSaving ? "保存中…" : "保存"}
                                </Btn>
                            </span>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3">
                        <label className="flex items-center gap-3">
                            <span className="w-[70px] text-right text-[13px] text-(--muted-foreground)">角色名称：</span>
                            <TextInput
                                value={roleDraft.name}
                                onChange={(v) => setRoleDraft({ ...roleDraft, name: v })}
                                placeholder="如：审核员"
                                disabled={roleDraft.isSystem}
                                className="flex-1"
                            />
                        </label>
                        {roleDraft.isSystem && (
                            <div className="pl-[82px] text-[12px] text-(--muted-foreground)">预置角色名称不可修改，可调整权限。</div>
                        )}
                        <div className="rounded-lg bg-(--table-zebra-bg) px-3 py-2 text-[12.5px] leading-5 text-(--muted-foreground)">
                            权限说明：{summarizePermissions(catalog, roleDraft.permissions)}
                        </div>
                        <div className="max-h-[380px] overflow-auto rounded-lg border border-(--border) px-3">
                            {catalog.length === 0 ? (
                                <div className="py-4 text-center text-[13px] text-(--muted-foreground)">权限目录加载中…</div>
                            ) : (
                                catalog.map((module) => {
                                    const actions = module.actions ?? [];
                                    const moduleKeys = actions.map((a) => `${module.key}.${a.key}`);
                                    const allChecked = moduleKeys.length > 0 && moduleKeys.every((k) => roleDraft.permissions.includes(k));
                                    return (
                                        <div
                                            key={module.key}
                                            className="flex flex-wrap items-center gap-x-4 gap-y-1 border-b border-(--border) py-2 last:border-b-0"
                                        >
                                            <span className="w-[92px] shrink-0 text-[13px] text-(--foreground)">{module.name}</span>
                                            <CheckBox
                                                label="全选"
                                                checked={allChecked}
                                                onChange={(checked) => toggleModule(module, checked)}
                                            />
                                            {actions.map((action) => {
                                                const key = `${module.key}.${action.key}`;
                                                return (
                                                    <CheckBox
                                                        key={key}
                                                        label={action.name}
                                                        checked={roleDraft.permissions.includes(key)}
                                                        onChange={(checked) => togglePermission(key, checked)}
                                                    />
                                                );
                                            })}
                                        </div>
                                    );
                                })
                            )}
                        </div>
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 老板看账（已接后端：财务概要本期 / 本年累计 / 同比 / 环比） ---------- */

const bossMoney = (value: number | null | undefined) =>
    (value ?? 0).toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const bossPct = (value: number | null | undefined) => {
    if (value === null || value === undefined) return "—";
    return `${value > 0 ? "+" : ""}${value.toFixed(2)}%`;
};

/** 经营成果（流量类）展示行。 */
const BOSS_PROFIT_ITEMS = ["营业收入", "营业成本", "期间费用", "利润总额", "净利润"];

/** 财务状况（存量类）展示行。 */
const BOSS_BALANCE_ITEMS = ["资产总额", "负债总额", "所有者权益", "应收款", "应付款", "应交税费"];

export function LemonBoss() {
    const { accountSetId, token } = useAccountContext();
    const base = useDefaultAccountingPeriod();
    const [year, setYear] = useState(base.year);
    const [month, setMonth] = useState(base.month);
    const [data, setData] = useState<FinancialSummaryDto | null>(null);
    const [loading, setLoading] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    const load = useCallback(async () => {
        if (!accountSetId) return;
        setLoading(true);
        try {
            setData(await new ReportsApi(apiConfig(token)).accountsetsAccountSetIdReportsFinancialSummaryGet({ accountSetId, year, month }));
        } catch (err) {
            setToast(await apiErrorMessage(err, "加载财务概要失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, year, month]);

    useEffect(() => {
        void load();
    }, [load]);

    function shift(delta: number) {
        const total = year * 12 + (month - 1) + delta;
        setYear(Math.floor(total / 12));
        setMonth((total % 12) + 1);
    }

    const row = (item: string) => (data?.rows ?? []).find((r) => r.item === item);
    const current = (item: string) => row(item)?.currentAmount ?? 0;
    const ytd = (item: string) => row(item)?.yearToDateAmount ?? 0;

    const revenue = current("营业收入");
    const netProfit = current("净利润");
    const cashNet = current("资金收支");
    const debtRatio = current("资产负债率(%)");
    const revenueMom = row("营业收入")?.currentMomPercent;
    const revenueYoy = row("营业收入")?.currentYoyPercent;
    const profitYtd = ytd("净利润");
    const revenueYtd = ytd("营业收入");
    const cashIn = current("资金收入");
    const cashOut = current("资金支付");

    const cards = [
        { title: "本月营业收入", value: bossMoney(revenue), hint: `环比 ${bossPct(revenueMom)} · 同比 ${bossPct(revenueYoy)}`, color: "var(--brand)" },
        { title: "本月净利润", value: bossMoney(netProfit), hint: `本年累计 ${bossMoney(profitYtd)}`, color: netProfit >= 0 ? "var(--tone-green)" : "var(--tone-red)" },
        { title: "本月资金收支", value: bossMoney(cashNet), hint: `收 ${bossMoney(cashIn)} · 付 ${bossMoney(cashOut)}`, color: cashNet >= 0 ? "var(--tone-green)" : "var(--tone-red)" },
        { title: "资产负债率", value: `${(debtRatio ?? 0).toFixed(2)}%`, hint: `资产 ${bossMoney(current("资产总额"))}`, color: "var(--brand)" },
    ];

    const verdict = [
        cashNet >= 0
            ? `本月资金净流入 ${bossMoney(cashNet)} 元，账面资金相对宽裕。`
            : `本月资金净流出 ${bossMoney(Math.abs(cashNet))} 元，需关注回款与付款节奏。`,
        netProfit >= 0
            ? `本月实现净利润 ${bossMoney(netProfit)} 元，收入利润率约 ${revenue === 0 ? "—" : `${((netProfit / revenue) * 100).toFixed(2)}%`}。`
            : `本月亏损 ${bossMoney(Math.abs(netProfit))} 元，建议复核成本与期间费用。`,
        `截至本月底应收款 ${bossMoney(current("应收款"))} 元、应付款 ${bossMoney(current("应付款"))} 元，应交税费 ${bossMoney(current("应交税费"))} 元。`,
    ];

    return (
        <Page>
            <ToolRow
                left={
                    <PeriodStepper
                        text={`${year}年${month}月`}
                        onPrev={() => shift(-1)}
                        onNext={() => shift(1)}
                        value={{ year, month }}
                        onPick={(p) => {
                            setYear(p.year);
                            setMonth(p.month);
                        }}
                    />
                }
                right={<RefreshIcon onClick={() => void load()} />}
            />

            <div className="mx-auto max-w-[1040px] w-full px-4 pb-6">
                <div className="grid grid-cols-4 gap-4">
                    {cards.map((c) => (
                        <div key={c.title} className="rounded-2xl border border-(--border) bg-(--card) p-5 shadow-[0_1px_3px_rgba(15,23,42,0.04)]">
                            <div className="text-[13px] text-(--muted-foreground)">{c.title}</div>
                            <div className="mt-2 text-[22px] font-bold tabular-nums" style={{ color: c.color }}>{c.value}</div>
                            <div className="mt-1 text-[12px] text-(--muted-foreground)">{c.hint}</div>
                        </div>
                    ))}
                </div>

                <div className="mt-4 grid grid-cols-3 gap-4">
                    <div className="col-span-2 rounded-2xl border border-(--border) bg-(--card) p-5">
                        <div className="flex items-center justify-between">
                            <span className="text-[14px] font-semibold text-(--foreground)">经营成果</span>
                            <span className="text-[12px] text-(--muted-foreground)">{data?.periodText ?? `${year}年${month}月`}</span>
                        </div>
                        <div className="mt-3">
                        <AntTable
                            pagination={false}
                            rowKey={(x) => x.item}
                            scroll={{ x: "max-content" }}
                            dataSource={BOSS_PROFIT_ITEMS.map((item) => ({ item, r: row(item) }))}
                            columns={[
                                { title: "项目", render: (_, x) => <span className={x.item === "净利润" || x.item === "利润总额" ? "font-medium" : ""}>{x.item}</span> },
                                { title: "本期", align: "right", render: (_, x) => bossMoney(x.r?.currentAmount) },
                                { title: "本年累计", align: "right", render: (_, x) => bossMoney(x.r?.yearToDateAmount) },
                                { title: "同比", align: "right", render: (_, x) => bossPct(x.r?.currentYoyPercent) },
                                { title: "环比", align: "right", render: (_, x) => bossPct(x.r?.currentMomPercent) },
                            ]}
                        />
                        </div>
                    </div>

                    <div className="rounded-2xl border border-(--border) bg-(--card) p-5">
                        <div className="text-[14px] font-semibold text-(--foreground)">财务状况（期末）</div>
                        <div className="mt-3 flex flex-col">
                            {BOSS_BALANCE_ITEMS.map((item) => (
                                <div key={item} className="flex items-center justify-between border-b border-(--border) py-2 last:border-b-0">
                                    <span className="text-[13px] text-(--muted-foreground)">{item}</span>
                                    <span className="text-[13px] font-medium tabular-nums text-(--foreground)">{bossMoney(current(item))}</span>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>

                <div className="mt-4 rounded-2xl border border-(--brand-border) bg-(--brand-soft) p-6">
                    <div className="text-[14px] font-semibold text-(--brand)">本月经营解读</div>
                    <div className="mt-3 flex flex-col gap-2 text-[13px] leading-6 text-(--foreground)">
                        {verdict.map((t) => (
                            <span key={t} className="flex gap-2">
                                <span className="text-(--brand)">•</span> {t}
                            </span>
                        ))}
                        <span className="mt-1 text-[12px] text-(--muted-foreground)">
                            本年累计收入 {bossMoney(revenueYtd)} 元，累计净利润 {bossMoney(profitYtd)} 元；以上数据均按已记账凭证实时计算。
                        </span>
                    </div>
                </div>

                {loading && <div className="mt-3 text-center text-[12px] text-(--muted-foreground)">加载中…</div>}
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 备份恢复（备份数据包已接后端：整账套 JSON 导出 + 恢复） ---------- */

const BACKUP_TABS = ["备份数据包", "归档Excel", "归档PDF", "文件下载中心", "审计对接"];

/** 二进制下载：fetch + blob + a[download]，返回错误文案（成功返回 null）。 */
async function settingsDownload(url: string, token: string, fallbackName: string): Promise<string | null> {
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
async function settingsUpload<T>(url: string, token: string, file: File): Promise<{ data?: T; error?: string }> {
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

export function LemonBackup() {
    const { accountSetId, token } = useAccountContext();
    const [tab, setTab] = useState(0);
    const period = useDefaultAccountingPeriod();
    const [busy, setBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    const [restoreFile, setRestoreFile] = useState<File | null>(null);
    const [restoreChecked, setRestoreChecked] = useState(false);
    const [restoreResult, setRestoreResult] = useState<BackupRestoreResultDto | null>(null);
    const [lastBackup, setLastBackup] = useState<string | null>(null);
    const [auditBusy, setAuditBusy] = useState(false);
    const [auditSoftware, setAuditSoftware] = useState("鼎信诺");
    const [archiveBusy, setArchiveBusy] = useState(false);
    const fileRef = useRef<HTMLInputElement>(null);
    const isAudit = tab === 4;

    /** 归档导出：excel / pdf。 */
    const doArchive = async (kind: "excel" | "pdf") => {
        if (!token || !accountSetId || archiveBusy) {
            return;
        }
        setArchiveBusy(true);
        try {
            const err = await settingsDownload(`/api/accountsets/${accountSetId}/audit/archive/${kind}`, token, kind === "excel" ? "账套归档.xlsx" : "账套归档.pdf");
            if (err) {
                setToast(err);
            }
        } catch {
            setToast("归档失败，请稍后重试。");
        } finally {
            setArchiveBusy(false);
        }
    };

    /** 审计一键提取：导出该期间的多页签 Excel 取数包。 */
    const doAuditExtract = async () => {
        if (!token || !accountSetId || auditBusy) {
            return;
        }
        setAuditBusy(true);
        try {
            const suffix = `${period.year}${String(period.month).padStart(2, "0")}`;
            const err = await settingsDownload(
                `/api/accountsets/${accountSetId}/audit/extract?year=${period.year}&month=${period.month}`,
                token,
                `审计取数_${suffix}.xlsx`,
            );
            if (err) {
                setToast(err);
            }
        } catch {
            setToast("审计取数失败，请稍后重试。");
        } finally {
            setAuditBusy(false);
        }
    };

    /** 下载整账套备份数据包（JSON，含科目期初 / 凭证 / 发票 / 资产 / 费用单据等全部业务表）。 */
    const doBackup = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        setBusy(true);
        try {
            const err = await settingsDownload(`/api/accountsets/${accountSetId}/backup`, token, "账套备份.json");
            if (err) {
                setToast(err);
            } else {
                setLastBackup(new Date().toLocaleString("zh-CN"));
            }
        } finally {
            setBusy(false);
        }
    };

    /** 从数据包恢复：清空账套现有业务数据后按备份原样写回。 */
    const doRestore = async () => {
        if (!token || !accountSetId || !restoreFile || busy) {
            return;
        }
        if (!restoreChecked) {
            setToast("请先勾选风险确认。");
            return;
        }
        setBusy(true);
        try {
            const form = new FormData();
            form.append("file", restoreFile);
            const res = await fetch(`/api/accountsets/${accountSetId}/backup/restore`, {
                method: "POST",
                headers: { Authorization: `Bearer ${token}` },
                body: form,
            });
            const body = await res.json().catch(() => null);
            if (!res.ok) {
                const message = body && typeof body === "object" && "message" in body ? String((body as { message?: unknown }).message ?? "") : "";
                setToast(message || `恢复失败（HTTP ${res.status}）`);
            } else {
                setRestoreResult(body as BackupRestoreResultDto);
                setRestoreFile(null);
                setRestoreChecked(false);
            }
        } finally {
            setBusy(false);
        }
    };

    return (
        <Page>
            <SubTabs tabs={BACKUP_TABS} active={tab} onChange={setTab} />
            {tab === 0 && (
                <>
                    <ToolRow
                        left={
                            <>
                                <Btn variant="primary" disabled={busy} title="导出账套全部业务数据为 JSON 备份文件" onClick={() => void doBackup()}>
                                    开始备份
                                </Btn>
                                <Btn disabled={busy} title="从此前下载的备份数据包恢复（覆盖现有数据）" onClick={() => fileRef.current?.click()}>
                                    上传本地备份
                                </Btn>
                                <input
                                    ref={fileRef}
                                    type="file"
                                    accept=".json"
                                    className="hidden"
                                    onChange={(e) => {
                                        const file = e.target.files?.[0] ?? null;
                                        e.target.value = "";
                                        if (file) {
                                            setRestoreFile(file);
                                            setRestoreChecked(false);
                                        }
                                    }}
                                />
                            </>
                        }
                        right={
                            <span className="text-[13px] text-(--muted-foreground)">
                                {lastBackup ? `上次备份：${lastBackup}` : "尚未备份"}
                            </span>
                        }
                    />
                    <div className="mx-auto max-w-[980px] w-full flex-1 px-4">
                        <AntTable
                            pagination={false}
                            dataSource={[]}
                            scroll={{ x: "max-content" }}
                            locale={{ emptyText: <EmptyTip text="备份文件实时下载到本地，不在服务器留存；恢复时选择本地数据包即可" /> }}
                            columns={[
                                { title: "备份名称" },
                                { title: "日期" },
                                { title: "文件大小" },
                                { title: "操作人" },
                                { title: "操作" },
                            ]}
                        />
                        <div className="mt-8 flex flex-col gap-2 pb-8 text-[12px] leading-5 text-(--muted-foreground)">
                            <span>备份数据包 → “备份”整个账套的数据（✅支持下载到本地和数据恢复，⚪不支持在线预览）</span>
                            <span>归档Excel和归档PDF → 将整个账套的核心账簿归档成 Excel 或 PDF 文件（✅支持下载到本地，⚪不支持数据恢复）</span>
                        </div>
                    </div>
                </>
            )}
            {tab === 1 || tab === 2 ? (
                <>
                    <ToolRow
                        left={
                            <>
                                <Btn variant="primary" onClick={() => void doArchive(tab === 1 ? "excel" : "pdf")} disabled={archiveBusy}>
                                    {archiveBusy ? "归档中…" : "开始归档"}
                                </Btn>
                                <span className="text-[12px] text-(--muted-foreground)">
                                    {tab === 1 ? "导出多页签 Excel：归档说明 / 科目余额表 / 凭证清单 / 凭证分录" : "导出 PDF：封面 + 科目余额表 + 凭证分录总表"}
                                </span>
                            </>
                        }
                    />
                    <div className="mx-auto max-w-[980px] w-full rounded-2xl bg-(--table-zebra-bg) p-5 text-[13px] leading-7 text-(--foreground)">
                        <div className="font-bold">归档说明:</div>
                        <div>① 归档范围：账套基本信息、全部末级科目余额（建档期初 + 全部凭证累计发生）、全部凭证清单与分录；</div>
                        <div>② 归档文件实时生成并下载到本地，不在服务器留存；</div>
                        <div>③ 归档 ≠ 备份：归档文件用于查阅与打印，不能用于恢复数据；需要恢复请使用「备份数据包」。</div>
                    </div>
                </>
            ) : null}
            {tab === 3 && (
                <>
                    <WipNotice text="文件下载中心界面复刻，暂未接入文件存储；备份与归档文件均直接下载到本地。" />
                    <ToolRow left={<WipBtn>删除</WipBtn>} right={<RefreshIcon />} />
                    <div className="mx-auto max-w-[980px] w-full flex-1 px-4">
                        <AntTable
                            pagination={false}
                            dataSource={[]}
                            scroll={{ x: "max-content" }}
                            locale={{ emptyText: <EmptyTip /> }}
                            columns={[
                                { title: "" },
                                { title: "文件名称" },
                                { title: "来源类型" },
                                { title: "来源位置" },
                                { title: "日期" },
                                { title: "文件大小" },
                                { title: "操作人" },
                                { title: "操作" },
                            ]}
                        />
                    </div>
                </>
            )}
            {isAudit && (
                <div className="mx-auto mt-6 max-w-[1000px] w-full px-4">
                    <div className="flex min-h-[300px] items-center justify-between rounded-2xl border border-(--brand-border) bg-(--brand-soft) px-12 py-10">
                        <div>
                            <div className="text-[22px] font-bold text-(--foreground)">审计无忧，一键提取</div>
                            <div className="mt-2 text-[13px] text-(--muted-foreground)">按期间导出科目余额表、凭证清单与凭证分录，审计软件可直接取数。</div>
                            <div className="mt-4 flex items-center gap-2">
                                <span className="text-[13px] text-(--muted-foreground)">审计软件：</span>
                                <Select
                                    value={auditSoftware}
                                    onChange={setAuditSoftware}
                                    options={["鼎信诺", "用友审计", "金蝶审计", "通用 Excel"]}
                                    className="w-[160px]"
                                />
                            </div>
                            <div className="mt-2 flex items-center gap-2">
                                <span className="text-[13px] text-(--muted-foreground)">取数期间：</span>
                                <span className="rounded-md border border-(--border) bg-(--card) px-3 py-1.5 text-[13px] text-(--foreground)">
                                    {period.year}年{String(period.month).padStart(2, "0")}月
                                </span>
                                <span className="text-[12px] text-(--muted-foreground)">（期间取账套当前会计期间）</span>
                            </div>
                            <div className="mt-5">
                                <Btn variant="primary" onClick={() => void doAuditExtract()} disabled={auditBusy}>
                                    {auditBusy ? "生成中…" : "一键提取"}
                                </Btn>
                            </div>
                        </div>
                        <div className="flex flex-col gap-2 text-right text-[13px] text-(--muted-foreground)">
                            <span>安全授权，取数方便</span>
                            <span>科目余额表 / 凭证清单 / 凭证分录 / 取数说明</span>
                            <span>标准 xlsx，可直接导入 {auditSoftware}</span>
                        </div>
                    </div>
                </div>
            )}
            {/* 恢复风险确认 */}
            {restoreFile && !restoreResult && (
                <Dialog
                    title="确认恢复"
                    width={520}
                    onClose={() => setRestoreFile(null)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" disabled={busy} onClick={() => void doRestore()}>
                                确定恢复
                            </Btn>
                            <Btn onClick={() => setRestoreFile(null)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] leading-6 text-(--foreground)">
                        <div>
                            将从数据包 <b>{restoreFile.name}</b> 恢复账套数据：现有全部业务数据（凭证、发票、日记账、资产、费用单据、员工工资等）会被
                            <b className="text-(--danger)"> 清空后写回备份内容</b>，操作不可撤销。
                        </div>
                        <CheckBox
                            label="我已知晓恢复将覆盖账套现有数据，并确认数据包属于当前账套"
                            checked={restoreChecked}
                            onChange={setRestoreChecked}
                        />
                    </div>
                </Dialog>
            )}
            {/* 恢复结果 */}
            {restoreResult && (
                <Dialog
                    title="恢复完成"
                    width={520}
                    onClose={() => setRestoreResult(null)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => setRestoreResult(null)}>
                                确定
                            </Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-2 px-1 text-[13px] leading-6 text-(--foreground)">
                        <div>
                            共恢复 <b>{restoreResult.totalRows ?? 0}</b> 行业务数据
                            {restoreResult.backedUpAt ? `（备份时间：${new Date(restoreResult.backedUpAt).toLocaleString("zh-CN")}）` : ""}。
                        </div>
                        {Object.entries(restoreResult.restored ?? {})
                            .filter(([, n]) => n > 0)
                            .map(([k, n]) => (
                                <div key={k} className="text-(--muted-foreground)">
                                    {k}：{n} 行
                                </div>
                            ))}
                        <div className="text-(--muted-foreground)">结账期间进度已随备份还原，刷新页面后生效。</div>
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
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
        <Page>
            <ToolRow
                left={
                    <>
                        <DateInput value={start} onChange={setStart} className="w-[150px]" />
                        <span className="text-(--muted-foreground)">至</span>
                        <DateInput value={end} onChange={setEnd} className="w-[150px]" />
                        <TextInput value={keyword} onChange={setKeyword} placeholder="请输入日志的关键字搜索" className="w-[260px]" />
                        <Btn variant="primary" onClick={() => void load()}>查询</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            {error && <div className="px-4 text-[13px] text-(--danger)">{error}</div>}
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
            <Pager total={rows.length} />
        </Page>
    );
}

/* ---------- 关联云发票 / 关联进销存（能力引导页） ---------- */

/* ---------- 关联云发票 / 关联进销存（建设中：后端暂无外部系统关联能力） ---------- */

function RelationTable({ cols2 }: { cols2: string }) {
    const accountSet = useCurrentAccountSet();
    return (
        <AntTable
            pagination={false}
            rowKey={() => "rel"}
            scroll={{ x: "max-content" }}
            dataSource={[{ account: accountSet?.companyName || accountSet?.name || "账套" }]}
            columns={[
                { title: "关联状态", width: 130, render: () => <StatusPill tone="gray">未关联</StatusPill> },
                { title: "财务软件账套", width: 240, dataIndex: "account" },
                { title: cols2 },
                { title: "操作", width: 100, render: () => <span className="text-[12px] text-(--muted-foreground)">关联功能建设中 <WipTag /></span> },
            ]}
        />
    );
}

function StepCard({ title, desc }: { title: string; desc: string }) {
    return (
        <div className="flex-1 rounded-2xl border border-(--border) bg-(--card) p-5 text-center">
            <div className="mx-auto flex h-10 w-10 items-center justify-center rounded-full bg-(--brand-soft) text-[18px] text-(--brand)">✓</div>
            <div className="mt-3 text-[14px] font-bold text-(--foreground)">{title}</div>
            <div className="mt-2 text-[12px] leading-5 text-(--muted-foreground)">{desc}</div>
        </div>
    );
}

export function LemonEisRelation() {
    const [helpOpen, setHelpOpen] = useState(false);
    return (
        <Page>
            <WipNotice text="关联云发票界面复刻，尚未接入后端：发票关联与自动凭证能力待建设。" />
            <ToolRow
                left={
                    <button
                        type="button"
                        onClick={() => setHelpOpen(true)}
                        className="cursor-pointer text-[13px] text-(--brand) hover:underline"
                    >
                        查看帮助
                    </button>
                }
                right={<RefreshIcon />}
            />
            <div className="mx-auto max-w-[980px] w-full px-4 pb-8">
                <RelationTable cols2="云发票企业" />
                <div className="mt-8 mb-3 text-[14px] font-bold text-(--foreground)">操作指引:</div>
                <div className="flex items-center gap-2">
                    <StepCard title="关联云发票企业" desc="拥有财务软件的关联云发票权限，且是云发票的系统管理员才有权限设置关联" />
                    <span className="text-(--muted-foreground)">›</span>
                    <StepCard title="开具发票" desc="云发票支持立即开票、批量开票、单据开票、复制开票等多种开票方式" />
                    <span className="text-(--muted-foreground)">›</span>
                    <StepCard title="自动生成凭证" desc="选择发票数据生成凭证" />
                </div>
                <div className="mt-8 mb-6 flex h-[120px] items-center justify-between rounded-2xl bg-(--brand-soft) px-10">
                    <span className="text-[22px] font-bold text-(--brand)">云财发票</span>
                    <span className="text-[13px] text-(--muted-foreground)">一站式数电发票服务平台</span>
                    <WipBtn>立即使用 <WipTag /></WipBtn>
                </div>
            </div>
            {helpOpen && (
                <Dialog
                    title="关联云发票 · 使用帮助"
                    width={560}
                    onClose={() => setHelpOpen(false)}
                    footer={
                        <Btn variant="primary" onClick={() => setHelpOpen(false)}>
                            我知道了
                        </Btn>
                    }
                >
                    <ol className="m-0 flex list-decimal flex-col gap-2 pl-5 text-[13px] leading-6 text-(--foreground)">
                        <li>由云发票的系统管理员发起关联；操作人需要同时拥有财务软件的关联云发票权限。</li>
                        <li>关联后，云发票开具的销项发票可按开票日期同步到本系统发票池，无需再手工导入。</li>
                        <li>在「销项发票」中勾选已同步的发票即可生成凭证；凭证科目按发票方向与商品明细匹配。</li>
                        <li>解除关联后不再同步新发票，已生成的凭证与历史发票不受影响。</li>
                    </ol>
                    <div className="mt-4 rounded-lg bg-(--table-zebra-bg) px-3 py-2 text-[12px] leading-5 text-(--muted-foreground)">
                        当前版本尚未接入云发票关联接口，本页为流程示意；关联状态与「立即使用」暂不可用。
                    </div>
                </Dialog>
            )}
        </Page>
    );
}

export function LemonScmRelation() {
    const [helpOpen, setHelpOpen] = useState(false);
    return (
        <Page>
            <WipNotice text="关联进销存界面复刻，尚未接入后端：业财关联与凭证自动生成能力待建设。" />
            <ToolRow
                left={
                    <button
                        type="button"
                        onClick={() => setHelpOpen(true)}
                        className="cursor-pointer text-[13px] text-(--brand) hover:underline"
                    >
                        查看帮助
                    </button>
                }
                right={<RefreshIcon />}
            />
            <div className="mx-auto max-w-[980px] w-full px-4 pb-8">
                <RelationTable cols2="进销存账套" />
                <div className="mt-8 mb-3 text-[14px] font-bold text-(--foreground)">操作指引:</div>
                <div className="flex items-center gap-2">
                    <StepCard title="关联进销存账套" desc="只有同时拥有财务软件和进销存的账套管理员才有权限设置关联" />
                    <span className="text-(--muted-foreground)">›</span>
                    <StepCard title="设置进销存核算参数" desc="检查确定进销存核算参数的设置" />
                    <span className="text-(--muted-foreground)">›</span>
                    <StepCard title="完善进销存基础资料" desc="设置进销存基础资料与财务软件账套的会计科目之间的对应关系" />
                    <span className="text-(--muted-foreground)">›</span>
                    <StepCard title="自动生成凭证" desc="选择进销存单据生成凭证" />
                </div>
                <div className="mt-8 mb-6 flex h-[120px] items-center gap-8 rounded-2xl bg-(--brand-soft) px-10">
                    <span className="flex gap-2">
                        {["采购", "销售", "库存", "财务"].map((t) => (
                            <span key={t} className="rounded bg-(--card) px-3 py-1 text-[12px] text-(--brand)">{t}</span>
                        ))}
                    </span>
                    <span className="flex-1 text-[13px] leading-5 text-(--muted-foreground)">
                        财务与业务数据不同步，核对数据费事费力，还总出错？您只需一个：云财业财
                        <br />
                        业财税完美融合，全面理顺企业内外部流程
                        <br />
                        做更懂中国企业的业财税一体化系统
                    </span>
                    <WipBtn>前往业财体验 <WipTag /></WipBtn>
                </div>
            </div>
            {helpOpen && (
                <Dialog
                    title="关联进销存 · 使用帮助"
                    width={560}
                    onClose={() => setHelpOpen(false)}
                    footer={
                        <Btn variant="primary" onClick={() => setHelpOpen(false)}>
                            我知道了
                        </Btn>
                    }
                >
                    <ol className="m-0 flex list-decimal flex-col gap-2 pl-5 text-[13px] leading-6 text-(--foreground)">
                        <li>由同时拥有财务软件与进销存权限的账套管理员发起关联。</li>
                        <li>关联后先在进销存中确认核算参数，再把存货、客户、供应商等基础资料对应到财务账套的会计科目。</li>
                        <li>采购、销售、库存单据可按期间同步到本系统，勾选后按对应科目自动生成凭证。</li>
                        <li>解除关联不会删除已生成的凭证和历史单据，只停止后续同步。</li>
                    </ol>
                    <div className="mt-4 rounded-lg bg-(--table-zebra-bg) px-3 py-2 text-[12px] leading-5 text-(--muted-foreground)">
                        当前版本尚未接入进销存关联接口，本页为流程示意；关联状态与「前往业财体验」暂不可用。
                    </div>
                </Dialog>
            )}
        </Page>
    );
}

/* ---------- 旧账导入（已接后端：科目余额表 → 期初余额） ---------- */

export function LemonImportOld() {
    const { accountSetId, token } = useAccountContext();
    const [busy, setBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    const [result, setResult] = useState<OpeningBalanceImportResultDto | null>(null);
    const [fileName, setFileName] = useState<string | null>(null);
    const oldFileRef = useRef<HTMLInputElement>(null);

    async function downloadTemplate() {
        if (!token || !accountSetId) return;
        const err = await settingsDownload(`/api/accountsets/${accountSetId}/accounts/opening-balances/template`, token, "旧账导入_科目余额表.xlsx");
        if (err) setToast(err);
    }

    async function upload(file: File) {
        if (!token || !accountSetId) return;
        setBusy(true);
        setFileName(file.name);
        try {
            const { data, error } = await settingsUpload<OpeningBalanceImportResultDto>(
                `/api/accountsets/${accountSetId}/accounts/opening-balances/import`,
                token,
                file,
            );
            if (error || !data) {
                setToast(error ?? "导入失败。");
                return;
            }
            setResult(data);
        } catch {
            setToast("导入失败，请检查文件格式。");
        } finally {
            setBusy(false);
        }
    }

    return (
        <Page>
            {/* Page 卡片体 padding 为 0：纵向留白交给本容器的 padding + gap（子元素 margin 会穿透卡片体折叠出去），
                内容卡统一用 SectionCard，内边距由卡片组件自己承担。 */}
            <div className="flex flex-col gap-8 p-6">
                <div className="mx-auto w-full max-w-[1000px]"  >
                    <div className="mb-8 flex items-center justify-center">
                        {["选择导入方式", "下载模板并填写", "导入旧账数据"].map((t, i) => (
                            <div key={t} className="flex items-center">
                                <span className="flex items-center gap-2">
                                    <span className="flex h-7 w-7 items-center justify-center rounded-full bg-(--brand-fill) text-[13px] text-white">
                                        {i + 1}
                                    </span>
                                    <span className="text-[13px] text-(--brand)">{t}</span>
                                </span>
                                {i < 2 && <span className="mx-4 h-px w-24 bg-(--muted)" />}
                            </div>
                        ))}
                    </div>

                    <div className="flex flex-col items-center gap-4">
                        <div className="flex items-center gap-3 text-[13px] text-(--foreground)">
                            <span className="rounded bg-(--brand-soft) px-2 py-1 text-(--brand)">科目余额表导入</span>
                            <span>按科目编码把旧账余额写入本账套期初，支持借贷两列或单列余额 + 方向列。</span>
                        </div>
                        <div className="flex items-center gap-3">
                            <Btn onClick={() => void downloadTemplate()}>下载模板</Btn>
                            <Btn variant="primary" onClick={() => oldFileRef.current?.click()} disabled={busy}>
                                {busy ? "导入中…" : "选择文件导入"}
                            </Btn>
                            <input
                                ref={oldFileRef}
                                type="file"
                                accept=".xlsx,.xls"
                                className="hidden"
                                onChange={(e) => {
                                    const f = e.target.files?.[0];
                                    e.target.value = "";
                                    if (f) void upload(f);
                                }}
                            />
                        </div>
                        {fileName && <div className="text-[12px] text-(--muted-foreground)">已选择：{fileName}</div>}
                    </div>

                    {result && (
                        <div className="mt-8 rounded-xl border border-(--border) p-4">
                            <div className="flex flex-wrap items-center gap-6 text-[13px]">
                                <span className="text-(--foreground)">新增录入 <b className="text-(--foreground)">{result.imported ?? 0}</b> 个科目</span>
                                <span className="text-(--foreground)">覆盖原有余额 <b className="text-(--foreground)">{result.updated ?? 0}</b> 个科目</span>
                                <span className="text-(--foreground)">借方合计 <b className="tabular-nums text-(--foreground)">{Number(result.totalDebit ?? 0).toFixed(2)}</b></span>
                                <span className="text-(--foreground)">贷方合计 <b className="tabular-nums text-(--foreground)">{Number(result.totalCredit ?? 0).toFixed(2)}</b></span>
                                <span className={result.balanced ? "text-(--success)" : "text-(--danger)"}>
                                    {result.balanced ? "✓ 试算平衡" : "✗ 试算不平衡"}
                                </span>
                            </div>
                            {(result.failures ?? []).length > 0 && (
                                <div className="mt-4">
                                    <div className="mb-2 text-[13px] font-medium text-(--danger)">未导入 {result.failures?.length} 行：</div>
                                    <AntTable
                                        pagination={false}
                                        rowKey={(f, i) => `${f.row}-${i}`}
                                        dataSource={(result.failures ?? []).slice(0, 50)}
                                        columns={[
                                            { title: "行号", dataIndex: "row" },
                                            { title: "科目编码", render: (_, f) => f.code || "—" },
                                            { title: "原因", dataIndex: "reason" },
                                        ]}
                                    />
                                </div>
                            )}
                        </div>
                    )}

                    <div className="mt-8 flex justify-center gap-6 text-[12px] text-(--muted-foreground)">
                        <span>在线导入旧账数据（需对接旧系统接口，暂未开放）</span>
                        <span>导账工具一键导入（暂未开放）</span>
                        <span>科目余额表导账（已开放）</span>
                    </div>
                </div>

                <div className="mx-auto w-full max-w-[640px] rounded-2xl bg-(--table-zebra-bg) p-5 text-[13px] leading-7 text-(--foreground)">
                    <div className="font-bold">导入说明:</div>
                    <div>① 仅迁移科目期初余额，历史凭证与明细账不随本次导入迁移；</div>
                    <div>② 科目编码必须与本账套一致，编码不存在或非末级科目的行会被跳过并在结果中列出；</div>
                    <div>③ 导入后请在「期初余额」页核对试算平衡，再开始本期记账。</div>
                </div>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
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
        <Page>
            <div className="flex flex-col items-center gap-6 px-6 py-8">
                <h2 className="text-[18px] font-bold text-(--foreground)">重新初始化</h2>
                <div className="w-full max-w-[1000px] rounded-xl border border-(--border) px-8 py-8">
                    <div className="mx-auto flex w-full max-w-[640px] flex-col gap-5">
                        <Alert
                            type="error"
                            showIcon
                            title="重新初始化将彻底清空您在账套中录入的所有数据，并不可恢复，请慎重！！"
                        />
                        <div className="text-[14px] font-bold leading-6 text-(--foreground)">
                            您将要重新初始化账套：{accountSet?.name ?? "（加载中…）"}，以下数据将被永久删除：
                        </div>
                        <ol className="flex flex-col gap-2 rounded-xl bg-(--muted) p-4 text-[13px] leading-6 text-(--foreground)">
                            {[
                                "新增的所有会计科目、辅助核算等基础设置",
                                "所有已录入的凭证、发票、日记账、资产及初始化数据",
                                "已保存的凭证模板、数据透视表模板等个性化配置模板",
                                "如果您已结账，重新初始化后会退回到账套启用期间",
                            ].map((item, i) => (
                                <li key={item} className="flex gap-1.5">
                                    <span className="shrink-0 tabular-nums text-(--muted-foreground)">{i + 1}、</span>
                                    <span>{item}</span>
                                </li>
                            ))}
                        </ol>
                        <div className="flex justify-center pt-1">
                            <CheckBox
                                label="我已清楚重新初始化将会清空所有数据，并知晓操作风险"
                                checked={checked}
                                onChange={setChecked}
                            />
                        </div>
                        <div className="flex justify-center">
                            <Btn variant={checked && !busy ? "primary" : "disabled"} disabled={!checked || busy} onClick={() => void doReinit()}>
                                {busy ? "处理中…" : "重新初始化"}
                            </Btn>
                        </div>
                        {result && (
                            <div className="rounded-lg bg-(--success-soft) px-4 py-3 text-center text-[13px] leading-6 text-(--success)">
                                {result}
                            </div>
                        )}
                    </div>
                </div>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
