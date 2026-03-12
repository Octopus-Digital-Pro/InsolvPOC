using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Insolvio.Data;
using Insolvio.API.Middleware;
using Insolvio.Core.Services;
using Insolvio.Integrations.Services;
using Insolvio.Data.Services; // CurrentUserService (ASP.NET IHttpContextAccessor dependency stays in Data)
using Insolvio.Core.Abstractions;
using Insolvio.Core.Configuration;
using Insolvio.API.Authorization;
using Insolvio.Domain.Enums;

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
            sql.MigrationsAssembly("Insolvio.Data");
            sql.CommandTimeout(300); // 5 min — allows large import batches to complete
            sql.EnableRetryOnFailure(
                maxRetryCount: 6,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null); // null = use default transient list (includes 1205 deadlock)
        })
    // The global tenant query filter is intentional; suppress the EF Core navigation-interaction advisory.
    .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

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
builder.Services.AddHttpClient();
builder.Services.AddScoped<IDocumentAiService, DocumentAiService>();
builder.Services.AddScoped<WordTemplateImportService>();
builder.Services.AddScoped<IncomingDocumentProfileService>();
builder.Services.AddScoped<DocumentClassificationService>();
builder.Services.Configure<MailMergeOptions>(builder.Configuration.GetSection(MailMergeOptions.SectionName));
builder.Services.AddScoped<IHtmlPdfService, HtmlPdfService>();
builder.Services.AddScoped<MailMergeService>();
builder.Services.AddSingleton<IDocumentSigningService, DocumentSigningService>();
builder.Services.AddScoped<DeadlineEngine>();
builder.Services.AddScoped<CreditorMeetingService>();
builder.Services.AddScoped<IDocumentExtractionService, StubDocumentExtractionService>();
builder.Services.AddScoped<ICaseSummaryService, StubCaseSummaryService>();

// New services per InsolvencyAppRules
builder.Services.AddScoped<CaseCreationService>();
builder.Services.AddScoped<MergeEngine>();
builder.Services.AddScoped<TemplateGenerationService>();
builder.Services.AddScoped<TaskEscalationService>();
builder.Services.AddScoped<SummaryRefreshService>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<ICaseDocumentUploadService, CaseDocumentUploadService>();

// DDD service layer
builder.Services.AddScoped<ICaseService, CaseService>();
builder.Services.AddScoped<ICaseDeadlineService, CaseDeadlineService>();
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
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<ICreditorClaimsService, CreditorClaimsService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<IErrorLogService, ErrorLogService>();
builder.Services.AddScoped<ICaseCalendarService, CaseCalendarService>();
builder.Services.AddScoped<IDeadlineSettingsService, DeadlineSettingsService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<ISigningKeyService, SigningKeyService>();
builder.Services.AddScoped<ICaseEventService, CaseEventService>();
builder.Services.AddScoped<IAiConfigService, AiConfigService>();
builder.Services.AddScoped<ITenantAiConfigService, TenantAiConfigService>();
builder.Services.AddScoped<ICaseAiService, CaseAiService>();
builder.Services.AddScoped<ICaseWorkflowService, CaseWorkflowService>();
builder.Services.AddScoped<IAiFeedbackService, AiFeedbackService>();
builder.Services.AddScoped<ITrainingService, TrainingService>();
builder.Services.AddScoped<ICaseEmailAddressGenerator, CaseEmailAddressGenerator>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IInboundEmailProcessorService, InboundEmailProcessorService>();
builder.Services.AddScoped<IRegionService, RegionService>();

// Background services
builder.Services.AddHostedService<Insolvio.API.BackgroundServices.DeadlineReminderService>();
builder.Services.AddHostedService<Insolvio.API.BackgroundServices.TemplateEnforcementService>();

