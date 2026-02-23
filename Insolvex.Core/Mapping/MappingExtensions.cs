using Insolvex.Core.DTOs;
using Insolvex.Domain.Entities;

namespace Insolvex.Core.Mapping;

public static class MappingExtensions
{
    public static UserDto ToDto(this User user) => new(
      user.Id,
        user.Email,
   user.FirstName,
        user.LastName,
        user.FullName,
  user.Role,
        user.IsActive,
        user.LastLoginDate,
        user.AvatarUrl,
     user.TenantId
    );

    public static TenantDto ToDto(this Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
tenant.Domain,
        tenant.IsActive,
        tenant.SubscriptionExpiry,
    tenant.PlanName
    );

    public static CompanyDto ToDto(this Company company, int caseCount = 0) => new(
        company.Id,
company.Name,
        company.CompanyType.ToString(),
        company.CuiRo,
        company.TradeRegisterNo,
company.VatNumber,
        company.Address,
        company.Locality,
  company.County,
     company.Country,
        company.PostalCode,
    company.Caen,
        company.IncorporationYear,
    company.ShareCapitalRon,
        company.Phone,
        company.Email,
     company.ContactPerson,
        company.Iban,
        company.BankName,
        company.AssignedToUserId,
        company.AssignedTo?.FullName,
        company.CreatedOn,
        caseCount
    );

    public static CaseDto ToDto(this InsolvencyCase c, int docCount = 0, int partyCount = 0, List<CasePhaseDto>? phases = null) => new(
        c.Id,
c.CaseNumber,
        c.CourtName,
        c.CourtSection,
        c.JudgeSyndic,
        c.DebtorName,
        c.DebtorCui,
        c.ProcedureType,
      c.Stage,
 c.LawReference,
      c.PractitionerName,
        c.PractitionerRole,
        c.PractitionerFiscalId,
        c.PractitionerDecisionNo,
        c.OpeningDate,
     c.NextHearingDate,
        c.ClaimsDeadline,
        c.ContestationsDeadline,
 c.DefinitiveTableDate,
     c.ReorganizationPlanDeadline,
        c.ClosureDate,
        c.TotalClaimsRon,
     c.SecuredClaimsRon,
  c.UnsecuredClaimsRon,
  c.BudgetaryClaimsRon,
        c.EmployeeClaimsRon,
        c.EstimatedAssetValueRon,
  c.BpiPublicationNo,
      c.BpiPublicationDate,
        c.OpeningDecisionNo,
        c.Notes,
        c.CompanyId,
        c.Company?.Name,
        c.AssignedToUserId,
        c.AssignedTo?.FullName,
        c.CreatedOn,
      docCount,
        partyCount,
        phases
  );

    public static DocumentDto ToDto(this InsolvencyDocument d) => new(
   d.Id,
        d.CaseId,
        d.SourceFileName,
        d.DocType,
        d.DocumentDate,
        d.UploadedBy,
   d.UploadedAt,
        d.RawExtraction,
    d.RequiresSignature,
        d.IsSigned,
        d.Purpose,
        d.Summary,
        d.ClassificationConfidence,
        d.StorageKey,
        d.FileHash
    );

    public static TaskDto ToDto(this CompanyTask t) => new(
        t.Id,
        t.CompanyId,
        t.Company?.Name,
        t.Title,
        t.Description,
        t.Labels,
        t.Deadline,
        t.Status,
        t.AssignedToUserId,
  t.AssignedTo?.FullName,
        t.CreatedOn
    );

    public static AuditLogDto ToDto(this AuditLog log) => new(
    log.Id,
        log.Action,
        log.Description,
  log.UserId,
        log.UserEmail,
     log.UserFullName,
  log.TenantName,
        log.EntityType,
        log.EntityId,
        log.EntityName,
     log.CaseNumber,
        log.Changes,
      log.OldValues,
      log.NewValues,
     log.IpAddress,
     log.UserAgent,
        log.RequestMethod,
log.RequestPath,
        log.ResponseStatusCode,
  log.DurationMs,
        log.Severity,
        log.Category,
        log.CorrelationId,
   log.Timestamp
    );

    public static ErrorLogDto ToDto(this ErrorLog log) => new(
  log.Id,
     log.Message,
        log.StackTrace,
        log.Source,
        log.RequestPath,
        log.RequestMethod,
        log.UserId,
        log.UserEmail,
        log.Timestamp,
      log.IsResolved
    );

    public static CasePartyDto ToDto(this CaseParty p) => new(
        p.Id,
   p.CaseId,
        p.CompanyId,
      p.Company?.Name,
    p.Role.ToString(),
        p.RoleDescription,
    p.ClaimAmountRon,
      p.ClaimAccepted,
        p.JoinedDate,
        p.Notes
    );

    public static CasePhaseDto ToDto(this CasePhase p) => new(
        p.Id,
        p.CaseId,
  p.PhaseType.ToString(),
        p.Status.ToString(),
        p.SortOrder,
        p.StartedOn,
        p.CompletedOn,
    p.DueDate,
        p.Notes,
        p.CourtDecisionRef,
        p.UpdatedByUserId
    );

    public static InsolvencyFirmDto ToDto(this InsolvencyFirm f) => new(
f.Id,
f.TenantId,
        f.FirmName,
        f.CuiRo,
        f.TradeRegisterNo,
  f.VatNumber,
   f.UnpirRegistrationNo,
f.UnpirRfo,
     f.Address,
        f.Locality,
        f.County,
        f.Country,
    f.PostalCode,
        f.Phone,
        f.Fax,
      f.Email,
f.Website,
        f.ContactPerson,
        f.Iban,
     f.BankName,
        f.SecondaryIban,
      f.SecondaryBankName,
        f.LogoUrl
    );
}
