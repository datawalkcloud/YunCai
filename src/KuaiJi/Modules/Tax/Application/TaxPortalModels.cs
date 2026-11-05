using KuaiJi.Modules.Invoices.Domain;

namespace KuaiJi.Modules.Tax.Application
{
    /// <summary>服务端税局登录的状态取值，与前端弹窗的分支一一对应。</summary>
    public static class TaxPortalStates
    {
        public const string Starting = "starting";
        public const string CaptchaRequired = "captcha_required";
        public const string SmsRequired = "sms_required";
        /// <summary>个税（自然人电子税务局）扫码登录：二维码已回传，等用户用个人所得税 APP 扫码确认。</summary>
        public const string ScanRequired = "scan_required";
        public const string Success = "success";
        public const string Failed = "failed";
    }

    /// <summary>
    /// 服务端税局通道的类型。企业电子税务局门户与自然人电子税务局（个税）是两套站点、
    /// 两套账号与两份登录态，会话与存档都按类型分开保存。
    /// </summary>
    public enum TaxPortalKind
    {
        /// <summary>企业电子税务局门户（etax.&lt;省份&gt;.chinatax.gov.cn）。</summary>
        Enterprise = 0,

        /// <summary>自然人电子税务局（etax.chinatax.gov.cn，个税人员信息采集在这里）。</summary>
        Iit = 1,
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

        /// <summary>只在 State 为 scan_required 时返回：个税扫码登录的二维码（data URL）。</summary>
        public string QrImage { get; init; } = string.Empty;

        /// <summary>只在 State 为 failed 时返回：失败那一刻税局页面的截图（data URL），让页面直接看到无头浏览器里发生了什么。</summary>
        public string FailureShot { get; init; } = string.Empty;
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
        /// <summary>探测哪套税局站点；缺省探测企业门户。</summary>
        public TaxPortalKind? Kind { get; init; }

        public List<string>? Path { get; init; }

        /// <summary>
        /// 按字段标签（如「税款所属期起」）填写的值。
        /// 这里刻意用非空字典 + 空值兜底：可空的 Dictionary 会被 .NET 10 导出成
        /// type: ["null","object"]，openapi-generator 7.20 不认这种 map，会生成非法的 TS。
        /// </summary>
        public Dictionary<string, string> Fill { get; init; } = [];

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

        /// <summary>诊断用：先点「重置」清空筛选再查询，用来对比「日期没提交」与「确实没有发票」。</summary>
        public bool ResetFilters { get; init; }
    }

    /// <summary>临时诊断用：个税摸底要点的菜单文字（缺省「人员信息采集」）。</summary>
    public sealed class IitPlainProbeRequest
    {
        public string? Menu { get; init; }
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
    /// 会话归属：账套 + 登录用户 + 税局通道类型。企业门户与个税是两套登录态，
    /// 同一账套同一用户可以同时各挂一份会话，互不复用。
    /// </summary>
    public readonly record struct TaxPortalSessionKey(Guid AccountSetId, string UserId, TaxPortalKind Kind);

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

        /// <summary>页头的征收类型（如「服务型」），由税局按户认定；报文里没有时为空，页面不显示。</summary>
        public string? LevyType { get; init; }

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

    /// <summary>发起个税（自然人电子税务局）登录的请求。</summary>
    public sealed class TaxIitLoginRequest
    {
        /// <summary>password = 账号密码登录（默认）；scan = 扫码登录（个人所得税 APP 扫码）。</summary>
        public string? Mode { get; init; }
    }

