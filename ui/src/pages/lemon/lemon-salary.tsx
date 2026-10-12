/**
 * 复刻区工资模块（对应柠檬云 /Salary/*）：
 * 工资管理、员工信息、工资统计表、五险一金设置。
 */
import { useCallback, useEffect, useState, type ReactNode } from "react";
import { CircleHelp } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";


import {
    Btn,
    CheckBox,
    Page,
    Pager,
    RefreshIcon,
    SubTabs,
    Table,
    Toast,
    ToolRow,
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { SalariesApi, AccountsApi, type AccrueViewDto, type AccountDto, type EmployeeDto, type InsuranceItemDto, type SalaryBillDto, type SalarySummaryRowDto } from "@/apis-gen";

/** 金额格式化：千分位两位小数，空值显示 0.00。 */
function nf(v: number | null | undefined): string {
    return (v ?? 0).toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** 工资数据 Hook（员工 / 险种 / 期间工资单 / 统计）。 */
function useSalaryData() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [employees, setEmployees] = useState<EmployeeDto[]>([]);
    const [insurance, setInsurance] = useState<InsuranceItemDto[]>([]);
    const [bills, setBills] = useState<SalaryBillDto[]>([]);
    const [summary, setSummary] = useState<SalarySummaryRowDto[]>([]);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async () => {
        if (!accountSetId) return;
        const api = new SalariesApi(apiConfig(token));
        try {
            setEmployees(await api.accountsetsAccountSetIdSalaryEmployeesGet({ accountSetId }));
            setInsurance(await api.accountsetsAccountSetIdSalaryInsuranceGet({ accountSetId }));
            setBills(await api.accountsetsAccountSetIdSalaryBillsGet({ accountSetId, period: 202607 }));
            setSummary(await api.accountsetsAccountSetIdSalarySummaryGet({ accountSetId, startPeriod: 202607, endPeriod: 202607 }));
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载工资数据失败。"));
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void reload();
    }, [reload]);

    return { employees, insurance, bills, summary, error, reload };
}

/* ---------- 工资管理 ---------- */

/** 计提工资、发放工资视图：两张金额卡。 */
/** 计提工资/发放工资视图（已接后端）：双卡显示当期应发/实发合计，一键生成计提/发放凭证。 */
function SalaryAccrueView() {
    const { token, user } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [period, setPeriod] = useState<number | null>(null);
    const [view, setView] = useState<AccrueViewDto | null>(null);
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    // 期间省略时取最近工资表期间；无工资表则回退当前月。
    const reload = useCallback(async () => {
        if (!token || !accountSetId) {
            return;
        }
        try {
            const api = new SalariesApi(apiConfig(token));
            const now = new Date();
            const resolved =
                period ??
                Number(`${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, "0")}`);
            setPeriod(resolved);
            setView(await api.accountsetsAccountSetIdSalaryAccrueViewGet({ accountSetId, period: resolved }));
        } catch (e) {
            setToast(await apiErrorMessage(e, "加载计提视图失败"));
        }
    }, [token, accountSetId, period]);
    useEffect(() => {
        void reload();
    }, [reload]);

    const stepMonth = (delta: number) => {
        if (!period) {
            return;
        }
        const year = Math.floor(period / 100);
        const month = period % 100;
        const next = new Date(year, month - 1 + delta, 1);
        setPeriod(Number(`${next.getFullYear()}${String(next.getMonth() + 1).padStart(2, "0")}`));
    };

    const generate = async (kind: "accrue" | "pay") => {
        if (!token || !accountSetId || !period || busy) {
            return;
        }
        setBusy(true);
        try {
            const api = new SalariesApi(apiConfig(token));
            const request = {
                accountSetId,
                generateSalaryVoucherRequest: { period, preparer: user?.displayName || user?.username || "" },
            };
            const result =
                kind === "accrue"
                    ? await api.accountsetsAccountSetIdSalaryGenerateAccruePost(request)
                    : await api.accountsetsAccountSetIdSalaryGeneratePayPost(request);
            setToast(`已生成 ${result.count ?? 0} 张凭证`);
        } catch (e) {
            setToast(await apiErrorMessage(e, "生成凭证失败"));
        } finally {
            setBusy(false);
        }
    };

    const periodText = period ? `${Math.floor(period / 100)}年${String(period % 100).padStart(2, "0")}月` : "加载中…";
    const cards: Array<[string, string, "accrue" | "pay"]> = [
        ["计提工资", nf(view?.payable), "accrue"],
        ["发放工资", nf(view?.net), "pay"],
    ];
    return (
        <>
            <ToolRow
                left={
                    <span className="inline-flex h-8 items-center gap-2 rounded-[2px] bg-[#44b449] px-2.5 text-sm text-white">
                        <span className="cursor-pointer opacity-80" onClick={() => stepMonth(-1)}>
                            ◀
                        </span>
                        {periodText}
                        <span className="mx-1 h-3.5 w-px bg-white/50" />
                        <span className="cursor-pointer opacity-80" onClick={() => stepMonth(1)}>
                            ▶
                        </span>
                    </span>
                }
                right={
                    <span className="flex items-center gap-1 text-[13px] text-[#3385ff]">
                        ⚙ 工资凭证设置
                    </span>
                }
            />
            <div className="mx-auto mt-6 grid w-[720px] grid-cols-2 gap-8">
                {cards.map(([title, amount, kind]) => (
                    <div key={kind} className="flex flex-col items-center rounded-md border border-[#e8e8e8] bg-white py-10">
                        <div className="text-[15px] text-[#333]">{title}</div>
                        <div className="mt-3 cursor-pointer border-b-2 border-[#44b449] text-[26px] text-[#44b449]">{amount}</div>
                        <Btn
                            variant={busy ? "disabled" : "primary"}
                            className="mt-6 !px-6"
                            onClick={() => void generate(kind)}
                        >
                            生成凭证
                        </Btn>
                    </div>
                ))}
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </>
    );
}

/** 新增员工表单（整页，境内人员）。 */
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

const empInput = "h-8 w-[220px] rounded-[2px] border border-[#d9d9d9] px-2 text-sm text-[#333] outline-none focus:border-[#44b449]";

/** 新增员工（整页表单，境内人员；已接后端 employeesPost）。 */
function EmployeeForm({ onBack, onSaved }: { onBack: () => void; onSaved: () => void }) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [form, setForm] = useState(emptyEmployee);
    const [accounts, setAccounts] = useState<AccountDto[]>([]);
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        new AccountsApi(apiConfig(token))
            .accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true })
            .then((list) => setAccounts(list ?? []))
            .catch(() => setAccounts([]));
    }, [token, accountSetId]);

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
            await new SalariesApi(apiConfig(token!)).accountsetsAccountSetIdSalaryEmployeesPost({
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
            ["*编号", <input key="code" className={empInput} value={form.code} onChange={(e) => set({ code: e.target.value })} />],
            ["*姓名", <input key="name" className={empInput} value={form.name} onChange={(e) => set({ name: e.target.value })} />],
            [
                "*证件类型",
                <select key="idType" className={empInput} value={form.idType} onChange={(e) => set({ idType: Number(e.target.value) })}>
                    <option value={1}>居民身份证</option>
                    <option value={2}>护照</option>
                    <option value={3}>其他</option>
                </select>,
            ],
        ],
        [
            ["*证件号码", <input key="idNo" className={empInput} value={form.idNo} onChange={(e) => set({ idNo: e.target.value })} />],
            [
                "*性别",
                <select key="gender" className={empInput} value={form.gender} onChange={(e) => set({ gender: Number(e.target.value) })}>
                    <option value={1}>男</option>
                    <option value={2}>女</option>
                </select>,
            ],
            ["*出生日期", <input key="birth" type="date" className={empInput} value={form.birthDate} onChange={(e) => set({ birthDate: e.target.value })} />],
        ],
        [
            ["手机", <input key="phone" className={empInput} value={form.phone} onChange={(e) => set({ phone: e.target.value })} />],
            ["邮箱", <input key="email" className={empInput} value={form.email} onChange={(e) => set({ email: e.target.value })} />],
            null,
        ],
        [
            ["银行名称", <input key="bank" className={empInput} value={form.bankName} onChange={(e) => set({ bankName: e.target.value })} />],
            ["银行账号", <input key="acct" className={empInput} value={form.bankAccountNo} onChange={(e) => set({ bankAccountNo: e.target.value })} />],
            ["*入职日期", <input key="hire" type="date" className={empInput} value={form.hireDate} onChange={(e) => set({ hireDate: e.target.value })} />],
        ],
        [
            ["*部门", <input key="dept" className={empInput} value={form.dept} onChange={(e) => set({ dept: e.target.value })} />],
            ["岗位", <input key="post" className={empInput} value={form.post} onChange={(e) => set({ post: e.target.value })} />],
            null,
        ],
        [
            ["职称", <input key="title" className={empInput} value={form.title} onChange={(e) => set({ title: e.target.value })} />],
            [
                "*计提工资科目",
                <select key="subject" className={empInput} value={form.subjectAccountId} onChange={(e) => set({ subjectAccountId: e.target.value })}>
                    <option value="">（请选择）</option>
                    {(accounts ?? []).map((a) => (
                        <option key={a.id} value={a.id}>
                            {`${a.code ?? ""} ${a.name ?? ""}`}
                        </option>
                    ))}
                </select>,
            ],
            ["备注", <input key="remark" className={empInput} value={form.remark} onChange={(e) => set({ remark: e.target.value })} />],
        ],
        [
            [
                "*员工状态",
                <select key="status" className={empInput} value={form.status} onChange={(e) => set({ status: Number(e.target.value) })}>
                    <option value={1}>在职</option>
                    <option value={2}>离职</option>
                </select>,
            ],
            null,
            null,
        ],
    ];

    return (
        <Page>
            <div className="pt-5 text-center text-[15px] font-bold text-[#333]">新增员工</div>
            <div className="mx-auto mt-3 w-[1000px]">
                <div className="flex justify-end">
                    <span className="flex text-sm">
                        <span className="cursor-pointer rounded-l-[2px] bg-[#44b449] px-4 py-1.5 text-white">境内人员</span>
                        <span className="cursor-pointer rounded-r-[2px] border border-[#d9d9d9] px-4 py-1.5 text-[#333]">境外人员</span>
                    </span>
                </div>
                <div className="mt-4 text-[15px] font-bold text-[#333]">基本信息</div>
                <div className="mt-3 flex flex-col gap-4">
                    {rows.map((row, i) => (
                        <div key={i} className="grid grid-cols-3 gap-8">
                            {row.map((cell, j) =>
                                cell ? (
                                    <div key={j} className="flex items-center">
                                        <span className="w-[150px] text-right text-sm text-[#333]">
                                            {String(cell[0]).startsWith("*") && <span className="text-[#f5222d]">*</span>}
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
                <div className="mt-8 text-[15px] font-bold text-[#333]">扣除款项</div>
                <div className="mt-3 flex items-center gap-4 text-sm text-[#333]">
                    <span>*是否缴纳五险一金：</span>
                    <span className="flex items-center gap-1.5">
                        <span className="h-3.5 w-3.5 rounded-full border-[5px] border-[#44b449] bg-white" /> 是
                    </span>
                    <span className="ml-4 flex items-center gap-1.5">
                        <span className="h-3.5 w-3.5 rounded-full border border-[#d9d9d9] bg-white" /> 否
                    </span>
                </div>
                <div className="mt-3 rounded-[2px] bg-[#fdf6e3] px-3 py-2 text-[13px] text-[#8a6d3b]">
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
    const data = useSalaryData();
    const [tab, setTab] = useState(0);
    const [adding, setAdding] = useState(false);
    if (tab === 1)
        return (
            <Page>
                <SubTabs tabs={["工资列表", "计提工资、发放工资"]} active={1} onChange={setTab} />
                <SalaryAccrueView />
            </Page>
        );
    if (adding)
        return (
            <EmployeeForm onBack={() => setAdding(false)} onSaved={data.reload} />
        );
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
    const list = data.bills;
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
        { dept: "合计", gross: totals.gross, deduct: totals.deductible, payable: totals.payable, personal: totals.personal, threshold: totals.threshold, tax: totals.tax, adjust: totals.adjust, net: totals.net, company: totals.company, cost: totals.cost, __cls: "bg-[#fafafa]" },
    ];
    return (
        <Page>
            <SubTabs tabs={["工资列表", "计提工资、发放工资"]} active={tab} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <span className="inline-flex h-8 items-center rounded-[2px] bg-[#44b449] px-2.5 text-sm text-white">
                            2026年07月
                            <span className="mx-1.5 h-3.5 w-px bg-white/50" />
                            ▾
                        </span>
                        <span className="cursor-pointer text-[13px] text-[#3385ff]">自定义工资项目</span>
                    </>
                }
                right={
                    <>
                        <CheckBox label="编辑、显示工资明细" />
                        <CheckBox label="多次发放工资" />
                        <Btn>生成工资条</Btn>
                        <span className="inline-flex items-center gap-1">
                            <Btn>同步个税申报表</Btn>
                            <CircleHelp className="h-3.5 w-3.5 text-[#999]" />
                        </span>
                        <Btn dropdown>导入</Btn>
                        <Btn>发送工资条</Btn>
                        <Btn>个税申报</Btn>
                        <Btn dropdown onClick={() => window.print()}>打印</Btn>
                        <Btn>附件</Btn>
                        <Btn dropdown>更多操作</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} />
            </div>
            <Pager total={1} count={1} />
        </Page>
    );
}

/* ---------- 员工信息 ---------- */

export function LemonSalaryEmployee() {
    const [adding, setAdding] = useState(false);
    const data = useSalaryData();
    if (adding) return <EmployeeForm onBack={() => setAdding(false)} onSaved={data.reload} />;
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
        ops: true,
    }));
    return (
        <Page>
            <SubTabs tabs={["员工信息"]} />
            <ToolRow
                left={
                    <>
                        <span className="inline-flex h-8 items-center rounded-[2px] bg-[#44b449] px-2.5 text-sm text-white">
                            所有员工
                            <span className="mx-1.5 h-3.5 w-px bg-white/50" />
                            ▾
                        </span>
                    </>
                }
                right={
                    <>
                        <CheckBox label="显示所有信息" />
                        <Btn variant="primary" onClick={() => setAdding(true)}>增加员工</Btn>
                        <Btn>同步税务人员信息</Btn>
                        <Btn dropdown>导入</Btn>
                        <Btn>导出</Btn>
                        <Btn dropdown>批量操作</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                <Table
                    cols={cols}
                    rows={rows.map((r) => ({
                        ...r,
                        ops: (
                            <span className="flex gap-2">
                                <span className="cursor-pointer text-[13px] text-[#3385ff] hover:underline">编辑</span>
                                <span className="cursor-pointer text-[13px] text-[#3385ff] hover:underline">删除</span>
                            </span>
                        ),
                    }))}
                />
            </div>
            <Pager total={1} count={1} />
        </Page>
    );
}

/* ---------- 工资统计表 ---------- */

export function LemonSalarySummary() {
    const data = useSalaryData();
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
        ...data.summary.map((r) => ({
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
            count: String(data.summary.reduce((s2, r) => s2 + (r.employeeCount ?? 0), 0)),
            payable: data.summary.reduce((s2, r) => s2 + (r.payable ?? 0), 0).toFixed(2),
            personal: data.summary.reduce((s2, r) => s2 + (r.personalDeduction ?? 0), 0).toFixed(2),
            company: data.summary.reduce((s2, r) => s2 + (r.companyPart ?? 0), 0).toFixed(2),
            net: data.summary.reduce((s2, r) => s2 + (r.net ?? 0), 0).toFixed(2),
            cost: data.summary.reduce((s2, r) => s2 + (r.cost ?? 0), 0).toFixed(2),
            __cls: "bg-[#fafafa]",
        },
    ];
    return (
        <Page>
            <SubTabs tabs={["工资统计表"]} />
            <ToolRow
                left={
                    <>
                        <span className="inline-flex h-8 items-center rounded-[2px] bg-[#44b449] px-2.5 text-sm text-white">
                            2026年01月—2026年10月
                            <span className="mx-1.5 h-3.5 w-px bg-white/50" />
                            ▾
                        </span>
                    </>
                }
                right={
                    <>
                        <CheckBox label="显示员工工资明细" />
                        <Btn onClick={() => window.print()}>打印</Btn>
                        <Btn variant="primary">导出</Btn>
                        <RefreshIcon />
                    </>
                }
            />
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} />
            </div>
            <Pager total={1} count={1} />
        </Page>
    );
}

/* ---------- 五险一金设置 ---------- */

export function LemonSalaryInsurance() {
    const data = useSalaryData();
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
            render: () => (
                <span className="cursor-pointer text-[13px] text-[#3385ff] hover:underline">编辑</span>
            ),
        },
    ];
    return (
        <Page>
            <SubTabs tabs={["五险一金设置"]} />
            <div className="mx-auto w-[1090px]">
                <ToolRow left={<Btn variant="primary">新增</Btn>} right={<RefreshIcon />} />
                <div>
                    <Table
                        cols={cols}
                        rows={data.insurance.map((i) => ({
                            name: i.name,
                            mode: "按比例缴纳",
                            companyRate: `${i.companyRate}%`,
                            personalRate: i.personalRate == null ? "" : `${i.personalRate}%`,
                        }))}
                    />
                </div>
            </div>
        </Page>
    );
}
