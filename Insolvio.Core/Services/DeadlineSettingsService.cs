using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

public sealed class DeadlineSettingsService : IDeadlineSettingsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly DeadlineEngine _engine;

    public DeadlineSettingsService(
        IApplicationDbContext db, ICurrentUserService currentUser,
        IAuditService audit, DeadlineEngine engine)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _engine = engine;
    }

    public async Task<TenantDeadlineSettingsDto?> GetTenantSettingsAsync(CancellationToken ct = default)
    {
        if (!_currentUser.TenantId.HasValue) throw new BusinessException("No tenant context");
        var s = await _db.TenantDeadlineSettings
            .FirstOrDefaultAsync(x => x.TenantId == _currentUser.TenantId.Value, ct);
        return s == null ? null : ToDto(s);
    }

    public async Task<TenantDeadlineSettingsDto> UpsertTenantSettingsAsync(
        UpdateTenantDeadlineSettingsRequest request, CancellationToken ct = default)
    {
        if (!_currentUser.TenantId.HasValue) throw new BusinessException("No tenant context");

        var existing = await _db.TenantDeadlineSettings
            .FirstOrDefaultAsync(x => x.TenantId == _currentUser.TenantId.Value, ct);

        if (existing == null)
        {
            existing = new TenantDeadlineSettings { TenantId = _currentUser.TenantId.Value };
            _db.TenantDeadlineSettings.Add(existing);
        }

        if (request.SendInitialNoticeWithinDays.HasValue) existing.SendInitialNoticeWithinDays = request.SendInitialNoticeWithinDays.Value;
        if (request.ClaimDeadlineDaysFromNotice.HasValue) existing.ClaimDeadlineDaysFromNotice = request.ClaimDeadlineDaysFromNotice.Value;
        if (request.ObjectionDeadlineDaysFromNotice.HasValue) existing.ObjectionDeadlineDaysFromNotice = request.ObjectionDeadlineDaysFromNotice.Value;
        if (request.MeetingNoticeMinimumDays.HasValue) existing.MeetingNoticeMinimumDays = request.MeetingNoticeMinimumDays.Value;
        if (request.ReportEveryNDays.HasValue) existing.ReportEveryNDays = request.ReportEveryNDays.Value;
        if (request.UseBusinessDays.HasValue) existing.UseBusinessDays = request.UseBusinessDays.Value;
        if (request.AdjustToNextWorkingDay.HasValue) existing.AdjustToNextWorkingDay = request.AdjustToNextWorkingDay.Value;
        if (request.ReminderDaysBeforeDeadline != null) existing.ReminderDaysBeforeDeadline = request.ReminderDaysBeforeDeadline;
        if (request.UrgentQueueHoursBeforeDeadline.HasValue) existing.UrgentQueueHoursBeforeDeadline = request.UrgentQueueHoursBeforeDeadline.Value;
        if (request.AutoAssignBackupOnCriticalOverdue.HasValue) existing.AutoAssignBackupOnCriticalOverdue = request.AutoAssignBackupOnCriticalOverdue.Value;
        if (request.EmailFromName != null) existing.EmailFromName = request.EmailFromName;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Deadline Settings Updated", existing.Id);
        return ToDto(existing);
    }

    public async Task<List<CaseDeadlineOverrideDto>> GetCaseOverridesAsync(Guid caseId, CancellationToken ct = default)
    {
        var overrides = await _db.CaseDeadlineOverrides
            .Where(o => o.CaseId == caseId && o.IsActive)
            .OrderBy(o => o.DeadlineKey)
            .ToListAsync(ct);
        return overrides.Select(ToDto).ToList();
    }

    public async Task<CaseDeadlineOverrideDto> CreateCaseOverrideAsync(
        Guid caseId, CreateCaseDeadlineOverrideRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BusinessException("Reason is mandatory for deadline overrides.");

        var caseEntity = await _db.InsolvencyCases.FirstOrDefaultAsync(c => c.Id == caseId, ct)
            ?? throw new NotFoundException("InsolvencyCase", caseId);

        // Deactivate existing overrides for same key
        var existing = await _db.CaseDeadlineOverrides
            .Where(o => o.CaseId == caseId && o.DeadlineKey == request.DeadlineKey && o.IsActive)
            .ToListAsync(ct);
        foreach (var old in existing) old.IsActive = false;

        // Get original value from engine before override
        var currentSettings = await _engine.GetEffectiveSettingsAsync(caseId, null);
        var originalValue = request.DeadlineKey switch
        {
            "ClaimDeadlineDaysFromNotice" => currentSettings.ClaimDeadlineDaysFromNotice.ToString(),
            "ObjectionDeadlineDaysFromNotice" => currentSettings.ObjectionDeadlineDaysFromNotice.ToString(),
            "SendInitialNoticeWithinDays" => currentSettings.SendInitialNoticeWithinDays.ToString(),
            "MeetingNoticeMinimumDays" => currentSettings.MeetingNoticeMinimumDays.ToString(),
            "ReportEveryNDays" => currentSettings.ReportEveryNDays.ToString(),
            _ => null,
        };

        var newOverride = new CaseDeadlineOverride
        {
            TenantId = caseEntity.TenantId,
            CaseId = caseId,
            DeadlineKey = request.DeadlineKey,
            OriginalValue = originalValue,
            OverrideValue = request.OverrideValue,
            Reason = request.Reason,
            OverriddenByUserId = _currentUser.UserId,
            OverriddenAt = DateTime.UtcNow,
            IsActive = true,
        };

        _db.CaseDeadlineOverrides.Add(newOverride);
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Case Deadline Override Created", "CaseDeadlineOverride", newOverride.Id,
            newValues: new { request.DeadlineKey, originalValue, request.OverrideValue, request.Reason },
            severity: "Warning");

        return ToDto(newOverride);
    }

    public async Task DeactivateCaseOverrideAsync(Guid caseId, Guid overrideId, CancellationToken ct = default)
    {
        var entity = await _db.CaseDeadlineOverrides
            .FirstOrDefaultAsync(o => o.Id == overrideId && o.CaseId == caseId, ct)
            ?? throw new NotFoundException("CaseDeadlineOverride", overrideId);

        entity.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Case Deadline Override Deactivated", overrideId);
    }

    private static TenantDeadlineSettingsDto ToDto(TenantDeadlineSettings s) => new(
        s.Id,
        s.SendInitialNoticeWithinDays,
        s.ClaimDeadlineDaysFromNotice,
        s.ObjectionDeadlineDaysFromNotice,
        s.MeetingNoticeMinimumDays,
        s.ReportEveryNDays,
        s.UseBusinessDays,
        s.AdjustToNextWorkingDay,
        s.ReminderDaysBeforeDeadline,
        s.EmailFromName,
        s.UrgentQueueHoursBeforeDeadline,
        s.AutoAssignBackupOnCriticalOverdue
    );

    private static CaseDeadlineOverrideDto ToDto(CaseDeadlineOverride o) => new(
        o.Id,
        o.CaseId,
        o.DeadlineKey,
        o.OriginalValue,
        o.OverrideValue,
        o.Reason,
        o.OverriddenByUserId,
        o.OverriddenAt,
        o.IsActive
    );
}
