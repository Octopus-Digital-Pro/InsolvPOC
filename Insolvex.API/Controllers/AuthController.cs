using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.Data.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs.Auth;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
  private readonly AuthenticationService _authService;
  private readonly IAuditService _audit;

  public AuthController(AuthenticationService authService, IAuditService audit)
  {
    _authService = authService;
    _audit = audit;
  }

  [HttpPost("login")]
  [AllowAnonymous]
  public async Task<IActionResult> Login([FromBody] LoginRequest request)
  {
    try
    {
      var result = await _authService.LoginAsync(request);
      await _audit.LogAuthAsync("User Login Succeeded", request.Email);
      return Ok(result);
    }
    catch (Exception)
    {
      await _audit.LogAuthAsync("User Login Failed", request.Email, severity: "Warning");
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
    await _audit.LogAuthAsync("User Changed Password", userId: Guid.Parse(userId));
    return Ok(new { message = "Password changed successfully" });
  }

  /// <summary>Request a password reset token (sent via email).</summary>
  [HttpPost("forgot-password")]
  [AllowAnonymous]
  public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
  {
    var result = await _authService.ForgotPasswordAsync(request.Email);
    return Ok(result);
  }

  /// <summary>Reset password using token.</summary>
  [HttpPost("reset-password")]
  [AllowAnonymous]
  public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
  {
    await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
    return Ok(new { message = "Password reset successfully. You can now log in." });
  }
}

public record ForgotPasswordRequestDto(string Email);
public record ResetPasswordRequestDto(string Token, string NewPassword);
