using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.UserView)]
public class UsersController : ControllerBase
{
  private readonly IUserService _users;
  private readonly ICurrentUserService _currentUser;

  public UsersController(IUserService users, ICurrentUserService currentUser)
  {
    _users = users;
    _currentUser = currentUser;
  }

  [HttpGet]
  public async Task<IActionResult> GetAll(CancellationToken ct)
      => Ok(await _users.GetAllAsync(ct));

  [HttpGet("{id:guid}")]
  public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
  {
    var user = await _users.GetByIdAsync(id, ct);
    if (user is null) return NotFound();
    return Ok(user);
  }

  [HttpPut("{id:guid}")]
  [RequirePermission(Permission.UserEdit)]
  public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
  {
    var user = await _users.UpdateAsync(id, request, ct);
    if (user is null) return NotFound();
    return Ok(user);
  }

  [HttpDelete("{id:guid}")]
  [RequirePermission(Permission.UserDeactivate)]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
  {
    await _users.DeactivateAsync(id, ct);
    return NoContent();
  }

  [HttpPost("invite")]
  [RequirePermission(Permission.UserInvite)]
  public async Task<IActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken ct)
      => Ok(await _users.InviteAsync(request, ct));

  [HttpGet("invitations")]
  public async Task<IActionResult> GetInvitations(CancellationToken ct)
      => Ok(await _users.GetInvitationsAsync(ct));

  [HttpDelete("invitations/{id:guid}")]
  [RequirePermission(Permission.UserInvite)]
  public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken ct)
  {
    await _users.RevokeInvitationAsync(id, ct);
    return NoContent();
  }

  [HttpPost("accept-invitation")]
  [AllowAnonymous]
  public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationRequest request, CancellationToken ct)
      => Ok(await _users.AcceptInvitationAsync(request, ct));

  [HttpGet("roles")]
  public IActionResult GetRoles()
  {
    var roles = Enum.GetValues<UserRole>()
        .Where(r => _currentUser.IsGlobalAdmin || r != UserRole.GlobalAdmin)
        .Select(r => new { value = r.ToString(), label = r.ToString() });
    return Ok(roles);
  }

  [HttpGet("my-permissions")]
  public IActionResult GetMyPermissions()
  {
    var perms = RolePermissions.GetPermissions(_currentUser.Role);
    return Ok(new
    {
      role = _currentUser.Role.ToString(),
      permissions = perms.Select(p => p.ToString()).OrderBy(p => p).ToList(),
      permissionCount = perms.Count,
    });
  }

  [HttpPost("{id:guid}/reset-password")]
  [RequirePermission(Permission.UserEdit)]
  public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] AdminResetPasswordRequest request, CancellationToken ct)
  {
    await _users.AdminResetPasswordAsync(id, request, ct);
    return Ok(new { message = "Password reset successfully" });
  }
}
