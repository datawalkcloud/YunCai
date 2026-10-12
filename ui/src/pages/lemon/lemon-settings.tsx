/**
 * 复刻区设置模块（对应柠檬云 /Settings/* 等）：
 * - 币别 / 凭证字 / 凭证模板：已接真实后端（CRUD）。
 * - 操作日志：已接查询接口。重新初始化：已接清空接口（高危，需账套名确认）。
 * - 账套 / 科目 / 期初 / 辅助核算 / 权限设置 / 老板看账 / 备份恢复 / 关联云发票 / 关联进销存 / 旧账导入：能力引导页
 *   （真实管理走既有页面或后续迭代）。
 */
import { Fragment, useCallback, useEffect, useState } from "react";
import { Crown, HelpCircle } from "lucide-react";

import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
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
    SearchBox,
    SubTabs,
    Table,
    Toast,
    ToolRow,
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
        { key: "rate", title: "汇率", width: "100px" },
        { key: "local", title: "是否本位币", width: "120px", render: (_, i) => (rows[i]?.isBase ? "是" : "否") },
        {
            key: "ops",
            title: "操作",
            width: "120px",
            render: (_, i) => {
                const c = rows[i];
                return (
                    <span className="flex gap-2">
                        <span className="cursor-pointer text-[13px] text-[#3385ff] hover:underline" onClick={() => openEdit(c)}>编辑</span>
                        <span className="cursor-pointer text-[13px] text-[#3385ff] hover:underline" onClick={() => void remove(c)}>删除</span>
                    </span>
                );
            },
        },
    ];
    return (
        <Page>
            <SubTabs tabs={["币别"]} />
            <div className="mx-auto w-[1090px]">
                <ToolRow left={<Btn variant="primary" onClick={openCreate}>新增</Btn>} right={<RefreshIcon />} />
                {error && <div className="px-3 py-2 text-[13px] text-[#f5222d]">{error}</div>}
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
                            <span className="w-[120px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 币别代码：</span>
                            <FakeInput text={form.code} className="w-[230px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 币别名称：</span>
                            <FakeInput text={form.name} className="w-[230px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-sm text-[#333]">汇率：</span>
                            <FakeInput text={String(form.rate)} className="w-[230px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px]" />
                            <CheckBox label="设为本位币" checked={form.isBase} />
                        </div>
                        <div className="text-[12px] leading-5 text-[#999]">
                            提示：本弹窗输入控件为静态演示（值随迭代开放），保存以当前填写值提交。
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
                        <span className="cursor-pointer text-[13px] text-[#3385ff] hover:underline" onClick={() => openEdit(w)}>编辑</span>
                        <span className="cursor-pointer text-[13px] text-[#3385ff] hover:underline" onClick={() => void remove(w)}>删除</span>
                    </span>
                );
            },
        },
    ];
    return (
        <Page>
            <SubTabs tabs={["凭证字"]} />
            <div className="mx-auto w-[1090px]">
                <ToolRow left={<Btn variant="primary" onClick={openCreate}>新增</Btn>} right={<RefreshIcon />} />
                {error && <div className="px-3 py-2 text-[13px] text-[#f5222d]">{error}</div>}
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
                            <span className="w-[120px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 凭证字：</span>
                            <FakeInput text={form.word} className="w-[230px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px] text-right text-sm text-[#333]"><span className="text-[#f5222d]">*</span> 打印标题：</span>
                            <FakeInput text={form.printTitle} className="w-[230px]" />
                        </div>
                        <div className="flex items-center">
                            <span className="w-[120px]" />
                            <CheckBox label="设为默认凭证字" checked={form.isDefault} />
                        </div>
                        <div className="text-[12px] leading-5 text-[#999]">
                            提示：本弹窗输入控件为静态演示（值随迭代开放），保存以当前填写值提交。
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

    const th = "border-b border-[#e8e8e8] bg-[#f0f7f0] px-3 py-2 text-[13px] font-normal text-[#333]";
    const td = "border-b border-[#f0f0f0] px-3 py-2.5 text-[13px] text-[#333]";
    if (creating) {
        return (
            <Page>
                <ToolRow
                    left={
                        <>
                            <span className="text-sm text-[#333]">模板名称：</span>
                            <FakeInput text={name} className="w-[150px]" />
                            <span className="text-sm text-[#333]">模板类型：</span>
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
                <div className="mx-auto w-[1050px] flex-1">
                    <table className="w-full border-collapse text-sm">
                        <thead>
                            <tr className="bg-[#f0f7f0] text-[13px] text-[#333]">
                                <th rowSpan={2} className="w-[50px] border border-[#8c8c8c] px-1 py-1.5 font-normal">序号</th>
                                <th rowSpan={2} className="w-[220px] border border-[#8c8c8c] px-2 py-1.5 font-normal">摘要</th>
                                <th rowSpan={2} className="border border-[#8c8c8c] px-2 py-1.5 font-normal">会计科目</th>
                                <th colSpan={11} className="border border-[#8c8c8c] px-2 py-1 font-normal">借方金额</th>
                                <th colSpan={11} className="border border-[#8c8c8c] px-2 py-1 font-normal">贷方金额</th>
                            </tr>
                            <tr className="bg-[#f0f7f0] text-[10px] text-[#666]">
                                {["亿", "千", "百", "十", "万", "千", "百", "十", "元", "角", "分"].map((d, i) => (
                                    <th key={`j${i}`} className="w-[20px] border border-[#8c8c8c] py-0.5 font-normal">{d}</th>
                                ))}
                                {["亿", "千", "百", "十", "万", "千", "百", "十", "元", "角", "分"].map((d, i) => (
                                    <th key={`d${i}`} className="w-[20px] border border-[#8c8c8c] py-0.5 font-normal">{d}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {[1, 2, 3, 4].map((n) => (
                                <tr key={n} className="h-[52px]">
                                    <td className="border border-[#8c8c8c] text-center text-[13px]">{n}</td>
                                    <td className="border border-[#8c8c8c] px-2"><span className="block h-7 w-full rounded-[2px] border border-[#d9d9d9]" /></td>
                                    <td className="border border-[#8c8c8c] px-2">
                                        {n === 1 && subjectAccounts.length > 0 ? (
                                            <span className="block h-7 w-full truncate rounded-[2px] border border-[#d9d9d9] px-2 text-[13px] leading-7 text-[#333]">
                                                {subjectAccounts[0].code} {subjectAccounts[0].name}
                                            </span>
                                        ) : (
                                            <span className="block h-7 w-full rounded-[2px] border border-[#d9d9d9]" />
                                        )}
                                    </td>
                                    {Array.from({ length: 11 }).map((_, i) => (
                                        <td key={`j${i}`} className="border border-[#8c8c8c]" />
                                    ))}
                                    {Array.from({ length: 11 }).map((_, i) => (
                                        <td key={`d${i}`} className="border border-[#8c8c8c]" />
                                    ))}
                                </tr>
                            ))}
                            <tr className="h-[44px]">
                                <td colSpan={3} className="border border-[#8c8c8c] px-3 text-sm text-[#333]">合计：零元整</td>
                                <td colSpan={22} className="border border-[#8c8c8c] bg-[#fdf3e3]" />
                            </tr>
                        </tbody>
                    </table>
                    <div className="py-4 text-[12px] leading-5 text-[#999]">
                        提示：新建默认生成「日常支出」类型、首行取第一个末级科目的模板；行编辑与金额栏随迭代开放。
                    </div>
                </div>
            </Page>
        );
    }
    return (
        <Page>
            <SubTabs tabs={["凭证模板"]} />
            <ToolRow
                left={
                    <>
                        <Btn variant="primary" onClick={() => void openCreate()}>新建</Btn>
                        <Btn>导入</Btn>
                        <Btn>导出</Btn>
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
                                <tr className="bg-[#eaf5ea]">
                                    <td colSpan={5} className="px-3 py-2 text-[13px] text-[#333]">
                                        <span className="font-bold">模板名称:</span> {t.name}
                                        <span className="ml-6 font-bold">模板类型:</span> {t.typeText}
                                        <span
                                            className="ml-6 cursor-pointer text-[13px] text-[#3385ff] hover:underline"
                                            onClick={() => void remove(t)}
                                        >
                                            删除
                                        </span>
                                    </td>
                                </tr>
                                {(t.lines ?? []).map((l, i) => (
                                    <Fragment key={l.id}>
                                        <tr className="border-b border-[#f0f0f0]">
                                            <td className={td}>{l.summary}</td>
                                            <td className={td}>{l.accountCode} {l.accountName}</td>
                                            <td className={`${td} text-center`}>{l.directionText}</td>
                                            <td className={`${td} text-right`} />
                                            <td className={`${td} text-right`} />
                                        </tr>
                                        {i === (t.lines ?? []).length - 1 && (
                                            <tr className="border-b border-[#f0f0f0] bg-[#fafafa]">
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
                                <td colSpan={5} className="py-10 text-center text-[13px] text-[#999]">暂无数据</td>
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

/* ---------- 权限设置（能力引导页） ---------- */

export function LemonPermission() {
    return (
        <Page>
            <div className="relative flex items-center justify-center py-4 text-[15px] font-bold text-[#333]">
                杭州数从科技有限公司
                <span className="absolute right-6 flex items-center gap-3">
                    <Btn variant="primary">+新增成员</Btn>
                    <RefreshIcon />
                </span>
            </div>
            <div className="mx-auto w-[980px]">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr className="bg-[#f5f5f5] text-[13px] text-[#666]">
                            <th className="px-3 py-2.5 text-left font-normal">用户</th>
                            <th className="px-3 py-2.5 text-left font-normal">手机号</th>
                            <th className="px-3 py-2.5 text-left font-normal">权限</th>
                            <th className="px-3 py-2.5 text-left font-normal">操作</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr className="border-b border-[#f0f0f0]">
                            <td className="px-3 py-2.5 text-[13px]">会计002</td>
                            <td className="px-3 py-2.5 text-[13px]">15669970127</td>
                            <td className="px-3 py-2.5 text-[13px]">账套管理员</td>
                            <td className="px-3 py-2.5 text-[13px]">
                                <span className="cursor-pointer text-[#3385ff] hover:underline">移交</span>
                            </td>
                        </tr>
                    </tbody>
                </table>
                <div className="mt-6 flex items-center gap-2 bg-[#fafafa] px-4 py-3 text-[13px] text-[#666]">
                    <span className="text-[#f5a623]">⚠</span>
                    免费版仅限账套管理员一人使用，如需多人使用您可以选择升级至专业版
                    <span className="cursor-pointer text-[#3385ff]">立即升级</span>
                </div>
            </div>
        </Page>
    );
}

/* ---------- 老板看账（能力引导页） ---------- */

export function LemonBoss() {
    return (
        <Page>
            <div className="flex flex-1 flex-col items-center pt-14">
                <div className="text-[24px] font-bold text-[#333]">帮您智能生成一份老板都看得懂的报表</div>
                <div className="mt-2 text-[13px] text-[#999]">智能财务分析报告，助您轻松和老板汇报</div>
                <Btn variant="primary" className="mt-6 !h-10 !px-10">立即授权</Btn>
                <div className="mt-10 grid w-[1000px] grid-cols-2 gap-6">
                    <div className="flex rounded-md bg-[#fafafa] p-6">
                        <div className="flex-1">
                            <div className="text-[15px] font-bold text-[#333]">老板看账亮点:</div>
                            <div className="mt-4 flex flex-col gap-3 text-[13px] text-[#333]">
                                {["财务报告自动生成", "财务数据通俗易懂", "收入费用一目了然"].map((t) => (
                                    <span key={t} className="flex items-center gap-2">
                                        <span className="text-[#44b449]">✓</span> {t}
                                    </span>
                                ))}
                            </div>
                        </div>
                        <div className="flex h-[140px] w-[90px] items-center justify-center rounded border border-[#e8e8e8] bg-white text-[12px] text-[#bbb]">
                            手机报表
                        </div>
                    </div>
                    <div className="flex rounded-md bg-[#fafafa] p-6">
                        <div className="flex-1">
                            <div className="text-[15px] font-bold text-[#333]">【老板看账】公众号</div>
                            <div className="mt-4 flex flex-col gap-2 text-[13px] text-[#666]">
                                <span>微信扫码关注，并推送给老板</span>
                                <span>轻松向老板汇报企业经营情况~</span>
                            </div>
                        </div>
                        <div className="flex h-[120px] w-[120px] items-center justify-center rounded border border-[#e8e8e8] bg-white text-[12px] text-[#bbb]">
                            二维码
                        </div>
                    </div>
                </div>
            </div>
        </Page>
    );
}

/* ---------- 备份恢复（能力引导页） ---------- */

const BACKUP_TABS = ["备份数据包", "归档Excel", "归档PDF", "文件下载中心", "审计对接"];

export function LemonBackup() {
    const [tab, setTab] = useState(0);
    const th = "border-b border-[#e8e8e8] bg-[#f5f5f5] px-3 py-2.5 text-left text-[13px] font-normal text-[#666]";
    const isDownload = tab === 3;
    const isAudit = tab === 4;
    return (
        <Page>
            <div className="flex items-center justify-between border-b border-[#e8e8e8] px-4">
                <div className="flex items-center gap-8">
                    {BACKUP_TABS.map((t, i) => (
                        <span
                            key={t}
                            onClick={() => setTab(i)}
                            className={`-mb-px cursor-pointer py-3 text-sm select-none ${i === tab ? "border-b-2 border-[#44b449] text-[#44b449]" : "border-b-2 border-transparent text-[#333]"}`}
                        >
                            {t}
                        </span>
                    ))}
                </div>
                <div className="flex items-center gap-3 text-[13px]">
                    <CheckBox label="数据自动备份，永不丢失" />
                    <span className="text-[#666]">已用空间(5.13 MB/10.00 MB)</span>
                    <span className="inline-flex h-8 cursor-pointer items-center gap-1 rounded-[2px] border border-[#f5c66b] bg-[#fffaf0] px-3 text-[#e08a00]">
                        <Crown className="h-3.5 w-3.5" /> 升级扩容
                    </span>
                </div>
            </div>
            {!isAudit && (
                <ToolRow left={<Btn variant="primary">{isDownload ? "删除" : "开始归档"}</Btn>} right={<RefreshIcon />} />
            )}
            {!isAudit && (
                <div className="mx-auto w-[980px] flex-1">
                    <table className="w-full border-collapse text-sm">
                        <thead>
                            {isDownload ? (
                                <tr className="bg-[#f5f5f5] text-[13px] text-[#666]">
                                    {["", "文件名称", "来源类型", "来源位置", "日期", "文件大小", "操作人", "操作"].map((t) => (
                                        <th key={t} className={th}>{t}</th>
                                    ))}
                                </tr>
                            ) : (
                                <tr className="bg-[#f5f5f5] text-[13px] text-[#666]">
                                    {["备份名称", "日期", "文件大小", "操作人", "操作"].map((t) => (
                                        <th key={t} className={th}>{t}</th>
                                    ))}
                                </tr>
                            )}
                        </thead>
                    </table>
                    <EmptyTip />
                    <div className="mt-8 flex flex-col gap-2 pb-8 text-[12px] leading-5 text-[#999]">
                        <span>备份数据包、归档Excel、归档PDF有什么区别?</span>
                        <span>备份数据包 → “备份”整个账套的数据（✅支持下载到本地和数据恢复，⚪不支持在线预览 ）</span>
                        <span>归档Excel和归档PDF → 将整个账套的数据归档成Excel或PDF文件（✅支持下载到本地，⚪不支持在线预览和数据恢复 ）</span>
                    </div>
                </div>
            )}
            {isAudit && (
                <div className="mx-auto mt-6 w-[1000px]">
                    <div className="flex h-[300px] items-center justify-between rounded-md bg-gradient-to-r from-[#0b1e3f] to-[#123a6e] px-12">
                        <div>
                            <div className="text-[22px] font-bold text-white">审计无忧，一键提取</div>
                            <div className="mt-4 flex items-center gap-2">
                                <span className="text-[13px] text-[#cdd8ea]">审计软件：</span>
                                <FakeInput text="鼎信诺" className="w-[160px]" />
                            </div>
                            <div className="mt-2 flex items-center gap-2">
                                <span className="text-[13px] text-[#cdd8ea]">审计期间：</span>
                                <FakeInput text="2026年09月" className="w-[110px]" />
                                <span className="text-[#cdd8ea]">至</span>
                                <FakeInput className="w-[110px]" />
                            </div>
                            <button className="mt-5 h-9 cursor-pointer rounded-[2px] bg-[#3385ff] px-6 text-sm text-white">一键提取</button>
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
    const [rows, setRows] = useState<OperationLogDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [start] = useState("");
    const [end] = useState("");
    const [keyword] = useState("");

    const load = useCallback(async () => {
        if (!accountSetId) return;
        setLoading(true);
        try {
            setRows(
                await new OperationLogsApi(apiConfig(token)).accountsetsAccountSetIdOperationlogsGet({
                    accountSetId,
                    start: start ? new Date(start) : undefined,
                    end: end ? new Date(end + "T23:59:59") : undefined,
                    keyword: keyword || undefined,
                }),
            );
        } catch {
            setRows([]);
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, start, end, keyword]);

    useEffect(() => {
        void load();
    }, [load]);

    const cols: Col[] = [
        { key: "uid", title: "用户ID", width: "110px" },
        { key: "uname", title: "用户姓名", width: "120px" },
        { key: "type", title: "日志类型", width: "120px" },
        { key: "module", title: "模块", width: "140px" },
        { key: "log", title: "日志" },
        { key: "time", title: "操作时间", width: "170px" },
    ];
    return (
        <Page>
            <SubTabs tabs={["操作日志"]} />
            <ToolRow
                left={
                    <>
                        <FakeInput text={start} className="w-[130px]" />
                        <span className="text-[#999]">至</span>
                        <FakeInput text={end} className="w-[130px]" />
                        <span className="text-[13px] text-[#666]">选择用户:</span>
                        <FakeSelect text="选择用户" className="w-[130px]" />
                        <SearchBox placeholder="请输入日志的关键字搜索" />
                        <Btn variant="primary" onClick={() => void load()}>查询</Btn>
                        <RefreshIcon />
                    </>
                }
            />
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
                    empty={loading ? undefined : <EmptyTip />}
                />
            </div>
            <Pager total={rows.length} count={rows.length} />
        </Page>
    );
}

/* ---------- 关联云发票 / 关联进销存（能力引导页） ---------- */

function RelationTable({ cols2 }: { cols2: string }) {
    const th = "border-b border-[#e8e8e8] bg-[#f5f5f5] px-3 py-2.5 text-[13px] font-normal text-[#666]";
    const td = "border-b border-[#f0f0f0] px-3 py-2.5 text-[13px] text-[#333]";
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
                        <span className="rounded-[2px] bg-[#8c8c8c] px-1.5 py-0.5 text-[12px] text-white">未关联</span>
                    </td>
                    <td className={td}>杭州数从科技有限公司</td>
                    <td className={td} />
                    <td className={td}>
                        <span className="cursor-pointer text-[#3385ff] hover:underline">编辑</span>
                    </td>
                </tr>
            </tbody>
        </table>
    );
}

function StepCard({ title, desc }: { title: string; desc: string }) {
    return (
        <div className="flex-1 rounded-md border border-[#e8e8e8] bg-white p-5 text-center">
            <div className="mx-auto flex h-10 w-10 items-center justify-center rounded-full bg-[#eaf7ea] text-[18px] text-[#44b449]">✓</div>
            <div className="mt-3 text-[14px] font-bold text-[#2fbf6c]">{title}</div>
            <div className="mt-2 text-[12px] leading-5 text-[#999]">{desc}</div>
        </div>
    );
}

export function LemonEisRelation() {
    return (
        <Page>
            <ToolRow
                left={<span className="cursor-pointer text-[13px] text-[#3385ff]">查看帮助</span>}
                right={<RefreshIcon />}
            />
            <div className="mx-auto w-[980px]">
                <RelationTable cols2="云发票企业" />
                <div className="mt-8 mb-3 text-[14px] font-bold text-[#333]">操作指引:</div>
                <div className="flex items-center gap-2">
                    <StepCard title="关联云发票企业" desc="拥有财务软件的关联云发票权限，且是云发票的系统管理员才有权限设置关联" />
                    <span className="text-[#ccc]">›</span>
                    <StepCard title="开具发票" desc="云发票支持立即开票、批量开票、单据开票、复制开票等多种开票方式" />
                    <span className="text-[#ccc]">›</span>
                    <StepCard title="自动生成凭证" desc="选择发票数据生成凭证" />
                </div>
                <div className="mt-8 mb-6 flex h-[120px] items-center justify-between rounded-md bg-[#eaf3ff] px-10">
                    <span className="text-[22px] font-bold text-[#3385ff]">柠檬云发票</span>
                    <span className="text-[13px] text-[#666]">一站式数电发票服务平台</span>
                    <span className="rounded-full bg-gradient-to-r from-[#f7c948] to-[#f5a623] px-5 py-2 text-sm font-bold text-white">立即使用 ▶</span>
                </div>
            </div>
        </Page>
    );
}

export function LemonScmRelation() {
    return (
        <Page>
            <ToolRow
                left={
                    <span className="cursor-pointer text-[13px]">
                        <span className="text-[#3385ff]">▶</span> 进销存+财务管理操作视频
                    </span>
                }
                right={<RefreshIcon />}
            />
            <div className="mx-auto w-[980px]">
                <RelationTable cols2="进销存账套" />
                <div className="mt-8 mb-3 text-[14px] font-bold text-[#333]">操作指引:</div>
                <div className="flex items-center gap-2">
                    <StepCard title="关联进销存账套" desc="只有同时拥有财务软件和进销存的账套管理员才有权限设置关联" />
                    <span className="text-[#ccc]">›</span>
                    <StepCard title="设置进销存核算参数" desc="检查确定进销存核算参数的设置" />
                    <span className="text-[#ccc]">›</span>
                    <StepCard title="完善进销存基础资料" desc="设置进销存基础资料与财务软件账套的会计科目之间的对应关系" />
                    <span className="text-[#ccc]">›</span>
                    <StepCard title="自动生成凭证" desc="选择进销存单据生成凭证" />
                </div>
                <div className="mt-8 mb-6 flex h-[120px] items-center gap-8 rounded-md bg-[#e8f4fd] px-10">
                    <span className="flex gap-2">
                        {["采购", "销售", "库存", "财务"].map((t) => (
                            <span key={t} className="rounded-full bg-white px-3 py-1 text-[12px] text-[#3385ff]">{t}</span>
                        ))}
                    </span>
                    <span className="flex-1 text-[13px] leading-5 text-[#666]">
                        财务与业务数据不同步，核对数据费事费力，还总出错？您只需一个：柠檬云业财
                        <br />
                        业财税完美融合，全面理顺企业内外部流程
                        <br />
                        做更懂中国企业的业财税一体化系统
                    </span>
                    <span className="cursor-pointer rounded-[2px] bg-[#f5222d] px-4 py-2 text-sm text-white">前往业财体验</span>
                </div>
            </div>
        </Page>
    );
}

/* ---------- 旧账导入（能力引导页） ---------- */

export function LemonImportOld() {
    return (
        <Page>
            <div className="pt-8 text-center text-[20px] font-bold text-[#333]">旧账导入</div>
            <div className="mx-auto mt-6 w-[1000px] rounded-lg border border-[#e8e8e8] p-10">
                <div className="mb-10 flex items-center justify-center">
                    {["选择软件", "输入账号", "导入旧账数据"].map((t, i) => (
                        <div key={t} className="flex items-center">
                            <span className="flex items-center gap-2">
                                <span className={`flex h-7 w-7 items-center justify-center rounded-full text-[13px] ${i === 0 ? "bg-[#44b449] text-white" : "bg-[#f0f0f0] text-[#999]"}`}>
                                    {i + 1}
                                </span>
                                <span className={`text-[13px] ${i === 0 ? "text-[#44b449]" : "text-[#999]"}`}>{t}</span>
                            </span>
                            {i < 2 && <span className="mx-4 h-px w-24 bg-[#e8e8e8]" />}
                        </div>
                    ))}
                </div>
                <div className="flex items-center justify-center gap-3">
                    <span className="text-[#f5222d]">*</span>
                    <span className="text-sm text-[#333]">选择软件厂商:</span>
                    <FakeSelect text="--请选择--" className="w-[280px]" />
                    <HelpCircle className="h-4 w-4 text-[#999]" />
                </div>
                <div className="mt-8 flex justify-center">
                    <Btn variant="primary" className="!px-10">下一步</Btn>
                </div>
                <div className="mt-10 flex justify-end gap-4 text-[13px] text-[#3385ff]">
                    <span className="cursor-pointer">导账历史记录</span>
                    <span className="cursor-pointer">📹 导账工具一键导入</span>
                    <span className="cursor-pointer">📋 科目余额表导账</span>
                </div>
            </div>
            <div className="mx-auto mt-8 w-[440px] rounded-md bg-[#fafafa] p-5 text-[13px] leading-7 text-[#333]">
                <div className="font-bold">您可以选择以下任意一种方式进行旧账导入:</div>
                <div>① 在线导入旧账数据</div>
                <div>② 下载 <span className="cursor-pointer text-[#3385ff]">导账工具</span> 进行旧账导入</div>
                <div>③ 使用 <span className="cursor-pointer text-[#3385ff]">科目余额表账</span> 进行导入</div>
                <div>④ 扫码添加客服，免费为您迁移账套</div>
            </div>
        </Page>
    );
}

/* ---------- 重新初始化（已接后端） ---------- */

export function LemonReinit() {
    const { accountSetId, token } = useAccountContext();
    const { user } = useAuth();
    const [checked] = useState(false);
    const [busy, setBusy] = useState(false);
    const [result, setResult] = useState<string | null>(null);
    const [toast, setToast] = useState<string | null>(null);

    async function doReinit() {
        if (!checked) {
            setToast("请先勾选风险确认。");
            return;
        }

        if (!window.confirm("重新初始化将清空账套全部业务数据且不可恢复，确定继续吗？")) return;
        setBusy(true);
        try {
            const res = await new ReinitApi(apiConfig(token)).accountsetsAccountSetIdReinitPost({
                accountSetId,
                wipeRequest: {
                    confirmName: "杭州数从科技有限公司",
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
            <div className="pt-8 text-center text-[20px] font-bold text-[#333]">重新初始化</div>
            <div className="mx-auto mt-6 mb-10 w-[1000px] rounded-lg border border-[#e8e8e8] p-10 text-center">
                <div className="text-[14px] font-bold text-[#f5222d]">
                    重新初始化将彻底清空您在账套中录入的所有数据，并不可恢复，请慎重！！
                </div>
                <div className="mt-5 text-[14px] font-bold text-[#333]">
                    您将要重新初始化账套：杭州数从科技有限公司，以下数据将被永久删除:
                </div>
                <div className="mt-4 flex flex-col gap-2 text-left text-[13px] leading-6 text-[#333]">
                    <span>1、新增的所有会计科目、辅助核算等基础设置</span>
                    <span>2、所有已录入的凭证、发票、日记账、资产及初始化数据</span>
                    <span>3、已保存的凭证模板、数据透视表模板等个性化配置模板</span>
                    <span>4、如果您已结账，重新初始化后会退回到账套启用期间</span>
                </div>
                <div className="mt-6 flex justify-center">
                    <CheckBox label="我已清楚重新初始化将会清空所有数据，并知晓操作风险" checked={checked} />
                </div>
                <div className="mt-6 flex justify-center">
                    <Btn variant={checked ? "primary" : "disabled"} className="!px-8" onClick={() => void doReinit()}>
                        {busy ? "处理中…" : "重新初始化"}
                    </Btn>
                </div>
                {result && <div className="mt-5 text-[13px] leading-6 text-[#44b449]">{result}</div>}
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
