using System.Text;
using KuaiJi.SharedKernel.Infrastructure.Binding;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.OpenApi;
using KuaiJi.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
builder.Services.AddScoped<KuaiJi.Modules.Closing.Domain.IClosingTemplateRepository, KuaiJi.Modules.Closing.Infrastructure.ClosingTemplateRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Application.IVoucherImportReader, KuaiJi.Modules.Vouchers.Infrastructure.XlsVoucherImportReader>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Application.VoucherImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Application.VoucherService>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Domain.ICurrencyRepository, KuaiJi.Modules.AccountSets.Infrastructure.CurrencyRepository>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Domain.IVoucherWordRepository, KuaiJi.Modules.AccountSets.Infrastructure.VoucherWordRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Vouchers.Domain.IVoucherTemplateRepository, KuaiJi.Modules.Vouchers.Infrastructure.VoucherTemplateRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Domain.IBillRepository, KuaiJi.Modules.Funds.Infrastructure.BillRepository>();
builder.Services.AddScoped<KuaiJi.Modules.FixedAssets.Domain.IFixedAssetRepository, KuaiJi.Modules.FixedAssets.Infrastructure.FixedAssetRepository>();
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
builder.Services.AddScoped<KuaiJi.Modules.Invoices.Application.InvoiceService>();
builder.Services.AddScoped<KuaiJi.Modules.Salary.Application.SalaryService>();
builder.Services.AddScoped<KuaiJi.Modules.Archives.Application.ArchiveService>();
builder.Services.AddScoped<KuaiJi.Modules.Auditing.Application.OperationLogService>();
builder.Services.AddScoped<KuaiJi.Modules.AccountSets.Application.ReinitService>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.AccountAutoCreateService>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.IAccountImportReader, KuaiJi.Modules.Accounting.Infrastructure.XlsxAccountImportReader>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.AccountImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Accounting.Application.OpeningBalanceService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.JournalService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.EntryCategoryService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.IJournalImportReader, KuaiJi.Modules.Funds.Infrastructure.XlsJournalImportReader>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.JournalImportService>();
builder.Services.AddScoped<KuaiJi.Modules.Funds.Application.TransferService>();
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
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.AssistIncomeService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.ExpenseDetailService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.EquityChangeService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.TaxStatisticsService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.FinancialSummaryService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.CustomReportService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Application.ReportExportService>();
builder.Services.AddScoped<KuaiJi.Modules.Reports.Domain.ICustomReportRepository, KuaiJi.Modules.Reports.Infrastructure.CustomReportRepository>();
builder.Services.AddScoped<KuaiJi.Modules.Closing.Application.ClosingService>();
builder.Services.AddScoped<KuaiJi.Modules.Closing.Application.ClosingTemplateService>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
          ?? throw new InvalidOperationException("缺少 Jwt 配置节");

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

// 应用启动时自动执行 EF Core 数据库迁移
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        if (context.Database.IsRelational())
        {
            context.Database.Migrate();
        }

        // 创建默认用户
        if (!context.Users.Any())
        {
            context.Users.AddRange(
                new KuaiJi.Modules.Identity.Domain.User
                {
                    Id = "1",
                    Username = "admin",
                    DisplayName = "管理员",
                    PasswordHash = KuaiJi.Modules.Identity.Infrastructure.PasswordHasher.Hash("admin123")
                },
                new KuaiJi.Modules.Identity.Domain.User
                {
                    Id = "2",
                    Username = "user",
                    DisplayName = "普通用户",
                    PasswordHash = KuaiJi.Modules.Identity.Infrastructure.PasswordHasher.Hash("user123")
                }
            );
            context.SaveChanges();
        }

        // 创建默认账套（科目/日记账/凭证均需归属于某一账套），并置为当前账套。
        if (!context.AccountSets.Any())
        {
            var defaultSet = KuaiJi.Modules.AccountSets.Domain.AccountSet.Create(
                "默认账套",
                "默认单位",
                KuaiJi.Modules.AccountSets.Domain.AccountingStandard.Enterprise,
                KuaiJi.Modules.AccountSets.Domain.TaxpayerType.General,
                "人民币",
                DateTime.UtcNow.Year,
                1);
            defaultSet.MarkAsCurrent();
            context.AccountSets.Add(defaultSet);
            context.SaveChanges();
        }
    }                           
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "数据库自动迁移或初始化数据过程中发生了错误。");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // 生产环境才强制 HTTPS 跳转；开发环境保持 HTTP 直连，
    // 以便 openapi-generator 等工具能直接读取 http 上的 OpenAPI 文档。
    app.UseHttpsRedirection();
}

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
