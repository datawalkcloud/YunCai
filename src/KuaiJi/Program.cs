using System.Text;
using KuaiJi.Infrastructure.Data;
using Microsoft.OpenApi;
using KuaiJi.Services;
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

builder.Services.AddControllers();
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

        return Task.CompletedTask;
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// 认证相关服务
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<KuaiJi.Domain.Users.IUserRepository, KuaiJi.Infrastructure.Persistence.UserRepository>();
builder.Services.AddScoped<KuaiJi.Domain.AccountSets.IAccountSetRepository, KuaiJi.Infrastructure.AccountSets.AccountSetRepository>();
builder.Services.AddScoped<KuaiJi.Domain.Accounting.IAccountRepository, KuaiJi.Infrastructure.Accounting.AccountRepository>();
builder.Services.AddScoped<KuaiJi.Domain.Journals.IJournalRepository, KuaiJi.Infrastructure.Journals.JournalRepository>();
builder.Services.AddScoped<KuaiJi.Domain.Journals.IEntryCategoryRepository, KuaiJi.Infrastructure.Journals.EntryCategoryRepository>();
builder.Services.AddScoped<KuaiJi.Domain.Vouchers.IVoucherRepository, KuaiJi.Infrastructure.Vouchers.VoucherRepository>();
builder.Services.AddScoped<KuaiJi.Application.Vouchers.IVoucherImportReader, KuaiJi.Infrastructure.Vouchers.XlsVoucherImportReader>();
builder.Services.AddScoped<KuaiJi.Application.Vouchers.VoucherImportService>();
builder.Services.AddScoped<KuaiJi.Application.Vouchers.VoucherService>();
builder.Services.AddScoped<KuaiJi.Application.Accounts.AccountAutoCreateService>();
builder.Services.AddScoped<KuaiJi.Application.Accounts.IAccountImportReader, KuaiJi.Infrastructure.Accounting.XlsxAccountImportReader>();
builder.Services.AddScoped<KuaiJi.Application.Accounts.AccountImportService>();
builder.Services.AddScoped<KuaiJi.Application.Accounts.OpeningBalanceService>();
builder.Services.AddScoped<KuaiJi.Application.Journals.JournalService>();
builder.Services.AddScoped<KuaiJi.Application.Journals.EntryCategoryService>();
builder.Services.AddScoped<KuaiJi.Application.Journals.IJournalImportReader, KuaiJi.Infrastructure.Journals.XlsJournalImportReader>();
builder.Services.AddScoped<KuaiJi.Application.Journals.JournalImportService>();
builder.Services.AddScoped<KuaiJi.Application.Reports.TrialBalanceService>();
builder.Services.AddScoped<KuaiJi.Application.Reports.BalanceSheetService>();
builder.Services.AddScoped<KuaiJi.Application.Reports.CashJournalService>();
builder.Services.AddScoped<KuaiJi.Application.Reports.IncomeStatementService>();
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
                new KuaiJi.Domain.Users.User
                {
                    Id = "1",
                    Username = "admin",
                    DisplayName = "管理员",
                    PasswordHash = KuaiJi.Services.PasswordHasher.Hash("admin123")
                },
                new KuaiJi.Domain.Users.User
                {
                    Id = "2",
                    Username = "user",
                    DisplayName = "普通用户",
                    PasswordHash = KuaiJi.Services.PasswordHasher.Hash("user123")
                }
            );
            context.SaveChanges();
        }

        // 创建默认账套（科目/日记账/凭证均需归属于某一账套），并置为当前账套。
        if (!context.AccountSets.Any())
        {
            var defaultSet = KuaiJi.Domain.AccountSets.AccountSet.Create(
                "默认账套",
                "默认单位",
                KuaiJi.Domain.AccountSets.AccountingStandard.Enterprise,
                KuaiJi.Domain.AccountSets.TaxpayerType.General,
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
