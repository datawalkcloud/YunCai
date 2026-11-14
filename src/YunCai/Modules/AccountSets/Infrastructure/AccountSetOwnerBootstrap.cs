using YunCai.Modules.AccountSets.Application;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.AccountSets.Infrastructure
{
    /// <summary>
    /// 存量账套归属回填：本次引入账套访问校验之前建的账套没有成员，或创建者成员未绑定登录账号，
    /// 会把整本账套挡在所有人之外。启动时把这些账套挂到初始管理员名下；已绑定其他账号的创建者不动。
    /// 只随数据初始化开关执行（开发 / 测试环境），生产环境由建账时写入的创建者成员负责。
    /// </summary>
    public static class AccountSetOwnerBootstrap
    {
        public static async Task EnsureOwnersAsync(
            AppDbContext context,
            MemberService memberService,
            string? adminUsername,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            var username = string.IsNullOrWhiteSpace(adminUsername) ? "admin" : adminUsername;
            var admin = await context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Username == username, cancellationToken);
            if (admin is null)
            {
                return;
            }

            var sets = await context.AccountSets
                .AsNoTracking()
                .Select(a => new { a.Id, a.Name, a.CompanyName })
                .ToListAsync(cancellationToken);
            if (sets.Count == 0)
            {
                return;
            }

            foreach (var set in sets)
            {
                var ownerName = string.IsNullOrWhiteSpace(set.CompanyName) ? set.Name : set.CompanyName;
                await memberService.EnsureOwnerAsync(set.Id, ownerName, admin.Id, admin.Phone, cancellationToken);
            }

            logger.LogInformation("已确认 {Count} 个账套的创建者成员归属（{Username}）。", sets.Count, username);
        }
    }
}
