using System.Text;
using KuaiJi.Infrastructure.Data;
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
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// 认证相关服务
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<KuaiJi.Domain.Users.IUserRepository, KuaiJi.Infrastructure.Persistence.UserRepository>();
builder.Services.AddScoped<KuaiJi.Domain.Accounting.IAccountRepository, KuaiJi.Infrastructure.Accounting.AccountRepository>();
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
