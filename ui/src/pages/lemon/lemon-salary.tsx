/**
 * 复刻区工资模块（对应柠檬云 /Salary/*）：
 * 工资管理、员工信息、工资统计表、五险一金设置。
 * 视觉遵循全站云财蓝：主色 #2f6bff、圆角 rounded-lg/2xl、白卡 + slate 灰阶文字。
 */
import { useCallback, useEffect, useState, type ReactNode } from "react";
import {
    BarChartOutlined,
    PayCircleOutlined,
    QuestionCircleOutlined,
    SafetyCertificateOutlined,
    TeamOutlined,
    UserAddOutlined,
    WalletOutlined,
} from "@ant-design/icons";

import { ICON_STYLE } from "@/components/page/styles";
import { Tile } from "@/components/page/tile";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import {
    makePeriod,
    shiftPeriod,
    useDefaultAccountingPeriod,
    type AccountingPeriod,
} from "@/accountSets/useCurrentAccountSet";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";

import {
    Btn,
    CheckBox,
    DateInput,
    Dialog,
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
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import {
    SalariesApi,
    AccountsApi,
    type AccrueViewDto,
    type AccountDto,
    type EmployeeDto,
    type InsuranceItemDto,
    type SalaryBillDto,
    type SalarySummaryRowDto,
} from "@/apis-gen";

/* ---------- 公共小工具 ---------- */

/** 金额格式化：千分位两位小数，空值显示 0.00。 */
function nf(v: number | null | undefined): string {
    return (v ?? 0).toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

const pad = (n: number) => String(n).padStart(2, "0");

/** 期间对象 → 「2026年10月」。 */
function periodLabel(p: AccountingPeriod): string {
    return `${p.year}年${pad(p.month)}月`;
}

/** yyyyMM 数字 → 期间对象。 */
function periodFromNumber(n: number): AccountingPeriod {
    return makePeriod(Math.floor(n / 100), n % 100);
}

/** 日期（Date 或字符串）→ yyyy-MM-dd，供输入框回填。 */
function toDateInput(d: Date | string | null | undefined): string {
    if (!d) {
        return "";
    }
    return new Date(d).toISOString().substring(0, 10);
}

/** 员工展示文本反推枚举（后端 EmployeeDto 仅回显文本，编辑需还原为数字）。 */
const genderOf = (t?: string | null) => (t === "女" ? 2 : 1);
const idTypeOf = (t?: string | null) => (t === "护照" ? 2 : t === "其他" ? 3 : 1);
const statusOf = (t?: string | null) => (t === "离职" ? 2 : 1);

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

/** 编辑/新增对话框里的「标签 + 控件」行。 */
function Field({ label, required, children }: { label: string; required?: boolean; children: ReactNode }) {
    return (
        <label className="flex items-center gap-3">
            <span className="w-[110px] shrink-0 text-right text-[13px] text-slate-700">
                {required && <span className="text-[#ef4444]">*</span>}
                {label}：
            </span>
            {children}
        </label>
    );
}

/** 员工表单可编辑字段（后端仅回显以下字段，其余隐藏字段暂不支持在此维护）。 */
interface EmployeeFormState {
    code: string;
    name: string;
    gender: number;
    idType: number;
    status: number;
    dept: string;
    phone: string;
    email: string;
    idNo: string;
    subjectAccountId: string;
    hireDate: string;
    remark: string;
}

function employeeToForm(e: EmployeeDto): EmployeeFormState {
    return {
        code: e.code ?? "",
        name: e.name ?? "",
        gender: genderOf(e.genderText),
        idType: idTypeOf(e.idTypeText),
        status: statusOf(e.statusText),
        dept: e.dept ?? "",
        phone: e.phone ?? "",
        email: e.email ?? "",
        idNo: e.idNo ?? "",
        subjectAccountId: e.subjectAccountId ?? "",
        hireDate: toDateInput(e.hireDate),
        remark: e.remark ?? "",
    };
}

function toSaveEmployee(f: EmployeeFormState) {
    return {
        code: f.code.trim(),
        name: f.name.trim(),
        gender: f.gender,
        idType: f.idType,
        status: f.status,
        idNo: f.idNo.trim() || null,
        dept: f.dept.trim() || null,
        phone: f.phone.trim() || null,
        email: f.email.trim() || null,
        hireDate: f.hireDate ? new Date(f.hireDate) : null,
        subjectAccountId: f.subjectAccountId || null,
        remark: f.remark.trim() || null,
    };
}

/** 员工编辑对话框：Dialog + TextInput/Select，保存走 PUT 端点。 */
function EmployeeEditDialog({
    employee,
    accounts,
    onClose,
    onSaved,
}: {
    employee: EmployeeDto;
    accounts: AccountDto[];
    onClose: () => void;
    onSaved: () => void;
}) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [form, setForm] = useState<EmployeeFormState>(() => employeeToForm(employee));
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const set = (patch: Partial<EmployeeFormState>) => setForm((prev) => ({ ...prev, ...patch }));

    const save = async () => {
        if (!token || !accountSetId || !employee.id || busy) {
            return;
        }
        if (!form.code.trim() || !form.name.trim()) {
            setToast("编号与姓名必填");
            return;
        }
        setBusy(true);
        try {
            await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryEmployeesIdPut({
                accountSetId,
                id: employee.id,
                saveEmployeeRequest: toSaveEmployee(form),
            });
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
            title="编辑员工"
            width={620}
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
            <div className="grid grid-cols-2 gap-x-6 gap-y-3">
                <Field label="编号" required>
                    <TextInput className="w-full" value={form.code} onChange={(v) => set({ code: v })} />
                </Field>
                <Field label="姓名" required>
                    <TextInput className="w-full" value={form.name} onChange={(v) => set({ name: v })} />
                </Field>
                <Field label="性别" required>
                    <Select
                        className="w-full"
                        value={String(form.gender)}
                        onChange={(v) => set({ gender: Number(v) })}
                        options={[
                            { value: "1", label: "男" },
                            { value: "2", label: "女" },
                        ]}
                    />
                </Field>
                <Field label="证件类型" required>
                    <Select
                        className="w-full"
                        value={String(form.idType)}
                        onChange={(v) => set({ idType: Number(v) })}
                        options={[
                            { value: "1", label: "居民身份证" },
                            { value: "2", label: "护照" },
                            { value: "3", label: "其他" },
                        ]}
                    />
                </Field>
                <Field label="证件号码">
                    <TextInput className="w-full" value={form.idNo} onChange={(v) => set({ idNo: v })} />
                </Field>
                <Field label="部门">
                    <TextInput className="w-full" value={form.dept} onChange={(v) => set({ dept: v })} />
                </Field>
                <Field label="手机">
                    <TextInput className="w-full" value={form.phone} onChange={(v) => set({ phone: v })} />
                </Field>
                <Field label="邮箱">
                    <TextInput className="w-full" value={form.email} onChange={(v) => set({ email: v })} />
                </Field>
                <Field label="入职日期">
                    <DateInput className="w-full" value={form.hireDate} onChange={(v) => set({ hireDate: v })} />
                </Field>
                <Field label="员工状态" required>
                    <Select
                        className="w-full"
                        value={String(form.status)}
                        onChange={(v) => set({ status: Number(v) })}
                        options={[
                            { value: "1", label: "在职" },
                            { value: "2", label: "离职" },
                        ]}
                    />
                </Field>
                <Field label="计提工资科目">
                    <Select
                        className="w-full"
                        value={form.subjectAccountId}
                        onChange={(v) => set({ subjectAccountId: v })}
                        options={[
                            { value: "", label: "（请选择）" },
                            ...accounts.map((a) => ({ value: a.id ?? "", label: `${a.code ?? ""} ${a.name ?? ""}` })),
                        ]}
                    />
                </Field>
                <Field label="备注">
                    <TextInput className="w-full" value={form.remark} onChange={(v) => set({ remark: v })} />
                </Field>
            </div>
            <div className="mt-4 rounded-lg border border-[#fde68a] bg-[#fffbeb] px-3 py-2 text-[12px] leading-5 text-[#92640b]">
                出生日期、银行信息与岗位/职称暂不支持在此编辑（后端员工列表未返回这些字段）。
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Dialog>
    );
}

/** 删除确认对话框。 */
function ConfirmDialog({
    title,
    text,
    confirmText = "删除",
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

/* ---------- 工资数据 Hook ---------- */

/** 员工与五险一金（与期间无关）。 */
function useEmployeeAndInsurance() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [employees, setEmployees] = useState<EmployeeDto[]>([]);
    const [insurance, setInsurance] = useState<InsuranceItemDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setLoading(true);
        const api = new SalariesApi(apiConfig(token));
        try {
            const [emps, ins] = await Promise.all([
                api.accountsetsAccountSetIdSalaryEmployeesGet({ accountSetId }),
                api.accountsetsAccountSetIdSalaryInsuranceGet({ accountSetId }),
            ]);
            setEmployees(emps ?? []);
            setInsurance(ins ?? []);
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载工资数据失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void reload();
    }, [reload]);

    return { employees, insurance, loading, error, reload };
}

/** 加载叶子科目列表（编辑对话框科目下拉用）。 */
function useLeafAccounts() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
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

/* ---------- 工资管理 ---------- */

/** 计提工资/发放工资视图（已接后端）：双卡显示当期应发/实发合计，一键生成计提/发放凭证。 */
function SalaryAccrueView() {
    const { token, user } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const defaultPeriod = useDefaultAccountingPeriod();
    const [override, setOverride] = useState<AccountingPeriod | null>(null);
    const period = override ?? defaultPeriod;
    const [view, setView] = useState<AccrueViewDto | null>(null);
    const [toast, setToast] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);
    const [busy, setBusy] = useState(false);

    const reload = useCallback(async () => {
        if (!token || !accountSetId) {
            return;
        }
        setLoading(true);
        try {
            setView(
                await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryAccrueViewGet({
                    accountSetId,
                    period: period.period,
                }),
            );
            setToast(null);
        } catch (e) {
            setView(null);
            setToast(await apiErrorMessage(e, "加载计提视图失败"));
        } finally {
            setLoading(false);
        }
    }, [token, accountSetId, period.period]);
    useEffect(() => {
        void reload();
    }, [reload]);

    const generate = async (kind: "accrue" | "pay") => {
        if (!token || !accountSetId || busy) {
            return;
        }
        setBusy(true);
        try {
            const api = new SalariesApi(apiConfig(token));
            const request = {
                accountSetId,
                generateSalaryVoucherRequest: {
                    period: period.period,
                    preparer: user?.displayName || user?.username || "",
                },
            };
            const result =
                kind === "accrue"
                    ? await api.accountsetsAccountSetIdSalaryGenerateAccruePost(request)
                    : await api.accountsetsAccountSetIdSalaryGeneratePayPost(request);
            setToast(`已生成 ${result.count ?? 0} 张凭证`);
            await reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "生成凭证失败"));
        } finally {
            setBusy(false);
        }
    };

    const cards: Array<{
        title: string;
        amount: string;
        kind: "accrue" | "pay";
        tone: "blue" | "green";
        icon: typeof WalletOutlined;
    }> = [
        { title: "计提工资", amount: nf(view?.payable), kind: "accrue", tone: "blue", icon: WalletOutlined },
        { title: "发放工资", amount: nf(view?.net), kind: "pay", tone: "green", icon: PayCircleOutlined },
    ];
    return (
        <>
            <ToolRow
                left={<PeriodStepper text={periodLabel(period)} onPrev={() => setOverride(shiftPeriod(period, -1))} onNext={() => setOverride(shiftPeriod(period, 1))} />}
                right={
                    <span className="flex items-center gap-1 text-[13px] text-[#2f6bff]">
                        <WipBtn title="工资凭证科目配置尚未接入后端">⚙ 工资凭证设置</WipBtn>
                    </span>
                }
            />
            <StateBar loading={loading} error={toast && !view ? toast : null} />
            <div className="mx-auto mt-6 grid w-[720px] grid-cols-2 gap-8 pb-8">
                {cards.map((c) => (
                    <div key={c.kind} className="flex flex-col items-center gap-4">
                        <Tile className="w-full" label={c.title} value={c.amount} tone={c.tone} icon={c.icon} />
                        <Btn
                            variant={busy ? "disabled" : "primary"}
                            className="!px-6"
                            onClick={() => void generate(c.kind)}
                        >
                            生成凭证
                        </Btn>
                    </div>
                ))}
            </div>
            {toast && !loading && <Toast text={toast} onClose={() => setToast(null)} />}
        </>
    );
}