    /// <summary>
    /// 个税「人员信息采集」里的一行。字段口径与税局人员信息采集页一致，
    /// 与工资模块员工档案的映射由前端预览弹窗完成。
    /// </summary>
    public sealed class TaxIitPersonnelRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>税局侧序号/编号（有则回填）。</summary>
        public string Code { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        /// <summary>证件类型，如「居民身份证」。</summary>
        public string IdTypeText { get; init; } = string.Empty;

        public string IdNo { get; init; } = string.Empty;

        public string Phone { get; init; } = string.Empty;

        public string GenderText { get; init; } = string.Empty;

        /// <summary>任职受雇从业类型：雇员 / 保险营销员 / 证券经纪人 / 实习学生（全日制学历教育）/ 其他。</summary>
        public string EmployTypeText { get; init; } = string.Empty;

        /// <summary>人员状态：正常 / 非正常。</summary>
        public string StatusText { get; init; } = string.Empty;

        /// <summary>任职受雇从业日期（入职）。</summary>
        public string HireDate { get; init; } = string.Empty;

        /// <summary>离职日期。</summary>
        public string LeaveDate { get; init; } = string.Empty;

        /// <summary>部门（税局侧登记的部门名，可能为空）。</summary>
        public string Dept { get; init; } = string.Empty;

        /// <summary>报送状态：报送成功 / 报送失败 / 待报送 / 报送中。</summary>
        public string ReportStateText { get; init; } = string.Empty;

        /// <summary>身份验证状态：验证通过 / 验证不通过 / 待验证 / 验证中 / 暂不校验。</summary>
        public string VerifyStateText { get; init; } = string.Empty;

        /// <summary>是否已采集专项附加扣除（有值时回传「是/否」，取不到为空）。</summary>
        public string DeductionText { get; init; } = string.Empty;

        /// <summary>解析出该行的原始 JSON 片段，便于排查与补解析规则。</summary>
        public string Raw { get; init; } = string.Empty;
    }

    /// <summary>
    /// 个税取数结果的共同口径。取数跑在请求之外，执行器与任务中心只按这三列判断
    /// 任务办没办成，具体数据原样回传给页面。
    /// </summary>
    public interface IIitFetchResult
    {
        /// <summary>解析出的行数。</summary>
        int RowCount { get; }

        /// <summary>个税登录态缺失或已过期。</summary>
        bool NeedLogin { get; }

        /// <summary>没有结果时给用户的说明，同时写进台账的「解决建议」。</summary>
        string? Message { get; }
    }

    /// <summary>个税人员信息取数结果；登录态缺失时 NeedLogin=true，解析不出时带诊断摘要。</summary>
    public sealed class TaxIitPersonnelResultDto : IIitFetchResult
    {
        public IReadOnlyList<TaxIitPersonnelRowDto> Rows { get; init; } = Array.Empty<TaxIitPersonnelRowDto>();

        public string? Message { get; init; }

        public bool NeedLogin { get; init; }

        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;

        public int RowCount => Rows.Count;
    }

    /// <summary>
    /// 个税「专项附加扣除信息采集」里的一条扣除记录。一名人员可能同时有多条
    /// （子女教育、住房租金、赡养老人……），所以行粒度是「人员 × 扣除项目 × 有效期」。
    /// </summary>
    public sealed class TaxIitDeductionRowDto
    {
        public string Key { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        /// <summary>证件类型，如「居民身份证」。</summary>
        public string IdTypeText { get; init; } = string.Empty;

        public string IdNo { get; init; } = string.Empty;

        /// <summary>扣除项目：子女教育 / 继续教育 / 大病医疗 / 住房贷款利息 / 住房租金 / 赡养老人 / 3岁以下婴幼儿照护。</summary>
        public string ProjectText { get; init; } = string.Empty;

        /// <summary>扣除对象或关系，如「子女」「本人」「配偶父母」。</summary>
        public string RelativeText { get; init; } = string.Empty;

        /// <summary>扣除年度（如 2026）。</summary>
        public string YearText { get; init; } = string.Empty;

        /// <summary>有效期起。</summary>
        public string PeriodStart { get; init; } = string.Empty;

        /// <summary>有效期止。</summary>
        public string PeriodEnd { get; init; } = string.Empty;

        /// <summary>每月扣除金额（元）。</summary>
        public decimal MonthlyAmount { get; init; }

        /// <summary>申报方式：通过扣缴义务人申报 / 综合所得年度自行申报。</summary>
        public string DeclareWayText { get; init; } = string.Empty;

        /// <summary>验证状态：已验证 / 未验证 / 验证不通过。</summary>
        public string VerifyStateText { get; init; } = string.Empty;

        /// <summary>报送或下载状态。</summary>
        public string ReportStateText { get; init; } = string.Empty;

        /// <summary>解析出该行的原始 JSON 片段，便于排查与补解析规则。</summary>
        public string Raw { get; init; } = string.Empty;
    }

    /// <summary>个税专项附加扣除取数结果；登录态缺失时 NeedLogin=true，解析不出时带诊断摘要。</summary>
    public sealed class TaxIitDeductionResultDto : IIitFetchResult
    {
        public IReadOnlyList<TaxIitDeductionRowDto> Rows { get; init; } = Array.Empty<TaxIitDeductionRowDto>();

