namespace Insolvio.Core.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, UserDto User);

public record RegisterRequest(string Email, string Password, string FirstName, string LastName);

public record PasswordResetRequest(string Email);

public record PasswordResetConfirm(string Token, string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
