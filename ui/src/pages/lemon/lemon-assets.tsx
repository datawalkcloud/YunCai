/**
 * 复刻区资产模块（对应柠檬云 /FixedAssets/*）：
 * 资产管理（初始化/列表/变更记录/计提折旧四页签 + 期初确认弹窗）、
 * 折旧摊销明细表、资产汇总表（部门/类别双页签）、资产类别设置、资产核对总账。
 * 视觉遵循全站云财蓝：主色 #2f6bff、圆角 rounded-lg/2xl、白卡 + slate 灰阶文字。
 */
import { Fragment, useCallback, useEffect, useState } from "react";
import { Building, CalendarDays, ChevronDown, CircleHelp, ClipboardCheck, Layers, PieChart, TrendingDown } from "lucide-react";

import {
    Btn,
    CheckBox,
    Dialog,
    EmptyTip,
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
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import {
    shiftPeriod,
    useDefaultAccountingPeriod,
    type AccountingPeriod,
} from "@/accountSets/useCurrentAccountSet";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import {
    AccountsApi,
    FixedAssetsApi,
    type AccountDto,
    type FixedAssetCategoryDto,
    type FixedAssetChangeDto,
    type FixedAssetCheckRowDto,
    type FixedAssetDto,
    type DepreciationLedgerRowDto,
    type FixedAssetSummaryRowDto,
} from "@/apis-gen";

const pad = (n: number) => String(n).padStart(2, "0");

/** 期间对象 → 「2026年10月」。 */
function periodLabel(p: AccountingPeriod): string {
    return `${p.year}年${pad(p.month)}月`;
}

/** 日期（Date 或字符串）→ yyyy-MM-dd。 */
function toDateInput(d: Date | string | null | undefined): string {
    return d ? new Date(d).toISOString().substring(0, 10) : "";
}

/** 资产属性展示文本 → 枚举。 */
const attrOf = (t?: string | null) => (t === "无形资产" ? 2 : t === "长期待摊费用" ? 3 : 1);

/** 页面顶部错误/加载条。 */
function StateBar({ loading, error }: { loading?: boolean; error?: string | null }) {
    if (error) {
        return <div className="px-4 pb-2 text-[13px] text-[#ef4444]">{error}</div>;
    }
    if (loading) {
        return <div className="px-4 pb-2 text-[13px] text-slate-400">加载中…</div>;
    }
    return null;
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
            <div className="text-center text-sm leading-6 text-slate-700">{text}</div>
        </Dialog>
    );
}

/** 账套上下文。 */
function useAccountContext() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    return { accountSetId, token };
}

/** 固定资产数据 Hook（类别 / 卡片 / 核对，均与期间无关）。 */
function useFixedAssetData() {
    const { accountSetId, token } = useAccountContext();
    const [categories, setCategories] = useState<FixedAssetCategoryDto[]>([]);
    const [assets, setAssets] = useState<FixedAssetDto[]>([]);
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
            const [cats, cards, chk] = await Promise.all([
                api.accountsetsAccountSetIdFixedassetsCategoriesGet({ accountSetId }),
                api.accountsetsAccountSetIdFixedassetsAssetsGet({ accountSetId }),
                api.accountsetsAccountSetIdFixedassetsCheckGet({ accountSetId }),
            ]);
            setCategories(cats ?? []);
            setAssets(cards ?? []);
            setCheck(chk ?? []);
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载资产数据失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void reload();
    }, [reload]);

    return { categories, assets, check, loading, error, reload };
}

/** 期间选择：默认跟随账套工作期间，切换账套自动复位。 */
function useAssetPeriod() {
    const accountSetId = useCurrentAccountSetId() ?? "";
    const defaultPeriod = useDefaultAccountingPeriod();
    const [override, setOverride] = useState<AccountingPeriod | null>(null);
    useEffect(() => setOverride(null), [accountSetId]);
    const period = override ?? defaultPeriod;
    return { period, setOverride, step: (d: number) => setOverride(shiftPeriod(period, d)) };
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
    { key: "amount", title: "金额", align: "right", width: "120px" },
    { key: "ops", title: "关联凭证", width: "120px" },
];

