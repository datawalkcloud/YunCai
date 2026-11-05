/**
 * 服务端税局通道：登录、短信验证、税费种取数。
 *
 * 税局页面由服务器的无头浏览器打开，登录态留在服务器；网页只负责收短信验证码，
 * 用户不需要安装本机助手，也不需要打开任何税局页面。
 */
const API_BASE = "/api/TaxPortal";

export type TaxPortalLoginState =
    | "starting"
    | "captcha_required"
    | "sms_required"
    /** 个税扫码登录：二维码已回传，等个人所得税 APP 扫码确认。 */
    | "scan_required"
    | "success"
    | "failed";

/** 税局的文字点选验证码：底图与提示字都以 data URL 返回。 */
export interface TaxPortalCaptcha {
    promptImage: string;
    image: string;
    width: number;
    height: number;
}

export interface TaxPortalLoginResponse {
    state: TaxPortalLoginState;
    message: string;
    sessionId?: string;
    captcha?: TaxPortalCaptcha | null;
    /** 仅 scan_required 时返回：个税扫码登录的二维码（data URL）。 */
    qrImage?: string;
    /** 仅 failed 时返回：失败那一刻税局页面的截图（data URL）。 */
    failureShot?: string;
}

/** 相对底图左上角的 0~1 坐标，由服务器换算成真实像素后重放点击。 */
export interface TaxPortalCaptchaClick {
    x: number;
    y: number;
}

export interface TaxKindItem {
    name: string;
    rate: string;
    validFrom: string;
    validTo: string;
    source: string;
}

export interface TaxKindRow {
    key: string;
    project: string;
    declareKind: string;
    period: string;
    option: string;
    validFrom: string;
    validTo: string;
    source: string;
    items?: TaxKindItem[] | null;
}

