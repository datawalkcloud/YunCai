using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YunCai.Modules.Invoices.Domain;
using YunCai.Modules.Tax.Domain;
using YunCai.Modules.Tax.Infrastructure;
using Microsoft.Playwright;

namespace YunCai.Modules.Tax.Application
{
    /// <summary>
    /// 服务端登录电子税务局并取数。
    ///
    /// 登录由服务器上的无头 Chromium 完成：账号密码取自账套里已加密保存的税局资料，
    /// 短信验证码由网页转交，登录态（Cookie）只留在服务器，不下发到前端。
    /// 会话按「账套 + 用户」隔离，闲置 30 分钟回收；登录成功即把 storage state 加密写入数据库，
    /// 之后重新取数、服务重启都能直接恢复，只有税局自己让会话过期时才需要再收一次短信。
    /// </summary>
    public sealed class TaxPortalService : IAsyncDisposable
    {
        private static readonly TimeSpan SessionIdle = TimeSpan.FromMinutes(30);
        /// <summary>点选验证码只有几分钟有效期，给用户留足时间但别一直挂着登录任务。</summary>
        private static readonly TimeSpan CaptchaWait = TimeSpan.FromMinutes(2);
        private const int MaxCaptchaRounds = 3;
        private const int MaxSessions = 20;
        /// <summary>发票查询的翻页上限：税局的「下一页」按钮在末页可能只是禁用而不是消失，靠行数不增兜底。</summary>
        private const int MaxInvoicePages = 15;

        /// <summary>全量发票查询接口的地址片段，用来判断页面的查询/翻页请求回来没有。</summary>
        private const string InvoiceQueryMarker = "fpjcxx";

        /// <summary>直连发票接口时的路径；相对页面 origin 发，加密与签名由页面自己的请求钩子补。</summary>
        private const string InvoiceQueryPath = "/szzhzz/qlfpcx/v1/queryFpjcxx";

        /// <summary>直连时把页面默认的 20 条/页改成 500，少翻几页。</summary>
        private const int InvoiceDirectPageSize = 500;

        /// <summary>直连翻页上限，防止接口忽略页码时死循环。</summary>
        private const int InvoiceDirectMaxPages = 20;

        private static readonly Dictionary<string, string> RegionSubdomains = new(StringComparer.Ordinal)
        {
            ["北京市"] = "beijing",
            ["天津市"] = "tianjin",
            ["河北省"] = "hebei",
            ["山西省"] = "shanxi",
            ["内蒙古自治区"] = "neimenggu",
            ["辽宁省"] = "liaoning",
            ["吉林省"] = "jilin",
            ["黑龙江省"] = "heilongjiang",
            ["上海市"] = "shanghai",
            ["江苏省"] = "jiangsu",
            ["浙江省"] = "zhejiang",
            ["安徽省"] = "anhui",
            ["福建省"] = "fujian",
            ["江西省"] = "jiangxi",
            ["山东省"] = "shandong",
            ["河南省"] = "henan",
            ["湖北省"] = "hubei",
            ["湖南省"] = "hunan",
            ["广东省"] = "guangdong",
            ["广西壮族自治区"] = "guangxi",
            ["海南省"] = "hainan",
            ["重庆市"] = "chongqing",
            ["四川省"] = "sichuan",
            ["贵州省"] = "guizhou",
            ["云南省"] = "yunnan",
            ["西藏自治区"] = "xizang",
            ["陕西省"] = "shaanxi",
            ["甘肃省"] = "gansu",
            ["青海省"] = "qinghai",
            ["宁夏回族自治区"] = "ningxia",
            ["新疆维吾尔自治区"] = "xinjiang",
            ["新疆生产建设兵团"] = "xinjiangbt",
            ["大连市"] = "dalian",
            ["宁波市"] = "ningbo",
            ["厦门市"] = "xiamen",
            ["青岛市"] = "qingdao",
            ["深圳市"] = "shenzhen"
        };

        /// <summary>税局税种认定信息的标准菜单路径；各省文案略有差异，按步点击并在找不到时换后备路径。</summary>
        private static readonly string[] TaxKindsMenuPath = ["我要查询", "一户式查询", "纳税人信息查询", "税费种认定信息"];

        private static readonly string[] TaxKindsFallbackPath = ["我要办税", "综合信息报告", "应申报税费种信息报告"];

        /// <summary>
        /// 税费种认定的数据接口。和申报查询一样挂在数字账户子系统下、回明文 JSON，
        /// 体系建立后可以直接取，不必再点菜单等页面渲染。
        /// </summary>
        private const string TaxKindQueryPath = "/szc/szzh/sjswszzh/zhcx/nsrxx/v1/DescribeSfzrdxx";

        /// <summary>数字账户里的「纳税人信息查询」页；打开它就会种下子系统会话，之后的接口调用才认。</summary>
        private const string TaxKindPagePath = "/szzh/zhcx/nsrxxcx";

        /// <summary>税费种接口的分页大小与最多翻几页，防止税局忽略 pageSize 时无限翻页。</summary>
        private const int TaxKindPageSize = 500;
        private const int MaxTaxKindPages = 20;

        /// <summary>
        /// 「申报信息查询」的菜单路径。它同时是数字账户子系统的入口：
        /// 申报接口只有在这个页面被打开过、子系统种下自己的会话之后才认。
        /// </summary>
        private static readonly string[] DeclarationMenuPath = ["我要查询", "一户式查询", "申报信息查询"];

        /// <summary>数字账户子系统的页面路径前缀，用来确认新标签页确实进了申报信息查询。</summary>
        private const string DeclarationAppPath = "/szzh/";

        /// <summary>
        /// 申报查询接口的分页大小。该接口只认小写的 pageSize（大写的 PageNumber/PageSize 会被忽略、
        /// 固定按 10 条返回），而且 pageNumber 根本不生效，只能一次把整年取回。
        /// </summary>
        private const int DeclarationPageSize = 500;

        /// <summary>报表查询最多分几段取数：每段都要占着税局会话跑一次接口，区间太长要挡回来。</summary>
        private const int MaxReportWindows = 3;

        /// <summary>
        /// 缴款信息查询接口与它所在的业务页。和申报查询一样挂在数字账户子系统下，
        /// 业务页加载时种下子系统会话，之后在页面里直接 POST 就能拿到明文 JSON。
        /// </summary>
        private const string PaymentQueryPath = "/szc/szzh/sjswszzh/zhcx/jkxx/v1/DescribeJkxxcx";
        private const string PaymentPagePath = "/szzh/zhcx/jkxx/jkxxcx";

        /// <summary>已开具完税证明接口与它所在的「证明信息查询」页。</summary>
        private const string CertificateQueryPath = "/szc/szzh/sjswszzh/zhcx/wszm/v1/DescribeWszmxx";
        private const string CertificatePagePath = "/szzh/zhcx/zmxxcx";

        /// <summary>两个直连接口的分页大小与翻页上限，防止税局忽略页码时无限翻页。</summary>
        private const int PaymentQueryPageSize = 500;
        private const int MaxPaymentQueryPages = 20;
        private const int CertificateQueryPageSize = 500;
        private const int MaxCertificateQueryPages = 20;

        private const string ChromeUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";

        /// <summary>
        /// 个税站（自然人电子税务局）的上下文用与内核一致的 UA，避免 UA 串与 UA-CH 元数据自相矛盾。
        /// 实测：瑞数会把部分响应改写成 dmnHafHu 令牌串、首屏也可能先回一个 404 挑战页再自动重载，
        /// 但这些都不影响登录主流程——滑块通过后税局照常下发「安全验证」短信。
        /// 页面里常驻着「验证失败请重新滑动验证」这类模板文案，不能据此判定滑块失败。
        /// </summary>
        private static string IitUserAgent(IBrowser browser) =>
            $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{browser.Version} Safari/537.36";

        private readonly ConcurrentDictionary<TaxPortalSessionKey, TaxPortalSession> _sessions = new();
        private readonly SemaphoreSlim _browserGate = new(1, 1);
        /// <summary>同一账套被多个页面同时取数时，只恢复一次登录态，避免开出一堆重复的浏览器上下文。</summary>
        private readonly SemaphoreSlim _restoreGate = new(1, 1);
        private readonly Timer _purgeTimer;
        private readonly ILogger<TaxPortalService> _logger;
        private readonly TaxPortalSessionStore _stateStore;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private bool _disposed;

        public TaxPortalService(ILogger<TaxPortalService> logger, TaxPortalSessionStore stateStore)
        {
            _logger = logger;
            _stateStore = stateStore;
            _purgeTimer = new Timer(_ => _ = PurgeIdleAsync(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>发起登录：立即返回会话，登录过程在后台推进，网页轮询状态。</summary>
        public TaxPortalLoginStatusDto StartLogin(TaxPortalSessionKey key, TaxPortalCredentials credentials)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DiscardSession(key);

            var session = new TaxPortalSession(key);
            _sessions[key] = session;
            TrimSessions();
            _ = Task.Run(() => StartLoginCoreAsync(session, credentials));
            return session.Snapshot();
        }

        public TaxPortalLoginStatusDto GetStatus(TaxPortalSessionKey key)
        {
            return TryGetSession(key)?.Snapshot() ?? new TaxPortalLoginStatusDto
            {
                State = TaxPortalStates.Failed,
                Message = "登录会话已失效，请重新点击「获取验证码」。"
            };
        }

        /// <summary>
        /// 进入税局相关页面时确认登录态还能不能用，判据与取数完全一致：内存里的会话只有已登录才算数，
        /// 正在登录中（等点选、等短信）同样算未登录；内存里没有会话时先用数据库里保存的登录态恢复一次
        /// （服务重启、闲置回收之后也能免登录）。恢复不了说明税局那边已过期，返回 Failed 让网页弹窗重新收一次短信。
        /// </summary>
        public async Task<TaxPortalLoginStatusDto> ResumeAsync(TaxPortalSessionKey key, CancellationToken cancellationToken)
        {
            var usable = await GetUsableSessionAsync(key, cancellationToken);
            if (usable is not null)
            {
                return usable.Snapshot();
            }

            return new TaxPortalLoginStatusDto
            {
                State = TaxPortalStates.Failed,
                Message = "税局登录状态已失效，请重新登录电子税务局。"
            };
        }

        /// <summary>
        /// 把当前登录态导出给本机助手：只带税局域名下的 Cookie 与 localStorage，
        /// 助手注入自己的浏览器后再打开企业电子税务局，用户不必在助手窗口里重新登录。
        /// 内存里没有会话时先按磁盘存档恢复一次；税局侧已过期返回 null，由网页引导重新登录。
        /// </summary>
        public async Task<TaxPortalBrowserHandoffDto?> CreateBrowserHandoffAsync(
            TaxPortalSessionKey key,
            string landingUrl,
            CancellationToken cancellationToken)
        {
            var session = await GetUsableSessionAsync(key, cancellationToken);
            var context = session?.Context;
            if (context is null)
            {
                return null;
            }

            string state;
            try
            {
                state = await context.StorageStateAsync().WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                _logger.LogWarning("[税局登录] 导出登录态失败：{Message}", ex.Message);
                return null;
            }

            return ParseBrowserHandoff(state, landingUrl);
        }

        /// <summary>从 Playwright 的 storage state 里取税局域名下的 Cookie 与 localStorage。</summary>
        private static TaxPortalBrowserHandoffDto ParseBrowserHandoff(string storageState, string landingUrl)
        {
            var cookies = new List<TaxPortalBrowserCookieDto>();
            var origins = new List<TaxPortalBrowserOriginDto>();
            using var document = JsonDocument.Parse(storageState);
            var root = document.RootElement;

            if (root.TryGetProperty("cookies", out var cookieArray) && cookieArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cookieArray.EnumerateArray())
                {
                    var name = ReadString(item, "name");
                    var domain = ReadString(item, "domain");
                    if (name.Length == 0 || !IsTaxDomain(domain))
                    {
                        continue;
                    }

                    var path = ReadString(item, "path");
                    var sameSite = ReadString(item, "sameSite");
                    cookies.Add(new TaxPortalBrowserCookieDto
                    {
                        Name = name,
                        Value = ReadString(item, "value"),
                        Domain = domain,
                        Path = path.Length > 0 ? path : "/",
                        Expires = item.TryGetProperty("expires", out var expires) && expires.TryGetDouble(out var seconds)
                            ? seconds
                            : -1,
                        HttpOnly = item.TryGetProperty("httpOnly", out var httpOnly) && httpOnly.ValueKind == JsonValueKind.True,
                        Secure = item.TryGetProperty("secure", out var secure) && secure.ValueKind == JsonValueKind.True,
                        SameSite = sameSite.Length > 0 ? sameSite : "Lax"
                    });
                }
            }

            if (root.TryGetProperty("origins", out var originArray) && originArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in originArray.EnumerateArray())
                {
                    var origin = ReadString(item, "origin");
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || !IsTaxDomain(uri.Host))
                    {
                        continue;
                    }

                    var items = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (item.TryGetProperty("localStorage", out var storage) && storage.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var pair in storage.EnumerateArray())
                        {
                            var name = ReadString(pair, "name");
                            if (name.Length > 0)
                            {
                                items[name] = ReadString(pair, "value");
                            }
                        }
                    }

                    if (items.Count > 0)
                    {
                        origins.Add(new TaxPortalBrowserOriginDto { Origin = origin, Items = items });
                    }
                }
            }

