using System.Security.Claims;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NaderGorge.API.Configuration;
using NaderGorge.API.Middleware;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Infrastructure.Cache;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Interfaces;
using NaderGorge.Infrastructure.Background;
using NaderGorge.Infrastructure.Repositories;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Infrastructure.Providers;
using NaderGorge.Infrastructure.Observability;
using StackExchange.Redis;
using NaderGorge.API.Hubs;
using NaderGorge.API.BackgroundServices;
using NaderGorge.API.Services;
using NaderGorge.API.Serialization;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.Auth.Services;
using NaderGorge.API.Authorization;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

SecurityConfigurationValidator.Validate(builder);
builder.Services.AddPlatformFinanceConfiguration(builder.Configuration);

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Massar");
if (builder.Environment.IsProduction())
{
    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo("/app/App_Data/protected/data-protection-keys"));
}
builder.Services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443);
builder.Services.AddScoped<NaderGorge.Application.Common.HR.IHrRequestContext, NaderGorge.API.Services.HttpHrRequestContext>();

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
var redisSentinels = builder.Configuration["Redis:Sentinels"];
if (string.IsNullOrWhiteSpace(redisConnectionString) &&
    string.IsNullOrWhiteSpace(redisSentinels) &&
    !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Redis connection or Sentinel endpoints are required outside Development.");
}
var redisConfiguration = RedisConnectionFactory.BuildConfiguration(builder.Configuration);

// ----------// Redis cache configuration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.ConfigurationOptions = redisConfiguration;
});

// All Redis consumers share the lazy factory connection. This avoids opening a
// process-wide socket while the host is still being assembled and allows the
// integration route inventory to replace the transport before startup.
builder.Services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(serviceProvider =>
    serviceProvider.GetRequiredService<IRedisConnectionFactory>().GetConnection());
builder.Services.AddSingleton<ILoggerProvider, RedisSystemLogProvider>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    var knownProxies = builder.Configuration["ForwardedHeaders:KnownProxies"]
        ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? Array.Empty<string>();
    foreach (var proxy in knownProxies)
    {
        if (System.Net.IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});

// ---------- Database ----------
builder.Services.AddSingleton<SlowQueryInterceptor>();
builder.Services.AddSingleton<DbCommandMetricsInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    options.AddInterceptors(
        sp.GetRequiredService<SlowQueryInterceptor>(),
        sp.GetRequiredService<DbCommandMetricsInterceptor>());
});
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<NaderGorge.Application.Features.Reporting.IReportQueryService, NaderGorge.Application.Features.Reporting.ReportQueryService>();
builder.Services.AddScoped<NaderGorge.Application.Features.Reporting.IReportExportService, NaderGorge.Infrastructure.Services.ReportExportService>();
builder.Services.AddScoped<NaderGorge.Application.Features.Reporting.IStudentLedgerExportService, NaderGorge.Infrastructure.Services.StudentLedgerExportService>();

// ---------- Redis ----------
builder.Services.AddSingleton<IUserSecurityStateCache, RedisUserSecurityStateCache>();
builder.Services.AddScoped<IUserSecurityStateSource, EfUserSecurityStateSource>();
builder.Services.AddScoped<IUserSecurityStateResolver, UserSecurityStateResolver>();

// ---------- MediatR + Validation ----------
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ApiResponse).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(ApiResponse).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(NaderGorge.Application.Common.HR.HrAuthorizationBehavior<,>));