/** 新增员工表单字段默认值。 */
const emptyEmployee = {
    code: "",
    name: "",
    idType: 1,
    idNo: "",
    gender: 1,
    birthDate: "",
    phone: "",
    email: "",
    bankName: "",
    bankAccountNo: "",
    hireDate: "",
    dept: "",
    post: "",
    title: "",
    subjectAccountId: "",
    status: 1,
    remark: "",
};

/** 新增员工（整页表单，境内人员；已接后端 employeesPost）。 */
function EmployeeForm({ onBack, onSaved }: { onBack: () => void; onSaved: () => void }) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [form, setForm] = useState(emptyEmployee);
    const accounts = useLeafAccounts();
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    const set = (patch: Partial<typeof emptyEmployee>) => setForm((prev) => ({ ...prev, ...patch }));

    const save = async () => {
        if (!token || !accountSetId) {
            return;
        }
        if (!form.code.trim() || !form.name.trim()) {
            setToast("编号与姓名必填");
            return;
        }
        setBusy(true);
        try {
            await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryEmployeesPost({
                accountSetId,
                saveEmployeeRequest: {
                    code: form.code.trim(),
                    name: form.name.trim(),
                    gender: form.gender,
                    idType: form.idType,
                    idNo: form.idNo.trim() || null,
                    dept: form.dept.trim() || null,
                    phone: form.phone.trim() || null,
                    email: form.email.trim() || null,
                    birthDate: form.birthDate ? new Date(form.birthDate) : null,
                    hireDate: form.hireDate ? new Date(form.hireDate) : null,
                    bankName: form.bankName.trim() || null,
                    bankAccountNo: form.bankAccountNo.trim() || null,
                    post: form.post.trim() || null,
                    title: form.title.trim() || null,
                    subjectAccountId: form.subjectAccountId || null,
                    status: form.status,
                    remark: form.remark.trim() || null,
                },
            });
            setToast("员工已新增");
            onSaved();
            onBack();
        } catch (e) {
            setToast(await apiErrorMessage(e, "保存失败"));
        } finally {
            setBusy(false);
        }
    };

    const rows: Array<Array<[string, ReactNode] | null>> = [
        [
            ["*编号", <TextInput key="code" className="w-[220px]" value={form.code} onChange={(v) => set({ code: v })} />],
            ["*姓名", <TextInput key="name" className="w-[220px]" value={form.name} onChange={(v) => set({ name: v })} />],
            [
                "*证件类型",
                <Select
                    key="idType"
                    className="w-[220px]"
                    value={String(form.idType)}
                    onChange={(v) => set({ idType: Number(v) })}
                    options={[
                        { value: "1", label: "居民身份证" },
                        { value: "2", label: "护照" },
                        { value: "3", label: "其他" },
                    ]}
                />,
            ],
        ],
        [
            ["*证件号码", <TextInput key="idNo" className="w-[220px]" value={form.idNo} onChange={(v) => set({ idNo: v })} />],
            [
                "*性别",
                <Select
                    key="gender"
                    className="w-[220px]"
                    value={String(form.gender)}
                    onChange={(v) => set({ gender: Number(v) })}
                    options={[
                        { value: "1", label: "男" },
                        { value: "2", label: "女" },
                    ]}
                />,
            ],
            ["*出生日期", <DateInput key="birth" className="w-[220px]" value={form.birthDate} onChange={(v) => set({ birthDate: v })} />],
        ],
        [
            ["手机", <TextInput key="phone" className="w-[220px]" value={form.phone} onChange={(v) => set({ phone: v })} />],
            ["邮箱", <TextInput key="email" className="w-[220px]" value={form.email} onChange={(v) => set({ email: v })} />],
            null,
        ],
        [
            ["银行名称", <TextInput key="bank" className="w-[220px]" value={form.bankName} onChange={(v) => set({ bankName: v })} />],
            ["银行账号", <TextInput key="acct" className="w-[220px]" value={form.bankAccountNo} onChange={(v) => set({ bankAccountNo: v })} />],
            ["*入职日期", <DateInput key="hire" className="w-[220px]" value={form.hireDate} onChange={(v) => set({ hireDate: v })} />],
        ],
        [
            ["*部门", <TextInput key="dept" className="w-[220px]" value={form.dept} onChange={(v) => set({ dept: v })} />],
            ["岗位", <TextInput key="post" className="w-[220px]" value={form.post} onChange={(v) => set({ post: v })} />],
            null,
        ],
        [
            ["职称", <TextInput key="title" className="w-[220px]" value={form.title} onChange={(v) => set({ title: v })} />],
            [
                "*计提工资科目",
                <Select
                    key="subject"
                    className="w-[220px]"
                    value={form.subjectAccountId}
                    onChange={(v) => set({ subjectAccountId: v })}
                    options={[
                        { value: "", label: "（请选择）" },
                        ...accounts.map((a) => ({ value: a.id ?? "", label: `${a.code ?? ""} ${a.name ?? ""}` })),
                    ]}
                />,
            ],
            ["备注", <TextInput key="remark" className="w-[220px]" value={form.remark} onChange={(v) => set({ remark: v })} />],
        ],
        [
            [
                "*员工状态",
                <Select
                    key="status"
                    className="w-[220px]"
                    value={String(form.status)}
                    onChange={(v) => set({ status: Number(v) })}
                    options={[
                        { value: "1", label: "在职" },
                        { value: "2", label: "离职" },
                    ]}
                />,
            ],
            null,
            null,
        ],
    ];

    return (
        <Page title="新增员工" subtitle="登记境内员工基本信息与扣除款项" icon={UserAddOutlined}>
            <div className="mx-auto mt-3 w-[1000px]">
                <div className="flex justify-end">
                    <span className="flex text-sm">
                        <span className="cursor-pointer rounded-l-lg bg-[#2f6bff] px-4 py-1.5 text-white">境内人员</span>
                        <span className="cursor-pointer rounded-r-lg border border-slate-200 px-4 py-1.5 text-slate-600">境外人员</span>
                    </span>
                </div>
                <div className="mt-4 text-[15px] font-bold text-slate-800">基本信息</div>
                <div className="mt-3 flex flex-col gap-4">
                    {rows.map((row, i) => (
                        <div key={i} className="grid grid-cols-3 gap-8">
                            {row.map((cell, j) =>
                                cell ? (
                                    <div key={j} className="flex items-center">
                                        <span className="w-[150px] text-right text-sm text-slate-700">
                                            {String(cell[0]).startsWith("*") && <span className="text-[#ef4444]">*</span>}
                                            {String(cell[0]).replace("*", "")}：
                                        </span>
                                        {cell[1]}
                                    </div>
                                ) : (
                                    <span key={j} />
                                ),
                            )}
                        </div>
                    ))}
                </div>
                <div className="mt-8 text-[15px] font-bold text-slate-800">扣除款项</div>
                <div className="mt-3 flex items-center gap-4 text-sm text-slate-700">
                    <span>*是否缴纳五险一金：</span>
                    <span className="flex items-center gap-1.5">
                        <span className="h-3.5 w-3.5 rounded-full border-[5px] border-[#2f6bff] bg-white" /> 是
                    </span>
                    <span className="ml-4 flex items-center gap-1.5">
                        <span className="h-3.5 w-3.5 rounded-full border border-slate-300 bg-white" /> 否
                    </span>
                </div>
                <div className="mt-3 rounded-lg border border-[#fde68a] bg-[#fffbeb] px-3 py-2 text-[13px] text-[#92640b]">
                    五险一金比例由「五险一金设置」统一维护，保存员工后按账套比例计提。
                </div>
                <div className="mt-8 flex justify-center gap-3 pb-8">
                    <Btn variant={busy ? "disabled" : "primary"} className="!px-7" onClick={() => void save()}>
                        保存
                    </Btn>
                    <Btn className="!px-7" onClick={onBack}>
                        取消
                    </Btn>
                </div>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

export function LemonSalaryManage() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const defaultPeriod = useDefaultAccountingPeriod();
    const [override, setOverride] = useState<AccountingPeriod | null>(null);
    const period = override ?? defaultPeriod;
    // 切换账套时回到该账套默认期间。
    useEffect(() => setOverride(null), [accountSetId]);
    const [bills, setBills] = useState<SalaryBillDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [tab, setTab] = useState(0);
    const [adding, setAdding] = useState(false);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setLoading(true);
        try {
            setBills(await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryBillsGet({ accountSetId, period: period.period }));
            setError(null);
        } catch (e) {
            setBills([]);
            setError(await apiErrorMessage(e, "加载工资列表失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, period.period]);
    useEffect(() => {
        void reload();
    }, [reload]);

    if (tab === 1)
        return (
            <Page title="工资管理" subtitle="计提工资与发放工资一键生成凭证" icon={WalletOutlined}>
                <SubTabs tabs={["工资列表", "计提工资、发放工资"]} active={1} onChange={setTab} />
                <SalaryAccrueView />
            </Page>
        );
    if (adding) return <EmployeeForm onBack={() => setAdding(false)} onSaved={() => void reload()} />;

    const cols: Col[] = [
        { key: "ck", title: <CheckBox label="" />, width: "36px" },
        { key: "no", title: "编号", width: "70px" },
        { key: "name", title: "姓名", width: "100px" },
        { key: "dept", title: "部门", width: "100px" },
        { key: "gross", title: "工资总额", align: "right" },
        { key: "deduct", title: "应扣工资", align: "right" },
        { key: "payable", title: "应发工资", align: "right" },
        { key: "personal", title: "代扣个人款项", align: "right" },
        { key: "threshold", title: "个税免征额", align: "right" },
        { key: "tax", title: "本月应缴个税", align: "right" },
        { key: "adjust", title: "实发工资调整", align: "right" },
        { key: "net", title: "实发工资", align: "right" },
        { key: "company", title: "公司承担款项", align: "right" },
        { key: "cost", title: "员工成本", align: "right" },
    ];
    const list = bills;
    const totals = {
        gross: list.reduce((s2, b) => s2 + (b.gross ?? 0), 0).toFixed(2),
        deductible: list.reduce((s2, b) => s2 + (b.deductible ?? 0), 0).toFixed(2),
        payable: list.reduce((s2, b) => s2 + (b.payable ?? 0), 0).toFixed(2),
        personal: list.reduce((s2, b) => s2 + (b.personalDeduction ?? 0), 0).toFixed(2),
        threshold: "5000",
        tax: list.reduce((s2, b) => s2 + (b.tax ?? 0), 0).toFixed(2),
        adjust: list.reduce((s2, b) => s2 + (b.adjust ?? 0), 0).toFixed(2),
        net: list.reduce((s2, b) => s2 + (b.net ?? 0), 0).toFixed(2),
        company: list.reduce((s2, b) => s2 + (b.companyPart ?? 0), 0).toFixed(2),
        cost: list.reduce((s2, b) => s2 + (b.cost ?? 0), 0).toFixed(2),
    };
    const rows: Row[] = [
        ...list.map((b, i) => ({
            no: b.employeeCode,
            name: b.employeeName,
            dept: b.dept,
            gross: (b.gross ?? 0).toFixed(2),
            deduct: (b.deductible ?? 0).toFixed(2),
            payable: (b.payable ?? 0).toFixed(2),
            personal: (b.personalDeduction ?? 0).toFixed(2),
            threshold: (b.taxThreshold ?? 0).toFixed(0),
            tax: (b.tax ?? 0).toFixed(2),
            adjust: (b.adjust ?? 0).toFixed(2),
            net: (b.net ?? 0).toFixed(2),
            company: (b.companyPart ?? 0).toFixed(2),
            cost: (b.cost ?? 0).toFixed(2),
            zebra: i % 2 === 1,
        })),
        { dept: "合计", gross: totals.gross, deduct: totals.deductible, payable: totals.payable, personal: totals.personal, threshold: totals.threshold, tax: totals.tax, adjust: totals.adjust, net: totals.net, company: totals.company, cost: totals.cost, __style: { background: "#fafbfd", fontWeight: 500 } },
    ];
    return (
        <Page title="工资管理" subtitle="当期工资表与计提发放" icon={WalletOutlined}>
            <SubTabs tabs={["工资列表", "计提工资、发放工资"]} active={tab} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <PeriodStepper
                            text={periodLabel(period)}
                            onPrev={() => setOverride(shiftPeriod(period, -1))}
                            onNext={() => setOverride(shiftPeriod(period, 1))}
                        />
                        <WipBtn title="自定义工资项目尚未接入后端">自定义工资项目</WipBtn>
                    </>
                }
                right={
                    <>
                        <CheckBox label="编辑、显示工资明细" />
                        <CheckBox label="多次发放工资" />
                        <WipBtn title="生成工资条尚未接入后端">生成工资条</WipBtn>
                        <span className="inline-flex items-center gap-1">
                            <WipBtn title="同步个税申报表尚未接入后端">同步个税申报表</WipBtn>
                            <QuestionCircleOutlined className="text-[14px]" style={ICON_STYLE.muted} />
                        </span>
                        <WipBtn dropdown title="导入工资条尚未接入后端">导入</WipBtn>
                        <WipBtn title="发送工资条尚未接入后端">发送工资条</WipBtn>
                        <WipBtn title="个税申报尚未接入后端">个税申报</WipBtn>
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <WipBtn title="档案附件尚未接入后端">附件</WipBtn>
                        <WipBtn dropdown title="更多操作尚未接入后端">更多操作</WipBtn>
                        <span className="cursor-pointer" onClick={() => void reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <StateBar loading={loading} error={error} />
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} />
            </div>
            <Pager total={list.length} count={list.length} />
        </Page>
    );
}

/* ---------- 员工信息 ---------- */

export function LemonSalaryEmployee() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const data = useEmployeeAndInsurance();
    const accounts = useLeafAccounts();
    const [adding, setAdding] = useState(false);
    const [editing, setEditing] = useState<EmployeeDto | null>(null);
    const [deleting, setDeleting] = useState<EmployeeDto | null>(null);
    const [busy, setBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    const remove = async () => {
        if (!token || !accountSetId || !deleting?.id || busy) {
            return;
        }
        setBusy(true);
        try {
            await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryEmployeesIdDelete({
                accountSetId,
                id: deleting.id,
            });
            setDeleting(null);
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "删除失败"));
            setDeleting(null);
        } finally {
            setBusy(false);
        }
    };

    if (adding) return <EmployeeForm onBack={() => setAdding(false)} onSaved={() => void data.reload()} />;
    const cols: Col[] = [
        { key: "ck", title: <CheckBox label="" />, width: "36px" },
        { key: "no", title: "编号", width: "70px" },
        { key: "name", title: "姓名", width: "100px" },
        { key: "dept", title: "部门", width: "110px" },
        { key: "phone", title: "手机", width: "140px" },
        { key: "email", title: "邮箱", width: "140px" },
        { key: "idNo", title: "证件号码", width: "180px" },
        { key: "subject", title: "计提工资科目", width: "240px" },
        { key: "status", title: "员工状态", width: "90px" },
        { key: "ops", title: "操作", width: "110px" },
    ];
    const rows: Row[] = data.employees.map((e) => ({
        no: e.code,
        name: e.name,
        dept: e.dept,
        phone: e.phone,
        email: e.email,
        idNo: e.idNo,
        subject: e.subjectText,
        status: e.statusText,
        ops: (
            <span className="flex gap-2">
                <span className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline" onClick={() => setEditing(e)}>
                    编辑
                </span>
                <span className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline" onClick={() => setDeleting(e)}>
                    删除
                </span>
            </span>
        ),
    }));
    return (
        <Page title="员工信息" subtitle="在职与离职员工名册维护" icon={TeamOutlined}>
            <SubTabs tabs={["员工信息"]} />
            <ToolRow
                left={
                    <Btn variant="primary" dropdown>
                        所有员工
                    </Btn>
                }
                right={
                    <>
                        <CheckBox label="显示所有信息" />
                        <Btn variant="primary" onClick={() => setAdding(true)}>增加员工</Btn>
                        <WipBtn title="同步税务人员信息尚未接入后端">同步税务人员信息</WipBtn>
                        <WipBtn dropdown title="导入员工尚未接入后端">导入</WipBtn>
                        <WipBtn title="导出员工尚未接入后端">导出</WipBtn>
                        <WipBtn dropdown title="批量操作尚未接入后端">批量操作</WipBtn>
                        <span className="cursor-pointer" onClick={() => void data.reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <StateBar loading={data.loading} error={data.error} />
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} />
            </div>
            <Pager total={data.employees.length} count={data.employees.length} />
            {editing && (
                <EmployeeEditDialog
                    employee={editing}
                    accounts={accounts}
                    onClose={() => setEditing(null)}
                    onSaved={() => void data.reload()}
                />
            )}
            {deleting && (
                <ConfirmDialog
                    title="删除员工"
                    text={`确定删除员工「${deleting.name ?? deleting.code ?? ""}」吗？此操作不可恢复。`}
                    busy={busy}
                    onClose={() => setDeleting(null)}
                    onConfirm={() => void remove()}
                />
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}

/* ---------- 工资统计表 ---------- */

export function LemonSalarySummary() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const defaultPeriod = useDefaultAccountingPeriod();
    const [override, setOverride] = useState<AccountingPeriod | null>(null);
    const period = override ?? defaultPeriod;
    useEffect(() => setOverride(null), [accountSetId]);

    // 区间：年初 → 当前所选月末。
    const startPeriod = period.year * 100 + 1;
    const endPeriod = period.period;
    const [rows2, setRows2] = useState<SalarySummaryRowDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            return;
        }
        setLoading(true);
        try {
            setRows2(
                await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalarySummaryGet({
                    accountSetId,
                    startPeriod,
                    endPeriod,
                }),
            );
            setError(null);
        } catch (e) {
            setRows2([]);
            setError(await apiErrorMessage(e, "加载工资统计表失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, startPeriod, endPeriod]);
    useEffect(() => {
        void reload();
    }, [reload]);

    const rangeText = `${periodFromNumber(startPeriod).year}年01月—${periodLabel(period)}`;
    const cols: Col[] = [
        { key: "dept", title: "部门", align: "right" },
        { key: "count", title: "人数", align: "right" },
        { key: "payable", title: "应发工资", align: "right" },
        { key: "personal", title: "代扣个人款项", align: "right" },
        { key: "company", title: "公司承担款项", align: "right" },
        { key: "net", title: "实发工资", align: "right" },
        { key: "cost", title: "员工成本", align: "right" },
    ];
    const rows: Row[] = [
        ...rows2.map((r) => ({
            dept: r.dept,
            count: String(r.employeeCount),
            payable: (r.payable ?? 0).toFixed(2),
            personal: (r.personalDeduction ?? 0).toFixed(2),
            company: (r.companyPart ?? 0).toFixed(2),
            net: (r.net ?? 0).toFixed(2),
            cost: (r.cost ?? 0).toFixed(2),
        })),
        {
            dept: "合计",
            count: String(rows2.reduce((s2, r) => s2 + (r.employeeCount ?? 0), 0)),
            payable: rows2.reduce((s2, r) => s2 + (r.payable ?? 0), 0).toFixed(2),
            personal: rows2.reduce((s2, r) => s2 + (r.personalDeduction ?? 0), 0).toFixed(2),
            company: rows2.reduce((s2, r) => s2 + (r.companyPart ?? 0), 0).toFixed(2),
            net: rows2.reduce((s2, r) => s2 + (r.net ?? 0), 0).toFixed(2),
            cost: rows2.reduce((s2, r) => s2 + (r.cost ?? 0), 0).toFixed(2),
            __style: { background: "#fafbfd", fontWeight: 500 },
        },
    ];
    return (
        <Page title="工资统计表" subtitle={`按部门汇总 ${rangeText} 的工资数据`} icon={BarChartOutlined}>
            <SubTabs tabs={["工资统计表"]} />
            <ToolRow
                left={
                    <PeriodStepper
                        text={rangeText}
                        onPrev={() => setOverride(shiftPeriod(period, -1))}
                        onNext={() => setOverride(shiftPeriod(period, 1))}
                    />
                }
                right={
                    <>
                        <CheckBox label="显示员工工资明细" />
                        <Btn onClick={() => window.print()}>打印</Btn>
                        <WipBtn title="导出工资统计表尚未接入后端">导出</WipBtn>
                        <span className="cursor-pointer" onClick={() => void reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <StateBar loading={loading} error={error} />
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} />
            </div>
            <Pager total={rows2.length} count={rows2.length} />
        </Page>
    );
}

/* ---------- 五险一金设置 ---------- */

/** 五险一金编辑对话框（新增/编辑）：走 POST / PUT 端点。 */
function InsuranceEditDialog({
    item,
    onClose,
    onSaved,
}: {
    item: InsuranceItemDto | null;
    onClose: () => void;
    onSaved: () => void;
}) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [name, setName] = useState(item?.name ?? "");
    const [companyRate, setCompanyRate] = useState(item?.companyRate != null ? String(item.companyRate) : "");
    const [personalRate, setPersonalRate] = useState(item?.personalRate != null ? String(item.personalRate) : "");
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    const save = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (!name.trim()) {
            setToast("缴纳项目必填");
            return;
        }
        const request = {
            name: name.trim(),
            companyRate: Number(companyRate) || 0,
            personalRate: personalRate.trim() === "" ? null : Number(personalRate) || 0,
        };
        setBusy(true);
        try {
            const api = new SalariesApi(apiConfig(token));
            if (item?.id) {
                await api.accountsetsAccountSetIdSalaryInsuranceIdPut({ accountSetId, id: item.id, saveInsuranceRequest: request });
            } else {
                await api.accountsetsAccountSetIdSalaryInsurancePost({ accountSetId, saveInsuranceRequest: request });
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
            title={item ? "编辑五险一金" : "新增五险一金"}
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
            <div className="flex flex-col gap-3">
                <Field label="缴纳项目" required>
                    <TextInput className="w-[220px]" value={name} onChange={setName} />
                </Field>
                <Field label="公司缴纳比例(%)">
                    <TextInput className="w-[220px]" type="number" value={companyRate} onChange={setCompanyRate} />
                </Field>
                <Field label="个人缴纳比例(%)">
                    <TextInput className="w-[220px]" type="number" value={personalRate} onChange={setPersonalRate} />
                </Field>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Dialog>
    );
}

export function LemonSalaryInsurance() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const data = useEmployeeAndInsurance();
    const [editing, setEditing] = useState<InsuranceItemDto | null | "new">(null);
    const [deleting, setDeleting] = useState<InsuranceItemDto | null>(null);
    const [busy, setBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    const remove = async () => {
        if (!token || !accountSetId || !deleting?.id || busy) {
            return;
        }
        setBusy(true);
        try {
            await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryInsuranceIdDelete({ accountSetId, id: deleting.id });
            setDeleting(null);
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "删除失败"));
            setDeleting(null);
        } finally {
            setBusy(false);
        }
    };

    const cols: Col[] = [
        { key: "name", title: "缴纳项目", width: "160px" },
        { key: "mode", title: "缴纳方式", width: "140px" },
        { key: "companyRate", title: "公司缴纳比例", width: "140px" },
        { key: "personalRate", title: "个人缴纳比例", width: "140px" },
        { key: "companyAmt", title: "公司缴纳金额", width: "140px", render: () => "--" },
        { key: "personalAmt", title: "个人缴纳金额", width: "140px", render: () => "--" },
        {
            key: "ops",
            title: "操作",
            render: (row: Row) => (
                <span className="flex gap-2">
                    <span
                        className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline"
                        onClick={() => setEditing((row.__item as InsuranceItemDto) ?? null)}
                    >
                        编辑
                    </span>
                    <span
                        className="cursor-pointer text-[13px] text-[#2f6bff] hover:underline"
                        onClick={() => setDeleting((row.__item as InsuranceItemDto) ?? null)}
                    >
                        删除
                    </span>
                </span>
            ),
        },
    ];
    return (
        <Page title="五险一金设置" subtitle="缴纳项目与公司/个人比例维护" icon={SafetyCertificateOutlined}>
            <SubTabs tabs={["五险一金设置"]} />
            <div className="mx-auto w-[1090px]">
                <ToolRow
                    left={
                        <Btn variant="primary" onClick={() => setEditing("new")}>
                            新增
                        </Btn>
                    }
                    right={
                        <span className="cursor-pointer" onClick={() => void data.reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    }
                />
                <StateBar loading={data.loading} error={data.error} />
                <div>
                    <Table
                        cols={cols}
                        rows={data.insurance.map((i) => ({
                            __item: i,
                            name: i.name,
                            mode: i.modeText ?? "按比例缴纳",
                            companyRate: `${i.companyRate}%`,
                            personalRate: i.personalRate == null ? "" : `${i.personalRate}%`,
                        }))}
                    />
                </div>
            </div>
            {editing && (
                <InsuranceEditDialog
                    item={editing === "new" ? null : editing}
                    onClose={() => setEditing(null)}
                    onSaved={() => void data.reload()}
                />
            )}
            {deleting && (
                <ConfirmDialog
                    title="删除缴纳项目"
                    text={`确定删除「${deleting.name ?? ""}」吗？`}
                    busy={busy}
                    onClose={() => setDeleting(null)}
                    onConfirm={() => void remove()}
                />
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
