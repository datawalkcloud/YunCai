/**
 * 本地税务助手的探测、唤起与登录接口。
 *
 * 浏览器查不到系统注册了哪个自定义协议，所以「是否安装助手」只能靠探测助手常驻监听的
 * 本机 HTTP 端口：有进程应答即视为在运行，连接被拒或超时即视为未安装。
 * 探测用 no-cors 只关心「有没有人应答」；登录接口要读返回体，走 CORS 并在助手侧配好响应头。
 *
 * 已知盲区：https 公网页访问 localhost 会触发 Chrome 的 Private Network Access 预检，
 * 助手没回 Access-Control-Allow-Private-Network 头时请求会失败。所以「未检测到」不能当
 * 唯一结论，界面上要给用户留「重新检测」和直接唤起的出口。
 */

/** 助手本机服务端口，需与安装包注册的监听端口一致（src/TaxAssistant 监听 41731）。
 *  必须用 localhost：助手的 HttpListener 只注册了 localhost 前缀，127.0.0.1 会返回 400。 */
const ASSISTANT_ORIGIN = "http://localhost:41731";

const PING_URL = `${ASSISTANT_ORIGIN}/ping`;

/** 本机端口无监听时连接会立刻被拒，这个超时只用于兜住「端口被别的程序占着但不答应」的情况。 */
const PROBE_TIMEOUT_MS = 1500;

/** 助手注册的自定义协议；唤起时浏览器顶部会弹「打开 云财税务助手」确认框。 */
export const ASSISTANT_SCHEME = "yuncai:tax";

/** 电子税务局全国统一入口；助手未配置省份地址时也用它兜底。 */
export const ASSISTANT_TAX_PORTAL_URL = "https://etax.chinatax.gov.cn/";

/** 助手安装包下载地址：由 SPA 静态资源托管，构建时从 ui/public/downloads 原样拷贝。 */
export const ASSISTANT_DOWNLOAD_URL = "/downloads/YunCaiTaxAssistantSetup.exe";

/** 浏览器保存到本机时使用的文件名，服务器上仍用 ASCII 名以免 URL 编码问题。 */
const ASSISTANT_SETUP_FILENAME = "云财税务助手安装包.exe";

/**
 * 唤起助手时要交出去的企业身份。
 * 电子税务局由助手自己的窗口（内置浏览器）打开，网页侧只负责把「登哪家企业」传过去；
 * 参数名属于与安装包的约定，助手实现落地后以那边为准。
 */
export interface AssistantTarget {
    /** 公司名称，助手窗口用于显示与人工核对。 */
    companyName: string;
    /** 纳税人识别号（统一社会信用代码），助手靠它定位企业会话。 */
    taxpayerNumber: string;
    /** 报税地区（省级税务局名称），助手靠它决定打开哪个省的电子税务局。 */
    region?: string;
    /** 税局登录方式与身份类型，对应税局登录页的两个页签。 */
    loginType?: string;
    identityType?: string;
    /** 税局实名账号。只交账号，不交密码。 */
    account?: string;
    /** 服务器签发的一次性登录态票据；带上后助手会注入服务器上的登录态，打开税局即已登录。 */
    ticket?: string;
    /** 登录后要打开的企业电子税务局地址（随票据一起从后端取回）。 */
    url?: string;
    /** 后端 API 根地址，助手凭它兑换票据。 */
    apiBase?: string;
}

/** 探测本机是否有税务助手在应答。 */
export async function probeAssistant(timeoutMs = PROBE_TIMEOUT_MS): Promise<boolean> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    try {
        await fetch(PING_URL, { mode: "no-cors", signal: controller.signal });
        return true;
    } catch {
        return false;
    } finally {
        clearTimeout(timer);
    }
}

/**
 * 唤起本地助手，由助手打开自己的窗口登录电子税务局。
 * 用户没装助手时这一步静默失败，浏览器只在顶部给一条提示，页面感知不到，
 * 所以调用前必须先过 probeAssistant，调用后也不能声称「已登录」。
 */
/** 下载助手安装包。用 <a download> 触发，避免 window.open 被浏览器拦截。 */
export function downloadAssistant(): void {
    if (!ASSISTANT_DOWNLOAD_URL) return;
    const link = document.createElement("a");
    link.href = ASSISTANT_DOWNLOAD_URL;
    link.download = ASSISTANT_SETUP_FILENAME;
    link.rel = "noopener";
    document.body.appendChild(link);
    link.click();
    link.remove();
}

export function launchAssistant(target: AssistantTarget): void {
    const qs = new URLSearchParams({
        company: target.companyName,
        taxNo: target.taxpayerNumber,
        url: target.url || ASSISTANT_TAX_PORTAL_URL,
    });
    if (target.region) qs.set("region", target.region);
    if (target.loginType) qs.set("loginType", target.loginType);
    if (target.identityType) qs.set("identityType", target.identityType);
    if (target.account) qs.set("account", target.account);
    // 票据 2 分钟一次性，助手兑换后立即作废；税局密码与完整登录态都不出现在 URI 里。
    if (target.ticket) qs.set("ticket", target.ticket);
    if (target.apiBase) qs.set("apiBase", target.apiBase);
    window.location.href = `${ASSISTANT_SCHEME}?${qs.toString()}`;
}

