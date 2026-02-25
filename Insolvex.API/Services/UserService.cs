using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

public sealed class UserService : IUserService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public UserService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
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

        _db.UserInvitations.Add(invitation);
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