export function LemonAssetManage() {
    const [tab, setTab] = useState(1);
    const { period, step } = useAssetPeriod();
    const data = useFixedAssetData();
    const { token, accountSetId } = useAccountContext();

    const [initDialog, setInitDialog] = useState(false);
    const [showAdd, setShowAdd] = useState(false);
    const [addForm, setAddForm] = useState({ categoryId: "", name: "", model: "", dept: "", startDate: "", originValue: "", initialAccumulatedDepreciation: "" });
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    // 变更记录（tab2）按需拉取。
    const [changes, setChanges] = useState<FixedAssetChangeDto[]>([]);
    const [changesLoading, setChangesLoading] = useState(false);
    const loadChanges = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setChangesLoading(true);
        try {
            setChanges(await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsChangesGet({ accountSetId }));
        } catch (e) {
            setToast(await apiErrorMessage(e, "加载变更记录失败"));
        } finally {
            setChangesLoading(false);
        }
    }, [accountSetId, token]);
    useEffect(() => {
        if (tab === 2) {
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
            await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsAssetsPost({
                accountSetId,
                saveFixedAssetRequest: {
                    categoryId: addForm.categoryId,
                    name: addForm.name.trim(),
                    model: addForm.model.trim() || null,
                    dept: addForm.dept.trim() || null,
                    startDate: new Date(addForm.startDate),
                    originValue: Number(addForm.originValue) || 0,
                    initialAccumulatedDepreciation: Number(addForm.initialAccumulatedDepreciation) || 0,
                },
            });
            setShowAdd(false);
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
        } catch (e) {
            setToast(await apiErrorMessage(e, "计提折旧失败"));
        } finally {
            setBusy(false);
        }
    };

    const addAssetBtn = (
        <Btn onClick={() => setShowAdd(true)}>增加资产</Btn>
    );

    /* tab === 0 资产初始化 */
    if (tab === 0)
        return (
            <Page title="资产管理" subtitle="期初录入、资产列表、变更与计提折旧" icon={Building}>
                <SubTabs tabs={ASSET_TABS} active={0} onChange={setTab} />
                <ToolRow
                    left={<span className="text-[13px] text-slate-400">资产启用期间：{periodLabel(period)}</span>}
                    right={
                        <>
                            <Btn variant="primary" onClick={() => setInitDialog(true)}>
                                录入期初资产
                            </Btn>
                            <CheckBox label="显示所有信息" />
                            {addAssetBtn}
                            <WipBtn>复制资产</WipBtn>
                            <WipBtn title="导入资产尚未接入后端">导入</WipBtn>
                            <WipBtn dropdown title="导出资产尚未接入后端">导出</WipBtn>
                            <Btn dropdown onClick={() => window.print()}>打印</Btn>
                            <WipBtn dropdown title="更多操作尚未接入后端">更多</WipBtn>
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
                            { key: "name", title: "资产名称", width: "160px" },
                            { key: "model", title: "规格型号", width: "110px" },
                            { key: "date", title: "开始使用日期", width: "130px" },
                            { key: "origin", title: "资产原值", align: "right" },
                            { key: "dep", title: "期初累计折旧", align: "right" },
                            { key: "net", title: "资产净值", align: "right" },
                        ]}
                        rows={data.assets.map((a) => ({
                            no: a.code,
                            type: a.categoryName,
                            name: a.name,
                            model: a.model,
                            date: toDateInput(a.startDate),
                            origin: (a.originValue ?? 0).toFixed(2),
                            dep: (a.accumulatedDepreciation ?? 0).toFixed(2),
                            net: (a.netValue ?? ((a.originValue ?? 0) - (a.accumulatedDepreciation ?? 0))).toFixed(2),
                        }))}
                        empty={<EmptyTip text="还没有期初资产，点击上方「增加资产」录入期初卡片。" />}
                    />
                </div>
                <div className="px-4 pb-3 text-[13px] text-[#ef4444]">亲，资产初始化数据要与期初的资产数据一致哦！</div>
                <Pager total={data.assets.length} count={data.assets.length} />
                {initDialog && (
                    <InitDialog
                        onClose={() => setInitDialog(false)}
                        onConfirm={() => {
                            setInitDialog(false);
                            setShowAdd(true);
                        }}
                    />
                )}
                {showAdd && renderAddDialog()}
                {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            </Page>
        );

    /* tab === 2 变更记录及生成凭证 */
    if (tab === 2)
        return (
            <Page title="资产管理" subtitle="期初录入、资产列表、变更与计提折旧" icon={Building}>
                <SubTabs tabs={ASSET_TABS} active={2} onChange={setTab} />
                <ToolRow
                    left={<PeriodStepper text={periodLabel(period)} onPrev={() => step(-1)} onNext={() => step(1)} />}
                    right={
                        <span className="cursor-pointer" onClick={() => void loadChanges()} title="刷新">
                            <RefreshIcon />
                        </span>
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
                            amount: c.amount != null ? c.amount.toFixed(2) : "",
                            ops: <WipBtn title="变更记录生成凭证尚未接入后端">生成凭证</WipBtn>,
                        }))}
                        empty={<EmptyTip />}
                    />
                </div>
                <Pager total={changes.length} count={changes.length} />
                {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            </Page>
        );

    /* tab === 3 计提折旧及生成凭证 */
    if (tab === 3)
        return (
            <Page title="资产管理" subtitle="期初录入、资产列表、变更与计提折旧" icon={Building}>
                <SubTabs tabs={ASSET_TABS} active={3} onChange={setTab} />
                <WipNotice text="计提折旧会按当前会计期间为在用资产登记折旧变更；折旧凭证生成功能尚未接入后端。" />
                <div className="mx-4 mt-2 rounded-xl border border-[#fde68a] bg-[#fffbeb] px-4 py-2.5 text-[13px] leading-5 text-[#92640b]">
                    亲，点击“计提折旧”即可生成折旧摊销明细，灰色按钮表示尚未接入后端；如有疑问，请将鼠标移至灰字上面查看提示。
                </div>
                <div className="flex-1 px-4 pt-3">
                    <Table
                        cols={[
                            {
                                key: "period",
                                title: "会计期间",
                                render: () => (
                                    <PeriodStepper text={periodLabel(period)} onPrev={() => step(-1)} onNext={() => step(1)} />
                                ),
                            },
                            {
                                key: "active",
                                title: "在用资产",
                                render: () => `${data.assets.filter((a) => a.statusText === "使用中").length} 项`,
                            },
                            {
                                key: "dep",
                                title: "计提折旧",
                                render: () => <span className="text-slate-400">{busy ? "计提中…" : "待计提"}</span>,
                            },
                            { key: "voucher", title: "折旧凭证" },
                            {
                                key: "ops",
                                title: "操作",
                                render: () => (
                                    <span className="inline-flex items-center gap-2">
                                        <Btn variant={busy ? "disabled" : "primary"} onClick={() => void depreciate()}>
                                            计提折旧
                                        </Btn>
                                        <WipBtn title="折旧凭证生成尚未接入后端">
                                            生成凭证 <CircleHelp className="h-3 w-3" />
                                        </WipBtn>
                                    </span>
                                ),
                            },
                        ]}
                        rows={[{}]}
                    />
                </div>
                {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            </Page>
        );

    /* tab === 1 资产列表 */
    const cols: Col[] = [
        { key: "ck", title: <CheckBox label="" />, width: "36px" },
        { key: "no", title: "资产编号", width: "120px" },
        { key: "type", title: "资产类别", width: "130px" },
        { key: "attr", title: "资产属性", width: "110px" },
        { key: "name", title: "资产名称", width: "160px" },
        { key: "model", title: "规格型号", width: "110px" },
        { key: "date", title: "开始使用日期", width: "130px" },
        { key: "origin", title: "资产原值", align: "right" },
        { key: "dep", title: "累计折旧", align: "right" },
        { key: "net", title: "资产净值", align: "right" },
        { key: "ops", title: "操作", width: "110px" },
    ];
    return (
        <Page title="资产管理" subtitle="期初录入、资产列表、变更与计提折旧" icon={Building}>
            <SubTabs tabs={ASSET_TABS} active={1} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <PeriodStepper text={periodLabel(period)} onPrev={() => step(-1)} onNext={() => step(1)} />
                        <CheckBox label="显示所有信息" />
                    </>
                }
                right={
                    <>
                        {addAssetBtn}
                        <WipBtn>复制资产</WipBtn>
                        <WipBtn title="导入资产尚未接入后端">导入</WipBtn>
                        <WipBtn dropdown title="导出资产尚未接入后端">导出</WipBtn>
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <WipBtn dropdown title="批量操作尚未接入后端">批量操作</WipBtn>
                        <span className="cursor-pointer" onClick={() => void data.reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <div className="flex-1 px-4">
                <StateBar loading={data.loading} error={data.error} />
                <Table
                    cols={cols}
                    rows={data.assets.map((a) => ({
                        __asset: a,
                        no: a.code,
                        type: a.categoryName,
                        attr: a.attrText,
                        name: a.name,
                        model: a.model,
                        date: toDateInput(a.startDate),
                        origin: (a.originValue ?? 0).toFixed(2),
                        dep: (a.accumulatedDepreciation ?? 0).toFixed(2),
                        net: (a.netValue ?? ((a.originValue ?? 0) - (a.accumulatedDepreciation ?? 0))).toFixed(2),
                        ops: (
                            <span className="flex gap-2">
                                <span
                                    className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline"
                                    onClick={() => setClearing(a)}
                                >
                                    清理
                                </span>
                                <span
                                    className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline"
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
            <Pager total={data.assets.length} count={data.assets.length} />
            {showAdd && renderAddDialog()}
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
                    text={`确定删除资产「${deleting.name ?? ""}」吗？此操作不可恢复。`}
                    busy={busy}
                    onClose={() => setDeleting(null)}
                    onConfirm={() => void doDelete()}
                />
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );

    function renderAddDialog() {
        return (
            <Dialog
                title="增加资产"
                onClose={() => setShowAdd(false)}
                footer={
                    <div className="flex justify-center gap-3">
                        <Btn variant={busy ? "disabled" : "primary"} onClick={() => void createAsset()}>
                            保存
                        </Btn>
                        <Btn onClick={() => setShowAdd(false)}>取消</Btn>
                    </div>
                }
            >
                <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                    <label className="flex items-center gap-3">
                        <span className="w-[110px] text-right">资产类别：</span>
                        <Select
                            className="w-[220px]"
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
                            <span className="w-[110px] text-right">{label}</span>
                            {isDate ? (
                                <TextInput
                                    type="date"
                                    className="w-[220px]"
                                    value={String(addForm[key as keyof typeof addForm] ?? "")}
                                    onChange={(v) => setAddForm({ ...addForm, [key as keyof typeof addForm]: v })}
                                />
                            ) : (
                                <TextInput
                                    className={`w-[220px]${numeric ? " text-right" : ""}`}
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
                    <Btn variant="primary" className="w-[96px]" onClick={onConfirm}>
                        去录入
                    </Btn>
                    <Btn className="w-[96px]" onClick={onClose}>
                        关闭
                    </Btn>
                </div>
            }
        >
            <div className="text-center text-sm leading-6 text-slate-700">
                请确定是否存在资产的期初数据？如有期初，请点击「去录入」
                <br />
                前往录入期初资产卡片，否则请关闭~
            </div>
        </Dialog>
    );
}

/* ---------- 折旧摊销明细表 ---------- */

export function LemonAssetDepreciation() {
    const { token, accountSetId } = useAccountContext();
    const { period, step } = useAssetPeriod();
    const [ledger, setLedger] = useState<DepreciationLedgerRowDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

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
        <Page title="折旧摊销明细表" subtitle={`${label} 各资产本期与累计折旧明细`} icon={TrendingDown}>
            <SubTabs tabs={["折旧摊销明细表"]} />
            <ToolRow
                left={<PeriodStepper text={label} onPrev={() => step(-1)} onNext={() => step(1)} />}
                right={
                    <>
                        <WipBtn title="导出折旧明细尚未接入后端">导出</WipBtn>
                        <Btn onClick={() => window.print()}>打印</Btn>
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
                        origin: (r.originValue ?? 0).toFixed(2),
                        dep: (r.periodDepreciation ?? 0).toFixed(2),
                        yearDep: (r.yearAccumulated ?? 0).toFixed(2),
                        totalDep: (r.totalAccumulated ?? 0).toFixed(2),
                        impair: (r.impairment ?? 0).toFixed(2),
                        net: (r.netValue ?? 0).toFixed(2),
                    }))}
                    empty={<EmptyTip />}
                />
            </div>
            <Pager total={ledger.length} count={ledger.length} />
        </Page>
    );
}

/* ---------- 资产汇总表（部门/类别双页签） ---------- */

export function LemonAssetSummary() {
    const { token, accountSetId } = useAccountContext();
    const { period, step } = useAssetPeriod();
    const [tab, setTab] = useState(0);
    const [rows, setRows] = useState<FixedAssetSummaryRowDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

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

    const fmt = (v: number | null | undefined) => Number(v ?? 0).toFixed(2);
    const sub = ["期初数", "本期增加", "本期减少", "期末数"];
    const groupDefs: Array<{ title: string; keys: Array<keyof FixedAssetSummaryRowDto> }> = [
        { title: "资产原值", keys: ["originOpening", "originIncrease", "originDecrease", "originClosing"] },
        { title: "累计折旧", keys: ["depOpening", "depIncrease", "depDecrease", "depClosing"] },
        { title: "减值准备", keys: ["impairOpening", "impairIncrease", "impairDecrease", "impairClosing"] },
        { title: "资产净值", keys: ["netOpening", "netIncrease", "netDecrease", "netClosing"] },
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
    const label = periodLabel(period);
    return (
        <Page title="资产汇总表" subtitle="期初、本期增减与期末数按部门/类别归集" icon={PieChart}>
            <SubTabs tabs={["按使用部门汇总", "按资产类别汇总"]} active={tab} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <span className="text-[13px] text-slate-500">会计期间:</span>
                        <span className="inline-flex h-8 items-center gap-2 rounded-lg border border-slate-200 bg-white px-2.5 text-sm text-slate-700">
                            {label} <CalendarDays className="h-3.5 w-3.5 text-slate-400" />
                        </span>
                        <span className="text-slate-400">至</span>
                        <span className="inline-flex h-8 items-center gap-2 rounded-lg border border-slate-200 bg-white px-2.5 text-sm text-slate-700">
                            {label} <CalendarDays className="h-3.5 w-3.5 text-slate-400" />
                        </span>
                        <PeriodStepper text={label} onPrev={() => step(-1)} onNext={() => step(1)} />
                    </>
                }
                right={
                    <>
                        <WipBtn title="导出汇总表尚未接入后端">导出</WipBtn>
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
            <Pager total={rows.length} count={rows.length} />
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
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
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
            <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                <label className="flex items-center gap-3">
                    <span className="w-[110px] text-right">类别编码：</span>
                    <TextInput className="w-[220px]" value={form.code} onChange={(v) => set({ code: v })} />
                </label>
                <label className="flex items-center gap-3">
                    <span className="w-[110px] text-right">类别名称：</span>
                    <TextInput className="w-[220px]" value={form.name} onChange={(v) => set({ name: v })} />
                </label>
                <label className="flex items-center gap-3">
                    <span className="w-[110px] text-right">折旧方法：</span>
                    <Select className="w-[220px]" value={form.method} onChange={(v) => set({ method: v })} options={CATEGORY_METHOD_OPTIONS} />
                </label>
                <label className="flex items-center gap-3">
                    <span className="w-[110px] text-right">资产属性：</span>
                    <Select className="w-[220px]" value={form.attr} onChange={(v) => set({ attr: v })} options={CATEGORY_ATTR_OPTIONS} />
                </label>
                <label className="flex items-center gap-3">
                    <span className="w-[110px] text-right">资产科目：</span>
                    <Select
                        className="w-[220px]"
                        value={form.subjectAccountId}
                        onChange={(v) => set({ subjectAccountId: v })}
                        options={[
                            { value: "", label: "（请选择）" },
                            ...accounts.map((acc) => ({ value: acc.id ?? "", label: `${acc.code ?? ""} ${acc.name ?? ""}` })),
                        ]}
                    />
                </label>
                <label className="flex items-center gap-3">
                    <span className="w-[110px] text-right">使用月数：</span>
                    <TextInput className="w-[220px] text-right" value={form.months} onChange={(v) => set({ months: v.replace(/[^\d]/g, "") })} />
                </label>
                <label className="flex items-center gap-3">
                    <span className="w-[110px] text-right">净残值率(%)：</span>
                    <TextInput className="w-[220px] text-right" value={form.residualRate} onChange={(v) => set({ residualRate: v.replace(/[^\d.]/g, "") })} />
                </label>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Dialog>
    );
}

export function LemonAssetCategory() {
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
                        className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline"
                        onClick={() => setEditing(row.__cat as FixedAssetCategoryDto)}
                    >
                        编辑
                    </span>
                    <span
                        className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline"
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
        <Page title="资产类别设置" subtitle="类别编码、折旧方法与资产科目默认值" icon={Layers}>
            <SubTabs tabs={["资产类别设置"]} />
            <ToolRow
                left={<Btn onClick={() => setEditing("new")}>新增</Btn>}
                right={
                    <>
                        <TextInput className="w-[220px]" value={keyword} onChange={setKeyword} placeholder="输入编码或名称" />
                        <WipBtn title="导出类别尚未接入后端">导出</WipBtn>
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
            <Pager total={filtered.length} count={filtered.length} />
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

export function LemonAssetCheck() {
    const { token, accountSetId } = useAccountContext();
    const { period, step } = useAssetPeriod();
    const [check, setCheck] = useState<FixedAssetCheckRowDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // 后端核对接口不按期间过滤，切换期间仍重新取数以保持一致口径。
    const reload = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setLoading(true);
        try {
            setCheck(await new FixedAssetsApi(apiConfig(token)).accountsetsAccountSetIdFixedassetsCheckGet({ accountSetId }));
            setError(null);
        } catch (e) {
            setCheck([]);
            setError(await apiErrorMessage(e, "加载核对总账失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void reload();
    }, [reload, period.period]);

    const th = "border-b border-slate-200 bg-muted px-3 py-2.5 text-[12.5px] font-medium text-slate-500";
    const td = "border-b border-slate-100 px-3 py-2.5 text-[13px] text-slate-600 tabular-nums";
    return (
        <Page title="资产核对总账" subtitle="资产台账与总账科目余额逐行核对" icon={ClipboardCheck}>
            <SubTabs tabs={["资产核对总账"]} />
            <ToolRow
                left={<PeriodStepper text={periodLabel(period)} onPrev={() => step(-1)} onNext={() => step(1)} />}
                right={
                    <>
                        <Btn onClick={() => window.print()}>打印</Btn>
                        <WipBtn title="导出核对表尚未接入后端">导出</WipBtn>
                        <span className="cursor-pointer" onClick={() => void reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <StateBar loading={loading} error={error} />
            <div className="flex-1 px-4">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr>
                            <th className={`${th} w-[140px] text-left`}>项目</th>
                            <th className={`${th} text-left`}>名称</th>
                            <th className={`${th} text-right`}>期初余额</th>
                            <th className={`${th} text-right`}>借方</th>
                            <th className={`${th} text-right`}>贷方</th>
                            <th className={`${th} text-right`}>余额</th>
                        </tr>
                    </thead>
                    <tbody>
                        {check.length === 0 && (
                            <tr>
                                <td colSpan={6} className="py-10 text-center text-[13px] text-slate-400">
                                    暂无数据
                                </td>
                            </tr>
                        )}
                        {(() => {
                            const groups: Array<{ name: string; rows: FixedAssetCheckRowDto[] }> = [];
                            for (const r of check) {
                                const item = r.item ?? "";
                                if (item.startsWith("▼ ")) {
                                    groups.push({ name: item.slice(2), rows: [] });
                                } else if (groups.length) {
                                    groups[groups.length - 1].rows.push(r);
                                }
                            }
                            return groups.map((g) => (
                                <Fragment key={g.name}>
                                    <tr>
                                        <td colSpan={6} className={`${td} font-bold`}>
                                            <span className="inline-flex items-center gap-1">
                                                <ChevronDown className="h-3 w-3 text-slate-400" /> {g.name}
                                            </span>
                                        </td>
                                    </tr>
                                    {g.rows.map((r, i) => (
                                        <tr key={g.name + i}>
                                            <td className={r.isDiff ? `${td} text-[#2f6bff]` : td}>{r.isDiff ? "差异" : r.item}</td>
                                            <td className={td}>{r.name}</td>
                                            <td className={`${td} text-right`}>{r.opening ? r.opening.toFixed(2) : ""}</td>
                                            <td className={`${td} text-right`}>{r.debit ? r.debit.toFixed(2) : ""}</td>
                                            <td className={`${td} text-right`}>{r.credit ? r.credit.toFixed(2) : ""}</td>
                                            <td className={`${td} text-right`}>{r.balance ? r.balance.toFixed(2) : ""}</td>
                                        </tr>
                                    ))}
                                </Fragment>
                            ));
                        })()}
                    </tbody>
                </table>
            </div>
        </Page>
    );
}
