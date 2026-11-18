/**
 * 复刻区资产模块：
 * 资产管理（初始化/列表/变更记录/计提折旧四页签 + 期初确认弹窗 + 折旧凭证设置）、
 * 折旧摊销明细表、资产汇总表（部门/类别双页签）、资产类别设置、资产核对总账，
 * 以及折旧凭证 / 变更凭证生成、资产导入导出与批量操作。
 * 视觉遵循全站云财蓝：主色 #273cf3、直角卡片、白卡 + slate 灰阶文字。
 */
import { useCallback, useEffect, useRef, useState } from "react";
import { Dropdown, Table as AntTable } from "antd";
import { DownOutlined } from "@ant-design/icons";

import { HelpTip } from "@/components/page/help-tip";
import { ICON_STYLE, FORM_CONTROL_W, FORM_LABEL_W } from "@/components/page/styles";

import {
    Btn,
    ExportBtn,
    PrintBtn,
    CheckBox,
    DateInput,
    Dialog,
    EmptyTip,
    HintBox,
    Page,
    Pager,
    PeriodStepper,
    RefreshIcon,
    Select,
    StateBar,
    SubTabs,
    Table,
    TextInput,
    Toast,
    ToolRow,
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import type { AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { usePeriodStepper } from "./components/usePeriodStepper";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { printPdf } from "@/lib/print-pdf";
import { fmtAmount } from "@/lib/format";
import {
    AccountsApi,
    FixedAssetsApi,
    type AccountDto,
    type ChangeVoucherPreviewDto,
    type DepreciationVoucherInfoDto,
    type FixedAssetCategoryDto,
    type FixedAssetChangeDto,
    type FixedAssetCheckRowDto,
    type FixedAssetDto,
    type FixedAssetImportResultDto,
    type FixedAssetVoucherSettingDto,
    type DepreciationLedgerRowDto,
    type FixedAssetSummaryRowDto,
} from "@/apis-gen";

/** 期间对象 → 「2026年10月」。 */
function periodLabel(p: AccountingPeriod): string {
    return `${p.year}年${p.month}月`;
}

/** 日期（Date 或字符串）→ yyyy-MM-dd。 */
function toDateInput(d: Date | string | null | undefined): string {
    return d ? new Date(d).toISOString().substring(0, 10) : "";
}

/** 资产属性展示文本 → 枚举。 */
const attrOf = (t?: string | null) => (t === "无形资产" ? 2 : t === "长期待摊费用" ? 3 : 1);

/** 资产模块接口前缀。 */
const assetBase = (accountSetId: string) => `/api/accountsets/${accountSetId}/fixedassets`;

/**
 * 打印资产表格：后端渲染 PDF 并在新窗口预览，前端只拼路径。
 * 失败时把可展示的中文文案交给页面的 toast（与导出按钮同样的提示位置）。
 */
async function printAssetSheet(
    token: string | null | undefined,
    accountSetId: string,
    path: string,
    setToast: (text: string) => void,
) {
    if (!token || !accountSetId) {
        return;
    }
    try {
        await printPdf(`${assetBase(accountSetId)}${path}`, token);
    } catch (e) {
        setToast(e instanceof Error ? e.message : "生成打印稿失败，请重试。");
    }
}

/** 二进制下载（xlsx）：fetch + blob + a[download]，返回错误文案（成功返回 null）。 */
async function downloadBlob(url: string, token: string, fallbackName: string): Promise<string | null> {
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

/** 上传 .xlsx 到导入端点，返回解析后的结果或错误文案。 */
async function uploadFile<T>(url: string, token: string, file: File): Promise<{ data?: T; error?: string }> {
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

/** 确认对话框（删除/清理）。 */
function ConfirmDialog({
    title,
    text,
    confirmText = "确定",
    busy,
    onClose,
    onConfirm,
}: {
    title: string;
    text: string;
    confirmText?: string;
    busy?: boolean;
    onClose: () => void;
    onConfirm: () => void;
}) {
    return (
        <Dialog
            title={title}
            onClose={onClose}
            footer={
                <div className="flex justify-center gap-3">
                    <Btn variant={busy ? "disabled" : "primary"} onClick={onConfirm}>
                        {confirmText}
                    </Btn>
                    <Btn onClick={onClose}>取消</Btn>
                </div>
            }
        >
            <div className="text-center text-[13px] leading-6 text-(--foreground)">{text}</div>
        </Dialog>
    );
}

/** 导入结果弹窗：成功条数 + 失败明细。 */
function ImportResultDialog({
    result,
    onClose,
}: {
    result: FixedAssetImportResultDto;
    onClose: () => void;
}) {
    const failures = result.failures ?? [];
    return (
        <Dialog
            title="导入结果"
            onClose={onClose}
            footer={
                <div className="flex justify-center">
                    <Btn variant="primary" onClick={onClose}>
                        知道了
                    </Btn>
                </div>
            }
        >
            <div className="px-1 text-[13px] leading-6 text-(--foreground)">
                <div>
                    新增 <b className="text-(--brand)">{result.imported ?? 0}</b> 条，更新{" "}
                    <b className="text-(--brand)">{result.updated ?? 0}</b> 条，失败{" "}
                    <b className={failures.length ? "text-(--danger)" : ""}>{failures.length}</b> 条。
                </div>
                {failures.length > 0 && (
                    // 纵向交给外层 max-h 滚动，横向交给 antd（scroll.x）；overflow-y-auto + overflow-x-hidden
                    // 避免外层 overflow-auto 把 overflow-x 也算成 auto，凭空多出一条横向滚动条。
                    <div className="mt-2 max-h-[240px] overflow-y-auto overflow-x-hidden rounded-lg border border-(--border)">
                        <AntTable
                            pagination={false}
                            scroll={{ x: "max-content" }}
                            columns={[
                                { title: "行号", dataIndex: "row", key: "row", width: 70, render: (v) => <span className="text-(--foreground)">{v}</span> },
                                { title: "失败原因", dataIndex: "reason", key: "reason", render: (v) => <span className="text-(--foreground)">{v}</span> },
                            ]}
                            dataSource={failures.map((f, i) => ({ key: i, row: f.row, reason: f.reason }))}
                        />
                    </div>
                )}
            </div>
        </Dialog>
    );
}

/** 账套上下文。 */
function useAccountContext() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    return { accountSetId, token };
}

/** 固定资产数据 Hook（类别不随期间；卡片传 period 时按该期间回算折旧，不传取当前口径）。 */
function useFixedAssetData(period?: number) {
    const { accountSetId, token } = useAccountContext();
    const [categories, setCategories] = useState<FixedAssetCategoryDto[]>([]);
    const [assets, setAssets] = useState<FixedAssetDto[]>([]);
    const [initialAssets, setInitialAssets] = useState<FixedAssetDto[]>([]);
    const [check, setCheck] = useState<FixedAssetCheckRowDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setLoading(true);
        const api = new FixedAssetsApi(apiConfig(token));
        try {
            const [cats, cards, initials, chk] = await Promise.all([
                api.accountsetsAccountSetIdFixedassetsCategoriesGet({ accountSetId }),
                api.accountsetsAccountSetIdFixedassetsAssetsGet({ accountSetId, period }),
                api.accountsetsAccountSetIdFixedassetsInitialAssetsGet({ accountSetId, period }),
                api.accountsetsAccountSetIdFixedassetsCheckGet({ accountSetId }),
            ]);
            setCategories(cats ?? []);
            setAssets(cards ?? []);
            setInitialAssets(initials ?? []);
            setCheck(chk ?? []);
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载资产数据失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, period]);

    useEffect(() => {
        void reload();
    }, [reload]);

    return { categories, assets, initialAssets, check, loading, error, reload };
}

/** 末级会计科目（凭证科目下拉共用）。 */
function useLeafAccounts() {
    const { token, accountSetId } = useAccountContext();
    const [accounts, setAccounts] = useState<AccountDto[]>([]);
    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        new AccountsApi(apiConfig(token))
            .accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true })
            .then((list) => setAccounts(list ?? []))
            .catch(() => setAccounts([]));
    }, [token, accountSetId]);
    return accounts;
}

/* ---------- 折旧 / 摊销凭证科目设置 ---------- */

