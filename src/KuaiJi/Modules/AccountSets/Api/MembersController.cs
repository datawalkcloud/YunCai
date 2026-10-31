using KuaiJi.Modules.AccountSets.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.AccountSets.Api
{
    /// <summary>账套成员接口：多人协作的成员与角色管理。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/members")]
    [Authorize]
    public class MembersController : ControllerBase
    {
        private readonly MemberService _memberService;

        public MembersController(MemberService memberService)
        {
            _memberService = memberService;
        }

        /// <summary>成员列表；首次访问会补种账套创建者。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AccountSetMemberDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _memberService.GetAsync(accountSetId, cancellationToken));
            }
            catch (MemberException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新增成员。</summary>
        [HttpPost]
        public async Task<ActionResult<AccountSetMemberDto>> Create(Guid accountSetId, [FromBody] SaveAccountSetMemberRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var dto = await _memberService.CreateAsync(accountSetId, request, cancellationToken);
                return CreatedAtAction(nameof(GetAll), new { accountSetId }, dto);
            }
            catch (MemberException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改成员资料与角色。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AccountSetMemberDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveAccountSetMemberRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _memberService.UpdateAsync(accountSetId, id, request, cancellationToken));
            }
            catch (MemberException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>启用 / 停用成员。</summary>
        [HttpPut("{id:guid}/enabled")]
        public async Task<ActionResult<AccountSetMemberDto>> SetEnabled(Guid accountSetId, Guid id, [FromBody] ToggleMemberRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _memberService.SetEnabledAsync(accountSetId, id, request.Enabled, cancellationToken));
            }
            catch (MemberException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除成员。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                await _memberService.DeleteAsync(accountSetId, id, cancellationToken);
                return NoContent();
            }
            catch (MemberException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
