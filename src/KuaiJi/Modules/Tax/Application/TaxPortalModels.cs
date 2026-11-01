namespace KuaiJi.Modules.Tax.Application
{
    /// <summary>服务端税局登录的状态取值，与前端弹窗的分支一一对应。</summary>
    public static class TaxPortalStates
    {
        public const string Starting = "starting";
        public const string CaptchaRequired = "captcha_required";
        public const string SmsRequired = "sms_required";
        public const string Success = "success";
        public const string Failed = "failed";
    }

    /// <summary>
    /// 税局的「文字点选」验证码：提示字与底图都以 data URL 下发，
    /// 网页按顺序点选后把归一化坐标交回服务器，由服务器在无头浏览器里重放点击。
    /// </summary>
    public sealed class TaxPortalCaptchaDto
    {
        /// <summary>提示文字图片（「请依次点击」后面的那几个字）。</summary>
        public string PromptImage { get; init; } = string.Empty;

        /// <summary>点选底图。</summary>
        public string Image { get; init; } = string.Empty;

        /// <summary>底图按此宽高显示，点选坐标按该尺寸归一化。</summary>
        public int Width { get; init; }

        public int Height { get; init; }
    }

    /// <summary>一次服务端登录会话的对外快照。</summary>
    public sealed class TaxPortalLoginStatusDto
    {
        public string State { get; init; } = TaxPortalStates.Starting;

        public string Message { get; init; } = string.Empty;

        public string SessionId { get; init; } = string.Empty;

        /// <summary>只在 State 为 captcha_required 时返回。</summary>
        public TaxPortalCaptchaDto? Captcha { get; init; }
    }

    /// <summary>网页上的一次点选，坐标是相对底图左上角的 0~1 比例。</summary>
    public sealed class TaxPortalCaptchaClick
    {
        public double X { get; init; }

        public double Y { get; init; }
    }

    /// <summary>网页提交的验证码点选序列。</summary>
    public sealed class TaxPortalCaptchaRequest
    {
        public IReadOnlyList<TaxPortalCaptchaClick>? Clicks { get; init; }
    }

    /// <summary>临时诊断用：要依次点击的菜单文字，以及可选的表单填写与查询动作。</summary>
    public sealed class ExploreRequest
    {
        public List<string>? Path { get; init; }

        /// <summary>按字段标签（如「税款所属期起」）填写的值。</summary>
        public Dictionary<string, string>? Fill { get; init; }

        /// <summary>填完后依次点击的按钮文字，如「查询」。</summary>
        public List<string>? Click { get; init; }

        /// <summary>在目标页面里执行的 JS 表达式，结果按字符串回传（仅诊断用，如读 DOM 或直接调接口）。</summary>
        public string? Evaluate { get; init; }
    }

    /// <summary>临时诊断用：发票查询的开票日期区间。</summary>
    public sealed class InvoiceQueryProbeRequest
    {
        public DateTime? StartDate { get; init; }

        public DateTime? EndDate { get; init; }
    }

    /// <summary>网页提交短信验证码时带回来的请求体。</summary>
    public sealed class TaxPortalVerifyRequest
    {
        public string? Code { get; init; }
    }

    /// <summary>交给本机助手注入浏览器的一条税局 Cookie。</summary>
    public sealed class TaxPortalBrowserCookieDto
    {
        public string Name { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;

        public string Domain { get; init; } = string.Empty;

        public string Path { get; init; } = "/";

        /// <summary>Unix 秒；会话级 Cookie 为 -1。</summary>
        public double Expires { get; init; } = -1;

        public bool HttpOnly { get; init; }

        public bool Secure { get; init; }

        /// <summary>Strict / Lax / None，与税局写入时的取值一致。</summary>
        public string SameSite { get; init; } = "Lax";
    }

    /// <summary>某个税局站点域下的 localStorage 快照；tpass 用它保存登录 token。</summary>
    public sealed class TaxPortalBrowserOriginDto
    {
        public string Origin { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Items { get; init; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 一次浏览器会话交接：服务器上已登录的 Cookie / localStorage，以及要打开的企业电子税务局地址。
    /// 助手把登录态注入自己的窗口后再导航，用户点「一键登录电子税务局」时不必重新登录。
    /// </summary>
    public sealed class TaxPortalBrowserHandoffDto
    {
        public string Url { get; init; } = string.Empty;

        public IReadOnlyList<TaxPortalBrowserCookieDto> Cookies { get; init; } = Array.Empty<TaxPortalBrowserCookieDto>();

        public IReadOnlyList<TaxPortalBrowserOriginDto> Origins { get; init; } = Array.Empty<TaxPortalBrowserOriginDto>();
    }

    /// <summary>网页换取的一次性会话票据；票据只能兑换一次，不直接下发 Cookie。</summary>
    public sealed class TaxPortalBrowserTicketDto
    {
        public string Ticket { get; init; } = string.Empty;

        /// <summary>助手最终要打开的企业电子税务局地址，随票据一起返回便于界面提前展示。</summary>
        public string Url { get; init; } = string.Empty;

        public int ExpiresInSeconds { get; init; } = 120;
    }

    /// <summary>本机助手兑换会话票据的请求体。</summary>
    public sealed class TaxPortalBrowserTicketRedeemRequest
    {
        public string? Ticket { get; init; }
    }

    /// <summary>服务端登录税局时使用的一整套身份资料，来自账套已保存的税局登录信息。</summary>
    public sealed record TaxPortalCredentials(
        string CompanyName,
        string TaxpayerNumber,
        string Region,
        string LoginType,
        string IdentityType,
        string Account,
        string Password);

    /// <summary>
    /// 会话归属：账套 + 登录用户。会话里持有税局登录态（Cookie），不能跨用户复用，
    /// 所以键里必须带用户标识，而不仅仅是账套。
    /// </summary>
    public readonly record struct TaxPortalSessionKey(Guid AccountSetId, string UserId);

    /// <summary>征收品目（税费种认定里的下钻明细）。</summary>
    public sealed class TaxKindItemDto
    {
        public string Name { get; init; } = string.Empty;

        public string Rate { get; init; } = string.Empty;

        public string ValidFrom { get; init; } = string.Empty;

        public string ValidTo { get; init; } = string.Empty;

        public string Source { get; init; } = "税局获取";
    }

    /// <summary>
    /// 税费种认定的一行。字段名与前端税费种管理页保持一致，页面直接把接口数据铺进表格。
    /// </summary>
    public sealed class TaxKindRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>征收项目，如增值税。</summary>
        public string Project { get; init; } = string.Empty;

        /// <summary>申报种类，如增值税及附加税费申报（小规模纳税人）。</summary>
        public string DeclareKind { get; init; } = string.Empty;

        /// <summary>申报期限：月 / 季 / 年 / 次。</summary>
        public string Period { get; init; } = string.Empty;

        /// <summary>申报选项：申报 / 代扣代缴 / 代收代缴。</summary>
        public string Option { get; init; } = string.Empty;

        public string ValidFrom { get; init; } = string.Empty;

        public string ValidTo { get; init; } = string.Empty;

        public string Source { get; init; } = "税局获取";

        public IReadOnlyList<TaxKindItemDto>? Items { get; init; }
    }

    /// <summary>税费种查询结果；取不到数据时带上抓包摘要，便于按真实响应补解析规则。</summary>
    public sealed class TaxKindsResultDto
    {
        public IReadOnlyList<TaxKindRowDto> Rows { get; init; } = Array.Empty<TaxKindRowDto>();

        public string? Message { get; init; }

        /// <summary>税局登录态缺失或已过期，网页需要弹出登录表单重新收一次短信。</summary>
        public bool NeedLogin { get; init; }

        /// <summary>只在没有解析出数据时返回：本次抓到的 JSON 响应地址与顶层字段。</summary>
        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>抓到的 JSON 响应，供税费种解析与诊断使用。</summary>
    public sealed record CapturedJsonPayload(string Url, string Body);

    /// <summary>一条已申报记录，字段与「税种申报」表格的列一一对应。</summary>
    public sealed class TaxDeclarationRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>申报种类，取税局的申报表种类名称。</summary>
        public string DeclareKind { get; init; } = string.Empty;

        public string PeriodStart { get; init; } = string.Empty;

        public string PeriodEnd { get; init; } = string.Empty;

        /// <summary>已申报 / 已作废 / 已更正。</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>应补（退）税额。</summary>
        public decimal Payable { get; init; }

        public string DeclareDate { get; init; } = string.Empty;

        /// <summary>反馈：作废日期或更正类型；申报表没有这两项时为空。</summary>
        public string Feedback { get; init; } = string.Empty;
    }

    /// <summary>税种申报的查询结果；登录态失效时由网页弹出登录表单。</summary>
    public sealed class TaxDeclarationsResultDto
    {
        public IReadOnlyList<TaxDeclarationRowDto> Rows { get; init; } = Array.Empty<TaxDeclarationRowDto>();

        public string? Message { get; init; }

        public bool NeedLogin { get; init; }

        /// <summary>只在没有解析出数据时返回：税局响应摘要，便于按真实接口补解析。</summary>
        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
