using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using System.Security.Cryptography;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.UserView)]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public UsersController(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => u.ToDto())
         .ToListAsync();
        return Ok(users);
 }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
  if (user == null) return NotFound();
  return Ok(user.ToDto());
  }

    [HttpPut("{id:guid}")]
  [RequirePermission(Permission.UserEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
   var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

  var oldValues = new { user.FirstName, user.LastName, role = user.Role.ToString(), user.IsActive };

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
  if (request.Role.HasValue) user.Role = request.Role.Value;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

  await _db.SaveChangesAsync();
   await _audit.LogEntityAsync("User.Updated", "User", user.Id, oldValues,
            new { user.FirstName, user.LastName, role = user.Role.ToString(), user.IsActive });
      return Ok(user.ToDto());
    }

 [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.UserDeactivate)]
    public async Task<IActionResult> Delete(Guid id)
    {
    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

     user.IsActive = false;
     await _db.SaveChangesAsync();
        await _audit.LogEntityAsync("User.Deactivated", "User", user.Id,
    oldValues: new { user.Email, user.IsActive }, severity: "Warning");
   return NoContent();
    }

    /// <summary>Invite a new user to the tenant.</summary>
    [HttpPost("invite")]
    [RequirePermission(Permission.UserInvite)]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest request)
 {
      if (!_currentUser.TenantId.HasValue)
            return BadRequest(new { message = "No tenant context" });

      // Check if user already exists
        var existing = await _db.Users.AnyAsync(u => u.Email == request.Email);
   if (existing)
            return BadRequest(new { message = "A user with this email already exists" });

        // Check for pending invitation
        var pendingInvite = await _db.UserInvitations
         .AnyAsync(i => i.Email == request.Email && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow);
   if (pendingInvite)
  return BadRequest(new { message = "A pending invitation already exists for this email" });

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
return BadRequest(new { message = $"Invalid role: {request.Role}" });

// Don't allow inviting GlobalAdmin
  if (role == UserRole.GlobalAdmin && !_currentUser.IsGlobalAdmin)
return Forbid();

   var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var invitation = new UserInvitation
 {
            Id = Guid.NewGuid(),
    TenantId = _currentUser.TenantId.Value,
       Email = request.Email,
     FirstName = request.FirstName,
          LastName = request.LastName,
          Role = role,
    Token = token,
 ExpiresAt = DateTime.UtcNow.AddDays(7),
         InvitedByUserId = _currentUser.UserId,
            CreatedOn = DateTime.UtcNow,
         CreatedBy = _currentUser.Email ?? "System",
        };

        _db.UserInvitations.Add(invitation);
     await _db.SaveChangesAsync();

   await _audit.LogEntityAsync("User.Invited", "UserInvitation", invitation.Id,
            newValues: new { request.Email, request.FirstName, request.LastName, request.Role });

        return Ok(new
        {
  id = invitation.Id,
      email = invitation.Email,
          role = invitation.Role.ToString(),
            token = invitation.Token,
         expiresAt = invitation.ExpiresAt,
    message = "Invitation created. Share the token with the user.",
        });
    }

    /// <summary>Get all invitations for the current tenant.</summary>
  [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations()
    {
        var invitations = await _db.UserInvitations
          .OrderByDescending(i => i.CreatedOn)
            .Select(i => new
  {
    i.Id,
 i.Email,
     i.FirstName,
                i.LastName,
       role = i.Role.ToString(),
      i.IsAccepted,
      i.AcceptedAt,
          i.ExpiresAt,
                isExpired = i.ExpiresAt < DateTime.UtcNow,
 i.CreatedOn,
    })
      .ToListAsync();
        return Ok(invitations);
    }

    /// <summary>Accept an invitation (creates the user account).</summary>
    [HttpPost("accept-invitation")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationRequest request)
    {
        var invitation = await _db.UserInvitations
        .FirstOrDefaultAsync(i => i.Token == request.Token && !i.IsAccepted);
        if (invitation == null)
   return BadRequest(new { message = "Invalid or already used invitation token" });
        if (invitation.ExpiresAt < DateTime.UtcNow)
   return BadRequest(new { message = "Invitation has expired" });

        // Check if user already exists
        var existingUser = await _db.Users.AnyAsync(u => u.Email == invitation.Email);
        if (existingUser)
     return BadRequest(new { message = "A user with this email already exists" });

     var user = new User
        {
            Id = Guid.NewGuid(),
   TenantId = invitation.TenantId,
         Email = invitation.Email,
            FirstName = invitation.FirstName,
            LastName = invitation.LastName,
        Role = invitation.Role,
       PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
      IsActive = true,
    CreatedOn = DateTime.UtcNow,
     CreatedBy = "Invitation",
        };

      _db.Users.Add(user);
        invitation.IsAccepted = true;
        invitation.AcceptedAt = DateTime.UtcNow;
await _db.SaveChangesAsync();

        await _audit.LogAuthAsync("User.InvitationAccepted", invitation.Email, user.Id);

        return Ok(new { message = "Account created successfully. You can now log in.", userId = user.Id });
    }

    /// <summary>Get available roles for the dropdown.</summary>
    [HttpGet("roles")]
    public IActionResult GetRoles()
    {
        var roles = Enum.GetValues<UserRole>()
      .Where(r => !_currentUser.IsGlobalAdmin ? r != UserRole.GlobalAdmin : true)
 .Select(r => new { value = r.ToString(), label = r.ToString() });
        return Ok(roles);
    }

    /// <summary>Get permissions for the current user's role.</summary>
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

    /// <summary>Admin: force-reset a user's password.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission(Permission.UserEdit)]
    public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] AdminResetPasswordRequest request)
    {
      var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

      // GlobalAdmin can reset anyone; TenantAdmin cannot reset GlobalAdmins
        if (user.Role == UserRole.GlobalAdmin && !_currentUser.IsGlobalAdmin)
       return Forbid();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.ResetToken = null;
      user.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();

        await _audit.LogEntityAsync("User.PasswordResetByAdmin", "User", user.Id,
  newValues: new { user.Email, resetBy = _currentUser.Email }, severity: "Warning");

      return Ok(new { message = "Password reset successfully" });
    }
}

public record UpdateUserRequest(string? FirstName, string? LastName, UserRole? Role, bool? IsActive);
public record InviteUserRequest(string Email, string FirstName, string LastName, string Role);
public record AcceptInvitationRequest(string Token, string Password);
public record AdminResetPasswordRequest(string NewPassword);
