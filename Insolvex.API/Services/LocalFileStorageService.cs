using Insolvex.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Insolvex.API.Services;

/// <summary>
/// Local disk fallback for <see cref="IFileStorageService"/> when S3 is not configured.
/// Stores files under ContentRootPath/DocumentOutput.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IWebHostEnvironment env, ILogger<LocalFileStorageService> logger)
    {
        _basePath = Path.Combine(env.ContentRootPath, "DocumentOutput");
        Directory.CreateDirectory(_basePath);
        _logger = logger;
    }

    private string FullPath(string key) => Path.Combine(_basePath, key.Replace('/', Path.DirectorySeparatorChar));

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
 {
        var path = FullPath(key);
        var dir = Path.GetDirectoryName(path)!;
  Directory.CreateDirectory(dir);

 _logger.LogInformation("Saving file locally: {Path}", path);

        await using var fs = new FileStream(path, FileMode.Create);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
  {
     var path = FullPath(key);
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {key}", key);

        var ms = new MemoryStream();
    await using var fs = File.OpenRead(path);
      await fs.CopyToAsync(ms, ct);
   ms.Position = 0;
        return ms;
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = FullPath(key);
   if (File.Exists(path))
          File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
     return Task.FromResult(File.Exists(FullPath(key)));
    }

    public string GetPresignedUrl(string key, TimeSpan expiry)
    {
      // Local storage doesn't support presigned URLs — return relative path
    return $"/api/documents/download/{key}";
    }
}
