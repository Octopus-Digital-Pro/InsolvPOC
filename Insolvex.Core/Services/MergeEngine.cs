using HandlebarsDotNet;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Core.Services;

/// <summary>
/// Handlebars-based merge engine for HTML document templates.
/// 
/// Supported syntax:
///   {{Case.CaseNumber}}            – scalar
///   {{Debtor.Name}}                – role-based party lookup (convenience alias)
///   {{#each Claims}} … {{/each}}   – collection repeater
///   {{#if HasCreditors}} … {{/if}} – conditional
///   {{@index}}, {{RowNo}}          – loop variables
///
/// View-model shape:
///   Case { CaseNumber, ProcedureType, … }
///   Debtor { Name, CUI, Address, … }
///   Tribunal { CourtName, CourtSection, JudgeName, … }
///   Practitioner { Name, Role, CUI, … }
///   Firm { Name, CUI, Address, … }
///   Creditors[] { Name, Role, Amount, Priority, … }
///   Claims[] { RowNo, CreditorName, DeclaredAmount, AdmittedAmount, Percent, Rank, … }
///   Assets[] { RowNo, Description, Type, EstimatedValue, Status, … }
///   Totals { DeclaredTotal, AdmittedTotal, CreditorCount, AssetCount }
///   Meetings { Date, Time, Address }
///   Signatory { Name, Role }
///   Meta { CurrentDate, CurrentYear }
///   Recipient { Name, Address, Email, Identifier, Role }  — when recipientPartyId supplied
///
/// Flat-key backward compat: {{CaseNumber}}, {{DebtorName}} etc. also work
/// because we merge flat scalars into the root context.
/// </summary>
public class MergeEngine
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<MergeEngine> _logger;

    public MergeEngine(IApplicationDbContext db, ILogger<MergeEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the full hierarchical merge view-model for a case.
    /// </summary>
    public async Task<Dictionary<string, object?>> BuildViewModelAsync(
        Guid caseId,
        Guid? recipientPartyId = null,
        DateTime? pastTasksFromDate = null,
        DateTime? pastTasksToDate = null,
        DateTime? futureTasksFromDate = null,
        DateTime? futureTasksToDate = null)
    {
        var c = await _db.InsolvencyCases
            .Include(x => x.Company)
            .Include(x => x.Parties).ThenInclude(p => p.Company)
            .Include(x => x.Claims).ThenInclude(cl => cl.CreditorParty)
            .Include(x => x.Assets)
            .FirstOrDefaultAsync(x => x.Id == caseId);

        if (c == null) return new();

        var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync();

        var vm = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // ── Case ──
        var caseObj = BuildCaseObject(c);
        vm["Case"] = caseObj;

        // ── Debtor (convenience alias for debtor company) ──
        vm["Debtor"] = BuildDebtorObject(c);

        // ── Tribunal / Court ──
        vm["Tribunal"] = BuildTribunalObject(c);

        // ── Practitioner ──
        vm["Practitioner"] = BuildPractitionerObject(c, firm);

        // ── Firm ──
        vm["Firm"] = BuildFirmObject(firm);

        // ── Creditors[] – all creditor-role parties ──
        var creditors = c.Parties
            .Where(p => p.Role is CasePartyRole.SecuredCreditor
                or CasePartyRole.UnsecuredCreditor
                or CasePartyRole.BudgetaryCreditor
                or CasePartyRole.EmployeeCreditor)
            .OrderBy(p => p.Name)
            .Select((p, idx) => BuildCreditorObject(p, idx + 1))
            .ToList();
        vm["Creditors"] = creditors;

        // ── Claims[] – from CreditorClaim entities ──
        var claims = c.Claims
            .OrderBy(cl => cl.RowNumber)
            .Select((cl, idx) => BuildClaimObject(cl, idx + 1))
            .ToList();
        vm["Claims"] = claims;

        // ── Assets[] ──
        var assets = c.Assets
            .OrderBy(a => a.CreatedOn)
            .Select((a, idx) => BuildAssetObject(a, idx + 1))
            .ToList();
        vm["Assets"] = assets;

        // ── Totals ──
        vm["Totals"] = new Dictionary<string, object?>
        {
            ["DeclaredTotal"] = c.Claims.Sum(cl => cl.DeclaredAmount).ToString("N2"),
            ["AdmittedTotal"] = c.Claims.Sum(cl => cl.AdmittedAmount ?? 0m).ToString("N2"),
            ["CreditorCount"] = creditors.Count.ToString(),
            ["ClaimCount"] = claims.Count.ToString(),
            ["AssetCount"] = assets.Count.ToString(),
            ["TotalClaimsRon"] = c.TotalClaimsRon?.ToString("N2") ?? "",
            ["SecuredClaimsRon"] = c.SecuredClaimsRon?.ToString("N2") ?? "",
            ["UnsecuredClaimsRon"] = c.UnsecuredClaimsRon?.ToString("N2") ?? "",
        };

        // ── Meetings ──
        vm["Meetings"] = new Dictionary<string, object?>
        {
            ["Date"] = c.NextHearingDate?.ToString("dd.MM.yyyy") ?? "",
            ["Time"] = c.CreditorsMeetingTime ?? "12:00",
            ["Address"] = c.CreditorsMeetingAddress ?? firm?.Address ?? "",
        };

        // ── Signatory ──
        vm["Signatory"] = new Dictionary<string, object?>
        {
            ["Name"] = firm?.ContactPerson ?? c.PractitionerName ?? "",
            ["Organization"] = firm?.FirmName ?? c.PractitionerName ?? "",
        };

        // ── Meta ──
        vm["Meta"] = new Dictionary<string, object?>
        {
            ["CurrentDate"] = DateTime.UtcNow.ToString("dd.MM.yyyy"),
            ["CurrentYear"] = DateTime.UtcNow.Year.ToString(),
        };

        // ── Mandatory periodic report helpers: past/future task windows ──
        var today = DateTime.UtcNow.Date;

        var pastFrom = (pastTasksFromDate?.Date ?? today.AddDays(-30));
        var pastTo = (pastTasksToDate?.Date ?? today);
        if (pastTo > today) pastTo = today;
        if (pastFrom > pastTo) pastFrom = pastTo;

        var futureFrom = (futureTasksFromDate?.Date ?? today);
        if (futureFrom < today) futureFrom = today;
        var futureTo = (futureTasksToDate?.Date ?? today.AddDays(30));
        if (futureTo < futureFrom) futureTo = futureFrom;

        var caseTasks = await _db.CompanyTasks
            .AsNoTracking()
            .Where(t => t.CaseId == caseId)
            .ToListAsync();

        // Past reported tasks: Done (by CompletedAt) OR InProgress (by LastModifiedOn), report summary required
        var pastReportedTasks = caseTasks
            .Where(t =>
                !string.IsNullOrWhiteSpace(t.ReportSummary)
                && (
                    (t.Status == Insolvex.Domain.Enums.TaskStatus.Done
                        && t.CompletedAt.HasValue
                        && t.CompletedAt.Value.Date >= pastFrom
                        && t.CompletedAt.Value.Date <= pastTo)
                    ||
                    (t.Status == Insolvex.Domain.Enums.TaskStatus.InProgress
                        && t.LastModifiedOn.HasValue
                        && t.LastModifiedOn.Value.Date >= pastFrom
                        && t.LastModifiedOn.Value.Date <= pastTo)
                ))
            .OrderByDescending(t => t.CompletedAt ?? t.LastModifiedOn)
            .Select((t, idx) => new Dictionary<string, object?>
            {
                ["RowNo"] = (idx + 1).ToString(),
                ["Title"] = t.Title,
                ["Summary"] = t.ReportSummary ?? "",
                ["Status"] = t.Status.ToString(),
                ["CompletedAt"] = t.CompletedAt?.ToString("dd.MM.yyyy")
                                 ?? t.LastModifiedOn?.ToString("dd.MM.yyyy")
                                 ?? "",
            })
            .ToList();

        // Future tasks: by deadline, not completed/cancelled, names only
        var futurePlannedTasks = caseTasks
            .Where(t =>
                t.Deadline.HasValue
                && t.Deadline.Value.Date >= futureFrom
                && t.Deadline.Value.Date <= futureTo
                && t.Status != Insolvex.Domain.Enums.TaskStatus.Done
                && t.Status != Insolvex.Domain.Enums.TaskStatus.Cancelled)
            .OrderBy(t => t.Deadline)
            .Select((t, idx) => new Dictionary<string, object?>
            {
                ["RowNo"] = (idx + 1).ToString(),
                ["Title"] = t.Title,
                ["Deadline"] = t.Deadline!.Value.ToString("dd.MM.yyyy"),
                ["Status"] = t.Status.ToString(),
            })
            .ToList();

        var pastHtml = new StringBuilder();
        if (pastReportedTasks.Count > 0)
        {
            pastHtml.Append("<ul>");
            foreach (var task in pastReportedTasks)
            {
                pastHtml.Append("<li><strong>")
                    .Append(System.Net.WebUtility.HtmlEncode(task["Title"]?.ToString() ?? ""))
                    .Append("</strong>: ")
                    .Append(System.Net.WebUtility.HtmlEncode(task["Summary"]?.ToString() ?? ""))
                    .Append("</li>");
            }
            pastHtml.Append("</ul>");
        }

        var pastText = string.Join("\n", pastReportedTasks.Select(t =>
            $"- {t["Title"]}: {t["Summary"]}"));

        var futureHtml = new StringBuilder();
        if (futurePlannedTasks.Count > 0)
        {
            futureHtml.Append("<ul>");
            foreach (var task in futurePlannedTasks)
            {
                futureHtml.Append("<li>")
                    .Append(System.Net.WebUtility.HtmlEncode(task["Title"]?.ToString() ?? ""))
                    .Append("</li>");
            }
            futureHtml.Append("</ul>");
        }

        var futureText = string.Join("\n", futurePlannedTasks.Select(t =>
            $"- {t["Title"]}"));

        vm["PastTasksFromDate"] = pastFrom.ToString("dd.MM.yyyy");
        vm["PastTasksToDate"] = pastTo.ToString("dd.MM.yyyy");
        vm["FutureTasksFromDate"] = futureFrom.ToString("dd.MM.yyyy");
        vm["FutureTasksToDate"] = futureTo.ToString("dd.MM.yyyy");

        vm["PastReportedTasks"] = pastReportedTasks;
        vm["FuturePlannedTasks"] = futurePlannedTasks;
        vm["HasPastReportedTasks"] = pastReportedTasks.Count > 0;
        vm["HasFuturePlannedTasks"] = futurePlannedTasks.Count > 0;

        vm["PastTasksSummaryWithReport"] = pastText;
        vm["PastTasksSummaryWithReportHtml"] = pastHtml.ToString();
        vm["FutureTasksNames"] = futureText;
        vm["FutureTasksNamesHtml"] = futureHtml.ToString();

        // ── Consolidated task report summaries (all tasks with a ReportSummary) ──
        var reportSummary = string.Join("\n\n", caseTasks
            .Where(t => !string.IsNullOrWhiteSpace(t.ReportSummary))
            .OrderByDescending(t => t.CompletedAt ?? t.LastModifiedOn ?? t.CreatedOn)
            .Select(t => $"{t.Title}: {t.ReportSummary}"));
        if (vm["Case"] is Dictionary<string, object?> caseDict)
            caseDict["ReportSummary"] = reportSummary;
        vm["ReportSummary"] = reportSummary;

        // ── Booleans for {{#if}} ──
        vm["HasCreditors"] = creditors.Count > 0;
        vm["HasClaims"] = claims.Count > 0;
        vm["HasAssets"] = assets.Count > 0;

        // ── Recipient (if specified) ──
        if (recipientPartyId.HasValue)
        {
            var party = c.Parties.FirstOrDefault(p => p.Id == recipientPartyId.Value);
            if (party != null)
            {
                vm["Recipient"] = new Dictionary<string, object?>
                {
                    ["Name"] = party.Name ?? party.Company?.Name ?? "",
                    ["Address"] = party.Address ?? party.Company?.Address ?? "",
                    ["Email"] = party.Email ?? party.Company?.Email ?? "",
                    ["Identifier"] = party.Identifier ?? party.Company?.CuiRo ?? "",
                    ["Role"] = party.Role.ToString(),
                };
            }
        }

        // ── Flat scalars at root (backward compat with {{CaseNumber}} etc.) ──
        MergeFlatScalars(vm, c, firm, creditors.Count);

        return vm;
    }

    /// <summary>
    /// Render an HTML template body using Handlebars syntax with the case view-model.
    /// </summary>
    public async Task<(string RenderedHtml, Dictionary<string, object?> ViewModel)> RenderAsync(
        string bodyHtml,
        Guid caseId,
        Guid? recipientPartyId = null,
        DateTime? pastTasksFromDate = null,
        DateTime? pastTasksToDate = null,
        DateTime? futureTasksFromDate = null,
        DateTime? futureTasksToDate = null)
    {
        var viewModel = await BuildViewModelAsync(
            caseId,
            recipientPartyId,
            pastTasksFromDate,
            pastTasksToDate,
            futureTasksFromDate,
            futureTasksToDate);
        var rendered = Render(bodyHtml, viewModel);
        return (rendered, viewModel);
    }

    /// <summary>
    /// Pure render: compile Handlebars template and apply view-model.
    /// </summary>
    public static string Render(string templateHtml, Dictionary<string, object?> viewModel)
    {
        var handlebars = Handlebars.Create();
        // Register a default missing-value helper to leave unknown placeholders as-is
        handlebars.RegisterHelper("helperMissing", (context, arguments) =>
        {
            // For unknown helpers / missing values, return the original placeholder
            return "";
        });

        var compiled = handlebars.Compile(templateHtml);
        return compiled(viewModel);
    }

    // ── View-model builders ──────────────────────────────────────────────────

    private static Dictionary<string, object?> BuildCaseObject(InsolvencyCase c)
    {
        return new Dictionary<string, object?>
        {
            ["CaseNumber"] = c.CaseNumber,
            ["DebtorName"] = c.DebtorName,
            ["DebtorCui"] = c.DebtorCui ?? "",
            ["ProcedureType"] = c.ProcedureType.ToString(),
            ["Status"] = c.Status ?? "",
            ["LawReference"] = c.LawReference ?? "Legea 85/2014",
            ["CourtName"] = c.CourtName ?? "",
            ["CourtSection"] = c.CourtSection ?? "",
            ["JudgeSyndic"] = c.JudgeSyndic ?? "",
            ["NoticeDate"] = c.NoticeDate?.ToString("dd.MM.yyyy") ?? "",
            ["OpeningDate"] = c.OpeningDate?.ToString("dd.MM.yyyy") ?? "",
            ["NextHearingDate"] = c.NextHearingDate?.ToString("dd.MM.yyyy") ?? "",
            ["ClaimsDeadline"] = c.ClaimsDeadline?.ToString("dd.MM.yyyy") ?? "",
            ["ContestationsDeadline"] = c.ContestationsDeadline?.ToString("dd.MM.yyyy") ?? "",
            ["BpiPublicationNo"] = c.BpiPublicationNo ?? "",
            ["BpiPublicationDate"] = c.BpiPublicationDate?.ToString("dd.MM.yyyy") ?? "",
            ["OpeningDecisionNo"] = c.OpeningDecisionNo ?? "",
            ["ProcedureNameUpper"] = c.ProcedureType switch
            {
                ProcedureType.Faliment => "FALIMENTULUI",
                ProcedureType.Reorganizare => "REORGANIZARII JUDICIARE",
                ProcedureType.FalimentSimplificat => "PROCEDURII SIMPLIFICATE A FALIMENTULUI",
                ProcedureType.Insolventa => "INSOLVENȚEI",
                ProcedureType.ConcordatPreventiv => "CONCORDATULUI PREVENTIV",
                ProcedureType.MandatAdHoc => "MANDATULUI AD-HOC",
                _ => c.ProcedureType.ToString().ToUpperInvariant(),
            },
            ["NotificationNumber"] = c.NotificationNumber ?? c.OpeningDecisionNo ?? "",
            ["NotificationDate"] = DateTime.UtcNow.ToString("dd.MM.yyyy"),
            ["CaseYear"] = c.CaseNumber?.Split('/').LastOrDefault()?.Trim()
                           ?? DateTime.UtcNow.Year.ToString(),
            ["DefinitiveTableDate"] = c.DefinitiveTableDate?.ToString("dd.MM.yyyy") ?? "",
            ["CreditorsMeetingTime"] = c.CreditorsMeetingTime ?? "12:00",
            ["CreditorsMeetingAddress"] = c.CreditorsMeetingAddress ?? "",
            ["CourtTaxStampAmount"] = c.CourtTaxStampAmount ?? "200,00 lei",
        };
    }

    private static Dictionary<string, object?> BuildDebtorObject(InsolvencyCase c)
    {
        return new Dictionary<string, object?>
        {
            ["Name"] = c.DebtorName,
            ["CUI"] = c.DebtorCui ?? "",
            ["Address"] = c.Company?.Address ?? "",
            ["Locality"] = c.Company?.Locality ?? "",
            ["County"] = c.Company?.County ?? "",
            ["TradeRegisterNo"] = c.Company?.TradeRegisterNo ?? "",
            ["CAEN"] = c.Company?.Caen ?? "",
            ["AdministratorName"] = c.DebtorAdministratorName ?? "",
        };
    }

    private static Dictionary<string, object?> BuildTribunalObject(InsolvencyCase c)
    {
        return new Dictionary<string, object?>
        {
            ["CourtName"] = c.CourtName ?? "",
            ["CourtSection"] = c.CourtSection ?? "",
            ["JudgeName"] = c.JudgeSyndic ?? "",
            ["RegistryAddress"] = c.CourtRegistryAddress ?? "",
            ["RegistryPhone"] = c.CourtRegistryPhone ?? "",
            ["RegistryHours"] = c.CourtRegistryHours ?? "luni–vineri, cu începere de la ora 08:00",
        };
    }

    private static Dictionary<string, object?> BuildPractitionerObject(InsolvencyCase c, InsolvencyFirm? firm)
    {
        return new Dictionary<string, object?>
        {
            ["Name"] = c.PractitionerName ?? firm?.FirmName ?? "",
            ["Role"] = c.PractitionerRole ?? "",
            ["CUI"] = c.PractitionerFiscalId ?? firm?.CuiRo ?? "",
            ["DecisionNo"] = c.PractitionerDecisionNo ?? "",
            ["EntityName"] = firm?.FirmName ?? c.PractitionerName ?? "",
            ["Address"] = firm != null
                ? string.Join(", ", new[] { firm.Address, firm.Locality, firm.County }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
                : "",
            ["UNPIRNo"] = firm?.UnpirRegistrationNo ?? "",
            ["Phone"] = firm?.Phone ?? "",
            ["Fax"] = firm?.Fax ?? "",
            ["Email"] = firm?.Email ?? "",
            ["RepresentativeName"] = firm?.ContactPerson ?? c.PractitionerName ?? "",
        };
    }

    private static Dictionary<string, object?> BuildFirmObject(InsolvencyFirm? firm)
    {
        if (firm == null) return new();
        return new Dictionary<string, object?>
        {
            ["Name"] = firm.FirmName ?? "",
            ["CUI"] = firm.CuiRo ?? "",
            ["Address"] = firm.Address ?? "",
            ["Locality"] = firm.Locality ?? "",
            ["County"] = firm.County ?? "",
            ["Phone"] = firm.Phone ?? "",
            ["Email"] = firm.Email ?? "",
            ["IBAN"] = firm.Iban ?? "",
            ["BankName"] = firm.BankName ?? "",
            ["ContactPerson"] = firm.ContactPerson ?? "",
        };
    }

    private static Dictionary<string, object?> BuildCreditorObject(CaseParty p, int rowNo)
    {
        return new Dictionary<string, object?>
        {
            ["RowNo"] = rowNo.ToString(),
            ["Name"] = p.Name ?? p.Company?.Name ?? "",
            ["Role"] = p.Role.ToString(),
            ["Amount"] = p.ClaimAmountRon?.ToString("N2") ?? "",
            ["Priority"] = p.ClaimPriority ?? p.Role.ToString(),
            ["Address"] = p.Address ?? p.Company?.Address ?? "",
            ["Email"] = p.Email ?? p.Company?.Email ?? "",
            ["Identifier"] = p.Identifier ?? p.Company?.CuiRo ?? "",
        };
    }

    private static Dictionary<string, object?> BuildClaimObject(CreditorClaim cl, int rowNo)
    {
        var declaredAmt = cl.DeclaredAmount;
        var admittedAmt = cl.AdmittedAmount ?? 0m;
        var pct = declaredAmt > 0 ? (admittedAmt / declaredAmt * 100m) : 0m;

        return new Dictionary<string, object?>
        {
            ["RowNo"] = rowNo.ToString(),
            ["CreditorName"] = cl.CreditorParty?.Name ?? "",
            ["DeclaredAmount"] = declaredAmt.ToString("N2"),
            ["AdmittedAmount"] = admittedAmt.ToString("N2"),
            ["Percent"] = pct.ToString("N1") + "%",
            ["Rank"] = cl.Rank ?? "",
            ["Nature"] = cl.NatureDescription ?? "",
            ["Status"] = cl.Status ?? "",
        };
    }

    private static Dictionary<string, object?> BuildAssetObject(Asset a, int rowNo)
    {
        return new Dictionary<string, object?>
        {
            ["RowNo"] = rowNo.ToString(),
            ["Description"] = a.Description ?? "",
            ["Type"] = a.AssetType ?? "",
            ["EstimatedValue"] = a.EstimatedValue?.ToString("N2") ?? "",
            ["Status"] = a.Status ?? "",
            ["SaleProceeds"] = a.SaleProceeds?.ToString("N2") ?? "",
            ["EncumbranceDetails"] = a.EncumbranceDetails ?? "",
        };
    }

    /// <summary>
    /// Merge legacy flat scalars into the root so that {{CaseNumber}} still works.
    /// </summary>
    private static void MergeFlatScalars(
        Dictionary<string, object?> vm, InsolvencyCase c, InsolvencyFirm? firm, int creditorCount)
    {
        // Case scalars
        vm["CaseNumber"] = c.CaseNumber;
        vm["DebtorName"] = c.DebtorName;
        vm["DebtorCui"] = c.DebtorCui ?? "";
        vm["CourtName"] = c.CourtName ?? "";
        vm["CourtSection"] = c.CourtSection ?? "";
        vm["JudgeSyndic"] = c.JudgeSyndic ?? "";
        vm["ProcedureType"] = c.ProcedureType.ToString();
        vm["LawReference"] = c.LawReference ?? "Legea 85/2014";
        vm["NoticeDate"] = c.NoticeDate?.ToString("dd.MM.yyyy") ?? "";
        vm["OpeningDate"] = c.OpeningDate?.ToString("dd.MM.yyyy") ?? "";
        vm["NextHearingDate"] = c.NextHearingDate?.ToString("dd.MM.yyyy") ?? "";
        vm["ClaimsDeadline"] = c.ClaimsDeadline?.ToString("dd.MM.yyyy") ?? "";
        vm["ContestationsDeadline"] = c.ContestationsDeadline?.ToString("dd.MM.yyyy") ?? "";
        vm["BpiPublicationNo"] = c.BpiPublicationNo ?? "";
        vm["BpiPublicationDate"] = c.BpiPublicationDate?.ToString("dd.MM.yyyy") ?? "";
        vm["OpeningDecisionNo"] = c.OpeningDecisionNo ?? "";
        vm["PractitionerName"] = c.PractitionerName ?? firm?.FirmName ?? "";
        vm["PractitionerRole"] = c.PractitionerRole ?? "";
        vm["PractitionerFiscalId"] = c.PractitionerFiscalId ?? firm?.CuiRo ?? "";
        vm["TotalClaimsRon"] = c.TotalClaimsRon?.ToString("N2") ?? "";
        vm["SecuredClaimsRon"] = c.SecuredClaimsRon?.ToString("N2") ?? "";
        vm["UnsecuredClaimsRon"] = c.UnsecuredClaimsRon?.ToString("N2") ?? "";
        vm["FirmName"] = firm?.FirmName ?? "";
        vm["FirmCui"] = firm?.CuiRo ?? "";
        vm["FirmAddress"] = firm?.Address ?? "";
        vm["CurrentDate"] = DateTime.UtcNow.ToString("dd.MM.yyyy");
        vm["CurrentYear"] = DateTime.UtcNow.Year.ToString();
        vm["CreditorCount"] = creditorCount.ToString();

        // Procedure convenience
        vm["ProcedureNameUpper"] = c.ProcedureType switch
        {
            ProcedureType.Faliment => "FALIMENTULUI",
            ProcedureType.Reorganizare => "REORGANIZARII JUDICIARE",
            ProcedureType.FalimentSimplificat => "PROCEDURII SIMPLIFICATE A FALIMENTULUI",
            ProcedureType.Insolventa => "INSOLVENȚEI",
            ProcedureType.ConcordatPreventiv => "CONCORDATULUI PREVENTIV",
            ProcedureType.MandatAdHoc => "MANDATULUI AD-HOC",
            _ => c.ProcedureType.ToString().ToUpperInvariant(),
        };

        // Debtor company
        vm["DebtorAddress"] = c.Company?.Address ?? "";
        vm["DebtorLocality"] = c.Company?.Locality ?? "";
        vm["DebtorCounty"] = c.Company?.County ?? "";
        vm["DebtorTradeRegisterNo"] = c.Company?.TradeRegisterNo ?? "";
        vm["DebtorCaen"] = c.Company?.Caen ?? "";
        vm["DebtorAdministratorName"] = c.DebtorAdministratorName ?? "";
    }
}
