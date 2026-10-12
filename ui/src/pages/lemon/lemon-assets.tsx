/**
 * 复刻区资产模块（对应柠檬云 /FixedAssets/*）：
 * 资产管理（初始化/列表/变更记录/计提折旧四页签 + 期初确认弹窗）、
 * 折旧摊销明细表、资产汇总表（部门/类别双页签）、资产类别设置、资产核对总账。
 */
import { Fragment, useCallback, useEffect, useState } from "react";
import { CalendarDays, ChevronDown, CircleHelp } from "lucide-react";

import {
    Btn,
    CheckBox,
    Dialog,
    EmptyTip,
    Page,
    Pager,
    RefreshIcon,
    SubTabs,
    Table,
    Toast,
    ToolRow,
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import {
    AccountsApi,
    FixedAssetsApi,
    type AccountDto,
    type FixedAssetCategoryDto,
    type FixedAssetCheckRowDto,
    type FixedAssetDto,
    type DepreciationLedgerRowDto,
    type FixedAssetSummaryRowDto,
} from "@/apis-gen";

/** 账套上下文。 */
function useAccountContext() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    return { accountSetId, token };
}

/** 固定资产数据 Hook（类别 / 卡片 / 明细 / 汇总 / 核对）。 */
function useFixedAssetData() {
    const { accountSetId, token } = useAccountContext();
    const [categories, setCategories] = useState<FixedAssetCategoryDto[]>([]);
    const [assets, setAssets] = useState<FixedAssetDto[]>([]);
    const [ledger, setLedger] = useState<DepreciationLedgerRowDto[]>([]);
    const [summary, setSummary] = useState<FixedAssetSummaryRowDto[]>([]);
    const [check, setCheck] = useState<FixedAssetCheckRowDto[]>([]);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) return;
        const api = new FixedAssetsApi(apiConfig(token));
        try {
            const [cats, cards] = await Promise.all([
                api.accountsetsAccountSetIdFixedassetsCategoriesGet({ accountSetId }),
                api.accountsetsAccountSetIdFixedassetsAssetsGet({ accountSetId }),
            ]);
            setCategories(cats);
            setAssets(cards);
            setLedger(await api.accountsetsAccountSetIdFixedassetsDepreciationLedgerGet({ accountSetId, period: 202510 }));
            setSummary(await api.accountsetsAccountSetIdFixedassetsSummaryGet({ accountSetId, startPeriod: 202510, endPeriod: 202510 }));
            setCheck(await api.accountsetsAccountSetIdFixedassetsCheckGet({ accountSetId }));
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载资产数据失败。"));
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void reload();
    }, [reload]);

    return { categories, assets, ledger, summary, check, error, reload };
}


/** 资产模块公共「期初确认」弹窗。 */
function InitDialog({ onClose }: { onClose: () => void }) {
    return (
        <Dialog
            title="提示"
            onClose={onClose}
            footer={
                <Btn variant="primary" className="w-[96px]" onClick={onClose}>
                    确定
                </Btn>
            }
        >
            <div className="text-center text-sm leading-6 text-[#333]">
                请确定是否存在资产的期初数据？如有期初，请点击确定
                <br />
                前往录入期初，否则请关闭~
            </div>
        </Dialog>
    );
}

/** 绿色月份/期间小胶囊。 */
function GreenChip({ text }: { text: string }) {
    return (
        <span className="inline-flex h-8 items-center rounded-[2px] bg-[#44b449] px-2.5 text-sm text-white">
            {text}
            <span className="mx-1.5 h-3.5 w-px bg-white/50" />
            ▾
        </span>
    );
}

/* ---------- 资产管理（四页签） ---------- */

const ASSET_TABS = ["资产初始化", "资产列表", "变更记录及生成凭证", "计提折旧及生成凭证"];

