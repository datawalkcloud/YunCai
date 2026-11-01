using System.Text;
using System.Threading.RateLimiting;
using KuaiJi.SharedKernel.Infrastructure.Api;
using KuaiJi.SharedKernel.Infrastructure.Binding;
using KuaiJi.SharedKernel.Infrastructure.Data;
using KuaiJi.SharedKernel.Infrastructure.Runtime;
using Microsoft.OpenApi;
using KuaiJi.Modules.Identity.Infrastructure;
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

// 容器与系统服务里工作目录不可靠，默认只输出到控制台；需要落盘时配置 KuaiJi:Logging:File 为绝对路径。
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
        "缺少数据库连接串。请复制 src/KuaiJi/appsettings.Local.example.json 为 appsettings.Local.json 并填写，或设置环境变量 ConnectionStrings__DefaultConnection。");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// 认证相关服务
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<KuaiJi.Modules.Identity.Domain.IUserRepository, KuaiJi.Modules.Identity.Infrastructure.UserRepository>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Domain.IAccountSetRepository, KuaiJi.Modules.AccountSets.Infrastructure.AccountSetRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Domain.IAccountRepository, KuaiJi.Modules.Accounting.Infrastructure.AccountRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Assist.Domain.IAssistItemRepository, KuaiJi.Modules.Assist.Infrastructure.AssistItemRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Domain.IJournalRepository, KuaiJi.Modules.Funds.Infrastructure.JournalRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Domain.IEntryCategoryRepository, KuaiJi.Modules.Funds.Infrastructure.EntryCategoryRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Domain.ITransferRepository, KuaiJi.Modules.Funds.Infrastructure.TransferRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Domain.IVoucherRepository, KuaiJi.Modules.Vouchers.Infrastructure.VoucherRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Domain.IVoucherAttachmentRepository, KuaiJi.Modules.Vouchers.Infrastructure.VoucherAttachmentRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Closing.Domain.IClosingTemplateRepository, KuaiJi.Modules.Closing.Infrastructure.ClosingTemplateRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Application.IVoucherImportReader, KuaiJi.Modules.Vouchers.Infrastructure.XlsVoucherImportReader>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Application.VoucherImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Application.VoucherService>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Application.VoucherAttachmentService>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Domain.ICurrencyRepository, KuaiJi.Modules.AccountSets.Infrastructure.CurrencyRepository>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Domain.IVoucherWordRepository, KuaiJi.Modules.AccountSets.Infrastructure.VoucherWordRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Domain.IVoucherTemplateRepository, KuaiJi.Modules.Vouchers.Infrastructure.VoucherTemplateRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Domain.IBillRepository, KuaiJi.Modules.Funds.Infrastructure.BillRepository>();
builder.Services.AddScoped<KuaiJi.Modules.FixedAssets.Domain.IFixedAssetRepository, KuaiJi.Modules.FixedAssets.Infrastructure.FixedAssetRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Expenses.Domain.IExpenseRepository, KuaiJi.Modules.Expenses.Infrastructure.ExpenseRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Invoices.Domain.IInvoiceRepository, KuaiJi.Modules.Invoices.Infrastructure.InvoiceRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Salary.Domain.ISalaryRepository, KuaiJi.Modules.Salary.Infrastructure.SalaryRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Archives.Domain.IArchiveRepository, KuaiJi.Modules.Archives.Infrastructure.ArchiveRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Auditing.Domain.IOperationLogRepository, KuaiJi.Modules.Auditing.Infrastructure.OperationLogRepository>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Domain.IReinitRepository, KuaiJi.Modules.AccountSets.Infrastructure.ReinitRepository>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Application.CurrencyService>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Application.VoucherWordService>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Application.VoucherTemplateService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.BillService>();
builder.Services.AddScoped<KuaiJi.Modules.FixedAssets.Application.FixedAssetService>();
builder.Services.AddScoped<KuaiJi.Modules.Expenses.Application.ExpenseService>();
builder.Services.AddScoped<KuaiJi.Modules.Invoices.Application.InvoiceService>();
builder.Services.AddScoped<KuaiJi.Modules.Salary.Application.SalaryService>();
builder.Services.AddScoped<KuaiJi.Modules.Archives.Application.ArchiveService>();
builder.Services.AddScoped<KuaiJi.Modules.Auditing.Application.OperationLogService>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Application.ReinitService>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Application.IAccountSetMemberRepository, KuaiJi.Modules.AccountSets.Infrastructure.AccountSetMemberRepository>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Application.MemberService>();
// 税局密码加解密只依赖配置，无状态，做成单例避免每次请求重派密钥。
builder.Services.AddSingleton<KuaiJi.Modules.AccountSets.Application.ITaxCredentialProtector, KuaiJi.Modules.AccountSets.Infrastructure.AesTaxCredentialProtector>();
// 一次性票据放在进程内存里：助手兑换通常在点击后的几秒内完成，重启后未兑换的票据自然作废。
builder.Services.AddSingleton<KuaiJi.Modules.AccountSets.Application.TaxLoginTicketStore>();
// 服务端税局通道：无头浏览器与登录会话常驻进程；登录态加密后存数据库，
// 服务重启、重建容器、换服务器都能免登录取数。
builder.Services.AddSingleton<KuaiJi.Modules.Tax.Infrastructure.TaxPortalSessionStore>();
builder.Services.AddSingleton<KuaiJi.Modules.Tax.Application.TaxPortalBrowserHandoffStore>();
builder.Services.AddSingleton<KuaiJi.Modules.Tax.Application.TaxPortalService>();
builder.Services.AddScoped<KuaiJi.SharedKernel.Infrastructure.Data.AccountSetBackupService>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.AccountAutoCreateService>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.IAccountImportReader, KuaiJi.Modules.Accounting.Infrastructure.XlsxAccountImportReader>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.AccountImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.OpeningBalanceService>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.OpeningBalanceImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.JournalService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.EntryCategoryService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.IJournalImportReader, KuaiJi.Modules.Funds.Infrastructure.XlsJournalImportReader>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.JournalImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.JournalAccountImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.TransferService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.TransferImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.BillImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.FundReportService>();
builder.Services.AddScoped<KuaiJi.Modules.Assist.Application.AssistItemService>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.AccountAssistGuard>();
builder.Services.AddScoped<KuaiJi.Modules.Ledgers.Application.ILedgerSource, KuaiJi.Modules.Ledgers.Infrastructure.LedgerSource>();
builder.Services.AddScoped<KuaiJi.Modules.Ledgers.Application.LedgerDataSetFactory>();
builder.Services.AddScoped<KuaiJi.Modules.Ledgers.Application.SubsidiaryLedgerService>();
builder.Services.AddScoped<KuaiJi.Modules.Ledgers.Application.GeneralLedgerService>();
builder.Services.AddScoped<KuaiJi.Modules.Ledgers.Application.VoucherJournalService>();
builder.Services.AddScoped<KuaiJi.Modules.Ledgers.Application.VoucherSummaryService>();
builder.Services.AddScoped<KuaiJi.Modules.Ledgers.Application.AssistLedgerService>();
builder.Services.AddScoped<KuaiJi.Modules.Ledgers.Application.MultiColumnLedgerService>();
builder.Services.AddScoped<KuaiJi.Modules.Ledgers.Application.AgeingAnalysisService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.TrialBalanceService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.BalanceSheetService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.CashJournalService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.IncomeStatementService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.CashFlowService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.CashFlowSupplementService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.AssistIncomeService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.ExpenseDetailService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.EquityChangeService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.TaxStatisticsService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.FinancialSummaryService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.AuditExtractService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.AccountSetArchiveService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.CustomReportService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.ReportExportService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Domain.ICustomReportRepository, KuaiJi.Modules.Reports.Infrastructure.CustomReportRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Closing.Application.ClosingService>();
builder.Services.AddScoped<KuaiJi.Modules.Closing.Application.ClosingTemplateService>();
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
    });

builder.Services.AddAuthorization();

// 未被控制器捕获的异常统一走 Problem Details（见 GlobalExceptionHandler）。
builder.Services.AddProblemDetails();
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(180));

// 登录接口按来源 IP 限流，防止口令枚举。反向代理后要生效需开启 KuaiJi:Networking:TrustForwardedHeaders。
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
    // 生产环境由一次性任务执行：dotnet KuaiJi.dll --migrate-only
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var isDevelopment = app.Environment.IsDevelopment();

        if (runtime.Database.AutoMigrate || DatabaseBootstrap.IsMigrateOnly(args))
        {
            await DatabaseBootstrap.MigrateAsync(db, startupLogger);
        }

        await DatabaseBootstrap.SeedAsync(db, runtime, isDevelopment, startupLogger);

        if (DatabaseBootstrap.IsMigrateOnly(args))
        {
            startupLogger.LogInformation("数据库迁移与初始化完成，进程退出。");
            return;
        }
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
    startupLogger.LogCritical(ex, "KuaiJi 启动失败。");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
