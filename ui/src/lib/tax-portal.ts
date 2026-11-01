/**
 * 服务端税局通道：登录、短信验证、税费种取数。
 *
 * 税局页面由服务器的无头浏览器打开，登录态留在服务器；网页只负责收短信验证码，
 * 用户不需要安装本机助手，也不需要打开任何税局页面。
 */
const API_BASE = "/api/TaxPortal";

export type TaxPortalLoginState = "starting" | "captcha_required" | "sms_required" | "success" | "failed";

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
    message?: string | null;
    /** 税局登录态缺失或已过期：页面应弹出登录表单补一次短信验证码。 */
    needLogin?: boolean;
    diagnostics?: string[] | null;
    fetchedAt?: string;
}

const START_TIMEOUT_MS = 30000;
const VERIFY_TIMEOUT_MS = 60000;
const STATUS_TIMEOUT_MS = 10000;
const FETCH_TIMEOUT_MS = 120000;
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

/** 读取税局核定的税费种认定信息。 */
export function fetchTaxKinds(accountSetId: string, token: string | null): Promise<TaxKindsResult> {
    return request<TaxKindsResult>(
        `/${encodeURIComponent(accountSetId)}/tax-kinds`,
        token,
        FETCH_TIMEOUT_MS,
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