        public string? Message { get; init; }

        public bool NeedLogin { get; init; }

        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;

        public int RowCount => Rows.Count;
    }

    /// <summary>
    /// 个税「查询统计 → 申报记录查询」里的一条申报记录。一行 = 某个税款所属期报的一张表，
    /// 同一期可能有多条（正常申报 + 作废/更正）。金额与人次一律按税局展示的文本回传，
    /// 税局对未申报的格子给「-」，这里不做数字化，免得把口径猜错。
    /// </summary>
    public sealed class TaxIitFilingRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>税款所属期，如「2026-09」。</summary>
        public string Period { get; init; } = string.Empty;

        /// <summary>申报表名，如「综合所得预扣预缴表」。</summary>
        public string FormName { get; init; } = string.Empty;

        /// <summary>申报人次。</summary>
        public string Headcount { get; init; } = string.Empty;

        /// <summary>当期收入（元）。</summary>
        public string Income { get; init; } = string.Empty;

        /// <summary>应补（退）税额（元）。</summary>
        public string TaxDue { get; init; } = string.Empty;

        /// <summary>申报类型：正常申报 / 逾期申报 / 补充申报等。</summary>
        public string DeclareType { get; init; } = string.Empty;

        /// <summary>申报渠道：客户端 / WEB端 等。</summary>
        public string Channel { get; init; } = string.Empty;

        /// <summary>申报时间。</summary>
        public string DeclareTime { get; init; } = string.Empty;

        /// <summary>解析出该行的原始 JSON 片段，便于排查与补解析规则。</summary>
        public string Raw { get; init; } = string.Empty;
    }

    /// <summary>个税申报记录取数结果；登录态缺失时 NeedLogin=true，解析不出时带诊断摘要。</summary>
    public sealed class TaxIitFilingResultDto : IIitFetchResult
    {
        public IReadOnlyList<TaxIitFilingRowDto> Rows { get; init; } = Array.Empty<TaxIitFilingRowDto>();

        public string? Message { get; init; }

        public bool NeedLogin { get; init; }

        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;

        public int RowCount => Rows.Count;
    }

    /// <summary>
    /// 个税「查询统计 → 缴税记录查询」里的一条缴税记录。这是扣缴端按人按次的缴款流水，
    /// 与数字账户「缴款信息查询」（企业端）不是同一份数据，不能互相填充。
    /// 字段按税局展示的文本回传：金额可能带千分位或「-」，不做数字化。
    /// </summary>
    public sealed class TaxIitPaymentRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>税款所属期，如「2026-09」。</summary>
        public string Period { get; init; } = string.Empty;

        /// <summary>报表类型，如「综合所得预扣预缴表」。</summary>
        public string FormName { get; init; } = string.Empty;

        /// <summary>电子税票号。</summary>
        public string ETicketNo { get; init; } = string.Empty;

        /// <summary>缴税金额（元）。</summary>
        public string Amount { get; init; } = string.Empty;

        /// <summary>缴税日期。</summary>
        public string PaidOn { get; init; } = string.Empty;

        /// <summary>缴税种类：正税 / 滞纳金 / 罚款等。</summary>
        public string TaxKind { get; init; } = string.Empty;

        /// <summary>缴税方式，如「三方协议」。 </summary>
        public string Method { get; init; } = string.Empty;

        /// <summary>解析出该行的原始 JSON 片段，便于排查与补解析规则。</summary>
        public string Raw { get; init; } = string.Empty;
    }

    /// <summary>个税缴税记录取数结果；登录态缺失时 NeedLogin=true，解析不出时带诊断摘要。</summary>
    public sealed class TaxIitPaymentResultDto : IIitFetchResult
    {
        public IReadOnlyList<TaxIitPaymentRowDto> Rows { get; init; } = Array.Empty<TaxIitPaymentRowDto>();

        public string? Message { get; init; }

        public bool NeedLogin { get; init; }

        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;

        public int RowCount => Rows.Count;
    }

