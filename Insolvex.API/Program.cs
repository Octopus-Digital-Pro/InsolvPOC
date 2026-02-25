using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Insolvex.API.Data;
using Insolvex.API.Middleware;
using Insolvex.API.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Core.Configuration;
using Insolvex.API.Authorization;
using Insolvex.Domain.Enums;

var builder = WebApplication.CreateBuilder(args);

// ----- Kestrel: allow large file uploads up to 700 MB (e.g. ONRC CSV) -----
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 700_000_000; // 700 MB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(20);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    // Prevent Kestrel from cutting slow connections mid-upload
    options.Limits.MinRequestBodyDataRate = new Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate(
        bytesPerSecond: 100, gracePeriod: TimeSpan.FromSeconds(60));
});

// ----- Global multipart limits — must be >= Kestrel limit -----
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 700_000_000; // 700 MB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = 65_536;
});

// ----- Database -----
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql =>
        {
            sql.MigrationsAssembly("Insolvex.API");
            sql.CommandTimeout(300); // 5 min — allows large import batches to complete
        }));

// ----- Authentication -----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
         ValidateAudience = true,
   ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
   Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
     };
    });

builder.Services.AddAuthorization(options =>
{
    // Register a policy for each Permission enum value
  foreach (var permission in Enum.GetValues<Permission>())
 {
   options.AddPolicy(
     $"{RequirePermissionAttribute.PolicyPrefix}{permission}",
      policy => policy.Requirements.Add(new PermissionRequirement(permission)));
    }
});

// Permission handler
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();

// ----- CORS -----
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
   .AllowAnyHeader()
    .AllowAnyMethod()
           .AllowCredentials();
    });
});

// ----- Services -----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<DocumentClassificationService>();
builder.Services.Configure<MailMergeOptions>(builder.Configuration.GetSection(MailMergeOptions.SectionName));
builder.Services.AddScoped<HtmlPdfService>();
builder.Services.AddScoped<MailMergeService>();
builder.Services.AddSingleton<IDocumentSigningService, DocumentSigningService>();
builder.Services.AddScoped<WorkflowValidationService>();
builder.Services.AddScoped<DeadlineEngine>();
builder.Services.AddScoped<CreditorMeetingService>();
builder.Services.AddScoped<IDocumentExtractionService, StubDocumentExtractionService>();
builder.Services.AddScoped<ICaseSummaryService, StubCaseSummaryService>();

// New services per InsolvencyAppRules
builder.Services.AddScoped<CaseCreationService>();
builder.Services.AddScoped<TemplateGenerationService>();
builder.Services.AddScoped<TaskEscalationService>();
builder.Services.AddScoped<SummaryRefreshService>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();

// DDD service layer
builder.Services.AddScoped<ICaseService, CaseService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ICaseEmailService, CaseEmailService>();
builder.Services.AddScoped<IBulkEmailService, BulkEmailService>();
builder.Services.AddScoped<ITribunalService, TribunalService>();
builder.Services.AddScoped<IFinanceAuthorityService, FinanceAuthorityService>();
builder.Services.AddScoped<ILocalGovernmentService, LocalGovernmentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IONRCFirmService, ONRCFirmService>();
builder.Services.AddScoped<IFirmLookupService, FirmLookupService>();

// New services (controller refactor — zero _db in controllers)
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ICasePartyService, CasePartyService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<IErrorLogService, ErrorLogService>();
builder.Services.AddScoped<ICaseCalendarService, CaseCalendarService>();
builder.Services.AddScoped<ICasePhasesService, CasePhasesService>();
builder.Services.AddScoped<IDeadlineSettingsService, DeadlineSettingsService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<ISigningKeyService, SigningKeyService>();
builder.Services.AddScoped<ICaseEventService, CaseEventService>();
builder.Services.AddScoped<IAiConfigService, AiConfigService>();

// Background services
builder.Services.AddHostedService<Insolvex.API.BackgroundServices.DeadlineReminderService>();
builder.Services.AddHostedService<Insolvex.API.BackgroundServices.TemplateEnforcementService>();

// Email service
builder.Services.Configure<Insolvex.API.Services.SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailService, Insolvex.API.Services.SmtpEmailService>();
builder.Services.AddHostedService<Insolvex.API.BackgroundServices.EmailBackgroundService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ----- AWS S3 File Storage -----
builder.Services.Configure<S3StorageOptions>(builder.Configuration.GetSection(S3StorageOptions.SectionName));

// Storage provider: resolved from SystemConfig in DB (Local or AwsS3)
// Register both implementations as named services; factory picks the active one.
builder.Services.AddSingleton<LocalFileStorageService>();
builder.Services.AddSingleton<IFileStorageService>(sp =>
{
    // At startup, read the DB to determine which provider. Default to Local.
  using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var providerConfig = db.SystemConfigs.AsNoTracking()
  .FirstOrDefault(c => c.Key == "StorageProvider");

    var providerType = providerConfig?.Value ?? "Local";

    if (providerType.Equals("AwsS3", StringComparison.OrdinalIgnoreCase))
    {
        // Read S3 config from SystemConfig table
        var configs = db.SystemConfigs.AsNoTracking()
            .Where(c => c.Group == "Storage" && c.Key.StartsWith("S3:"))
       .ToDictionary(c => c.Key, c => c.Value);

   var s3Opts = new S3StorageOptions
        {
            AccessKeyId = configs.GetValueOrDefault("S3:AccessKeyId", ""),
  SecretAccessKey = configs.GetValueOrDefault("S3:SecretAccessKey", ""),
            Region = configs.GetValueOrDefault("S3:Region", "eu-central-1"),
            BucketName = configs.GetValueOrDefault("S3:BucketName", ""),
       KeyPrefix = configs.GetValueOrDefault("S3:KeyPrefix", "documents/"),
          ServiceUrl = configs.GetValueOrDefault("S3:ServiceUrl", ""),
  ForcePathStyle = bool.TryParse(configs.GetValueOrDefault("S3:ForcePathStyle", "false"), out var fps) && fps,
    };

        if (!string.IsNullOrWhiteSpace(s3Opts.BucketName))
        {
            var s3Config = new AmazonS3Config
    {
  RegionEndpoint = RegionEndpoint.GetBySystemName(s3Opts.Region),
    };
          if (!string.IsNullOrWhiteSpace(s3Opts.ServiceUrl))
  {
      s3Config.ServiceURL = s3Opts.ServiceUrl;
                s3Config.ForcePathStyle = s3Opts.ForcePathStyle;
  }

    var s3Client = new AmazonS3Client(s3Opts.AccessKeyId, s3Opts.SecretAccessKey, s3Config);
            return new S3FileStorageService(
        s3Client,
Microsoft.Extensions.Options.Options.Create(s3Opts),
           sp.GetRequiredService<ILogger<S3FileStorageService>>());
        }
    }

    // Default: Local file storage
 return sp.GetRequiredService<LocalFileStorageService>();
});

// ----- Controllers -----
builder.Services.AddControllers()
  .AddJsonOptions(options =>
    {
     options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
  options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// ----- Swagger -----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Insolvex API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer {token}'",
  Name = "Authorization",
    In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
 Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
    {
        new OpenApiSecurityScheme
      {
         Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ----- Middleware pipeline -----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ErrorLoggingMiddleware>();
app.UseExceptionHandler();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<AuditMiddleware>();

app.MapControllers();

// ----- SPA static files (production) -----
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

// ----- Auto-migrate in Development -----
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
  var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();
