using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.DTOs.Auth;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;

namespace Insolvex.API.Services;

public class AuthenticationService
{
    private readonly ApplicationDbContext _db;
 private readonly IConfiguration _config;
    private readonly IAuditService _audit;

    public AuthenticationService(ApplicationDbContext db, IConfiguration config, IAuditService audit)
    {
      _db = db;
        _config = config;
        _audit = audit;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
         .Include(u => u.Tenant)
    .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
         throw new BusinessException("Invalid email or password");

      if (!user.IsActive)
            throw new BusinessException("Account is inactive");

if (user.Tenant != null && !user.Tenant.IsActive)
    throw new BusinessException("Organization is inactive");

        user.LastLoginDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = GenerateJwtToken(user);

        await _audit.LogAsync("User.Login", user.Id);

        return new LoginResponse(token, user.ToDto());
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _db.Users
   .IgnoreQueryFilters()
 .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new BusinessException("User not found");

        return user.ToDto();
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _db.Users
     .IgnoreQueryFilters()
    .FirstOrDefaultAsync(u => u.Id == userId)
         ?? throw new BusinessException("User not found");

  if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
   throw new BusinessException("Current password is incorrect");

      user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
   await _db.SaveChangesAsync();

  await _audit.LogAsync("User.PasswordChanged", user.Id);
  }

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
     {
      new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim("TenantId", user.TenantId.ToString()),
      new Claim(ClaimTypes.GivenName, user.FirstName),
    new Claim(ClaimTypes.Surname, user.LastName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryDays = int.Parse(_config["Jwt:ExpiryDays"] ?? "7");

      var token = new JwtSecurityToken(
 issuer: _config["Jwt:Issuer"],
       audience: _config["Jwt:Audience"],
   claims: claims,
   expires: DateTime.UtcNow.AddDays(expiryDays),
signingCredentials: creds
  );

      return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
