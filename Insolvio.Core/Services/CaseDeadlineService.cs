using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

public sealed class CaseDeadlineService : ICaseDeadlineService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CaseDeadlineService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CaseDeadlineDto>> GetByCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        var deadlines = await _db.CaseDeadlines
            .Where(d => d.CaseId == caseId)
            .OrderBy(d => d.DueDate)
            .ToListAsync(ct);

        return deadlines.Select(ToDto).ToList();
    }

    public async Task<CaseDeadlineDto> CreateAsync(Guid caseId, CreateCaseDeadlineBody body, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("No tenant context.");

        var deadline = new CaseDeadline
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CaseId = caseId,
            Label = body.Label,
            DueDate = body.DueDate,
            PhaseKey = body.PhaseKey,
            RelativeTo = body.RelativeTo ?? "manual",
            OffsetDays = body.OffsetDays,
            Notes = body.Notes,
        };

        _db.CaseDeadlines.Add(deadline);
        await _db.SaveChangesAsync(ct);
        return ToDto(deadline);
    }

    public async Task<CaseDeadlineDto?> UpdateAsync(Guid caseId, Guid id, UpdateCaseDeadlineBody body, CancellationToken ct = default)
    {
        var deadline = await _db.CaseDeadlines
            .FirstOrDefaultAsync(d => d.Id == id && d.CaseId == caseId, ct);

        if (deadline is null) return null;

        if (body.Label is not null) deadline.Label = body.Label;
        if (body.DueDate.HasValue) deadline.DueDate = body.DueDate.Value;
        if (body.IsCompleted.HasValue) deadline.IsCompleted = body.IsCompleted.Value;
        if (body.PhaseKey is not null) deadline.PhaseKey = body.PhaseKey;
        if (body.RelativeTo is not null) deadline.RelativeTo = body.RelativeTo;
        if (body.OffsetDays.HasValue) deadline.OffsetDays = body.OffsetDays;
        if (body.Notes is not null) deadline.Notes = body.Notes;

        await _db.SaveChangesAsync(ct);
        return ToDto(deadline);
    }

    public async Task<bool> DeleteAsync(Guid caseId, Guid id, CancellationToken ct = default)
    {
        var deadline = await _db.CaseDeadlines
            .FirstOrDefaultAsync(d => d.Id == id && d.CaseId == caseId, ct);

        if (deadline is null) return false;

        _db.CaseDeadlines.Remove(deadline);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static CaseDeadlineDto ToDto(CaseDeadline d) => new(
        d.Id,
        d.CaseId,
        d.Label,
        d.DueDate,
        d.IsCompleted,
        d.PhaseKey,
        d.RelativeTo,
        d.OffsetDays,
        d.Notes
    );
}
