using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

/// <summary>
/// Manages per-tenant AI configuration stored in TenantAiConfig table.
/// Tenant admins can manage their own API key / model override.
/// Global admins can manage all fields for any tenant.
/// </summary>
public sealed class TenantAiConfigService : ITenantAiConfigService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly byte[] _encryptionKey;

    public TenantAiConfigService(IApplicationDbContext db, ICurrentUserService currentUser, IConfiguration config)
    {
        _db = db;
        _currentUser = currentUser;
        // Same derivation as AiConfigService so both can share the same secret
        var secret = config["AiConfig:EncryptionKey"]
                  ?? config["Jwt:Secret"]
                  ?? "insolvio-ai-default-enc-key-change-in-production";
        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public async Task<TenantAiConfigDto> GetAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        var tid = tenantId ?? _currentUser.TenantId ?? Guid.Empty;
        var cfg = await GetOrCreateAsync(tid, ct);
        return ToDto(cfg);
    }

    public async Task<TenantAiConfigDto> UpdateAsync(Guid tenantId, UpdateTenantAiConfigRequest request, CancellationToken ct = default)
    {
        var cfg = await GetOrCreateAsync(tenantId, ct);

        cfg.AiEnabled = request.AiEnabled;
        cfg.MonthlyTokenLimit = Math.Max(0, request.MonthlyTokenLimit);
        cfg.SummaryEnabled = request.SummaryEnabled;
        cfg.ChatEnabled = request.ChatEnabled;
        cfg.SummaryActivityDays = Math.Clamp(request.SummaryActivityDays, 7, 90);
        cfg.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // Key override fields
        cfg.Provider = Nullify(request.Provider);
        cfg.ApiEndpoint = Nullify(request.ApiEndpoint);
        cfg.ModelName = Nullify(request.ModelName);
        if (request.ApiKey is not null)
            cfg.ApiKeyEncrypted = request.ApiKey.Length == 0 ? null : Encrypt(request.ApiKey.Trim());

        cfg.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(cfg);
    }

    public async Task<TenantAiConfigDto> UpdateOwnApiKeyAsync(UpdateTenantAiKeyRequest request, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? Guid.Empty;
        var cfg = await GetOrCreateAsync(tenantId, ct);

        cfg.Provider = Nullify(request.Provider);
        cfg.ApiEndpoint = Nullify(request.ApiEndpoint);
        cfg.ModelName = Nullify(request.ModelName);
        if (request.ApiKey is not null)
            cfg.ApiKeyEncrypted = request.ApiKey.Length == 0 ? null : Encrypt(request.ApiKey.Trim());

        cfg.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(cfg);
    }

    public async Task<string?> GetDecryptedApiKeyAsync(Guid tenantId, CancellationToken ct = default)
    {
        var encrypted = await _db.TenantAiConfigs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ApiKeyEncrypted)
            .FirstOrDefaultAsync(ct);

        if (encrypted is null) return null;
        try { return Decrypt(encrypted); }
        catch { return null; }
    }

    public async Task<bool> IsAiEnabledAsync(Guid tenantId, CancellationToken ct = default)
    {
        var cfg = await _db.TenantAiConfigs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .Select(c => (bool?)c.AiEnabled)
            .FirstOrDefaultAsync(ct);

        return cfg ?? false;
    }

    public async Task<bool> RecordTokenUsageAsync(Guid tenantId, int tokensUsed, CancellationToken ct = default)
    {
        var cfg = await GetOrCreateAsync(tenantId, ct);

        // Reset monthly counter if we're in a new month
        var currentMonth = int.Parse(DateTime.UtcNow.ToString("yyyyMM"));
        if (cfg.UsageResetMonth != currentMonth)
        {
            cfg.CurrentMonthTokensUsed = 0;
            cfg.UsageResetMonth = currentMonth;
        }

        cfg.CurrentMonthTokensUsed += tokensUsed;
        await _db.SaveChangesAsync(ct);

        // Return true if limit exceeded (0 = no limit)
        return cfg.MonthlyTokenLimit > 0 && cfg.CurrentMonthTokensUsed > cfg.MonthlyTokenLimit;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<TenantAiConfig> GetOrCreateAsync(Guid tenantId, CancellationToken ct)
    {
        var cfg = await _db.TenantAiConfigs
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (cfg is null)
        {
            cfg = new TenantAiConfig
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CreatedOn = DateTime.UtcNow,
            };
            _db.TenantAiConfigs.Add(cfg);
            await _db.SaveChangesAsync(ct);
        }

        return cfg;
    }

    private static TenantAiConfigDto ToDto(TenantAiConfig cfg) => new(
        cfg.Id,
        cfg.AiEnabled,
        cfg.MonthlyTokenLimit,
        cfg.CurrentMonthTokensUsed,
        cfg.SummaryEnabled,
        cfg.ChatEnabled,
        cfg.SummaryActivityDays,
        cfg.Notes,
        cfg.UpdatedAt,
        cfg.ApiKeyEncrypted is not null,
        cfg.Provider,
        cfg.ApiEndpoint,
        cfg.ModelName
    );

    private static string? Nullify(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // AES-256-CBC: prepend the 16-byte IV to the ciphertext, then Base64-encode.
    private string Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var result = new byte[aes.IV.Length + cipher.Length];
        aes.IV.CopyTo(result, 0);
        cipher.CopyTo(result, aes.IV.Length);
        return Convert.ToBase64String(result);
    }

    private string Decrypt(string ciphertext)
    {
        var all = Convert.FromBase64String(ciphertext);
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = all[..16];
        using var decryptor = aes.CreateDecryptor();
        return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(all, 16, all.Length - 16));
    }
}