// ---------- Services ----------
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<NaderGorge.Application.Common.HR.IHrAuditWriter, NaderGorge.Application.Common.HR.HrAuditWriter>();
builder.Services.AddScoped<NaderGorge.Application.Common.HR.IHrAuthorizationService, NaderGorge.Application.Common.HR.HrAuthorizationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.People.IHrLifecycleNotificationService, NaderGorge.Application.Features.HR.People.HrLifecycleNotificationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Attendance.AttendancePolicyEvaluator>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Leave.LeaveRequestService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Approvals.ApprovalEngine>();
builder.Services.AddHostedService<NaderGorge.API.Services.HrApprovalEscalationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Payroll.PayrollCalculationEngine>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Payroll.Commands.PayrollRunService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Payroll.FinancialRequests.FinancialRequestService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Lifecycle.DocumentAssetService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Performance.PerformanceCaseService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Recruitment.RecruitmentService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Lifecycle.LifecycleOrchestrationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Migration.HrMigrationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Retention.HrRetentionService>();
builder.Services.AddScoped<NaderGorge.Application.Features.HR.Reporting.WorkforceReportService>();
builder.Services.AddScoped<IVideoProvider, YouTubeVideoProvider>();
builder.Services.AddScoped<IVideoProvider, VkVideoProvider>();
builder.Services.AddScoped<IVideoProvider, BunnyVideoProvider>();
builder.Services.AddHttpClient<IBunnyStreamClient, BunnyStreamClient>();
builder.Services.AddScoped<IAccessCheckService, AccessCheckService>();
builder.Services.AddScoped<IAcademicScopeService, AcademicScopeService>();
builder.Services.AddScoped<IGiftUsageService, GiftUsageService>();
builder.Services.AddScoped<IPromotionalBalanceService, PromotionalBalanceService>();
builder.Services.AddScoped<ISalesTargetResolver, SalesTargetResolver>();
builder.Services.AddScoped<IDiscountEngine, DiscountEngine>();
builder.Services.AddScoped<ISalesRedemptionService, SalesRedemptionService>();
builder.Services.AddScoped<IVideoEncryptionService, VideoEncryptionService>();
builder.Services.AddSingleton<IJobEnqueuer, RedisJobEnqueuer>();
builder.Services.AddSingleton<IAiJobCancellationStore, RedisAiJobCancellationStore>();
builder.Services.AddScoped<ICachedPlatformSettingsReader, CachedPlatformSettingsReader>();
builder.Services.AddScoped<BalanceService>();
builder.Services.AddScoped<RechargeAutoMatchingService>();
builder.Services.AddScoped<AcademicValidationService>();
builder.Services.AddScoped<NaderGorge.Application.Services.TeacherAuthorizationService>();
builder.Services.AddScoped<TeacherAccountingService>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.IFinancialPostingService, NaderGorge.Infrastructure.Services.Finance.FinancialPostingService>();
builder.Services.AddScoped<NaderGorge.Application.Features.Admin.PlatformFinance.PlatformFinanceDashboardService>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.IPlatformFinanceOperationsService, NaderGorge.Infrastructure.Services.Finance.PlatformFinanceOperationsService>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.IPlatformFinancePlanningService, NaderGorge.Infrastructure.Services.Finance.PlatformFinancePlanningService>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.IPlatformFinanceExportService, NaderGorge.Infrastructure.Services.Finance.PlatformFinanceExportService>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.ITeacherFinanceExportService, NaderGorge.Infrastructure.Services.Finance.TeacherFinanceExportService>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.IPlatformFinanceMigrationService, NaderGorge.Infrastructure.Services.Finance.PlatformFinanceMigrationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.Admin.PlatformFinance.Reports.PlatformFinancialReportQueries>();
builder.Services.AddScoped<NaderGorge.Infrastructure.Services.Finance.Migration.FinancialReconciliationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.Admin.PlatformFinance.Periods.AccountingPeriodCommands>();
builder.Services.AddScoped<NaderGorge.Application.Features.Admin.PlatformFinance.Teachers.GetTeacherFinancialSummaryQuery>();
builder.Services.AddScoped<NaderGorge.Infrastructure.Services.Finance.RefundPostingService>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.IFinancialSourceAdapter, NaderGorge.Infrastructure.Services.Finance.Adapters.RechargeFinancialAdapter>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.IFinancialSourceAdapter, NaderGorge.Infrastructure.Services.Finance.Adapters.SalesFinancialAdapter>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.IFinancialSourceAdapter, NaderGorge.Infrastructure.Services.Finance.Adapters.TeacherFinancialAdapter>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.IFinancialSourceAdapter, NaderGorge.Infrastructure.Services.Finance.Adapters.PayrollFinancialAdapter>();
builder.Services.AddScoped<NaderGorge.Application.Interfaces.Finance.ILiveFinancialProjectionCoordinator, NaderGorge.Application.Services.Finance.LiveFinancialProjectionCoordinator>();
builder.Services.AddSingleton<NaderGorge.Infrastructure.Observability.PlatformFinanceMetrics>();
builder.Services.AddScoped<TeacherAgreementResolver>();
builder.Services.AddScoped<CodeGroupFinancialAccountingService>();
builder.Services.AddScoped<IIdempotencyService, RedisIdempotencyService>();
builder.Services.AddScoped<IClusterLeaseService, PostgresClusterLeaseService>();
builder.Services.AddScoped<IContentImageStorage, ContentImageStorage>();
var sharedPublicRoot = string.IsNullOrWhiteSpace(builder.Environment.WebRootPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "wwwroot")
    : builder.Environment.WebRootPath;
