using System.Text;
using System.Threading.RateLimiting;
using YunCai.SharedKernel.Infrastructure.Api;
using YunCai.SharedKernel.Infrastructure.Binding;
using YunCai.SharedKernel.Infrastructure.Data;
using YunCai.SharedKernel.Infrastructure.Runtime;
using Microsoft.OpenApi;
using YunCai.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 本机凭据（数据库连接串、JWT 签名密钥）放在不入库的 appsettings.Local.json。
// 追加在默认源之后会盖过环境变量，所以补加一次环境变量源，保证优先级：
// appsettings.json < appsettings.{环境}.json < appsettings.Local.json < 环境变量 < 命令行参数。
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

var runtime = builder.Configuration.GetSection(RuntimeOptions.SectionName).Get<RuntimeOptions>() ?? new RuntimeOptions();

// Add services to the container.
var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext();

// 容器与系统服务里工作目录不可靠，默认只输出到控制台；需要落盘时配置 YunCai:Logging:File 为绝对路径。
if (!string.IsNullOrWhiteSpace(runtime.Logging.File))
{
    loggerConfiguration.WriteTo.File(
        runtime.Logging.File,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
}

Log.Logger = loggerConfiguration.CreateLogger();

builder.Services.AddSerilog(Log.Logger);

builder.Services.AddControllers(options =>
{
    // 前端生成的客户端会把日期参数序列化成 ISO 8601 带时间缀，DateOnly 默认绑定会 400。
    options.ModelBinderProviders.Insert(0, new LenientDateOnlyModelBinderProvider());
    // 全局账套访问控制：路由带账套标识的接口只放行该账套的启用成员。
    options.Filters.AddService<YunCai.Modules.AccountSets.Api.AccountSetAccessFilter>();
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // .NET 10 的 schema 生成会把数值字段描述成 ["integer"/"number","string"] 联合类型
    // （允许从字符串读取数值），这会让 openapi-generator 产出无法使用的空对象类型。
    // 这里剥离多余的 string，使数值字段生成为纯 integer/number。
    options.AddSchemaTransformer((schema, context, cancellationToken) =>
    {
        var type = schema.Type;
        if (type is not null
            && (type.Value & JsonSchemaType.String) != 0
            && (type.Value & (JsonSchemaType.Integer | JsonSchemaType.Number)) != 0)
        {
            schema.Type = type.Value & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        // 「$ref + default」的组合（枚举参数的 C# 默认值会产出 default: 0）会让
        // openapi-generator 误判类型并生成未定义的 numberToJSON，故引用型 schema 不输出默认值。
        if (schema.Type is null && schema.Default is not null)
        {
            schema.Default = null;
        }

        return Task.CompletedTask;
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "缺少数据库连接串。请复制 src/YunCai/appsettings.Local.example.json 为 appsettings.Local.json 并填写，或设置环境变量 ConnectionStrings__DefaultConnection。");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<YunCai.Modules.Auditing.Infrastructure.AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(connectionString)
        .AddInterceptors(sp.GetRequiredService<YunCai.Modules.Auditing.Infrastructure.AuditSaveChangesInterceptor>()));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// 认证相关服务
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<YunCai.Modules.Identity.Domain.IUserRepository, YunCai.Modules.Identity.Infrastructure.UserRepository>();
builder.Services.AddScoped<YunCai.Modules.Identity.Application.UserAdminService>();

// 手机验证码登录：验证码存进程内存缓存；本地开发走模拟通道（验证码写日志并在响应里回显），
// 其他环境在没有接入真实短信通道前一律拒绝发送，避免误以为短信已发出。
builder.Services.AddMemoryCache();
// 高危操作二次确认令牌用 Data Protection 签名，短时有效、无需落库。
builder.Services.AddDataProtection();
builder.Services.AddScoped<YunCai.Modules.Identity.Application.StepUpService>();
builder.Services.AddScoped<YunCai.SharedKernel.Infrastructure.Security.IStepUpValidator>(
    sp => sp.GetRequiredService<YunCai.Modules.Identity.Application.StepUpService>());
builder.Services.Configure<YunCai.Modules.Identity.Application.SmsOptions>(
    builder.Configuration.GetSection("YunCai:Sms"));
builder.Services.AddSingleton<YunCai.Modules.Identity.Application.SmsCodeService>();
if (builder.Environment.IsDevelopment()
    && string.Equals(builder.Configuration["YunCai:Sms:Provider"], "Mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<YunCai.Modules.Identity.Application.ISmsSender,
        YunCai.Modules.Identity.Infrastructure.Sms.MockSmsSender>();
}
else
{
    builder.Services.AddSingleton<YunCai.Modules.Identity.Application.ISmsSender,
        YunCai.Modules.Identity.Infrastructure.Sms.UnconfiguredSmsSender>();
}

builder.Services.AddScoped<YunCai.Modules.AccountSets.Domain.IAccountSetRepository, YunCai.Modules.AccountSets.Infrastructure.AccountSetRepository>();
builder.Services.AddScoped<YunCai.Modules.Accounting.Domain.IAccountRepository, YunCai.Modules.Accounting.Infrastructure.AccountRepository>();
builder.Services.AddScoped<YunCai.Modules.Assist.Domain.IAssistItemRepository, YunCai.Modules.Assist.Infrastructure.AssistItemRepository>();
builder.Services.AddScoped<YunCai.Modules.Funds.Domain.IJournalRepository, YunCai.Modules.Funds.Infrastructure.JournalRepository>();
builder.Services.AddScoped<YunCai.Modules.Funds.Domain.IEntryCategoryRepository, YunCai.Modules.Funds.Infrastructure.EntryCategoryRepository>();
builder.Services.AddScoped<YunCai.Modules.Funds.Domain.ITransferRepository, YunCai.Modules.Funds.Infrastructure.TransferRepository>();
builder.Services.AddScoped<YunCai.Modules.Vouchers.Domain.IVoucherRepository, YunCai.Modules.Vouchers.Infrastructure.VoucherRepository>();
builder.Services.AddScoped<YunCai.Modules.Vouchers.Domain.IVoucherAttachmentRepository, YunCai.Modules.Vouchers.Infrastructure.VoucherAttachmentRepository>();
builder.Services.AddScoped<YunCai.Modules.Closing.Domain.IClosingTemplateRepository, YunCai.Modules.Closing.Infrastructure.ClosingTemplateRepository>();
builder.Services.AddScoped<YunCai.Modules.Vouchers.Application.IVoucherImportReader, YunCai.Modules.Vouchers.Infrastructure.XlsVoucherImportReader>();
builder.Services.AddScoped<YunCai.Modules.Vouchers.Application.VoucherImportService>();
builder.Services.AddScoped<YunCai.Modules.Vouchers.Application.VoucherService>();
builder.Services.AddScoped<YunCai.Modules.Vouchers.Application.VoucherAttachmentService>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Domain.ICurrencyRepository, YunCai.Modules.AccountSets.Infrastructure.CurrencyRepository>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Domain.IVoucherWordRepository, YunCai.Modules.AccountSets.Infrastructure.VoucherWordRepository>();
builder.Services.AddScoped<YunCai.Modules.Vouchers.Domain.IVoucherTemplateRepository, YunCai.Modules.Vouchers.Infrastructure.VoucherTemplateRepository>();
builder.Services.AddScoped<YunCai.Modules.Funds.Domain.IBillRepository, YunCai.Modules.Funds.Infrastructure.BillRepository>();
builder.Services.AddScoped<YunCai.Modules.FixedAssets.Domain.IFixedAssetRepository, YunCai.Modules.FixedAssets.Infrastructure.FixedAssetRepository>();
builder.Services.AddScoped<YunCai.Modules.Expenses.Domain.IExpenseRepository, YunCai.Modules.Expenses.Infrastructure.ExpenseRepository>();
builder.Services.AddScoped<YunCai.Modules.Invoices.Domain.IInvoiceRepository, YunCai.Modules.Invoices.Infrastructure.InvoiceRepository>();
// 税局「一键取票」落库：按「账套 + 方向 + 发票号」跳过重复，不覆盖已认证/已入账的发票。
builder.Services.AddScoped<YunCai.Modules.Invoices.Domain.IInvoiceTaxImporter, YunCai.Modules.Invoices.Infrastructure.InvoiceTaxImporter>();
builder.Services.AddScoped<YunCai.Modules.Salary.Domain.ISalaryRepository, YunCai.Modules.Salary.Infrastructure.SalaryRepository>();
builder.Services.AddScoped<YunCai.Modules.Archives.Domain.IArchiveRepository, YunCai.Modules.Archives.Infrastructure.ArchiveRepository>();
builder.Services.AddScoped<YunCai.Modules.Archives.Domain.IArchiveCategoryRepository, YunCai.Modules.Archives.Infrastructure.ArchiveCategoryRepository>();
builder.Services.AddScoped<YunCai.Modules.Auditing.Domain.IOperationLogRepository, YunCai.Modules.Auditing.Infrastructure.OperationLogRepository>();
builder.Services.AddScoped<YunCai.Modules.Budgets.Domain.IBudgetRepository, YunCai.Modules.Budgets.Infrastructure.BudgetRepository>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Domain.IReinitRepository, YunCai.Modules.AccountSets.Infrastructure.ReinitRepository>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Application.CurrencyService>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Application.VoucherWordService>();
builder.Services.AddScoped<YunCai.Modules.Vouchers.Application.VoucherTemplateService>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.BillService>();
builder.Services.AddScoped<YunCai.Modules.FixedAssets.Application.FixedAssetService>();
builder.Services.AddScoped<YunCai.Modules.Expenses.Application.ExpenseService>();
builder.Services.AddScoped<YunCai.Modules.Invoices.Application.InvoiceService>();
builder.Services.AddScoped<YunCai.Modules.Salary.Application.SalaryService>();
builder.Services.AddScoped<YunCai.Modules.Archives.Application.ArchiveService>();
builder.Services.AddScoped<YunCai.Modules.Archives.Application.ArchiveCategoryService>();
builder.Services.AddScoped<YunCai.Modules.Auditing.Application.OperationLogService>();
builder.Services.AddScoped<YunCai.Modules.Budgets.Application.BudgetService>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Application.ReinitService>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Application.IAccountSetMemberRepository, YunCai.Modules.AccountSets.Infrastructure.AccountSetMemberRepository>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Domain.IAccountSetRoleRepository, YunCai.Modules.AccountSets.Infrastructure.AccountSetRoleRepository>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Application.RoleService>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Application.MemberService>();
builder.Services.AddScoped<YunCai.Modules.AccountSets.Api.AccountSetAccessFilter>();
// 税局密码加解密只依赖配置，无状态，做成单例避免每次请求重派密钥。
builder.Services.AddSingleton<YunCai.Modules.AccountSets.Application.ITaxCredentialProtector, YunCai.Modules.AccountSets.Infrastructure.AesTaxCredentialProtector>();
// 一次性票据放在进程内存里：助手兑换通常在点击后的几秒内完成，重启后未兑换的票据自然作废。
builder.Services.AddSingleton<YunCai.Modules.AccountSets.Application.TaxLoginTicketStore>();
// 服务端税局通道：无头浏览器与登录会话常驻进程；登录态加密后存数据库，
// 服务重启、重建容器、换服务器都能免登录取数。
builder.Services.AddSingleton<YunCai.Modules.Tax.Infrastructure.TaxPortalSessionStore>();
// 任务中心台账：每次服务端税局操作落一行，写入走自己的作用域，做成单例。
builder.Services.AddSingleton<YunCai.Modules.Tax.Infrastructure.TaxTaskStore>();
builder.Services.AddSingleton<YunCai.Modules.Tax.Application.TaxPortalBrowserHandoffStore>();
builder.Services.AddSingleton<YunCai.Modules.Tax.Application.TaxPortalService>();
// 个税取数跑在请求之外：提交即返回任务标识，后台执行器负责跑完并回填任务中心。
builder.Services.AddSingleton<YunCai.Modules.Tax.Application.TaxIitFetchJobRunner>();
// 一键取票同样是后台任务：税局查询要翻页，跑完再统一落库。
builder.Services.AddSingleton<YunCai.Modules.Tax.Application.TaxInvoiceSyncJobRunner>();
// 个税报表填写按累计预扣法读工资模块数据，属于纯查询，不落库。
builder.Services.AddScoped<YunCai.Modules.Tax.Application.IitReportService>();
builder.Services.AddScoped<YunCai.SharedKernel.Infrastructure.Data.AccountSetBackupService>();
builder.Services.AddScoped<YunCai.Modules.Accounting.Application.AccountAutoCreateService>();
builder.Services.AddScoped<YunCai.Modules.Accounting.Application.IAccountImportReader, YunCai.Modules.Accounting.Infrastructure.XlsxAccountImportReader>();
builder.Services.AddScoped<YunCai.Modules.Accounting.Application.AccountImportService>();
builder.Services.AddScoped<YunCai.Modules.Accounting.Application.OpeningBalanceService>();
builder.Services.AddScoped<YunCai.Modules.Accounting.Application.OpeningBalanceImportService>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.JournalService>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.EntryCategoryService>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.IJournalImportReader, YunCai.Modules.Funds.Infrastructure.XlsJournalImportReader>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.JournalImportService>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.JournalAccountImportService>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.TransferService>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.TransferImportService>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.BillImportService>();
builder.Services.AddScoped<YunCai.Modules.Funds.Application.FundReportService>();
builder.Services.AddScoped<YunCai.Modules.Assist.Application.AssistItemService>();
builder.Services.AddScoped<YunCai.Modules.Accounting.Application.AccountAssistGuard>();
builder.Services.AddScoped<YunCai.Modules.Ledgers.Application.ILedgerSource, YunCai.Modules.Ledgers.Infrastructure.LedgerSource>();
builder.Services.AddScoped<YunCai.Modules.Ledgers.Application.LedgerDataSetFactory>();
builder.Services.AddScoped<YunCai.Modules.Ledgers.Application.SubsidiaryLedgerService>();
builder.Services.AddScoped<YunCai.Modules.Ledgers.Application.GeneralLedgerService>();
builder.Services.AddScoped<YunCai.Modules.Ledgers.Application.VoucherJournalService>();
builder.Services.AddScoped<YunCai.Modules.Ledgers.Application.VoucherSummaryService>();
builder.Services.AddScoped<YunCai.Modules.Ledgers.Application.AssistLedgerService>();
builder.Services.AddScoped<YunCai.Modules.Ledgers.Application.MultiColumnLedgerService>();
builder.Services.AddScoped<YunCai.Modules.Ledgers.Application.AgeingAnalysisService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.TrialBalanceService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.BalanceSheetService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.BalanceSheetXlsExportService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.BalanceSheetPdfExportService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.CashJournalService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.IncomeStatementService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.CashFlowService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.CashFlowSupplementService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.CashFlowFormulaService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Domain.CashFlow.ICashFlowFormulaRepository, YunCai.Modules.Reports.Infrastructure.CashFlowFormulaRepository>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.AssistIncomeService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.ExpenseDetailService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.EquityChangeService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.TaxStatisticsService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.FinancialSummaryService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.AuditExtractService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.AccountSetArchiveService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.CustomReportService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Application.ReportExportService>();
builder.Services.AddScoped<YunCai.Modules.Reports.Domain.ICustomReportRepository, YunCai.Modules.Reports.Infrastructure.CustomReportRepository>();
builder.Services.AddScoped<YunCai.Modules.Closing.Application.ClosingService>();
builder.Services.AddScoped<YunCai.Modules.Closing.Application.ClosingTemplateService>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
          ?? throw new InvalidOperationException("缺少 Jwt 配置节");

if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key 未配置或长度不足 32 位。请在 appsettings.Local.json 的 Jwt:Key 填入随机串（生产环境用环境变量 Jwt__Key），否则令牌可被伪造。");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            // 每次请求核对账号状态与令牌版本：停用账号、改密后旧令牌立即失效。
            OnTokenValidated = context =>
            {
                var repo = context.HttpContext.RequestServices
                    .GetRequiredService<YunCai.Modules.Identity.Domain.IUserRepository>();
                var userId = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                var user = string.IsNullOrEmpty(userId) ? null : repo.FindById(userId);
                if (user is null)
                {
                    context.Fail("账号不存在。");
                    return Task.CompletedTask;
                }

                if (!user.IsActive)
                {
                    context.Fail("账号已停用。");
                    return Task.CompletedTask;
                }

                var version = context.Principal?.FindFirst("ver")?.Value;
                if (!int.TryParse(version, out var tokenVersion) || tokenVersion != user.TokenVersion)
                {
                    context.Fail("登录状态已失效，请重新登录。");
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // 登录账号管理只对系统管理员开放；与账套内的「账套管理员」互不替代。
    options.AddPolicy("SystemAdmin", policy => policy.RequireClaim("sysadmin", "true"));
});

// 未被控制器捕获的异常统一走 Problem Details（见 GlobalExceptionHandler）。
builder.Services.AddProblemDetails();
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(180));

// 登录接口按来源 IP 限流，防止口令枚举。反向代理后要生效需开启 YunCai:Networking:TrustForwardedHeaders。
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
    // 验证码发送另设更严的窗口，配合业务层「同一手机号 60 秒一次」防止短信轰炸。
    options.AddPolicy("sms", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));
});

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

if (runtime.Networking.TrustForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        // 信任所有来源的 X-Forwarded-*：仅在后端端口不对外暴露、只有反向代理能直连时使用，
        // 否则客户端可自带头伪造来源 IP 绕过限流。
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
    });
}

