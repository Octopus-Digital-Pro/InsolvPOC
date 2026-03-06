using Insolvio.Core.DTOs;
using Microsoft.AspNetCore.Http;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Settings portal service: tenant info, scheduled emails, system config,
/// insolvency firm, document templates, and demo reset.
/// All mutations are audited.
/// </summary>
public interface ISettingsService
{
    // Tenant
    Task<object> GetTenantAsync(CancellationToken ct = default);
    Task UpdateTenantAsync(UpdateTenantSettingsRequest request, CancellationToken ct = default);

    // Scheduled emails
    Task<(List<ScheduledEmailDto> Items, int Total)> GetScheduledEmailsAsync(bool? sent, int page, int pageSize, CancellationToken ct = default);
    Task<object> CreateScheduledEmailAsync(CreateScheduledEmailRequest request, CancellationToken ct = default);
    Task DeleteScheduledEmailAsync(Guid id, CancellationToken ct = default);

    // System config
    Task<List<SystemConfigDto>> GetSystemConfigAsync(string? group, CancellationToken ct = default);
    Task UpdateSystemConfigAsync(UpdateSystemConfigRequest request, CancellationToken ct = default);

    // Demo reset
    Task<object> DemoResetAsync(CancellationToken ct = default);

    // Firm
    Task<InsolvencyFirmDto?> GetFirmAsync(CancellationToken ct = default);
    Task<InsolvencyFirmDto> UpsertFirmAsync(UpsertInsolvencyFirmRequest request, CancellationToken ct = default);

    // Templates
    Task<List<object>> GetTemplatesAsync(CancellationToken ct = default);
    Task<object> UploadTemplateAsync(IFormFile file, string templateType, string? name, string? description, string? stage, bool global, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid id, CancellationToken ct = default);
    Task<(Stream Stream, string FileName, string ContentType)> DownloadTemplateAsync(Guid id, CancellationToken ct = default);
}
