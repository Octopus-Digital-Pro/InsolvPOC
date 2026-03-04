using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Insolvex.Core.Abstractions;
using Insolvex.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insolvex.Integrations.Services;

/// <summary>
/// AWS S3 implementation of <see cref="IFileStorageService"/>.
/// </summary>
public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly S3StorageOptions _options;
    private readonly ILogger<S3FileStorageService> _logger;

    public S3FileStorageService(IAmazonS3 s3, IOptions<S3StorageOptions> options, ILogger<S3FileStorageService> logger)
{
        _s3 = s3;
        _options = options.Value;
    _logger = logger;
    }

    private string FullKey(string key) =>
        string.IsNullOrWhiteSpace(_options.KeyPrefix)
      ? key
            : $"{_options.KeyPrefix.TrimEnd('/')}/{key}";

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var fullKey = FullKey(key);
        _logger.LogInformation("Uploading {Key} to S3 bucket {Bucket}", fullKey, _options.BucketName);

   var request = new PutObjectRequest
        {
     BucketName = _options.BucketName,
     Key = fullKey,
            InputStream = content,
  ContentType = contentType,
   ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        };

    await _s3.PutObjectAsync(request, ct);
   return fullKey;
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var fullKey = FullKey(key);
        _logger.LogInformation("Downloading {Key} from S3 bucket {Bucket}", fullKey, _options.BucketName);

        var response = await _s3.GetObjectAsync(_options.BucketName, fullKey, ct);
      return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
   var fullKey = FullKey(key);
        _logger.LogInformation("Deleting {Key} from S3 bucket {Bucket}", fullKey, _options.BucketName);

        await _s3.DeleteObjectAsync(_options.BucketName, fullKey, ct);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
var fullKey = FullKey(key);
        try
{
await _s3.GetObjectMetadataAsync(_options.BucketName, fullKey, ct);
  return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
 return false;
  }
    }

  public string GetPresignedUrl(string key, TimeSpan expiry)
  {
        var fullKey = FullKey(key);
        var request = new GetPreSignedUrlRequest
        {
      BucketName = _options.BucketName,
     Key = fullKey,
      Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET,
        };
        return _s3.GetPreSignedURL(request);
    }

    public async Task EnsureFolderAsync(string folderPrefix, CancellationToken ct = default)
    {
        // S3 has no real folders — upload a zero-byte .folder marker to make the prefix visible
        var markerKey = FullKey(folderPrefix.TrimEnd('/') + "/.folder");
        var alreadyExists = false;
        try
        {
            await _s3.GetObjectMetadataAsync(_options.BucketName, markerKey, ct);
            alreadyExists = true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }

        if (!alreadyExists)
        {
            _logger.LogDebug("Creating S3 folder marker: {Key}", markerKey);
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = markerKey,
                InputStream = new MemoryStream(),
                ContentType = "application/x-directory",
            }, ct);
        }
    }
}
