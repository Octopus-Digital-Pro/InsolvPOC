using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Entities;

namespace Insolvex.API.Services;

/// <summary>
/// Manages the global AI provider configuration with AES-256 encrypted key storage.
/// The encryption key is derived from appsettings["AiConfig:EncryptionKey"],
/// falling back to the JWT secret so no extra config is required to get started.
/// </summary>
public sealed class AiConfigService : IAiConfigService
{
    private readonly ApplicationDbContext _db;
    private readonly byte[] _encryptionKey;

    public AiConfigService(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        // Derive a 32-byte (AES-256) key from the configured secret
        var secret = config["AiConfig:EncryptionKey"]
                  ?? config["Jwt:Secret"]
                  ?? "insolvex-ai-default-enc-key-change-in-production";
        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    // ── Public interface ──────────────────────────────────────────────────

    public async Task<AiConfigDto> GetAsync(CancellationToken ct = default)
    {
        var cfg = await GetOrCreateAsync(ct);
        return ToDto(cfg);
    }

    public async Task<AiConfigDto> UpdateAsync(UpdateAiConfigRequest request, CancellationToken ct = default)
    {
        var cfg = await GetOrCreateAsync(ct);

        cfg.Provider = request.Provider.Trim();
        cfg.ApiEndpoint = Nullify(request.ApiEndpoint);
        cfg.ModelName = Nullify(request.ModelName);
        cfg.DeploymentName = Nullify(request.DeploymentName);
        cfg.IsEnabled = request.IsEnabled;
        cfg.Notes = Nullify(request.Notes);

        // ApiKey handling:
        //   null          → leave existing key unchanged
        //   empty string  → clear the stored key
        //   any value     → encrypt and replace
        if (request.ApiKey is not null)
        {
            cfg.ApiKeyEncrypted = request.ApiKey.Length == 0
                ? null
                : Encrypt(request.ApiKey.Trim());
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(cfg);
    }

    public async Task<string?> GetDecryptedApiKeyAsync(CancellationToken ct = default)
    {
        var cfg = await _db.AiSystemConfigs
            .AsNoTracking()
            .OrderBy(c => c.CreatedOn)
            .FirstOrDefaultAsync(ct);

        if (cfg?.ApiKeyEncrypted is null) return null;

        try { return Decrypt(cfg.ApiKeyEncrypted); }
        catch { return null; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<AiSystemConfig> GetOrCreateAsync(CancellationToken ct)
    {
        var cfg = await _db.AiSystemConfigs
            .OrderBy(c => c.CreatedOn)
            .FirstOrDefaultAsync(ct);

        if (cfg is null)
        {
            cfg = new AiSystemConfig { Provider = "OpenAI", IsEnabled = false };
            _db.AiSystemConfigs.Add(cfg);
            await _db.SaveChangesAsync(ct);
        }

        return cfg;
    }

    private static AiConfigDto ToDto(AiSystemConfig cfg) => new(
        cfg.Id,
        cfg.Provider,
        cfg.ApiKeyEncrypted is not null,
        cfg.ApiEndpoint,
        cfg.ModelName,
        cfg.DeploymentName,
        cfg.IsEnabled,
        cfg.Notes,
        cfg.LastModifiedOn);

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
        var plain = decryptor.TransformFinalBlock(all, 16, all.Length - 16);
        return Encoding.UTF8.GetString(plain);
    }
}