function AssetVoucherSettingDialog({ onClose }: { onClose: () => void }) {
    const { token, accountSetId } = useAccountContext();
    const accounts = useLeafAccounts();
    const [setting, setSetting] = useState<FixedAssetVoucherSettingDto | null>(null);
    const [form, setForm] = useState({
        depreciationExpenseAccountId: "",
        depreciationAccountId: "",
        amortizationExpenseAccountId: "",
        amortizationAccountId: "",
        disposalAccountId: "",
    });
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        new FixedAssetsApi(apiConfig(token))
            .accountsetsAccountSetIdFixedassetsVoucherSettingGet({ accountSetId })
            .then((dto) => {
                setSetting(dto);
                setForm({
                    depreciationExpenseAccountId: dto.depreciationExpenseAccountId ?? "",
                    depreciationAccountId: dto.depreciationAccountId ?? "",
                    amortizationExpenseAccountId: dto.amortizationExpenseAccountId ?? "",
                    amortizationAccountId: dto.amortizationAccountId ?? "",
                    disposalAccountId: dto.disposalAccountId ?? "",
                });
            })
            .catch(async (e) => setToast(await apiErrorMessage(e, "加载凭证设置失败")));
    }, [token, accountSetId]);

    const save = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        setBusy(true);
        try {
            await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsVoucherSettingPut({
                accountSetId,
                saveFixedAssetVoucherSettingRequest: {
                    depreciationExpenseAccountId: form.depreciationExpenseAccountId || null,
                    depreciationAccountId: form.depreciationAccountId || null,
                    amortizationExpenseAccountId: form.amortizationExpenseAccountId || null,
                    amortizationAccountId: form.amortizationAccountId || null,
                    disposalAccountId: form.disposalAccountId || null,
                },
            });
            onClose();
        } catch (e) {
            setToast(await apiErrorMessage(e, "保存失败"));
        } finally {
            setBusy(false);
        }
    };

    const accountOptions = [
        { value: "", label: "（留空自动识别）" },
        ...accounts.map((a) => ({ value: a.id ?? "", label: `${a.code ?? ""} ${a.name ?? ""}` })),
    ];
    const rows: Array<[string, keyof typeof form, string?]> = [
        ["折旧费用科目：", "depreciationExpenseAccountId", setting?.depreciationExpenseAccountText ?? undefined],
        ["累计折旧科目：", "depreciationAccountId", setting?.depreciationAccountText ?? undefined],
        ["摊销费用科目：", "amortizationExpenseAccountId", setting?.amortizationExpenseAccountText ?? undefined],
        ["累计摊销科目：", "amortizationAccountId", setting?.amortizationAccountText ?? undefined],
        ["固定资产清理科目：", "disposalAccountId", setting?.disposalAccountText ?? undefined],
    ];
    return (
        <Dialog
            title="折旧 / 摊销凭证设置"
            width={560}
            onClose={onClose}
            footer={
                <div className="flex justify-center gap-3">
                    <Btn variant={busy ? "disabled" : "primary"} onClick={() => void save()}>
                        保存
                    </Btn>
                    <Btn onClick={onClose}>取消</Btn>
                </div>
            }
        >
            <div className="flex flex-col gap-3 px-1 text-[13px] text-(--foreground)">
                <div className="rounded-lg bg-(--brand-soft) px-3 py-2 text-[12.5px] leading-5 text-(--brand)">
                    留空的项由系统按科目编码自动识别（1602 累计折旧 / 1702 累计摊销 / 6602 折旧摊销费用 / 1606 固定资产清理）。
                </div>
                {rows.map(([label, key, autoText]) => (
                    <label key={String(key)} className="flex flex-col gap-1">
                        <span className="flex items-center gap-3">
                            <span className="w-[130px] shrink-0 text-right">{label}</span>
                            <Select
                                className={FORM_CONTROL_W}
                                value={form[key]}
                                onChange={(v) => setForm({ ...form, [key]: v })}
                                options={accountOptions}
                            />
                        </span>
                        {!form[key] && autoText && (
                            <span className="pl-[142px] text-[12.5px] text-(--muted-foreground)">自动：{autoText}</span>
                        )}
                    </label>
                ))}
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Dialog>
    );
}

/* ---------- 变更记录生成凭证 ---------- */

interface ChangeEntryRow {
    summary: string;
    accountId: string;
    debit: string;
    credit: string;
}

function ChangeVoucherDialog({
    change,
    onClose,
    onSaved,
}: {
    change: FixedAssetChangeDto;
    onClose: () => void;
    onSaved: () => void;
}) {
    const { token, accountSetId } = useAccountContext();
    const accounts = useLeafAccounts();
    const [preview, setPreview] = useState<ChangeVoucherPreviewDto | null>(null);
    const [rows, setRows] = useState<ChangeEntryRow[]>([]);
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    useEffect(() => {
        if (!token || !accountSetId || !change.id) {
            return;
        }
        new FixedAssetsApi(apiConfig(token))
            .accountsetsAccountSetIdFixedassetsChangesIdVoucherPreviewGet({ accountSetId, id: change.id })
            .then((dto) => {
                setPreview(dto);
                setRows(
                    (dto.entries ?? []).map((e) => ({
                        summary: e.summary ?? `${change.kindText ?? ""}`,
                        accountId: e.accountId ?? "",
                        debit: e.debit ? String(e.debit) : "",
                        credit: e.credit ? String(e.credit) : "",
                    })),
                );
            })
            .catch(async (e) => setToast(await apiErrorMessage(e, "加载建议分录失败")));
    }, [token, accountSetId, change.id, change.kindText]);

    const accountOptions = [
        { value: "", label: "（请选择科目）" },
        ...accounts.map((a) => ({ value: a.id ?? "", label: `${a.code ?? ""} ${a.name ?? ""}` })),
    ];
    const debitTotal = rows.reduce((s, r) => s + (Number(r.debit) || 0), 0);
    const creditTotal = rows.reduce((s, r) => s + (Number(r.credit) || 0), 0);

    const submit = async () => {
        if (!token || !accountSetId || !change.id || busy) {
            return;
        }
        if (rows.some((r) => !r.accountId)) {
            setToast("请为每一条分录选择会计科目");
            return;
        }
        if (Math.abs(debitTotal - creditTotal) > 0.005) {
            setToast("借贷不平衡，请检查金额");
            return;
        }
        setBusy(true);
        try {
            await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsChangesIdVoucherPost({
                accountSetId,
                id: change.id,
                generateChangeVoucherRequest: {
                    entries: rows.map((r) => ({
                        summary: r.summary,
                        accountId: r.accountId,
                        debit: Number(r.debit) || 0,
                        credit: Number(r.credit) || 0,
                    })),
                },
            });
            onSaved();
            onClose();
        } catch (e) {
            setToast(await apiErrorMessage(e, "生成凭证失败"));
        } finally {
            setBusy(false);
        }
    };

    const setRow = (i: number, patch: Partial<ChangeEntryRow>) =>
        setRows((prev) => prev.map((r, j) => (j === i ? { ...r, ...patch } : r)));

    return (
        <Dialog
            title={`生成凭证 · ${change.kindText ?? ""} · ${change.assetCode ?? ""} ${change.assetName ?? ""}`}
            width={720}
            onClose={onClose}
            footer={
                <div className="flex justify-center gap-3">
                    <Btn variant={busy ? "disabled" : "primary"} onClick={() => void submit()}>
                        生成凭证
                    </Btn>
                    <Btn onClick={onClose}>取消</Btn>
                </div>
            }
        >
            <div className="flex flex-col gap-2 px-1 text-[13px] text-(--foreground)">
                {preview?.hint && <HintBox>{preview.hint}</HintBox>}
                <AntTable<ChangeEntryRow & { __i: number }>
                    pagination={false}
                    rowKey={(_, i) => String(i)}
                    columns={[
                        {
                            title: "摘要",
                            dataIndex: "summary",
                            key: "summary",
                            render: (_, r) => (
                                <TextInput value={r.summary} onChange={(v) => setRow(r.__i, { summary: v })} />
                            ),
                        },
                        {
                            title: "会计科目",
                            dataIndex: "accountId",
                            key: "accountId",
                            render: (_, r) => (
                                <Select
                                    className="w-full"
                                    value={r.accountId}
                                    onChange={(v) => setRow(r.__i, { accountId: v })}
                                    options={accountOptions}
                                />
                            ),
                        },
                        {
                            title: "借方金额",
                            dataIndex: "debit",
                            key: "debit",
                            width: 120,
                            align: "right",
                            render: (_, r) => (
                                <TextInput
                                    className="text-right"
                                    value={r.debit}
                                    onChange={(v) => setRow(r.__i, { debit: v.replace(/[^\d.]/g, ""), credit: "" })}
                                />
                            ),
                        },
                        {
                            title: "贷方金额",
                            dataIndex: "credit",
                            key: "credit",
                            width: 120,
                            align: "right",
                            render: (_, r) => (
                                <TextInput
                                    className="text-right"
                                    value={r.credit}
                                    onChange={(v) => setRow(r.__i, { credit: v.replace(/[^\d.]/g, ""), debit: "" })}
                                />
                            ),
                        },
                    ]}
                    dataSource={rows.map((r, i) => ({ ...r, __i: i }))}
                />
                <div className="flex justify-end gap-6 pr-1 text-[12.5px] text-(--muted-foreground)">
                    <span>借方合计：{debitTotal.toFixed(2)}</span>
                    <span>贷方合计：{creditTotal.toFixed(2)}</span>
                </div>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Dialog>
    );
}