    /// <summary>
    /// 后台取数任务的进展。接口先回 running，页面轮询到 success 时 Data 里就是
    /// 各接口原本的取数结果；任务同时登记在任务中心，内存里的结果过期后只查得到台账。
    /// </summary>
    public sealed class TaxIitJobDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>任务说明，与任务中心那一行的「任务说明」一致。</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>running / success / failed，与任务中心同一套取值。</summary>
        public string Status { get; init; } = "running";

        /// <summary>个税登录态缺失：页面应弹出「个税申报登录」。</summary>
        public bool NeedLogin { get; init; }

        /// <summary>失败或没有结果时给用户的说明。</summary>
        public string? Message { get; init; }

        /// <summary>完成后的取数结果，结构与各接口原同步返回的一致。</summary>
        public object? Data { get; init; }

        public DateTimeOffset SubmittedAt { get; init; }

        public DateTimeOffset? FinishedAt { get; init; }
    }

    /// <summary>任务中心的一行：服务端向税局办过一次的一件事。</summary>
    public sealed class TaxTaskRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>税款所属期（yyyyMM）。</summary>
        public int Period { get; init; }

        /// <summary>任务说明，如「同步自然人电子税局人员信息」。</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>running / success / failed，与前端「任务状态」列的取值一一对应。</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>失败时给用户的下一步建议；成功为空。</summary>
        public string? Suggestion { get; init; }

        public DateTimeOffset SubmittedAt { get; init; }

        /// <summary>任务结束时间；进行中的任务为默认值，页面显示「—」。</summary>
        public DateTimeOffset FinishedAt { get; init; }

        /// <summary>操作人：发起这次任务的登录用户显示名。</summary>
        public string Operator { get; init; } = string.Empty;
    }

    /// <summary>任务中心按提交时间区间查询的结果，时间倒序。</summary>
    public sealed class TaxTasksResultDto
    {
        public IReadOnlyList<TaxTaskRowDto> Rows { get; init; } = Array.Empty<TaxTaskRowDto>();

        /// <summary>该区间内的任务条数。</summary>
        public int Total { get; init; }
    }

    /// <summary>从税局「全量发票查询」解析出的票头；落库由 Invoices 模块完成。</summary>
    public sealed class TaxInvoiceFetchResultDto
    {
        public IReadOnlyList<TaxInvoiceRecord> Rows { get; init; } = Array.Empty<TaxInvoiceRecord>();

        public string? Message { get; init; }

        /// <summary>税局登录态缺失或已过期，网页需要弹出登录表单。</summary>
        public bool NeedLogin { get; init; }

        /// <summary>只在没有解析出数据时返回：本次抓到的 JSON 响应地址与顶层字段。</summary>
        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>一次一键取票写入发票池的结果。</summary>
    public sealed class TaxInvoiceSyncResultDto
    {
        /// <summary>税局返回并解析出的发票张数。</summary>
        public int Fetched { get; init; }

        /// <summary>本次新写入发票池的张数。</summary>
        public int Imported { get; init; }

        /// <summary>已存在或数据不完整而跳过的张数。</summary>
        public int Skipped { get; init; }

        public bool NeedLogin { get; init; }

        public string? Message { get; init; }

        public IReadOnlyList<string>? Diagnostics { get; init; }

        public IReadOnlyList<string>? Warnings { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>一键取票后台任务的进展；取数在税局无头浏览器里跑，页面按任务标识轮询。</summary>
    public sealed class TaxInvoiceSyncJobDto
    {
        public string Key { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        /// <summary>running / success / failed。</summary>
        public string Status { get; init; } = "running";

        /// <summary>税局登录态缺失：页面应弹出登录表单。</summary>
        public bool NeedLogin { get; init; }

        public string? Message { get; init; }

        /// <summary>成功时的取票结果；失败或登录态缺失时为空。</summary>
        public TaxInvoiceSyncResultDto? Data { get; init; }

        public DateTimeOffset SubmittedAt { get; init; }

        public DateTimeOffset? FinishedAt { get; init; }
    }

    /// <summary>
    /// 电子税务局「缴款信息查询」里的一条缴款记录。字段与「缴款信息查询」表格一一对应，
    /// 税局把不同费种拆成多行、同一笔缴款还会有正税与滞纳金各一行，这里不做合并。
    /// </summary>
    public sealed class TaxPaymentRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>征收项目，如增值税。</summary>
        public string Project { get; init; } = string.Empty;

        /// <summary>征收品目，如销售货物(13%)。</summary>
        public string Item { get; init; } = string.Empty;

        public string PeriodStart { get; init; } = string.Empty;

        public string PeriodEnd { get; init; } = string.Empty;

        /// <summary>实缴（退）金额；负数表示退还。</summary>
        public decimal Amount { get; init; }

        /// <summary>正税 / 滞纳金 / 罚款。</summary>
        public string TaxKind { get; init; } = string.Empty;

        /// <summary>缴款日期（入退库日期）。</summary>
        public string PaidOn { get; init; } = string.Empty;

        /// <summary>税费缴纳方式，如三方协议、银行端查询缴税。</summary>
        public string Method { get; init; } = string.Empty;

        /// <summary>票证种类，如电子缴款书。</summary>
        public string VoucherKind { get; init; } = string.Empty;

        /// <summary>票证号码（原凭证号）。</summary>
        public string VoucherNo { get; init; } = string.Empty;

        /// <summary>电子税票号码。</summary>
        public string ETicketNo { get; init; } = string.Empty;
    }

    /// <summary>缴款信息查询结果；登录态失效时由网页弹出登录表单。</summary>
    public sealed class TaxPaymentsResultDto
    {
        public IReadOnlyList<TaxPaymentRowDto> Rows { get; init; } = Array.Empty<TaxPaymentRowDto>();

        public string? Message { get; init; }

        public bool NeedLogin { get; init; }

        /// <summary>只在没有解析出数据时返回：接口回传的字段名与失败原因。</summary>
        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 「缴税」页的一条应缴税款。取自税局的申报记录（已核实的明文接口），按当期申报的
    /// 应补（退）税额列示；滞纳金与缴款状态以电子税务局为准，接口会在 Message 里说明。
    /// </summary>
    public sealed class TaxPayableRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>申报种类（税种 / 报表名称）。</summary>
        public string Kind { get; init; } = string.Empty;

        public string PeriodStart { get; init; } = string.Empty;

        public string PeriodEnd { get; init; } = string.Empty;

        /// <summary>申报期限或缴款期限。</summary>
        public string Deadline { get; init; } = string.Empty;

        /// <summary>应补（退）税额；负数表示应退。</summary>
        public decimal Refund { get; init; }

        /// <summary>应缴税额。</summary>
        public decimal Payable { get; init; }

        /// <summary>滞纳金、利息、违约金合计。</summary>
        public decimal Surcharge { get; init; }

        /// <summary>缴款状态；税局没给时为空，页面按「以税局为准」展示。</summary>
        public string Status { get; init; } = string.Empty;
    }

    /// <summary>应缴税款查询结果。</summary>
    public sealed class TaxPayablesResultDto
    {
        public IReadOnlyList<TaxPayableRowDto> Rows { get; init; } = Array.Empty<TaxPayableRowDto>();

        public string? Message { get; init; }

        public bool NeedLogin { get; init; }

        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 税局已开具的完税证明（「下载完税证明」列表的一行）。字段取自数字账户的
    /// 「证明信息查询」接口；文件本身仍需到电子税务局下载，这里只负责把可下载的证明列出来。
    /// </summary>
    public sealed class TaxCertificateRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>证明名称，如税收完税证明。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>票证号码。</summary>
        public string VoucherNo { get; init; } = string.Empty;

        /// <summary>开具税务机关。</summary>
        public string Authority { get; init; } = string.Empty;

        public string PeriodStart { get; init; } = string.Empty;

        public string PeriodEnd { get; init; } = string.Empty;

        /// <summary>入（退）库日期。</summary>
        public string PaidOn { get; init; } = string.Empty;

        /// <summary>开具金额；负数表示退还。</summary>
        public decimal Amount { get; init; }

        /// <summary>开具日期。</summary>
        public string IssuedOn { get; init; } = string.Empty;

        /// <summary>证明样式（票证种类），如税收完税证明（非印刷）。</summary>
        public string Format { get; init; } = string.Empty;
    }

    /// <summary>已开具完税证明的查询结果。</summary>
    public sealed class TaxCertificatesResultDto
    {
        public IReadOnlyList<TaxCertificateRowDto> Rows { get; init; } = Array.Empty<TaxCertificateRowDto>();

        public string? Message { get; init; }

        public bool NeedLogin { get; init; }

        public IReadOnlyList<string>? Diagnostics { get; init; }

        public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