builder.Services.AddSingleton<ISharedFileStorage>(_ => new SharedFileStorage(
    new Dictionary<SharedFileArea, string>
    {
        [SharedFileArea.Public] = sharedPublicRoot,
        [SharedFileArea.Protected] = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "protected"),
        [SharedFileArea.Private] = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "private"),
        [SharedFileArea.LiveSupport] = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "live-support"),
        [SharedFileArea.Subtitles] = Path.Combine(sharedPublicRoot, "subtitles"),
        [SharedFileArea.MindMaps] = Path.Combine(sharedPublicRoot, "mindmaps")
    }));
builder.Services.AddScoped<ILiveSupportService, LiveSupportService>();
builder.Services.AddScoped<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIAdminService, LiveSupportAIAdminService>();
builder.Services.AddScoped<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIKnowledgeService, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIKnowledgeService>();
builder.Services.AddScoped<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIContextBuilder, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIContextBuilder>();
builder.Services.AddScoped<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAITurnOrchestrator, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAITurnOrchestrator>();
builder.Services.AddSingleton<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIDataProtector, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIDataProtector>();
builder.Services.AddScoped<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIActionExecutor, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIActionExecutor>();
builder.Services.AddScoped<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIVerificationService, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIVerificationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIRegistrationService, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIRegistrationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIHandoffService, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIHandoffService>();
builder.Services.AddScoped<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIRecoveryService, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIRecoveryService>();
builder.Services.AddHttpClient<NaderGorge.Application.Features.LiveSupportAI.Interfaces.ILiveSupportAIWorkerPreviewClient, NaderGorge.Infrastructure.Services.LiveSupportAI.LiveSupportAIWorkerPreviewClient>();
builder.Services.AddScoped<ILiveSupportActionService, LiveSupportActionService>();
builder.Services.AddScoped<ILiveSupportActionExecutor>(sp => sp.GetRequiredService<ILiveSupportActionService>());
builder.Services.AddScoped<ILiveSupportAssignmentCoordinator>(sp => (ILiveSupportAssignmentCoordinator)sp.GetRequiredService<ILiveSupportService>());
builder.Services.AddScoped<ILiveSupportGuestSessionService, LiveSupportGuestSessionService>();
builder.Services.AddScoped<ILiveSupportEventWriter, NaderGorge.Application.Features.LiveSupport.Services.LiveSupportEventWriter>();
builder.Services.AddSingleton<ILiveSupportAttachmentStorage, LiveSupportAttachmentStorage>();
builder.Services.AddSingleton<ILiveSupportPresenceStore, LiveSupportPresenceStore>();
builder.Services.AddHttpClient<WhatsAppVerificationService>();
builder.Services.AddHttpClient<WhatsAppCloudService>();
builder.Services.AddHttpClient<ThanaweyaResultsService>();
builder.Services.AddHttpClient<NaderGorge.Application.Features.Admin.Ocr.IAssessmentOcrService, NaderGorge.Infrastructure.Services.GoogleVisionAssessmentOcrService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});
builder.Services.AddHostedService<ThanaweyaResultsImportHostedService>();
builder.Services.AddScoped<WhatsAppExamNotificationService>();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    })
    .AddStackExchangeRedis(options =>
    {
        options.Configuration = redisConfiguration;
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("MassarSignalR");
    });
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();
builder.Services.AddHostedService<AdminAIRecoveryBackgroundService>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIAccessGate, NaderGorge.Infrastructure.Services.AdminAI.AdminAIAccessGate>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIRecoveryService, NaderGorge.Infrastructure.Services.AdminAI.AdminAIRecoveryService>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIExternalOperationReconciler, NaderGorge.Infrastructure.Services.AdminAI.AdminAIExternalOperationReconciler>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIConversationService, NaderGorge.Application.Features.AdminAI.Commands.AdminAIConversationService>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAITurnOrchestrator, NaderGorge.Infrastructure.Services.AdminAI.AdminAITurnOrchestrator>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAITurnCompletionService, NaderGorge.Infrastructure.Services.AdminAI.AdminAITurnCompletionService>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIReadExecutor, NaderGorge.Infrastructure.Services.AdminAI.AdminAIReadCapabilityExecutor>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIProposalBuilder, NaderGorge.Infrastructure.Services.AdminAI.AdminAIProposalBuilder>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIActionExecutor, NaderGorge.Infrastructure.Services.AdminAI.AdminAIActionExecutor>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIConfirmationChallengeService, NaderGorge.Infrastructure.Services.AdminAI.AdminAIConfirmationChallengeService>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAISecureInputService, NaderGorge.Infrastructure.Services.AdminAI.AdminAISecureInputService>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Commands.AdminAIProposalCommands>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Queries.AdminAIAuditQueries>();
builder.Services.AddScoped<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIAuditWriter, NaderGorge.Infrastructure.Services.AdminAI.AdminAIAuditWriter>();
builder.Services.AddSingleton<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAISensitiveDataPolicy, NaderGorge.Application.Features.AdminAI.Security.AdminAISensitiveDataPolicy>();
builder.Services.AddSingleton<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAIDataProtector, NaderGorge.Infrastructure.Services.AdminAI.AdminAIDataProtector>();
builder.Services.AddSingleton<NaderGorge.Application.Features.AdminAI.Interfaces.IAdminAICapabilityRegistry>(_ =>
    new NaderGorge.Application.Features.AdminAI.Catalog.AdminAICapabilityRegistry([]));
