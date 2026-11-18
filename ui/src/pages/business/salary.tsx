/**
 * 复刻区工资模块：
 * 工资管理、员工信息、工资统计表、五险一金设置。
 * 视觉遵循全站云财蓝：主色 #273cf3、直角卡片、白卡 + slate 灰阶文字。
 */
import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { Alert, Drawer, Dropdown, Radio, Segmented } from "antd";
import { InfoCircleOutlined, SettingOutlined } from "@ant-design/icons";

import { HelpTip } from "@/components/page/help-tip";

import { ICON_STYLE, PILL, FORM_CONTROL_W, FORM_LABEL_W } from "@/components/page/styles";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { makePeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { printPdf } from "@/lib/print-pdf";

import {
    Btn,
    ExportBtn,
    PrintBtn,
    CheckBox,
    DateInput,
    Dialog,
    EmptyTip,
    HintBox,
    LinkAction,
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
    WipBtn,
} from "./components/ui";
import type { Col, Row } from "./components/ui";
import { usePeriodStepper } from "./components/usePeriodStepper";
import {
    SalariesApi,
    AccountsApi,
    type AccrueViewDto,
    type AccountDto,
    type EmployeeDto,
    type EmployeeImportResultDto,
    type InsuranceItemDto,
    type SalaryBillDto,
    type SalaryBillImportResultDto,
    type SalarySummaryRowDto,
    type SalaryVoucherSettingDto,
} from "@/apis-gen";
import { syncIitPersonnel } from "@/lib/tax-iit";
import { IitLoginModal } from "@/pages/tax/IitLoginModal";

/* ---------- 公共小工具 ---------- */

/** 金额格式化：千分位两位小数，空值显示 0.00。 */
function nf(v: number | null | undefined): string {
    return (v ?? 0).toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** 期间对象 → 「2026年10月」。 */
function periodLabel(p: AccountingPeriod): string {
    return `${p.year}年${p.month}月`;
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

/** 二进制下载（xlsx / pdf）：fetch + blob + a[download]，返回错误文案（成功返回 null）。 */
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

/** 编辑/新增对话框里的「标签 + 控件」行。 */
function Field({ label, required, children }: { label: string; required?: boolean; children: ReactNode }) {
    return (
        <label className="flex items-center gap-3">
            <span className={`${FORM_LABEL_W} shrink-0 text-right text-[13px] text-(--foreground)`}>
                {required && <span className="text-(--danger)">*</span>}
                {label}：
            </span>
            {children}
        </label>
    );
}

/** 员工抽屉的可编辑字段。日期统一用 yyyy-MM-dd 字符串，提交时再包成 Date。 */
interface EmployeeDraft {
    code: string;
    name: string;
    idType: number;
    idNo: string;
    gender: number;
    birthDate: string;
    phone: string;
    email: string;
    education: string;
    isDisabled: boolean;
    isMartyrFamily: boolean;
    isSoloElder: boolean;
    /** 是否高级专家："" 未选择 / "1" 是 / "2" 否 */
    isSeniorExpert: string;
    bankName: string;
    bankAccountNo: string;
    remark: string;
    employmentType: number;
    deductBasicExpense: boolean;
    dept: string;
    post: string;
    title: string;
    hireDate: string;
    payStartDate: string;
    subjectAccountId: string;
    status: number;
    paysSocialInsurance: boolean;
    /** 险种标识 → 缴纳基数文本（空串表示未填，沿用默认基数） */
    bases: Record<string, string>;
    /** 险种标识 → 公司比例文本（空串表示沿用险种默认比例） */
    companyRates: Record<string, string>;
    /** 险种标识 → 个人比例文本（空串表示沿用险种默认比例） */
    personalRates: Record<string, string>;
}

/** 学历候选（与人员档案一致，允许留空）。 */
const EDUCATION_OPTIONS = ["专科", "本科", "硕士", "博士", "其他"];

/** 任职受雇从业类型展示文本 → 枚举值（后端只回显文本）。 */
const employmentTypeOf = (t?: string | null) =>
    t === "保险营销员" ? 2 : t === "证券经纪人" ? 3 : t === "劳务派遣" ? 4 : t === "其他" ? 5 : 1;

/** 三态「是否高级专家」：null 未选择。 */
const seniorExpertOf = (v: boolean | null | undefined) => (v === true ? "1" : v === false ? "2" : "");

/** 缴纳基数文本 → 数字；非法或负数按 0（即不登记该险种）。 */
function toAmount(text: string): number {
    const n = Number(text.replace(/[,，\s]/g, ""));
    return Number.isFinite(n) && n > 0 ? n : 0;
}

/** 员工险种生效基数：个人已设置（>0）取个人，否则取「五险一金设置」的默认基数。 */
function effectiveBaseOf(item: InsuranceItemDto, ownText: string | undefined): number {
    const own = toAmount(ownText ?? "");
    return own > 0 ? own : toAmount(String(item.baseAmount ?? 0));
}

/** 员工险种生效比例：个人文本非空取个人（0 也是有效值），否则取险种默认。 */
function effectiveRateOf(ownText: string | undefined, fallback: number | null | undefined): number | null {
    const text = (ownText ?? "").trim();
    if (text !== "") {
        const n = Number(text.replace(/[,，\s]/g, ""));
        return Number.isFinite(n) ? n : 0;
    }
    return fallback ?? null;
}

/** 比例文本 → 提交值；空串表示沿用默认，非数字按 0。 */
function rateForSubmit(text: string | undefined): number | null {
    const trimmed = (text ?? "").trim();
    if (trimmed === "") {
        return null;
    }
    const n = Number(trimmed.replace(/[,，\s]/g, ""));
    return Number.isFinite(n) ? n : 0;
}

/** 日期字符串 → 提交用 Date（先按 yyyy-MM-dd 构造，避免东八区回退一天）。 */
const toDateArg = (text: string) => (text ? new Date(text) : null);

function emptyDraft(code: string): EmployeeDraft {
    return {
        code,
        name: "",
        idType: 1,
        idNo: "",
        gender: 1,
        birthDate: "",
        phone: "",
        email: "",
        education: "本科",
        isDisabled: false,
        isMartyrFamily: false,
        isSoloElder: false,
        isSeniorExpert: "",
        bankName: "",
        bankAccountNo: "",
        remark: "",
        employmentType: 1,
        deductBasicExpense: true,
        dept: "",
        post: "",
        title: "",
        hireDate: "",
        payStartDate: "",
        subjectAccountId: "",
        status: 1,
        paysSocialInsurance: true,
        bases: {},
        companyRates: {},
        personalRates: {},
    };
}

/** 员工档案 → 抽屉草稿；各险种先按账套配置铺空，再用员工已登记的个人值覆盖。 */
function draftFromEmployee(e: EmployeeDto, insurance: InsuranceItemDto[]): EmployeeDraft {
    const bases: Record<string, string> = {};
    const companyRates: Record<string, string> = {};
    const personalRates: Record<string, string> = {};
    for (const item of insurance) {
        if (item.id) {
            bases[item.id] = "";
            companyRates[item.id] = "";
            personalRates[item.id] = "";
        }
    }
    for (const row of e.insurances ?? []) {
        if (!row.insuranceItemId) {
            continue;
        }
        if ((row.insuranceBase ?? 0) > 0) {
            bases[row.insuranceItemId] = String(row.insuranceBase);
        }
        if (row.companyRate != null) {
            companyRates[row.insuranceItemId] = String(row.companyRate);
        }
        if (row.personalRate != null) {
            personalRates[row.insuranceItemId] = String(row.personalRate);
        }
    }

    return {
        code: e.code ?? "",
        name: e.name ?? "",
        idType: idTypeOf(e.idTypeText),
        idNo: e.idNo ?? "",
        gender: genderOf(e.genderText),
        birthDate: toDateInput(e.birthDate),
        phone: e.phone ?? "",
        email: e.email ?? "",
        education: e.education ?? "",
        isDisabled: e.isDisabled ?? false,
        isMartyrFamily: e.isMartyrFamily ?? false,
        isSoloElder: e.isSoloElder ?? false,
        isSeniorExpert: seniorExpertOf(e.isSeniorExpert),
        bankName: e.bankName ?? "",
        bankAccountNo: e.bankAccountNo ?? "",
        remark: e.remark ?? "",
        employmentType: employmentTypeOf(e.employmentTypeText),
        deductBasicExpense: e.deductBasicExpense ?? true,
        dept: e.dept ?? "",
        post: e.post ?? "",
        title: e.title ?? "",
        hireDate: toDateInput(e.hireDate),
        payStartDate: toDateInput(e.payStartDate),
        subjectAccountId: e.subjectAccountId ?? "",
        status: statusOf(e.statusText),
        paysSocialInsurance: e.paysSocialInsurance ?? true,
        bases,
        companyRates,
        personalRates,
    };
}

/** 草稿 → SaveEmployeeRequest；不缴五险一金时不提交任何个人值，留空项沿用账套默认。 */
function draftToRequest(d: EmployeeDraft, insurance: InsuranceItemDto[]) {
    const insurances = d.paysSocialInsurance
        ? insurance
              .map((i) => {
                  const id = i.id ?? "";
                  return {
                      insuranceItemId: id,
                      insuranceBase: toAmount(d.bases[id] ?? ""),
                      companyRate: rateForSubmit(d.companyRates[id]),
                      personalRate: rateForSubmit(d.personalRates[id]),
                  };
              })
              .filter((row) => row.insuranceItemId && (row.insuranceBase > 0 || row.companyRate !== null || row.personalRate !== null))
        : [];

    return {
        code: d.code.trim(),
        name: d.name.trim(),
        gender: d.gender,
        idType: d.idType,
        idNo: d.idNo.trim() || null,
        dept: d.dept.trim() || null,
        phone: d.phone.trim() || null,
        email: d.email.trim() || null,
        birthDate: toDateArg(d.birthDate),
        hireDate: toDateArg(d.hireDate),
        bankName: d.bankName.trim() || null,
        bankAccountNo: d.bankAccountNo.trim() || null,
        post: d.post.trim() || null,
        title: d.title.trim() || null,
        education: d.education || null,
        isDisabled: d.isDisabled,
        isMartyrFamily: d.isMartyrFamily,
        isSoloElder: d.isSoloElder,
        isSeniorExpert: d.isSeniorExpert === "" ? null : d.isSeniorExpert === "1",
        employmentType: d.employmentType,
        deductBasicExpense: d.deductBasicExpense,
        payStartDate: toDateArg(d.payStartDate),
        paysSocialInsurance: d.paysSocialInsurance,
        insurances,
        subjectAccountId: d.subjectAccountId || null,
        status: d.status,
        remark: d.remark.trim() || null,
    };
}

/** 新增时的建议编号：取现有编号末尾数字的最大值 +1，三位补零。 */
function nextEmployeeCode(employees: EmployeeDto[]): string {
    const nums = employees
        .map((e) => Number((e.code ?? "").match(/\d+$/)?.[0] ?? ""))
        .filter((n) => Number.isFinite(n) && n > 0);
    return String((nums.length ? Math.max(...nums) : 0) + 1).padStart(3, "0");
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
                <div className="flex justify-end gap-3">
                    <Btn variant="primary" disabled={busy} onClick={onConfirm}>
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

/** 凭证进度标签：全部生成绿、部分生成琥珀、未生成灰。 */
function VoucherState({ done, total }: { done: number; total: number }) {
    if (total === 0) {
        return (
            <span className="rounded bg-(--muted) px-2 py-0.5 text-[11px] font-medium text-(--muted-foreground) dark:bg-(--card)/10 dark:text-(--muted-foreground)">
                暂无工资单
            </span>
        );
    }
    const all = done >= total;
    const tone = all ? PILL.green : done > 0 ? PILL.amber : PILL.gray;
    return (
        <span className="rounded px-2 py-0.5 text-[11px] font-medium" style={{ background: tone.bg, color: tone.fg }}>
            {all ? `已全部生成（${total} 张）` : done > 0 ? `已生成 ${done} / ${total}` : "未生成"}
        </span>
    );
}

/** 处理步骤行：编号 + 说明 + 金额 + 凭证进度 + 生成按钮。 */
function AccrueStep({
    no,
    title,
    desc,
    amountLabel,
    amount,
    done,
    total,
    actionText,
    busy,
    disabled,
    onGenerate,
}: {
    no: string;
    title: string;
    desc: string;
    amountLabel: string;
    amount?: number | null;
    done: number;
    total: number;
    actionText: string;
    busy: boolean;
    disabled: boolean;
    onGenerate: () => void;
}) {
    return (
        <div className="flex flex-wrap items-center gap-x-6 gap-y-4 rounded-lg border border-(--border) px-5 py-4 dark:border-white/10">
            <span className="flex size-7 shrink-0 items-center justify-center rounded-md bg-(--muted) text-[12px] font-semibold tabular-nums text-(--foreground) dark:bg-(--card)/10 dark:text-(--muted-foreground)">
                {no}
            </span>
            <div className="min-w-[200px] flex-1">
                <div className="flex flex-wrap items-center gap-2">
                    <span className="text-[14px] font-semibold text-(--foreground) dark:text-(--muted-foreground)">{title}</span>
                    <VoucherState done={done} total={total} />
                </div>
                <div className="mt-1 text-[12.5px] leading-5 text-(--muted-foreground) dark:text-(--muted-foreground)">{desc}</div>
            </div>
            <div className="min-w-[128px] text-right">
                <div className="text-[12px] text-(--muted-foreground) dark:text-(--muted-foreground)">{amountLabel}</div>
                <div className="mt-0.5 text-[20px] leading-none font-semibold tabular-nums text-(--foreground) dark:text-(--muted-foreground)">
                    ¥{nf(amount)}
                </div>
            </div>
            <Btn variant="primary" disabled={disabled} onClick={onGenerate}>
                {busy ? "生成中…" : actionText}
            </Btn>
        </div>
    );
}

/** 计提工资/发放工资视图（已接后端）：按期展示应发/实发金额与凭证生成进度，一键生成对应凭证。 */
function SalaryAccrueView() {
    const { token, user } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const { period, stepper } = usePeriodStepper();
    const [view, setView] = useState<AccrueViewDto | null>(null);
    const [bills, setBills] = useState<SalaryBillDto[]>([]);
    const [toast, setToast] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);
    const [busy, setBusy] = useState<"accrue" | "pay" | null>(null);
    const [settingOpen, setSettingOpen] = useState(false);

    const reload = useCallback(async () => {
        if (!token || !accountSetId) {
            return;
        }
        setLoading(true);
        try {
            const api = new SalariesApi(apiConfig(token));
            const [accrueView, billList] = await Promise.all([
                api.accountsetsAccountSetIdSalaryAccrueViewGet({ accountSetId, period: period.period }),
                api.accountsetsAccountSetIdSalaryBillsGet({ accountSetId, period: period.period }),
            ]);
            setView(accrueView);
            setBills(billList ?? []);
            setToast(null);
        } catch (e) {
            setView(null);
            setBills([]);
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
        setBusy(kind);
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
            setBusy(null);
        }
    };

    // 只有应发 / 实发大于 0 的工资单才会出凭证，与后端的跳过条件保持一致。
    const accrueTargets = bills.filter((b) => (b.cost ?? 0) > 0);
    const payTargets = bills.filter((b) => (b.net ?? 0) > 0);
    const accrueDone = accrueTargets.filter((b) => b.accruedVoucherText).length;
    const payDone = payTargets.filter((b) => b.paidVoucherText).length;

    return (
        <>
            <ToolRow
                left={<PeriodStepper {...stepper} />}
                right={
                    <Btn
                        icon={<SettingOutlined />}
                        onClick={() => setSettingOpen(true)}
                        title="配置计提 / 发放凭证使用的科目与发放账户"
                    >
                        工资凭证设置
                    </Btn>
                }
            />
            <StateBar loading={loading} error={toast && !view ? toast : null} />
            <div className="px-4 pb-6">
                <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
                    <span className="text-[13px] font-medium text-(--foreground) dark:text-(--muted-foreground)">本期处理</span>
                    <span className="text-[12.5px] text-(--muted-foreground) dark:text-(--muted-foreground)">
                        {bills.length > 0
                            ? `${periodLabel(period)} · ${bills.length} 人工资单`
                            : view
                              ? `${periodLabel(period)} · 暂无工资单，请先在「工资列表」录入或导入`
                              : periodLabel(period)}
                    </span>
                </div>
                <div className="flex flex-col gap-3">
                    <AccrueStep
                        no="01"
                        title="计提工资"
                        desc="按应发工资合计计提「应付职工薪酬」，生成计提凭证。"
                        amountLabel="应发合计"
                        amount={view?.payable}
                        done={accrueDone}
                        total={accrueTargets.length}
                        actionText="生成计提凭证"
                        busy={busy === "accrue"}
                        disabled={busy !== null}
                        onGenerate={() => void generate("accrue")}
                    />
                    <AccrueStep
                        no="02"
                        title="发放工资"
                        desc="按实发工资发放，代扣个税与个人款项在凭证中分列。"
                        amountLabel="实发合计"
                        amount={view?.net}
                        done={payDone}
                        total={payTargets.length}
                        actionText="生成发放凭证"
                        busy={busy === "pay"}
                        disabled={busy !== null}
                        onGenerate={() => void generate("pay")}
                    />
                </div>
                <div className="mt-4 flex items-start gap-2 rounded-lg bg-(--table-zebra-bg) px-4 py-3 text-[12.5px] leading-5 text-(--muted-foreground) dark:bg-(--card)/5 dark:text-(--muted-foreground)">
                    <InfoCircleOutlined className="mt-[3px]" style={ICON_STYLE.muted} />
                    <span>
                        先计提、再发放。计提凭证：借 员工计提工资科目 / 贷 应付职工薪酬；发放凭证：借 应付职工薪酬 / 贷
                        银行存款、应交个人所得税及代扣个人款项。科目可在右上角「工资凭证设置」中指定，留空时按编码自动推断。
                    </span>
                </div>
            </div>
            {toast && !loading && <Toast text={toast} onClose={() => setToast(null)} />}
            {settingOpen && (
                <SalaryVoucherSettingDialog
                    onClose={() => setSettingOpen(false)}
                    onSaved={(text) => {
                        setSettingOpen(false);
                        setToast(text);
                    }}
                />
            )}
        </>
    );
}

/** 工资凭证科目设置：把应付职工薪酬 / 个税 / 代扣款项 / 发放账户显式定下来，留空则继续自动推断。 */
function SalaryVoucherSettingDialog({ onClose, onSaved }: { onClose: () => void; onSaved: (text: string) => void }) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const accounts = useLeafAccounts();
    const [form, setForm] = useState({ payrollAccountId: "", taxAccountId: "", insuranceAccountId: "", bankAccountId: "" });
    const [resolved, setResolved] = useState<SalaryVoucherSettingDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (!token || !accountSetId) {
            return;
        }
        setLoading(true);
        new SalariesApi(apiConfig(token))
            .accountsetsAccountSetIdSalaryVoucherSettingGet({ accountSetId })
            .then((dto) => {
                setResolved(dto);
                setForm({
                    payrollAccountId: dto.payrollAccountId ?? "",
                    taxAccountId: dto.taxAccountId ?? "",
                    insuranceAccountId: dto.insuranceAccountId ?? "",
                    bankAccountId: dto.bankAccountId ?? "",
                });
                setError(null);
            })
            .catch(async (e) => setError(await apiErrorMessage(e, "加载工资凭证设置失败")))
            .finally(() => setLoading(false));
    }, [token, accountSetId]);

    const save = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        setBusy(true);
        try {
            const dto = await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryVoucherSettingPut({
                accountSetId,
                saveSalaryVoucherSettingRequest: {
                    payrollAccountId: form.payrollAccountId || undefined,
                    taxAccountId: form.taxAccountId || undefined,
                    insuranceAccountId: form.insuranceAccountId || undefined,
                    bankAccountId: form.bankAccountId || undefined,
                },
            });
            onSaved(
                `已保存：应付职工薪酬 ${dto.payrollAccountText ?? "未设置"}，发放账户 ${dto.bankAccountText ?? "未设置"}`,
            );
        } catch (e) {
            setError(await apiErrorMessage(e, "保存失败"));
        } finally {
            setBusy(false);
        }
    };

    const options = accounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` }));
    const rows: Array<{ key: keyof typeof form; label: string; hint: string; auto: string | null | undefined }> = [
        { key: "payrollAccountId", label: "应付职工薪酬科目", hint: "计提凭证贷方 / 发放凭证借方", auto: resolved?.payrollAccountText },
        { key: "bankAccountId", label: "发放账户", hint: "发放凭证贷方「银行存款」", auto: resolved?.bankAccountText },
        { key: "taxAccountId", label: "个人所得税科目", hint: "发放时贷方记应缴个税", auto: resolved?.taxAccountText },
        { key: "insuranceAccountId", label: "代扣个人款项科目", hint: "发放时贷方记代扣社保等", auto: resolved?.insuranceAccountText },
    ];

    return (
        <Dialog
            title="工资凭证设置"
            width={560}
            onClose={onClose}
            footer={
                <div className="flex justify-end gap-2">
                    <Btn onClick={onClose}>取消</Btn>
                    <Btn variant="primary" onClick={() => void save()}>
                        {busy ? "保存中…" : "保存"}
                    </Btn>
                </div>
            }
        >
            <div className="flex flex-col gap-3 py-2">
                <div className="text-[12.5px] text-(--muted-foreground)">
                    留空的项沿用自动推断（按科目编码找应付职工薪酬 2211、个人所得税、其他应付款，发放账户取首个启用的银行日记账）。
                </div>
                {loading && <div className="text-[13px] text-(--muted-foreground)">加载中…</div>}
                {rows.map((r) => (
                    <Field key={r.key} label={r.label}>
                        <span className="flex min-w-0 flex-1 flex-col gap-1">
                            <Select
                                className="min-w-0 flex-1"
                                value={form[r.key]}
                                onChange={(v) => setForm((prev) => ({ ...prev, [r.key]: v }))}
                                options={[{ value: "", label: `自动：${r.auto ?? "未找到"}` }, ...options]}
                            />
                            <span className="pl-0.5 text-[12.5px] text-(--muted-foreground)">{r.hint}</span>
                        </span>
                    </Field>
                ))}
                {error && <div className="text-[13px] text-(--danger)">{error}</div>}
            </div>
        </Dialog>
    );
}

/** 抽屉分区：加粗小标题 + 内容。 */
function Section({ title, children }: { title: string; children: ReactNode }) {
    return (
        <section>
            <div className="text-[15px] font-bold text-(--foreground)">{title}</div>
            <div className="mt-3">{children}</div>
        </section>
    );
}

/** 新增/编辑员工抽屉（对应「新增员工」）：三段版式，扣除款项比例取「五险一金设置」。 */
function EmployeeDrawer({
    employee,
    insurance,
    accounts,
    employees,
    open,
    onClose,
    onSaved,
}: {
    employee: EmployeeDto | null;
    insurance: InsuranceItemDto[];
    accounts: AccountDto[];
    /** 全部员工：仅用于新增时建议编号。 */
    employees: EmployeeDto[];
    open: boolean;
    onClose: () => void;
    onSaved: (text: string) => void;
}) {
    return (
        <Drawer
            open={open}
            size={1000}
            title={employee ? `编辑员工 - ${employee.name ?? employee.code ?? ""}` : "新增员工"}
            onClose={onClose}
            styles={{ body: { display: "flex", flexDirection: "column", gap: 24 } }}
        >
            {open && (
                <EmployeeDrawerForm
                    key={employee?.id ?? "new"}
                    employee={employee}
                    insurance={insurance}
                    accounts={accounts}
                    suggestedCode={nextEmployeeCode(employees)}
                    onClose={onClose}
                    onSaved={onSaved}
                />
            )}
        </Drawer>
    );
}

/** 抽屉表单体：随抽屉打开重新挂载，草稿从 employee（或建议编号）初始化。 */
function EmployeeDrawerForm({
    employee,
    insurance,
    accounts,
    suggestedCode,
    onClose,
    onSaved,
}: {
    employee: EmployeeDto | null;
    insurance: InsuranceItemDto[];
    accounts: AccountDto[];
    suggestedCode: string;
    onClose: () => void;
    onSaved: (text: string) => void;
}) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [draft, setDraft] = useState<EmployeeDraft>(() =>
        employee ? draftFromEmployee(employee, insurance) : emptyDraft(suggestedCode),
    );
    // 境外人员不持有居民身份证，证件类型只给护照/其他；打开时按已有证件类型回推。
    const [abroad, setAbroad] = useState(() => (employee ? idTypeOf(employee.idTypeText) !== 1 : false));
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const set = (patch: Partial<EmployeeDraft>) => setDraft((prev) => ({ ...prev, ...patch }));

    const save = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (!draft.code.trim() || !draft.name.trim()) {
            setError("编号与姓名必填");
            return;
        }
        if (draft.idType === 1 && !draft.idNo.trim()) {
            setError("证件类型为居民身份证时，证件号码必填");
            return;
        }
        setBusy(true);
        setError(null);
        try {
            const api = new SalariesApi(apiConfig(token));
            const body = draftToRequest(draft, insurance);
            if (employee?.id) {
                await api.accountsetsAccountSetIdSalaryEmployeesIdPut({ accountSetId, id: employee.id, saveEmployeeRequest: body });
            } else {
                await api.accountsetsAccountSetIdSalaryEmployeesPost({ accountSetId, saveEmployeeRequest: body });
            }
            onSaved(employee ? "员工已更新" : "员工已新增");
        } catch (e) {
            setError(await apiErrorMessage(e, "保存失败"));
        } finally {
            setBusy(false);
        }
    };

    const idTypeOptions = abroad
        ? [
              { value: "2", label: "护照" },
              { value: "3", label: "其他" },
          ]
        : [
              { value: "1", label: "居民身份证" },
              { value: "2", label: "护照" },
              { value: "3", label: "其他" },
          ];

    /** 金额列按生效缴纳基数 × 生效比例（个人填写优先，留空取账套默认）实时算出，只读。 */
    const insuranceRows: Row[] = insurance.map((item) => {
        const id = item.id ?? "";
        const base = effectiveBaseOf(item, draft.bases[id]);
        const companyRate = effectiveRateOf(draft.companyRates[id], item.companyRate) ?? 0;
        const personalRate = effectiveRateOf(draft.personalRates[id], item.personalRate) ?? 0;
        return {
            __id: id,
            name: item.name,
            cAmt: nf((base * companyRate) / 100),
            pAmt: nf((base * personalRate) / 100),
        };
    });
    /** 比例输入：留空显示账套默认值占位，填写后按个人比例计算。 */
    const rateInput = (id: string, field: "companyRates" | "personalRates", fallback: number | null | undefined) => (
        <TextInput
            className="w-full"
            type="number"
            value={draft[field][id] ?? ""}
            placeholder={fallback == null ? "默认 —" : `默认 ${fallback}%`}
            onChange={(v) => set({ [field]: { ...draft[field], [id]: v } } as Partial<EmployeeDraft>)}
        />
    );
    const insuranceCols: Col[] = [
        { key: "name", title: "缴纳项目", width: "140px" },
        {
            key: "base",
            title: "缴纳基数",
            width: "150px",
            render: (row) => {
                const id = String(row.__id);
                const item = insurance.find((i) => i.id === id);
                const defaultBase = toAmount(String(item?.baseAmount ?? 0));
                return (
                    <TextInput
                        className="w-full"
                        type="number"
                        value={draft.bases[id] ?? ""}
                        placeholder={defaultBase > 0 ? `默认 ${nf(defaultBase)}` : "未设置"}
                        onChange={(v) => set({ bases: { ...draft.bases, [id]: v } })}
                    />
                );
            },
        },
        {
            key: "cRate",
            title: "比例",
            group: "公司承担部分",
            width: "110px",
            render: (row) => {
                const id = String(row.__id);
                const item = insurance.find((i) => i.id === id);
                return rateInput(id, "companyRates", item?.companyRate);
            },
        },
        { key: "cAmt", title: "金额", group: "公司承担部分", align: "right", width: "120px" },
        {
            key: "pRate",
            title: "比例",
            group: "个人承担部分",
            width: "110px",
            render: (row) => {
                const id = String(row.__id);
                const item = insurance.find((i) => i.id === id);
                return rateInput(id, "personalRates", item?.personalRate);
            },
        },
        { key: "pAmt", title: "金额", group: "个人承担部分", align: "right", width: "120px" },
    ];

    return (
        <>
            <div className="flex justify-end">
                <Segmented
                    options={["境内人员", "境外人员"]}
                    value={abroad ? "境外人员" : "境内人员"}
                    onChange={(v) => {
                        const isAbroad = v === "境外人员";
                        setAbroad(isAbroad);
                        if (isAbroad && draft.idType === 1) {
                            set({ idType: 2 });
                        }
                    }}
                />
            </div>

            <Section title="基本信息">
                <div className="grid grid-cols-3 gap-x-6 gap-y-4">
                    <Field label="编号" required>
                        <TextInput className="min-w-0 flex-1" value={draft.code} onChange={(v) => set({ code: v })} />
                    </Field>
                    <Field label="姓名" required>
                        <TextInput className="min-w-0 flex-1" value={draft.name} onChange={(v) => set({ name: v })} />
                    </Field>
                    <Field label="证件类型" required>
                        <Select
                            className="min-w-0 flex-1"
                            value={String(draft.idType)}
                            onChange={(v) => set({ idType: Number(v) })}
                            options={idTypeOptions}
                        />
                    </Field>
                    <Field label="证件号码" required={draft.idType === 1}>
                        <TextInput className="min-w-0 flex-1" value={draft.idNo} onChange={(v) => set({ idNo: v })} />
                    </Field>
                    <Field label="性别" required>
                        <Select
                            className="min-w-0 flex-1"
                            value={String(draft.gender)}
                            onChange={(v) => set({ gender: Number(v) })}
                            options={[
                                { value: "1", label: "男" },
                                { value: "2", label: "女" },
                            ]}
                        />
                    </Field>
                    <Field label="出生日期">
                        <DateInput className="min-w-0 flex-1" value={draft.birthDate} onChange={(v) => set({ birthDate: v })} />
                    </Field>
                    <Field label="手机">
                        <TextInput className="min-w-0 flex-1" value={draft.phone} onChange={(v) => set({ phone: v })} />
                    </Field>
                    <Field label="邮箱">
                        <TextInput className="min-w-0 flex-1" value={draft.email} onChange={(v) => set({ email: v })} />
                    </Field>
                    <Field label="开户银行">
                        <TextInput className="min-w-0 flex-1" value={draft.bankName} onChange={(v) => set({ bankName: v })} />
                    </Field>
                    <Field label="银行账号">
                        <TextInput className="min-w-0 flex-1" value={draft.bankAccountNo} onChange={(v) => set({ bankAccountNo: v })} />
                    </Field>
                    <Field label="备注">
                        <TextInput className="min-w-0 flex-1" value={draft.remark} onChange={(v) => set({ remark: v })} />
                    </Field>
                </div>
            </Section>

            <Section title="任职受雇信息">
                <div className="grid grid-cols-3 gap-x-6 gap-y-4">
                    <Field label="从业类型" required>
                        <Select
                            className="min-w-0 flex-1"
                            value={String(draft.employmentType)}
                            onChange={(v) => set({ employmentType: Number(v) })}
                            options={[
                                { value: "1", label: "雇员" },
                                { value: "2", label: "保险营销员" },
                                { value: "3", label: "证券经纪人" },
                                { value: "4", label: "劳务派遣" },
                                { value: "5", label: "其他" },
                            ]}
                        />
                    </Field>
                    <Field label="部门">
                        <TextInput className="min-w-0 flex-1" value={draft.dept} onChange={(v) => set({ dept: v })} />
                    </Field>
                    <Field label="岗位">
                        <TextInput className="min-w-0 flex-1" value={draft.post} onChange={(v) => set({ post: v })} />
                    </Field>
                    <Field label="职称">
                        <TextInput className="min-w-0 flex-1" value={draft.title} onChange={(v) => set({ title: v })} />
                    </Field>
                    <Field label="学历">
                        <Select
                            className="min-w-0 flex-1"
                            value={draft.education}
                            onChange={(v) => set({ education: v })}
                            options={[{ value: "", label: "（未填写）" }, ...EDUCATION_OPTIONS]}
                        />
                    </Field>
                    <Field label="入职日期">
                        <DateInput className="min-w-0 flex-1" value={draft.hireDate} onChange={(v) => set({ hireDate: v })} />
                    </Field>
                    <Field label="工资始发日期">
                        <DateInput className="min-w-0 flex-1" value={draft.payStartDate} onChange={(v) => set({ payStartDate: v })} />
                    </Field>
                    <div className="flex min-w-0 items-center gap-3">
                        <span className={`${FORM_LABEL_W} shrink-0 text-right text-[13px] text-(--foreground)`}>
                            <span className="text-(--danger)">*</span>减除费用：
                        </span>
                        <Radio.Group
                            value={draft.deductBasicExpense ? "1" : "2"}
                            onChange={(e) => set({ deductBasicExpense: e.target.value === "1" })}
                        >
                            <Radio value="1">是</Radio>
                            <Radio value="2">否</Radio>
                        </Radio.Group>
                    </div>
                    <Field label="高级专家">
                        <Select
                            className="min-w-0 flex-1"
                            value={draft.isSeniorExpert}
                            onChange={(v) => set({ isSeniorExpert: v })}
                            options={[
                                { value: "", label: "（未选择）" },
                                { value: "1", label: "是" },
                                { value: "2", label: "否" },
                            ]}
                        />
                    </Field>
                    <Field label="员工状态" required>
                        <Select
                            className="min-w-0 flex-1"
                            value={String(draft.status)}
                            onChange={(v) => set({ status: Number(v) })}
                            options={[
                                { value: "1", label: "在职" },
                                { value: "2", label: "离职" },
                            ]}
                        />
                    </Field>
                    <Field label="计提工资科目">
                        <Select
                            className="min-w-0 flex-1"
                            value={draft.subjectAccountId}
                            onChange={(v) => set({ subjectAccountId: v })}
                            options={[
                                { value: "", label: "（请选择）" },
                                ...accounts.map((a) => ({ value: a.id ?? "", label: `${a.code ?? ""} ${a.name ?? ""}` })),
                            ]}
                        />
                    </Field>
                    <div className="flex min-w-0 items-center gap-4">
                        <span className={`${FORM_LABEL_W} shrink-0 text-right text-[13px] text-(--foreground)`}>税收减免：</span>
                        <span className="flex flex-wrap items-center gap-4">
                            <CheckBox label="残疾" checked={draft.isDisabled} onChange={(c) => set({ isDisabled: c })} />
                            <CheckBox label="烈属" checked={draft.isMartyrFamily} onChange={(c) => set({ isMartyrFamily: c })} />
                            <CheckBox label="孤老" checked={draft.isSoloElder} onChange={(c) => set({ isSoloElder: c })} />
                        </span>
                    </div>
                </div>
            </Section>

            <Section title="扣除款项">
                <div className="flex items-center gap-3">
                    <span className={`${FORM_LABEL_W} shrink-0 text-right text-[13px] text-(--foreground)`}>
                        <span className="text-(--danger)">*</span>是否缴纳五险一金：
                    </span>
                    <Radio.Group
                        value={draft.paysSocialInsurance ? "1" : "2"}
                        onChange={(e) => set({ paysSocialInsurance: e.target.value === "1" })}
                    >
                        <Radio value="1">是</Radio>
                        <Radio value="2">否</Radio>
                    </Radio.Group>
                </div>
                {draft.paysSocialInsurance ? (
                    insurance.length > 0 ? (
                        <div className="mt-3">
                            <Table cols={insuranceCols} rows={insuranceRows} empty={<EmptyTip />} />
                            <div className="mt-2 text-[12.5px] text-(--muted-foreground)">
                                缴纳基数与比例留空时沿用「五险一金设置」的默认值，填写后按个人值计算。
                            </div>
                        </div>
                    ) : (
                        <HintBox className="mt-3">尚未配置五险一金项目，请先到「五险一金设置」新增。</HintBox>
                    )
                ) : null}
            </Section>

            {error && <Alert type="error" showIcon title={error} />}

            <div className="flex justify-center gap-3 pb-2">
                <Btn variant="primary" disabled={busy} onClick={() => void save()}>
                    {busy ? "保存中…" : "保存"}
                </Btn>
                <Btn onClick={onClose}>取消</Btn>
            </div>
        </>
    );
}

export function SalaryManage() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const { period, stepper } = usePeriodStepper();
    const [bills, setBills] = useState<SalaryBillDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [tab, setTab] = useState(0);
    const [toast, setToast] = useState<string | null>(null);
    const [importResult, setImportResult] = useState<SalaryBillImportResultDto | null>(null);
    const fileRef = useRef<HTMLInputElement | null>(null);
    /** 显示所有信息：补出计提凭证与发放凭证两列。 */
    const [showAll, setShowAll] = useState(false);

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

    const salaryBase = `/api/accountsets/${accountSetId}/salary`;

    /** 生成工资条 PDF：当期全部员工一人一条，一页 3 条并带裁切线。 */
    const buildPayslips = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(`${salaryBase}/payslips/pdf?period=${period.period}`, token, `工资条_${period.period}.pdf`);
        setToast(err ?? "工资条已生成，正在下载…");
    };

    /** 打印当期工资表：后端渲染 PDF（表头含账套名与期间），前端只负责新窗口预览。 */
    const printBills = async () => {
        if (!token || !accountSetId) {
            return;
        }
        try {
            await printPdf(`${salaryBase}/bills/pdf?period=${period.period}`, token);
        } catch (e) {
            setToast(e instanceof Error ? e.message : "生成打印稿失败，请重试。");
        }
    };

    /** 导出当期工资表（一行一员工，末行合计）。 */
    const exportBills = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(`${salaryBase}/bills/export?period=${period.period}`, token, `工资表_${period.period}.xlsx`);
        if (err) {
            setToast(err);
        }
    };

    /** 下载工资表导入模板（与导出同列，导出文件可直接改完再导入）。 */
    const downloadBillTemplate = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(`${salaryBase}/bills/export?period=${period.period}`, token, "工资表导入模板.xlsx");
        if (err) {
            setToast(err);
        }
    };

    /** 导入工资表：按编号匹配员工，同期间已有则更新；已生成凭证的行会跳过并说明原因。 */
    const importBills = async (file: File) => {
        if (!token || !accountSetId) {
            return;
        }
        const { data, error } = await uploadFile<SalaryBillImportResultDto>(
            `${salaryBase}/bills/import?period=${period.period}`,
            token,
            file,
        );
        if (error) {
            setToast(error);
            return;
        }
        setImportResult(data ?? null);
        await reload();
    };

    if (tab === 1)
        return (
            <Page>
                <SubTabs tabs={["工资列表", "计提工资、发放工资"]} active={1} onChange={setTab} />
                <SalaryAccrueView />
            </Page>
        );
    const cols: Col[] = [
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
        ...(showAll
            ? ([
                  { key: "accruedVoucher", title: "计提凭证", width: "160px" },
                  { key: "paidVoucher", title: "发放凭证", width: "160px" },
              ] as Col[])
            : []),
    ];
    const list = bills;
    const sumOf = (pick: (b: SalaryBillDto) => number | null | undefined) =>
        list.reduce((s2, b) => s2 + (pick(b) ?? 0), 0);
    const totals = {
        gross: sumOf((b) => b.gross),
        deductible: sumOf((b) => b.deductible),
        payable: sumOf((b) => b.payable),
        personal: sumOf((b) => b.personalDeduction),
        threshold: 5000,
        tax: sumOf((b) => b.tax),
        adjust: sumOf((b) => b.adjust),
        net: sumOf((b) => b.net),
        company: sumOf((b) => b.companyPart),
        cost: sumOf((b) => b.cost),
    };
    const rows: Row[] = [
        ...list.map((b) => ({
            no: b.employeeCode,
            name: b.employeeName,
            dept: b.dept,
            gross: nf(b.gross),
            deduct: nf(b.deductible),
            payable: nf(b.payable),
            personal: nf(b.personalDeduction),
            threshold: String(b.taxThreshold ?? 0),
            tax: nf(b.tax),
            adjust: nf(b.adjust),
            net: nf(b.net),
            company: nf(b.companyPart),
            cost: nf(b.cost),
            accruedVoucher: b.accruedVoucherText ?? "",
            paidVoucher: b.paidVoucherText ?? "",
        })),
        // 空期间不渲染全 0 的合计行，空态交给 Table 的 empty。
        ...(list.length
            ? [
                  {
                      dept: "合计",
                      gross: nf(totals.gross),
                      deduct: nf(totals.deductible),
                      payable: nf(totals.payable),
                      personal: nf(totals.personal),
                      threshold: String(totals.threshold),
                      tax: nf(totals.tax),
                      adjust: nf(totals.adjust),
                      net: nf(totals.net),
                      company: nf(totals.company),
                      cost: nf(totals.cost),
                      __style: { background: "var(--table-zebra-bg)", fontWeight: 500 },
                  },
              ]
            : []),
    ];
    return (
        <Page>
            <SubTabs tabs={["工资列表", "计提工资、发放工资"]} active={tab} onChange={setTab} />
            <ToolRow
                left={
                    <>
                        <PeriodStepper {...stepper} />
                        <WipBtn title="自定义工资项目需支持动态列与公式，暂未实现">自定义工资项目</WipBtn>
                    </>
                }
                right={
                    <>
                        <CheckBox label="显示所有信息" checked={showAll} onChange={setShowAll} />
                        <span title="多次发放工资需拆分工资单，暂未实现，仅作展示">
                            <CheckBox label="多次发放工资" />
                        </span>
                        <Btn dropdown onClick={() => void buildPayslips()} title="按当期工资单生成工资条 PDF（一页 3 条，带裁切线）">
                            生成工资表
                        </Btn>
                        <span className="inline-flex items-center gap-1">
                            <WipBtn title="同步个税申报表需对接电子税务局，暂未实现">同步个税申报表</WipBtn>
                            <HelpTip
                                className="text-[14px]"
                                title="按当期工资表以累计预扣法计算个税，并推送到电子税务局的个税申报表；需要税局登录态与申报接口，当前尚未接入。"
                            />
                        </span>
                        <Btn dropdown onClick={() => fileRef.current?.click()} title="从 .xlsx 导入工资表（按员工编号匹配）">
                            导入
                        </Btn>
                        <WipBtn title="发送工资条需短信 / 邮件通道，暂未实现">发送工资条</WipBtn>
                        <WipBtn title="个税申报需对接电子税务局，暂未实现">个税申报</WipBtn>
                        <PrintBtn title="打印当期工资表（后端生成 PDF）" onClick={() => void printBills()}>打印</PrintBtn>
                        <WipBtn title="工资表附件归档暂未实现">附件</WipBtn>
                        <Dropdown
                            trigger={["click"]}
                            menu={{
                                items: [
                                    { key: "export", label: "导出工资表" },
                                    { key: "template", label: "下载导入模板" },
                                ],
                                onClick: ({ key }) => void (key === "export" ? exportBills() : downloadBillTemplate()),
                            }}
                        >
                            <span>
                                <Btn dropdown title="导出工资表 / 下载导入模板">
                                    更多操作
                                </Btn>
                            </span>
                        </Dropdown>
                        <span className="cursor-pointer" onClick={() => void reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <StateBar loading={loading} error={error} />
            {list.length > 0 && (
                <div className="mx-4 mb-3 flex flex-wrap items-center gap-x-8 gap-y-2 rounded-lg bg-(--table-zebra-bg) px-4 py-2.5 text-[13px]">
                    <span className="text-(--muted-foreground)">
                        员工 <span className="ml-1 font-medium tabular-nums text-(--foreground)">{list.length}</span> 人
                    </span>
                    <span className="text-(--muted-foreground)">
                        应发合计 <span className="ml-1 font-medium tabular-nums text-(--foreground)">¥{nf(totals.payable)}</span>
                    </span>
                    <span className="text-(--muted-foreground)">
                        实发合计 <span className="ml-1 text-[14px] font-semibold tabular-nums text-(--foreground)">¥{nf(totals.net)}</span>
                    </span>
                    <span className="text-(--muted-foreground)">
                        本月个税 <span className="ml-1 font-medium tabular-nums text-(--foreground)">¥{nf(totals.tax)}</span>
                    </span>
                    <span className="text-(--muted-foreground)">
                        员工成本 <span className="ml-1 font-medium tabular-nums text-(--foreground)">¥{nf(totals.cost)}</span>
                    </span>
                </div>
            )}
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} empty={<EmptyTip />} />
            </div>
            <Pager total={list.length} />
            <input
                ref={fileRef}
                type="file"
                accept=".xlsx"
                className="hidden"
                onChange={(e) => {
                    const file = e.target.files?.[0];
                    e.target.value = "";
                    if (file) {
                        void importBills(file);
                    }
                }}
            />
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            {importResult && (
                <Dialog
                    title="导入结果"
                    width={520}
                    onClose={() => setImportResult(null)}
                    footer={
                        <div className="flex justify-end">
                            <Btn onClick={() => setImportResult(null)}>关闭</Btn>
                        </div>
                    }
                >
                    <div className="py-2 text-[13px] text-(--foreground)">
                        新增 <b>{importResult.imported}</b> 条，更新 <b>{importResult.updated}</b> 条
                        {(importResult.failures ?? []).length > 0 && `，失败 ${(importResult.failures ?? []).length} 条`}
                    </div>
                    {(importResult.failures ?? []).length > 0 && (
                        <div className="max-h-[240px] overflow-auto rounded border border-(--border)">
                            {(importResult.failures ?? []).map((f, i) => (
                                <div key={i} className="flex gap-2 border-b border-(--border) px-2 py-1 text-[12.5px] last:border-0">
                                    <span className="w-[52px] shrink-0 text-(--muted-foreground)">第 {f.row} 行</span>
                                    <span className="text-(--danger)">{f.reason}</span>
                                </div>
                            ))}
                        </div>
                    )}
                </Dialog>
            )}
        </Page>
    );
}

/* ---------- 员工信息 ---------- */

export function SalaryEmployee() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const data = useEmployeeAndInsurance();
    const accounts = useLeafAccounts();
    /** 抽屉目标："new" 新增、员工对象编辑、null 关闭。 */
    const [drawer, setDrawer] = useState<EmployeeDto | null | "new">(null);
    const [deleting, setDeleting] = useState<EmployeeDto | null>(null);
    const [busy, setBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [importResult, setImportResult] = useState<EmployeeImportResultDto | null>(null);
    /** 税局人员同步：取数在服务端的无头浏览器里跑，按分钟计；未登录时弹个税登录窗。 */
    const [syncing, setSyncing] = useState(false);
    const [loginOpen, setLoginOpen] = useState(false);
    const empFileRef = useRef<HTMLInputElement | null>(null);
    /** 列表筛选：部门为空表示不限，status 为空表示不限，active 在职，disabled 已停用。 */
    const [deptFilter, setDeptFilter] = useState("");
    const [statusFilter, setStatusFilter] = useState<"" | "active" | "disabled">("");
    /** 显示所有信息：展开性别、出生日期、用工类型、开户行与银行账号等档案字段。 */
    const [showAll, setShowAll] = useState(false);

    const empBase = `/api/accountsets/${accountSetId}/salary`;

    /** 导出员工名册（列与导入模板一致）。 */
    const exportEmployees = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(`${empBase}/employees/export`, token, "员工信息.xlsx");
        if (err) {
            setToast(err);
        }
    };

    /** 下载员工导入模板。 */
    const downloadTemplate = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(`${empBase}/employees/import-template`, token, "员工导入模板.xlsx");
        if (err) {
            setToast(err);
        }
    };

    /** 导入员工：按编号匹配，存在则更新、不存在则新增。 */
    const importEmployees = async (file: File) => {
        if (!token || !accountSetId) {
            return;
        }
        const { data: result, error } = await uploadFile<EmployeeImportResultDto>(`${empBase}/employees/import`, token, file);
        if (error) {
            setToast(error);
            return;
        }
        setImportResult(result ?? null);
        await data.reload();
    };

    /** 同步税局人员信息：取回个税「人员信息采集」名单并写进员工档案，税局没给的字段用档案默认值。 */
    const syncFromIit = async () => {
        if (!token || !accountSetId || syncing) {
            return;
        }
        setSyncing(true);
        try {
            const outcome = await syncIitPersonnel(accountSetId, token);
            if (outcome.needLogin) {
                setLoginOpen(true);
                return;
            }
            if (!outcome.sync) {
                setToast(outcome.message ?? "未从个税系统取回人员信息。");
                return;
            }
            const { imported, updated, skipped } = outcome.sync;
            setToast(
                `已同步个税人员 ${outcome.rows.length} 人：新增 ${imported} 人、更新 ${updated} 人` +
                    (skipped ? `，${skipped} 条缺姓名未登记` : ""),
            );
            await data.reload();
        } catch (e) {
            setToast(e instanceof Error ? e.message : "同步税局人员信息失败");
        } finally {
            setSyncing(false);
        }
    };

    /** 批量删除员工：已有工资单的员工由后端跳过并给出原因。 */
    const bulkDelete = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (selected.size === 0) {
            setToast("请先勾选要删除的员工");
            return;
        }
        setBusy(true);
        try {
            const res = await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryEmployeesBulkDeletePost({
                accountSetId,
                salaryBulkIdsRequest: { ids: [...selected] },
            });
            setSelected(new Set());
            const messages = res.messages ?? [];
            const extra = messages.length > 0 ? `；${messages.slice(0, 3).join("，")}` : "";
            setToast(`已删除 ${res.affected} 人${(res.skipped ?? 0) > 0 ? `，跳过 ${res.skipped} 人` : ""}${extra}`);
            await data.reload();
        } catch (e) {
            setToast(await apiErrorMessage(e, "批量删除失败"));
        } finally {
            setBusy(false);
        }
    };

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

    const depts = [...new Set(data.employees.map((e) => e.dept ?? "").filter(Boolean))];
    const employees = data.employees.filter(
        (e) =>
            (!deptFilter || e.dept === deptFilter) &&
            (!statusFilter || (statusFilter === "disabled" ? e.isDisabled : !e.isDisabled)),
    );
    const pageIds = employees.map((e) => e.id ?? "").filter(Boolean);
    const allChecked = pageIds.length > 0 && pageIds.every((id) => selected.has(id));
    const toggleOne = (id: string, checked: boolean) =>
        setSelected((prev) => {
            const next = new Set(prev);
            if (checked) {
                next.add(id);
            } else {
                next.delete(id);
            }
            return next;
        });
    const toggleAll = (checked: boolean) => setSelected(checked ? new Set(pageIds) : new Set());

    const cols: Col[] = [
        {
            key: "ck",
            title: <CheckBox label="" checked={allChecked} onChange={toggleAll} />,
            width: "36px",
        },
        { key: "no", title: "编号", width: "70px" },
        { key: "name", title: "姓名", width: "100px" },
        { key: "dept", title: "部门", width: "110px" },
        { key: "phone", title: "手机", width: "140px" },
        { key: "email", title: "邮箱", width: "140px" },
        { key: "idNo", title: "证件号码", width: "180px" },
        ...(showAll
            ? ([
                  { key: "gender", title: "性别", width: "70px" },
                  { key: "birth", title: "出生日期", width: "110px" },
                  { key: "employment", title: "用工类型", width: "110px" },
                  { key: "hireDate", title: "入职日期", width: "110px" },
                  { key: "bank", title: "开户行", width: "160px" },
                  { key: "bankAccount", title: "银行账号", width: "180px" },
                  { key: "remark", title: "备注", width: "160px" },
              ] as Col[])
            : []),
        { key: "subject", title: "计提工资科目", width: "240px" },
        { key: "status", title: "员工状态", width: "90px" },
        { key: "ops", title: "操作", width: "110px" },
    ];
    const rows: Row[] = employees.map((e) => ({
        ck: <CheckBox label="" checked={selected.has(e.id ?? "")} onChange={(c) => toggleOne(e.id ?? "", c)} />,
        no: e.code,
        name: e.name,
        dept: e.dept,
        phone: e.phone,
        email: e.email,
        idNo: e.idNo,
        gender: e.genderText,
        birth: toDateInput(e.birthDate),
        employment: e.employmentTypeText,
        hireDate: toDateInput(e.hireDate),
        bank: e.bankName ?? "",
        bankAccount: e.bankAccountNo ?? "",
        remark: e.remark ?? "",
        subject: e.subjectText,
        status: e.statusText,
        ops: (
            <span className="flex gap-2">
                <LinkAction onClick={() => setDrawer(e)}>编辑</LinkAction>
                <LinkAction danger onClick={() => setDeleting(e)}>删除</LinkAction>
            </span>
        ),
    }));
    return (
        <Page>
            <ToolRow
                left={
                    <Dropdown
                        trigger={["click"]}
                        menu={{
                            items: [
                                { key: "all", label: "所有员工" },
                                ...depts.map((d) => ({ key: `dept:${d}`, label: `部门：${d}` })),
                                { type: "divider" as const },
                                { key: "active", label: "在职员工" },
                                { key: "disabled", label: "已停用员工" },
                            ],
                            onClick: ({ key }) => {
                                if (key === "all") {
                                    setDeptFilter("");
                                    setStatusFilter("");
                                } else if (key === "active" || key === "disabled") {
                                    setDeptFilter("");
                                    setStatusFilter(key);
                                } else if (key.startsWith("dept:")) {
                                    setDeptFilter(key.slice(5));
                                    setStatusFilter("");
                                }
                            },
                        }}
                    >
                        <span>
                            <Btn dropdown>
                                {deptFilter || (statusFilter === "active" ? "在职员工" : statusFilter === "disabled" ? "已停用员工" : "所有员工")}
                            </Btn>
                        </span>
                    </Dropdown>
                }
                right={
                    <>
                        <CheckBox label="显示所有信息" checked={showAll} onChange={setShowAll} />
                        <Btn variant="primary" onClick={() => setDrawer("new")}>增加员工</Btn>
                        <Btn
                            onClick={() => void syncFromIit()}
                            disabled={syncing}
                            title="从自然人电子税务局取回「人员信息采集」名单并按证件号/姓名写入员工档案"
                        >
                            {syncing ? "同步中…" : "同步税局人员信息"}
                        </Btn>
                        <Btn dropdown onClick={() => empFileRef.current?.click()} title="从 .xlsx 导入员工（按编号匹配，存在则更新）">
                            导入
                        </Btn>
                        <ExportBtn onClick={() => void exportEmployees()} title="导出员工名册为 .xlsx">
                            导出
                        </ExportBtn>
                        <Dropdown
                            trigger={["click"]}
                            menu={{
                                items: [
                                    { key: "delete", label: `批量删除（已选 ${selected.size} 人）`, disabled: selected.size === 0 },
                                    { key: "template", label: "下载导入模板" },
                                ],
                                onClick: ({ key }) => void (key === "delete" ? bulkDelete() : downloadTemplate()),
                            }}
                        >
                            <span>
                                <Btn dropdown title="批量删除 / 下载导入模板">
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
            <StateBar loading={data.loading} error={data.error} />
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} empty={<EmptyTip />} />
            </div>
            <Pager total={data.employees.length} />
            <EmployeeDrawer
                open={drawer !== null}
                employee={drawer && drawer !== "new" ? drawer : null}
                insurance={data.insurance}
                accounts={accounts}
                employees={data.employees}
                onClose={() => setDrawer(null)}
                onSaved={(text) => {
                    setDrawer(null);
                    setToast(text);
                    void data.reload();
                }}
            />
            {deleting && (
                <ConfirmDialog
                    title="删除员工"
                    text={`确定删除员工「${deleting.name ?? deleting.code ?? ""}」吗？此操作不可恢复。`}
                    busy={busy}
                    onClose={() => setDeleting(null)}
                    onConfirm={() => void remove()}
                />
            )}
            <IitLoginModal
                open={loginOpen}
                accountSetId={accountSetId}
                onClose={() => setLoginOpen(false)}
                onSuccess={() => {
                    setLoginOpen(false);
                    // 登录成功后接着把刚才没跑成的同步补上，省得用户再点一次。
                    void syncFromIit();
                }}
            />
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
            <input
                ref={empFileRef}
                type="file"
                accept=".xlsx"
                className="hidden"
                onChange={(e) => {
                    const file = e.target.files?.[0];
                    e.target.value = "";
                    if (file) {
                        void importEmployees(file);
                    }
                }}
            />
            {importResult && (
                <Dialog
                    title="导入结果"
                    width={520}
                    onClose={() => setImportResult(null)}
                    footer={
                        <div className="flex justify-end">
                            <Btn onClick={() => setImportResult(null)}>关闭</Btn>
                        </div>
                    }
                >
                    <div className="py-2 text-[13px] text-(--foreground)">
                        新增 <b>{importResult.imported}</b> 人，更新 <b>{importResult.updated}</b> 人
                        {(importResult.failures ?? []).length > 0 && `，失败 ${(importResult.failures ?? []).length} 行`}
                    </div>
                    {(importResult.failures ?? []).length > 0 && (
                        <div className="max-h-[240px] overflow-auto rounded border border-(--border)">
                            {(importResult.failures ?? []).map((f, i) => (
                                <div key={i} className="flex gap-2 border-b border-(--border) px-2 py-1 text-[12.5px] last:border-0">
                                    <span className="w-[52px] shrink-0 text-(--muted-foreground)">第 {f.row} 行</span>
                                    <span className="text-(--danger)">{f.reason}</span>
                                </div>
                            ))}
                        </div>
                    )}
                </Dialog>
            )}
        </Page>
    );
}

/* ---------- 工资统计表 ---------- */

export function SalarySummary() {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const { period, stepper } = usePeriodStepper();

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

    const rangeText = `${periodFromNumber(startPeriod).year}年1月—${periodLabel(period)}`;

    /** 导出工资统计表（与当前区间一致）。 */
    const [toast, setToast] = useState<string | null>(null);
    const exportSummary = async () => {
        if (!token || !accountSetId) {
            return;
        }
        const err = await downloadBlob(
            `/api/accountsets/${accountSetId}/salary/summary/export?startPeriod=${startPeriod}&endPeriod=${endPeriod}`,
            token,
            `工资统计表_${startPeriod}-${endPeriod}.xlsx`,
        );
        if (err) {
            setToast(err);
        }
    };

    /** 打印工资统计表（年初至所选期间）：与导出同区间，版式在后端。 */
    const printSummary = async () => {
        if (!token || !accountSetId) {
            return;
        }
        try {
            await printPdf(
                `/api/accountsets/${accountSetId}/salary/summary/pdf?startPeriod=${startPeriod}&endPeriod=${endPeriod}`,
                token,
            );
        } catch (e) {
            setToast(e instanceof Error ? e.message : "生成打印稿失败，请重试。");
        }
    };
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
            __style: { background: "var(--table-zebra-bg)", fontWeight: 500 },
        },
    ];
    return (
        <Page>
            <ToolRow
                left={
                    <PeriodStepper {...stepper} text={rangeText} />
                }
                right={
                    <>
                        <span title="展开员工明细行暂未实现，仅作展示">
                            <CheckBox label="显示员工工资明细" />
                        </span>
                        <PrintBtn onClick={() => void printSummary()} title="打印当前区间的工资统计表（后端生成 PDF）">打印</PrintBtn>
                        <ExportBtn onClick={() => void exportSummary()} title="导出当前区间的工资统计表为 .xlsx">导出</ExportBtn>
                        <span className="cursor-pointer" onClick={() => void reload()} title="刷新">
                            <RefreshIcon />
                        </span>
                    </>
                }
            />
            <StateBar loading={loading} error={error} />
            <div className="flex-1 px-4">
                <Table cols={cols} rows={rows} empty={<EmptyTip />} />
            </div>
            <Pager total={rows2.length} />
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
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
    const [baseAmount, setBaseAmount] = useState(item?.baseAmount ? String(item.baseAmount) : "");
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
            baseAmount: Number(baseAmount) || 0,
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
                <div className="flex justify-end gap-3">
                    <Btn variant="primary" disabled={busy} onClick={() => void save()}>
                        保存
                    </Btn>
                    <Btn onClick={onClose}>取消</Btn>
                </div>
            }
        >
            <div className="flex flex-col gap-3">
                <Field label="缴纳项目" required>
                    <TextInput className={FORM_CONTROL_W} value={name} onChange={setName} />
                </Field>
                <Field label="缴纳基数">
                    <TextInput className={FORM_CONTROL_W} type="number" value={baseAmount} onChange={setBaseAmount} placeholder="留空表示不设默认基数" />
                </Field>
                <Field label="公司缴纳比例(%)">
                    <TextInput className={FORM_CONTROL_W} type="number" value={companyRate} onChange={setCompanyRate} />
                </Field>
                <Field label="个人缴纳比例(%)">
                    <TextInput className={FORM_CONTROL_W} type="number" value={personalRate} onChange={setPersonalRate} />
                </Field>
                <p className="text-[12.5px] text-(--muted-foreground)">员工档案未单独填写缴纳基数时，按这里的默认基数计算。</p>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Dialog>
    );
}

export function SalaryInsurance() {
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
        { key: "baseAmount", title: "缴纳基数", width: "140px", align: "right" },
        { key: "companyRate", title: "公司缴纳比例", width: "140px" },
        { key: "personalRate", title: "个人缴纳比例", width: "140px" },
        { key: "companyAmt", title: "公司缴纳金额", width: "140px", render: () => "--" },
        { key: "personalAmt", title: "个人缴纳金额", width: "140px", render: () => "--" },
        {
            key: "ops",
            title: "操作",
            render: (row: Row) => (
                <span className="flex gap-2">
                    <LinkAction onClick={() => setEditing((row.__item as InsuranceItemDto) ?? null)}>编辑</LinkAction>
                    <LinkAction danger onClick={() => setDeleting((row.__item as InsuranceItemDto) ?? null)}>删除</LinkAction>
                </span>
            ),
        },
    ];
    return (
        <Page>
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
            <div className="flex-1 px-4">
                <Table
                    cols={cols}
                    rows={data.insurance.map((i) => ({
                        __item: i,
                        name: i.name,
                        mode: i.modeText ?? "按比例缴纳",
                        baseAmount: i.baseAmount && i.baseAmount > 0 ? nf(i.baseAmount) : "未设置",
                        companyRate: `${i.companyRate}%`,
                        personalRate: i.personalRate == null ? "" : `${i.personalRate}%`,
                    }))}
                    empty={<EmptyTip />}
                />
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