/**
 * 用当前登录态换取一次性会话票据。
 * 助手把票据换回服务器上的税局 Cookie 并注入自己的浏览器，打开电子税务局时就是登录状态，
 * 用户不需要在助手窗口里再登录一次；票据 2 分钟内只能兑换一次。
 */
export async function requestTaxBrowserTicket(
    accountSetId: string,
    token: string | null,
): Promise<{ ticket: string; url: string }> {
    const response = await fetch(
        `/api/TaxPortal/${encodeURIComponent(accountSetId)}/browser-session-ticket`,
        {
            method: "POST",
            headers: token ? { Authorization: `Bearer ${token}` } : undefined,
        },
    );
    let data: { ticket?: unknown; url?: unknown; message?: unknown } = {};
    try {
        data = (await response.json()) as { ticket?: unknown; url?: unknown; message?: unknown };
    } catch {
        // 非 JSON 响应按失败处理。
    }
    if (!response.ok || typeof data.ticket !== "string" || !data.ticket) {
        const message = typeof data.message === "string" && data.message
            ? data.message
            : "获取税局登录态失败，请重新登录电子税务局。";
        throw new Error(message);
    }
    return { ticket: data.ticket, url: typeof data.url === "string" ? data.url : "" };
}

/** 助手侧登录状态：等短信、等用户过滑块、成功或失败。 */
export type TaxLoginState = "starting" | "sms_required" | "manual_required" | "success" | "failed";

export interface TaxLoginResponse {
    state: TaxLoginState;
    message: string;
    sessionId?: string | null;
}

export interface TaxLoginStartInput {
    companyName: string;
    taxNo: string;
    region: string;
    loginType: string;
    identityType: string;
    account: string;
    /** 本次填写的密码；留空时改用 ticket 取回账套里已保存的那份。 */
    password?: string;
    /** 由带登录态的云端接口签发的 2 分钟一次性票据。 */
    ticket?: string;
    /** 云端 API 根地址（形如 https://站点/api），助手凭它兑换票据。 */
    apiBase?: string;
    accountId?: string;
}

const START_TIMEOUT_MS = 45000;
const VERIFY_TIMEOUT_MS = 50000;
const STATUS_TIMEOUT_MS = 8000;

async function readAssistantError(response: Response): Promise<string> {
    try {
        const data = (await response.json()) as { message?: unknown };
        if (typeof data?.message === "string" && data.message) return data.message;
    } catch {
        // 返回体不是 JSON 时用兜底文案。
    }
    return "助手返回了错误，请重试。";
}

async function postAssistantJson<T>(path: string, body: unknown, timeoutMs: number): Promise<T> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    try {
        const response = await fetch(`${ASSISTANT_ORIGIN}${path}`, {
            method: "POST",
            mode: "cors",
            cache: "no-store",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
            signal: controller.signal,
        });
        if (!response.ok) throw new Error(await readAssistantError(response));
        return (await response.json()) as T;
    } catch (err) {
        if (err instanceof DOMException && err.name === "AbortError") {
            throw new Error("助手响应超时，请确认助手窗口已打开后重试。");
        }
        if (err instanceof TypeError) {
            throw new Error("无法连接云财税务助手，请确认助手正在运行。");
        }
        throw err;
    } finally {
        clearTimeout(timer);
    }
}

/** 让助手打开税局登录页并自动填表；返回时通常停在「等短信验证码」。 */
export function startTaxLogin(input: TaxLoginStartInput): Promise<TaxLoginResponse> {
    return postAssistantJson<TaxLoginResponse>("/tax-login/start", input, START_TIMEOUT_MS);
}

/** 把网页上收到的短信验证码交给助手提交。 */
export function verifyTaxLogin(sessionId: string, code: string): Promise<TaxLoginResponse> {
    return postAssistantJson<TaxLoginResponse>("/tax-login/verify", { sessionId, code }, VERIFY_TIMEOUT_MS);
}

/** 轮询助手侧进展（用户在窗口里过完滑块后，状态会自动变成等短信）。 */
export async function getTaxLoginStatus(sessionId: string): Promise<TaxLoginResponse> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), STATUS_TIMEOUT_MS);
    try {
        const response = await fetch(`${ASSISTANT_ORIGIN}/tax-login/status?sessionId=${encodeURIComponent(sessionId)}`, {
            method: "GET",
            mode: "cors",
            cache: "no-store",
            signal: controller.signal,
        });
        if (!response.ok) throw new Error(await readAssistantError(response));
        return (await response.json()) as TaxLoginResponse;
    } finally {
        clearTimeout(timer);
    }
}

/**
 * 用当前登录态把账套里已保存的税局密码换成一次性票据。
 * 密码本身不会回到浏览器：票据交给助手后，由助手直接向云端兑换。
 */
export async function requestTaxLoginTicket(accountSetId: string, token: string | null): Promise<string> {
    const response = await fetch(`/api/AccountSets/${encodeURIComponent(accountSetId)}/tax-login-ticket`, {
        method: "POST",
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    });
    let data: { ticket?: unknown; message?: unknown } = {};
    try {
        data = (await response.json()) as { ticket?: unknown; message?: unknown };
    } catch {
        // 非 JSON 响应按失败处理。
    }
    if (!response.ok || typeof data.ticket !== "string" || !data.ticket) {
        const message = typeof data.message === "string" && data.message
            ? data.message
            : "获取已保存的税局密码失败，请重新填写一次密码。";
        throw new Error(message);
    }
    return data.ticket;
}