// Email service
builder.Services.Configure<Insolvio.Integrations.Services.SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailService, Insolvio.Integrations.Services.SmtpEmailService>();
builder.Services.AddHostedService<Insolvio.API.BackgroundServices.EmailBackgroundService>();
builder.Services.AddHostedService<Insolvio.API.BackgroundServices.InboundEmailPollingService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ----- AWS S3 File Storage -----
builder.Services.Configure<S3StorageOptions>(builder.Configuration.GetSection(S3StorageOptions.SectionName));

// Storage provider: toggled via SystemConfig.StorageProvider ("Local" or "AwsS3").
// Credentials come from appsettings.Production.json Aws:S3 section — never stored in the DB.
// On EC2 with an IAM role attached, leave AccessKeyId/SecretAccessKey blank: the AWS SDK
// will discover credentials automatically from the instance metadata endpoint.
builder.Services.AddSingleton<LocalFileStorageService>();
builder.Services.AddSingleton<IFileStorageService>(sp =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Feature flag: StorageProvider row in SystemConfig (seeded as "Local")
    var providerConfig = db.SystemConfigs.AsNoTracking()
        .FirstOrDefault(c => c.Key == "StorageProvider");
    var providerType = providerConfig?.Value ?? "Local";

    if (providerType.Equals("AwsS3", StringComparison.OrdinalIgnoreCase))
    {
        // Primary source: appsettings (Aws:S3 section) — never put secrets in the DB.
        var s3Opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<S3StorageOptions>>().Value;

        // Secondary source: DB SystemConfig rows (non-secret values like Region/KeyPrefix).
        // Credentials (AccessKeyId, SecretAccessKey) are IGNORED from DB for security.
        if (string.IsNullOrWhiteSpace(s3Opts.BucketName))
        {
            var dbCfg = db.SystemConfigs.AsNoTracking()
                .Where(c => c.Group == "Storage" && c.Key.StartsWith("S3:"))
                .ToDictionary(c => c.Key, c => c.Value);
            s3Opts = new S3StorageOptions
            {
                // Credentials ONLY from appsettings / IAM role — never from DB
                AccessKeyId = s3Opts.AccessKeyId,
                SecretAccessKey = s3Opts.SecretAccessKey,
                Region = dbCfg.GetValueOrDefault("S3:Region", "eu-central-1"),
                BucketName = dbCfg.GetValueOrDefault("S3:BucketName", ""),
                KeyPrefix = dbCfg.GetValueOrDefault("S3:KeyPrefix", "documents/"),
                ServiceUrl = dbCfg.GetValueOrDefault("S3:ServiceUrl", ""),
                ForcePathStyle = bool.TryParse(dbCfg.GetValueOrDefault("S3:ForcePathStyle", "false"), out var fps2) && fps2,
            };
        }

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

            // Use explicit keys when provided; otherwise fall back to the AWS default
            // credential chain (EC2 IAM role → env vars → ~/.aws/credentials).
            IAmazonS3 s3Client = string.IsNullOrWhiteSpace(s3Opts.AccessKeyId)
                ? new AmazonS3Client(s3Config)
                : new AmazonS3Client(s3Opts.AccessKeyId, s3Opts.SecretAccessKey, s3Config);

            return new S3FileStorageService(
                s3Client,
                Microsoft.Extensions.Options.Options.Create(s3Opts),
                sp.GetRequiredService<ILogger<S3FileStorageService>>());
        }
    }

    // Default: local disk (DocumentOutput/)
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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Insolvio API", Version = "v1" });
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

// ----- SPA static files (production only — Vite dev server handles the SPA in development) -----
if (!app.Environment.IsDevelopment())
{
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

// ----- Migrate and seed on first start (all environments) -----
// MigrateAsync is idempotent; seed methods check for existing data and skip when the DB already has records.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
    await DbSeeder.EnsureDemoUsersAsync(db);   // upserts demo accounts on every startup
    await DbSeeder.SeedSystemTemplatesAsync(db);
    await DbSeeder.SeedWorkflowStagesAsync(db);
    await DbSeeder.SeedRegionsAsync(db);       // ensures default regions (Romania, Hungary)
}

app.Run();
