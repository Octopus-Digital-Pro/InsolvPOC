using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;

namespace Insolvex.Core.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public AuditService(IApplicationDbContext db, ICurrentUserService currentUser, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    // ── Backward-compatible simple log ──

    public Task LogAsync(string action, Guid? entityId = null, object? changes = null)
    {
        return LogAsync(new AuditEntry
        {
            Action = action,
            EntityType = InferEntityType(action),
            EntityId = entityId,
            Changes = changes,
            Category = InferCategory(action),
            Severity = "Info",
        });
    }

    // ── Full structured log ──

    public async Task LogAsync(AuditEntry entry)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userFullName = await ResolveUserFullNameAsync();
        var (tenantName, tenantLanguage) = await ResolveTenantInfoAsync();

        // Build court-grade description in the tenant's language
        var description = entry.Description
            ?? BuildDescription(entry, userFullName, tenantName, tenantLanguage ?? "en");

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            TenantName = tenantName,
            Action = entry.Action,
            Description = description,
            UserId = _currentUser.UserId,
            UserEmail = _currentUser.Email,
            UserFullName = userFullName,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            EntityName = entry.EntityName,
            CaseNumber = entry.CaseNumber,
            Changes = Serialize(entry.Changes),
            OldValues = Serialize(entry.OldValues),
            NewValues = Serialize(entry.NewValues),
            IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
            RequestMethod = httpContext?.Request?.Method,
            RequestPath = httpContext?.Request?.Path.Value,
            Severity = entry.Severity,
            Category = entry.Category,
            CorrelationId = entry.CorrelationId,
            Timestamp = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    // ── Entity CRUD with old/new diffing ──

    public Task LogEntityAsync(string action, string entityType, Guid entityId,
        object? oldValues = null, object? newValues = null,
        string severity = "Info", string? correlationId = null)
    {
        return LogAsync(new AuditEntry
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            Changes = ComputeDiff(oldValues, newValues),
            Category = InferCategory(action, entityType),
            Severity = severity,
            CorrelationId = correlationId,
        });
    }

    // ── Auth events ──

    public Task LogAuthAsync(string action, string? email = null, Guid? userId = null, string severity = "Info")
    {
        return LogAsync(new AuditEntry
        {
            Action = action,
            EntityType = "User",
            EntityId = userId ?? _currentUser.UserId,
            EntityName = email,
            NewValues = email != null ? new { email } : null,
            Category = "Auth",
            Severity = severity,
        });
    }

    // ── Workflow events ──

    public Task LogWorkflowAsync(string action, Guid caseId, object? details = null, string severity = "Info")
    {
        return LogAsync(new AuditEntry
        {
            Action = action,
            EntityType = "InsolvencyCase",
            EntityId = caseId,
            NewValues = details,
            Category = "Workflow",
            Severity = severity,
        });
    }

    // ── Signing events ──

    public Task LogSigningAsync(string action, Guid documentId, object? details = null, string severity = "Info")
    {
        return LogAsync(new AuditEntry
        {
            Action = action,
            EntityType = "InsolvencyDocument",
            EntityId = documentId,
            NewValues = details,
            Category = "Signing",
            Severity = severity,
        });
    }

    // ── Description Builder (language-aware) ──

    private string BuildDescription(AuditEntry entry, string? userName, string? tenantName, string language)
    {
        var who = userName ?? _currentUser.Email ?? "System";
        var what = DescribeAction(entry.Action, language);
        var entity = entry.EntityName != null
            ? $" \"{entry.EntityName}\""
            : (entry.EntityId.HasValue ? $" (ID: {entry.EntityId.Value:N})" : "");
        var entityType = entry.EntityType ?? "";
        var caseRef = entry.CaseNumber != null ? $" [{CaseLabel(language)} {entry.CaseNumber}]" : "";
        var changes = DescribeChanges(entry);
        var tenant = tenantName != null ? $" [{TenantLabel(language)}: {tenantName}]" : "";
        var severity = entry.Severity != "Info" ? $" [{entry.Severity.ToUpperInvariant()}]" : "";

        return $"{who} {what} {entityType}{entity}{caseRef}{changes}{tenant}{severity}".Trim();
    }

    private static string CaseLabel(string lang) => lang switch
    {
        "ro" => "Dosar",
        "hu" => "Ügy",
        _ => "Case",
    };

    private static string TenantLabel(string lang) => lang switch
    {
        "ro" => "Firmă",
        "hu" => "Cég",
        _ => "Tenant",
    };

    private static string DescribeAction(string action, string lang) => lang switch
    {
        "ro" => DescribeActionRo(action),
        "hu" => DescribeActionHu(action),
        _ => DescribeActionEn(action),
    };

    private static string DescribeActionEn(string action) => action switch
    {
        // Auth
        "Auth.Login.Success" => "successfully logged in",
        "Auth.Login.Failed" => "failed login attempt",
        "Auth.PasswordChanged" => "changed their password",
        "Auth.PasswordResetRequested" => "requested a password reset",
        "Auth.PasswordReset" => "reset their password using a token",
        "Auth.PasswordResetByAdmin" => "had their password reset by an administrator",
        // Users
        "User.Updated" => "updated user",
        "User.Deactivated" => "deactivated user",
        "User.Invited" => "invited new user",
        "User.InvitationAccepted" => "accepted an invitation and created account",
        // Cases
        "Case.Created" => "created case",
        "Case.Updated" => "updated case",
        "Case.Deleted" => "deleted case",
        // Documents
        "Document.Uploaded" => "uploaded document",
        "Document.Updated" => "updated document",
        "Document.Deleted" => "deleted document",
        "Document.Signed" => "digitally signed document",
        "Document.SignatureVerified" => "verified digital signature on document",
        // Tasks
        "Task.Created" => "created task",
        "Task.Updated" => "updated task",
        "Task.Completed" => "completed task",
        "Task.Deleted" => "deleted task",
        // Parties
        "Party.Added" => "added party to case",
        "Party.Updated" => "updated party",
        "Party.Removed" => "removed party from case",
        // Phases / Workflow
        "Phase.Initialized" => "initialized workflow phases for case",
        "Phase.Updated" => "updated phase status",
        "Phase.Advanced" => "advanced workflow to next phase",
        "Stage.Advanced" => "advanced case to next stage",
        // Settings
        "Settings.Updated" => "updated system settings",
        "Firm.Updated" => "updated insolvency firm details",
        "Tenant.Created" => "created new tenant",
        "Tenant.Updated" => "updated tenant",
        // Signing
        "Signing.KeyUploaded" => "uploaded a signing key",
        "Signing.KeyDeactivated" => "deactivated a signing key",
        _ => action.Replace(".", " ").ToLowerInvariant(),
    };

    private static string DescribeActionRo(string action) => action switch
    {
        // Auth
        "Auth.Login.Success" => "s-a autentificat cu succes",
        "Auth.Login.Failed" => "tentativă eșuată de autentificare",
        "Auth.PasswordChanged" => "și-a schimbat parola",
        "Auth.PasswordResetRequested" => "a solicitat resetarea parolei",
        "Auth.PasswordReset" => "și-a resetat parola folosind un token",
        "Auth.PasswordResetByAdmin" => "a avut parola resetată de un administrator",
        // Users
        "User.Updated" => "a actualizat utilizatorul",
        "User.Deactivated" => "a dezactivat utilizatorul",
        "User.Invited" => "a invitat un utilizator nou",
        "User.InvitationAccepted" => "a acceptat invitația și și-a creat contul",
        // Cases
        "Case.Created" => "a creat dosarul",
        "Case.Updated" => "a actualizat dosarul",
        "Case.Deleted" => "a șters dosarul",
        // Documents
        "Document.Uploaded" => "a încărcat documentul",
        "Document.Updated" => "a actualizat documentul",
        "Document.Deleted" => "a șters documentul",
        "Document.Signed" => "a semnat digital documentul",
        "Document.SignatureVerified" => "a verificat semnătura digitală a documentului",
        // Tasks
        "Task.Created" => "a creat sarcina",
        "Task.Updated" => "a actualizat sarcina",
        "Task.Completed" => "a finalizat sarcina",
        "Task.Deleted" => "a șters sarcina",
        // Parties
        "Party.Added" => "a adăugat o parte la dosar",
        "Party.Updated" => "a actualizat partea",
        "Party.Removed" => "a eliminat partea din dosar",
        // Phases / Workflow
        "Phase.Initialized" => "a inițializat fazele de lucru pentru dosar",
        "Phase.Updated" => "a actualizat statusul fazei",
        "Phase.Advanced" => "a avansat fluxul la faza următoare",
        "Stage.Advanced" => "a avansat dosarul la etapa următoare",
        // Settings
        "Settings.Updated" => "a actualizat setările sistemului",
        "Firm.Updated" => "a actualizat datele firmei de insolvență",
        "Tenant.Created" => "a creat un tenant nou",
        "Tenant.Updated" => "a actualizat tenant-ul",
        // Signing
        "Signing.KeyUploaded" => "a încărcat o cheie de semnătură",
        "Signing.KeyDeactivated" => "a dezactivat o cheie de semnătură",
        _ => action.Replace(".", " ").ToLowerInvariant(),
    };

    private static string DescribeActionHu(string action) => action switch
    {
        // Auth
        "Auth.Login.Success" => "sikeresen bejelentkezett",
        "Auth.Login.Failed" => "sikertelen bejelentkezési kísérlet",
        "Auth.PasswordChanged" => "megváltoztatta jelszavát",
        "Auth.PasswordResetRequested" => "jelszó-visszaállítást kért",
        "Auth.PasswordReset" => "jelszavát tokennel visszaállította",
        "Auth.PasswordResetByAdmin" => "jelszavát rendszergazda állította vissza",
        // Users
        "User.Updated" => "frissítette a felhasználót",
        "User.Deactivated" => "deaktiválta a felhasználót",
        "User.Invited" => "új felhasználót meghívott",
        "User.InvitationAccepted" => "elfogadta a meghívót és létrehozta a fiókját",
        // Cases
        "Case.Created" => "létrehozta az ügyet",
        "Case.Updated" => "frissítette az ügyet",
        "Case.Deleted" => "törölte az ügyet",
        // Documents
        "Document.Uploaded" => "feltöltötte a dokumentumot",
        "Document.Updated" => "frissítette a dokumentumot",
        "Document.Deleted" => "törölte a dokumentumot",
        "Document.Signed" => "digitálisan aláírta a dokumentumot",
        "Document.SignatureVerified" => "ellenőrizte a dokumentum digitális aláírását",
        // Tasks
        "Task.Created" => "létrehozta a feladatot",
        "Task.Updated" => "frissítette a feladatot",
        "Task.Completed" => "teljesítette a feladatot",
        "Task.Deleted" => "törölte a feladatot",
        // Parties
        "Party.Added" => "felet adott az ügyhez",
        "Party.Updated" => "frissítette a felet",
        "Party.Removed" => "eltávolította a felet az ügyből",
        // Phases / Workflow
        "Phase.Initialized" => "inicializálta az ügy munkafolyamat-fázisait",
        "Phase.Updated" => "frissítette a fázis állapotát",
        "Phase.Advanced" => "a munkafolyamatot a következő fázisra léptette",
        "Stage.Advanced" => "az ügyet a következő szakaszra léptette",
        // Settings
        "Settings.Updated" => "frissítette a rendszerbeállításokat",
        "Firm.Updated" => "frissítette az inszolvencia-iroda adatait",
        "Tenant.Created" => "új tenant-et hozott létre",
        "Tenant.Updated" => "frissítette a tenant-et",
        // Signing
        "Signing.KeyUploaded" => "aláírási kulcsot töltött fel",
        "Signing.KeyDeactivated" => "aláírási kulcsot deaktivált",
        _ => action.Replace(".", " ").ToLowerInvariant(),
    };

    private string DescribeChanges(AuditEntry entry)
    {
        if (entry.Changes == null && entry.OldValues == null && entry.NewValues == null)
            return "";

        try
        {
            var diff = entry.Changes ?? ComputeDiff(entry.OldValues, entry.NewValues);
            if (diff == null) return "";

            var json = JsonSerializer.SerializeToElement(diff, JsonOpts);
            if (json.ValueKind != JsonValueKind.Object) return "";

            var parts = new List<string>();
            foreach (var prop in json.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object
                    && prop.Value.TryGetProperty("from", out var from)
                    && prop.Value.TryGetProperty("to", out var to))
                {
                    parts.Add($"{prop.Name}: '{from}' → '{to}'");
                }
            }

            return parts.Count > 0 ? $" (changes: {string.Join(", ", parts)})" : "";
        }
        catch
        {
            return "";
        }
    }

    // ── Helpers ──

    private async Task<string?> ResolveUserFullNameAsync()
    {
        if (_currentUser.UserId == null) return null;
        try
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == _currentUser.UserId.Value)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync();
            return user;
        }
        catch { return _currentUser.Email; }
    }

    private async Task<(string? Name, string? Language)> ResolveTenantInfoAsync()
    {
        if (_currentUser.TenantId == null) return (null, null);
        try
        {
            var info = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
                .Where(t => t.Id == _currentUser.TenantId.Value)
                .Select(t => new { t.Name, t.Language, t.Region })
                .FirstOrDefaultAsync();

            if (info == null) return (null, null);

            var language = (info.Language ?? "").ToLowerInvariant();
            if (language is not ("ro" or "hu"))
            {
                language = info.Region switch
                {
                    Domain.Enums.SystemRegion.Romania => "ro",
                    Domain.Enums.SystemRegion.Hungary => "hu",
                    _ => "en",
                };
            }

            return (info.Name, language);
        }
        catch { return (null, null); }
    }

    private static string? Serialize(object? value)
    {
        if (value == null) return null;
        if (value is string s) return s;
        return JsonSerializer.Serialize(value, JsonOpts);
    }

    private static object? ComputeDiff(object? oldObj, object? newObj)
    {
        if (oldObj == null || newObj == null) return null;
        try
        {
            var oldJson = JsonSerializer.SerializeToElement(oldObj, JsonOpts);
            var newJson = JsonSerializer.SerializeToElement(newObj, JsonOpts);
            if (oldJson.ValueKind != JsonValueKind.Object || newJson.ValueKind != JsonValueKind.Object)
                return null;

            var diff = new Dictionary<string, object?>();
            foreach (var prop in newJson.EnumerateObject())
            {
                if (!oldJson.TryGetProperty(prop.Name, out var oldProp)
                    || oldProp.ToString() != prop.Value.ToString())
                {
                    diff[prop.Name] = new
                    {
                        from = oldJson.TryGetProperty(prop.Name, out var old) ? old.ToString() : null,
                        to = prop.Value.ToString(),
                    };
                }
            }
            return diff.Count > 0 ? diff : null;
        }
        catch { return null; }
    }

    private static string InferEntityType(string action)
    {
        var dot = action.IndexOf('.');
        return dot > 0 ? action[..dot] : "Unknown";
    }

    private static string InferCategory(string action, string? entityType = null)
    {
        var key = (entityType ?? action).ToLowerInvariant();
        return key switch
        {
            _ when key.Contains("auth") || key.Contains("login") || key.Contains("password") => "Auth",
            _ when key.Contains("case") && !key.Contains("party") && !key.Contains("phase") => "Case",
            _ when key.Contains("document") || key.Contains("upload") || key.Contains("template") => "Document",
            _ when key.Contains("task") => "Task",
            _ when key.Contains("party") || key.Contains("creditor") => "Party",
            _ when key.Contains("phase") || key.Contains("stage") || key.Contains("workflow") || key.Contains("advance") => "Workflow",
            _ when key.Contains("sign") => "Signing",
            _ when key.Contains("meeting") || key.Contains("calendar") => "Meeting",
            _ when key.Contains("setting") || key.Contains("firm") || key.Contains("tenant") || key.Contains("config") => "Settings",
            _ when key.Contains("user") || key.Contains("invite") => "User",
            _ => "System",
        };
    }
}
