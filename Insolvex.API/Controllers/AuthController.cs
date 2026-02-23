using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.API.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs.Auth;
using Insolvex.Core.Exceptions;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthenticationService _authService;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public AuthController(AuthenticationService authService, ApplicationDbContext db, IAuditService audit)
    {
  _authService = authService;
      _db = db;
        _audit = audit;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
        try
 {
         var result = await _authService.LoginAsync(request);
  await _audit.LogAuthAsync("Auth.Login.Success", request.Email);
      return Ok(result);
        }
        catch (Exception)
  {
  await _audit.LogAuthAsync("Auth.Login.Failed", request.Email, severity: "Warning");
throw;
  }
    }

    [HttpGet("me")]
[Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
     return Unauthorized();

var user = await _authService.GetCurrentUserAsync(Guid.Parse(userId));
        return Ok(user);
}

    /// <summary>Change the current user's password.</summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
   if (userId == null)
   return Unauthorized();

        await _authService.ChangePasswordAsync(Guid.Parse(userId), request);
        await _audit.LogAuthAsync("Auth.PasswordChanged", userId: Guid.Parse(userId));
        return Ok(new { message = "Password changed successfully" });
    }

    /// <summary>Request a password reset token (sent via email).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
 var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == request.Email);
if (user == null)
  return Ok(new { message = "If that email exists, a reset link has been sent." }); // Security: don't reveal existence

        user.ResetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
  user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

await _audit.LogAuthAsync("Auth.PasswordResetRequested", user.Email, user.Id);

    // TODO: Send email with reset link
  // For now, just return the token (in production, send email)
    return Ok(new { message = "If that email exists, a reset link has been sent.", token = user.ResetToken });
    }

    /// <summary>Reset password using token.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
 public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
  var user = await _db.Users.IgnoreQueryFilters()
 .FirstOrDefaultAsync(u => u.ResetToken == request.Token && u.ResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
  return BadRequest(new { message = "Invalid or expired reset token" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.ResetToken = null;
 user.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();

        await _audit.LogAuthAsync("Auth.PasswordReset", user.Email, user.Id, severity: "Warning");

  return Ok(new { message = "Password reset successfully. You can now log in." });
    }
}

public record ForgotPasswordRequestDto(string Email);
public record ResetPasswordRequestDto(string Token, string NewPassword);