export function LemonAssetManage() {
    const [tab, setTab] = useState(1);
    const [dialog, setDialog] = useState(true);
    const close = () => setDialog(false);
    const data = useFixedAssetData();
    const { token, accountSetId } = useAccountContext();
    const [showAdd, setShowAdd] = useState(false);
    const [addForm, setAddForm] = useState({ categoryId: "", name: "", model: "", dept: "", startDate: "", originValue: "", initialAccumulatedDepreciation: "" });
    const [addToast, setAddToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    const createAsset = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (!addForm.name.trim() || !addForm.categoryId || !addForm.startDate) {
            setAddToast("类别、名称、开始使用日期必填");
            return;
        }
        setBusy(true);
        try {
            await new FixedAssetsApi(apiConfig(token!)).accountsetsAccountSetIdFixedassetsAssetsPost({
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
            setAddToast("资产已增加");
            await data.reload();
        } catch (e) {
            setAddToast(await apiErrorMessage(e, "增加资产失败"));
        } finally {
            setBusy(false);
        }
    };

    const addAssetBtn = (
        <Btn onClick={() => setShowAdd(true)}>增加资产</Btn>
    );

    if (tab === 0)
        return (
            <Page>
                <SubTabs tabs={ASSET_TABS} active={0} onChange={setTab} />
                <ToolRow
                    left={<span className="text-[13px] text-[#999]">资产启用期间：2025年10月</span>}
                    right={
                        <>
                            <span className="cursor-pointer text-[13px] text-[#3385ff]">关闭无形资产</span>
                            <CheckBox label="显示所有信息" />
                            {addAssetBtn}
                            <Btn>复制资产</Btn>
                            <Btn>导入</Btn>
                            <Btn dropdown>导出</Btn>
                            <Btn dropdown onClick={() => window.print()}>打印</Btn>
                            <Btn dropdown>更多</Btn>
                            <RefreshIcon />
                        </>
                    }
                />
                <div className="flex-1 px-4">
                    <table className="w-full border-collapse text-sm">
                        <thead>
                            <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                                {["", "资产编号", "资产类别", "资产属性", "资产名称", "规格型号", "开始使用日期", "资产原值", "累计折旧", "资产净值", "操作"].map(
                                    (t, i) => (
                                        <th key={i} className="border-b border-[#e8e8e8] px-3 py-2.5 text-left font-normal">
                                            {t}
                                        </th>
                                    ),
                                )}
                            </tr>
                        </thead>
                    </table>
                    <EmptyTip />
                </div>
                <div className="px-4 pb-3 text-[13px] text-[#f5222d]">亲，资产初始化数据要与期初的资产数据一致哦！</div>
                <Pager total={0} count={0} />
                {dialog && <InitDialog onClose={close} />}
            </Page>
        );

    if (tab === 2)
        return (
            <Page>
                <SubTabs tabs={ASSET_TABS} active={2} onChange={setTab} />
                <ToolRow left={<GreenChip text="2025年10月" />} right={<RefreshIcon />} />
                <div className="flex-1 px-4">
                    <table className="w-full border-collapse text-sm">
                        <thead>
                            <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                                {["", "变动单类别", "资产编号", "资产名称", "变动前内容", "变动后内容", "变动期间", "关联凭证", "操作"].map(
                                    (t, i) => (
                                        <th key={i} className="border-b border-[#e8e8e8] px-3 py-2.5 text-left font-normal">
                                            {t}
                                        </th>
                                    ),
                                )}
                            </tr>
                        </thead>
                    </table>
                    <EmptyTip />
                </div>
                <Pager total={0} count={0} />
                {dialog && <InitDialog onClose={close} />}
            </Page>
        );

    if (tab === 3)
        return (
            <Page>
                <SubTabs tabs={ASSET_TABS} active={3} onChange={setTab} />
                <div className="mx-4 mt-2 rounded-[2px] bg-[#fff7e8] px-4 py-2.5 text-[13px] leading-5 text-[#e08a00]">
                    亲，点击“计提折旧”即可生成折旧摊销明细表，点击“生成凭证”即可生成折旧凭证！灰色表示不能操作，如有疑问，请将鼠标移至灰字上面，查看提示操作。
                </div>
                <div className="flex-1 px-4 pt-3">
                    <table className="w-full border-collapse text-sm">
                        <thead>
                            <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                                {["会计期间", "新增资产", "变更记录", "计提折旧", "折旧凭证", "操作"].map((t) => (
                                    <th key={t} className="border-b border-[#e8e8e8] px-3 py-2.5 text-left font-normal">
                                        {t}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            <tr className="border-b border-[#f0f0f0]">
                                <td className="px-3 py-2.5 text-[13px]">2025年10月</td>
                                <td className="px-3 py-2.5 text-[13px] text-[#bbb]">无数据</td>
                                <td className="px-3 py-2.5 text-[13px] text-[#bbb]">无数据</td>
                                <td className="px-3 py-2.5 text-[13px] text-[#bbb]">无数据</td>
                                <td className="px-3 py-2.5 text-[13px]" />
                                <td className="px-3 py-2.5 text-[13px]">
                                    <span className="inline-flex items-center gap-1 text-[#3385ff]">
                                        计提折旧 <CircleHelp className="h-3 w-3" />
                                    </span>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>
                <Pager total={1} count={1} />
                {dialog && <InitDialog onClose={close} />}
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
        { key: "dep", title: "本期折旧", align: "right" },
        { key: "net", title: "资产净值", align: "right" },
        { key: "ops", title: "操作", width: "80px" },
    ];
    return (
        <Page>
            <SubTabs tabs={ASSET_TABS} active={1} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <GreenChip text="2025年10月" />
                        <CheckBox label="显示所有信息" />
                    </>
                }
                right={
                    <>
                        {addAssetBtn}
                        <Btn>复制资产</Btn>
                        <Btn>导入</Btn>
                        <Btn dropdown>导出</Btn>
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <Btn dropdown>批量操作</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                {data.error && <div className="px-3 py-2 text-[13px] text-[#f5222d]">{data.error}</div>}
                <Table
                    cols={cols}
                    rows={data.assets.map((a) => ({
                        no: a.code,
                        type: a.categoryName,
                        attr: a.attrText,
                        name: a.name,
                        model: a.model,
                        date: a.startDate ? new Date(a.startDate).toISOString().substring(0, 10) : "",
                        origin: (a.originValue ?? 0).toFixed(2),
                        dep: (a.accumulatedDepreciation ?? 0).toFixed(2),
                        net: ((a.originValue ?? 0) - (a.accumulatedDepreciation ?? 0)).toFixed(2),
                    }))}
                    empty={<EmptyTip />}
                />
            </div>
            <Pager total={data.assets.length} count={data.assets.length} />
            {dialog && <InitDialog onClose={close} />}
            {/* 增加资产弹窗 */}
            {showAdd && (
                <Dialog
                    title="增加资产"
                    onClose={() => setShowAdd(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void createAsset()}>
                                保存
                            </Btn>
                            <Btn onClick={() => setShowAdd(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-[#333]">
                        <label className="flex items-center gap-3">
                            <span className="w-[110px] text-right">资产类别：</span>
                            <select
                                className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] bg-white px-2 outline-none focus:border-[#44b449]"
                                value={addForm.categoryId}
                                onChange={(e) => setAddForm({ ...addForm, categoryId: e.target.value })}
                            >
                                <option value="">（请选择）</option>
                                {(data.categories ?? []).map((c) => (
                                    <option key={c.id} value={c.id}>
                                        {`${c.code ?? ""} ${c.name ?? ""}`}
                                    </option>
                                ))}
                            </select>
                        </label>
                        {[
                            ["资产名称：", "name", false],
                            ["规格型号：", "model", false],
                            ["使用部门：", "dept", false],
                            ["开始使用日期：", "startDate", true],
                            ["资产原值：", "originValue", false],
                            ["期初累计折旧：", "initialAccumulatedDepreciation", false],
                        ].map(([label, key, isDate]) => (
                            <label key={String(key)} className="flex items-center gap-3">
                                <span className="w-[110px] text-right">{label}</span>
                                <input
                                    type={isDate ? "date" : "text"}
                                    className={"h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]" + (isDate ? "" : key === "originValue" || key === "initialAccumulatedDepreciation" ? " text-right" : "")}
                                    value={String(addForm[key as keyof typeof addForm] ?? "")}
                                    onChange={(e) =>
                                        setAddForm({
                                            ...addForm,
                                            [key as keyof typeof addForm]: isDate ? e.target.value : String(e.target.value).replace(/[^\d.]/g, ""),
                                        })
                                    }
                                />
                            </label>
                        ))}
                    </div>
                </Dialog>
            )}
            {addToast && <Toast text={addToast} onClose={() => setAddToast(null)} />}
        </Page>
    );
}

/* ---------- 折旧摊销明细表 ---------- */

export function LemonAssetDepreciation() {
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
    const data = useFixedAssetData();
    return (
        <Page>
            <SubTabs tabs={["折旧摊销明细表"]} />
            <ToolRow left={<GreenChip text="2025年10月" />} right={<><Btn>导出</Btn><Btn onClick={() => window.print()}>打印</Btn><RefreshIcon /></>} />
            <div className="flex-1 px-4">
                <Table
                    cols={cols}
                    rows={data.ledger.map((r) => ({
                        period: "2025年10月",
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
            <Pager total={data.ledger.length} count={data.ledger.length} />
        </Page>
    );
}

/* ---------- 资产汇总表（部门/类别双页签） ---------- */

export function LemonAssetSummary() {
    const [tab, setTab] = useState(0);
    const data = useFixedAssetData();
    const th = "border-b border-[#e8e8e8] bg-[#f0f7f0] px-3 py-2 text-[13px] font-normal text-[#333]";
    const sub = ["期初数", "本期增加", "本期减少", "期末数"];
    return (
        <Page>
            <SubTabs tabs={["按使用部门汇总", "按资产类别汇总"]} active={tab} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <span className="text-[13px] text-[#666]">会计期间:</span>
                        <span className="inline-flex h-8 items-center gap-2 rounded-[2px] border border-[#d9d9d9] px-2.5 text-sm">
                            2025年10月 <CalendarDays className="h-3.5 w-3.5 text-[#999]" />
                        </span>
                        <span className="text-[#999]">至</span>
                        <span className="inline-flex h-8 items-center gap-2 rounded-[2px] border border-[#d9d9d9] px-2.5 text-sm">
                            2025年10月 <CalendarDays className="h-3.5 w-3.5 text-[#999]" />
                        </span>
                        <Btn variant="primary">查询</Btn>
                    </>
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
                        <tr>
                            <th rowSpan={2} className={`${th} text-left`}>
                                {tab === 0 ? "部门名称" : "资产类别"}
                            </th>
                            {["资产原值", "累计折旧", "减值准备", "资产净值"].map((g) => (
                                <th key={g} colSpan={4} className={`${th} text-center`}>
                                    {g}
                                </th>
                            ))}
                        </tr>
                        <tr>
                            {["", "", "", ""].flatMap((_, gi) =>
                                sub.map((s) => (
                                    <th key={`${gi}${s}`} className={`${th} text-right`}>
                                        {s}
                                    </th>
                                )),
                            )}
                        </tr>
                    </thead>
                </table>
                {data.summary.length === 0 ? (
                    <EmptyTip />
                ) : (
                    <table className="w-full border-collapse text-sm">
                        <tbody>
                            {data.summary.map((r) => (
                                <tr key={r.groupName} className="border-b border-[#f0f0f0]">
                                    <td className="px-3 py-2.5 text-[13px] text-[#333]">{r.groupName}</td>
                                    {[r.originOpening, r.originIncrease, r.originDecrease, r.originClosing,
                                      r.depOpening, r.depIncrease, r.depDecrease, r.depClosing,
                                      r.impairOpening, r.impairIncrease, r.impairDecrease, r.impairClosing,
                                      r.netOpening, r.netIncrease, r.netDecrease, r.netClosing].map((v, i) => (
                                        <td key={i} className="px-3 py-2.5 text-right text-[13px] text-[#333]">{Number(v ?? 0).toFixed(2)}</td>
                                    ))}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </div>
            <Pager total={0} count={0} />
        </Page>
    );
}

/* ---------- 资产类别设置 ---------- */

export function LemonAssetCategory() {
    const data = useFixedAssetData();
    const { token, accountSetId } = useAccountContext();
    const [showAdd, setShowAdd] = useState(false);
    const [form, setForm] = useState({ code: "", name: "", method: 1, attr: 1, subjectAccountId: "", months: "", residualRate: "" });
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
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

    const create = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (!form.code.trim() || !form.name.trim() || !form.subjectAccountId) {
            setToast("编码、名称与资产科目必填");
            return;
        }
        setBusy(true);
        try {
            await new FixedAssetsApi(apiConfig(token!)).accountsetsAccountSetIdFixedassetsCategoriesPost({
                accountSetId,
                saveFixedAssetCategoryRequest: {
                    code: form.code.trim(),
                    name: form.name.trim(),
                    method: form.method,
                    attr: form.attr,
                    subjectAccountId: form.subjectAccountId,
                    months: Number(form.months) || 0,
                    residualRate: Number(form.residualRate) || 0,
                },
            });
            setShowAdd(false);
            setForm({ code: "", name: "", method: 1, attr: 1, subjectAccountId: "", months: "", residualRate: "" });
            setToast("类别已新增");
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "新增失败"));
        } finally {
            setBusy(false);
        }
    };

    const remove = async (id: string) => {
        if (!token || !accountSetId) {
            return;
        }
        try {
            await new FixedAssetsApi(apiConfig(token!)).accountsetsAccountSetIdFixedassetsCategoriesIdDelete({ accountSetId, id });
            setToast("类别已删除");
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "删除失败（类别下有资产时不可删）"));
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
                    <span className="cursor-pointer text-[13px] text-[#3385ff] hover:underline">编辑</span>
                    <span className="cursor-pointer text-[13px] text-[#3385ff] hover:underline" onClick={() => void remove(String(row.id))}>
                        删除
                    </span>
                </span>
            ),
        },
    ];
    return (
        <Page>
            <SubTabs tabs={["资产类别设置"]} />
            <ToolRow
                left={<Btn onClick={() => setShowAdd(true)}>新增</Btn>}
                right={
                    <>
                        <span className="inline-flex h-8 w-[220px] items-center justify-between rounded-[2px] border border-[#d9d9d9] px-2.5 text-sm text-[#999]">
                            输入编码或名称 🔍
                        </span>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                <Table
                    cols={cols}
                    rows={data.categories.map((c) => ({
                        id: c.id ?? "",
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
            <Pager total={data.categories.length} count={data.categories.length} />
            {/* 新增类别弹窗 */}
            {showAdd && (
                <Dialog
                    title="新增资产类别"
                    onClose={() => setShowAdd(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn variant="primary" onClick={() => void create()}>
                                保存
                            </Btn>
                            <Btn onClick={() => setShowAdd(false)}>取消</Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-[#333]">
                        {[
                            ["类别编码：", <input key="c" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} />],
                            ["类别名称：", <input key="n" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 outline-none focus:border-[#44b449]" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />],
                            ["折旧方法：", <select key="m" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] bg-white px-2 outline-none focus:border-[#44b449]" value={form.method} onChange={(e) => setForm({ ...form, method: Number(e.target.value) })}>
                                <option value={1}>平均年限法</option>
                            </select>],
                            ["资产属性：", <select key="a" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] bg-white px-2 outline-none focus:border-[#44b449]" value={form.attr} onChange={(e) => setForm({ ...form, attr: Number(e.target.value) })}>
                                <option value={1}>固定资产</option>
                                <option value={2}>无形资产</option>
                                <option value={3}>长期待摊费用</option>
                            </select>],
                            ["资产科目：", <select key="s" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] bg-white px-2 outline-none focus:border-[#44b449]" value={form.subjectAccountId} onChange={(e) => setForm({ ...form, subjectAccountId: e.target.value })}>
                                <option value="">（请选择）</option>
                                {categoryAccounts.map((acc) => (
                                    <option key={acc.id} value={acc.id}>
                                        {`${acc.code ?? ""} ${acc.name ?? ""}`}
                                    </option>
                                ))}
                            </select>],
                            ["使用月数：", <input key="mo" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 text-right outline-none focus:border-[#44b449]" value={form.months} onChange={(e) => setForm({ ...form, months: e.target.value.replace(/[^\d]/g, "") })} />],
                            ["净残值率(%)：", <input key="r" className="h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 text-right outline-none focus:border-[#44b449]" value={form.residualRate} onChange={(e) => setForm({ ...form, residualRate: e.target.value.replace(/[^\d.]/g, "") })} />],
                        ].map(([label, control]) => (
                            <label key={String(label)} className="flex items-center gap-3">
                                <span className="w-[110px] text-right">{label}</span>
                                {control}
                            </label>
                        ))}
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 资产核对总账 ---------- */

export function LemonAssetCheck() {
    const data = useFixedAssetData();
    const th = "border-b border-[#e8e8e8] bg-[#f0f7f0] px-3 py-2 text-[13px] font-normal text-[#333]";
    const td = "border-b border-[#f0f0f0] px-3 py-2.5 text-[13px] text-[#333]";
    return (
        <Page>
            <SubTabs tabs={["资产核对总账"]} />
            <ToolRow left={<GreenChip text="2025年10月" />} right={<><Btn onClick={() => window.print()}>打印</Btn><Btn>导出</Btn><RefreshIcon /></>} />
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
                        {data.check.length === 0 && (
                            <tr><td colSpan={6} className="py-10 text-center text-[13px] text-[#999]">暂无数据</td></tr>
                        )}
                        {(() => {
                            const groups: Array<{ name: string; rows: FixedAssetCheckRowDto[] }> = [];
                            for (const r of data.check) {
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
                                            <ChevronDown className="h-3 w-3 text-[#999]" /> {g.name}
                                        </span>
                                    </td>
                                </tr>
                                {g.rows.map((r, i) => (
                                    <tr key={g.name + i}>
                                        <td className={r.isDiff ? `${td} text-[#3385ff]` : td}>{r.isDiff ? "差异" : r.item}</td>
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
