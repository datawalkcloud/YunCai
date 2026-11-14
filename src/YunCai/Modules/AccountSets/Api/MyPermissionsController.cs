using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using YunCai.Modules.AccountSets.Application;
using YunCai.Modules.AccountSets.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.AccountSets.Api
{
    /// <summary>当前登录用户在指定账套内的角色与权限，供前端控制菜单与操作入口。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/my-permissions")]
    [Authorize]
    public class MyPermissionsController : ControllerBase
    {
        private readonly IAccountSetMemberRepository _members;

        public MyPermissionsController(IAccountSetMemberRepository members)
        {
            _members = members;
        }

        private string? CurrentUserId =>
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        [HttpGet]
        public async Task<ActionResult<MyPermissionsDto>> Get(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (CurrentUserId is not { } userId)
            {
                return Unauthorized();
            }

            var access = await _members.GetAccessAsync(accountSetId, userId, User.FindFirstValue("phone"), cancellationToken);
            if (access is null)
            {
                return Forbid();
            }

            return Ok(new MyPermissionsDto
            {
                RoleId = access.RoleId,
                RoleName = access.RoleName,
                IsAdmin = access.IsAdmin,
                Permissions = access.Permissions,
                PermissionText = RolePermissionCatalog.BuildSummary(access.Permissions),
            });
        }
    }

    public sealed class MyPermissionsDto
    {
        public Guid RoleId { get; init; }

        public string RoleName { get; init; } = string.Empty;

        public bool IsAdmin { get; init; }

        public IReadOnlyList<string> Permissions { get; init; } = [];

        public string PermissionText { get; init; } = string.Empty;
    }
}