export interface TaxKindsResult {
    rows: TaxKindRow[];
    /** 页头的征收类型（税局按户认定，如「服务型」）；报文里没有时为空，页面不显示。 */
    levyType?: string | null;
    message?: string | null;
    /** 税局登录态缺失或已过期：页面应弹出登录表单补一次短信验证码。 */
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

/** 税局里一条已申报的记录。 */
export interface TaxDeclarationRow {
    key: string;
    /** 申报表名称，如增值税及附加税费申报表（小规模纳税人适用） */
    declareKind: string;
    periodStart: string;
    periodEnd: string;
    /** 已申报 / 已更正 / 已作废 */
    status: string;
    /** 应补(退)税额 */
    payable: number;
    declareDate: string;
    feedback: string;
}

export interface TaxDeclarationsResult {
    rows: TaxDeclarationRow[];
    message?: string | null;
    /** 税局登录态缺失或已过期：页面应引导用户重新登录。 */
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

const START_TIMEOUT_MS = 30000;
const VERIFY_TIMEOUT_MS = 60000;
const STATUS_TIMEOUT_MS = 10000;
const FETCH_TIMEOUT_MS = 120000;
/** 直连接口要先把税局页面落到数字账户业务页，再分页取数，比普通请求慢，单独放宽。 */
const PORTAL_QUERY_TIMEOUT_MS = 180000;
/** 确认登录态要先打开税局首页验证保存的 Cookie，通常十秒出头，给到一分钟。 */
const RESUME_TIMEOUT_MS = 60000;

async function readError(response: Response): Promise<string> {
    try {
        const data = (await response.json()) as { message?: unknown; detail?: unknown };
        if (typeof data?.detail === "string" && data.detail) return data.detail;
        if (typeof data?.message === "string" && data.message) return data.message;
    } catch {
        // 非 JSON 响应按通用文案处理。
    }
    return "请求失败，请稍后重试。";
}

async function request<T>(path: string, token: string | null, timeoutMs: number, init?: RequestInit): Promise<T> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    try {
        const response = await fetch(`${API_BASE}${path}`, {
            ...init,
            cache: "no-store",
            headers: {
                ...(init?.body ? { "Content-Type": "application/json" } : {}),
                ...(token ? { Authorization: `Bearer ${token}` } : {}),
            },
            signal: controller.signal,
        });
        if (!response.ok) throw new Error(await readError(response));
        if (response.status === 204) return undefined as T;
        return (await response.json()) as T;
    } catch (err) {
        if (err instanceof DOMException && err.name === "AbortError") {
            throw new Error("服务器处理超时，请稍后重试。");
        }
        throw err;
    } finally {
        clearTimeout(timer);
    }
}

/** 个税通道（tax-iit.ts）与企业门户同一套鉴权与超时口径，复用同一个请求助手。 */
export { request as taxPortalRequest, START_TIMEOUT_MS, VERIFY_TIMEOUT_MS, STATUS_TIMEOUT_MS, FETCH_TIMEOUT_MS };

/** 用账套里保存的税局资料在服务器上发起登录；返回时会话已在后台推进。 */
export function startTaxPortalLogin(accountSetId: string, token: string | null): Promise<TaxPortalLoginResponse> {
    return request<TaxPortalLoginResponse>(
        `/${encodeURIComponent(accountSetId)}/login`,
        token,
        START_TIMEOUT_MS,
        { method: "POST" },
    );
}

/**
 * 进入税局页面时确认登录态：success 说明可以直接用，failed 说明需要重新登录。
 * 服务器可能要先拉起浏览器恢复保存的登录态，所以超时给得比查询状态长。
 */
export function resumeTaxPortalLogin(accountSetId: string, token: string | null): Promise<TaxPortalLoginResponse> {
    return request<TaxPortalLoginResponse>(
        `/${encodeURIComponent(accountSetId)}/login/resume`,
        token,
        RESUME_TIMEOUT_MS,
        { method: "POST" },
    );
}

/** 网页轮询登录进展（等短信、成功或失败）。 */
export function getTaxPortalLoginStatus(accountSetId: string, token: string | null): Promise<TaxPortalLoginResponse> {
    return request<TaxPortalLoginResponse>(
        `/${encodeURIComponent(accountSetId)}/login/status`,
        token,
        STATUS_TIMEOUT_MS,
        { method: "GET" },
    );
}

/** 把收到的短信验证码交给服务器提交。 */
export function verifyTaxPortalLogin(accountSetId: string, token: string | null, code: string): Promise<TaxPortalLoginResponse> {
    return request<TaxPortalLoginResponse>(
        `/${encodeURIComponent(accountSetId)}/login/verify`,
        token,
        VERIFY_TIMEOUT_MS,
        { method: "POST", body: JSON.stringify({ code }) },
    );
}

/** 把网页上按顺序点好的验证码坐标交给服务器重放。 */
export function submitTaxPortalCaptcha(
    accountSetId: string,
    token: string | null,
    clicks: TaxPortalCaptchaClick[],
): Promise<TaxPortalLoginResponse> {
    return request<TaxPortalLoginResponse>(
        `/${encodeURIComponent(accountSetId)}/login/captcha`,
        token,
        STATUS_TIMEOUT_MS,
        { method: "POST", body: JSON.stringify({ clicks }) },
    );
}

/**
 * 最近一次从税局取回的税费种认定。申报期限（月 / 季 / 年）只存在于认定里，
 * 报税工作台据此推默认税款所属期；放在内存里是为了不再为取一个默认值去打税局。
 */
let cachedTaxKinds: TaxKindRow[] = [];

/** 取回过的税费种认定；还没取过时为空数组，调用方按兜底口径处理。 */
export function getCachedTaxKinds(): TaxKindRow[] {
    return cachedTaxKinds;
}

/** 读取税局核定的税费种认定信息。 */
export async function fetchTaxKinds(accountSetId: string, token: string | null): Promise<TaxKindsResult> {
    const result = await request<TaxKindsResult>(
        `/${encodeURIComponent(accountSetId)}/tax-kinds`,
        token,
        FETCH_TIMEOUT_MS,
        { method: "GET" },
    );
    if (!result.needLogin && result.rows.length > 0) cachedTaxKinds = result.rows;
    return result;
}

/** 读取所选税款所属期（yyyyMM）在电子税务局里的申报记录。 */
export function fetchTaxDeclarations(
    accountSetId: string,
    token: string | null,
    period: string,
): Promise<TaxDeclarationsResult> {
    return request<TaxDeclarationsResult>(
        `/${encodeURIComponent(accountSetId)}/declarations?period=${encodeURIComponent(period)}`,
        token,
        FETCH_TIMEOUT_MS,
        { method: "GET" },
    );
}

/** 按申报日期区间（yyyy-MM-dd，含首尾）读取电子税务局里的申报表，供「报表查询」页。 */
export function fetchTaxReports(
    accountSetId: string,
    token: string | null,
    from: string,
    to: string,
): Promise<TaxDeclarationsResult> {
    const query = `from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
    return request<TaxDeclarationsResult>(
        `/${encodeURIComponent(accountSetId)}/declaration-reports?${query}`,
        token,
        FETCH_TIMEOUT_MS,
        { method: "GET" },
    );
}

/** 税局里一条已缴款的记录（缴款信息查询）。 */
export interface TaxPaymentRow {
    key: string;
    /** 征收项目，如增值税 */
    project: string;
    /** 征收品目，如销售货物(13%) */
    item: string;
    periodStart: string;
    periodEnd: string;
    /** 实缴（退）金额，负数为退还 */
    amount: number;
    /** 正税 / 滞纳金 / 罚款 */
    taxKind: string;
    /** 缴款（入库）日期 */
    paidOn: string;
    method: string;
    voucherKind: string;
    voucherNo: string;
    eTicketNo: string;
}

export interface TaxPaymentsResult {
    rows: TaxPaymentRow[];
    message?: string | null;
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

/** 当期应缴的一行（缴税页）。状态可能为空：数据取自申报记录时税局没给缴款状态。 */
export interface TaxPayableRow {
    key: string;
    kind: string;
    periodStart: string;
    periodEnd: string;
    deadline: string;
    refund: number;
    payable: number;
    surcharge: number;
    status: string;
}

export interface TaxPayablesResult {
    rows: TaxPayableRow[];
    message?: string | null;
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

/** 税局已开具的一张完税证明（下载完税证明页）。 */
export interface TaxCertificateRow {
    key: string;
    /** 证明名称，如税收完税证明 */
    name: string;
    /** 票证号码 */
    voucherNo: string;
    /** 开具税务机关 */
    authority: string;
    periodStart: string;
    periodEnd: string;
    paidOn: string;
    amount: number;
    issuedOn: string;
    format: string;
}

export interface TaxCertificatesResult {
    rows: TaxCertificateRow[];
    message?: string | null;
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

/** 按缴款日期区间（yyyy-MM-dd，含首尾）读取「缴款信息查询」。 */
export function fetchTaxPayments(
    accountSetId: string,
    token: string | null,
    from: string,
    to: string,
): Promise<TaxPaymentsResult> {
    const query = `from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
    return request<TaxPaymentsResult>(
        `/${encodeURIComponent(accountSetId)}/payments?${query}`,
        token,
        PORTAL_QUERY_TIMEOUT_MS,
        { method: "GET" },
    );
}

/** 按税款所属期（yyyyMM）读取「缴税」页的应缴税款。 */
export function fetchTaxPayables(
    accountSetId: string,
    token: string | null,
    period: string,
): Promise<TaxPayablesResult> {
    return request<TaxPayablesResult>(
        `/${encodeURIComponent(accountSetId)}/payables?period=${encodeURIComponent(period)}`,
        token,
        PORTAL_QUERY_TIMEOUT_MS,
        { method: "GET" },
    );
}

/** 按开具日期区间（yyyy-MM-dd，含首尾）读取已开具的完税证明。 */
export function fetchTaxCertificates(
    accountSetId: string,
    token: string | null,
    from: string,
    to: string,
): Promise<TaxCertificatesResult> {
    const query = `from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
    return request<TaxCertificatesResult>(
        `/${encodeURIComponent(accountSetId)}/certificates?${query}`,
        token,
        PORTAL_QUERY_TIMEOUT_MS,
        { method: "GET" },
    );
}

/** 退出税局登录，丢弃服务器上的登录态。 */
export function logoutTaxPortal(accountSetId: string, token: string | null): Promise<void> {
    return request<void>(
        `/${encodeURIComponent(accountSetId)}/logout`,
        token,
        STATUS_TIMEOUT_MS,
        { method: "POST" },
    );
}

/** 一键取票的落库结果：取回多少张、新增多少张、重复跳过多少张。 */
export interface TaxInvoiceSyncResult {
    fetched: number;
    imported: number;
    skipped: number;
    needLogin?: boolean;
    message?: string | null;
    diagnostics?: string[] | null;
    warnings?: string[] | null;
    fetchedAt?: string;
}

/** 一键取票的后台任务：running 时税局还在查，success 时发票已经落库。 */
export interface TaxInvoiceSyncJob {
    key: string;
    title: string;
    status: "running" | "success" | "failed";
    needLogin?: boolean;
    message?: string | null;
    data?: TaxInvoiceSyncResult | null;
    submittedAt: string;
    finishedAt?: string | null;
}

const JOB_POLL_INTERVAL_MS = 2000;
/** 后端给取票任务五分钟上限，页面再多等半分钟兜底。 */
const JOB_MAX_WAIT_MS = 330_000;

/**
 * 提交一次一键取票并轮询到任务结束。查询与翻页都在服务器上的无头浏览器里跑，
 * 接口先返回任务标识；发票落库由后台执行器完成，页面拿到成功结果后刷新列表即可。
 */
export async function syncTaxInvoices(
    accountSetId: string,
    token: string | null,
    startDate: string,
    endDate: string,
): Promise<TaxInvoiceSyncJob> {
    const query = `startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`;
    let job = await request<TaxInvoiceSyncJob>(
        `/${encodeURIComponent(accountSetId)}/invoices/sync?${query}`,
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
        job = await request<TaxInvoiceSyncJob>(
            `/${encodeURIComponent(accountSetId)}/invoices/sync/${encodeURIComponent(job.key)}`,
            token,
            STATUS_TIMEOUT_MS,
            { method: "GET" },
        );
    }

    return job;
}


/** 个税「报表填写」的一行申报表，字段与税局综合所得扣缴申报表的可算列一一对应。 */
export interface TaxIitReportRow {
    key: string;
    code: string;
    name: string;
    idNo: string;
    /** 本期收入 = 应发工资 */
    currentIncome: number;
    /** 减除费用标准 */
    standardDeduction: number;
    cumulativeIncome: number;
    cumulativeStandardDeduction: number;
    /** 累计专项扣除：工资表只登记「代扣个人款项」合计，未拆分险种 */
    cumulativeSpecialDeduction: number;
    remark: string;
    taxableIncome: number;
    rate: number;
    quickDeduction: number;
    cumulativeTax: number;
    /** 已缴税额 = 本年 1 月至上月已登记的个税 */
    paidTax: number;
    /** 应补（退）税额 */
    refundable: number;
}

export interface TaxIitReportResult {
    year: number;
    month: number;
    rows: TaxIitReportRow[];
    personCount: number;
    totalIncome: number;
    totalRefundable: number;
    /** 当期还没有工资表数据时的说明。 */
    message?: string | null;
}

/** 按累计预扣法从本系统工资表生成个税申报表（period = yyyyMM）。 */
export function fetchIitReport(
    accountSetId: string,
    token: string | null,
    period: string,
): Promise<TaxIitReportResult> {
    return request<TaxIitReportResult>(
        `/${encodeURIComponent(accountSetId)}/iit/report?period=${encodeURIComponent(period)}`,
        token,
        FETCH_TIMEOUT_MS,
        { method: "GET" },
    );
}

/** 任务中心的一行：服务器在税局侧办过的一件事。 */
export interface TaxTaskRow {
    key: string;
    /** 税款所属期（yyyyMM）。 */
    period: number;
    title: string;
    /** running / success / failed。个税取数先落 running，任务在后台跑完再回填。 */
    status: string;
    /** 失败时服务器给的下一步建议；成功为空。 */
    suggestion?: string | null;
    submittedAt: string;
    finishedAt: string;
    /** 操作人：发起这次任务的登录用户。 */
    operator: string;
}

export interface TaxTasksResult {
    rows: TaxTaskRow[];
    total: number;
}

/** 按提交时间区间（yyyy-MM-dd，含首尾）查询本账套的税局任务，时间倒序。 */
export function fetchTaxTasks(
    accountSetId: string,
    token: string | null,
    from: string,
    to: string,
): Promise<TaxTasksResult> {
    const query = `from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
    return request<TaxTasksResult>(
        `/${encodeURIComponent(accountSetId)}/tasks?${query}`,
        token,
        STATUS_TIMEOUT_MS,
        { method: "GET" },
    );
}