builder.Services.AddHostedService<LiveSupportRecoveryBackgroundService>();
builder.Services.AddHostedService<LiveSupportAIRecoveryBackgroundService>();
builder.Services.AddHostedService<RechargeRequestExpiryBackgroundService>();

// ---------- Authentication ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var secret = builder.Configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JWT Secret not configured");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("Invalid user claim.");
                    return;
                }

                var ct = context.HttpContext.RequestAborted;
                var securityStateResolver = context.HttpContext.RequestServices
                    .GetRequiredService<IUserSecurityStateResolver>();
                var securityState = await securityStateResolver
                    .ResolveAsync(userId, ct);

                if (securityState is null || !securityState.IsActive)
                {
                    context.Fail("User session is no longer active.");
                    return;
                }

                if (!int.TryParse(context.Principal?.FindFirst("passwordResetVersion")?.Value, out var tokenPasswordVersion) ||
                    tokenPasswordVersion != securityState.PasswordResetVersion)
                {
                    context.Fail("User password state changed.");
                    return;
                }

                if (!int.TryParse(context.Principal?.FindFirst("securityStampVersion")?.Value, out var tokenSecurityVersion) ||
                    tokenSecurityVersion != securityState.SecurityStampVersion)
                {
                    context.Fail("User security state changed.");
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddLiveSupportPolicies();
    options.AddPolicy("RequireAssistantReviewer", policy =>
        policy.RequireRole("Admin", "Assistant", "AssistantReviewer", "Staff"));

    options.AddPolicy("RequireAcademicAssistant", policy =>
        policy.RequireRole("Admin", "Teacher", "AssistantAcademic", "Assistant", "Staff"));

    options.AddPolicy("RequireStudent", policy =>
        policy.RequireRole("Student"));

    options.AddPolicy("RequireParent", policy =>
        policy.RequireRole("Parent"));
});