/* ---------- 复制资产 ---------- */

function CopyAssetDialog({
    assets,
    preset,
    onClose,
    onSaved,
}: {
    assets: FixedAssetDto[];
    preset: FixedAssetDto | null;
    onClose: () => void;
    onSaved: () => void;
}) {
    const { token, accountSetId } = useAccountContext();
    const [sourceId, setSourceId] = useState(preset?.id ?? assets[0]?.id ?? "");
    const [form, setForm] = useState({ code: "", name: "" });
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const source = assets.find((a) => a.id === sourceId);

    useEffect(() => {
        setForm({ code: "", name: source?.name ?? "" });
    }, [sourceId, source?.name]);

    const submit = async () => {
        if (!token || !accountSetId || !sourceId || busy) {
            return;
        }
        setBusy(true);
        try {
            await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsAssetsIdCopyPost({
                accountSetId,
                id: sourceId,
                copyFixedAssetRequest: { code: form.code.trim() || null, name: form.name.trim() || null },
            });
            onSaved();
            onClose();
        } catch (e) {
            setToast(await apiErrorMessage(e, "复制资产失败"));
        } finally {
            setBusy(false);
        }
    };

    return (
        <Dialog
            title="复制资产"
            onClose={onClose}
            footer={
                <div className="flex justify-center gap-3">
                    <Btn variant={busy ? "disabled" : "primary"} onClick={() => void submit()}>
                        保存
                    </Btn>
                    <Btn onClick={onClose}>取消</Btn>
                </div>
            }
        >
            <div className="flex flex-col gap-3 px-1 text-[13px] text-(--foreground)">
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>原资产：</span>
                    <Select
                        className={FORM_CONTROL_W}
                        value={sourceId}
                        onChange={setSourceId}
                        options={assets.map((a) => ({ value: a.id ?? "", label: `${a.code ?? ""} ${a.name ?? ""}` }))}
                    />
                </label>
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>新资产编号：</span>
                    <TextInput
                        className={FORM_CONTROL_W}
                        value={form.code}
                        onChange={(v) => setForm({ ...form, code: v })}
                        placeholder="留空自动追加序号"
                    />
                </label>
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>新资产名称：</span>
                    <TextInput className={FORM_CONTROL_W} value={form.name} onChange={(v) => setForm({ ...form, name: v })} />
                </label>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Dialog>
    );
}

/* ---------- 资产管理（四页签） ---------- */

const ASSET_TABS = ["资产初始化", "资产列表", "变更记录及生成凭证", "计提折旧及生成凭证"];

/** 变更记录列定义。 */
const CHANGE_COLS: Col[] = [
    { key: "kind", title: "变动单类别", width: "130px" },
    { key: "code", title: "资产编号", width: "120px" },
    { key: "name", title: "资产名称", width: "160px" },
    { key: "before", title: "变动前内容", width: "140px" },
    { key: "after", title: "变动后内容", width: "140px" },
    { key: "period", title: "变动期间", width: "120px" },
    { key: "ops", title: "关联凭证", width: "150px" },
];

