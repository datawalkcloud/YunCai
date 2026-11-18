/**
 * 个税通道：自然人电子税务局（etax.chinatax.gov.cn）的服务端登录与取数。
 *
 * 与企业电子税务局是两套站点、两套账号、两份登录态：这里登录的是办税人员的实名账号，
 * 人员信息与专项附加扣除都在这里取。鉴权、超时与错误口径全部复用 tax-portal 的请求助手。
 *
 * 取数一律走「提交任务 → 轮询」：税局页面由服务器的无头浏览器一步步点，耗时不可控，
 * 所以接口只负责把任务派出去，进展同时登记在任务中心，页面轮询到结束才拿到数据。
 */
import {
    START_TIMEOUT_MS,
    STATUS_TIMEOUT_MS,
    VERIFY_TIMEOUT_MS,
    taxPortalRequest,
    type TaxPortalLoginResponse,
} from "@/lib/tax-portal";

/** 个税登录方式：实名账号密码，或用个人所得税 APP 扫码。 */
export type TaxIitLoginMode = "password" | "scan";

/** 个税「人员信息采集」的一行，字段口径与税局页面一致。 */
export interface TaxIitPersonnelRow {
    key: string;
    code: string;
    name: string;
    /** 证件类型，如「居民身份证」。 */
    idTypeText: string;
    idNo: string;
    phone: string;
    genderText: string;
    /** 任职受雇从业类型：雇员 / 保险营销员 / 证券经纪人 / 实习学生 / 其他。 */
    employTypeText: string;
    /** 人员状态：正常 / 非正常。 */
    statusText: string;
    hireDate: string;
    leaveDate: string;
    dept: string;
    /** 报送状态：报送成功 / 报送失败 / 待报送 / 报送中。 */
    reportStateText: string;
    /** 身份验证状态：验证通过 / 验证不通过 / 待验证 / 验证中 / 暂不校验。 */
    verifyStateText: string;
    /** 是否已采集专项附加扣除（「是」/「否」，取不到为空）。 */
    deductionText: string;
    /** 该行的原始 JSON 片段，排查用。 */
    raw: string;
}

/** 个税「专项附加扣除信息采集」的一条扣除，行粒度是「人员 × 扣除项目 × 有效期」。 */
export interface TaxIitDeductionRow {
    key: string;
    name: string;
    idTypeText: string;
    idNo: string;
    /** 子女教育 / 继续教育 / 大病医疗 / 住房贷款利息 / 住房租金 / 赡养老人 / 3岁以下婴幼儿照护。 */
    projectText: string;
    /** 扣除对象或关系，如「子女」「本人」「配偶父母」。 */
    relativeText: string;
    yearText: string;
    periodStart: string;
    periodEnd: string;
    /** 每月扣除金额（元）。 */
    monthlyAmount: number;
    /** 通过扣缴义务人申报 / 综合所得年度自行申报。 */
    declareWayText: string;
    verifyStateText: string;
    reportStateText: string;
    raw: string;
}

/** 个税「查询统计 → 申报记录查询」的一条申报记录：一行 = 某期报的一张表。 */
export interface TaxIitFilingRow {
    key: string;
    /** 税款所属期，如「2026-09」。 */
    period: string;
    /** 申报表名，如「综合所得预扣预缴表」。 */
    formName: string;
    headcount: string;
    /** 当期收入（元），税局未申报的格子回「-」，这里原样展示。 */
    income: string;
    /** 应补（退）税额（元）。 */
    taxDue: string;
    /** 正常申报 / 逾期申报 / 补充申报等。 */
    declareType: string;
    /** 申报渠道：客户端 / WEB端 等。 */
    channel: string;
    declareTime: string;
    /** 该行的原始 JSON 片段，排查用。 */
    raw: string;
}