            return new TaxPortalBrowserHandoffDto { Url = landingUrl, Cookies = cookies, Origins = origins };
        }

        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        /// <summary>交接只允许税局域名，避免把浏览器里别的站点 Cookie 一并带出去。</summary>
        private static bool IsTaxDomain(string host)
        {
            var value = host.TrimStart('.');
            return value.Equals("chinatax.gov.cn", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".chinatax.gov.cn", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>提交短信验证码；成功会保留登录态，后续取数复用同一个浏览器上下文。</summary>
        public async Task<TaxPortalLoginStatusDto> VerifyAsync(TaxPortalSessionKey key, string code, CancellationToken cancellationToken)
        {
            var session = RequireSession(key, "登录会话已失效，请重新点击「获取验证码」。");
            if (string.IsNullOrWhiteSpace(code) || !Regex.IsMatch(code.Trim(), @"^\d{4,8}$"))
            {
                throw new ArgumentException("请输入税局短信里的数字验证码。");
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (session.State == TaxPortalStates.Success)
                {
                    return session.Snapshot();
                }

                if (session.State != TaxPortalStates.SmsRequired)
                {
                    throw new InvalidOperationException("当前登录步骤还不需要验证码，请稍候。");
                }

                var page = session.Page ?? throw new InvalidOperationException("税局页面已关闭，请重新点击「获取验证码」。");
                var filled = await EvaluateJsonAsync(page, TaxPortalPageScripts.FillSms(code.Trim()), cancellationToken);
                if (filled?["filled"]?.GetValue<bool>() != true)
                {
                    if (await IsSuccessAsync(page, cancellationToken))
                    {
                        await MarkSuccessAsync(session);
                        return session.Snapshot();
                    }

                    throw new InvalidOperationException("税局页面没有找到短信验证码输入框，请重新获取验证码。");
                }

                session.SetState(TaxPortalStates.Starting, "正在校验短信验证码…");
                await EvaluateBoolAsync(page, TaxPortalPageScripts.ClickConfirm, cancellationToken);

                if (await WaitForSuccessAsync(page, TimeSpan.FromSeconds(45), cancellationToken))
                {
                    await MarkSuccessAsync(session);
                    return session.Snapshot();
                }

                var error = await ReadErrorTextAsync(page, cancellationToken);
                session.SetState(
                    TaxPortalStates.SmsRequired,
                    string.IsNullOrWhiteSpace(error) ? "验证码未通过或已过期，请重新输入或重新获取。" : error);
                return session.Snapshot();
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>提交网页上完成的文字点选；后台登录任务正等着这一组归一化坐标。</summary>
        public TaxPortalLoginStatusDto SubmitCaptcha(TaxPortalSessionKey key, IReadOnlyList<TaxPortalCaptchaClick>? clicks)
        {
            var session = RequireSession(key, "登录会话已失效，请重新点击「获取验证码」。");
            var points = clicks ?? Array.Empty<TaxPortalCaptchaClick>();
            if (points.Count is < 1 or > 6)
            {
                throw new ArgumentException("请按照提示顺序点选全部文字后再提交。");
            }

            if (points.Any(point => point.X is < 0 or > 1 || point.Y is < 0 or > 1))
            {
                throw new ArgumentException("验证码点选位置无效，请重新点选。");
            }

            if (!session.TrySubmitCaptcha(points))
            {
                throw new InvalidOperationException("当前没有等待点选的税局验证码，请稍候。");
            }

            return session.Snapshot();
        }

        /// <summary>
        /// 读取税费种认定：在已登录的会话里打开「税费种」页，解析页面自身请求回来的 JSON。
        /// 内存里没有会话时先用数据库里的登录态恢复（服务重启、闲置回收之后都能免登录取数），
        /// 恢复不了就返回 NeedLogin，由网页弹出登录表单重新收一次短信。
        /// 报文里没有申报种类，取回的行按账套的纳税人资格与会计制度补全。
        /// </summary>
        public async Task<TaxKindsResultDto> FetchTaxKindsAsync(
            TaxPortalSessionKey key,
            TaxKindProfile profile,
            CancellationToken cancellationToken)
        {
            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session is null)
            {
                return NeedLoginResult("税局登录状态已过期，请重新登录电子税务局。");
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (session.State != TaxPortalStates.Success || session.Page is null)
                {
                    return NeedLoginResult("请先完成税局登录，再更新税费种认定。");
                }

                var page = session.Page;

                // 先直接在门户页面里调接口：省掉「点菜单 + 等渲染 + 读表格」的十秒级开销。
                // 子系统会话还没建立或接口不认时返回 null，再走下面的菜单兜底。
                if (await FetchTaxKindsDirectAsync(session, page, profile, cancellationToken) is { Rows.Count: > 0 } direct)
                {
                    session.Touch();
                    return direct;
                }

                var collector = new JsonResponseCollector();
                bool entered;
                using (new PageResponseWatch(session.Context, page, collector))
                {
                    entered = await TryOpenTaxKindsAsync(page, cancellationToken);
                    await Task.Delay(3000, cancellationToken);
                }

                if (await IsLoginPageAsync(page, cancellationToken))
                {
                    // 页面被税局踢回登录页：服务端存着的 Cookie 已失效，删掉存档让网页重新收短信。
                    _logger.LogInformation("[税局取数] 会话 {SessionId} 已被税局踢回登录页，清理登录态", session.Id);
                    await EndSessionAsync(key, session);
                    return NeedLoginResult("税局登录状态已过期，请重新登录电子税务局。");
                }

                var payloads = (await collector.ReadAsync(_logger, cancellationToken)).ToList();
                var parsed = TaxKindPayloadParser.Parse(payloads);
                if (parsed.Rows.Count == 0)
                {
                    // 报文被瑞数改写成密文时网络层拿不到 JSON，但税局页面自己解密后照样把认定表画了出来，改从表格里读。
                    payloads.AddRange(await ReadRenderedTablesAsync(session, page, cancellationToken));
                    parsed = TaxKindPayloadParser.Parse(payloads);
                }
                session.Touch();

                if (parsed.Rows.Count > 0)
                {
                    return new TaxKindsResultDto { Rows = WithDeclareKinds(parsed.Rows, profile), LevyType = parsed.LevyType };
                }

                // 解析规则要靠真实报文写，不能猜字段名：解析不出行时留一行原文当证据。
                // 只留带税费种字段的报文——登录类报文带姓名手机号，不该进日志。
                foreach (var payload in payloads.Where(LooksLikeTaxKind))
                {
                    _logger.LogWarning(
                        "[税局取数·报文] {Url} {Body}",
                        payload.Url.Split('?')[0],
                        payload.Body.Length > 600 ? payload.Body[..600] : payload.Body);
                }

                if (!entered)
                {
                    var outline = await DescribeMenusAsync(page, cancellationToken);
                    _logger.LogWarning("[税局取数] 没有找到税费种菜单，页面结构：{Outline}", outline);
                    return new TaxKindsResultDto
                    {
                        Rows = Array.Empty<TaxKindRowDto>(),
                        Message = "登录后没有找到「税费种认定信息」菜单，税局页面结构可能已调整。",
                        Diagnostics = [outline]
                    };
                }

                // 一条 JSON 都没抓到时的诊断是空的，光看「没解析出来」分不清是菜单没进去、新标签页没盯住，还是报文全是密文。
                var evidence = collector.Evidence(payloads.Count);
                _logger.LogWarning(
                    "[税局取数] 会话 {SessionId} 未解析出税费种，进入页面={Entered}，字段={Fields}，{Evidence}",
                    session.Id,
                    entered,
                    string.Join(" / ", parsed.Diagnostics),
                    string.Join(" ", evidence));

                return new TaxKindsResultDto
                {
                    Rows = Array.Empty<TaxKindRowDto>(),
                    LevyType = parsed.LevyType,
                    Message = "已打开税局税费种页面，但没有识别出认定信息。请把服务器日志里「[税局取数]」那一行发给开发补解析规则。",
                    Diagnostics = parsed.Diagnostics.Concat(evidence).Take(20).ToList()
                };
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>
        /// 在门户页面里直接调税费种认定接口并翻页取全。返回 null 表示这条捷径走不通
        /// （子系统会话没建立、被踢回登录页、报文不是 JSON），调用方继续走点菜单的兜底路径。
        /// </summary>
        private async Task<TaxKindsResultDto?> FetchTaxKindsDirectAsync(
            TaxPortalSession session,
            IPage page,
            TaxKindProfile profile,
            CancellationToken cancellationToken)
        {
            var saved = await _stateStore.LoadAsync(session.Key, cancellationToken);
            var homeUri = saved is not null && Uri.TryCreate(saved.HomeUrl, UriKind.Absolute, out var savedUri)
                ? savedUri
                : null;
            var homeHost = homeUri?.Host;
            var pageUri = Uri.TryCreate(page.Url, UriKind.Absolute, out var current) ? current : null;

            // 恢复出来的会话常常停在 loginb，那里发接口会被拒，必须先落到数字账户的业务页：
            // 子系统会话是页面加载时自己种的，跳到业务路由就等于把它补上了，不用再点菜单。
            var baseUri = homeUri ?? pageUri;
            if (baseUri is null)
            {
                return null;
            }

            // 目前只在浙江的数字账户子站上验证过这套接口；其他省继续走菜单，免得每次白等一轮重试。
            if (!baseUri.Host.Contains("zhejiang", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (pageUri is null
                || !string.Equals(pageUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
                || !pageUri.AbsolutePath.StartsWith("/szzh", StringComparison.OrdinalIgnoreCase))
            {
                await NavigateAsync(page, new Uri(baseUri, TaxKindPagePath).ToString(), cancellationToken);
                await Task.Delay(800, cancellationToken);
            }

            var frame = FindPortalFrame(page, homeHost);
            var payloads = new List<CapturedJsonPayload>();
            var fetched = 0;
            int? total = null;
            for (var pageNumber = 1; pageNumber <= MaxTaxKindPages; pageNumber++)
            {
                var url = $"{TaxKindQueryPath}?pageNum={pageNumber}&pageSize={TaxKindPageSize}"
                    + "&djxh=&zsxm=&zfsbz=&dqsfyx=Y";
                // 税局页面自己是 POST 发的（空请求体），换成 GET 会被网关回 InternalError；
                // X-Client-ID 也照抄页面，否则数字账户不认。
                var expression = "(async()=>{const r=await fetch('" + url + "',{method:'POST',"
                    + "headers:{'Accept':'application/json;charset=UTF-8','X-Client-ID':'fkh_mall'},"
                    + "credentials:'include'});return await r.text();})()";
                // 刚落到业务页时前端还在初始化，头一两次请求可能抛 Failed to fetch 或回 HTML；
                // 隔几百毫秒重试，比固定等几秒稳，也不会在已经就绪时白等。
                var raw = "";
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        raw = await FetchAsync(frame, expression, cancellationToken);
                    }
                    catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                    {
                        raw = "";
                        _logger.LogInformation("[税局取数] 税费种接口直连第 {Attempt} 次失败：{Message}", attempt, ex.Message);
                    }

                    if (raw.Length > 0 && raw[0] is '{' or '[')
                    {
                        break;
                    }

                    await Task.Delay(700, cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(raw) || raw[0] is not ('{' or '['))
                {
                    _logger.LogInformation("[税局取数] 税费种接口直连拿不到 JSON，改用菜单路径：{Body}", Clip(raw, 200));
                    return null;
                }

                payloads.Add(new CapturedJsonPayload(url, raw));
                var (pageTotal, pageSize) = ReadPageCounts(raw);
                total ??= pageTotal;
                fetched += pageSize ?? 0;

                // 税局有时按自己的上限截断 pageSize；以它给的 Total 为准，取够就停。
                if (pageSize is null or 0
                    || (total is not null && fetched >= total)
                    || (total is null && pageSize < TaxKindPageSize))
                {
                    break;
                }
            }

            var parsed = TaxKindPayloadParser.Parse(payloads);
            if (parsed.Rows.Count == 0)
            {
                _logger.LogInformation(
                    "[税局取数] 税费种接口直连取回 {Count} 份报文但没解析出认定行，改用菜单路径",
                    payloads.Count);
            }

            return parsed.Rows.Count > 0
                ? new TaxKindsResultDto { Rows = WithDeclareKinds(parsed.Rows, profile), LevyType = parsed.LevyType }
                : null;
        }

        /// <summary>
        /// 报文里没有申报种类时按征收项目、期限与账套资格补出申报表名；税局哪天回了真实种类就不要覆盖它。
        /// </summary>
        private static IReadOnlyList<TaxKindRowDto> WithDeclareKinds(IReadOnlyList<TaxKindRowDto> rows, TaxKindProfile profile)
        {
            if (rows.All(row => row.DeclareKind.Length > 0))
            {
                return rows;
            }

            return rows.Select(row => row.DeclareKind.Length > 0 ? row : new TaxKindRowDto
            {
                Key = row.Key,
                Project = row.Project,
                DeclareKind = TaxDeclareKindMapper.Map(row.Project, row.Period, row.Option, profile),
                Period = row.Period,
                Option = row.Option,
                ValidFrom = row.ValidFrom,
                ValidTo = row.ValidTo,
                Source = row.Source,
                Items = row.Items,
            }).ToList();
        }

        /// <summary>读数字账户接口回的分页信息；结构不是预期的 {"Response":{"Data":{...}}} 时返回空。</summary>
        private static (int? Total, int? PageSize) ReadPageCounts(string raw)
        {
            try
            {
                if (JsonNode.Parse(raw)?["Response"]?["Data"] is JsonObject data
                    && data["Total"] is JsonValue totalValue
                    && totalValue.TryGetValue<int>(out var total))
                {
                    return data["List"] is JsonArray list ? (total, list.Count) : (total, null);
                }
            }
            catch (JsonException)
            {
            }

            return (null, null);
        }

        /// <summary>报文里有没有税费种的字段；用它把登录类报文（带姓名、手机号）挡在日志之外。</summary>
        private static bool LooksLikeTaxKind(CapturedJsonPayload payload)
        {
            var text = payload.Url + payload.Body;
            return text.Contains("税费种", StringComparison.Ordinal)
                || text.Contains("zsxm", StringComparison.OrdinalIgnoreCase)
                || text.Contains("zspm", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 读取所选税款所属期的申报记录：在税局页面里直接调用「申报信息查询」接口（DescribeSbmxcx2）。
        /// 接口只按「所属期起」落在查询区间内过滤，所以按整个年度一次取回，再在本地保留与所选月份
        /// 有交集的行——季报覆盖整季、年报覆盖全年，都会出现在对应的每一个月里。
        /// </summary>
        public async Task<TaxDeclarationsResultDto> FetchDeclarationsAsync(
            TaxPortalSessionKey key,
            int year,
            int month,
            CancellationToken cancellationToken)
        {
            var monthStart = new DateOnly(year, month, 1);
            var (raw, failure) = await FetchDeclarationPayloadAsync(
                key, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), cancellationToken);
            if (failure is not null)
            {
                return failure;
            }

            var result = TaxDeclarationPayloadParser.Parse(raw!, monthStart, monthStart.AddMonths(1).AddDays(-1));
            if (result.Rows.Count == 0)
            {
                _logger.LogWarning(
                    "[税局取数] 未取到 {Year} 年 {Month} 月的申报记录：{Diagnostics}",
                    year,
                    month,
                    string.Join(" / ", result.Diagnostics ?? Array.Empty<string>()));
            }

            return result;
        }

        /// <summary>
        /// 按申报日期区间读取申报表列表，供「报表查询」页使用。
        /// 税局接口只按所属期过滤，且规定所属期起止不能超过一年，而上年度的年报、残保金常常今年才申报，
        /// 所以把所属期窗口往前多带一个自然年、按年分段逐个取回，合并后再按申报日期筛。
        /// </summary>
        public async Task<TaxDeclarationsResultDto> FetchDeclarationReportsAsync(
            TaxPortalSessionKey key,
            DateOnly declareFrom,
            DateOnly declareTo,
            CancellationToken cancellationToken)
        {
            var windows = PeriodWindows(new DateOnly(declareFrom.Year - 1, 1, 1), declareTo);
            if (windows.Count > MaxReportWindows)
            {
                // 每段都要在无头浏览器里跑一次税局接口，且全程占着税局会话；区间太长直接挡回来。
                return new TaxDeclarationsResultDto
                {
                    Message = $"查询区间要分 {windows.Count} 段取数，最多支持 {MaxReportWindows} 段，请把申报日期区间缩短后再查。"
                };
            }

            var bodies = new List<string>(windows.Count);
            foreach (var (periodFrom, periodTo) in windows)
            {
                var (raw, failure) = await FetchDeclarationPayloadAsync(key, periodFrom, periodTo, cancellationToken);
                if (failure is not null)
                {
                    return failure;
                }

                bodies.Add(raw!);
            }

            var result = TaxDeclarationPayloadParser.ParseReports(bodies, declareFrom, declareTo);
            if (result.Rows.Count == 0)
            {
                _logger.LogWarning(
                    "[税局取数] 未取到 {From:yyyy-MM-dd} 至 {To:yyyy-MM-dd} 期间的申报表：{Diagnostics}",
                    declareFrom,
                    declareTo,
                    string.Join(" / ", result.Diagnostics ?? Array.Empty<string>()));
            }

            return result;
        }

        /// <summary>按自然年把所属期窗口切成不超过一年的若干段，对应税局「所属期起止不能超过一年」的限制。</summary>
        private static List<(DateOnly From, DateOnly To)> PeriodWindows(DateOnly from, DateOnly to)
        {
            var windows = new List<(DateOnly From, DateOnly To)>();
            for (var year = from.Year; year <= to.Year; year++)
            {
                windows.Add((
                    year == from.Year ? from : new DateOnly(year, 1, 1),
                    year == to.Year ? to : new DateOnly(year, 12, 31)));
            }

            return windows;
        }

        /// <summary>
        /// 在已登录的税局页面里按所属期起区间调一次「申报信息查询」，返回税局的原始响应正文。
        /// 取不到报文时（没有登录态、被踢回登录页、浏览器故障）第二个返回值就是直接回给页面的结果。
        /// </summary>
        private async Task<(string? Raw, TaxDeclarationsResultDto? Failure)> FetchDeclarationPayloadAsync(
            TaxPortalSessionKey key,
            DateOnly periodFrom,
            DateOnly periodTo,
            CancellationToken cancellationToken)
        {
            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session is null)
            {
                return (null, new TaxDeclarationsResultDto
                {
                    NeedLogin = true,
                    Message = "税局登录状态已过期，请重新登录电子税务局。"
                });
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (session.State != TaxPortalStates.Success || session.Page is null)
                {
                    return (null, new TaxDeclarationsResultDto
                    {
                        NeedLogin = true,
                        Message = "请先完成税局登录，再查询申报记录。"
                    });
                }

                var page = session.Page;
                var saved = await _stateStore.LoadAsync(key, cancellationToken);
                var homeHost = saved is not null && Uri.TryCreate(saved.HomeUrl, UriKind.Absolute, out var savedUri)
                    ? savedUri.Host
                    : null;
                var pageUri = Uri.TryCreate(page.Url, UriKind.Absolute, out var current) ? current : null;

                // 页面可能停在发票等第三方税局子站：申报接口只挂在电子税务局门户域名下，先回门户。
                if (saved is not null
                    && homeHost is not null
                    && (pageUri is null || !string.Equals(pageUri.Host, homeHost, StringComparison.OrdinalIgnoreCase)))
                {
                    await NavigateAsync(page, saved.HomeUrl, cancellationToken);
                    await Task.Delay(1200, cancellationToken);
                }

                var expression = "(async()=>{const r=await fetch('/szc/szzh/sjswszzh/zhcx/v1/DescribeSbmxcx2',"
                    + "{method:'POST',headers:{'Content-Type':'application/json'},credentials:'include',"
                    + $"body:JSON.stringify({{pageNumber:1,pageSize:{DeclarationPageSize},"
                    + $"skssqq:'{periodFrom:yyyy-MM-dd}',skssqz:'{periodTo:yyyy-MM-dd}'}})}});"
                    + "return await r.text();})()";

                var frame = FindPortalFrame(page, homeHost);
                var raw = await FetchAsync(frame, expression, cancellationToken);

                // 申报接口挂在「数字账户」子系统下，浏览器没进过它的页面时该子系统没有会话，
                // 税局网关会回 9999。按菜单打开一次申报信息查询页补上会话，再取一次。
                if (TaxDeclarationPayloadParser.NeedsAppSession(raw))
                {
                    _logger.LogInformation(
                        "[税局取数] 会话 {SessionId} 缺少数字账户子系统会话，按菜单打开「申报信息查询」后重试",
                        session.Id);
                    if (await OpenDeclarationAppAsync(session, cancellationToken))
                    {
                        raw = await FetchAsync(frame, expression, cancellationToken);
                    }
                }

                if (await IsLoginPageAsync(page, cancellationToken))
                {
                    _logger.LogInformation("[税局取数] 会话 {SessionId} 查询申报记录时被踢回登录页", session.Id);
                    await EndSessionAsync(key, session);
                    return (null, new TaxDeclarationsResultDto
                    {
                        NeedLogin = true,
                        Message = "税局登录状态已过期，请重新登录电子税务局。"
                    });
                }

                if (TaxDeclarationPayloadParser.NeedsAppSession(raw))
                {
                    // 补过会话仍然被拒：多半是门户登录态本身出了问题，交给网页引导重新登录。
                    _logger.LogWarning("[税局取数] 会话 {SessionId} 补建数字账户会话后仍被税局拒绝：{Body}", session.Id, raw);
                    return (null, new TaxDeclarationsResultDto
                    {
                        NeedLogin = true,
                        Message = "税局数字账户拒绝返回申报明细，请重新登录电子税务局后再试。",
                        Diagnostics = [Clip(raw, 300)]
                    });
                }

                session.Touch();
                return (raw, null);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                _logger.LogWarning("[税局取数] 查询申报记录失败：{Message}", ex.Message);
                return (null, new TaxDeclarationsResultDto
                {
                    Message = "读取税局申报记录失败，请稍后重试。",
                    Diagnostics = [ex.Message]
                });
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>在门户页面里调一次申报查询接口，返回税局的原始响应正文。</summary>
        private static async Task<string> FetchAsync(IFrame frame, string expression, CancellationToken cancellationToken)
        {
            return await frame.EvaluateAsync<string>(expression)
                .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken) ?? "";
        }

        /// <summary>
        /// 按菜单打开一次「申报信息查询」。税局会在新的标签页里进入数字账户子系统，
        /// 该子系统自己的会话就是在这一步种下的，之后门户页面里的接口调用才认。
        /// </summary>
        private async Task<bool> OpenDeclarationAppAsync(TaxPortalSession session, CancellationToken cancellationToken)
        {
            if (session.Page is not { } page)
            {
                return false;
            }

            var context = session.Context;
            var before = context?.Pages.ToList() ?? [];
            if (!await ClickMenuPathAsync(page, DeclarationMenuPath, cancellationToken))
            {
                _logger.LogWarning("[税局取数] 没有找到「申报信息查询」菜单，无法补建数字账户会话");
                return false;
            }

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                var opened = context?.Pages.FirstOrDefault(
                    item => !before.Contains(item)
                        && item.Url.Contains(DeclarationAppPath, StringComparison.OrdinalIgnoreCase));
                if (opened is not null)
                {
                    // 页面首屏自己发出的那几个请求才是种会话的那一步，等它们跑完。
                    await Task.Delay(2500, cancellationToken);
                    return true;
                }

                await Task.Delay(500, cancellationToken);
            }

            _logger.LogWarning("[税局取数] 已点击「申报信息查询」，但税局没有打开数字账户页面");
            return false;
        }

        /// <summary>一次性排查：打开「未申报查询」并回传期间抓到的接口请求与报文。</summary>
        public async Task<TaxDeclarationsResultDto> ProbeUndeclaredAsync(
            TaxPortalSessionKey key,
            CancellationToken cancellationToken)
        {
            var notes = new List<string>();
            const string pagePath = "/szzh/zhcx/sbxx/wsbcx";
            const string queryPath = "/szc/szzh/sjswszzh/zhcx/v1/DescribeQnsbwcx";
            const string body = "{\"nsqx\":\"\",\"sbqxq\":\"\",\"sbqxz\":\"\",\"skssqq\":\"\",\"skssqz\":\"\","
                + "\"zsdlfs\":\"\",\"zspm\":\"\",\"zsxm\":\"\",\"pageNum\":1,\"pageSize\":20}";

            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session is null || session.Page is not { } page)
            {
                return new TaxDeclarationsResultDto { NeedLogin = true, Message = "没有可用的税局会话。" };
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                var saved = await _stateStore.LoadAsync(key, cancellationToken);
                var homeUri = saved is not null && Uri.TryCreate(saved.HomeUrl, UriKind.Absolute, out var parsed)
                    ? parsed
                    : null;
                var pageUri = Uri.TryCreate(page.Url, UriKind.Absolute, out var current) ? current : null;
                var baseUri = homeUri ?? pageUri;
                notes.Add($"起始={page.Url} home={saved?.HomeUrl}");
                if (baseUri is null)
                {
                    return new TaxDeclarationsResultDto { Diagnostics = notes };
                }

                if (homeUri is not null
                    && !string.Equals(pageUri?.Host, homeUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    await NavigateAsync(page, homeUri.ToString(), cancellationToken);
                    await Task.Delay(1500, cancellationToken);
                }

                await OpenDigitalAccountPageAsync(page, baseUri, pagePath, cancellationToken);
                await Task.Delay(6000, cancellationToken);
                notes.Add($"业务页={page.Url}");
                notes.Add("帧: " + string.Join(" | ", page.Frames.Select(f => f.Url.Split('?')[0]).Take(8)));

                var frame = FindPortalFrame(page, homeUri?.Host);
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    foreach (var withClient in new[] { true, false })
                    {
                        var expression = "(async()=>{const r=await fetch('" + queryPath + "',{method:'POST',"
                            + "headers:{'Content-Type':'application/json'"
                            + (withClient ? ",'Accept':'application/json;charset=UTF-8','X-Client-ID':'fkh_mall'" : string.Empty)
                            + "},credentials:'include',body:JSON.stringify(" + body + ")});return await r.text();})()";
                        var raw = await FetchAsync(frame, expression, cancellationToken);
                        notes.Add($"#{attempt} client={withClient} 帧={frame.Url.Split('?')[0]} :: {Clip(raw, 900)}");
                        if (!TaxDeclarationPayloadParser.NeedsAppSession(raw) && raw.TrimStart().StartsWith('{'))
                        {
                            return new TaxDeclarationsResultDto { Diagnostics = notes };
                        }
                    }

                    await Task.Delay(4000, cancellationToken);
                }

                return new TaxDeclarationsResultDto { Diagnostics = notes };
            }
            finally
            {
                session.Gate.Release();
            }
        }

        // ===== 缴款信息 / 应缴税款 / 已开具完税证明 =====
        //
        // 缴款记录与已开具证明都走数字账户的明文接口：在已登录的缴款信息查询 / 证明信息查询页里
        // 直接 POST，由页面自己的钩子完成瑞数的加密与签名，不再读页面渲染出来的表格。
        // 应缴税款没有独立的待缴清单接口，按已核实的申报记录接口取当期申报的应补（退）税额。

        /// <summary>直连查询失败的统一结果；NeedLogin 时页面弹登录框，其余字段只用于说明与排查。</summary>
        private sealed record DirectQueryFailure(
            bool NeedLogin,
            string Message,
            IReadOnlyList<string> Diagnostics);

        /// <summary>读取「缴款信息查询」：按缴款日期区间直连税局接口取回已入库的缴款记录。</summary>
        public async Task<TaxPaymentsResultDto> FetchPaymentsAsync(
            TaxPortalSessionKey key,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken)
        {
            var payloads = new List<CapturedJsonPayload>();
            var failure = await CollectDirectQueryPagesAsync(
                key,
                PaymentPagePath,
                PaymentQueryPath,
                "缴款信息",
                pageNumber => $"{{\"jkrqq\":\"{from:yyyy-MM-dd}\",\"jkrqz\":\"{to:yyyy-MM-dd}\","
                    + "\"skssqq\":\"\",\"skssqz\":\"\",\"skzlDm\":\"\",\"zspmDm\":\"\",\"zsxmDm\":\"\","
                    + $"\"pageNum\":{pageNumber},\"pageSize\":{PaymentQueryPageSize}}}",
                MaxPaymentQueryPages,
                payloads,
                cancellationToken);

            if (failure is not null)
            {
                return new TaxPaymentsResultDto
                {
                    NeedLogin = failure.NeedLogin,
                    Message = failure.Message,
                    Diagnostics = failure.Diagnostics.Count > 0 ? failure.Diagnostics : null
                };
            }

            var result = TaxPaymentPayloadParser.ParsePayments(payloads, from, to);
            if (result.Rows.Count > 0)
            {
                return result;
            }

            _logger.LogWarning(
                "[税局取数] 未取到 {From:yyyy-MM-dd} 至 {To:yyyy-MM-dd} 的缴款记录：{Diagnostics}",
                from,
                to,
                string.Join(" / ", result.Diagnostics ?? Array.Empty<string>()));
            return result;
        }

        /// <summary>
        /// 读取「缴税」页的应缴税款。税局没有独立的待缴清单接口，这里按已核实的申报记录接口
        /// 取当期申报，列示应补（退）税额；缴款状态、滞纳金与利息以电子税务局为准。
        /// </summary>
        public async Task<TaxPayablesResultDto> FetchPayablesAsync(
            TaxPortalSessionKey key,
            int year,
            int month,
            CancellationToken cancellationToken)
        {
            var declarations = await FetchDeclarationsAsync(key, year, month, cancellationToken);
            if (declarations.NeedLogin)
            {
                return new TaxPayablesResultDto { NeedLogin = true, Message = declarations.Message };
            }

            var rows = declarations.Rows
                .Where(row => row.Status != "已作废" && (row.Payable != 0m))
                .Select(row => new TaxPayableRowDto
                {
                    Key = row.Key,
                    Kind = row.DeclareKind,
                    PeriodStart = row.PeriodStart,
                    PeriodEnd = row.PeriodEnd,
                    Deadline = NextFifteenth(row.PeriodEnd),
                    Refund = row.Payable,
                    Payable = row.Payable,
                    Surcharge = 0m,
                    Status = string.Empty
                })
                .ToList();

            if (rows.Count > 0)
            {
                return new TaxPayablesResultDto
                {
                    Rows = rows,
                    Message = "应缴税额取自税局申报记录；缴款状态、滞纳金与利息请以电子税务局为准。"
                };
            }

            _logger.LogWarning(
                "[税局取数] 未取到 {Year} 年 {Month} 月的应缴税款：{Diagnostics}",
                year,
                month,
                string.Join(" / ", declarations.Diagnostics ?? Array.Empty<string>()));
            return new TaxPayablesResultDto
            {
                Message = "所选税款所属期没有需要缴纳的税款。",
                Diagnostics = declarations.Diagnostics?.Take(20).ToList()
            };
        }

        /// <summary>读取「下载完税证明」：按开具日期区间直连税局接口取回已开具的完税证明。</summary>
        public async Task<TaxCertificatesResultDto> FetchIssuedCertificatesAsync(
            TaxPortalSessionKey key,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken)
        {
            var payloads = new List<CapturedJsonPayload>();
            var failure = await CollectDirectQueryPagesAsync(
                key,
                CertificatePagePath,
                CertificateQueryPath,
                "已开具完税证明",
                pageNumber => $"{{\"djxh\":\"\",\"kjrqq\":\"{from:yyyy-MM-dd}\",\"kjrqz\":\"{to:yyyy-MM-dd}\","
                    + $"\"pageNum\":{pageNumber},\"pageSize\":{CertificateQueryPageSize},\"zmzl\":\"1\"}}",
                MaxCertificateQueryPages,
                payloads,
                cancellationToken);

            if (failure is not null)
            {
                return new TaxCertificatesResultDto
                {
                    NeedLogin = failure.NeedLogin,
                    Message = failure.Message,
                    Diagnostics = failure.Diagnostics.Count > 0 ? failure.Diagnostics : null
                };
            }

            var result = TaxPaymentPayloadParser.ParseCertificates(payloads, from, to);
            if (result.Rows.Count > 0)
            {
                return result;
            }

            _logger.LogWarning(
                "[税局取数] 未取到 {From:yyyy-MM-dd} 至 {To:yyyy-MM-dd} 的已开具完税证明：{Diagnostics}",
                from,
                to,
                string.Join(" / ", result.Diagnostics ?? Array.Empty<string>()));
            return result;
        }

        /// <summary>
        /// 在数字账户业务页里直接 POST 查询接口并按 Total 翻页取全。
        /// 落在 loginb 或第三方子站时先回门户、再打开业务页，让子系统会话在页面加载时种下；
        /// 接口异常、被踢回登录页或子系统会话缺失时返回失败，由调用方决定怎么向用户说明。
        /// </summary>
        private async Task<DirectQueryFailure?> CollectDirectQueryPagesAsync(
            TaxPortalSessionKey key,
            string pagePath,
            string queryPath,
            string title,
            Func<int, string> bodyOfPage,
            int maxPages,
            List<CapturedJsonPayload> payloads,
            CancellationToken cancellationToken)
        {
            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session is null)
            {
                return new DirectQueryFailure(true, "税局登录状态已过期，请重新登录电子税务局。", []);
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (session.State != TaxPortalStates.Success || session.Page is null)
                {
                    return new DirectQueryFailure(true, $"请先完成税局登录，再查询{title}。", []);
                }

                var page = session.Page;
                var saved = await _stateStore.LoadAsync(key, cancellationToken);
                var homeUri = saved is not null && Uri.TryCreate(saved.HomeUrl, UriKind.Absolute, out var savedUri)
                    ? savedUri
                    : null;
                var pageUri = Uri.TryCreate(page.Url, UriKind.Absolute, out var current) ? current : null;

                // 页面可能停在发票等第三方税局子站：先回门户，数字账户接口只在门户域名下认。
                if (homeUri is not null
                    && (pageUri is null || !string.Equals(pageUri.Host, homeUri.Host, StringComparison.OrdinalIgnoreCase)))
                {
                    await NavigateAsync(page, homeUri.ToString(), cancellationToken);
                    await Task.Delay(1200, cancellationToken);
                    pageUri = Uri.TryCreate(page.Url, UriKind.Absolute, out current) ? current : null;
                }

                var baseUri = homeUri ?? pageUri;
                if (baseUri is null || pageUri is null)
                {
                    return new DirectQueryFailure(false, "税局门户地址无法识别，请重新登录电子税务局。", []);
                }

                // 目前只在浙江的数字账户子站上验证过这套接口，其他省不要白等一轮浏览器。
                if (!baseUri.Host.Contains("zhejiang", StringComparison.OrdinalIgnoreCase))
                {
                    return new DirectQueryFailure(
                        false,
                        $"目前只接入了浙江电子税务局的{title}接口，请到电子税务局查询。",
                        []);
                }

                if (!pageUri.AbsolutePath.StartsWith(pagePath, StringComparison.OrdinalIgnoreCase))
                {
                    await OpenDigitalAccountPageAsync(page, baseUri, pagePath, cancellationToken);
                }

                var frame = FindPortalFrame(page, homeUri?.Host);
                var fetched = 0;
                int? total = null;
                for (var pageNumber = 1; pageNumber <= maxPages; pageNumber++)
                {
                    var expression = "(async()=>{const r=await fetch('" + queryPath + "',{method:'POST',"
                        + "headers:{'Content-Type':'application/json'},credentials:'include',body:JSON.stringify("
                        + bodyOfPage(pageNumber) + ")});return await r.text();})()";
                    // 刚落到业务页时前端还在初始化，头一两次请求可能抛 Failed to fetch 或回 HTML；
                    // 隔几百毫秒重试，比固定等几秒稳，也不会在已经就绪时白等。
                    var raw = "";
                    for (var attempt = 1; attempt <= 3; attempt++)
                    {
                        try
                        {
                            raw = await FetchAsync(frame, expression, cancellationToken);
                        }
                        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                        {
                            raw = "";
                            _logger.LogInformation(
                                "[税局取数] {Title}接口第 {Attempt} 次调用失败：{Message}",
                                title,
                                attempt,
                                ex.Message);
                        }

                        var start = raw.TrimStart();
                        if (start.Length > 0 && start[0] is '{' or '[')
                        {
                            break;
                        }

                        await Task.Delay(700, cancellationToken);
                    }

                    if (await IsLoginPageAsync(page, cancellationToken))
                    {
                        _logger.LogInformation("[税局取数] 会话 {SessionId} 查询{Title}时被踢回登录页", session.Id, title);
                        await EndSessionAsync(key, session);
                        return new DirectQueryFailure(true, "税局登录状态已过期，请重新登录电子税务局。", []);
                    }

                    if (TaxDeclarationPayloadParser.NeedsAppSession(raw))
                    {
                        // 子系统会话是业务页加载时自己种的；刚打开还没种好时重开一次再取。
                        _logger.LogInformation(
                            "[税局取数] 会话 {SessionId} 调用{Title}接口时缺少数字账户子系统会话，重开业务页后重试",
                            session.Id,
                            title);
                        await OpenDigitalAccountPageAsync(page, baseUri, pagePath, cancellationToken);
                        raw = await FetchAsync(frame, expression, cancellationToken);
                        if (TaxDeclarationPayloadParser.NeedsAppSession(raw))
                        {
                            _logger.LogWarning(
                                "[税局取数] 会话 {SessionId} 补建数字账户会话后仍被税局拒绝：{Body}",
                                session.Id,
                                Clip(raw, 300));
                            return new DirectQueryFailure(
                                true,
                                $"税局数字账户拒绝返回{title}，请重新登录电子税务局后再试。",
                                [Clip(raw, 300)]);
                        }
                    }

                    var body = raw.TrimStart();
                    if (body.Length == 0 || body[0] is not ('{' or '['))
                    {
                        _logger.LogWarning("[税局取数] {Title}接口没有返回 JSON：{Body}", title, Clip(raw, 300));
                        return new DirectQueryFailure(false, $"读取税局{title}失败，请稍后重试。", [Clip(raw, 300)]);
                    }

                    payloads.Add(new CapturedJsonPayload(queryPath, raw));
                    var (pageTotal, count) = ReadPageCounts(raw);
                    total ??= pageTotal;
                    fetched += count ?? 0;

                    // 税局有时按自己的上限截断 pageSize；以它给的 Total 为准，取够就停。
                    if (count is null or 0 || (total is not null && fetched >= total))
                    {
                        break;
                    }
                }

                session.Touch();
                return null;
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                _logger.LogWarning("[税局取数] 查询{Title}失败：{Message}", title, ex.Message);
                return new DirectQueryFailure(false, $"读取税局{title}失败，请稍后重试。", [ex.Message]);
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>
        /// 直接打开数字账户业务页。菜单进子系统时 URL 会带一个 ruuid 让前端重新初始化，
        /// 这里照做，确保子系统会话在页面加载时被种下。
        /// </summary>
        private static async Task OpenDigitalAccountPageAsync(
            IPage page, Uri baseUri, string pagePath, CancellationToken cancellationToken)
        {
            var target = new Uri(
                baseUri,
                $"{pagePath}?ruuid={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
            await NavigateAsync(page, target.ToString(), cancellationToken);
            await Task.Delay(1500, cancellationToken);
        }
        /// <summary>申报期限的通用口径：所属期止的次月 15 日；节假日顺延以税局为准。</summary>
        private static string NextFifteenth(string periodEnd)
        {
            return DateOnly.TryParseExact(
                periodEnd, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var end)
                ? new DateOnly(end.Year, end.Month, 1).AddMonths(1).AddDays(14).ToString("yyyy-MM-dd")
                : string.Empty;
        }

        // ===== 个税（自然人电子税务局 etax.chinatax.gov.cn）=====
        //
        // 个税人员信息不在这套企业门户里，而在自然人电子税务局的「单位办税 → 扣缴申报」。
        // 那是一套独立的站点与账号体系（个人所得税 APP 的账号密码），所以单独起会话、
        // 单独存登录态（TaxPortalKind.Iit），支持账号密码与 APP 扫码两种登录方式。

        private const string IitHomeUrl = "https://etax.chinatax.gov.cn/";

        /// <summary>发起个税登录：mode=password 用账套保存的实名账号密码，mode=scan 用个人所得税 APP 扫码。</summary>
        public TaxPortalLoginStatusDto StartIitLogin(
            TaxPortalSessionKey key,
            string mode,
            string account,
            string password)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // 一次提交＝一条短信：登录还在进行中（或正等用户填码）时，重复点「登录」「获取验证码」
            // 不再重开一轮（重开会重拖滑块并再次提交），只把当前进展回给页面。
            // 真要重来先点「退出登录」，那会把会话丢掉。
            if (_sessions.TryGetValue(key, out var running) && running.IsPending)
            {
                return running.Snapshot();
            }

            DiscardSession(key);

            var session = new TaxPortalSession(key);
            _sessions[key] = session;
            TrimSessions();
            var scan = !string.Equals(mode, "password", StringComparison.OrdinalIgnoreCase);
            _ = Task.Run(() => scan
                ? StartIitScanCoreAsync(session)
                : StartIitPasswordCoreAsync(session, account, password));
            return session.Snapshot();
        }

        private async Task StartIitPasswordCoreAsync(TaxPortalSession session, string account, string password)
        {
            try
            {
                await session.Gate.WaitAsync();
                try
                {
                    if (await TryRestoreCoreAsync(session, CancellationToken.None))
                    {
                        return;
                    }

                    await DriveIitPasswordLoginCoreAsync(session, account, password);
                }
                finally
                {
                    session.Gate.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[个税登录] 会话 {SessionId} 失败：{Message}", session.Id, ex.Message);
                session.SetState(TaxPortalStates.Failed, ex.Message);
            }
        }

        private async Task StartIitScanCoreAsync(TaxPortalSession session)
        {
            try
            {
                await session.Gate.WaitAsync();
                try
                {
                    if (await TryRestoreCoreAsync(session, CancellationToken.None))
                    {
                        return;
                    }

                    await DriveIitScanCoreAsync(session);
                }
                finally
                {
                    session.Gate.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[个税扫码] 会话 {SessionId} 失败：{Message}", session.Id, ex.Message);
                session.SetState(TaxPortalStates.Failed, ex.Message);
            }
        }

        /// <summary>账号密码登录个税：真实键入 → 拖滑块 → 提交 →（可能）短信中继 → 成功。</summary>
        private async Task DriveIitPasswordLoginCoreAsync(TaxPortalSession session, string account, string password)
        {
            var page = await OpenIitLoginPageAsync(session);

            session.SetState(TaxPortalStates.Starting, "正在填写个税实名账号…");
            await TypeIitLoginAsync(page, account, password, CancellationToken.None);

            session.SetState(TaxPortalStates.Starting, "正在拖动个税登录的安全滑块…");
            var sliderFailure = await DragIitSliderAsync(
                "个税登录", ct => SliderHereAsync(page, page.MainFrame, ct), CancellationToken.None);
            if (sliderFailure is not null)
            {
                // 滑块阶段就收口，一次提交都没点，所以这一趟绝不会多出短信。
                await SaveIitFailureShotAsync(session, page);
                throw new InvalidOperationException(
                    $"个税登录没做成：{sliderFailure}，提交没有点下去，也不会收到短信。"
                    + "请稍后再试一次，或改用「个人所得税 APP 扫码登录」。"
                    + $"（页面状态 {await DiagnoseAsync(page, CancellationToken.None)}）");
            }

            session.SetState(TaxPortalStates.Starting, "正在提交个税登录…");

            // 提交被拒时税局经常什么都不显示，原因只写在登录接口的响应里，所以把这一段的 JSON 响应全收下来。
            var collector = new JsonResponseCollector();
            var context = session.Context;
            var watched = context is null ? new List<IPage> { page } : context.Pages.ToList();
            void Attach(IPage target)
            {
                target.Response += collector.OnResponse;
                target.Request += collector.OnRequest;
            }

            void Detach(IPage target)
            {
                target.Response -= collector.OnResponse;
                target.Request -= collector.OnRequest;
            }
            void OnOpenedPage(object? sender, IPage opened)
            {
                watched.Add(opened);
                Attach(opened);
            }

            foreach (var watchedPage in watched)
            {
                Attach(watchedPage);
            }

            if (context is not null)
            {
                context.Page += OnOpenedPage;
            }

            bool submitted;
            try
            {
                var portalAtClick = collector.PortalRequestCount;
                submitted = await EvaluateBoolAsync(page, TaxPortalPageScripts.IitClickSubmit, CancellationToken.None);
                _logger.LogInformation("[个税登录] 提交按钮点击={Submitted}", submitted ? "ok" : "miss");
                await Task.Delay(1500);

                // 提交被表单自己拦住时（滑块被税局打回原样），页面不会有任何后续，
                // 干等 75 秒纯属浪费。两个条件同时成立才判「没出门」：点击后税局域名一个新请求都没有
                // + 滑块已被打回原样。没出门就不可能触发短信，所以这条早退只是不再白等，不会少发短信。
                var afterClick = await ReadIitSliderAsync(page, CancellationToken.None);
                if (afterClick?["reset"]?.GetValue<bool>() == true && collector.PortalRequestCount == portalAtClick)
                {
                    var blockedTip = afterClick["tip"]?.GetValue<string>() ?? "请重新滑动验证";
                    _logger.LogInformation("[个税登录] 提交被表单拦下：滑块打回原样（{Tip}），点击后没有新请求发往税局", blockedTip);
                    await SaveIitFailureShotAsync(session, page);
                    throw new InvalidOperationException(
                        $"个税登录没做成：点提交时被税局的滑块复核拦住（{blockedTip}），请求没有发出去，也不会收到短信。"
                        + "请稍后再试一次，或改用「个人所得税 APP 扫码登录」。");
                }

                var deadline = DateTime.UtcNow.AddSeconds(75);
                string? lastSeen = null;
                while (DateTime.UtcNow < deadline)
                {
                    // 税局可能把「安全验证」换到新的标签页，只盯原来那一页永远等不到，所以逐页逐帧扫。
                    foreach (var candidate in IitCandidatePages(session, page))
                    {
                        var stage = await ReadIitStageAsync(candidate, CancellationToken.None);
                        if (stage.Success)
                        {
                            session.Page = candidate;
                            await MarkSuccessAsync(session);
                            return;
                        }

                        if (stage.Sms)
                        {
                            session.Page = candidate;
                            session.SetState(TaxPortalStates.SmsRequired, "个税系统已发送短信验证码，请填写到本页后提交。");
                            return;
                        }
                    }

                    // 提示条只活几秒，所以每轮都看一眼，但只在画面真的变了时写日志。
                    var seen = await WatchIitLoginAsync(session, page);
                    var detail = seen?.ToJsonString();
                    if (!string.IsNullOrEmpty(detail) && detail != lastSeen)
                    {
                        lastSeen = detail;
                        _logger.LogInformation("[个税登录·观察] {Detail}", detail);
                    }

                    await Task.Delay(1200, CancellationToken.None);
                }

                await EvaluateBoolAsync(page, TaxPortalPageScripts.DismissDialog, CancellationToken.None);
                var error = await ReadErrorTextAsync(page, CancellationToken.None);
                if (string.IsNullOrWhiteSpace(error) && lastSeen is not null)
                {
                    // 提交被拒时税局多半只弹一条几秒就消失的提示条，观察日志已经把文案抓下来了，直接回给用户。
                    error = JsonNode.Parse(lastSeen)?["msgs"] is JsonArray { Count: > 0 } toasts
                        ? string.Join("；", toasts.Select(item => item?.GetValue<string>() ?? string.Empty))
                        : string.Empty;
                }

                // 这里绝不自动重拖再提交：一次提交对应一条短信，重复提交最容易触发税局的账号/来源风控。
                // 但滑块被税局打回原样时表单自己就拦住了提交，一个请求都没发出去，这个原因比「没弹窗」准确得多。
                var slider = await ReadIitSliderAsync(page, CancellationToken.None);
                if (string.IsNullOrWhiteSpace(error) && slider?["reset"]?.GetValue<bool>() == true)
                {
                    error = $"滑块被税局复核打回了原样（{slider["tip"]?.GetValue<string>() ?? "请重新滑动验证"}），提交没有发出去。";
                }

                var apiNote = await ReadIitLoginRejectionsAsync(collector);
                await SaveIitFailureShotAsync(session, page);
                throw new InvalidOperationException(
                    (string.IsNullOrWhiteSpace(error)
                        ? $"个税登录卡住了：提交后 75 秒内页面上没有出现「安全验证」弹窗（{(submitted ? "已点到提交" : "没点到提交按钮")}）。"
                        : $"个税页面提示：{error}")
                    + apiNote
                    + "请稍后再试一次，或改用「个人所得税 APP 扫码登录」。");
            }
            finally
            {
                if (context is not null)
                {
                    context.Page -= OnOpenedPage;
                }

                foreach (var watchedPage in watched)
                {
                    Detach(watchedPage);
                }
            }
        }

        /// <summary>把提交这一段里税局接口回过的 JSON 摘成一句人能看懂的原因（同时全量打进日志）。</summary>
        private async Task<string> ReadIitLoginRejectionsAsync(JsonResponseCollector collector)
        {
            var payloads = await collector.ReadAsync(_logger, CancellationToken.None);
            var notes = new List<string>();
            foreach (var payload in payloads)
            {
                _logger.LogInformation(
                    "[个税登录·接口] {Url} {Body}",
                    payload.Url.Split('?')[0],
                    payload.Body.Length > 400 ? payload.Body[..400] : payload.Body);

                try
                {
                    var node = JsonNode.Parse(payload.Body);
                    var message = node?["message"]?.GetValue<string>()
                        ?? node?["msg"]?.GetValue<string>()
                        ?? node?["returnMessage"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        notes.Add(message);
                    }
                }
                catch (JsonException)
                {
                }
            }

            return notes.Count == 0
                ? payloads.Count == 0
                    ? "（税局没有回任何接口，说明请求可能没发出去）"
                    : $"（收到 {payloads.Count} 个接口响应，均未给出文案，详见日志「[个税登录·接口]」）"
                : $"，税局接口提示：{string.Join("；", notes.Distinct().Take(3))}";
        }

        /// <summary>
        /// 登录卡住时拍一张整页图放进临时目录，并把路径打进日志。
        /// DOM 探针只能给出文案，「页面一片正常但就是不跳」这种局面要靠像素看清税局到底给了什么。
        /// </summary>
        private async Task SaveIitFailureShotAsync(TaxPortalSession session, IPage page)
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), $"yuncai-iit-login-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true })
                    .WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                _logger.LogInformation("[个税登录·截图] {Path}", path);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException or IOException)
            {
            }

            // 整页 PNG 有一两兆，塞进轮询的状态里不合适；页面看当前视口的 JPEG 就够判断了
            // （登录表单和滑块都在首屏），需要细看再按日志里的 PNG 路径取原图。
            try
            {
                var bytes = await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Type = ScreenshotType.Jpeg,
                    Quality = 60,
                }).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                session.SetFailureShot($"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}");
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
            }
        }

        /// <summary>上下文里当前还活着的页面；税局换标签页时，新页面才是真正的登录页。</summary>
        private static IEnumerable<IPage> IitCandidatePages(TaxPortalSession session, IPage page)
        {
            var pages = session.Context?.Pages;
            return pages is { Count: > 0 } ? pages : new[] { page };
        }

        /// <summary>
        /// 取一份页面实况（可见弹窗、隐藏的弹窗节点、滑块文案、提示条、可见输入框）。
        /// 提交后等不到弹窗时，靠这行分清是滑块没过、账号被拒，还是弹窗出现了但判据没覆盖。
        /// </summary>
        private static async Task<JsonObject?> WatchIitLoginAsync(TaxPortalSession session, IPage page)
        {
            foreach (var candidate in IitCandidatePages(session, page))
            {
                var seen = await EvaluateJsonAsync(candidate, TaxPortalPageScripts.IitWatch, CancellationToken.None);
                if (seen is not null)
                {
                    return seen;
                }
            }

            return null;
        }

        /// <summary>扫码登录个税：切到「个人所得税APP」页签，把二维码回传给网页，等用户扫码确认。</summary>
        private async Task DriveIitScanCoreAsync(TaxPortalSession session)
        {
            var page = await OpenIitLoginPageAsync(session);

            if (!await EvaluateBoolAsync(page, TaxPortalPageScripts.IitClickQrTab, CancellationToken.None))
            {
                throw new InvalidOperationException("没有找到「个人所得税APP」扫码登录页签，页面结构可能已调整。");
            }
            await Task.Delay(1500);

            var deadline = DateTime.UtcNow.AddMinutes(5);
            var qr = string.Empty;
            while (DateTime.UtcNow < deadline)
            {
                if (await ReadIitStageSuccessAsync(page, CancellationToken.None))
                {
                    await MarkSuccessAsync(session);
                    return;
                }

                var current = await EvaluateJsonAsync(page, TaxPortalPageScripts.IitQrImage, CancellationToken.None);
                var image = current?["image"]?.GetValue<string>() ?? string.Empty;
                if (image.Length > 0 && !string.Equals(image, qr, StringComparison.Ordinal))
                {
                    qr = image;
                    session.SetQr(qr);
                    session.SetState(TaxPortalStates.ScanRequired, "请用个人所得税 APP 扫描二维码并确认登录。");
                }

                if (await EvaluateBoolAsync(page, TaxPortalPageScripts.IitQrExpired, CancellationToken.None))
                {
                    await EvaluateBoolAsync(page, TaxPortalPageScripts.IitClickQrRefresh, CancellationToken.None);
                    await Task.Delay(1500, CancellationToken.None);
                }

                await Task.Delay(2000, CancellationToken.None);
            }

            throw new InvalidOperationException("扫码登录超时：请重新发起，并在 5 分钟内用个人所得税 APP 完成扫码确认。");
        }

        /// <summary>
        /// 打开个税登录页并切到「密码登录」页签。
        /// 注意这里不能注入 <see cref="TaxPortalPageScripts.StealthScript"/>：手工覆写 navigator.webdriver
        /// 会被瑞数当成自动化指纹，挑战页停在空白 document（实测 body 长度为 0）；
        /// 启动参数里的 --disable-blink-features=AutomationControlled 本来就让它为 false，多余且有害。
        /// 首页过了瑞数之后自带登录面板（默认停在扫码页签），比直连 /webstatic/login 可靠。
        /// </summary>
        private async Task<IPage> OpenIitLoginPageAsync(TaxPortalSession session)
        {
            var browser = await GetBrowserAsync();
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "zh-CN",
                TimezoneId = "Asia/Shanghai",
                ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
                UserAgent = IitUserAgent(browser)
            });
            session.Context = context;
            var page = await context.NewPageAsync();
            session.Page = page;

            session.SetState(TaxPortalStates.Starting, "正在打开自然人电子税务局…");
            _logger.LogInformation("[个税登录] 会话 {SessionId} 打开 {Url}", session.Id, IitHomeUrl);
            await NavigateAsync(page, IitHomeUrl, CancellationToken.None);
            if (!await WaitForPageAsync(page, TimeSpan.FromSeconds(40), CancellationToken.None))
            {
                throw new InvalidOperationException(
                    "自然人电子税务局页面加载超时，请检查服务器网络后重试。" + await DiagnoseAsync(page, CancellationToken.None));
            }

            // 首页右上角的「登录」入口只在面板没展开时才点；面板本身就在首页正文里。
            if (!await EvaluateBoolAsync(page, TaxPortalPageScripts.IitClickPasswordTab, CancellationToken.None))
            {
                await EvaluateBoolAsync(page, TaxPortalPageScripts.ClickLoginEntry, CancellationToken.None);
                await EvaluateBoolAsync(page, TaxPortalPageScripts.IitClickPasswordTab, CancellationToken.None);
            }

            await Task.Delay(2000, CancellationToken.None);
            if (!await WaitForLoginFormAsync(page, TimeSpan.FromSeconds(15), CancellationToken.None))
            {
                throw new InvalidOperationException("未识别到个税登录表单，页面结构可能已调整。" + await DiagnoseAsync(page, CancellationToken.None));
            }

            return page;
        }

        /// <summary>
        /// 账号密码必须用真实键盘敲进去：个税登录面板是 Element UI，脚本直接改 value 只派发合成事件，
        /// 阿里云滑块采集不到输入行为，提交时会回「验证失败请重新滑动验证」。
        /// </summary>
        private static async Task TypeIitLoginAsync(IPage page, string account, string password, CancellationToken cancellationToken)
        {
            var userBox = page.Locator("input[name='username']");
            var pwdBox = page.Locator("input[type='password']");
            await userBox.ClickAsync();
            await userBox.FillAsync(string.Empty);
            await userBox.PressSequentiallyAsync(account, new() { Delay = 120 });
            await Task.Delay(400, cancellationToken);
            await pwdBox.ClickAsync();
            await pwdBox.FillAsync(string.Empty);
            await pwdBox.PressSequentiallyAsync(password, new() { Delay = 90 });
        }

        /// <summary>
        /// 拖动阿里云滑块到最右边：先轮询等它挂载（滑块是异步挂载的，偶发要等十几秒），
        /// 轨迹统一走 <see cref="DragSliderAsync"/> 那套拟人封装，拖完再复查税局有没有把它打回原样。
        /// 阿里云的服务端校验有随机性，同一次允许多次重试。
        /// 「在哪儿找滑块」由 probe 决定：登录页只看主文档，取数页要逐标签页逐帧找——
        /// 明文验证的滑块挂在扣缴工作台的应用帧里，只看最初那个页面会当成没弹滑块。
        /// 返回 null 表示滑块真的站住了；否则回一句失败原因，直接拼给用户的提示。
        /// </summary>
        private async Task<string?> DragIitSliderAsync(
            string stage, Func<CancellationToken, Task<IitSlider?>> probe, CancellationToken cancellationToken)
        {
            int revoked = 0;
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                var slider = await WaitForSliderAsync(probe, 20, 1500, cancellationToken);
                if (slider is null)
                {
                    _logger.LogInformation("[{Stage}] 第 {Attempt} 次：没等到滑块", stage, attempt);
                    return "页面上始终没挂载出安全滑块";
                }

                var state = await slider.ReadAsync(cancellationToken);
                if (state?["passed"]?.GetValue<bool>() == true)
                {
                    if (await IitSliderHoldsAsync(slider, stage, cancellationToken))
                    {
                        return null;
                    }

                    revoked++;
                    // 被打回原样后滑块的坐标变了，必须重读一遍再拖，不能拿「通过」时的位置去拖。
                    await Task.Delay(1500, cancellationToken);
                    continue;
                }

                var track = state?["body"]?.AsObject();
                var handle = state?["slider"]?.AsObject();
                if (track is null || handle is null)
                {
                    await Task.Delay(2000, cancellationToken);
                    continue;
                }

                double Double(JsonObject o, string name) => o[name]?.GetValue<double>() ?? 0;
                double startX = Double(handle, "x") + Double(handle, "w") / 2;
                double startY = Double(handle, "y") + Double(handle, "h") / 2;
                double endX = Double(track, "x") + Double(track, "w") - 1;
                await DragSliderAsync(slider.Page, startX, startY, endX, cancellationToken);

                await Task.Delay(2600, cancellationToken);
                state = await slider.ReadAsync(cancellationToken);
                if (state?["passed"]?.GetValue<bool>() == true)
                {
                    if (await IitSliderHoldsAsync(slider, stage, cancellationToken))
                    {
                        _logger.LogInformation("[{Stage}] 滑块第 {Attempt} 次通过且站得住", stage, attempt);
                        return null;
                    }

                    revoked++;
                }

                _logger.LogInformation(
                    "[{Stage}] 滑块第 {Attempt} 次没过：{Tip}",
                    stage,
                    attempt,
                    state?["tip"]?.GetValue<string>() ?? "(无)");
                await Task.Delay(1500, cancellationToken);
            }

            return revoked > 0
                ? $"连拖 {revoked} 次都先「验证通过」再被税局复核打回原样"
                : "安全滑块没能通过验证";
        }

        /// <summary>
        /// 滑块「验证通过」只活一瞬间：税局随后自己再判一次，判砸了就把滑块打回「请按住滑块，拖动到最右边」，
        /// 此时点提交表单直接拦住、一个请求都发不出去。所以通过之后要连续复查五秒确认站得住才去提交；
        /// 中途被打回就重拖——重拖不发消息，只有提交才会，所以这一步没有触发短信的风险。
        /// 判据只用「打回原样」：红色「验证失败」文案会残留在页面上，拿它当信号会把后面真通过的滑块误杀。
        /// 实测收回落在「通过」之后约 4 秒，三秒窗口太短；再往后拖也没意义，阿里云的通过态不是长生不老的。
        /// </summary>
        private async Task<bool> IitSliderHoldsAsync(IitSlider slider, string stage, CancellationToken cancellationToken)
        {
            for (int check = 1; check <= 10; check++)
            {
                await Task.Delay(500, cancellationToken);
                var state = await slider.ReadAsync(cancellationToken);
                if (state is null)
                {
                    return true;
                }

                if (state["reset"]?.GetValue<bool>() == true)
                {
                    _logger.LogInformation(
                        "[{Stage}] 滑块「验证通过」在第 {Seconds} 秒被税局收回：{Tip}",
                        stage,
                        check / 2.0,
                        state["tip"]?.GetValue<string>() ?? "(无)");
                    return false;
                }
            }

            return true;
        }

        /// <summary>真实的鼠标按下—分步移动—抬起，走的是元素级 UI 事件，页面脚本伪造不出来。</summary>
        private static async Task DragSliderAsync(IPage page, double startX, double startY, double endX, CancellationToken cancellationToken)
        {
            var rnd = new Random();

            // 人先把鼠标晃到滑块上，落点也不会正好在正中心。
            for (int i = 0; i < 4; i++)
            {
                await page.Mouse.MoveAsync(
                    (float)(startX - 70 + rnd.NextDouble() * 40),
                    (float)(startY + 20 + rnd.NextDouble() * 20));
                await Task.Delay(rnd.Next(40, 90), cancellationToken);
            }

            float pressX = (float)(startX + rnd.Next(-3, 4));
            float pressY = (float)(startY + rnd.Next(-2, 3));
            await page.Mouse.MoveAsync(pressX, pressY, new MouseMoveOptions { Steps = 3 });
            await Task.Delay(rnd.Next(90, 200), cancellationToken);
            await page.Mouse.DownAsync();
            await Task.Delay(rnd.Next(60, 140), cancellationToken);

            // 真人一次滑到位要 0.4~0.9 秒，鼠标按 60~125Hz 上报，即 8~16ms 一个点。
            // 之前 12~55ms 一个点等于 20~80Hz 却集中在 30Hz，这个采样间隔本身就与任何真实鼠标对不上，
            // 是最好认的一类自动化特征。收尾会冲过终点几像素再回拉，跟人把滑块顶到底的习惯一致。
            int steps = rnd.Next(45, 80);
            double overshoot = rnd.Next(2, 6);
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                double eased = Math.Min(1, t < 0.35 ? t * t * 4.1 : 1 - Math.Pow(1 - t, 3) * 0.42);
                double x = pressX + (endX + overshoot - pressX) * eased;
                if (t > 0.82)
                {
                    x -= overshoot * (t - 0.82) / 0.18;
                }

                double y = pressY + Math.Sin(i * 0.6) * rnd.NextDouble() * 2.2;
                await page.Mouse.MoveAsync((float)Math.Round(x), (float)Math.Round(y));
                // 偶尔卡一下（掉帧、系统忙）才是人的样子；一直匀速反而假。
                await Task.Delay(rnd.Next(5) == 0 ? rnd.Next(26, 60) : rnd.Next(6, 14), cancellationToken);
            }

            await Task.Delay(rnd.Next(140, 300), cancellationToken);
            await page.Mouse.UpAsync();
        }

        /// <summary>等滑块挂载（阿里云的 JS 从 o.alicdn.com 异步加载），探测由 probe 决定看哪儿。</summary>
        private static async Task<IitSlider?> WaitForSliderAsync(
            Func<CancellationToken, Task<IitSlider?>> probe, int attempts, int delayMs, CancellationToken cancellationToken)
        {
            for (int i = 0; i < attempts; i++)
            {
                var slider = await probe(cancellationToken);
                if (slider is not null)
                {
                    return slider;
                }

                await Task.Delay(delayMs, cancellationToken);
            }

            return null;
        }

        private static Task<JsonObject?> ReadIitSliderAsync(IPage page, CancellationToken cancellationToken) =>
            ReadSliderStateAsync(page, page.MainFrame, cancellationToken);

        /// <summary>一处滑块的落点：页面（鼠标事件按页面坐标发）+ 帧（滑块的 DOM 在哪个文档里）。</summary>
        private sealed record IitSlider(IPage Page, IFrame Frame)
        {
            public Task<JsonObject?> ReadAsync(CancellationToken cancellationToken) =>
                ReadSliderStateAsync(Page, Frame, cancellationToken);
        }

        /// <summary>
        /// 读滑块状态。iframe 里 getBoundingClientRect 的 x/y 是相对帧内视口的，直接喂给 page.Mouse 会拖到别处，
        /// 所以命中滑块后改用 Playwright 的元素包围盒（相对主文档）覆盖一遍几何。
        /// </summary>
        private static async Task<JsonObject?> ReadSliderStateAsync(IPage page, IFrame frame, CancellationToken cancellationToken)
        {
            var state = await EvaluateJsonAsync(frame, TaxPortalPageScripts.IitSliderState, cancellationToken);
            if (state is null || state["present"]?.GetValue<bool>() != true)
            {
                return state;
            }

            foreach (var (key, selector) in new[]
                     {
                         ("body", "#aliyunCaptcha-sliding-body"),
                         ("slider", "#aliyunCaptcha-sliding-slider"),
                     })
            {
                var box = await BoundingBoxAsync(frame, selector, cancellationToken);
                if (box is not null && state[key] is JsonObject rect)
                {
                    rect["x"] = box.Value.X;
                    rect["y"] = box.Value.Y;
                    rect["w"] = box.Value.W;
                    rect["h"] = box.Value.H;
                }
            }

            return state;
        }

        /// <summary>
        /// 元素在页面坐标系里的包围盒；取不到（帧已销毁、元素隐藏）就回 null，沿用 DOM 里读到的那份。
        /// 超时压到亚秒级：通过后的复查每半秒读一次滑块，这里慢下来会把整段判定的节奏一起拖长。
        /// </summary>
        private static async Task<(double X, double Y, double W, double H)?> BoundingBoxAsync(
            IFrame frame, string selector, CancellationToken cancellationToken)
        {
            try
            {
                var box = await frame.Locator(selector).BoundingBoxAsync(new LocatorBoundingBoxOptions { Timeout = 800 })
                    .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
                return box is null ? null : (box.X, box.Y, box.Width, box.Height);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                return null;
            }
        }

        /// <summary>这一帧里有没有滑块：挂载中或已「验证通过」都算，后者要靠它复查有没有被打回。</summary>
        private static async Task<IitSlider?> SliderHereAsync(IPage page, IFrame frame, CancellationToken cancellationToken)
        {
            var state = await ReadSliderStateAsync(page, frame, cancellationToken);
            return state is not null
                && (state["present"]?.GetValue<bool>() == true || state["passed"]?.GetValue<bool>() == true)
                ? new IitSlider(page, frame)
                : null;
        }

        /// <summary>逐个标签页、逐个帧找滑块：「单位办税」是另一个标签页，应用还嵌在帧里，只盯主文档永远找不到。</summary>
        private static async Task<IitSlider?> FindIitSliderAsync(
            TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            foreach (var candidate in IitCandidatePages(session, page))
            {
                foreach (var frame in candidate.Frames)
                {
                    var slider = await SliderHereAsync(candidate, frame, cancellationToken);
                    if (slider is not null)
                    {
                        return slider;
                    }
                }
            }

            return null;
        }


        /// <summary>提交个税短信验证码；成功后保留登录态供取数复用。</summary>
        public async Task<TaxPortalLoginStatusDto> VerifyIitAsync(TaxPortalSessionKey key, string code, CancellationToken cancellationToken)
        {
            var session = RequireSession(key, "登录会话已失效，请重新发起个税登录。");
            if (string.IsNullOrWhiteSpace(code) || !Regex.IsMatch(code.Trim(), @"^\d{4,8}$"))
            {
                throw new ArgumentException("请输入个税系统短信里的数字验证码。");
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (session.State == TaxPortalStates.Success)
                {
                    return session.Snapshot();
                }

                if (session.State != TaxPortalStates.SmsRequired)
                {
                    throw new InvalidOperationException("当前登录步骤还不需要验证码，请稍候。");
                }

                var page = session.Page ?? throw new InvalidOperationException("个税页面已关闭，请重新发起登录。");
                var target = await FillIitSmsAsync(session, page, code.Trim(), cancellationToken);
                if (target is null)
                {
                    if (await ReadIitStageSuccessAsync(page, cancellationToken))
                    {
                        await MarkSuccessAsync(session);
                        return session.Snapshot();
                    }

                    throw new InvalidOperationException("个税页面没有找到短信验证码输入框，请重新发起登录。");
                }

                session.Page = target.Page;
                session.SetState(TaxPortalStates.Starting, "正在校验短信验证码…");
                await ClickIitSmsConfirmAsync(session, target.Page, target.Frame);

                var deadline = DateTime.UtcNow.AddSeconds(45);
                while (DateTime.UtcNow < deadline)
                {
                    if (await ReadIitSuccessAsync(session, target.Page, cancellationToken))
                    {
                        await MarkSuccessAsync(session);
                        return session.Snapshot();
                    }

                    await Task.Delay(900, cancellationToken);
                }

                var error = await ReadErrorTextAsync(target.Page, cancellationToken);
                session.SetState(
                    TaxPortalStates.SmsRequired,
                    string.IsNullOrWhiteSpace(error) ? "验证码未通过或已过期，请重新输入或重新发起登录。" : error);
                return session.Snapshot();
            }
            finally
            {
                session.Gate.Release();
            }
        }

        private sealed record IitStageResult(bool Success, bool Sms);

        private sealed record IitSmsTarget(IPage Page, IFrame Frame);

        /// <summary>
        /// 把验证码填进「安全验证」弹窗。弹窗可能在 iframe 里，也可能被税局开成新的标签页，
        /// 所以逐页逐帧试；填中后把所在的页面/帧带回去，确认点击必须在同一个帧里做。
        /// </summary>
        private static async Task<IitSmsTarget?> FillIitSmsAsync(
            TaxPortalSession session, IPage page, string code, CancellationToken cancellationToken)
        {
            foreach (var candidate in IitCandidatePages(session, page))
            {
                foreach (var frame in candidate.Frames)
                {
                    var json = await EvaluateJsonAsync(frame, TaxPortalPageScripts.IitFillSms(code), cancellationToken);
                    if (json?["filled"]?.GetValue<bool>() == true)
                    {
                        return new IitSmsTarget(candidate, frame);
                    }
                }
            }

            return null;
        }

        private static async Task ClickIitSmsConfirmAsync(TaxPortalSession session, IPage page, IFrame frame)
        {
            if (await EvaluateBoolAsync(frame, TaxPortalPageScripts.IitClickSmsConfirm, CancellationToken.None))
            {
                return;
            }

            // 弹窗可能在填码后被重新挂到别的帧，再全量扫一遍兜底。
            foreach (var candidate in IitCandidatePages(session, page))
            {
                foreach (var item in candidate.Frames)
                {
                    if (await EvaluateBoolAsync(item, TaxPortalPageScripts.IitClickSmsConfirm, CancellationToken.None))
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>跨标签页判成功：税局验证通过后可能落在新页面，而不是最初那一页。</summary>
        private async Task<bool> ReadIitSuccessAsync(TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            foreach (var candidate in IitCandidatePages(session, page))
            {
                if (await ReadIitStageSuccessAsync(candidate, cancellationToken))
                {
                    session.Page = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>逐帧读个税页面的登录阶段：已进入登录后界面 / 出现短信验证码输入框。</summary>
        private static async Task<IitStageResult> ReadIitStageAsync(IPage page, CancellationToken cancellationToken)
        {
            foreach (var frame in page.Frames)
            {
                var json = await EvaluateJsonAsync(frame, TaxPortalPageScripts.IitStage, cancellationToken);
                if (json is null)
                {
                    continue;
                }

                var success = json["success"]?.GetValue<bool>() == true;
                var sms = json["sms"]?.GetValue<bool>() == true;
                if (success || sms)
                {
                    return new IitStageResult(success, sms);
                }
            }

            return new IitStageResult(false, false);
        }

        private static async Task<bool> ReadIitStageSuccessAsync(IPage page, CancellationToken cancellationToken)
        {
            return (await ReadIitStageAsync(page, cancellationToken)).Success;
        }

        /// <summary>个税页面是否已经退回未登录状态（出现登录表单或明确的过期提示）。</summary>
        private async Task<bool> IitLoggedOutAsync(IPage page, CancellationToken cancellationToken)
        {
            if (await ReadIitStageSuccessAsync(page, cancellationToken))
            {
                return false;
            }

            return await EvaluateBoolAsync(page, TaxPortalPageScripts.SessionExpired, cancellationToken);
        }

        /// <summary>
        /// 个税取数的公共流程：取会话（内存没有就先用数据库登录态恢复）→ 确认没被踢回登录页
        /// → 装明文抓手 + 挂抓包 → 执行导航 → 交给解析器。解析不出行时，趁页面还在把菜单结构写进日志，
        /// 这是补解析规则的唯一线索。
        /// 报文是整包密文（<c>dmnHafHu=</c>），所以明文有三个来源，按可靠性排序：
        /// 页面解密后交给 <c>JSON.parse</c> 的那一份（<paramref name="plainFragment"/> 指定接口片段）、
        /// 网络层恰好是明文的少数接口、以及最后才用的渲染表格。
        /// </summary>
        private async Task<IitFetchResult<T>> FetchIitAsync<T>(
            TaxPortalSessionKey key,
            string companyName,
            Func<TaxPortalSession, string, CancellationToken, Task<List<string>>> drive,
            Func<IReadOnlyList<CapturedJsonPayload>, (IReadOnlyList<T> Rows, IReadOnlyList<string> Diagnostics)> parse,
            string? plainFragment,
            CancellationToken cancellationToken)
        {
            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session is null)
            {
                return new IitFetchResult<T> { NeedLogin = true, Message = "个税登录状态已过期，请先登录自然人电子税务局。" };
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (session.State != TaxPortalStates.Success || session.Page is null)
                {
                    return new IitFetchResult<T> { NeedLogin = true, Message = "请先完成个税登录，再同步个税数据。" };
                }

                var page = session.Page;
                if (await IitLoggedOutAsync(page, cancellationToken))
                {
                    _logger.LogInformation("[个税取数] 会话 {SessionId} 登录态已失效，清理存档", session.Id);
                    await EndSessionAsync(key, session);
                    return new IitFetchResult<T>
                    {
                        NeedLogin = true,
                        Message = "个税登录状态已过期，请重新登录自然人电子税务局。"
                    };
                }

                var collector = new JsonResponseCollector();
                List<string> steps;
                // 抓手必须在点查询之前装好；「单位办税」是另开的标签页，所以同时注册为上下文级初始化脚本，
                // 新页一诞生就带上钩子，第一次查询的明文也在钩子底下过。
                await InstallIitPlainHookAsync(session, page, cancellationToken);
                using (new PageResponseWatch(session.Context, page, collector))
                {
                    steps = await drive(session, companyName, cancellationToken);
                    await Task.Delay(3500, cancellationToken);
                }

                var payloads = (await collector.ReadAsync(_logger, cancellationToken)).ToList();
                payloads.AddRange(await TakeIitPlainAsync(session, page, plainFragment, cancellationToken));

                // 瑞数把 /web/dkdj/ 的报文改写成 dmnHafHu= 密文，网络层读不到 JSON；
                // 页面自己解密后把名册渲染在表格里，所以把表格行也当作一份报文交给同一套解析。
                payloads.AddRange(await ReadRenderedTablesAsync(session, page, cancellationToken));
                session.Touch();

                // 税局接口回过什么先原样留证：解析规则要靠真实报文写，不能猜字段名。
                foreach (var payload in payloads)
                {
                    _logger.LogInformation(
                        "[个税取数·接口] {Url} {Body}",
                        payload.Url.Split('?')[0],
                        payload.Body.Length > 600 ? payload.Body[..600] : payload.Body);
                }

                var (rows, diagnostics) = parse(payloads);
                if (rows.Count > 0)
                {
                    return new IitFetchResult<T> { Rows = rows };
                }

                var work = session.Page ?? page;
                var outline = await DescribeMenusAsync(work, cancellationToken);
                await DescribeIitTabsAsync(session, page, cancellationToken);
                // Evidence 里带「非 JSON 响应」的报文头部：个税站整包密文（dmnHafHu=）与瑞数挑战页
                // 都只在这份样本里看得见，缺了它就分不清「页面没发请求」和「发了但网络层是密文」。
                var evidence = collector.Evidence(payloads.Count).Concat(diagnostics).ToList();
                _logger.LogWarning(
                    "[个税取数] 会话 {SessionId} 未解析出数据，导航步骤={Steps}，页面结构={Outline}，证据={Evidence}",
                    session.Id,
                    string.Join(" / ", steps),
                    outline,
                    string.Join(" / ", evidence));
                return new IitFetchResult<T>
                {
                    Diagnostics = evidence
                        .Concat(new[] { "steps: " + string.Join(" / ", steps) })
                        .Take(20)
                        .ToList()
                };
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                _logger.LogWarning("[个税取数] 读取失败：{Message}", ex.Message);
                return new IitFetchResult<T>
                {
                    Message = "读取个税数据失败，请稍后重试。",
                    Diagnostics = [ex.Message]
                };
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>个税取数的统一返回：要么有行，要么带 NeedLogin / Message / 诊断摘要。</summary>
        private sealed class IitFetchResult<T>
        {
            public IReadOnlyList<T> Rows { get; init; } = [];

            public bool NeedLogin { get; init; }

            public string? Message { get; init; }

            public IReadOnlyList<string> Diagnostics { get; init; } = [];
        }

        /// <summary>
        /// 从个税「单位办税」里读取人员信息采集名单。内存里没有会话时先用数据库里的登录态恢复。
        /// </summary>
        public async Task<TaxIitPersonnelResultDto> FetchIitPersonnelAsync(
            TaxPortalSessionKey key,
            string companyName,
            CancellationToken cancellationToken)
        {
            var result = await FetchIitAsync(
                key,
                companyName,
                DriveToIitPersonnelAsync,
                payloads => (IitPersonnelPayloadParser.Parse(payloads, out var d), d),
                "queryRyxxcjList",
                cancellationToken);

            return new TaxIitPersonnelResultDto
            {
                Rows = result.Rows,
                NeedLogin = result.NeedLogin,
                Message = result.Message ?? (result.Rows.Count > 0
                    ? null
                    : "已进入个税系统但没有解析出人员信息。请把服务器日志里「[个税取数]」那一行发给开发补解析规则。"),
                Diagnostics = result.Diagnostics,
            };
        }

        /// <summary>从个税「单位办税 → 专项附加扣除信息采集」读取员工的扣除明细。</summary>
        public async Task<TaxIitDeductionResultDto> FetchIitDeductionsAsync(
            TaxPortalSessionKey key,
            string companyName,
            CancellationToken cancellationToken)
        {
            var result = await FetchIitAsync(
                key,
                companyName,
                DriveToIitDeductionAsync,
                payloads => (IitDeductionPayloadParser.Parse(payloads, out var d), d),
                "zxfjkc",
                cancellationToken);

            return new TaxIitDeductionResultDto
            {
                Rows = result.Rows,
                NeedLogin = result.NeedLogin,
                Message = result.Message ?? (result.Rows.Count > 0
                    ? null
                    : "已进入个税系统但没有解析出专项附加扣除。请把服务器日志里「[个税取数]」那一行发给开发补解析规则。"),
                Diagnostics = result.Diagnostics,
            };
        }

        /// <summary>
        /// 导航到「扣缴申报 → 查询统计 → 单位申报记录查询」，读取本单位的申报记录。
        /// start / end 形如「2026-01」，任一为空就沿用税局页面默认的所属期。
        /// </summary>
        public async Task<TaxIitFilingResultDto> FetchIitFilingsAsync(
            TaxPortalSessionKey key,
            string companyName,
            string? start,
            string? end,
            CancellationToken cancellationToken)
        {
            var result = await FetchIitAsync(
                key,
                companyName,
                (session, company, ct) => DriveToIitFilingsAsync(session, company, start, end, ct),
                payloads => (IitFilingPayloadParser.Parse(payloads, out var d), d),
                "queryQySbjl",
                cancellationToken);

            return new TaxIitFilingResultDto
            {
                Rows = result.Rows,
                NeedLogin = result.NeedLogin,
                Message = result.Message ?? (result.Rows.Count > 0
                    ? null
                    : "已进入个税系统但没有解析出申报记录。请把服务器日志里「[个税取数]」那一行发给开发补解析规则。"),
                Diagnostics = result.Diagnostics,
            };
        }

        /// <summary>
        /// 读取个税「查询统计 → 缴税记录查询」。这是扣缴端按人按次的缴款流水，
        /// 与数字账户的 DescribeJkxxcx（企业端缴款信息）不是同一份数据，不能互相填充。
        /// dateField=period 时按税款所属期过滤，否则按缴税日期；start / end 形如 2026-01 或 2026-01-01。
        /// </summary>
        public async Task<TaxIitPaymentResultDto> FetchIitPaymentsAsync(
            TaxPortalSessionKey key,
            string companyName,
            string? dateField,
            string? start,
            string? end,
            CancellationToken cancellationToken)
        {
            var byPeriod = string.Equals(dateField, "period", StringComparison.OrdinalIgnoreCase);
            var result = await FetchIitAsync(
                key,
                companyName,
                (session, company, ct) => DriveToIitPaymentsAsync(session, company, byPeriod, start, end, ct),
                payloads => (IitPaymentPayloadParser.Parse(payloads, byPeriod, start, end, out var d), d),
                "kjsbjsxxCx",
                cancellationToken);

            return new TaxIitPaymentResultDto
            {
                Rows = result.Rows,
                NeedLogin = result.NeedLogin,
                Message = result.Message ?? (result.Rows.Count > 0
                    ? null
                    : "已进入个税系统但没有解析出缴税记录。请把服务器日志里「[个税取数]」那一行发给开发补解析规则。"),
                Diagnostics = result.Diagnostics,
            };
        }

        /// <summary>
        /// 导航到「单位办税 → 缴税记录查询」，按需切筛选列并填日期区间后查询。
        /// 页面日期框各省画法不同，填不进去也不报错：结果仍按所选区间在解析层过滤。
        /// </summary>
        private async Task<List<string>> DriveToIitPaymentsAsync(
            TaxPortalSession session,
            string companyName,
            bool byPeriod,
            string? start,
            string? end,
            CancellationToken cancellationToken)
        {
            var steps = await DriveToIitMenuAsync(session, companyName, "缴税记录查询", null, cancellationToken);
            if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            {
                return steps;
            }

            var page = session.Page ?? throw new InvalidOperationException("个税页面已关闭，请重新发起个税登录。");
            if (byPeriod)
            {
                var chosen = await ClickIitInAnyFrameAsync(
                    page, TaxPortalPageScripts.IitChooseFilter("税款所属期"), cancellationToken);
                steps.Add($"筛选列={(chosen is null ? "miss" : chosen.ToJsonString())}");
                if (chosen is not null && chosen["already"]?.GetValue<bool>() != true)
                {
                    await Task.Delay(600, cancellationToken);
                    var option = await ClickIitInAnyFrameAsync(
                        page, TaxPortalPageScripts.IitClickOption("税款所属期"), cancellationToken);
                    steps.Add($"切到所属期={(option is null ? "miss" : "ok")}");
                    if (option is not null)
                    {
                        await Task.Delay(800, cancellationToken);
                    }
                }
            }

            var filled = await ClickIitInAnyFrameAsync(
                page, TaxPortalPageScripts.IitSetPeriod(start.Trim(), end.Trim()), cancellationToken);
            steps.Add($"日期区间={(filled is null ? "miss" : filled.ToJsonString())}");
            if (filled is null)
            {
                return steps;
            }

            await Task.Delay(800, cancellationToken);
            var queried = await ClickIitQueryInAnyFrameAsync(page, true, cancellationToken);
            steps.Add($"按区间查询={(queried is null ? "miss" : $"ok({queried})")}");
            if (queried is not null)
            {
                await Task.Delay(3500, cancellationToken);
            }

            return steps;
        }

        /// <summary>
        /// 导航到「单位办税 → 人员信息采集」，再把证件号码从脱敏切成明文。
        /// 税局默认只给 3412************97，这种号码同步回档案时对不上人，所以查询完还要点一次明文开关；
        /// 开关后面挂的是阿里云滑块，拖动沿用个税登录那一套拟人封装。
        /// </summary>
        private async Task<List<string>> DriveToIitPersonnelAsync(
            TaxPortalSession session,
            string companyName,
            CancellationToken cancellationToken)
        {
            var steps = await DriveToIitMenuAsync(session, companyName, "人员信息采集", null, cancellationToken);
            await RevealIitPlaintextAsync(session, steps, cancellationToken);
            return steps;
        }

        /// <summary>
        /// 让人员列表把证件号码明文显示出来：先等名册表格真的画出来（「单位办税」是另开的标签页，
        /// 菜单点下去到数据上屏有空档，空档里数不到星号），再数页面上还有多少串打星号的号码，一颗没有就什么都不做；
        /// 有就按文案点明文开关（各省叫法不一，点不到只记一条带全部按钮文案的诊断），弹二次确认就确认，
        /// 弹滑块就按登录同一套轨迹拖；拖完星号还在才补点一次「查询」，让明文随新报文和表格重新下发。
        /// 每一步都写进 steps，取数拿不到明文时日志能直接看出卡在哪一步。
        /// </summary>
        private async Task RevealIitPlaintextAsync(
            TaxPortalSession session, List<string> steps, CancellationToken cancellationToken)
        {
            if (session.Page is not IPage page)
            {
                return;
            }

            var before = await WaitForIitTableAsync(session, page, cancellationToken);
            steps.Add($"脱敏={before.Total}");
            if (before.Total == 0)
            {
                return;
            }

            var plain = await ClickIitPlainInAnyFrameAsync(session, page, cancellationToken);
            if (plain is null || plain["clicked"]?.GetValue<bool>() != true)
            {
                steps.Add("明文开关=miss");
                _logger.LogInformation(
                    "[个税明文] 没找到明文开关（候选 {Cands} 个），页面按钮文案={Buttons}",
                    plain?["cands"]?.GetValue<int>() ?? 0,
                    plain?["btns"]?.ToJsonString() ?? "(读不到)");
                return;
            }

            steps.Add($"明文开关={plain["text"]?.GetValue<string>() ?? "?"}");
            await Task.Delay(1200, cancellationToken);

            var confirmed = await ClickIitEverywhereAsync(session, page, TaxPortalPageScripts.IitConfirmPlain, cancellationToken);
            if (confirmed is not null)
            {
                steps.Add($"二次确认={confirmed["text"]?.GetValue<string>() ?? "ok"}");
                await Task.Delay(800, cancellationToken);
            }

            // 有的省份只做了二次确认、压根不弹滑块，所以只等十秒；等不到就按「不需要滑块」继续，别把取数拖死。
            Func<CancellationToken, Task<IitSlider?>> probe = ct => FindIitSliderAsync(session, page, ct);
            if (await WaitForSliderAsync(probe, 10, 1000, cancellationToken) is not null)
            {
                var failure = await DragIitSliderAsync("个税明文", probe, cancellationToken);
                steps.Add($"滑块={(failure is null ? "ok" : failure)}");
                if (failure is not null)
                {
                    _logger.LogInformation("[个税明文] 滑块没过：{Failure}，证件号码继续按脱敏同步", failure);
                    return;
                }
            }
            else
            {
                steps.Add("滑块=无需");
            }

            await Task.Delay(1500, cancellationToken);
            var after = await ProbeIitMaskedAsync(session, page, cancellationToken);
            steps.Add($"脱敏后={after.Total}");
            if (after.Total == 0)
            {
                return;
            }

            var queried = await ClickIitQueryEverywhereAsync(session, page, true, cancellationToken);
            steps.Add($"重查={(queried is null ? "miss" : $"ok({queried})")}");
            if (queried is not null)
            {
                await Task.Delay(3500, cancellationToken);
            }
        }

        /// <summary>
        /// 脱敏探针的实况：命中多少处、名册表格画出来多少行、认不认得到「证件号码」这一列、
        /// 扫过几个标签页几个帧、头一个样例长什么样。
        /// 早退时也要能从日志里看出「扫到了什么」，否则「页面本来就没星号」和「扫错了标签页／表格还没渲染」长得一样。
        /// </summary>
        private sealed record IitMaskedProbe(int Total, int Rows, int IdCol, int Tabs, int Frames, string Sample);

        /// <summary>
        /// 点明文开关前先等名册表格真的画出来：菜单点下去到数据上屏有几百毫秒到几秒的空档，
        /// 空档里脱敏数是 0，会被当成「本来就明文」直接跳过，明文开关永远轮不到被点。
        /// 只认「有行」不够——门户工作台自己也有表格，所以要等到认出「证件号码」这一列，
        /// 或者已经数到星号（各省表头文案未必带这四个字，认列名认不出来时不能把明文步骤拖死）。
        /// 等不到就按最后一次探针的结果继续，取数本身还有兜底诊断。
        /// </summary>
        private async Task<IitMaskedProbe> WaitForIitTableAsync(
            TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            var probe = await ProbeIitMaskedAsync(session, page, cancellationToken);
            for (var attempt = 0; attempt < 12 && probe.IdCol == 0 && probe.Total == 0; attempt++)
            {
                await Task.Delay(1000, cancellationToken);
                probe = await ProbeIitMaskedAsync(session, page, cancellationToken);
            }

            return probe;
        }

        /// <summary>
        /// 数页面上打星号的脱敏值。和取表格一样跨标签页扫：「单位办税」是另开的标签页，
        /// session.Page 有可能还停在门户首页，只扫当前这一页会一个都没数到，明文开关也就永远不会去点。
        /// </summary>
        private async Task<IitMaskedProbe> ProbeIitMaskedAsync(
            TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            var total = 0;
            var rows = 0;
            var idCol = 0;
            var tabs = 0;
            var frames = 0;
            var sample = string.Empty;
            foreach (var candidate in IitCandidatePages(session, page))
            {
                tabs++;
                foreach (var frame in candidate.Frames)
                {
                    frames++;
                    var seen = await EvaluateJsonAsync(frame, TaxPortalPageScripts.IitMaskedCells, cancellationToken);
                    if (seen is null)
                    {
                        continue;
                    }

                    total += seen["masked"]?.GetValue<int>() ?? 0;
                    // 行数和列数只在「最多的一帧」计数：Element UI 的固定列会克隆一份表体，多帧累加会翻倍。
                    rows = Math.Max(rows, seen["rows"]?.GetValue<int>() ?? 0);
                    idCol = Math.Max(idCol, seen["idCol"]?.GetValue<int>() ?? 0);
                    if (sample.Length == 0 && seen["sample"] is JsonArray list)
                    {
                        sample = string.Join(" | ", list.Select(node => node?.GetValue<string>() ?? string.Empty));
                    }
                }
            }

            _logger.LogInformation(
                "[个税明文] 证件列={IdCol} 表格 {Rows} 行 / 脱敏 {Masked} 处（标签页 {Tabs} 个 / 帧 {Frames} 个），样例={Sample}",
                idCol,
                rows,
                total,
                tabs,
                frames,
                sample.Length == 0 ? "(无)" : sample);
            return new IitMaskedProbe(total, rows, idCol, tabs, frames, sample);
        }

        /// <summary>逐标签页、逐帧跑一个点击类脚本，返回第一个点到的实况（跨页理由同 ProbeIitMaskedAsync）。</summary>
        private static async Task<JsonObject?> ClickIitEverywhereAsync(
            TaxPortalSession session, IPage page, string script, CancellationToken cancellationToken)
        {
            foreach (var candidate in IitCandidatePages(session, page))
            {
                var seen = await ClickIitInAnyFrameAsync(candidate, script, cancellationToken);
                if (seen is not null)
                {
                    return seen;
                }
            }

            return null;
        }

        /// <summary>跨标签页点「查询」，返回点到的那颗是什么（工具栏／面板）。</summary>
        private static async Task<string?> ClickIitQueryEverywhereAsync(
            TaxPortalSession session, IPage page, bool submit, CancellationToken cancellationToken)
        {
            foreach (var candidate in IitCandidatePages(session, page))
            {
                var hit = await ClickIitQueryInAnyFrameAsync(candidate, submit, cancellationToken);
                if (hit is not null)
                {
                    return hit;
                }
            }

            return null;
        }

        /// <summary>
        /// 逐标签页逐帧点明文开关。和 ClickIitEverywhereAsync 的差别：没点着时也要把实况（按钮文案）带回给调用方，
        /// 这个开关各省叫什么名字目前只能从实况里学。
        /// </summary>
        private static async Task<JsonObject?> ClickIitPlainInAnyFrameAsync(
            TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            // 没点着时把所有扫过的帧的按钮文案并成一份带回：光知道「最后一帧有什么按钮」不够，
            // 得看得出明文开关到底挂在哪一帧、叫什么名字。
            var labels = new HashSet<string>(StringComparer.Ordinal);
            var cands = 0;
            foreach (var candidate in IitCandidatePages(session, page))
            {
                foreach (var frame in candidate.Frames)
                {
                    var seen = await EvaluateJsonAsync(frame, TaxPortalPageScripts.IitClickPlain, cancellationToken);
                    if (seen is null)
                    {
                        continue;
                    }

                    if (seen["clicked"]?.GetValue<bool>() == true)
                    {
                        return seen;
                    }

                    cands += seen["cands"]?.GetValue<int>() ?? 0;
                    foreach (var label in (seen["btns"] as JsonArray)?.OfType<JsonNode>() ?? Enumerable.Empty<JsonNode>())
                    {
                        labels.Add(label?.GetValue<string>() ?? string.Empty);
                    }
                }
            }

            if (labels.Count == 0 && cands == 0)
            {
                return null;
            }

            var result = new JsonObject
            {
                ["clicked"] = false,
                ["cands"] = cands,
            };
            var btns = new JsonArray();
            foreach (var label in labels.Take(40))
            {
                btns.Add(label);
            }

            result["btns"] = btns;
            return result;
        }

        /// <summary>
        /// 导航到「单位办税 → 专项附加扣除信息采集」。有的省份把菜单挂在「专项附加扣除」下，
        /// 所以主文案点不到时退一次到短文案。
        /// </summary>
        private static Task<List<string>> DriveToIitDeductionAsync(
            TaxPortalSession session,
            string companyName,
            CancellationToken cancellationToken) =>
            DriveToIitMenuAsync(session, companyName, "专项附加扣除信息采集", "专项附加扣除", cancellationToken);

        /// <summary>
        /// 导航到「扣缴申报 → 查询统计 → 单位申报记录查询」。通用菜单流程会先用税局默认条件查一次，
        /// 这里再把起止所属期填进日期框重查一次；各省日期框画法不同，填不进去也照默认那份结果回。
        /// 菜单文案必须是全名：工作台里另有「集中申报 → 申报记录查询」，那是按个人查的，本单位永远 0 条。
        /// </summary>
        private async Task<List<string>> DriveToIitFilingsAsync(
            TaxPortalSession session,
            string companyName,
            string? start,
            string? end,
            CancellationToken cancellationToken)
        {
            var steps = await DriveToIitMenuAsync(session, companyName, "单位申报记录查询", null, cancellationToken);

            if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            {
                return steps;
            }

            var page = session.Page ?? throw new InvalidOperationException("个税页面已关闭，请重新发起个税登录。");
            var filled = await ClickIitInAnyFrameAsync(
                page, TaxPortalPageScripts.IitSetPeriod(start.Trim(), end.Trim()), cancellationToken);
            steps.Add($"所属期={(filled is null ? "miss" : filled.ToJsonString())}");
            if (filled is null)
            {
                return steps;
            }

            await Task.Delay(800, cancellationToken);
            var queried = await ClickIitQueryInAnyFrameAsync(page, true, cancellationToken);
            steps.Add($"按所属期查询={(queried is null ? "miss" : $"ok({queried})")}");
            if (queried is not null)
            {
                await Task.Delay(3500, cancellationToken);
            }

            return steps;
        }

        /// <summary>
        /// 进入个税「单位办税」并选中本单位，再点目标菜单。各单位办税入口的文案与层级可能调整，
        /// 每一步都按可见文案找元素点，点过的步骤回传给调用方做诊断。
        /// </summary>
        private static async Task<List<string>> DriveToIitMenuAsync(
            TaxPortalSession session,
            string companyName,
            string menuText,
            string? menuFallback,
            CancellationToken cancellationToken)
        {
            var steps = new List<string>();
            var page = session.Page ?? throw new InvalidOperationException("个税页面已关闭，请重新发起个税登录。");
            var home = page;

            // 单位办税入口：登录后首页顶部或工作台，找不到时可能已经在单位工作台里。
            var clicked = await ClickMenuTextInAnyFrameAsync(page, "单位办税", cancellationToken);
            steps.Add($"单位办税={(clicked ? "ok" : "miss")}");

            // 实测税局把「单位办税」开成另一个标签页（/withholding/ 扣缴申报工作台），
            // 继续停在首页找单位、找菜单只会全是 miss，所以先等工作台出现并切过去。
            page = await IitWorkspaceAsync(session, page, cancellationToken);
            steps.Add($"工作台={(ReferenceEquals(page, home) ? "沿用原页" : "已切换")}");

            // 第一次进扣缴工作台会压一层「WEB端扣缴功能使用须知」，不点「我知道了」下面谁都点不到。
            var noticed = await ClickMenuTextInAnyFrameAsync(page, "我知道了", cancellationToken);
            steps.Add($"关须知={(noticed ? "ok" : "miss")}");
            if (noticed)
            {
                await Task.Delay(1500, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(companyName))
            {
                // 扣缴义务人（单位）选择：列表里点本公司那一行，再点确定/进入。
                var companyHit = await ClickMenuTextInAnyFrameAsync(page, companyName, cancellationToken);
                steps.Add($"选择单位={(companyHit ? "ok" : "miss")}");
                if (companyHit)
                {
                    await Task.Delay(2000, cancellationToken);
                    await ClickMenuTextInAnyFrameAsync(page, "确定", cancellationToken);
                    await Task.Delay(2500, cancellationToken);
                }
            }

            // 顶部导航的「扣缴申报」负责把左侧 el-menu 打开；菜单条目此时可能仍是隐藏的，
            // 点不开也不拦着下一步——下面的目标菜单允许直接点隐藏条目。
            var expanded = await ClickIitMenuInAnyFrameAsync(page, "扣缴申报", cancellationToken);
            steps.Add($"展开扣缴申报={expanded ?? "miss"}");
            await Task.Delay(1500, cancellationToken);

            var via = menuText;
            var menuHit = await ClickIitMenuInAnyFrameAsync(page, menuText, cancellationToken);
            if (menuHit is null && menuFallback is not null)
            {
                menuHit = await ClickIitMenuInAnyFrameAsync(page, menuFallback, cancellationToken);
                via = menuFallback;
            }

            steps.Add($"{menuText}={(menuHit is null ? "miss" : $"ok({menuHit})")}{(via == menuFallback ? $"(退到{via})" : string.Empty)}");
            if (menuHit is not null)
            {
                await Task.Delay(2500, cancellationToken);

                // 菜单页只画表格框架：实测落到「人员信息采集」后税局一个请求都不发。
                // 工具栏的「查询」是筛选面板的开关（点完变成「收起」），面板里那颗才真正发列表请求，所以两步走。
                var unfolded = await ClickIitQueryInAnyFrameAsync(page, false, cancellationToken);
                steps.Add($"展开筛选={(unfolded is null ? "miss" : $"ok({unfolded})")}");
                if (unfolded is not null)
                {
                    await Task.Delay(1200, cancellationToken);
                }

                var queried = await ClickIitQueryInAnyFrameAsync(page, true, cancellationToken);
                steps.Add($"查询={(queried is null ? "miss" : $"ok({queried})")}");
                if (queried is not null)
                {
                    await Task.Delay(3500, cancellationToken);
                }
            }

            return steps;
        }

        /// <summary>
        /// 等「单位办税」另开的工作台标签页出现并切过去；已经在工作台里或没有新页就沿用原页。
        /// 取最新那个匹配页：同一会话重复取数会各开一个工作台，旧的可能已经点到别的菜单。
        /// </summary>
        private static async Task<IPage> IitWorkspaceAsync(
            TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            if (page.Url.Contains("/withholding/", StringComparison.OrdinalIgnoreCase))
            {
                return page;
            }

            for (var attempt = 0; attempt < 10; attempt++)
            {
                var opened = IitCandidatePages(session, page)
                    .LastOrDefault(item => !ReferenceEquals(item, page)
                        && item.Url.Contains("/withholding/", StringComparison.OrdinalIgnoreCase));
                if (opened is not null)
                {
                    session.Page = opened;
                    await Task.Delay(2500, cancellationToken);
                    return opened;
                }

                await Task.Delay(1000, cancellationToken);
            }

            return page;
        }

        /// <summary>
        /// 导航失败时把上下文里每个标签页、每个帧的可点元素各打一行日志。
        /// 「单位办税」这类入口在税局侧可能开成新标签页，只看最初那一页会误判成文案变了。
        /// </summary>
        private async Task DescribeIitTabsAsync(TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            var tabs = IitCandidatePages(session, page).ToList();
            for (var tab = 0; tab < tabs.Count; tab++)
            {
                var frames = tabs[tab].Frames;
                for (var frame = 0; frame < frames.Count; frame++)
                {
                    var seen = await EvaluateJsonAsync(frames[frame], TaxPortalPageScripts.IitApps, cancellationToken);
                    if (seen is not null)
                    {
                        _logger.LogInformation(
                            "[个税取数·实况] 标签页 {Tab}/{Tabs} 帧 {Frame}/{Frames} {Detail}",
                            tab, tabs.Count, frame, frames.Count, seen.ToJsonString());
                    }
                }
            }
        }

        /// <summary>
        /// 临时诊断：在个税（ITS）页面里摸清它的加解密落在哪一层，并顺手装一层 <c>JSON.parse</c> 抓手，
        /// 重新点一次「查询」让它自己解密后的明文报文回流。
        /// 报文整包密文（<c>dmnHafHu=</c>）不等于拿不到明文：解密就在这个 JS 上下文里做，
        /// 抓到明文就能按字段接直连，不必在后端复刻算法。摸底完成后可删。
        /// </summary>
        public async Task<object> DebugIitPlainAsync(
            TaxPortalSessionKey key,
            string companyName,
            string? menu,
            CancellationToken cancellationToken)
        {
            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session is null || session.State != TaxPortalStates.Success || session.Page is null)
            {
                return new { needLogin = true, message = "请先完成自然人电子税务局登录，再用本诊断端点。" };
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                var target = string.IsNullOrWhiteSpace(menu) ? "人员信息采集" : menu.Trim();
                // 先按常规流程点到目标菜单（它会用税局默认条件查一次），此时表格已经渲染出来了。
                var steps = await DriveToIitMenuAsync(session, companyName, target, null, cancellationToken);
                var page = session.Page;
                if (page is null)
                {
                    return new { needLogin = true, steps };
                }

                // 钩子装在目标菜单所在的各个帧上：装完再点一次查询，响应才在钩子底下过。
                var hooked = await EvaluateEachIitFrameAsync(session, page, TaxPortalPageScripts.IitPlainHook, cancellationToken);
                var queried = await ClickIitQueryInAnyFrameAsync(page, true, cancellationToken);
                steps.Add($"重查={(queried is null ? "miss" : $"ok({queried})")}");
                if (queried is not null)
                {
                    await Task.Delay(4000, cancellationToken);
                }

                var plain = await EvaluateEachIitFrameAsync(session, page, TaxPortalPageScripts.IitPlainTake(""), cancellationToken);
                var recon = await EvaluateEachIitFrameAsync(session, page, TaxPortalPageScripts.IitCryptoRecon, cancellationToken);
                _logger.LogInformation("[个税摸底] {Url} 步骤={Steps} 明文={Plain}", page.Url, string.Join(" / ", steps), plain.ToJsonString());
                return new
                {
                    url = page.Url,
                    target,
                    steps,
                    hooked,
                    plain,
                    recon
                };
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                return new { error = ex.Message };
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>在所有个税候选标签页的每一帧上跑同一段脚本，按「标签页/帧」回传结果；一帧失败不影响其余。</summary>
        private static async Task<JsonArray> EvaluateEachIitFrameAsync(
            TaxPortalSession session,
            IPage page,
            string script,
            CancellationToken cancellationToken)
        {
            var outArray = new JsonArray();
            var tabIndex = 0;
            foreach (var candidate in IitCandidatePages(session, page))
            {
                var frames = candidate.Frames;
                for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                {
                    JsonObject? seen;
                    try
                    {
                        // 摸底脚本要 fetch 若干份 JS 源码，比常规评估慢，单独放宽到 60 秒。
                        var raw = await frames[frameIndex]
                            .EvaluateAsync<string>(TaxPortalPageScripts.AsJsonAsync(script))
                            .WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
                        seen = string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw) as JsonObject;
                    }
                    catch (Exception ex) when (ex is PlaywrightException or TimeoutException or System.Text.Json.JsonException)
                    {
                        seen = new JsonObject { ["error"] = ex.Message };
                    }

                    if (seen is not null)
                    {
                        outArray.Add(new JsonObject
                        {
                            ["tab"] = tabIndex,
                            ["frame"] = frameIndex,
                            ["href"] = frames[frameIndex].Url,
                            ["result"] = seen
                        });
                    }
                }

                tabIndex++;
            }

            return outArray;
        }

        /// <summary>
        /// 给个税会话挂明文抓手。上下文级初始化脚本管「以后新建的标签页」（单位办税是另开的一页），
        /// 运行时评估管「已经开着的页与帧」，两边都装才不漏第一次查询。
        /// 装失败只记一条警告：抓手是加速通道，掉线了还有读渲染表格这条保底路径。
        /// </summary>
        private async Task InstallIitPlainHookAsync(TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            if (!session.IitPlainHooked && session.Context is { } context)
            {
                try
                {
                    await context.AddInitScriptAsync(TaxPortalPageScripts.AsInvoke(TaxPortalPageScripts.IitPlainHook));
                    session.IitPlainHooked = true;
                }
                catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                {
                    _logger.LogWarning("[个税取数·明文] 初始化脚本挂不上（将只装现有页面）：{Message}", ex.Message);
                }
            }

            try
            {
                await EvaluateEachIitFrameAsync(session, page, TaxPortalPageScripts.IitPlainHook, cancellationToken);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                _logger.LogWarning("[个税取数·明文] 现有页面装抓手失败：{Message}", ex.Message);
            }
        }

        /// <summary>
        /// 收各帧明文抓手缓冲里属于 <paramref name="fragment" /> 这个接口的报文，转成和抓包同形的载荷。
        /// 页面在解密之后才会把文本交给 <c>JSON.parse</c>，所以这里拿到的是可直接套现有解析器的明文字段。
        /// </summary>
        private async Task<List<CapturedJsonPayload>> TakeIitPlainAsync(
            TaxPortalSession session,
            IPage page,
            string? fragment,
            CancellationToken cancellationToken)
        {
            var result = new List<CapturedJsonPayload>();
            if (string.IsNullOrWhiteSpace(fragment))
            {
                return result;
            }

            JsonArray plain;
            try
            {
                plain = await EvaluateEachIitFrameAsync(
                    session, page, TaxPortalPageScripts.IitPlainTake(fragment), cancellationToken);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                _logger.LogWarning("[个税取数·明文] 取缓冲失败：{Message}", ex.Message);
                return result;
            }

            foreach (var entry in plain.OfType<JsonObject>())
            {
                if (entry["result"] is not JsonObject peek)
                {
                    continue;
                }

                foreach (var item in peek["rows"]?.AsArray() ?? [])
                {
                    if (item is not JsonObject row)
                    {
                        continue;
                    }

                    var body = row["body"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        continue;
                    }

                    var url = row["url"]?.GetValue<string>() ?? string.Empty;
                    result.Add(new CapturedJsonPayload(url, body));
                    _logger.LogInformation("[个税取数·明文] 帧 {Frame} {Url} {Size} 字节", entry["frame"]?.ToString() ?? "?", url, body.Length);
                }
            }

            return result;
        }

        /// <summary>
        /// 把各标签页表格里渲染出来的行拼成一份 JSON 报文（键就是税局的列名），供取数解析复用。
        /// 税局表格分页渲染、表体只有当前页，所以读一页就点一次「下一页」，直到分页器没有下一页。
        /// </summary>
        private static async Task<List<CapturedJsonPayload>> ReadRenderedTablesAsync(
            TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            const int maxPages = 50;
            var list = new List<CapturedJsonPayload>();
            foreach (var candidate in IitCandidatePages(session, page))
            {
                var rows = new JsonArray();
                // 每页都会把同页的固定表格再读一遍，按行内容去重，只留新出现的行。
                var seenRows = new HashSet<string>(StringComparer.Ordinal);
                var url = candidate.Url;
                var hash = string.Empty;

                for (var pageIndex = 0; pageIndex < maxPages; pageIndex++)
                {
                    var seen = await EvaluateJsonAsync(candidate, TaxPortalPageScripts.IitTable, cancellationToken);
                    if (seen?["tables"] is not JsonArray tables)
                    {
                        break;
                    }

                    url = seen["url"]?.GetValue<string>() ?? url;
                    hash = seen["hash"]?.GetValue<string>() ?? hash;

                    foreach (var table in tables.OfType<JsonObject>())
                    {
                        foreach (var row in (table["rows"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
                        {
                            var json = row.ToJsonString();
                            if (seenRows.Add(json))
                            {
                                rows.Add(row.DeepClone());
                            }
                        }
                    }

                    if (tables.Count == 0)
                    {
                        break;
                    }

                    var before = await EvaluateJsonAsync(candidate, TaxPortalPageScripts.IitPager(false), cancellationToken);
                    if (before?["next"]?.GetValue<bool>() != true)
                    {
                        break;
                    }

                    var clicked = await EvaluateJsonAsync(candidate, TaxPortalPageScripts.IitPager(true), cancellationToken);
                    if (clicked?["clicked"]?.GetValue<bool>() != true)
                    {
                        break;
                    }

                    if (!await WaitIitPageMovedAsync(candidate, before, cancellationToken))
                    {
                        break;
                    }
                }

                if (rows.Count == 0)
                {
                    continue;
                }

                list.Add(new CapturedJsonPayload($"{url}{hash}#table", rows.ToJsonString()));
            }

            return list;
        }

        /// <summary>
        /// 翻页后等表格真的换页：页码变了，或首行内容变了（有的分页器不标当前页）。
        /// 等不到就停，避免对没刷新的表再点一次「下一页」把中间几页跳过去。
        /// </summary>
        private static async Task<bool> WaitIitPageMovedAsync(
            IPage page, JsonObject before, CancellationToken cancellationToken)
        {
            var beforePage = before["page"]?.GetValue<int>() ?? 0;
            var beforeFirst = before["first"]?.GetValue<string>() ?? string.Empty;
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(200, cancellationToken);
                var state = await EvaluateJsonAsync(page, TaxPortalPageScripts.IitPager(false), cancellationToken);
                if (state is null)
                {
                    continue;
                }

                var pageNow = state["page"]?.GetValue<int>() ?? 0;
                if (pageNow != beforePage)
                {
                    // 页码先变、数据后到，多给一帧渲染时间。
                    await Task.Delay(300, cancellationToken);
                    return true;
                }

                var firstNow = state["first"]?.GetValue<string>() ?? string.Empty;
                if (firstNow.Length > 0 && !string.Equals(firstNow, beforeFirst, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>申报查询接口挂在门户域名下，优先在门户自己的 frame 里执行请求。</summary>
        private static IFrame FindPortalFrame(IPage page, string? homeHost)
        {
            if (homeHost is not null)
            {
                foreach (var frame in page.Frames)
                {
                    if (Uri.TryCreate(frame.Url, UriKind.Absolute, out var uri)
                        && string.Equals(uri.Host, homeHost, StringComparison.OrdinalIgnoreCase))
                    {
                        return frame;
                    }
                }
            }

            return page.MainFrame;
        }

        /// <summary>
        /// 临时诊断：在已登录的税局页面上按给定文字逐步点击，返回页面地址、菜单文字和抓到的 JSON 摘要，
        /// 用于摸清各省发票、申报等业务的菜单路径与接口结构。摸底完成后删除本方法与对应端点。
        /// </summary>
        public async Task<object> ExploreAsync(
            TaxPortalSessionKey key,
            ExploreRequest? request,
            CancellationToken cancellationToken)
        {
            var path = request?.Path ?? [];
            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session?.Page is null)
            {
                return new { needLogin = true };
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                var pages = new List<IPage> { session.Page };
                EventHandler<IPage> onPage = (_, opened) => { lock (pages) pages.Add(opened); };
                if (session.Context is { } ctx)
                {
                    ctx.Page += onPage;
                }

                var page = session.Page;
                var collector = new JsonResponseCollector();
                page.Response += collector.OnResponse;
                var steps = new List<string>();
                try
                {
                    foreach (var text in path)
                    {
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            continue;
                        }

                        var clicked = await ClickMenuTextInAnyFrameAsync(page, text, cancellationToken);
                        steps.Add($"{text}={(clicked ? "ok" : "miss")}");
                        if (!clicked)
                        {
                            break;
                        }

                        await Task.Delay(1600, cancellationToken);

                        // 税局的业务入口常常开新标签页：点击后新开的页面才是要摸的目标。
                        IPage? opened = null;
                        lock (pages)
                        {
                            opened = pages.LastOrDefault();
                        }

                        if (opened is not null && opened != page)
                        {
                            page = opened;
                            page.Response += collector.OnResponse;
                            await Task.Delay(1200, cancellationToken);
                        }
                    }

                    var filled = request?.Fill is { Count: > 0 } fields
                        ? await FillFieldsByLabelAsync(page, fields, cancellationToken)
                        : [];

                    var clickResults = new List<string>();
                    if (request?.Click is { Count: > 0 } clicks)
                    {
                        foreach (var text in clicks)
                        {
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                continue;
                            }

                            var clicked = await ClickMenuTextInAnyFrameAsync(page, text, cancellationToken);
                            clickResults.Add($"{text}={(clicked ? "ok" : "miss")}");
                            await Task.Delay(2500, cancellationToken);
                        }
                    }

                    await Task.Delay(1500, cancellationToken);
                    var evaluated = new List<string>();
                    if (!string.IsNullOrWhiteSpace(request?.Evaluate))
                    {
                        foreach (var frame in page.Frames)
                        {
                            try
                            {
                                var result = await frame.EvaluateAsync<string>(request.Evaluate)
                                    .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                                if (!string.IsNullOrWhiteSpace(result) && result != "{}")
                                {
                                    evaluated.Add($"{frame.Url} ｜ {result}");
                                }
                            }
                            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                            {
                                evaluated.Add($"{frame.Url} ｜ 执行失败：{ex.Message}");
                            }
                        }
                    }

                    var outline = await DescribeMenusAsync(page, cancellationToken);
                    var payloads = await collector.ReadAsync(_logger, cancellationToken);
                    List<IPage> snapshot;
                    lock (pages)
                    {
                        snapshot = pages.ToList();
                    }

                    var openPages = new List<object>();
                    foreach (var item in snapshot)
                    {
                        openPages.Add(new { url = item.Url, title = await GetTitleAsync(item, cancellationToken) });
                    }

                    return new
                    {
                        url = page.Url,
                        title = await GetTitleAsync(page, cancellationToken),
                        steps,
                        openPages,
                        filled,
                        clicks = clickResults,
                        evaluated,
                        outline,
                        payloads = payloads.Take(24).Select(payload => DescribePayload(payload)).ToList(),
                        // 「回的是不是 HTML」只能靠这份证据判断：ReadAsync 会把非 JSON 响应丢掉，
                        // 只回 payloads 就等于把密文（形态 ③）和挑战页（形态 ④）都当成「没抓到」。
                        nonJson = collector.NonJsonSamples.Take(12).ToList(),
                        apiRequests = collector.ApiRequests.Take(12).ToList()
                    };
                }
                finally
                {
                    page.Response -= collector.OnResponse;
                    if (session.Context is { } cleanup)
                    {
                        cleanup.Page -= onPage;
                    }
                }
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>
        /// 临时诊断：在已登录的税局上打开「全量发票查询」，填开票日期并点查询，回传抓到的 JSON 摘要。
        /// 用于确定查询接口与字段结构，正式取数落地后删除。
        /// </summary>
        public async Task<object> DebugInvoiceQueryAsync(
            TaxPortalSessionKey key,
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken,
            bool resetFilters = false)
        {
            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session?.Page is null || session.Context is null)
            {
                return new { needLogin = true };
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                var collector = new JsonResponseCollector();
                var (page, steps) = await EnsureInvoiceQueryPageAsync(session, cancellationToken);
                if (page is null)
                {
                    return new { error = "没找到发票查询页面", steps };
                }

                page.Response += collector.OnResponse;
                page.Request += collector.OnRequest;
                try
                {
                    InvoiceQueryFill? filled;
                    if (resetFilters)
                    {
                        // 先清空所有筛选，再切到「全量」页签后重新填日期：用来分清日期没提交、
                        // 页签停在「近24小时」预设，与「确实没有发票」三种情况。
                        if (await ClickExactTextInAnyFrameAsync(page, "重置", cancellationToken))
                        {
                            steps.Add("重置=ok");
                            await Task.Delay(1000, cancellationToken);
                        }

                        if (await ClickExactTextInAnyFrameAsync(page, "全量", cancellationToken))
                        {
                            steps.Add("全量=ok");
                            await Task.Delay(1000, cancellationToken);
                        }

                        filled = await FillInvoiceQueryAsync(page, start, end, cancellationToken);
                    }
                    else
                    {
                        filled = await FillInvoiceQueryAsync(page, start, end, cancellationToken);
                    }
                    await Task.Delay(2500, cancellationToken);
                    var payloads = await collector.ReadAsync(_logger, cancellationToken);
                    return new
                    {
                        url = page.Url,
                        title = await GetTitleAsync(page, cancellationToken),
                        steps,
                        filled,
                        requests = collector.Requests,
                        state = await EvaluateJsonAsync(page, TaxPortalPageScripts.InvoiceQueryState, cancellationToken),
                        outline = await DescribeMenusAsync(page, cancellationToken),
                        payloads = payloads.Take(20).Select(DescribePayload).ToList()
                    };
                }
                finally
                {
                    page.Response -= collector.OnResponse;
                    page.Request -= collector.OnRequest;
                }
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>
        /// 从「全量发票查询」取开票日期区间内的发票。查询与翻页都由页面自己发请求，
        /// 这里只负责把请求回来的 JSON 收集起来交给解析器，翻到行数不再增加为止。
        /// 发票方向按购销方识别号与本企业税号比对判定，不依赖页面当前停在哪个页签。
        /// </summary>
        public async Task<TaxInvoiceFetchResultDto> FetchInvoicesAsync(
            TaxPortalSessionKey key,
            string ownTaxpayerNumber,
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken)
        {
            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session is null)
            {
                return new TaxInvoiceFetchResultDto
                {
                    NeedLogin = true,
                    Message = "税局登录状态已过期，请重新登录电子税务局。"
                };
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (session.State != TaxPortalStates.Success || session.Context is null)
                {
                    return new TaxInvoiceFetchResultDto
                    {
                        NeedLogin = true,
                        Message = "请先完成税局登录，再一键取票。"
                    };
                }

                var (page, steps) = await EnsureInvoiceQueryPageAsync(session, cancellationToken);
                // 任务中心只显示 Message，导航走到哪一步失败必须写进文案，否则只能看服务器日志。
                var trail = steps.Count == 0
                    ? "没有记录到导航步骤"
                    : string.Join(" → ", steps.Select(step => step.Length <= 60 ? step : step[..60] + "…"));
                if (page is null)
                {
                    var outlinePage = session.Page ?? session.Context?.Pages.LastOrDefault();
                    var outline = outlinePage is null ? "(没有可读页面)" : await DescribeMenusAsync(outlinePage, cancellationToken);
                    _logger.LogWarning("[税局取票] 没找到全量发票查询页，步骤={Steps}，页面结构={Outline}", string.Join(" / ", steps), outline);
                    return new TaxInvoiceFetchResultDto
                    {
                        Message = $"没有找到税局「全量发票查询」页面（{trail}），税局页面结构可能已调整。",
                        Diagnostics = [.. steps, $"页面结构：{outline}"]
                    };
                }

                // 查询结果可能是同标签页里的 iframe / 子页面发的请求，所有页面都挂上监听。
                var collector = new JsonResponseCollector();
                var watchedPages = session.Context!.Pages.ToList();
                foreach (var watched in watchedPages)
                {
                    watched.Response += collector.OnResponse;
                    watched.Request += collector.OnRequest;
                }

                var rows = new List<TaxInvoiceRecord>();
                var diagnostics = (IReadOnlyList<string>)Array.Empty<string>();
                var payloads = (IReadOnlyList<CapturedJsonPayload>)Array.Empty<CapturedJsonPayload>();
                InvoiceQueryFill? fill = null;
                try
                {
                    // 基线必须在点「查询」之前取：点完请求可能立刻回来，取完再记就看不到增量了。
                    var firstBaseline = collector.ResponseCountFrom(InvoiceQueryMarker);
                    await InstallInvoiceQueryCaptureAsync(page, cancellationToken);
                    fill = await FillInvoiceQueryAsync(page, start, end, cancellationToken);

                    // 查询是页面自己发 XHR：每 200ms 看一次接口回来没有，一回来就解析。
                    // 区间内没有发票时也能凭「响应已到」立刻收尾，不再干等满 20 秒。
                    var firstDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
                    while (DateTime.UtcNow < firstDeadline)
                    {
                        await Task.Delay(200, cancellationToken);
                        if (collector.ResponseCountFrom(InvoiceQueryMarker) <= firstBaseline)
                        {
                            continue;
                        }

                        payloads = await collector.ReadAsync(_logger, cancellationToken);
                        var parsed = TaxInvoicePayloadParser.Parse(payloads, ownTaxpayerNumber, out diagnostics);
                        if (parsed.Count > 0)
                        {
                            rows = parsed.ToList();
                            break;
                        }

                        // 查询可能分几次请求回来，给后到的响应留一小段窗口再判「确实没有发票」。
                        await Task.Delay(400, cancellationToken);
                        payloads = await collector.ReadAsync(_logger, cancellationToken);
                        parsed = TaxInvoicePayloadParser.Parse(payloads, ownTaxpayerNumber, out diagnostics);
                        if (parsed.Count > 0)
                        {
                            rows = parsed.ToList();
                        }

                        break;
                    }

                    // 先试直连：页面自己的请求钩子会把明文报文加密，我们只改页码与每页条数。
                    // 直连成不成立取决于页面版本（钩子是否还挂在 XHR 上、报文里有没有页码字段），
                    // 任何一步对不上都退回点「下一页」，不影响结果。
                    var directApplied = false;
                    var firstTotal = ReadInvoiceTotal(payloads);
                    if (rows.Count > 0 && (firstTotal is null || rows.Count < firstTotal))
                    {
                        var direct = await TryFetchInvoicesDirectAsync(page, ownTaxpayerNumber, cancellationToken);
                        if (direct is { Count: > 0 })
                        {
                            rows = direct;
                            directApplied = true;
                        }
                    }

                    // 翻页：点一次「下一页」等到行数增长，不增长说明已经到底。
                    for (var pageIndex = 1; !directApplied && pageIndex < MaxInvoicePages && rows.Count > 0; pageIndex++)
                    {
                        var pageBaseline = collector.ResponseCountFrom(InvoiceQueryMarker);
                        if (!await ClickMenuTextInAnyFrameAsync(page, "下一页", cancellationToken))
                        {
                            break;
                        }

                        var grew = false;
                        var pageDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(12);
                        while (DateTime.UtcNow < pageDeadline)
                        {
                            await Task.Delay(200, cancellationToken);
                            if (collector.ResponseCountFrom(InvoiceQueryMarker) <= pageBaseline)
                            {
                                continue;
                            }

                            payloads = await collector.ReadAsync(_logger, cancellationToken);
                            var parsed = TaxInvoicePayloadParser.Parse(payloads, ownTaxpayerNumber, out diagnostics);
                            if (parsed.Count > rows.Count)
                            {
                                rows = parsed.ToList();
                                grew = true;
                            }

                            break;
                        }

                        if (!grew)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    foreach (var watched in watchedPages)
                    {
                        watched.Response -= collector.OnResponse;
                        watched.Request -= collector.OnRequest;
                    }
                }

                if (await IsLoginPageAsync(page, cancellationToken))
                {
                    _logger.LogInformation("[税局取数] 会话 {SessionId} 已被税局踢回登录页，清理登录态", session.Id);
                    await EndSessionAsync(key, session);
                    return new TaxInvoiceFetchResultDto
                    {
                        NeedLogin = true,
                        Message = "税局登录状态已过期，请重新登录电子税务局。"
                    };
                }

                session.Touch();
                if (rows.Count > 0)
                {
                    _logger.LogInformation(
                        "[税局取票] 区间 {Start}~{End} 解析出 {Count} 张发票",
                        start,
                        end,
                        rows.Count);
                    return new TaxInvoiceFetchResultDto { Rows = rows };
                }

                var fillNote = fill is null
                    ? "日期未填"
                    : $"日期填写 {(fill.Filled.Count == 0 ? "0 个" : string.Join("/", fill.Filled))}"
                      + (fill.Filled.Count == 0 && fill.Placeholders.Count > 0
                          ? $"（页面输入框：{string.Join("、", fill.Placeholders.Take(8))}）"
                          : "")
                      + $"，查询按钮{(fill.Clicked ? "已点" : "没点到")}";
                var evidence = collector.Evidence(payloads.Count);
                var zeroResult = diagnostics.Any(note => note.Contains("返回 0 条", StringComparison.Ordinal));
                if (zeroResult)
                {
                    // 税局明确回了 0 条：这是一次正常的空同步，发票池保持原样，不写任何记录。
                    _logger.LogInformation(
                        "[税局取票] 区间 {Start}~{End} 税局返回 0 条发票，按成功空结果处理",
                        start,
                        end);
                    return new TaxInvoiceFetchResultDto
                    {
                        Diagnostics = [.. steps, fillNote, .. evidence, .. diagnostics]
                    };
                }

                var hint = string.Join("；", new[] { fillNote }.Concat(diagnostics.Take(2)).Concat(evidence));
                hint = hint.Length <= 400 ? hint : hint[..400] + "…";
                _logger.LogWarning(
                    "[税局取票] 区间 {Start}~{End} 未解析出发票，步骤={Steps}，填写={Fill}，抓到的响应：{Diagnostics}",
                    start,
                    end,
                    string.Join(" / ", steps),
                    fillNote,
                    string.Join(" / ", evidence.Concat(diagnostics)));
                return new TaxInvoiceFetchResultDto
                {
                    Message = $"已打开税局发票查询（{trail}），但没有识别出这个区间的发票（{hint}）。请把这条提示发给开发补解析规则。",
                    Diagnostics = [.. steps, fillNote, .. evidence, .. diagnostics]
                };
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>
        /// 找到「全量发票查询」页。首页热门服务的「发票业务」会经 SSO 新开一个门户页，
        /// 门户里可能还要再点一层「发票业务」才有「发票查询」；找不到入口时退回「税务数字账户」，
        /// 发票业务同样挂在数字账户下。每一步都等菜单渲染出来再点，失败时把走过的步骤带回去。
        /// </summary>
        private async Task<(IPage? Page, List<string> Steps)> EnsureInvoiceQueryPageAsync(
            TaxPortalSession session,
            CancellationToken cancellationToken)
        {
            var steps = new List<string>();
            var context = session.Context;
            if (context is null)
            {
                steps.Add("会话里没有浏览器上下文");
                return (null, steps);
            }

            IPage? FindQueryPage() => context.Pages.FirstOrDefault(item => item.Url.Contains("invoice-query", StringComparison.OrdinalIgnoreCase));

            var existing = FindQueryPage();
            if (existing is not null)
            {
                steps.Add("复用已打开的发票查询页");
                return (existing, steps);
            }

            var basePage = session.Page ?? context.Pages.FirstOrDefault();
            if (basePage is null)
            {
                steps.Add("会话里没有可用的税局页面");
                return (null, steps);
            }

            // 刚恢复的会话首页可能还在渲染；取数跑过别的菜单后主页面会停在子页，先回首页。
            await Task.Delay(1200, cancellationToken);
            await ReturnToPortalHomeAsync(session, basePage, steps, cancellationToken);
            await WaitForMenuAsync(basePage, ["发票业务", "税务数字账户", "我要办税", "我要查询"], steps, cancellationToken);

            var entry = await FollowMenuAsync(session, basePage, "发票业务", steps, cancellationToken);
            if (entry is not null)
            {
                var found = await WalkInvoiceQueryMenusAsync(session, entry, steps, cancellationToken);
                if (found is not null)
                {
                    return (found, steps);
                }
            }

            // 首页点不到发票业务时，从数字账户进：数字账户首页同样有发票业务入口。
            await ReturnToPortalHomeAsync(session, basePage, steps, cancellationToken);
            await WaitForMenuAsync(basePage, ["税务数字账户", "发票业务", "我要办税", "我要查询"], steps, cancellationToken);
            var digital = await FollowMenuAsync(session, basePage, "税务数字账户", steps, cancellationToken);
            if (digital is null)
            {
                return (null, steps);
            }

            return (await WalkInvoiceQueryMenusAsync(session, digital, steps, cancellationToken), steps);
        }

        /// <summary>
        /// 在发票应用里逐级点菜单：SSO 门户页只有「发票业务」时再进一层，
        /// 然后点「发票查询」，最后点「全量发票查询」。点完没跳转说明是展开子菜单，同页继续找下一级。
        /// </summary>
        private async Task<IPage?> WalkInvoiceQueryMenusAsync(
            TaxPortalSession session,
            IPage entry,
            List<string> steps,
            CancellationToken cancellationToken)
        {
            var current = entry;
            await WaitForMenuAsync(current, ["全量发票查询", "发票查询", "发票业务", "我要办税", "我要查询"], steps, cancellationToken);

            if (!await HasMenuTextInAnyFrameAsync(current, "发票查询", cancellationToken)
                && await HasMenuTextInAnyFrameAsync(current, "发票业务", cancellationToken))
            {
                var inner = await FollowMenuAsync(session, current, "发票业务", steps, cancellationToken);
                if (inner is not null)
                {
                    current = inner;
                    await WaitForMenuAsync(current, ["全量发票查询", "发票查询", "我要办税", "我要查询"], steps, cancellationToken);
                }
            }

            var queryPage = await FollowMenuAsync(session, current, "发票查询", steps, cancellationToken);
            if (queryPage is null)
            {
                return null;
            }

            await WaitForMenuAsync(queryPage, ["全量发票查询"], steps, cancellationToken);
            var fullPage = await FollowMenuAsync(session, queryPage, "全量发票查询", steps, cancellationToken);
            await Task.Delay(2500, cancellationToken);
            // 「发票查询」可能本身就是查询页，全量发票查询只是同页里的一个筛选项。
            return fullPage ?? queryPage;
        }

        /// <summary>轮询到菜单文案出现为止；超时只记一笔，后面点击失败会再记具体是哪一项。</summary>
        private async Task<bool> WaitForMenuAsync(
            IPage page,
            IReadOnlyList<string> texts,
            List<string> steps,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(25);
            while (DateTime.UtcNow < deadline)
            {
                foreach (var frame in page.Frames)
                {
                    foreach (var text in texts)
                    {
                        if (await EvaluateBoolAsync(frame, TaxPortalPageScripts.HasMenuText(text), cancellationToken))
                        {
                            return true;
                        }
                    }
                }

                await Task.Delay(800, cancellationToken);
            }

            steps.Add($"等待菜单 {string.Join("/", texts)} 超时");
            return false;
        }

        private static async Task<bool> HasMenuTextInAnyFrameAsync(
            IPage page,
            string text,
            CancellationToken cancellationToken)
        {
            foreach (var frame in page.Frames)
            {
                if (await EvaluateBoolAsync(frame, TaxPortalPageScripts.HasMenuText(text), cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 点一个菜单项并跟到点开的页面：税局有的入口新开标签页，有的在原地跳转，
        /// SPA 还可能在地址不变的情况下切换视图，所以点完后等新标签页、等 URL 变化，
        /// 最后都退回原页面交给下一步继续找菜单。
        /// </summary>
        private static async Task<IPage?> FollowMenuAsync(
            TaxPortalSession session,
            IPage page,
            string text,
            List<string> steps,
            CancellationToken cancellationToken)
        {
            var context = session.Context;
            var before = context?.Pages.ToList() ?? [];
            var urlBefore = page.Url;

            var clicked = false;
            for (var attempt = 0; attempt < 3 && !clicked; attempt++)
            {
                clicked = await ClickMenuTextInAnyFrameAsync(page, text, cancellationToken);
                if (!clicked)
                {
                    await Task.Delay(1200, cancellationToken);
                }
            }

            if (!clicked)
            {
                steps.Add($"{text}=miss");
                return null;
            }

            steps.Add($"{text}=ok");
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                var opened = context?.Pages.LastOrDefault(item => !before.Contains(item) && item != page);
                if (opened is not null)
                {
                    // 新标签页通常先过一遍 SSO 再渲染业务菜单，等它落稳再点下一级。
                    await Task.Delay(2500, cancellationToken);
                    steps.Add($"{text}页 {opened.Url}");
                    return opened;
                }

                if (!string.Equals(page.Url, urlBefore, StringComparison.Ordinal))
                {
                    await Task.Delay(1800, cancellationToken);
                    steps.Add($"{text}页 {page.Url}");
                    return page;
                }

                await Task.Delay(400, cancellationToken);
            }

            // 地址没变：可能是 SPA 原地切换，也可能按钮只是展开子菜单，交给下一步再找。
            await Task.Delay(1200, cancellationToken);
            steps.Add($"{text}未跳转");
            return page;
        }

        /// <summary>把会话主页面带回门户首页；已经在首页时什么都不做。</summary>
        private async Task ReturnToPortalHomeAsync(
            TaxPortalSession session,
            IPage page,
            List<string> steps,
            CancellationToken cancellationToken)
        {
            if (page.Url.Contains("/loginb", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var saved = await _stateStore.LoadAsync(session.Key, cancellationToken);
            var homeUrl = string.IsNullOrWhiteSpace(saved?.HomeUrl)
                ? ResolveEtaxHomeUrlFrom(page.Url)
                : saved!.HomeUrl;
            await NavigateAsync(page, homeUrl, cancellationToken);
            await Task.Delay(3000, cancellationToken);
            steps.Add("回到税局首页");
        }

        /// <summary>把当前税局地址的域名换成 etax 门户并回到首页（各省子域名沿用当前访问的省）。</summary>
        private static string ResolveEtaxHomeUrlFrom(string currentUrl)
        {
            if (Uri.TryCreate(currentUrl, UriKind.Absolute, out var uri)
                && uri.Host.Contains(".chinatax.gov.cn", StringComparison.OrdinalIgnoreCase))
            {
                var parts = uri.Host.Split('.');
                if (parts.Length > 2)
                {
                    parts[0] = "etax";
                    return $"{uri.Scheme}://{string.Join('.', parts)}:{uri.Port}/loginb/";
                }
            }

            return "https://etax.zhejiang.chinatax.gov.cn:8443/loginb/";
        }

        /// <summary>
        /// 把开票日期起止填进去并点「查询」。日期走 JS（税局日历控件多为 readonly，Type 填不进值），
        /// 按钮只认完全一致的「查询」——宽匹配会点到同页的「全量发票查询」菜单，请求根本没发出去。
        /// </summary>
        private static async Task<InvoiceQueryFill> FillInvoiceQueryAsync(
            IPage page,
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken)
        {
            var placeholders = new List<string>();
            var filled = new List<string>();
            var startText = "";
            var endText = "";

            foreach (var frame in page.Frames)
            {
                var dates = await EvaluateJsonAsync(
                    frame,
                    TaxPortalPageScripts.FillInvoiceDates(
                        start.ToString("yyyy-MM-dd"),
                        end.ToString("yyyy-MM-dd")),
                    cancellationToken);
                if (dates is not null)
                {
                    if (dates["start"]?.GetValue<bool>() == true && !filled.Contains("开票日期起"))
                    {
                        filled.Add("开票日期起");
                        startText = dates["startText"]?.GetValue<string>() ?? "";
                    }

                    if (dates["end"]?.GetValue<bool>() == true && !filled.Contains("开票日期止"))
                    {
                        filled.Add("开票日期止");
                        endText = dates["endText"]?.GetValue<string>() ?? "";
                    }
                }

                // 找不到日期框时，把这些输入框的占位符带回去，便于按真实写法补匹配规则。
                IReadOnlyList<IElementHandle> inputs;
                try
                {
                    inputs = await frame.QuerySelectorAllAsync("input").WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                }
                catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                {
                    continue;
                }

                foreach (var input in inputs)
                {
                    var placeholder = await input.GetAttributeAsync("placeholder") ?? "";
                    if (placeholder.Length > 0 && placeholders.Count < 40)
                    {
                        placeholders.Add(placeholder);
                    }
                }
            }

            var clicked = await ClickExactTextInAnyFrameAsync(page, "查询", cancellationToken);
            return new InvoiceQueryFill(placeholders, filled, clicked, startText, endText);
        }

        private sealed record InvoiceQueryFill(
            IReadOnlyList<string> Placeholders,
            IReadOnlyList<string> Filled,
            bool Clicked,
            string StartText,
            string EndText);

        /// <summary>给发票查询页的每个 frame 挂上请求钩子；页面刷新过也能重复挂（脚本内部幂等）。</summary>
        private async Task InstallInvoiceQueryCaptureAsync(IPage page, CancellationToken cancellationToken)
        {
            var installed = 0;
            foreach (var frame in page.Frames)
            {
                if (await EvaluateBoolAsync(frame, TaxPortalPageScripts.InstallInvoiceQueryCapture, cancellationToken))
                {
                    installed++;
                }
            }

            _logger.LogInformation("[税局取票] 发票查询页已挂请求钩子（{Count} 个 frame）", installed);
        }

        /// <summary>
        /// 用页面自己的 XHR 直连全量发票查询：点「查询」时钩子已经拿到明文报文，这里只把
        /// pageSize 改大、按页码循环重发。加密（Jmbw）和地址签名都由页面那层钩子完成，
        /// 所以能不能成功取决于页面版本；返回 null 表示直连没走通，调用方退回点「下一页」。
        /// </summary>
        private async Task<List<TaxInvoiceRecord>?> TryFetchInvoicesDirectAsync(
            IPage page,
            string ownTaxpayerNumber,
            CancellationToken cancellationToken)
        {
            // 优先看发票查询页自己的 frame：门户主页面里可能还留着上一次查询的旧钩子记录。
            var frames = page.Frames
                .OrderByDescending(frame => frame.Url.Contains("invoice-query", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var frame in frames)
            {
                var capture = await EvaluateJsonAsync(frame, TaxPortalPageScripts.ReadInvoiceQueryCapture, cancellationToken);
                if (capture?["hits"] is not JsonArray hits || hits.Count == 0)
                {
                    continue;
                }

                var bodies = hits.OfType<JsonObject>()
                    .Select(item => item["body"]?.GetValue<string>() ?? string.Empty)
                    .ToList();
                var original = bodies.LastOrDefault(body =>
                    body.TrimStart().StartsWith('{')
                    && !body.Contains("\"Jmbw\"", StringComparison.Ordinal));
                if (original is null)
                {
                    var encrypted = bodies.LastOrDefault(body => body.Contains("\"Jmbw\"", StringComparison.Ordinal));
                    if (encrypted is not null)
                    {
                        _logger.LogInformation(
                            "[税局取票] 页面钩子只拿到加密报文（{Length} 字符），改回点「下一页」",
                            encrypted.Length);
                    }

                    continue;
                }

                JsonObject? body;
                try
                {
                    body = JsonNode.Parse(original) as JsonObject;
                }
                catch (JsonException)
                {
                    body = null;
                }

                if (body is null)
                {
                    _logger.LogInformation("[税局取票] 页面查询报文不是 JSON 对象，改回点「下一页」");
                    continue;
                }

                var pageKey = FindInvoiceBodyKey(body, "pageno", "pagenum", "pageindex", "page", "current");
                var sizeKey = FindInvoiceBodyKey(body, "pagesize", "rows", "limit", "size");
                if (pageKey is null)
                {
                    _logger.LogInformation(
                        "[税局取票] 查询明情报文没有页码字段（{Keys}），改回点「下一页」",
                        string.Join("/", body.Select(pair => pair.Key)));
                    continue;
                }

                _logger.LogInformation(
                    "[税局取票] 页面查询明情报文：{Body}",
                    original.Length > 800 ? original[..800] : original);
                if (sizeKey is not null)
                {
                    body[sizeKey] = InvoiceDirectPageSize;
                }

                var collected = new List<CapturedJsonPayload>();
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var accepted = 0;
                // 只有确认翻到了末页（响应里的页号不再推进、总条数已拿到）才采用直连结果；
                // 中途失败或接口不认页码时返回 null，交给点「下一页」把结果取全。
                var reachedEnd = false;
                for (var pageIndex = 1; pageIndex <= InvoiceDirectMaxPages; pageIndex++)
                {
                    body[pageKey] = pageIndex;
                    var sent = await SendInvoiceQueryAsync(frame, body.ToJsonString(), cancellationToken);
                    if (sent?["status"]?.GetValue<int>() != 200)
                    {
                        _logger.LogWarning(
                            "[税局取票] 直连接口第 {Page} 页失败：{Result}，改回点「下一页」",
                            pageIndex,
                            sent is null ? "页面脚本超时" : sent.ToJsonString());
                        break;
                    }

                    accepted++;
                    var payload = new CapturedJsonPayload(
                        $"{frame.Url} {InvoiceQueryPath}",
                        sent["text"]?.GetValue<string>() ?? string.Empty);
                    var parsed = TaxInvoicePayloadParser.Parse([payload], ownTaxpayerNumber, out _);
                    if (parsed.Count == 0)
                    {
                        reachedEnd = true;
                        break;
                    }

                    var before = unique.Count;
                    foreach (var row in parsed)
                    {
                        unique.Add($"{row.Direction}|{row.InvoiceNo}");
                    }

                    if (unique.Count == before)
                    {
                        // 页码没生效（返回了同一页）：拿不到后面的数据，改走页面翻页。
                        break;
                    }

                    collected.Add(payload);
                    var total = ReadInvoiceTotal([payload]);
                    if (total is not null && unique.Count >= total)
                    {
                        reachedEnd = true;
                        break;
                    }
                }

                if (accepted > 0 && reachedEnd)
                {
                    var rows = TaxInvoicePayloadParser.Parse(collected, ownTaxpayerNumber, out _).ToList();
                    _logger.LogInformation(
                        "[税局取票] 直连接口取回 {Count} 张发票（每页 {PageSize}，{PageCount} 页）",
                        rows.Count,
                        sizeKey is null ? "页面默认" : InvoiceDirectPageSize,
                        accepted);
                    return rows;
                }
            }

            return null;
        }

        /// <summary>在页面里重发一次查询。明文 body 由页面钩子加密，这里只看状态码与响应文本。</summary>
        private static async Task<JsonObject?> SendInvoiceQueryAsync(
            IFrame frame,
            string bodyJson,
            CancellationToken cancellationToken)
        {
            try
            {
                var raw = await frame
                    .EvaluateAsync<string>(TaxPortalPageScripts.AsJsonAsync(TaxPortalPageScripts.SendInvoiceQuery(bodyJson)))
                    .WaitAsync(TimeSpan.FromSeconds(35), cancellationToken);
                return string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw) as JsonObject;
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException or JsonException)
            {
                return null;
            }
        }

        /// <summary>从发票查询响应里读总条数（Data.Total），读不到返回 null。</summary>
        private static int? ReadInvoiceTotal(IReadOnlyList<CapturedJsonPayload> payloads)
        {
            foreach (var payload in payloads)
            {
                if (!payload.Url.Contains(InvoiceQueryMarker, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var root = JsonNode.Parse(payload.Body);
                    var data = root?["Response"]?["Data"] ?? root?["Data"] ?? root;
                    if (data?["Total"] is JsonValue upper && upper.TryGetValue<int>(out var total))
                    {
                        return total;
                    }

                    if (data?["total"] is JsonValue lower && lower.TryGetValue<int>(out total))
                    {
                        return total;
                    }
                }
                catch (JsonException)
                {
                }
            }

            return null;
        }

        /// <summary>按「忽略大小写与下划线」找报文字段，兼容 pageNo/pageNum/PageSize 等写法。</summary>
        private static string? FindInvoiceBodyKey(JsonObject body, params string[] candidates)
        {
            foreach (var pair in body)
            {
                var normalized = pair.Key.Replace("_", string.Empty).ToLowerInvariant();
                if (candidates.Contains(normalized))
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private static async Task<string> GetTitleAsync(IPage page, CancellationToken cancellationToken)
        {
            try
            {
                return await page.TitleAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                return "";
            }
        }

        /// <summary>把抓到的响应压成「地址 + 顶层字段 + 第一条样本」，便于按真实结构写解析规则。</summary>
        private static string DescribePayload(CapturedJsonPayload payload)
        {
            try
            {
                var node = JsonNode.Parse(payload.Body);
                var url = payload.Url;
                if (url.Length > 160)
                {
                    url = url[..160] + "…";
                }

                if (node is JsonArray array)
                {
                    var first = array.Count > 0 ? array[0]?.ToJsonString() ?? "" : "";
                    return $"{url} ｜ array({array.Count}) ｜ {Clip(first, 1200)}";
                }

                if (node is JsonObject obj)
                {
                    var keys = string.Join(",", obj.Select(pair => pair.Key).Take(30));
                    var firstArray = obj.Select(pair => pair.Value).OfType<JsonArray>().FirstOrDefault();
                    var sample = firstArray is { Count: > 0 }
                        ? $"数组字段样本：{Clip(firstArray[0]?.ToJsonString() ?? "", 1200)}"
                        : Clip(obj.ToJsonString(), 800);
                    return $"{url} ｜ keys={keys} ｜ {sample}";
                }

                return $"{url} ｜ {Clip(payload.Body, 400)}";
            }
            catch (JsonException)
            {
                return $"{payload.Url} ｜ (非 JSON)";
            }
        }

        private static string Clip(string text, int length) => text.Length <= length ? text : text[..length] + "…";

        /// <summary>退出税局：关掉浏览器上下文并删掉数据库里的登录态，Cookie 一并丢弃。</summary>
        public async Task LogoutAsync(TaxPortalSessionKey key)
        {
            if (_sessions.TryRemove(key, out var session))
            {
                await CloseSessionAsync(session);
            }

            await _stateStore.DeleteAsync(key);
        }

        private static TaxKindsResultDto NeedLoginResult(string message)
        {
            return new TaxKindsResultDto
            {
                Rows = Array.Empty<TaxKindRowDto>(),
                NeedLogin = true,
                Message = message
            };
        }

        /// <summary>
        /// 取一个可以取数的会话：内存里已登录的直接用；否则用数据库里的登录态恢复；
        /// 税局侧已过期则返回 null（存档同时清掉），由调用方要求网页重新登录。
        /// </summary>
        private async Task<TaxPortalSession?> GetUsableSessionAsync(TaxPortalSessionKey key, CancellationToken cancellationToken)
        {
            var existing = TryGetSession(key);
            if (existing is not null)
            {
                return existing.State == TaxPortalStates.Success ? existing : null;
            }

            await _restoreGate.WaitAsync(cancellationToken);
            try
            {
                existing = TryGetSession(key);
                if (existing is not null)
                {
                    return existing.State == TaxPortalStates.Success ? existing : null;
                }

                var restored = new TaxPortalSession(key);
                if (!await TryRestoreCoreAsync(restored, cancellationToken))
                {
                    return null;
                }

                // 恢复期间可能有人另起了一次完整登录，那种情况下新会话优先。
                existing = TryGetSession(key);
                if (existing is not null)
                {
                    await DiscardContextAsync(restored);
                    return existing.State == TaxPortalStates.Success ? existing : null;
                }

                _sessions[key] = restored;
                TrimSessions();
                return restored;
            }
            finally
            {
                _restoreGate.Release();
            }
        }

        /// <summary>
        /// 用数据库里保存的 storage state 恢复登录态：开一个带 Cookie 的上下文，打开税局首页，
        /// 看到登录后的界面就算恢复成功。税局自己让会话过期时恢复失败，此时删掉存档。
        /// </summary>
        private async Task<bool> TryRestoreCoreAsync(TaxPortalSession session, CancellationToken cancellationToken)
        {
            var saved = await _stateStore.LoadAsync(session.Key, cancellationToken);
            if (saved is null)
            {
                return false;
            }

            session.SetState(TaxPortalStates.Starting, "正在恢复上次的税局登录…");
            try
            {
                var browser = await GetBrowserAsync();
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    Locale = "zh-CN",
                    TimezoneId = "Asia/Shanghai",
                    ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
                    UserAgent = ChromeUserAgent,
                    StorageState = saved.StorageState
                });
                session.Context = context;
                // 企业门户靠这段脚本伪装正常浏览器；个税站点的瑞数把 navigator.webdriver 的覆写当成自动化指纹，
                // 注入后挑战页会停在空白 document，所以个税会话保持内核默认状态（启动参数已经让 webdriver 为 false）。
                if (session.Key.Kind != TaxPortalKind.Iit)
                {
                    await context.AddInitScriptAsync(TaxPortalPageScripts.StealthScript);
                }

                var page = await context.NewPageAsync();
                session.Page = page;

                _logger.LogInformation("[税局登录] 会话 {SessionId} 尝试恢复保存的登录态", session.Id);
                await NavigateAsync(page, saved.HomeUrl, cancellationToken);
                var restored = await WaitForSuccessAsync(page, TimeSpan.FromSeconds(15), cancellationToken, session.Key.Kind);
                if (restored)
                {
                    await MarkSuccessAsync(session);
                    _logger.LogInformation("[税局登录] 会话 {SessionId} 已恢复税局登录态，无需重新收短信", session.Id);
                    return true;
                }

                // 恢复失败时要看清税局把页面停在哪：落到登录页说明 Cookie 确实过期，
                // 停在业务页却识别不出，就是判定规则要补。
                var diagnose = session.Page is null
                    ? null
                    : await EvaluateJsonAsync(session.Page, TaxPortalPageScripts.DiagnosePage, cancellationToken);
                _logger.LogInformation(
                    "[税局登录] 会话 {SessionId} 保存的税局登录态已失效，恢复后停在 {Url}（{Title}）：{Text}",
                    session.Id,
                    diagnose?["url"]?.GetValue<string>() ?? saved.HomeUrl,
                    diagnose?["title"]?.GetValue<string>() ?? "",
                    Clip(diagnose?["text"]?.GetValue<string>() ?? "", 300));
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException or InvalidOperationException)
            {
                _logger.LogWarning("[税局登录] 恢复税局登录态失败：{Message}", ex.Message);
            }

            await DiscardContextAsync(session);
            await _stateStore.DeleteAsync(session.Key);
            return false;
        }

        private async Task EndSessionAsync(TaxPortalSessionKey key, TaxPortalSession session)
        {
            _sessions.TryRemove(new KeyValuePair<TaxPortalSessionKey, TaxPortalSession>(key, session));
            await CloseSessionAsync(session);
            await _stateStore.DeleteAsync(key);
        }

        /// <summary>
        /// 后台推进登录：先看数据库里有没有上次保存的登录态，能恢复就直接进成功态（不必再收短信）；
        /// 恢复不了才走完整登录流程。
        /// </summary>
        private async Task StartLoginCoreAsync(TaxPortalSession session, TaxPortalCredentials credentials)
        {
            try
            {
                await session.Gate.WaitAsync();
                try
                {
                    if (await TryRestoreCoreAsync(session, CancellationToken.None))
                    {
                        return;
                    }

                    await DriveLoginCoreAsync(session, credentials);
                }
                finally
                {
                    session.Gate.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[税局登录] 会话 {SessionId} 失败：{Message}", session.Id, ex.Message);
                session.SetState(TaxPortalStates.Failed, ex.Message);
            }
        }

        private async Task DriveLoginCoreAsync(TaxPortalSession session, TaxPortalCredentials credentials)
        {
            var browser = await GetBrowserAsync();
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "zh-CN",
                TimezoneId = "Asia/Shanghai",
                ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
                UserAgent = ChromeUserAgent
            });
            session.Context = context;
            await context.AddInitScriptAsync(TaxPortalPageScripts.StealthScript);

            var page = await context.NewPageAsync();
            session.Page = page;
            AttachLoginUrlCapture(session, page);

            var loginUrl = ResolveLoginUrl(credentials.Region);
            session.SetState(TaxPortalStates.Starting, "正在打开电子税务局…");
            _logger.LogInformation("[税局登录] 会话 {SessionId} 打开 {Url}", session.Id, loginUrl);
            await NavigateAsync(page, loginUrl, CancellationToken.None);

            if (!await WaitForPageAsync(page, TimeSpan.FromSeconds(30), CancellationToken.None))
            {
                throw new InvalidOperationException("电子税务局页面加载超时，请检查服务器网络后重试。");
            }

            await Task.Delay(1500);

            if (!await HasPasswordAsync(page, CancellationToken.None))
            {
                session.SetState(TaxPortalStates.Starting, "正在打开税局登录页…");
                await OpenLoginPageAsync(session, page, CancellationToken.None);
            }

            await EvaluateBoolAsync(page, TaxPortalPageScripts.SelectTab(credentials.LoginType), CancellationToken.None);
            if (!await WaitForLoginFormAsync(page, TimeSpan.FromSeconds(10), CancellationToken.None))
            {
                var diagnostic = await DiagnoseAsync(page, CancellationToken.None);
                _logger.LogWarning("[税局登录] 未识别账号密码框，页面={Diagnostic}", diagnostic);
                throw new InvalidOperationException("未识别到税局登录表单，税局页面结构可能已调整。");
            }

            var fill = await EvaluateJsonAsync(
                page,
                TaxPortalPageScripts.FillLogin(credentials.TaxpayerNumber, credentials.Account, credentials.Password),
                CancellationToken.None);
            _logger.LogInformation(
                "[税局登录] 会话 {SessionId} 填表：税号={TaxNo} 账号={Account} 密码={Password}",
                session.Id,
                fill?["taxNo"]?.GetValue<bool>() == true,
                fill?["account"]?.GetValue<bool>() == true,
                fill?["password"]?.GetValue<bool>() == true);
            if (fill?["password"]?.GetValue<bool>() != true)
            {
                _logger.LogWarning("[税局登录] 填表失败，页面={Diagnostic}", await DiagnoseAsync(page, CancellationToken.None));
                throw new InvalidOperationException("税局登录表单已打开但自动填表失败，税局页面结构可能已调整。");
            }

            var manual = await DetectManualAsync(page, CancellationToken.None);
            if (manual is not null)
            {
                // 服务端无头浏览器没有人能拖滑块/看图形码，直接告诉用户，避免一直等待。
                throw new InvalidOperationException(manual);
            }

            session.SetState(TaxPortalStates.Starting, "正在提交税局登录…");
            await EvaluateBoolAsync(page, TaxPortalPageScripts.ClickLogin, CancellationToken.None);
            await Task.Delay(1200);

            var deadline = DateTime.UtcNow.AddSeconds(45);
            var captchaRounds = 0;
            var manualRounds = 0;
            while (true)
            {
                var outcome = await WaitForLoginStageAsync(page, deadline, CancellationToken.None);
                if (outcome == LoginStage.Success)
                {
                    await MarkSuccessAsync(session);
                    return;
                }

                if (outcome == LoginStage.Sms)
                {
                    await EvaluateBoolAsync(page, TaxPortalPageScripts.ClickSendSms, CancellationToken.None);
                    session.SetState(TaxPortalStates.SmsRequired, "税局已发送短信验证码，请填写到本页后提交。");
                    return;
                }

                if (outcome == LoginStage.Captcha)
                {
                    // 先在页内自动点选；连续 MaxCaptchaRounds 轮都不过，才退回网页人工点选。
                    if (captchaRounds < MaxCaptchaRounds)
                    {
                        captchaRounds++;
                        session.SetState(TaxPortalStates.Starting, "正在自动识别税局验证码…");
                        await TryAutoSolveCaptchaAsync(session, page, CancellationToken.None);
                    }
                    else if (manualRounds++ == 0)
                    {
                        _logger.LogWarning(
                            "[税局登录] 会话 {SessionId} 自动点选 {Rounds} 轮未通过，改为网页人工点选",
                            session.Id,
                            captchaRounds);
                        await RelayCaptchaAsync(session, page, CancellationToken.None);
                    }
                    else
                    {
                        throw new InvalidOperationException("税局验证码连续未通过，请稍后重新点击「登录税局」。");
                    }

                    deadline = DateTime.UtcNow.AddSeconds(45);
                    continue;
                }

                break;
            }

            await EvaluateBoolAsync(page, TaxPortalPageScripts.DismissDialog, CancellationToken.None);
            var error = await ReadErrorTextAsync(page, CancellationToken.None);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? "税局登录没有进入短信验证步骤，可能要求完成滑块或图片验证，请稍后重试。"
                : error);
        }

        /// <summary>
        /// 页内自动点选：把提示字与底图交给注入的匹配器算出各字坐标，再在无头浏览器里按顺序点、点「确定」，
        /// 最后看弹层是否收起判断这一轮是否通过。识别失败或这一轮没过都只返回 false，
        /// 由调用方按轮次决定重试，重试够了再退回人工点选。
        /// </summary>
        private async Task<bool> TryAutoSolveCaptchaAsync(TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            var snapshot = await CaptureCaptchaAsync(page, cancellationToken);
            if (snapshot is null)
            {
                _logger.LogWarning("[税局登录] 会话 {SessionId} 没截到点选验证码，无法自动点选", session.Id);
                return false;
            }

            var prompt = snapshot.Captcha.PromptImage;
            var background = snapshot.Captcha.Image;
            if (string.IsNullOrEmpty(prompt) || string.IsNullOrEmpty(background))
            {
                _logger.LogWarning("[税局登录] 会话 {SessionId} 验证码原图缺失，无法自动点选", session.Id);
                return false;
            }

            JsonObject? solved;
            try
            {
                var raw = await snapshot.Frame
                    .EvaluateAsync<string>(TaxPortalPageScripts.SolveCaptcha, new object[] { prompt, background })
                    .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                solved = string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw) as JsonObject;
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException or System.Text.Json.JsonException)
            {
                _logger.LogWarning(ex, "[税局登录] 会话 {SessionId} 验证码自动识别出错", session.Id);
                return false;
            }

            var size = solved?["bg"]?.AsArray();
            var glyphCount = solved?["glyphs"]?.GetValue<int>() ?? 0;
            var clicks = solved?["clicks"]?.AsArray();
            var naturalWidth = size is { Count: >= 2 } ? size[0]?.GetValue<double>() ?? 0 : 0;
            var naturalHeight = size is { Count: >= 2 } ? size[1]?.GetValue<double>() ?? 0 : 0;
            if (glyphCount <= 0 || naturalWidth <= 0 || naturalHeight <= 0 || clicks is null || clicks.Count != glyphCount)
            {
                _logger.LogWarning(
                    "[税局登录] 会话 {SessionId} 验证码只定位到 {Found}/{Total} 个提示字，放弃本轮",
                    session.Id,
                    clicks?.Count ?? 0,
                    glyphCount);
                return false;
            }

            var box = snapshot.Box;
            foreach (var click in clicks)
            {
                var x = click?["x"]?.GetValue<double>() ?? 0;
                var y = click?["y"]?.GetValue<double>() ?? 0;
                await page.Mouse.ClickAsync(
                    (float)(box.X + x / naturalWidth * box.Width),
                    (float)(box.Y + y / naturalHeight * box.Height));
                await Task.Delay(220, cancellationToken);
            }

            if (!await EvaluateBoolAsync(snapshot.Frame, TaxPortalPageScripts.ClickCaptchaConfirm, cancellationToken))
            {
                _logger.LogWarning("[税局登录] 会话 {SessionId} 验证码没有找到「确定」按钮", session.Id);
                return false;
            }

            await Task.Delay(1200, cancellationToken);
            await WaitForCaptchaSettledAsync(page, TimeSpan.FromSeconds(12), cancellationToken);
            var passed = await ReadStageAsync(page, cancellationToken) != LoginStage.Captcha;
            _logger.LogInformation(
                "[税局登录] 会话 {SessionId} 自动点选 {Count} 个点{Result}（识别 {Ms}ms）",
                session.Id,
                clicks.Count,
                passed ? "，验证码已通过" : "，验证码未通过",
                solved?["ms"]?.GetValue<int>() ?? 0);
            return passed;
        }

        /// <summary>
        /// 文字点选的转人工通道：截下提示字与底图交给网页，等用户按顺序点完，
        /// 再在无头浏览器里按同一顺序重放点击并确认。
        /// </summary>
        private async Task RelayCaptchaAsync(TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            var snapshot = await CaptureCaptchaAsync(page, cancellationToken)
                ?? throw new InvalidOperationException("税局要求完成验证码，但没有截取到验证码图片，请稍后重试。");

            session.BeginCaptcha(snapshot.Captcha);
            _logger.LogInformation("[税局登录] 会话 {SessionId} 等待网页点选验证码", session.Id);

            IReadOnlyList<TaxPortalCaptchaClick> clicks;
            try
            {
                clicks = await session.WaitCaptchaAsync(CaptchaWait, cancellationToken);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
                throw new InvalidOperationException("验证码等待超时，请重新点击「登录税局」。");
            }

            var box = snapshot.Box;
            foreach (var click in clicks)
            {
                await page.Mouse.ClickAsync(
                    (float)(box.X + click.X * box.Width),
                    (float)(box.Y + click.Y * box.Height));
                await Task.Delay(220, cancellationToken);
            }

            _logger.LogInformation("[税局登录] 会话 {SessionId} 已重放 {Count} 个验证码点选", session.Id, clicks.Count);
            if (!await EvaluateBoolAsync(snapshot.Frame, TaxPortalPageScripts.ClickCaptchaConfirm, cancellationToken))
            {
                throw new InvalidOperationException("税局验证码页面没有找到「确定」按钮，请稍后重试。");
            }

            await WaitForCaptchaSettledAsync(page, TimeSpan.FromSeconds(10), cancellationToken);
        }

        /// <summary>截取点选验证码：提示字与底图各截一张，再记下底图在视口里的位置供重放点击。</summary>
        private static async Task<CaptchaSnapshot?> CaptureCaptchaAsync(IPage page, CancellationToken cancellationToken)
        {
            foreach (var frame in page.Frames)
            {
                try
                {
                    var geometry = await EvaluateJsonAsync(frame, TaxPortalPageScripts.CaptchaGeometry, cancellationToken);
                    if (geometry?["present"]?.GetValue<bool>() != true)
                    {
                        continue;
                    }

                    var image = frame.Locator("#tpass-captcha-slider-bg-img").First;
                    var box = await image.BoundingBoxAsync().WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                    if (box is null || box.Width < 40 || box.Height < 20)
                    {
                        continue;
                    }

                    var background = InlineImageOrNull(geometry["image"]?.GetValue<string>())
                        ?? ToDataUrl(await image.ScreenshotAsync().WaitAsync(TimeSpan.FromSeconds(8), cancellationToken));
                    var promptImage = "";
                    if (geometry["prompt"]?.GetValue<bool>() == true)
                    {
                        promptImage = InlineImageOrNull(geometry["promptImage"]?.GetValue<string>()) ?? "";
                        if (promptImage.Length == 0)
                        {
                            var tip = frame.Locator("#tpass-captcha-tip-img").First;
                            if (await tip.IsVisibleAsync().WaitAsync(TimeSpan.FromSeconds(3), cancellationToken))
                            {
                                var tipBytes = await tip.ScreenshotAsync().WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                                promptImage = ToDataUrl(tipBytes);
                            }
                        }
                    }

                    var captcha = new TaxPortalCaptchaDto
                    {
                        PromptImage = promptImage,
                        Image = background,
                        Width = (int)Math.Round(box.Width),
                        Height = (int)Math.Round(box.Height)
                    };
                    return new CaptchaSnapshot(captcha, new ImageBox(box.X, box.Y, box.Width, box.Height), frame);
                }
                catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                {
                    // 验证码刷新时元素被替换会取不到，换下一帧再试。
                }
            }

            return null;
        }

        private static string ToDataUrl(byte[] bytes)
        {
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        private static string? InlineImageOrNull(string? candidate)
        {
            return candidate is { Length: > 100 } && candidate.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : null;
        }

        private sealed record CaptchaSnapshot(TaxPortalCaptchaDto Captcha, ImageBox Box, IFrame Frame);

        private readonly record struct ImageBox(double X, double Y, double Width, double Height);

        /// <summary>
        /// 按税局的标准菜单路径逐级点开税费种认定：我要查询 → 一户式查询 → 纳税人信息查询 → 税费种认定信息。
        /// 找不到时换「我要办税 → 综合信息报告 → 应申报税费种信息报告」，最后回门户首页再试一次。
        /// </summary>
        private async Task<bool> TryOpenTaxKindsAsync(IPage page, CancellationToken cancellationToken)
        {
            if (await ClickMenuPathAsync(page, TaxKindsMenuPath, cancellationToken))
            {
                return true;
            }

            if (await ClickMenuPathAsync(page, TaxKindsFallbackPath, cancellationToken))
            {
                return true;
            }

            // 有的省把菜单收在门户首页里，先回首页再按路径找一次。
            try
            {
                var origin = new Uri(page.Url).GetLeftPart(UriPartial.Authority);
                await NavigateAsync(page, origin + "/", cancellationToken);
                await Task.Delay(1500, cancellationToken);
            }
            catch (Exception ex) when (ex is UriFormatException or PlaywrightException)
            {
                return false;
            }

            return await ClickMenuPathAsync(page, TaxKindsMenuPath, cancellationToken)
                || await ClickMenuPathAsync(page, TaxKindsFallbackPath, cancellationToken);
        }

        private async Task<bool> ClickMenuPathAsync(
            IPage page,
            IReadOnlyList<string> path,
            CancellationToken cancellationToken)
        {
            for (var index = 0; index < path.Count; index++)
            {
                var text = path[index];
                if (!await ClickMenuTextInAnyFrameAsync(page, text, cancellationToken))
                {
                    // 只报「没找到」下一轮还是靠猜，把当前页面能看到的文案一起留下，照实补路径。
                    var outline = await DescribeMenusAsync(page, cancellationToken);
                    _logger.LogInformation("[税局取数] 没找到菜单「{Text}」，页面结构：{Outline}", text, outline);
                    return false;
                }

                _logger.LogInformation("[税局取数] 已点击菜单「{Text}」", text);
                await Task.Delay(index == 0 ? 900 : 1200, cancellationToken);
            }

            return true;
        }

        /// <summary>
        /// 在页面的每个帧里跑一次点击脚本，返回第一个「clicked:true」的实况；一个都没命中返回 null。
        /// </summary>
        private static async Task<JsonObject?> ClickIitInAnyFrameAsync(
            IPage page,
            string script,
            CancellationToken cancellationToken)
        {
            foreach (var frame in page.Frames)
            {
                var seen = await EvaluateJsonAsync(frame, script, cancellationToken);
                if (seen?["clicked"]?.GetValue<bool>() == true)
                {
                    return seen;
                }
            }

            return null;
        }

        /// <summary>点 el-menu 菜单项（允许命中被折叠隐藏的条目），返回「怎么点到的」。</summary>
        private static async Task<string?> ClickIitMenuInAnyFrameAsync(
            IPage page,
            string text,
            CancellationToken cancellationToken)
        {
            var seen = await ClickIitInAnyFrameAsync(
                page, TaxPortalPageScripts.IitClickMenu(text), cancellationToken);
            if (seen is null)
            {
                return null;
            }

            var via = seen["via"]?.GetValue<string>() ?? string.Empty;
            var tag = seen["tag"]?.GetValue<string>() ?? string.Empty;
            return $"{via}·{tag}";
        }

        /// <summary>点「查询」（submit=false 只展开筛选面板，true 点面板里那颗发请求），返回点到了什么。</summary>
        private static async Task<string?> ClickIitQueryInAnyFrameAsync(
            IPage page,
            bool submit,
            CancellationToken cancellationToken)
        {
            var seen = await ClickIitInAnyFrameAsync(
                page, TaxPortalPageScripts.IitClickQuery(submit), cancellationToken);
            if (seen is null)
            {
                return null;
            }

            var form = seen["inForm"]?.GetValue<int>() == 1 ? "面板" : "工具栏";
            var tag = seen["tag"]?.GetValue<string>() ?? string.Empty;
            return $"{form}·{tag}";
        }

        private static async Task<bool> ClickMenuTextInAnyFrameAsync(
            IPage page,
            string text,
            CancellationToken cancellationToken)
        {
            foreach (var frame in page.Frames)
            {
                try
                {
                    var raw = await frame.EvaluateAsync<string>(
                            TaxPortalPageScripts.AsJson(TaxPortalPageScripts.ClickMenuText(text)))
                        .WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                    if (raw is not null && raw.Contains("\"clicked\":true", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                {
                }
            }

            return false;
        }

        /// <summary>只点文案完全一致的元素；用于「查询」这类和菜单文案容易混淆的按钮。</summary>
        private static async Task<bool> ClickExactTextInAnyFrameAsync(
            IPage page,
            string text,
            CancellationToken cancellationToken)
        {
            foreach (var frame in page.Frames)
            {
                try
                {
                    var raw = await frame.EvaluateAsync<string>(
                            TaxPortalPageScripts.AsJson(TaxPortalPageScripts.ClickExactText(text)))
                        .WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                    if (raw is not null && raw.Contains("\"clicked\":true", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                {
                }
            }

            return false;
        }

        /// <summary>诊断用：按字段标签（占位符、aria-label 或表单项文字）把值填进输入框。</summary>
        private static async Task<List<string>> FillFieldsByLabelAsync(
            IPage page,
            IReadOnlyDictionary<string, string> fields,
            CancellationToken cancellationToken)
        {
            var results = new List<string>();
            foreach (var (label, value) in fields)
            {
                var matched = false;
                foreach (var frame in page.Frames)
                {
                    IReadOnlyList<IElementHandle> inputs;
                    try
                    {
                        inputs = await frame.QuerySelectorAllAsync("input, textarea")
                            .WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                    }
                    catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                    {
                        continue;
                    }

                    foreach (var input in inputs)
                    {
                        string hint;
                        try
                        {
                            var placeholder = await input.GetAttributeAsync("placeholder") ?? "";
                            var ariaLabel = await input.GetAttributeAsync("aria-label") ?? "";
                            var around = await input.EvaluateAsync<string>(
                                "e => { const p = e.closest('.t-form__item, .form-item, .el-form-item, .ant-form-item, td, label, div'); return p ? (p.innerText || '') : ''; }");
                            hint = $"{placeholder} {ariaLabel} {around}";
                        }
                        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                        {
                            continue;
                        }

                        if (!hint.Contains(label, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        matched = true;
                        try
                        {
                            await input.ClickAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                            await input.FillAsync("").WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                            await input.TypeAsync(value).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                            await page.Keyboard.PressAsync("Enter");
                            results.Add($"{label}={value}");
                        }
                        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                        {
                            results.Add($"{label}=失败：{ex.Message}");
                        }

                        break;
                    }

                    if (matched)
                    {
                        break;
                    }
                }

                if (!matched)
                {
                    results.Add($"{label}=未找到输入框");
                }
            }

            return results;
        }

        private static async Task<string> DescribeMenusAsync(IPage page, CancellationToken cancellationToken)
        {
            var parts = new List<string>();
            foreach (var frame in page.Frames)
            {
                var json = await EvaluateJsonAsync(frame, TaxPortalPageScripts.MenuOutline, cancellationToken);
                if (json is null)
                {
                    continue;
                }

                var url = json["url"]?.GetValue<string>() ?? "";
                var texts = json["texts"]?.AsArray()
                    .Select(node => node?.GetValue<string>() ?? "")
                    .Where(t => t.Length > 0)
                    .ToList() ?? new List<string>();
                parts.Add($"{url} → {string.Join("、", texts)}");
            }

            return parts.Count == 0 ? "(没有可读的菜单)" : string.Join(" ｜ ", parts);
        }

        /// <summary>
        /// 当前页面是否已退回税局登录页（密码框可能在 iframe 里）。
        /// 已登录页面里常驻着隐藏的切换账号表单，所以要求密码框可见或出现明确的超时提示，
        /// 否则会把正常的取数请求误判成登录过期、把可用的登录态删掉。
        /// </summary>
        private static async Task<bool> IsLoginPageAsync(IPage page, CancellationToken cancellationToken)
        {
            foreach (var frame in page.Frames)
            {
                if (await EvaluateBoolAsync(frame, TaxPortalPageScripts.SessionExpired, cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task OpenLoginPageAsync(TaxPortalSession session, IPage page, CancellationToken cancellationToken)
        {
            var clicked = await EvaluateBoolAsync(page, TaxPortalPageScripts.ClickLoginEntry, cancellationToken);
            // 页面自己请求到的 OAuth 地址带着本省 client_id，比 DOM 里找到的链接更可靠。
            var link = session.CapturedLoginUrl ?? await ReadLoginLinkAsync(page, cancellationToken);
            _logger.LogInformation("[税局登录] 首页登录入口 点击={Clicked} 页面登录链接={Link}", clicked, link ?? "");

            await Task.Delay(1200, cancellationToken);
            if (await WaitForLoginFormAsync(page, TimeSpan.FromSeconds(12), cancellationToken))
            {
                return;
            }

            if (!string.IsNullOrEmpty(link)
                && Uri.TryCreate(link, UriKind.Absolute, out var loginUri)
                && IsAllowedTaxHost(loginUri))
            {
                _logger.LogInformation("[税局登录] 打开页面下发的登录地址 {Url}", link);
                await NavigateAsync(page, link, cancellationToken);
                if (await WaitForLoginFormAsync(page, TimeSpan.FromSeconds(20), cancellationToken))
                {
                    return;
                }
            }

            await EvaluateBoolAsync(page, TaxPortalPageScripts.ClickLoginEntry, cancellationToken);
            await WaitForLoginFormAsync(page, TimeSpan.FromSeconds(10), cancellationToken);
        }

        /// <summary>
        /// 税局首页会把本省 OAuth 登录地址放在一个配置响应里（带各省自己的 client_id），
        /// 比自己拼 tpass 地址可靠，所以从响应正文里捞出来备用。
        /// </summary>
        private void AttachLoginUrlCapture(TaxPortalSession session, IPage page)
        {
            page.Response += OnResponse;

            async void OnResponse(object? sender, IResponse response)
            {
                try
                {
                    var url = response.Url;
                    if (!url.Contains("login", StringComparison.OrdinalIgnoreCase)
                        && !url.Contains("config", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    var contentType = response.Headers.TryGetValue("content-type", out var value) ? value : "";
                    if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                        && !contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    var text = await response.TextAsync();
                    var match = Regex.Match(text, "https?://[^\"'\\s\\\\<>]+oauth2/login\\?[^\"'\\s\\\\<>]+");
                    if (!match.Success)
                    {
                        return;
                    }

                    var captured = match.Value
                        .Replace("\\u0026", "&", StringComparison.OrdinalIgnoreCase)
                        .Replace("\\/", "/", StringComparison.Ordinal)
                        .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase);
                    if (Uri.TryCreate(captured, UriKind.Absolute, out var uri) && IsAllowedTaxHost(uri))
                    {
                        session.CapturedLoginUrl = captured;
                        _logger.LogInformation("[税局登录] 捕获到页面下发的登录地址 {Url}", captured);
                    }
                }
                catch (Exception)
                {
                    // 页面导航导致响应体读取失败是常态，忽略。
                }
            }
        }

        private static async Task<string?> ReadLoginLinkAsync(IPage page, CancellationToken cancellationToken)
        {
            var json = await EvaluateJsonAsync(page, TaxPortalPageScripts.ReadLoginLink, cancellationToken);
            var url = json?["url"]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        }

        /// <summary>报税地区必须是省局列表里的正式名称，乱码或历史脏数据会在这里被挡下。</summary>
        public static bool IsSupportedRegion(string? region)
        {
            return RegionSubdomains.ContainsKey((region ?? string.Empty).Trim());
        }

        /// <summary>企业电子税务局首页：按报税地区换到 etax 子域，未识别地区退回全国入口。</summary>
        public static string ResolveEtaxHomeUrl(string? region)
        {
            var name = (region ?? string.Empty).Trim();
            return RegionSubdomains.TryGetValue(name, out var subdomain)
                ? $"https://etax.{subdomain}.chinatax.gov.cn:8443/loginb/"
                : "https://etax.chinatax.gov.cn/";
        }

        private static string ResolveLoginUrl(string? region)
        {
            var name = (region ?? string.Empty).Trim();
            return RegionSubdomains.TryGetValue(name, out var subdomain)
                ? $"https://tpass.{subdomain}.chinatax.gov.cn:8443/"
                : "https://tpass.chinatax.gov.cn:8443/";
        }

        private static bool IsAllowedTaxHost(Uri uri)
        {
            var host = uri.Host;
            return host.Equals("chinatax.gov.cn", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".chinatax.gov.cn", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<IBrowser> GetBrowserAsync()
        {
            if (_browser is { IsConnected: true })
            {
                return _browser;
            }

            await _browserGate.WaitAsync();
            try
            {
                if (_browser is { IsConnected: true })
                {
                    return _browser;
                }

                _playwright ??= await Playwright.CreateAsync();
                try
                {
                    _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = true,
                        Args =
                        [
                            "--no-sandbox",
                            "--disable-dev-shm-usage",
                            "--disable-blink-features=AutomationControlled",
                            "--lang=zh-CN"
                        ]
                    });
                }
                catch (PlaywrightException ex)
                {
                    throw new InvalidOperationException(
                        "服务器尚未安装税局登录所需的浏览器内核，请先在部署环境执行 playwright install chromium。", ex);
                }

                _logger.LogInformation("[税局登录] 无头浏览器已启动：{Version}", _browser.Version);
                return _browser;
            }
            finally
            {
                _browserGate.Release();
            }
        }

        private async Task<bool> WaitForPageAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + timeout;
            do
            {
                if (await EvaluateBoolAsync(page, TaxPortalPageScripts.PageReady, cancellationToken))
                {
                    return true;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    break;
                }

                await Task.Delay(700, cancellationToken);
            }
            while (true);

            return false;
        }

        private async Task<bool> WaitForLoginFormAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + timeout;
            do
            {
                if (await HasPasswordAsync(page, cancellationToken))
                {
                    return true;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    break;
                }

                await Task.Delay(500, cancellationToken);
            }
            while (true);

            return false;
        }

        private static async Task<LoginStage> WaitForLoginStageAsync(IPage page, DateTime deadline, CancellationToken cancellationToken)
        {
            while (DateTime.UtcNow < deadline)
            {
                var stage = await ReadStageAsync(page, cancellationToken);
                if (stage != LoginStage.None)
                {
                    return stage;
                }

                await Task.Delay(700, cancellationToken);
            }

            return LoginStage.None;
        }

        /// <summary>点完「确定」后等验证码弹层收起；收起前的画面仍会报 captcha。</summary>
        private static async Task WaitForCaptchaSettledAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await ReadStageAsync(page, cancellationToken) != LoginStage.Captcha)
                {
                    return;
                }

                await Task.Delay(600, cancellationToken);
            }
        }

        private async Task<bool> WaitForSuccessAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return await WaitForSuccessAsync(page, timeout, cancellationToken, TaxPortalKind.Enterprise);
        }

        /// <summary>按通道类型判定「已进入登录后界面」并轮询等待；个税站点用 IitStage，避免未登录首页假阳性。</summary>
        private async Task<bool> WaitForSuccessAsync(
            IPage page,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            TaxPortalKind kind)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var success = kind == TaxPortalKind.Iit
                    ? await ReadIitStageSuccessAsync(page, cancellationToken)
                    : await IsSuccessAsync(page, cancellationToken);
                if (success)
                {
                    return true;
                }

                await Task.Delay(900, cancellationToken);
            }

            return false;
        }

        private async Task<bool> IsSuccessAsync(IPage page, CancellationToken cancellationToken)
        {
            return await ReadStageAsync(page, cancellationToken) == LoginStage.Success;
        }

        /// <summary>税局的验证码/短信框可能落在 iframe 里，逐帧读一次状态。</summary>
        private static async Task<LoginStage> ReadStageAsync(IPage page, CancellationToken cancellationToken)
        {
            foreach (var frame in page.Frames)
            {
                var json = await EvaluateJsonAsync(frame, TaxPortalPageScripts.Stage, cancellationToken);
                if (json?["success"]?.GetValue<bool>() == true)
                {
                    return LoginStage.Success;
                }

                if (json?["captcha"]?.GetValue<bool>() == true)
                {
                    return LoginStage.Captcha;
                }

                if (json?["sms"]?.GetValue<bool>() == true)
                {
                    return LoginStage.Sms;
                }
            }

            return LoginStage.None;
        }

        private static async Task<string?> DetectManualAsync(IPage page, CancellationToken cancellationToken)
        {
            var slider = await EvaluateJsonAsync(page, TaxPortalPageScripts.DetectSlider, cancellationToken);
            if (slider?["slider"]?.GetValue<bool>() == true)
            {
                return "税局要求拖动滑块验证，服务器端无法自动完成，请稍后重试。";
            }

            var image = await EvaluateJsonAsync(page, TaxPortalPageScripts.DetectImageCaptcha, cancellationToken);
            if (image?["image"]?.GetValue<bool>() == true && image?["empty"]?.GetValue<bool>() == true)
            {
                return "税局要求填写图形验证码，服务器端无法自动完成，请稍后重试。";
            }

            return null;
        }

        private static async Task<string> ReadErrorTextAsync(IPage page, CancellationToken cancellationToken)
        {
            var json = await EvaluateJsonAsync(page, TaxPortalPageScripts.ReadErrorText, cancellationToken);
            return json?["text"]?.GetValue<string>()?.Trim() ?? "";
        }

        private static async Task<string> DiagnoseAsync(IPage page, CancellationToken cancellationToken)
        {
            var json = await EvaluateJsonAsync(page, TaxPortalPageScripts.DiagnosePage, cancellationToken);
            return json?.ToJsonString() ?? "(脚本不可用)";
        }

        private static async Task<bool> HasPasswordAsync(IPage page, CancellationToken cancellationToken)
        {
            return await EvaluateBoolAsync(page, TaxPortalPageScripts.HasPassword, cancellationToken);
        }

        private static async Task<bool> EvaluateBoolAsync(IPage page, string script, CancellationToken cancellationToken)
        {
            try
            {
                return await page.EvaluateAsync<bool>(script).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                return false;
            }
        }

        private static async Task<JsonObject?> EvaluateJsonAsync(IPage page, string script, CancellationToken cancellationToken)
        {
            try
            {
                var raw = await page.EvaluateAsync<string>(TaxPortalPageScripts.AsJson(script))
                    .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                return string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw) as JsonObject;
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException or System.Text.Json.JsonException)
            {
                return null;
            }
        }

        private static async Task<JsonObject?> EvaluateJsonAsync(IFrame frame, string script, CancellationToken cancellationToken)
        {
            try
            {
                var raw = await frame.EvaluateAsync<string>(TaxPortalPageScripts.AsJson(script))
                    .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                return string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw) as JsonObject;
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException or System.Text.Json.JsonException)
            {
                return null;
            }
        }

        private static async Task<bool> EvaluateBoolAsync(IFrame frame, string script, CancellationToken cancellationToken)
        {
            try
            {
                return await frame.EvaluateAsync<bool>(script).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                return false;
            }
        }

        private static async Task NavigateAsync(IPage page, string url, CancellationToken cancellationToken)
        {
            try
            {
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000
                }).WaitAsync(TimeSpan.FromSeconds(65), cancellationToken);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                // 页面已经跳转但静态资源慢时 GotoAsync 会超时；后续用等待脚本判断是否可用。
            }
        }

        /// <summary>进入成功态，并把这次的 Cookie / localStorage 加密写库，下次取数、服务重启都能直接恢复。</summary>
        private async Task MarkSuccessAsync(TaxPortalSession session)
        {
            var fallbackHome = session.Key.Kind == TaxPortalKind.Iit ? IitHomeUrl : "https://tpass.chinatax.gov.cn:8443/";
            session.SetState(TaxPortalStates.Success, session.Key.Kind == TaxPortalKind.Iit ? "已登录自然人电子税务局。" : "已登录电子税务局。");

            var context = session.Context;
            if (context is null)
            {
                return;
            }

            try
            {
                var state = await context.StorageStateAsync().WaitAsync(TimeSpan.FromSeconds(10));
                // 记住登录成功时停留的业务页地址：只回门户根路径时税局往往又落回单点登录入口，
                // 直接回到业务页才能让恢复判定看到「我要办税」这类已登录界面。
                var homeUrl = session.Page is { } page
                    && Uri.TryCreate(page.Url, UriKind.Absolute, out var uri)
                    && uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? uri.AbsoluteUri
                        : fallbackHome;
                await _stateStore.SaveAsync(session.Key, state, homeUrl, CancellationToken.None);
                _logger.LogInformation("[税局登录] 会话 {SessionId} 登录态已加密保存，下次可直接免登录取数", session.Id);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException or IOException or InvalidOperationException)
            {
                // 入库失败不影响本次登录，只是下次需要重新收一次短信。
                _logger.LogWarning("[税局登录] 保存税局登录态失败：{Message}", ex.Message);
            }
        }

        private TaxPortalSession RequireSession(TaxPortalSessionKey key, string message)
        {
            return TryGetSession(key) ?? throw new InvalidOperationException(message);
        }

        private TaxPortalSession? TryGetSession(TaxPortalSessionKey key)
        {
            if (!_sessions.TryGetValue(key, out var session) || session.Removed)
            {
                return null;
            }

            session.Touch();
            return session;
        }

        private void DiscardSession(TaxPortalSessionKey key)
        {
            if (_sessions.TryRemove(key, out var old))
            {
                _ = CloseSessionAsync(old);
            }
        }

        private async Task CloseSessionAsync(TaxPortalSession session)
        {
            session.Removed = true;
            // 顺手唤醒还在等点选的登录任务，免得它挂到超时才去碰已经关掉的页面。
            session.SetState(TaxPortalStates.Failed, "登录会话已关闭。");
            await DiscardContextAsync(session);
        }

        private async Task DiscardContextAsync(TaxPortalSession session)
        {
            var context = session.Context;
            session.Context = null;
            session.Page = null;
            if (context is null)
            {
                return;
            }

            try
            {
                await context.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[税局登录] 关闭浏览器上下文失败：{Message}", ex.Message);
            }
        }

        private void TrimSessions()
        {
            if (_sessions.Count <= MaxSessions)
            {
                return;
            }

            foreach (var pair in _sessions.OrderBy(item => item.Value.UpdatedAt).Take(_sessions.Count - MaxSessions))
            {
                if (_sessions.TryRemove(pair.Key, out var session))
                {
                    _ = CloseSessionAsync(session);
                }
            }
        }

        private async Task PurgeIdleAsync()
        {
            try
            {
                var cutoff = DateTimeOffset.UtcNow - SessionIdle;
                foreach (var pair in _sessions)
                {
                    if (pair.Value.UpdatedAt >= cutoff || !_sessions.TryRemove(pair.Key, out var session))
                    {
                        continue;
                    }

                    await CloseSessionAsync(session);
                    _logger.LogInformation("[税局登录] 会话 {SessionId} 闲置超时，已回收。", session.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[税局登录] 回收闲置会话失败：{Message}", ex.Message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _purgeTimer.Dispose();
            foreach (var pair in _sessions)
            {
                pair.Value.Removed = true;
            }

            _sessions.Clear();
            if (_browser is not null)
            {
                try
                {
                    await _browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception)
                {
                }
            }

            _playwright?.Dispose();
            _browserGate.Dispose();
        }

        private enum LoginStage
        {
            None,
            Captcha,
            Sms,
            Success
        }

        private sealed class TaxPortalSession
        {
            private readonly object _stateLock = new();

            public TaxPortalSession(TaxPortalSessionKey key)
            {
                Key = key;
                Id = Guid.NewGuid().ToString("N");
                UpdatedAt = DateTimeOffset.UtcNow;
            }

            public TaxPortalSessionKey Key { get; }

            public string Id { get; }

            public SemaphoreSlim Gate { get; } = new(1, 1);

            public IBrowserContext? Context { get; set; }

            public IPage? Page { get; set; }

            /// <summary>明文抓手（<c>JSON.parse</c> 包装）是否已经挂到本会话的浏览器上下文上，避免重复挂。</summary>
            public bool IitPlainHooked { get; set; }

            public string? CapturedLoginUrl { get; set; }

            public bool Removed { get; set; }

            public DateTimeOffset UpdatedAt { get; private set; }

            public string State { get; private set; } = TaxPortalStates.Starting;

            public string Message { get; private set; } = "正在打开电子税务局…";

            /// <summary>这一轮登录还在税局侧进行（含等用户填码/扫码）；此时重开一次就等于再提交一次。</summary>
            public bool IsPending => State is TaxPortalStates.Starting
                or TaxPortalStates.CaptchaRequired
                or TaxPortalStates.SmsRequired
                or TaxPortalStates.ScanRequired;

            public TaxPortalCaptchaDto? Captcha { get; private set; }

            /// <summary>个税扫码登录的二维码（data URL）；只在 scan_required 状态对外可见。</summary>
            public string QrImage { get; private set; } = string.Empty;

            /// <summary>登录失败那一刻税局页面的画面（data URL）；只在 failed 状态对外可见。</summary>
            public string FailureShot { get; private set; } = string.Empty;

            private TaskCompletionSource<IReadOnlyList<TaxPortalCaptchaClick>>? _captchaSource;

            public void Touch()
            {
                UpdatedAt = DateTimeOffset.UtcNow;
            }

            public void SetQr(string qrDataUrl)
            {
                lock (_stateLock)
                {
                    QrImage = qrDataUrl;
                }
            }

            public void SetFailureShot(string pngDataUrl)
            {
                lock (_stateLock)
                {
                    FailureShot = pngDataUrl;
                }
            }

            public void SetState(string state, string message)
            {
                lock (_stateLock)
                {
                    State = state;
                    Message = message;
                    UpdatedAt = DateTimeOffset.UtcNow;
                    if (state != TaxPortalStates.CaptchaRequired)
                    {
                        Captcha = null;
                    }

                    if ((state is TaxPortalStates.Failed or TaxPortalStates.Success) && _captchaSource is not null)
                    {
                        _captchaSource.TrySetCanceled();
                        _captchaSource = null;
                    }
                }
            }

            public void BeginCaptcha(TaxPortalCaptchaDto captcha)
            {
                lock (_stateLock)
                {
                    Captcha = captcha;
                    State = TaxPortalStates.CaptchaRequired;
                    Message = "税局要求按顺序点击验证码中的文字，请在本页点选后提交。";
                    UpdatedAt = DateTimeOffset.UtcNow;
                    _captchaSource = new TaskCompletionSource<IReadOnlyList<TaxPortalCaptchaClick>>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            public bool TrySubmitCaptcha(IReadOnlyList<TaxPortalCaptchaClick> clicks)
            {
                lock (_stateLock)
                {
                    if (State != TaxPortalStates.CaptchaRequired || _captchaSource is null)
                    {
                        return false;
                    }

                    Captcha = null;
                    State = TaxPortalStates.Starting;
                    Message = "正在提交税局验证码…";
                    UpdatedAt = DateTimeOffset.UtcNow;
                    var source = _captchaSource;
                    _captchaSource = null;
                    source.TrySetResult(clicks);
                    return true;
                }
            }

            public async Task<IReadOnlyList<TaxPortalCaptchaClick>> WaitCaptchaAsync(
                TimeSpan timeout,
                CancellationToken cancellationToken)
            {
                TaskCompletionSource<IReadOnlyList<TaxPortalCaptchaClick>> source;
                lock (_stateLock)
                {
                    source = _captchaSource
                        ?? throw new InvalidOperationException("当前没有等待点选的税局验证码。");
                }

                return await source.Task.WaitAsync(timeout, cancellationToken);
            }

            public TaxPortalLoginStatusDto Snapshot()
            {
                lock (_stateLock)
                {
                    return new TaxPortalLoginStatusDto
                    {
                        State = State,
                        Message = Message,
                        SessionId = Id,
                        Captcha = Captcha,
                        QrImage = State == TaxPortalStates.ScanRequired ? QrImage : string.Empty,
                        FailureShot = State == TaxPortalStates.Failed ? FailureShot : string.Empty
                    };
                }
            }
        }

        /// <summary>
        /// 把 JSON 收集器挂到会话里的每个标签页，含操作期间新开的那些。
        /// 税局的「单位办税」「应申报税费种信息报告」都是另开一页的，数据全在新页里发出去，
        /// 只盯最初那一页会一个响应都抓不到。
        /// </summary>
        private sealed class PageResponseWatch : IDisposable
        {
            private readonly JsonResponseCollector _collector;
            private readonly IBrowserContext? _context;
            private readonly List<IPage> _watched;
            private readonly EventHandler<IPage> _onPageOpened;

            public PageResponseWatch(IBrowserContext? context, IPage page, JsonResponseCollector collector)
            {
                _collector = collector;
                _context = context;
                _watched = context?.Pages.ToList() ?? [page];
                foreach (var watched in _watched)
                {
                    watched.Response += collector.OnResponse;
                    watched.Request += collector.OnRequest;
                }

                _onPageOpened = (_, opened) =>
                {
                    _watched.Add(opened);
                    opened.Response += collector.OnResponse;
                    opened.Request += collector.OnRequest;
                };

                if (context is not null)
                {
                    context.Page += _onPageOpened;
                }
            }

            public void Dispose()
            {
                _context?.Page -= _onPageOpened;
                foreach (var watched in _watched)
                {
                    watched.Response -= _collector.OnResponse;
                    watched.Request -= _collector.OnRequest;
                }
            }
        }

        /// <summary>收集一次页面操作期间返回的 JSON 响应；税局的数据都在这些响应里。</summary>
        private sealed class JsonResponseCollector
        {
            private const int MaxResponses = 150;
            private const int MaxBodyLength = 2_000_000;

            private readonly List<IResponse> _responses = [];
            private readonly List<string> _urls = [];
            private readonly List<string> _nonJson = [];
            private readonly List<string> _requests = [];
            private readonly List<string> _apiRequests = [];
            private readonly HashSet<string> _nonJsonPaths = new(StringComparer.Ordinal);
            private readonly object _lock = new();
            private int _portalRequests;

            /// <summary>
            /// 发往税局域名的非静态请求数（含导航）。点提交前后的差值能分清
            /// 「提交被表单拦住、一个请求都没出门」和「请求发了但页面没反应」，
            /// 前者不可能触发短信，就不必再干等下去。阿里云滑头的请求在别的域名，不算。
            /// </summary>
            public int PortalRequestCount
            {
                get
                {
                    lock (_lock)
                    {
                        return _portalRequests;
                    }
                }
            }

            public void OnRequest(object? sender, IRequest request)
            {
                try
                {
                    var url = request.Url;
                    if (!url.Contains("chinatax.gov.cn", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    lock (_lock)
                    {
                        _portalRequests++;
                    }

                    var path = url.Split('?')[0];
                    if (IsStaticResource(path))
                    {
                        return;
                    }

                    var method = string.IsNullOrWhiteSpace(request.Method)
                        ? "GET"
                        : request.Method.ToUpperInvariant();
                    if (method == "GET")
                    {
                        // 只留带查询串、又像业务接口的 GET；导航和静态资源太吵，别挤掉诊断。
                        var queryIndex = url.IndexOf('?');
                        if (queryIndex < 0 || !path.Contains("fp", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        var query = url[(queryIndex + 1)..];
                        AddRequestNote($"{method} {path}?{(query.Length > 180 ? query[..180] + "…" : query)}");
                        return;
                    }

                    var body = request.PostData ?? string.Empty;
                    AddRequestNote($"{method} {path} 体={(body.Length > 220 ? body[..220] + "…" : body)}");
                    // 业务接口的请求体单独留一份完整的，用来摸清直连模板；鉴权与门户壳的请求不要。
                    // /web/dkdj/ = 个税扣缴端（响应是 dmnHafHu 密文，但请求体先留着才能判断能不能重放），
                    // /mhzx/ = 门户待办（未申报查询的候选数据源）。
                    if (path.Contains("/szc/", StringComparison.OrdinalIgnoreCase)
                        || path.Contains("/szhz", StringComparison.OrdinalIgnoreCase)
                        || path.Contains("/qlfpcx", StringComparison.OrdinalIgnoreCase)
                        || path.Contains("/hdjl/", StringComparison.OrdinalIgnoreCase)
                        || path.Contains("/web/dkdj/", StringComparison.OrdinalIgnoreCase)
                        || path.Contains("/mhzx/", StringComparison.OrdinalIgnoreCase))
                    {
                        lock (_lock)
                        {
                            if (_apiRequests.Count < 30)
                            {
                                _apiRequests.Add($"{method} {url} 体={Clip(body, 4000)}");
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            private void AddRequestNote(string note)
            {
                lock (_lock)
                {
                    if (_requests.Count < 20 && !_requests.Contains(note))
                    {
                        _requests.Add(note);
                    }
                }
            }

            /// <summary>这段时间里页面提交给税局的接口请求（含 POST 体），用来核对日期区间有没有真的发出去。</summary>
            public IReadOnlyList<string> Requests
            {
                get
                {
                    lock (_lock)
                    {
                        return _requests.ToList();
                    }
                }
            }

            /// <summary>业务接口的完整请求（URL + 报文），用来摸清直连模板。</summary>
            public IReadOnlyList<string> ApiRequests
            {
                get
                {
                    lock (_lock)
                    {
                        return _apiRequests.ToList();
                    }
                }
            }

            /// <summary>这段时间里页面发过哪些请求（去掉查询串、静态资源）：用来分清「没发请求」还是「发了但没解析出来」。</summary>
            public IReadOnlyList<string> SeenUrls
            {
                get
                {
                    lock (_lock)
                    {
                        return _urls.ToList();
                    }
                }
            }

            /// <summary>
            /// 地址里含指定片段的响应条数。查询和翻页都用它判断「这一批请求回来了没有」，
            /// 回来就立刻读报文，不必按固定间隔空转。
            /// </summary>
            public int ResponseCountFrom(string fragment)
            {
                lock (_lock)
                {
                    var count = 0;
                    foreach (var response in _responses)
                    {
                        try
                        {
                            if (response.Url.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                            {
                                count++;
                            }
                        }
                        catch (PlaywrightException)
                        {
                        }
                    }

                    return count;
                }
            }

            /// <summary>非 JSON 响应的实况（状态码 + 地址 + 报文头部），密文改写只有在这儿看得见。</summary>
            public IReadOnlyList<string> NonJsonSamples
            {
                get
                {
                    lock (_lock)
                    {
                        return _nonJson.ToList();
                    }
                }
            }

            /// <summary>
            /// 一句能自证的诊断：抓到几份 JSON、页面发过哪些请求、有哪些响应不是 JSON。
            /// 「没解析出来」和「什么都没回来」是两回事，分不开就只能靠用户再跑一遍。
            /// </summary>
            public IReadOnlyList<string> Evidence(int jsonPayloads)
            {
                var lines = new List<string> { $"JSON={jsonPayloads} 请求={SeenUrls.Count}" };
                if (SeenUrls.Count > 0)
                {
                    lines.Add("路径: " + string.Join(" | ", SeenUrls.Take(12)));
                }

                if (Requests.Count > 0)
                {
                    lines.Add("提交: " + string.Join(" | ", Requests.Take(4)));
                }

                if (NonJsonSamples.Count > 0)
                {
                    lines.Add("非 JSON: " + string.Join(" | ", NonJsonSamples.Take(6)));
                }

                return lines;
            }

            private static bool IsStaticResource(string path)
                => path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase);

            public void OnResponse(object? sender, IResponse response)
            {
                try
                {
                    var url = response.Url;

                    // 税局的静态资源带着 ?0y1tUn9l=… 这类令牌串，只看 EndsWith(".js") 会漏掉，
                    // 先剥掉查询串再判扩展名。
                    var path = url.Split('?')[0];
                    if (IsStaticResource(path))
                    {
                        return;
                    }

                    lock (_lock)
                    {
                        if (_responses.Count < MaxResponses)
                        {
                            _responses.Add(response);
                        }

                        if (_urls.Count < MaxResponses && !_urls.Contains(path))
                        {
                            _urls.Add(path);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            public async Task<IReadOnlyList<CapturedJsonPayload>> ReadAsync(ILogger logger, CancellationToken cancellationToken)
            {
                List<IResponse> snapshot;
                lock (_lock)
                {
                    snapshot = _responses.ToList();
                }

                var payloads = new List<CapturedJsonPayload>();
                foreach (var response in snapshot)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var body = await response.TextAsync().WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                        if (body.Length == 0
                            || body.Length > MaxBodyLength)
                        {
                            continue;
                        }

                        if (body[0] != '{' && body[0] != '[')
                        {
                            // 税局查询接口回的不是 JSON 时，多半是被前置系统改写了（挑战页/密文/空壳），
                            // 光看「没抓到数据」没法判断，把状态码和头部留下。
                            var path = response.Url.Split('?')[0];
                            var head = body.Length > 160 ? body[..160] : body;
                            logger.LogDebug(
                                "[税局取数] 非 JSON 响应 {Status} {Url} 头部={Head}",
                                response.Status,
                                path,
                                head);
                            lock (_lock)
                            {
                                if (_nonJsonPaths.Add(path) && _nonJson.Count < MaxResponses)
                                {
                                    _nonJson.Add($"{response.Status} {path} 头部={(head.Length > 60 ? head[..60] : head)}");
                                }
                            }
                            continue;
                        }

                        payloads.Add(new CapturedJsonPayload(response.Url, body));
                    }
                    catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                    {
                        logger.LogDebug("[税局取数] 响应体已释放，跳过 {Url}", response.Url);
                    }
                }

                return payloads;
            }
        }
    }
}