export function AssetManage() {
    const [tab, setTab] = useState(1);
    const { period, stepper } = usePeriodStepper();
    const data = useFixedAssetData(period.period);
    const { token, accountSetId } = useAccountContext();
    const fileRef = useRef<HTMLInputElement>(null);

    const [initDialog, setInitDialog] = useState(false);
    const [showAdd, setShowAdd] = useState(false);
    const [addInitial, setAddInitial] = useState(false);
    const [addForm, setAddForm] = useState({ categoryId: "", name: "", model: "", dept: "", startDate: "", originValue: "", initialAccumulatedDepreciation: "" });
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    /** 显示所有信息：把部门、累计折旧、状态、清理日期等完整字段一并展开。 */
    const [showAll, setShowAll] = useState(false);

    // 勾选 / 批量 / 导入导出 / 复制 / 凭证设置
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [importResult, setImportResult] = useState<FixedAssetImportResultDto | null>(null);
    const [copyOpen, setCopyOpen] = useState<{ preset: FixedAssetDto | null } | null>(null);
    const [voucherSettingOpen, setVoucherSettingOpen] = useState(false);
    const [bulkClearOpen, setBulkClearOpen] = useState(false);
    const [bulkDeleteOpen, setBulkDeleteOpen] = useState(false);
    const [changeVoucher, setChangeVoucher] = useState<FixedAssetChangeDto | null>(null);

    // 折旧凭证状态（tab3）。
    const [depVoucher, setDepVoucher] = useState<DepreciationVoucherInfoDto | null>(null);
    const loadDepVoucher = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        try {
            setDepVoucher(
                await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsDepreciationVoucherGet({
                    accountSetId,
                    period: period.period,
                }),
            );
        } catch {
            setDepVoucher(null);
        }
    }, [accountSetId, token, period.period]);

    useEffect(() => {
        if (tab === 3) {
            void loadDepVoucher();
        }
    }, [tab, loadDepVoucher]);

    // 变更记录（tab2）按需拉取。
    const [changes, setChanges] = useState<FixedAssetChangeDto[]>([]);
    const [changesLoading, setChangesLoading] = useState(false);
    const loadChanges = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setChangesLoading(true);
        try {
            setChanges(await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsChangesGet({
                accountSetId,
                startPeriod: period.period,
                endPeriod: period.period,
            }));
        } catch (e) {
            setToast(await apiErrorMessage(e, "加载变更记录失败"));
        } finally {
            setChangesLoading(false);
        }
    }, [accountSetId, token, period.period]);
    useEffect(() => {
        if (tab === 2 || tab === 3) {
            void loadChanges();
        }
    }, [tab, loadChanges]);

    // 卡片操作：清理 / 删除。
    const [clearing, setClearing] = useState<FixedAssetDto | null>(null);
    const [deleting, setDeleting] = useState<FixedAssetDto | null>(null);

    const createAsset = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (!addForm.name.trim() || !addForm.categoryId || !addForm.startDate) {
            setToast("类别、名称、开始使用日期必填");
            return;
        }
        setBusy(true);
        try {
            const api = new FixedAssetsApi(apiConfig(token));
            const body = {
                saveFixedAssetRequest: {
                    name: addForm.name.trim(),
                    categoryId: addForm.categoryId,
                    model: addForm.model.trim() || null,
                    dept: addForm.dept.trim() || null,
                    startDate: new Date(addForm.startDate),
                    originValue: Number(addForm.originValue) || 0,
                    initialAccumulatedDepreciation: Number(addForm.initialAccumulatedDepreciation) || 0,
                    code: "",
                },
            };
            if (addInitial) {
                await api.accountsetsAccountSetIdFixedassetsInitialAssetsPost({ accountSetId, ...body });
            } else {
                await api.accountsetsAccountSetIdFixedassetsAssetsPost({ accountSetId, ...body });
            }
            setShowAdd(false);
            setAddInitial(false);
            setAddForm({ categoryId: "", name: "", model: "", dept: "", startDate: "", originValue: "", initialAccumulatedDepreciation: "" });
            setToast("资产已增加");
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "增加资产失败"));
        } finally {
            setBusy(false);
        }
    };

    const doClear = async () => {
        if (!token || !accountSetId || !clearing?.id || busy) {
            return;
        }
        setBusy(true);
        try {
            await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsAssetsIdClearPost({
                accountSetId,
                id: clearing.id,
                clearAssetRequest: { clearedDate: new Date(period.endDate) },
            });
            setToast(`已清理资产「${clearing.name ?? ""}」`);
            setClearing(null);
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "资产清理失败"));
            setClearing(null);
        } finally {
            setBusy(false);
        }
    };

    const doDelete = async () => {
        if (!token || !accountSetId || !deleting?.id || busy) {
            return;
        }
        setBusy(true);
        try {
            await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsAssetsIdDelete({ accountSetId, id: deleting.id });
            setToast("资产已删除");
            setDeleting(null);
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "删除失败"));
            setDeleting(null);
        } finally {
            setBusy(false);
        }
    };

    const depreciate = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        setBusy(true);
        try {
            const result = await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsDepreciatePost({
                accountSetId,
                depreciatePeriodRequest: { period: period.period },
            });
            const skipped = result.skipped ?? [];
            setToast(
                `「${periodLabel(period)}」计提折旧 ${result.depreciatedCount ?? 0} 项` +
                    (skipped.length ? `，${skipped.length} 项跳过：${skipped[0]}` : ""),
            );
            await data.reload();
            await loadDepVoucher();
        } catch (e) {
            setToast(await apiErrorMessage(e, "计提折旧失败"));
        } finally {
            setBusy(false);
        }
    };

    const generateDepVoucher = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        setBusy(true);
        try {
            const api = new FixedAssetsApi(apiConfig(token));
            const result = await api.accountsetsAccountSetIdFixedassetsDepreciationVoucherPost({
                accountSetId,
                generateDepreciationVoucherRequest: { period: period.period },
            });
            setDepVoucher(result);
            setToast(`已生成折旧凭证 ${result.voucherText ?? ""}，合计 ${(result.totalAmount ?? 0).toFixed(2)} 元`);
            await loadChanges();
        } catch (e) {
            setToast(await apiErrorMessage(e, "生成折旧凭证失败"));
        } finally {
            setBusy(false);
        }
    };

    const exportAssets = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(`${assetBase(accountSetId)}/assets/export`, token, "资产清单.xlsx");
        if (err) {
            setToast(err);
        }
    };

    const downloadTemplate = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(`${assetBase(accountSetId)}/assets/import-template`, token, "资产导入模板.xlsx");
        if (err) {
            setToast(err);
        }
    };

    const importAssets = async (file: File) => {
        if (!token || !accountSetId) {
            return;
        }
        setBusy(true);
        try {
            const { data: result, error } = await uploadFile<FixedAssetImportResultDto>(
                `${assetBase(accountSetId)}/assets/import`,
                token,
                file,
            );
            if (error || !result) {
                setToast(error ?? "导入失败");
                return;
            }
            setImportResult(result);
            await data.reload();
        } finally {
            setBusy(false);
            if (fileRef.current) {
                fileRef.current.value = "";
            }
        }
    };

    const bulkDelete = async () => {
        if (!token || !accountSetId || selected.size === 0 || busy) {
            return;
        }
        setBusy(true);
        try {
            const r = await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsAssetsBulkDeletePost({
                accountSetId,
                fixedAssetBulkIdsRequest: { ids: [...selected] },
            });
            setToast(
                `已删除 ${r.affected ?? 0} 项` +
                    (r.skipped ? `，跳过 ${r.skipped} 项${r.messages?.length ? `：${r.messages[0]}` : ""}` : ""),
            );
            setSelected(new Set());
            setBulkDeleteOpen(false);
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "批量删除失败"));
        } finally {
            setBusy(false);
        }
    };

    const bulkClear = async () => {
        if (!token || !accountSetId || selected.size === 0 || busy) {
            return;
        }
        setBusy(true);
        try {
            const r = await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsAssetsBulkClearPost({
                accountSetId,
                bulkClearAssetsRequest: { ids: [...selected], clearedDate: new Date(period.endDate) },
            });
            setToast(
                `已清理 ${r.affected ?? 0} 项` +
                    (r.skipped ? `，跳过 ${r.skipped} 项${r.messages?.length ? `：${r.messages[0]}` : ""}` : ""),
            );
            setSelected(new Set());
            setBulkClearOpen(false);
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "批量清理失败"));
        } finally {
            setBusy(false);
        }
    };

    const toggleAll = (checked: boolean) =>
        setSelected(checked ? new Set(data.assets.map((a) => a.id ?? "").filter(Boolean)) : new Set());

    const openAdd = (initial: boolean) => {
        setAddInitial(initial);
        setShowAdd(true);
    };

    const addAssetBtn = (initial: boolean) => (
        <Btn variant="primary" onClick={() => openAdd(initial)}>增加资产</Btn>
    );

    const importExportBtns = (
        <>
            <Btn onClick={() => setCopyOpen({ preset: null })} title="以现有卡片为模板新建一张资产">
                复制资产
            </Btn>
            <Btn onClick={() => fileRef.current?.click()} title="从 .xlsx 导入资产（按资产编号匹配）">
                导入
            </Btn>
            <Dropdown
                trigger={["click"]}
                menu={{
                    items: [
                        { key: "export", label: "导出资产" },
                        { key: "template", label: "下载导入模板" },
                    ],
                    onClick: ({ key }) => void (key === "export" ? exportAssets() : downloadTemplate()),
                }}
            >
                <span>
                    <ExportBtn dropdown title="导出资产 / 下载导入模板">
                        导出
                    </ExportBtn>
                </span>
            </Dropdown>
        </>
    );

    const hiddenInput = (
        <input
            ref={fileRef}
            type="file"
            accept=".xlsx"
            className="hidden"
            onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) {
                    void importAssets(file);
                }
            }}
        />
    );

    /* tab === 0 资产初始化 */
    if (tab === 0)
        return (
            <Page>
                <SubTabs tabs={ASSET_TABS} active={0} onChange={setTab} />
                <ToolRow
                    left={<span className="text-[13px] text-(--muted-foreground)">资产启用期间：{periodLabel(period)}</span>}
                    right={
                        <>
                            <Btn variant="primary" onClick={() => setInitDialog(true)}>
                                录入期初资产
                            </Btn>
                            <CheckBox label="显示所有信息" checked={showAll} onChange={setShowAll} />
                            {addAssetBtn(true)}
                            {importExportBtns}
                            <PrintBtn title="打印资产初始化清单（后端生成 PDF）" onClick={() => void printAssetSheet(token, accountSetId, `/initial-assets/pdf?period=${period.period}`, setToast)}>打印</PrintBtn>
                            <Btn dropdown onClick={() => setVoucherSettingOpen(true)} title="配置折旧 / 摊销 / 清理凭证科目">
                                折旧凭证设置
                            </Btn>
                            <span className="cursor-pointer" onClick={() => void data.reload()} title="刷新">
                                <RefreshIcon />
                            </span>
                        </>
                    }
                />
                <div className="flex-1 px-4">
                    <Table
                        cols={[
                            { key: "no", title: "资产编号", width: "120px" },
                            { key: "type", title: "资产类别", width: "130px" },
                            { key: "attr", title: "资产属性", width: "110px" },
                            { key: "name", title: "资产名称", width: "160px" },
                            { key: "model", title: "规格型号", width: "110px" },
                            { key: "date", title: "开始使用日期", width: "130px" },
                            ...(showAll
                                ? ([
                                      { key: "dept", title: "使用部门", width: "110px" },
                                      { key: "status", title: "状态", width: "90px" },
                                  ] as Col[])
                                : []),
                            { key: "origin", title: "资产原值", align: "right" },
                            { key: "dep", title: "累计折旧", align: "right" },
                            { key: "net", title: "资产净值", align: "right" },
                        ]}
                        rows={data.initialAssets.map((a) => ({
                            no: a.code,
                            type: a.categoryName,
                            attr: a.attrText,
                            name: a.name,
                            model: a.model,
                            date: toDateInput(a.startDate),
                            dept: a.dept ?? "",
                            status: a.statusText,
                            origin: fmtAmount(a.originValue),
                            dep: fmtAmount(a.accumulatedDepreciation),
                            net: fmtAmount(a.netValue ?? (a.originValue ?? 0) - (a.accumulatedDepreciation ?? 0)),
                        }))}
                        empty={<EmptyTip text="还没有期初资产，点击上方「增加资产」录入期初卡片。" />}
                    />
                </div>
                <div className="px-4 pb-3">
                    <HintBox>亲，资产初始化数据要与期初的资产数据一致哦！</HintBox>
                </div>
                <Pager total={data.initialAssets.length} />
                {initDialog && (
                    <InitDialog
                        onClose={() => setInitDialog(false)}
                        onConfirm={() => {
                            setInitDialog(false);
                            openAdd(true);
                        }}
                    />
                )}
                {showAdd && renderAddDialog()}
                {copyOpen && (
                    <CopyAssetDialog
                        assets={data.initialAssets}
                        preset={copyOpen.preset}
                        onClose={() => setCopyOpen(null)}
                        onSaved={() => void data.reload()}
                    />
                )}
                {voucherSettingOpen && <AssetVoucherSettingDialog onClose={() => setVoucherSettingOpen(false)} />}
                {importResult && <ImportResultDialog result={importResult} onClose={() => setImportResult(null)} />}
                {hiddenInput}
                {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            </Page>
        );

    /* tab === 2 变更记录及生成凭证 */
    if (tab === 2)
        return (
            <Page>
                <SubTabs tabs={ASSET_TABS} active={2} onChange={setTab} />
                <ToolRow
                    left={<PeriodStepper {...stepper} />}
                    right={
                        <>
                            <Btn onClick={() => setVoucherSettingOpen(true)} title="配置折旧 / 摊销 / 清理凭证科目">
                                折旧凭证设置
                            </Btn>
                            <span className="cursor-pointer" onClick={() => void loadChanges()} title="刷新">
                                <RefreshIcon />
                            </span>
                        </>
                    }
                />
                <StateBar loading={changesLoading} />
                <div className="flex-1 px-4">
                    <Table
                        cols={CHANGE_COLS}
                        rows={changes.map((c) => ({
                            kind: c.kindText,
                            code: c.assetCode,
                            name: c.assetName,
                            before: c.before,
                            after: c.after,
                            period: c.period,
                            ops: c.voucherText ? (
                                <span className="text-(--brand)">{c.voucherText}</span>
                            ) : c.kindText === "新增" ? (
                                <span className="text-(--muted-foreground)">—</span>
                            ) : (
                                <Btn onClick={() => setChangeVoucher(c)}>生成凭证</Btn>
                            ),
                        }))}
                        empty={<EmptyTip />}
                    />
                </div>
                <Pager total={changes.length} />
                {changeVoucher && (
                    <ChangeVoucherDialog
                        change={changeVoucher}
                        onClose={() => setChangeVoucher(null)}
                        onSaved={() => {
                            setToast("凭证已生成");
                            void loadChanges();
                        }}
                    />
                )}
                {voucherSettingOpen && <AssetVoucherSettingDialog onClose={() => setVoucherSettingOpen(false)} />}
                {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            </Page>
        );

    /* tab === 3 计提折旧及生成凭证 */
    if (tab === 3) {
        const newAssetCount = data.assets.filter((a) => {
            if (!a.startDate) {
                return false;
            }
            const start = new Date(a.startDate as unknown as string);
            return start.getFullYear() * 100 + start.getMonth() + 1 === period.period;
        }).length;
        return (
            <Page>
                <SubTabs tabs={ASSET_TABS} active={3} onChange={setTab} />
                <div className="mx-4 mt-2 rounded-lg bg-(--brand-soft) px-4 py-2.5 text-[13px] leading-5 text-(--brand)">
                    亲，点击“计提折旧”即可生成当期折旧摊销明细；再点击“生成凭证”按资产属性汇总生成一张折旧 / 摊销凭证。
                    科目可在“折旧凭证设置”中调整。
                </div>
                <div className="flex-1 px-4 pt-3">
                    <Table
                        cols={[
                            {
                                key: "period",
                                title: "会计期间",
                                render: () => (
                                    <PeriodStepper {...stepper} />
                                ),
                            },
                            {
                                key: "newAssets",
                                title: "新增资产",
                                render: () => `${newAssetCount} 项`,
                            },
                            {
                                key: "changes",
                                title: "变更记录",
                                render: () => `${changes.length} 项`,
                            },
                            {
                                key: "dep",
                                title: "计提折旧",
                                render: () =>
                                    depVoucher && depVoucher.changeCount ? (
                                        <span className="text-(--foreground)">
                                            已计提 {depVoucher.changeCount} 项，合计 {(depVoucher.totalAmount ?? 0).toFixed(2)} 元
                                        </span>
                                    ) : (
                                        <span className="text-(--muted-foreground)">{busy ? "计提中…" : "待计提"}</span>
                                    ),
                            },
                            {
                                key: "voucher",
                                title: "折旧凭证",
                                render: () =>
                                    depVoucher?.voucherText ? (
                                        <span className="text-(--brand)">{depVoucher.voucherText}</span>
                                    ) : (
                                        <span className="text-(--muted-foreground)">未生成</span>
                                    ),
                            },
                            {
                                key: "ops",
                                title: "操作",
                                render: () => (
                                    <span className="inline-flex items-center gap-2">
                                        <Btn variant={busy ? "disabled" : "primary"} onClick={() => void depreciate()}>
                                            计提折旧
                                        </Btn>
                                        <Btn
                                            variant={busy || !depVoucher?.changeCount || depVoucher?.generated ? "disabled" : "primary"}
                                            onClick={() => void generateDepVoucher()}
                                            title={
                                                depVoucher?.generated
                                                    ? "本期折旧凭证已生成"
                                                    : "按当期计提记录生成折旧 / 摊销凭证"
                                            }
                                        >
                                            生成凭证
                                        </Btn>
                                        <span
                                            className="cursor-pointer text-[13px] text-(--brand) hover:underline"
                                            onClick={() => setVoucherSettingOpen(true)}
                                        >
                                            凭证设置
                                        </span>
                                        <HelpTip title="折旧凭证按资产属性汇总：固定资产走折旧费用与累计折旧，无形资产走摊销费用与累计摊销。" />
                                    </span>
                                ),
                            },
                        ]}
                        rows={[{}]}
                    />
                </div>
                {voucherSettingOpen && <AssetVoucherSettingDialog onClose={() => setVoucherSettingOpen(false)} />}
                {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            </Page>
        );
    }

    /* tab === 1 资产列表 */
    const allChecked = data.assets.length > 0 && data.assets.every((a) => selected.has(a.id ?? ""));
    const assetTotals = data.assets.reduce(
        (acc, a) => {
            acc.origin += a.originValue ?? 0;
            acc.dep += a.currentPeriodDepreciation ?? 0;
            acc.net += a.netValue ?? (a.originValue ?? 0) - (a.accumulatedDepreciation ?? 0);
            return acc;
        },
        { origin: 0, dep: 0, net: 0 },
    );
    const cols: Col[] = [
        {
            key: "ck",
            title: <CheckBox label="" checked={allChecked} onChange={toggleAll} />,
            width: "36px",
        },
        { key: "no", title: "资产编号", width: "120px" },
        { key: "type", title: "资产类别", width: "130px" },
        { key: "attr", title: "资产属性", width: "110px" },
        { key: "name", title: "资产名称", width: "160px" },
        { key: "model", title: "规格型号", width: "110px" },
        { key: "date", title: "开始使用日期", width: "130px" },
        ...(showAll
            ? ([
                  { key: "dept", title: "使用部门", width: "110px" },
                  { key: "accum", title: "累计折旧", align: "right" },
                  { key: "status", title: "状态", width: "90px" },
                  { key: "cleared", title: "清理日期", width: "110px" },
              ] as Col[])
            : []),
        { key: "origin", title: "资产原值", align: "right" },
        // 原值 / 本期折旧 / 资产净值（累计折旧与状态不单列）
        { key: "dep", title: "本期折旧", align: "right" },
        { key: "net", title: "资产净值", align: "right" },
        { key: "ops", title: "操作", width: "150px" },
    ];
    return (
        <Page>
            <SubTabs tabs={ASSET_TABS} active={1} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <PeriodStepper {...stepper} />
                        <CheckBox label="显示所有信息" checked={showAll} onChange={setShowAll} />
                    </>
                }
                right={
                    <>
                        {addAssetBtn(false)}
                        {importExportBtns}
                        <PrintBtn title="打印固定资产清单（后端生成 PDF）" onClick={() => void printAssetSheet(token, accountSetId, `/assets/pdf?period=${period.period}`, setToast)}>打印</PrintBtn>
                        <Dropdown
                            trigger={["click"]}
                            menu={{
                                items: [
                                    { key: "clear", label: `批量清理（已选 ${selected.size} 项）`, disabled: selected.size === 0 },
                                    { key: "delete", label: `批量删除（已选 ${selected.size} 项）`, disabled: selected.size === 0 },
                                    { key: "setting", label: "折旧凭证设置" },
                                ],
                                onClick: ({ key }) => {
                                    if (key === "clear") {
                                        setBulkClearOpen(true);
                                    } else if (key === "delete") {
                                        setBulkDeleteOpen(true);
                                    } else {
                                        setVoucherSettingOpen(true);
                                    }
                                },
                            }}
                        >
                            <span>
                                <Btn dropdown title="批量清理 / 批量删除 / 凭证设置">
                                    批量操作
                                </Btn>
                            </span>
                        </Dropdown>
                        <span className="cursor-pointer" onClick={() => void data.reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <div className="flex-1 px-4">
                <StateBar loading={data.loading} error={data.error} />
                {data.assets.length > 0 && (
                    <div className="mb-3 flex flex-wrap items-center gap-x-8 gap-y-2 rounded-lg bg-(--table-zebra-bg) px-4 py-2.5 text-[13px]">
                        <span className="text-(--muted-foreground)">
                            资产{" "}
                            <span className="ml-1 font-medium tabular-nums text-(--foreground)">{data.assets.length}</span> 项
                        </span>
                        <span className="text-(--muted-foreground)">
                            原值合计{" "}
                            <span className="ml-1 font-medium tabular-nums text-(--foreground)">¥{fmtAmount(assetTotals.origin, true)}</span>
                        </span>
                        <span className="text-(--muted-foreground)">
                            本期折旧{" "}
                            <span className="ml-1 font-medium tabular-nums text-(--foreground)">¥{fmtAmount(assetTotals.dep, true)}</span>
                        </span>
                        <span className="text-(--muted-foreground)">
                            净值合计{" "}
                            <span className="ml-1 text-[14px] font-semibold tabular-nums text-(--foreground)">¥{fmtAmount(assetTotals.net, true)}</span>
                        </span>
                    </div>
                )}
                <Table
                    cols={cols}
                    rows={data.assets.map((a) => ({
                        ck: (
                            <CheckBox
                                label=""
                                checked={selected.has(a.id ?? "")}
                                onChange={(c) =>
                                    setSelected((prev) => {
                                        const next = new Set(prev);
                                        if (c) {
                                            next.add(a.id ?? "");
                                        } else {
                                            next.delete(a.id ?? "");
                                        }
                                        return next;
                                    })
                                }
                            />
                        ),
                        no: a.code,
                        type: a.categoryName,
                        attr: a.attrText,
                        name: a.name,
                        model: a.model,
                        date: toDateInput(a.startDate),
                        dept: a.dept ?? "",
                        accum: fmtAmount(a.accumulatedDepreciation),
                        status: a.statusText,
                        cleared: toDateInput(a.clearedDate),
                        origin: fmtAmount(a.originValue),
                        // 本期折旧（后端取最近一次计提期间的折旧额）
                        dep: fmtAmount(a.currentPeriodDepreciation),
                        net: fmtAmount(a.netValue ?? (a.originValue ?? 0) - (a.accumulatedDepreciation ?? 0)),
                        ops: (
                            <span className="flex gap-2">
                                <span
                                    className="cursor-pointer text-[13px] text-(--brand) hover:underline"
                                    onClick={() => setCopyOpen({ preset: a })}
                                >
                                    复制
                                </span>
                                <span
                                    className="cursor-pointer text-[13px] text-(--brand) hover:underline"
                                    onClick={() => setClearing(a)}
                                >
                                    清理
                                </span>
                                <span
                                    className="cursor-pointer text-[13px] text-(--brand) hover:underline"
                                    onClick={() => setDeleting(a)}
                                >
                                    删除
                                </span>
                            </span>
                        ),
                    }))}
                    empty={<EmptyTip />}
                />
            </div>
            <Pager total={data.assets.length} />
            {showAdd && renderAddDialog()}
            {copyOpen && (
                <CopyAssetDialog
                    assets={data.assets}
                    preset={copyOpen.preset}
                    onClose={() => setCopyOpen(null)}
                    onSaved={() => void data.reload()}
                />
            )}
            {clearing && (
                <ConfirmDialog
                    title="资产清理"
                    text={`确定将资产「${clearing.name ?? ""}」按 ${periodLabel(period)} 清理吗？清理后终止计提并登记清理变更。`}
                    confirmText="清理"
                    busy={busy}
                    onClose={() => setClearing(null)}
                    onConfirm={() => void doClear()}
                />
            )}
            {deleting && (
                <ConfirmDialog
                    title="删除资产"
                    text={`确定删除资产「${deleting.name ?? ""}」吗？该资产的变更记录会一并删除，此操作不可恢复。`}
                    busy={busy}
                    onClose={() => setDeleting(null)}
                    onConfirm={() => void doDelete()}
                />
            )}
            {bulkClearOpen && (
                <ConfirmDialog
                    title="批量清理资产"
                    text={`确定按 ${periodLabel(period)} 清理已选的 ${selected.size} 项资产吗？`}
                    confirmText="清理"
                    busy={busy}
                    onClose={() => setBulkClearOpen(false)}
                    onConfirm={() => void bulkClear()}
                />
            )}
            {bulkDeleteOpen && (
                <ConfirmDialog
                    title="批量删除资产"
                    text={`确定删除已选的 ${selected.size} 项资产吗？已生成凭证的资产会被跳过。`}
                    busy={busy}
                    onClose={() => setBulkDeleteOpen(false)}
                    onConfirm={() => void bulkDelete()}
                />
            )}
            {voucherSettingOpen && <AssetVoucherSettingDialog onClose={() => setVoucherSettingOpen(false)} />}
            {importResult && <ImportResultDialog result={importResult} onClose={() => setImportResult(null)} />}
            {hiddenInput}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );

    function renderAddDialog() {
        const close = () => {
            setShowAdd(false);
            setAddInitial(false);
        };
        return (
            <Dialog
                title={addInitial ? "增加期初资产" : "增加资产"}
                onClose={close}
                footer={
                    <div className="flex justify-center gap-3">
                        <Btn variant={busy ? "disabled" : "primary"} onClick={() => void createAsset()}>
                            保存
                        </Btn>
                        <Btn onClick={close}>取消</Btn>
                    </div>
                }
            >
                <div className="flex flex-col gap-3 px-1 text-[13px] text-(--foreground)">
                    <label className="flex items-center gap-3">
                        <span className={`${FORM_LABEL_W} text-right`}>资产类别：</span>
                        <Select
                            className={FORM_CONTROL_W}
                            value={addForm.categoryId}
                            onChange={(v) => setAddForm({ ...addForm, categoryId: v })}
                            options={[
                                { value: "", label: "（请选择）" },
                                ...(data.categories ?? []).map((c) => ({ value: c.id ?? "", label: `${c.code ?? ""} ${c.name ?? ""}` })),
                            ]}
                        />
                    </label>
                    {[
                        ["资产名称：", "name", false, false],
                        ["规格型号：", "model", false, false],
                        ["使用部门：", "dept", false, false],
                        ["开始使用日期：", "startDate", false, true],
                        ["资产原值：", "originValue", true, false],
                        ["期初累计折旧：", "initialAccumulatedDepreciation", true, false],
                    ].map(([label, key, numeric, isDate]) => (
                        <label key={String(key)} className="flex items-center gap-3">
                            <span className={`${FORM_LABEL_W} text-right`}>{label}</span>
                            {isDate ? (
                                <DateInput
                                    className={FORM_CONTROL_W}
                                    value={String(addForm[key as keyof typeof addForm] ?? "")}
                                    onChange={(v) => setAddForm({ ...addForm, [key as keyof typeof addForm]: v })}
                                />
                            ) : (
                                <TextInput
                                    className={`${FORM_CONTROL_W}${numeric ? " text-right" : ""}`}
                                    value={String(addForm[key as keyof typeof addForm] ?? "")}
                                    onChange={(v) =>
                                        setAddForm({
                                            ...addForm,
                                            [key as keyof typeof addForm]: numeric ? v.replace(/[^\d.]/g, "") : v,
                                        })
                                    }
                                />
                            )}
                        </label>
                    ))}
                    <div className="pl-[122px] text-[12.5px] text-(--muted-foreground)">资产编号由系统按类别自动生成，可导入后统一调整。</div>
                </div>
            </Dialog>
        );
    }
}

/** 资产模块公共「期初确认」弹窗。 */
function InitDialog({ onClose, onConfirm }: { onClose: () => void; onConfirm: () => void }) {
    return (
        <Dialog
            title="提示"
            onClose={onClose}
            footer={
                <div className="flex justify-center gap-3">
                    <Btn variant="primary" onClick={onConfirm}>
                        去录入
                    </Btn>
                    <Btn onClick={onClose}>
                        关闭
                    </Btn>
                </div>
            }
        >
            <div className="text-center text-[13px] leading-6 text-(--foreground)">
                请确定是否存在资产的期初数据？如有期初，请点击「去录入」
                <br />
                前往录入期初资产卡片，否则请关闭~
            </div>
        </Dialog>
    );
}

/* ---------- 折旧摊销明细表 ---------- */

export function AssetDepreciation() {
    const { token, accountSetId } = useAccountContext();
    const { period, stepper } = usePeriodStepper();
    const [ledger, setLedger] = useState<DepreciationLedgerRowDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [toast, setToast] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setLoading(true);
        try {
            setLedger(
                await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsDepreciationLedgerGet({
                    accountSetId,
                    period: period.period,
                }),
            );
            setError(null);
        } catch (e) {
            setLedger([]);
            setError(await apiErrorMessage(e, "加载折旧明细失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, period.period]);

    useEffect(() => {
        void reload();
    }, [reload]);

    const exportLedger = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(
            `${assetBase(accountSetId)}/depreciation-ledger/export?period=${period.period}`,
            token,
            `折旧摊销明细表_${period.period}.xlsx`,
        );
        if (err) {
            setToast(err);
        }
    };

    const cols: Col[] = [
        { key: "period", title: "会计期间", width: "100px" },
        { key: "no", title: "资产编号", width: "110px" },
        { key: "type", title: "资产类别", width: "120px" },
        { key: "attr", title: "资产属性", width: "110px" },
        { key: "name", title: "资产名称", width: "150px" },
        { key: "model", title: "规格型号", width: "100px" },
        { key: "dept", title: "使用部门", width: "100px" },
        { key: "origin", title: "资产原值", align: "right" },
        { key: "dep", title: "本期折旧", align: "right" },
        { key: "yearDep", title: "本年累计折旧", align: "right" },
        { key: "totalDep", title: "累计折旧", align: "right" },
        { key: "impair", title: "减值准备", align: "right" },
        { key: "net", title: "资产净值", align: "right" },
    ];
    const label = periodLabel(period);
    return (
        <Page>
            <ToolRow
                left={<PeriodStepper {...stepper} />}
                right={
                    <>
                        <ExportBtn onClick={() => void exportLedger()} title="导出当期折旧摊销明细为 .xlsx">
                            导出
                        </ExportBtn>
                        <PrintBtn onClick={() => void printAssetSheet(token, accountSetId, `/depreciation-ledger/pdf?period=${period.period}`, setToast)} title="打印当期折旧摊销明细表（后端生成 PDF）">
                            打印
                        </PrintBtn>
                        <span className="cursor-pointer" onClick={() => void reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <StateBar loading={loading} error={error} />
            <div className="flex-1 px-4">
                <Table
                    cols={cols}
                    rows={ledger.map((r) => ({
                        period: label,
                        no: r.assetCode,
                        type: r.categoryName,
                        attr: r.attrText,
                        name: r.assetName,
                        model: r.model,
                        dept: r.dept,
                        origin: fmtAmount(r.originValue),
                        dep: fmtAmount(r.periodDepreciation),
                        yearDep: fmtAmount(r.yearAccumulated),
                        totalDep: fmtAmount(r.totalAccumulated),
                        impair: fmtAmount(r.impairment),
                        net: fmtAmount(r.netValue),
                    }))}
                    empty={<EmptyTip />}
                />
            </div>
            <Pager total={ledger.length} />
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 资产汇总表（部门/类别双页签） ---------- */

export function AssetSummary() {
    const { token, accountSetId } = useAccountContext();
    const { period, stepper } = usePeriodStepper();
    const [tab, setTab] = useState(0);
    const [rows, setRows] = useState<FixedAssetSummaryRowDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [toast, setToast] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setLoading(true);
        try {
            setRows(
                await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsSummaryGet({
                    accountSetId,
                    startPeriod: period.period,
                    endPeriod: period.period,
                    byCategory: tab === 1,
                }),
            );
            setError(null);
        } catch (e) {
            setRows([]);
            setError(await apiErrorMessage(e, "加载资产汇总表失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, period.period, tab]);

    useEffect(() => {
        void reload();
    }, [reload]);

    const exportSummary = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(
            `${assetBase(accountSetId)}/summary/export?startPeriod=${period.period}&endPeriod=${period.period}&byCategory=${tab === 1}`,
            token,
            `资产汇总表_${period.period}.xlsx`,
        );
        if (err) {
            setToast(err);
        }
    };

    const fmt = (v: number | null | undefined) => fmtAmount(v);
    const sub = ["期初数", "本期增加", "本期减少", "期末数"];
    const groupDefs: Array<{ title: string; keys: Array<keyof FixedAssetSummaryRowDto> }> = [
        { title: "资产原值", keys: ["originOpening", "originIncrease", "originDecrease", "originClosing"] },
        { title: "累计折旧", keys: ["depOpening", "depIncrease", "depDecrease", "depClosing"] },
        { title: "减值准备", keys: ["impairOpening", "impairIncrease", "impairDecrease", "impairClosing"] },
        // 「资产净值」不单独成列（原值 - 累计折旧 - 减值准备 即可推得）

    ];
    const cols: Col[] = [
        { key: "groupName", title: tab === 0 ? "部门名称" : "资产类别", width: "140px" },
        ...groupDefs.flatMap((g) =>
            g.keys.map((k, j) => ({
                key: k as string,
                title: sub[j],
                align: "right" as const,
                group: g.title,
                width: "90px",
            })),
        ),
    ];
    const tableRows: Row[] = rows.map((r) => {
        const row: Row = { groupName: r.groupName };
        for (const g of groupDefs) {
            for (const k of g.keys) row[k as string] = fmt(r[k] as number | null | undefined);
        }
        return row;
    });
    return (
        <Page>
            <SubTabs tabs={["按使用部门汇总", "按资产类别汇总"]} active={tab} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <span className="text-[13px] text-(--muted-foreground)">会计期间:</span>
                        <PeriodStepper {...stepper} />
                    </>
                }
                right={
                    <>
                        <ExportBtn onClick={() => void exportSummary()} title="导出当前分组的资产汇总表为 .xlsx">
                            导出
                        </ExportBtn>
                        <span className="cursor-pointer" onClick={() => void reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <StateBar loading={loading} error={error} />
            <div className="flex-1 px-4">
                <Table cols={cols} rows={tableRows} empty={<EmptyTip />} />
            </div>
            <Pager total={rows.length} />
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 资产类别设置 ---------- */

const CATEGORY_METHOD_OPTIONS = [{ value: "1", label: "平均年限法" }];
const CATEGORY_ATTR_OPTIONS = [
    { value: "1", label: "固定资产" },
    { value: "2", label: "无形资产" },
    { value: "3", label: "长期待摊费用" },
];

/** 资产类别编辑/新增对话框：走 POST / PUT 端点。 */
function CategoryEditDialog({
    category,
    accounts,
    onClose,
    onSaved,
}: {
    category: FixedAssetCategoryDto | null;
    accounts: AccountDto[];
    onClose: () => void;
    onSaved: () => void;
}) {
    const { token, accountSetId } = useAccountContext();
    const [form, setForm] = useState(() => ({
        code: category?.code ?? "",
        name: category?.name ?? "",
        method: "1",
        attr: String(attrOf(category?.attrText)),
        subjectAccountId: category?.subjectAccountId ?? "",
        months: category?.months != null ? String(category.months) : "",
        residualRate: category?.residualRate != null ? String(category.residualRate) : "",
    }));
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const set = (patch: Partial<typeof form>) => setForm((prev) => ({ ...prev, ...patch }));

    const save = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (!form.code.trim() || !form.name.trim() || !form.subjectAccountId) {
            setToast("编码、名称与资产科目必填");
            return;
        }
        const body = {
            code: form.code.trim(),
            name: form.name.trim(),
            method: Number(form.method),
            attr: Number(form.attr),
            subjectAccountId: form.subjectAccountId,
            months: Number(form.months) || 0,
            residualRate: Number(form.residualRate) || 0,
        };
        setBusy(true);
        try {
            const api = new FixedAssetsApi(apiConfig(token));
            if (category?.id) {
                await api.accountsetsAccountSetIdFixedassetsCategoriesIdPut({ accountSetId, id: category.id, saveFixedAssetCategoryRequest: body });
            } else {
                await api.accountsetsAccountSetIdFixedassetsCategoriesPost({ accountSetId, saveFixedAssetCategoryRequest: body });
            }
            onSaved();
            onClose();
        } catch (e) {
            setToast(await apiErrorMessage(e, "保存失败"));
        } finally {
            setBusy(false);
        }
    };

    return (
        <Dialog
            title={category ? "编辑资产类别" : "新增资产类别"}
            onClose={onClose}
            footer={
                <div className="flex justify-center gap-3">
                    <Btn variant={busy ? "disabled" : "primary"} onClick={() => void save()}>
                        保存
                    </Btn>
                    <Btn onClick={onClose}>取消</Btn>
                </div>
            }
        >
            <div className="flex flex-col gap-3 px-1 text-[13px] text-(--foreground)">
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>类别编码：</span>
                    <TextInput className={FORM_CONTROL_W} value={form.code} onChange={(v) => set({ code: v })} />
                </label>
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>类别名称：</span>
                    <TextInput className={FORM_CONTROL_W} value={form.name} onChange={(v) => set({ name: v })} />
                </label>
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>折旧方法：</span>
                    <Select className={FORM_CONTROL_W} value={form.method} onChange={(v) => set({ method: v })} options={CATEGORY_METHOD_OPTIONS} />
                </label>
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>资产属性：</span>
                    <Select className={FORM_CONTROL_W} value={form.attr} onChange={(v) => set({ attr: v })} options={CATEGORY_ATTR_OPTIONS} />
                </label>
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>资产科目：</span>
                    <Select
                        className={FORM_CONTROL_W}
                        value={form.subjectAccountId}
                        onChange={(v) => set({ subjectAccountId: v })}
                        options={[
                            { value: "", label: "（请选择）" },
                            ...accounts.map((acc) => ({ value: acc.id ?? "", label: `${acc.code ?? ""} ${acc.name ?? ""}` })),
                        ]}
                    />
                </label>
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>使用月数：</span>
                    <TextInput className={`${FORM_CONTROL_W} text-right`} value={form.months} onChange={(v) => set({ months: v.replace(/[^\d]/g, "") })} />
                </label>
                <label className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} text-right`}>净残值率(%)：</span>
                    <TextInput className={`${FORM_CONTROL_W} text-right`} value={form.residualRate} onChange={(v) => set({ residualRate: v.replace(/[^\d.]/g, "") })} />
                </label>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Dialog>
    );
}

export function AssetCategory() {
    const data = useFixedAssetData();
    const { token, accountSetId } = useAccountContext();
    const [editing, setEditing] = useState<FixedAssetCategoryDto | null | "new">(null);
    const [deleting, setDeleting] = useState<FixedAssetCategoryDto | null>(null);
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [keyword, setKeyword] = useState("");
    const [categoryAccounts, setCategoryAccounts] = useState<AccountDto[]>([]);

    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        new AccountsApi(apiConfig(token))
            .accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true })
            .then((list) => setCategoryAccounts(list ?? []))
            .catch(() => setCategoryAccounts([]));
    }, [token, accountSetId]);

    const remove = async () => {
        if (!token || !accountSetId || !deleting?.id || busy) {
            return;
        }
        setBusy(true);
        try {
            await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsCategoriesIdDelete({ accountSetId, id: deleting.id });
            setToast("类别已删除");
            setDeleting(null);
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "删除失败（类别下有资产时不可删）"));
            setDeleting(null);
        } finally {
            setBusy(false);
        }
    };

    const exportCategories = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(`${assetBase(accountSetId)}/categories/export`, token, "资产类别.xlsx");
        if (err) {
            setToast(err);
        }
    };

    const cols: Col[] = [
        { key: "code", title: "资产类别编码", width: "130px" },
        { key: "name", title: "资产类别名称", width: "180px" },
        { key: "method", title: "折旧方法", width: "130px" },
        { key: "attr", title: "资产属性", width: "130px" },
        { key: "subject", title: "资产科目", width: "130px" },
        { key: "months", title: "使用月份", align: "right", width: "100px" },
        { key: "residual", title: "预计净残值率", align: "right", width: "120px" },
        { key: "remark", title: "备注" },
        {
            key: "ops",
            title: "操作",
            width: "110px",
            render: (row: Row) => (
                <span className="flex gap-2">
                    <span
                        className="cursor-pointer text-[13px] text-(--brand) hover:underline"
                        onClick={() => setEditing(row.__cat as FixedAssetCategoryDto)}
                    >
                        编辑
                    </span>
                    <span
                        className="cursor-pointer text-[13px] text-(--brand) hover:underline"
                        onClick={() => setDeleting(row.__cat as FixedAssetCategoryDto)}
                    >
                        删除
                    </span>
                </span>
            ),
        },
    ];
    const kw = keyword.trim().toLowerCase();
    const filtered = data.categories.filter(
        (c) => !kw || (c.code ?? "").toLowerCase().includes(kw) || (c.name ?? "").toLowerCase().includes(kw),
    );
    return (
        <Page>
            <ToolRow
                left={<Btn variant="primary" onClick={() => setEditing("new")}>新增</Btn>}
                right={
                    <>
                        <TextInput className={FORM_CONTROL_W} value={keyword} onChange={setKeyword} placeholder="输入编码或名称" />
                        <ExportBtn onClick={() => void exportCategories()} title="导出资产类别为 .xlsx">
                            导出
                        </ExportBtn>
                        <span className="cursor-pointer" onClick={() => void data.reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <StateBar loading={data.loading} error={data.error} />
            <div className="flex-1 px-4">
                <Table
                    cols={cols}
                    rows={filtered.map((c) => ({
                        __cat: c,
                        code: c.code,
                        name: c.name,
                        method: c.methodText,
                        attr: c.attrText,
                        subject: c.subjectName,
                        months: String(c.months),
                        residual: c.residualRate + "%",
                    }))}
                    empty={<EmptyTip />}
                />
            </div>
            <Pager total={filtered.length} />
            {editing && (
                <CategoryEditDialog
                    category={editing === "new" ? null : editing}
                    accounts={categoryAccounts}
                    onClose={() => setEditing(null)}
                    onSaved={() => void data.reload()}
                />
            )}
            {deleting && (
                <ConfirmDialog
                    title="删除资产类别"
                    text={`确定删除类别「${deleting.name ?? ""}」吗？类别下有资产时不可删除。`}
                    busy={busy}
                    onClose={() => setDeleting(null)}
                    onConfirm={() => void remove()}
                />
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 资产核对总账 ---------- */

export function AssetCheck() {
    const { token, accountSetId } = useAccountContext();
    const { period, stepper } = usePeriodStepper();
    const [check, setCheck] = useState<FixedAssetCheckRowDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [toast, setToast] = useState<string | null>(null);

    // 核对总账按期间取数（会计科目期初 + 期间内已过账发生额 vs 资产卡片回算）。
    const reload = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setLoading(true);
        try {
            setCheck(await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsCheckGet({
                accountSetId,
                startPeriod: period.period,
                endPeriod: period.period,
            }));
            setError(null);
        } catch (e) {
            setCheck([]);
            setError(await apiErrorMessage(e, "加载核对总账失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, period.period]);

    useEffect(() => {
        void reload();
    }, [reload, period.period]);

    const exportCheck = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(`${assetBase(accountSetId)}/check/export?startPeriod=${period.period}&endPeriod=${period.period}`, token, "资产核对总账.xlsx");
        if (err) {
            setToast(err);
        }
    };

    /** 对账行模型：▼ 前缀为分组行，其后的明细行挂在该分组下（分组前出现的明细行按原逻辑忽略）。 */
    const checkRows = (() => {
        const rows: Array<{ key: string; kind: "group" | "item"; groupName: string; item: FixedAssetCheckRowDto }> = [];
        let groupName = "";
        check.forEach((r, i) => {
            const raw = r.item ?? "";
            if (raw.startsWith("▼ ")) {
                groupName = raw.slice(2);
                rows.push({ key: `g-${i}`, kind: "group", groupName, item: r });
            } else if (groupName) {
                rows.push({ key: `r-${i}`, kind: "item", groupName, item: r });
            }
        });
        return rows;
    })();
    return (
        <Page>
            <div style={{ marginBottom: 16 }}>
                <ToolRow
                    left={<PeriodStepper {...stepper} />}
                    right={
                        <>
                            <PrintBtn onClick={() => void printAssetSheet(token, accountSetId, `/check/pdf?period=${period.period}`, setToast)} title="打印资产核对总账（后端生成 PDF）">
                                打印
                            </PrintBtn>
                            <ExportBtn onClick={() => void exportCheck()} title="导出资产核对总账为 .xlsx">
                                导出
                            </ExportBtn>
                            <span className="cursor-pointer" onClick={() => void reload()} title="刷新">
                                <RefreshIcon />
                            </span>
                        </>
                    }
                />
            </div>
            <StateBar loading={loading} error={error} />
            <div className="flex-1 px-4">
                <AntTable
                    pagination={false}
                    rowKey={(r) => r.key}
                    dataSource={checkRows}
                    scroll={{ x: "max-content" }}
                    locale={{ emptyText: "暂无数据" }}
                    rowClassName={(r) => (r.kind === "group" ? "font-bold" : "")}
                    columns={[
                        {
                            title: "项目",
                            width: 140,
                            render: (_, r) =>
                                r.kind === "group" ? (
                                    <span className="inline-flex items-center gap-1">
                                        <DownOutlined className="text-[12px]" style={ICON_STYLE.muted} /> {r.groupName}
                                    </span>
                                ) : (
                                    <span className={r.item.isDiff ? "text-(--brand)" : undefined}>
                                        {r.item.isDiff ? "差异" : r.item.item}
                                    </span>
                                ),
                            onCell: (r) => (r.kind === "group" ? { colSpan: 6 } : {}),
                        },
                        {
                            title: "名称",
                            render: (_, r) => r.item.name,
                            onCell: (r) => (r.kind === "group" ? { colSpan: 0 } : {}),
                        },
                        {
                            title: "期初余额",
                            align: "right",
                            render: (_, r) => fmtAmount(r.item.opening),
                            onCell: (r) => (r.kind === "group" ? { colSpan: 0 } : {}),
                        },
                        {
                            title: "借方",
                            align: "right",
                            render: (_, r) => fmtAmount(r.item.debit),
                            onCell: (r) => (r.kind === "group" ? { colSpan: 0 } : {}),
                        },
                        {
                            title: "贷方",
                            align: "right",
                            render: (_, r) => fmtAmount(r.item.credit),
                            onCell: (r) => (r.kind === "group" ? { colSpan: 0 } : {}),
                        },
                        {
                            title: "余额",
                            align: "right",
                            render: (_, r) => fmtAmount(r.item.balance),
                            onCell: (r) => (r.kind === "group" ? { colSpan: 0 } : {}),
                        },
                    ]}
                />
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