export interface TaxIitFilingResult {
    rows: TaxIitFilingRow[];
    message?: string | null;
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

/**
 * 个税「查询统计 → 缴税记录查询」的一条缴税记录：扣缴端按人按次的缴款流水，
 * 与企业端「缴款信息查询」（数字账户 DescribeJkxxcx）不是同一份数据。
 */
export interface TaxIitPaymentRow {
    key: string;
    /** 税款所属期，如「2026-09」。 */
    period: string;
    /** 报表类型，如「综合所得预扣预缴表」。 */
    formName: string;
    /** 电子税票号。 */
    eTicketNo: string;
    /** 缴税金额（元）。金额按税局展示的文本原样回传。 */
    amount: string;
    paidOn: string;
    /** 缴税种类：正税 / 滞纳金 / 罚款等。 */
    taxKind: string;
    /** 缴税方式，如「三方协议」。 */
    method: string;
    /** 该行的原始 JSON 片段，排查用。 */
    raw: string;
}

export interface TaxIitPaymentResult {
    rows: TaxIitPaymentRow[];
    message?: string | null;
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

/** 缴税记录的筛选列：按缴税日期或按税款所属期。 */
export type TaxIitPaymentDateField = "paidOn" | "period";

export interface TaxIitPersonnelResult {
    rows: TaxIitPersonnelRow[];
    message?: string | null;
    /** 个税登录态缺失或已过期：页面应弹出「个税申报登录」补一次登录。 */
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

export interface TaxIitDeductionResult {
    rows: TaxIitDeductionRow[];
    message?: string | null;
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

/**
 * 发起个税登录。账号密码取自账套保存的税局资料，未填时后端直接返回 400 提示先补全；
 * 扫码方式不需要账号密码。返回时会话已在服务器后台推进，需要轮询状态。
 */
export function startIitLogin(
    accountSetId: string,
    token: string | null,
    mode: TaxIitLoginMode,
): Promise<TaxPortalLoginResponse> {
    return taxPortalRequest<TaxPortalLoginResponse>(
        `/${encodeURIComponent(accountSetId)}/iit/login`,
        token,
        START_TIMEOUT_MS,
        { method: "POST", body: JSON.stringify({ mode }) },
    );
}

/** 轮询个税登录进展：等短信、等扫码、成功或失败。 */
export function getIitLoginStatus(accountSetId: string, token: string | null): Promise<TaxPortalLoginResponse> {
    return taxPortalRequest<TaxPortalLoginResponse>(
        `/${encodeURIComponent(accountSetId)}/iit/login/status`,
        token,
        STATUS_TIMEOUT_MS,
        { method: "GET" },
    );
}

/** 把个税短信验证码交给服务器提交。 */
export function verifyIitLogin(
    accountSetId: string,
    token: string | null,
    code: string,
): Promise<TaxPortalLoginResponse> {
    return taxPortalRequest<TaxPortalLoginResponse>(
        `/${encodeURIComponent(accountSetId)}/iit/login/verify`,
        token,
        VERIFY_TIMEOUT_MS,
        { method: "POST", body: JSON.stringify({ code }) },
    );
}

/** 退出个税登录，丢弃服务器上的个税登录态。 */
export function logoutIit(accountSetId: string, token: string | null): Promise<void> {
    return taxPortalRequest<void>(
        `/${encodeURIComponent(accountSetId)}/iit/logout`,
        token,
        STATUS_TIMEOUT_MS,
        { method: "POST" },
    );
}

/** 后台取数任务的一轮进展：running 时 data 还是空，结束后 data 就是该接口原本的同步结果。 */
export interface TaxIitJob {
    key: string;
    /** 任务说明，与任务中心那一行一致。 */
    title: string;
    status: "running" | "success" | "failed";
    needLogin?: boolean;
    message?: string | null;
    data?: unknown;
    submittedAt: string;
    finishedAt?: string | null;
}

const JOB_POLL_INTERVAL_MS = 2000;
/** 后端给单次取数三分钟上限，页面再多等半分钟兜底。 */
const JOB_MAX_WAIT_MS = 210_000;

/** 提交一次个税取数并轮询到任务结束；任务在服务端后台执行，进展与结果都登记在任务中心。 */
async function submitIitJob(
    accountSetId: string,
    token: string | null,
    submitPath: string,
): Promise<TaxIitJob> {
    let job = await taxPortalRequest<TaxIitJob>(
        `/${encodeURIComponent(accountSetId)}${submitPath}`,
        token,
        START_TIMEOUT_MS,
        { method: "POST" },
    );

    const deadline = Date.now() + JOB_MAX_WAIT_MS;
    while (job.status === "running") {
        if (Date.now() > deadline) {
            throw new Error(`「${job.title}」还没跑完，请稍后到任务中心查看结果。`);
        }
        await new Promise((resolve) => setTimeout(resolve, JOB_POLL_INTERVAL_MS));
        job = await taxPortalRequest<TaxIitJob>(
            `/${encodeURIComponent(accountSetId)}/iit/jobs/${encodeURIComponent(job.key)}`,
            token,
            STATUS_TIMEOUT_MS,
            { method: "GET" },
        );
    }

    return job;
}

/** 提交并等待个税「人员信息采集」取数任务。 */
export async function fetchIitPersonnel(
    accountSetId: string,
    token: string | null,
): Promise<TaxIitPersonnelResult> {
    const job = await submitIitJob(accountSetId, token, "/iit/personnel");
    // 任务办砸时后端只给文案，按「没有行」的口径回，页面照旧提示并弹登录框。
    return (job.data as TaxIitPersonnelResult | undefined) ?? {
        rows: [],
        needLogin: job.needLogin ?? false,
        message: job.message ?? "个税人员信息取数未完成，请稍后重试。",
    };
}

/** 人员名单落库结果，与后端 EmployeeImportResultDto 同口径。 */
export interface TaxIitSyncResult {
    /** 新建的员工数。 */
    imported: number;
    /** 更新的员工数。 */
    updated: number;
    /** 没处理的行数（缺姓名等）。 */
    skipped: number;
    failures?: { row: number; reason: string }[] | null;
}

/** 「同步税局人员信息」跑完一趟的结果：先取数，取到才落库。 */
export interface IitPersonnelSyncOutcome {
    rows: TaxIitPersonnelRow[];
    /** 个税登录态缺失或已过期，页面要弹「个税申报登录」。 */
    needLogin: boolean;
    message?: string | null;
    /** 落库统计；没取到数据或需要登录时为 null。 */
    sync: TaxIitSyncResult | null;
}

/**
 * 把税局人员名单写进工资模块的员工档案。
 *
 * 后端按证件号码匹配，脱敏号（3412************97）对不上时退到姓名：命中只覆盖税局给得到的列，
 * 没命中则新增一个员工，税局没有的字段一律用员工档案的默认值。
 * 这个接口挂在工资模块下（写的是 employees 表），所以没走税局通道的请求助手。
 */
export async function saveIitPersonnel(
    accountSetId: string,
    token: string | null,
    rows: TaxIitPersonnelRow[],
): Promise<TaxIitSyncResult> {
    const response = await fetch(
        `/api/accountsets/${encodeURIComponent(accountSetId)}/salary/employees/sync-from-iit`,
        {
            method: "POST",
            cache: "no-store",
            headers: {
                "Content-Type": "application/json",
                ...(token ? { Authorization: `Bearer ${token}` } : {}),
            },
            body: JSON.stringify({
                rows: rows.map((row) => ({
                    name: row.name,
                    code: row.code,
                    idTypeText: row.idTypeText,
                    idNo: row.idNo,
                    genderText: row.genderText,
                    phone: row.phone,
                    dept: row.dept,
                    employTypeText: row.employTypeText,
                    statusText: row.statusText,
                    hireDate: row.hireDate,
                    leaveDate: row.leaveDate,
                })),
            }),
        },
    );
    if (!response.ok) {
        const data = (await response.json().catch(() => null)) as { message?: string } | null;
        throw new Error(data?.message ?? "人员信息同步失败，请稍后重试。");
    }
    return (await response.json()) as TaxIitSyncResult;
}

/** 取回个税人员名单并落库到员工档案；未登录或没有数据时不落库。 */
export async function syncIitPersonnel(
    accountSetId: string,
    token: string | null,
): Promise<IitPersonnelSyncOutcome> {
    const result = await fetchIitPersonnel(accountSetId, token);
    if (result.needLogin || result.rows.length === 0) {
        return { rows: result.rows, needLogin: result.needLogin ?? false, message: result.message, sync: null };
    }
    const sync = await saveIitPersonnel(accountSetId, token, result.rows);
    return { rows: result.rows, needLogin: false, message: result.message, sync };
}

/** 提交并等待个税「专项附加扣除信息采集」取数任务。 */
export async function fetchIitDeductions(
    accountSetId: string,
    token: string | null,
): Promise<TaxIitDeductionResult> {
    const job = await submitIitJob(accountSetId, token, "/iit/deductions");
    return (job.data as TaxIitDeductionResult | undefined) ?? {
        rows: [],
        needLogin: job.needLogin ?? false,
        message: job.message ?? "专项附加扣除取数未完成，请稍后重试。",
    };
}

/**
 * 提交并等待个税申报记录取数任务。起止所属期形如 2026-01；任一为空就用税局页面默认的区间，
 * 后端填日期框失败也会照默认结果回，不报错。
 */
export async function fetchIitFilings(
    accountSetId: string,
    token: string | null,
    start?: string,
    end?: string,
): Promise<TaxIitFilingResult> {
    const query = new URLSearchParams();
    if (start) query.set("start", start);
    if (end) query.set("end", end);
    const suffix = query.toString() ? `?${query.toString()}` : "";
    const job = await submitIitJob(accountSetId, token, `/iit/filings${suffix}`);
    return (job.data as TaxIitFilingResult | undefined) ?? {
        rows: [],
        needLogin: job.needLogin ?? false,
        message: job.message ?? "个税申报记录取数未完成，请稍后重试。",
    };
}

/**
 * 提交并等待个税缴税记录取数。dateField=period 时起止形如 2026-01（税款所属期），
 * 否则形如 2026-01-01（缴税日期）；后端会按同一区间再过滤一次，页面日期框填不进去也不会串区间。
 */
export async function fetchIitPayments(
    accountSetId: string,
    token: string | null,
    dateField: TaxIitPaymentDateField,
    start: string,
    end: string,
): Promise<TaxIitPaymentResult> {
    const query = new URLSearchParams({ dateField, start, end });
    const job = await submitIitJob(accountSetId, token, `/iit/payments?${query.toString()}`);
    return (job.data as TaxIitPaymentResult | undefined) ?? {
        rows: [],
        needLogin: job.needLogin ?? false,
        message: job.message ?? "个税缴税记录取数未完成，请稍后重试。",
    };
}
