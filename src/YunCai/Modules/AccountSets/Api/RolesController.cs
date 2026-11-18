using YunCai.Modules.AccountSets.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.AccountSets.Api
{
    /// <summary>账套角色接口：权限设置页的角色与权限矩阵。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/roles")]
    [Authorize]
    [RequireAccountSetAdmin]
    public class RolesController : ControllerBase
    {
        private readonly RoleService _roleService;

        public RolesController(RoleService roleService)
        {
            _roleService = roleService;
        }

        /// <summary>角色列表；首次访问会播种预置角色。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AccountSetRoleDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _roleService.GetListAsync(accountSetId, cancellationToken));
            }
            catch (RoleException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>权限目录（模块 × 操作）。</summary>
        [HttpGet("catalog")]
        public ActionResult<IReadOnlyList<PermissionModuleDto>> GetCatalog()
        {
            return Ok(RoleService.GetCatalog());
        }

        /// <summary>新增自建角色。</summary>
        [HttpPost]
        public async Task<ActionResult<AccountSetRoleDto>> Create(Guid accountSetId, [FromBody] SaveAccountSetRoleRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var dto = await _roleService.CreateAsync(accountSetId, request, cancellationToken);
                return CreatedAtAction(nameof(GetAll), new { accountSetId }, dto);
            }
            catch (RoleException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改角色权限（预置角色只改权限，自建角色可改名）。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AccountSetRoleDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveAccountSetRoleRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _roleService.UpdateAsync(accountSetId, id, request, cancellationToken));
            }
            catch (RoleException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除自建角色。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                await _roleService.DeleteAsync(accountSetId, id, cancellationToken);
                return NoContent();
            }
            catch (RoleException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
