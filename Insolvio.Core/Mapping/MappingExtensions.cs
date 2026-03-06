using Insolvio.Core.DTOs;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Mapping;

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
   user.TenantId,
   user.UseSavedSigningKey
  );

  public static TenantDto ToDto(this Tenant tenant) => new(
      tenant.Id,
      tenant.Name,
      tenant.Domain,
      tenant.IsActive,
      tenant.SubscriptionExpiry,
      tenant.PlanName,
      tenant.Region.ToString(),
      tenant.Language
  );

  public static CompanyDto ToDto(this Company company, int caseCount = 0, List<string>? caseNumbers = null) => new(
      company.Id,
company.Name,
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
      caseCount,
      caseNumbers
  );

  public static CaseDto ToDto(this InsolvencyCase c, int docCount = 0, int partyCount = 0) => new(
      c.Id,
      c.CaseNumber,
    c.CourtName,
   c.CourtSection,
c.JudgeSyndic,
      c.Registrar,
  c.DebtorName,
      c.DebtorCui,
c.ProcedureType,
c.Status,
      c.LawReference,
c.PractitionerName,
      c.PractitionerRole,
   c.PractitionerFiscalId,
 c.PractitionerDecisionNo,
      c.NoticeDate,
    c.OpeningDate,
   c.NextHearingDate,
    c.ClaimsDeadline,
    c.ContestationsDeadline,
      c.DefinitiveTableDate,
   c.ReorganizationPlanDeadline,
      c.ClosureDate,
c.StatusChangedAt,
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
    partyCount
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
      d.SummaryByLanguageJson,
      d.ClassificationConfidence,
      d.StorageKey,
      d.FileHash
  );

  public static TaskDto ToDto(this CompanyTask t) => new(
 t.Id,
    t.CompanyId,
      t.Company?.Name,
      t.CaseId,
      t.Case?.CaseNumber,
      t.Title,
      t.Description,
      t.Labels,
      t.Category,
 t.Deadline,
t.DeadlineSource,
      t.IsCriticalDeadline,
      t.Status,
      t.BlockReason,
      t.AssignedToUserId,
  t.AssignedTo?.FullName,
   t.CreatedByUserId,
      t.CompletedAt,
 t.CreatedOn,
    t.Summary,
    t.SummaryByLanguageJson,
    t.ReportSummary
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
    p.Email ?? p.Company?.Email,
  p.Role.ToString(),
      p.RoleDescription,
  p.ClaimAmountRon,
    p.ClaimAccepted,
      p.JoinedDate,
      p.Notes,
      p.Name,
      p.Identifier
  );

  public static CreditorClaimDto ToDto(this CreditorClaim c) => new(
      c.Id,
      c.CaseId,
      c.CreditorPartyId,
      c.CreditorParty?.Name ?? c.CreditorParty?.Company?.Name ?? "Unknown",
      c.CreditorParty?.Identifier,
      c.CreditorParty?.Role.ToString() ?? "",
      c.RowNumber,
      c.DeclaredAmount,
      c.AdmittedAmount,
      c.Rank,
      c.NatureDescription,
      c.Status,
      c.ReceivedAt,
      c.Notes,
      c.CreatedOn
  );

  public static AssetDto ToDto(this Asset a) => new(
      a.Id,
      a.CaseId,
      a.AssetType,
      a.Description,
      a.EstimatedValue,
      a.EncumbranceDetails,
      a.SecuredCreditorPartyId,
      a.SecuredCreditorParty?.Name ?? a.SecuredCreditorParty?.Company?.Name,
      a.Status,
      a.SaleProceeds,
      a.DisposedAt,
      a.Notes,
      a.CreatedOn
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

  public static EmailDto ToDto(this ScheduledEmail e) => new(
      e.Id,
      e.CaseId,
      e.To,
      e.Cc,
      e.Bcc,
      e.Subject,
      e.Body,
      e.ScheduledFor,
      e.SentAt,
      e.IsSent,
      e.Status,
      e.RetryCount,
      e.ErrorMessage,
      e.ProviderMessageId,
      e.RelatedTaskId,
      e.CreatedOn,
      e.ThreadId,
      e.InReplyToId,
      e.Direction,
      e.FromName,
      e.CaseEmailAddress,
      e.AttachmentsJson,
      e.RelatedDocumentIdsJson
  );

  public static CalendarEventDto ToDto(this CalendarEvent c) => new(
      c.Id,
      c.CaseId,
      c.Title,
      c.Description,
  c.Start,
      c.End,
 c.AllDay,
      c.Location,
      c.EventType,
  c.ParticipantsJson,
      c.IcsUrl,
c.SyncedExternal,
      c.RelatedTaskId,
    c.RelatedMeetingId,
      c.IsCancelled,
 c.CreatedOn
  );

  public static GeneratedLetterDto ToDto(this GeneratedLetter g) => new(
  g.Id,
g.CaseId,
      g.TemplateId,
   g.TemplateType.ToString(),
      g.StorageKey,
      g.FileName,
      g.ContentType,
      g.FileSizeBytes,
      g.RenderedAt,
g.SentAt,
  g.DeliveryStatus,
      g.ErrorMessage,
      g.IsCritical,
 g.SendDeadline,
      g.CreatedOn
  );

  public static UserInvitationDto ToDto(this UserInvitation i) => new(
      i.Id,
      i.Email,
      i.FirstName,
      i.LastName,
      i.Role.ToString(),
      i.IsAccepted,
      i.AcceptedAt,
      i.ExpiresAt,
      i.CreatedOn
  );

  public static CaseSummaryDto ToDto(this CaseSummary s) => new(
      s.Id,
      s.Text,
      s.TextByLanguageJson,
      s.NextActionsJson,
      s.RisksJson,
      s.UpcomingDeadlinesJson,
      s.GeneratedAt,
      s.Trigger,
      s.Model
  );

  public static CaseSummaryHistoryItem ToHistoryItem(this CaseSummary s) => new(
      s.Id,
      s.GeneratedAt,
      s.Trigger,
      s.Model
  );

  public static SigningKeyDto ToDto(this UserSigningKey k) => new(
      k.Id,
      k.Name,
      k.SubjectName,
      k.IssuerName,
      k.Thumbprint,
      k.SerialNumber,
      k.ValidFrom,
      k.ValidTo,
      k.IsActive,
      k.ValidTo.HasValue && k.ValidTo.Value < DateTime.UtcNow,
      k.LastUsedAt,
      k.CreatedOn
  );

  public static ScheduledEmailDto ToScheduledEmailDto(this ScheduledEmail e) => new(
      e.Id,
      e.To,
      e.Cc,
      e.Subject,
      e.Body,
      e.ScheduledFor,
      e.SentAt,
      e.IsSent,
      e.Status,
      e.ErrorMessage,
      e.CreatedOn
  );

  public static SystemConfigDto ToDto(this SystemConfig c) => new(c.Key, c.Value, c.Description, c.Group);
}