// ---------- Rate Limiting ----------
builder.Services.AddRateLimitingPolicies();

// ---------- Controllers + Swagger ----------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------- Compression & Caching ----------
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
builder.Services.AddOutputCache();

// ---------- CORS ----------
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        var origins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:3000")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        policy.WithOrigins(origins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

if (builder.Configuration.GetValue("AdminAI:Enabled", false))
{
    var callbackSecret = builder.Configuration["AdminAI:CallbackSecret"];
    if (string.IsNullOrWhiteSpace(callbackSecret) || callbackSecret.Length < 32)
        throw new InvalidOperationException("AdminAI:CallbackSecret must contain at least 32 characters when AdminAI is enabled.");
    var hmacValue = builder.Configuration["AdminAI:HmacKey"];
    byte[] hmacKey;
    try { hmacKey = Convert.FromBase64String(hmacValue ?? string.Empty); }
    catch (FormatException exception) { throw new InvalidOperationException("AdminAI:HmacKey must be valid base64 when AdminAI is enabled.", exception); }
    if (hmacKey.Length < 32)
        throw new InvalidOperationException("AdminAI:HmacKey must contain at least 256 bits when AdminAI is enabled.");
}

var app = builder.Build();

// ---------- Middleware Pipeline ----------
// Resolve the original scheme before HSTS/redirect decisions. Production TLS
// terminates at the trusted node gateway.
app.UseForwardedHeaders();

var requireHttps = app.Environment.IsProduction() || app.Configuration.GetValue<bool>("Security:RequireHttps");
if (requireHttps)
{
    app.UseHsts();
    // TLS terminates at the node gateway. Worker-to-backend callbacks stay on
    // the private Docker network and cannot follow a redirect to port 443.
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/api/v1/internal"),
        branch => branch.UseHttpsRedirection());
}

// Keep the CORS middleware outside the exception handler.  A controller error
// must retain its CORS headers so browser clients can read the API error rather
// than reporting it as an opaque CORS failure.
app.UseCors("FrontendPolicy");
app.UseMiddleware<ClusterIdentityMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseErrorAwareRequestPerformance();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseStaticFiles();
app.UseOutputCache();
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RedisRateLimitingMiddleware>();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<PlatformHub>("/hubs/platform");
app.MapHub<LiveSupportHub>("/hubs/live-support");

if (app.Environment.EnvironmentName != "E2e")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NaderGorge.Infrastructure.Data.AppDbContext>();
    var canSeedDefaults = app.Configuration.GetValue<bool>("SeedDefaults:Enabled") && app.Environment.IsDevelopment();
    await NaderGorge.Infrastructure.Data.Seeder.SeedAsync(db, canSeedDefaults);
    await NaderGorge.Infrastructure.Data.PlatformFinanceSeeder.SeedAsync(db);
    if (app.Configuration.GetValue<bool>("SeedDemoCatalog:Enabled"))
        await NaderGorge.Infrastructure.Data.DemoCatalogSeeder.SeedAsync(db);
}

app.Run();

public partial class Program;