// 允许前端开发服务器跨域访问
const string CorsPolicy = "SpaCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    // 数据库结构迁移与初始数据。失败直接抛出让进程退出，避免「库结构没跟上却照常对外服务」。
    // 生产环境由一次性任务执行：dotnet YunCai.dll --migrate-only
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var isDevelopment = app.Environment.IsDevelopment();

        if (runtime.Database.AutoMigrate || DatabaseBootstrap.IsMigrateOnly(args))
        {
            await DatabaseBootstrap.MigrateAsync(db, startupLogger);
        }

        await DatabaseBootstrap.SeedAsync(db, runtime, isDevelopment, startupLogger);

        // 存量账套没有成员记录（或创建者未绑定账号）时，访问校验会把整本账套挡在所有人之外；
        // 初始化开启的环境统一回填到初始管理员名下，生产环境由建账时写入的创建者成员负责。
        if (runtime.Seed.Enabled)
        {
            await YunCai.Modules.AccountSets.Infrastructure.AccountSetOwnerBootstrap.EnsureOwnersAsync(
                db,
                scope.ServiceProvider.GetRequiredService<YunCai.Modules.AccountSets.Application.MemberService>(),
                runtime.Seed.AdminUsername,
                startupLogger);
        }

        if (DatabaseBootstrap.IsMigrateOnly(args))
        {
            startupLogger.LogInformation("数据库迁移与初始化完成，进程退出。");
            return;
        }
    }

    // 个税取数是后台任务，重启后内存里的任务全没了，台账却可能还留着「进行中」；
    // 开机先把它收口成失败，任务中心才不会挂着一行永远不动的进行中。台账是旁路能力，失败不影响启动。
    try
    {
        await app.Services.GetRequiredService<YunCai.Modules.Tax.Application.TaxIitFetchJobRunner>()
            .AbandonInterruptedAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "[任务中心] 收口重启前未完成任务失败");
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    else
    {
        // 未捕获异常 → Problem Details；HSTS 与 HTTPS 跳转按配置开启。
        app.UseExceptionHandler();
        if (runtime.Networking.UseHsts)
        {
            app.UseHsts();
        }
    }

    if (runtime.Networking.TrustForwardedHeaders)
    {
        app.UseForwardedHeaders();
    }

    if (runtime.Networking.HttpsRedirection && !app.Environment.IsDevelopment())
    {
        // 开发环境保持 HTTP 直连，方便 openapi-generator 等工具读取 http 上的 OpenAPI 文档。
        app.UseHttpsRedirection();
    }

    app.UseSerilogRequestLogging();

    // 基础安全响应头。前端与接口同源部署时这些头才有意义，但加上无害。
    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "same-origin";
        await next();
    });

    app.UseCors(CorsPolicy);
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "YunCai 启动失败。");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
