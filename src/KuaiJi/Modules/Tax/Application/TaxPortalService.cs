using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KuaiJi.Modules.Tax.Infrastructure;
using Microsoft.Playwright;

namespace KuaiJi.Modules.Tax.Application
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

        private const string ChromeUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";

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
        /// 进入税局相关页面时先确认登录态还能不能用：内存里有登录会话就直接回报它的进展，
        /// 没有就用数据库里保存的登录态恢复一次（服务重启、闲置回收之后也能免登录）。
        /// 恢复不了说明税局那边已过期，返回 Failed 让网页弹窗重新收一次短信。
        /// </summary>
        public async Task<TaxPortalLoginStatusDto> ResumeAsync(TaxPortalSessionKey key, CancellationToken cancellationToken)
        {
            var existing = TryGetSession(key);
            if (existing is not null)
            {
                return existing.Snapshot();
            }

            var restored = await GetUsableSessionAsync(key, cancellationToken);
            if (restored is not null)
            {
                return restored.Snapshot();
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
        /// </summary>
        public async Task<TaxKindsResultDto> FetchTaxKindsAsync(TaxPortalSessionKey key, CancellationToken cancellationToken)
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
                var collector = new JsonResponseCollector();
                page.Response += collector.OnResponse;
                bool entered;
                try
                {
                    entered = await TryOpenTaxKindsAsync(page, cancellationToken);
                    await Task.Delay(3000, cancellationToken);
                }
                finally
                {
                    page.Response -= collector.OnResponse;
                }

                if (await IsLoginPageAsync(page, cancellationToken))
                {
                    // 页面被税局踢回登录页：服务端存着的 Cookie 已失效，删掉存档让网页重新收短信。
                    _logger.LogInformation("[税局取数] 会话 {SessionId} 已被税局踢回登录页，清理登录态", session.Id);
                    await EndSessionAsync(key, session);
                    return NeedLoginResult("税局登录状态已过期，请重新登录电子税务局。");
                }

                var payloads = await collector.ReadAsync(_logger, cancellationToken);
                var rows = TaxKindPayloadParser.Parse(payloads, out var diagnostics);
                session.Touch();

                if (rows.Count > 0)
                {
                    return new TaxKindsResultDto { Rows = rows };
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

                _logger.LogWarning(
                    "[税局取数] 未解析出税费种，进入页面={Entered}，抓到的响应：{Diagnostics}",
                    entered,
                    string.Join(" / ", diagnostics));

                return new TaxKindsResultDto
                {
                    Rows = Array.Empty<TaxKindRowDto>(),
                    Message = "已打开税局税费种页面，但没有识别出认定信息。请把服务器日志里「[税局取数]」那一行发给开发补解析规则。",
                    Diagnostics = diagnostics
                };
            }
            finally
            {
                session.Gate.Release();
            }
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
            var session = await GetUsableSessionAsync(key, cancellationToken);
            if (session is null)
            {
                return new TaxDeclarationsResultDto
                {
                    NeedLogin = true,
                    Message = "税局登录状态已过期，请重新登录电子税务局。"
                };
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (session.State != TaxPortalStates.Success || session.Page is null)
                {
                    return new TaxDeclarationsResultDto
                    {
                        NeedLogin = true,
                        Message = "请先完成税局登录，再查询申报记录。"
                    };
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

                var monthStart = new DateOnly(year, month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var yearStart = new DateOnly(year, 1, 1);
                var yearEnd = new DateOnly(year, 12, 31);

                var expression = "(async()=>{const r=await fetch('/szc/szzh/sjswszzh/zhcx/v1/DescribeSbmxcx2',"
                    + "{method:'POST',headers:{'Content-Type':'application/json'},credentials:'include',"
                    + $"body:JSON.stringify({{PageNumber:1,PageSize:200,skssqq:'{yearStart:yyyy-MM-dd}',skssqz:'{yearEnd:yyyy-MM-dd}'}})}});"
                    + "return await r.text();})()";

                var frame = FindPortalFrame(page, homeHost);
                var raw = await frame.EvaluateAsync<string>(expression)
                    .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken) ?? "";

                if (await IsLoginPageAsync(page, cancellationToken))
                {
                    _logger.LogInformation("[税局取数] 会话 {SessionId} 查询申报记录时被踢回登录页", session.Id);
                    await EndSessionAsync(key, session);
                    return new TaxDeclarationsResultDto
                    {
                        NeedLogin = true,
                        Message = "税局登录状态已过期，请重新登录电子税务局。"
                    };
                }

                var result = TaxDeclarationPayloadParser.Parse(raw, monthStart, monthEnd, out var diagnostics);
                session.Touch();
                if (result.Rows.Count == 0 && result.Message is null)
                {
                    result = new TaxDeclarationsResultDto
                    {
                        Rows = result.Rows,
                        Message = "税局没有返回申报记录。",
                        Diagnostics = diagnostics
                    };
                }

                return result;
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                _logger.LogWarning("[税局取数] 查询申报记录失败：{Message}", ex.Message);
                return new TaxDeclarationsResultDto
                {
                    Message = "读取税局申报记录失败，请稍后重试。",
                    Diagnostics = [ex.Message]
                };
            }
            finally
            {
                session.Gate.Release();
            }
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
                        payloads = payloads.Take(24).Select(payload => DescribePayload(payload)).ToList()
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
            CancellationToken cancellationToken)
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
                try
                {
                    var filled = await FillInvoiceQueryAsync(page, start, end, cancellationToken);
                    await Task.Delay(2500, cancellationToken);
                    var payloads = await collector.ReadAsync(_logger, cancellationToken);
                    return new
                    {
                        url = page.Url,
                        title = await GetTitleAsync(page, cancellationToken),
                        steps,
                        filled,
                        outline = await DescribeMenusAsync(page, cancellationToken),
                        payloads = payloads.Take(20).Select(DescribePayload).ToList()
                    };
                }
                finally
                {
                    page.Response -= collector.OnResponse;
                }
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>找到已打开的全量发票查询页；没有就按菜单点进去（税局会开新标签页）。</summary>
        private async Task<(IPage? Page, List<string> Steps)> EnsureInvoiceQueryPageAsync(
            TaxPortalSession session,
            CancellationToken cancellationToken)
        {
            var steps = new List<string>();
            IPage? FindQueryPage() => session.Context?.Pages.FirstOrDefault(item => item.Url.Contains("invoice-query", StringComparison.OrdinalIgnoreCase));

            var existing = FindQueryPage();
            if (existing is not null)
            {
                steps.Add("复用已打开的发票查询页");
                return (existing, steps);
            }

            var basePage = session.Page;
            var context = session.Context;
            if (basePage is null || context is null)
            {
                return (null, steps);
            }

            // 刚恢复的会话首页可能还在渲染，热门服务区要等出来再点；取数跑过别的菜单后
            // 主页面会停在子页，先回首页再找「发票业务」。
            await Task.Delay(1200, cancellationToken);
            if (!basePage.Url.Contains("/loginb", StringComparison.OrdinalIgnoreCase))
            {
                await NavigateAsync(basePage, ResolveEtaxHomeUrlFrom(basePage.Url), cancellationToken);
                await Task.Delay(2600, cancellationToken);
                steps.Add("回到税局首页");
            }

            var before = context.Pages.ToList();
            var opened = await ClickMenuTextInAnyFrameAsync(basePage, "发票业务", cancellationToken);
            steps.Add($"发票业务={(opened ? "ok" : "miss")}");
            if (!opened)
            {
                return (null, steps);
            }

            IPage? business = null;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(12);
            while (business is null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(500, cancellationToken);
                business = context.Pages.LastOrDefault(item => !before.Contains(item));
            }

            if (business is null)
            {
                steps.Add("没开新标签页");
                return (null, steps);
            }

            await Task.Delay(2000, cancellationToken);
            steps.Add($"发票业务页 {business.Url}");

            var queryClick = await ClickMenuTextInAnyFrameAsync(business, "发票查询", cancellationToken);
            steps.Add($"发票查询={(queryClick ? "ok" : "miss")}");
            if (!queryClick)
            {
                return (null, steps);
            }

            await Task.Delay(2000, cancellationToken);
            var page = FindQueryPage() ?? business;
            var full = await ClickMenuTextInAnyFrameAsync(page, "全量发票查询", cancellationToken);
            steps.Add($"全量发票查询={(full ? "ok" : "miss")}");
            await Task.Delay(2800, cancellationToken);
            return (FindQueryPage() ?? page, steps);
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

        /// <summary>把开票日期起止填进去并点「查询」，返回找到的输入框与点击结果，便于按真实页面调整。</summary>
        private static async Task<object> FillInvoiceQueryAsync(
            IPage page,
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken)
        {
            var placeholders = new List<string>();
            var filled = new List<string>();
            foreach (var frame in page.Frames)
            {
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

                    var isStart = placeholder.Contains("开票日期（起）") || placeholder.Contains("开票日期(起)");
                    var isEnd = placeholder.Contains("开票日期（止）") || placeholder.Contains("开票日期(止)");
                    if (!isStart && !isEnd)
                    {
                        continue;
                    }

                    try
                    {
                        await input.ClickAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                        await input.FillAsync("");
                        await input.TypeAsync(isStart ? start.ToString("yyyy-MM-dd") : end.ToString("yyyy-MM-dd"));
                        await page.Keyboard.PressAsync("Enter");
                        filled.Add($"{placeholder}={(isStart ? start : end):yyyy-MM-dd}");
                    }
                    catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
                    {
                        filled.Add($"{placeholder}=失败：{ex.Message}");
                    }
                }
            }

            var clicked = await ClickMenuTextInAnyFrameAsync(page, "查询", cancellationToken);
            return new { placeholders, filled, clicked };
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
                await context.AddInitScriptAsync(TaxPortalPageScripts.StealthScript);
                var page = await context.NewPageAsync();
                session.Page = page;

                _logger.LogInformation("[税局登录] 会话 {SessionId} 尝试恢复保存的登录态", session.Id);
                await NavigateAsync(page, saved.HomeUrl, cancellationToken);
                if (await WaitForSuccessAsync(page, TimeSpan.FromSeconds(15), cancellationToken))
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
                    _logger.LogInformation("[税局取数] 没找到菜单「{Text}」", text);
                    return false;
                }

                _logger.LogInformation("[税局取数] 已点击菜单「{Text}」", text);
                await Task.Delay(index == 0 ? 900 : 1200, cancellationToken);
            }

            return true;
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
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await IsSuccessAsync(page, cancellationToken))
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
            session.SetState(TaxPortalStates.Success, "已登录电子税务局。");

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
                        : "https://tpass.chinatax.gov.cn:8443/";
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

            public string? CapturedLoginUrl { get; set; }

            public bool Removed { get; set; }

            public DateTimeOffset UpdatedAt { get; private set; }

            public string State { get; private set; } = TaxPortalStates.Starting;

            public string Message { get; private set; } = "正在打开电子税务局…";

            public TaxPortalCaptchaDto? Captcha { get; private set; }

            private TaskCompletionSource<IReadOnlyList<TaxPortalCaptchaClick>>? _captchaSource;

            public void Touch()
            {
                UpdatedAt = DateTimeOffset.UtcNow;
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
                        Captcha = Captcha
                    };
                }
            }
        }

        /// <summary>收集一次页面操作期间返回的 JSON 响应；税局的数据都在这些响应里。</summary>
        private sealed class JsonResponseCollector
        {
            private const int MaxResponses = 150;
            private const int MaxBodyLength = 2_000_000;

            private readonly List<IResponse> _responses = [];
            private readonly object _lock = new();

            public void OnResponse(object? sender, IResponse response)
            {
                try
                {
                    var url = response.Url;
                    if (url.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                        || url.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                        || url.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        || url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        || url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                        || url.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
                        || url.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    lock (_lock)
                    {
                        if (_responses.Count < MaxResponses)
                        {
                            _responses.Add(response);
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
                            || body.Length > MaxBodyLength
                            || (body[0] != '{' && body[0] != '['))
                        {
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
