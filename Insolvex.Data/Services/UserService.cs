using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Data.Services;

public sealed class UserService : IUserService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly string _frontendUrl;

    public UserService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit, IConfiguration config)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _frontendUrl = config["FrontendUrl"] ?? "http://localhost:5173";
    }

    public async Task<List<UserDto>> GetAllAsync(CancellationToken ct = default)
        => await _db.Users.OrderBy(u => u.FirstName).Select(u => u.ToDto()).ToListAsync(ct);

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User", id);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User", id);

        var oldValues = new { user.FirstName, user.LastName, user.Role, user.IsActive };

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.Role.HasValue) user.Role = request.Role.Value;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("User Profile Updated", "User", user.Id,
            oldValues, new { user.FirstName, user.LastName, user.Role, user.IsActive });

        return user.ToDto();
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User", id);

        user.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("User Account Deactivated", "User", user.Id,
            oldValues: new { user.Email }, severity: "Warning");
    }

    public async Task<UserInvitationDto> InviteAsync(InviteUserRequest request, CancellationToken ct = default)
    {
        var existing = await _db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == request.Email, ct);
        if (existing)
            throw new BusinessException("A user with that email already exists.");

        var pending = await _db.UserInvitations
            .AnyAsync(i => i.Email == request.Email && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow, ct);
        if (pending)
            throw new BusinessException("A pending invitation already exists for that email.");

        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId ?? throw new BusinessException("No tenant context"),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = Enum.Parse<UserRole>(request.Role, true),
            Token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            InvitedByUserId = _currentUser.UserId,
        };

        var inviteLink = $"{_frontendUrl}/accept-invitation?token={Uri.EscapeDataString(invitation.Token)}";
        _db.UserInvitations.Add(invitation);
        _db.ScheduledEmails.Add(new ScheduledEmail
        {
            TenantId = invitation.TenantId,
            To = invitation.Email,
            Subject = "Ați fost invitat(ă) să utilizați Insolvex",
            Body = $@"<html><body style=""font-family:sans-serif;background:#f4f6fb;padding:32px 0"">
<div style=""max-width:500px;margin:0 auto;background:#fff;border-radius:10px;padding:36px 40px;border:1px solid #e2e8f0"">
  <h2 style=""color:#1e293b;margin-top:0;font-size:22px"">Invitație Insolvex</h2>
  <p style=""color:#334155"">Bună <strong>{invitation.FirstName} {invitation.LastName}</strong>,</p>
  <p style=""color:#334155"">Ați fost invitat(ă) să accesați platforma <strong>Insolvex</strong> cu rolul <strong>{invitation.Role}</strong>.</p>
  <p style=""color:#334155"">Apăsați butonul de mai jos pentru a vă crea parola și a vă activa contul:</p>
  <p style=""text-align:center;margin:32px 0"">
    <a href=""{inviteLink}"" style=""background:#2563eb;color:#fff;padding:13px 28px;border-radius:7px;text-decoration:none;font-weight:600;font-size:15px;display:inline-block"">Activați contul</a>
  </p>
  <p style=""font-size:13px;color:#64748b"">Sau copiați link-ul: <a href=""{inviteLink}"" style=""color:#2563eb"">{inviteLink}</a></p>
  <hr style=""border:none;border-top:1px solid #e2e8f0;margin:24px 0""/>
  <p style=""font-size:12px;color:#94a3b8"">Link-ul este valabil <strong>7 zile</strong>. Dacă nu ați solicitat această invitație, ignorați emailul.</p>
</div></body></html>",
            ScheduledFor = DateTime.UtcNow,
            Status = "Scheduled",
            IsHtml = true,
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("User Invitation Sent", "UserInvitation", invitation.Id,
            newValues: new { invitation.Email, invitation.Role });
        return invitation.ToDto();
    }

    public async Task<List<UserInvitationDto>> GetInvitationsAsync(CancellationToken ct = default)
        => await _db.UserInvitations
            .OrderByDescending(i => i.CreatedOn)
            .Select(i => i.ToDto())
            .ToListAsync(ct);

    public async Task RevokeInvitationAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new BusinessException("No tenant context");
        var invitation = await _db.UserInvitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct)
            ?? throw new NotFoundException("UserInvitation", id);
        if (invitation.IsAccepted)
            throw new BusinessException("Cannot revoke an invitation that has already been accepted.");
        _db.UserInvitations.Remove(invitation);
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("User Invitation Revoked", "UserInvitation", invitation.Id,
            oldValues: new { invitation.Email, invitation.Role });
    }

    public async Task<UserDto> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct = default)
    {
        var invitation = await _db.UserInvitations
            .FirstOrDefaultAsync(i => i.Token == request.Token && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow, ct)
            ?? throw new BusinessException("Invalid or expired invitation token.");

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
        };

        invitation.IsAccepted = true;
        invitation.AcceptedAt = DateTime.UtcNow;

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("User Accepted Invitation", "User", user.Id,
            newValues: new { user.Email, user.Role });

        return user.ToDto();
    }

    public async Task AdminResetPasswordAsync(Guid id, AdminResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User", id);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Admin Reset User Password", "User", user.Id,
            severity: "Warning");
    }
}
