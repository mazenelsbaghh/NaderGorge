using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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
using StackExchange.Redis;
using NaderGorge.API.Hubs;
using NaderGorge.API.BackgroundServices;
using NaderGorge.API.Services;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.API.Authorization;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

SecurityConfigurationValidator.Validate(builder);

builder.Services.AddMemoryCache();

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redisConnectionString) && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Redis connection string is required outside Development.");
}

// ----------// Redis cache configuration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
});

// Singleton ConnectionMultiplexer for raw queue pushing (BulkGenerateCodesCommand)
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString ?? "localhost:6379,abortConnect=false")
);

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
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(sp.GetRequiredService<SlowQueryInterceptor>());
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// ---------- Redis ----------
builder.Services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();

// ---------- MediatR + Validation ----------
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ApiResponse).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(ApiResponse).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// ---------- Services ----------
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<IVideoProvider, YouTubeVideoProvider>();
builder.Services.AddScoped<IVideoProvider, VkVideoProvider>();
builder.Services.AddScoped<IVideoProvider, BunnyVideoProvider>();
builder.Services.AddHttpClient<IBunnyStreamClient, BunnyStreamClient>();
builder.Services.AddScoped<IAccessCheckService, AccessCheckService>();
builder.Services.AddScoped<IVideoEncryptionService, VideoEncryptionService>();
builder.Services.AddSingleton<IJobEnqueuer, RedisJobEnqueuer>();
builder.Services.AddScoped<ICachedPlatformSettingsReader, CachedPlatformSettingsReader>();
builder.Services.AddScoped<BalanceService>();
builder.Services.AddScoped<AcademicValidationService>();
builder.Services.AddScoped<NaderGorge.Application.Services.TeacherAuthorizationService>();
builder.Services.AddScoped<IIdempotencyService, RedisIdempotencyService>();
builder.Services.AddScoped<IContentImageStorage, ContentImageStorage>();
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
builder.Services.AddScoped<ILiveSupportActionService, LiveSupportActionService>();
builder.Services.AddScoped<ILiveSupportActionExecutor>(sp => sp.GetRequiredService<ILiveSupportActionService>());
builder.Services.AddScoped<ILiveSupportAssignmentCoordinator>(sp => (ILiveSupportAssignmentCoordinator)sp.GetRequiredService<ILiveSupportService>());
builder.Services.AddScoped<ILiveSupportGuestSessionService, LiveSupportGuestSessionService>();
builder.Services.AddScoped<ILiveSupportEventWriter, NaderGorge.Application.Features.LiveSupport.Services.LiveSupportEventWriter>();
builder.Services.AddSingleton<ILiveSupportAttachmentStorage, LiveSupportAttachmentStorage>();
builder.Services.AddSingleton<ILiveSupportPresenceStore, LiveSupportPresenceStore>();
builder.Services.AddHttpClient<WhatsAppVerificationService>();
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString ?? "localhost:6379,abortConnect=false", options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("MassarSignalR");
    });
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();
builder.Services.AddHostedService<LiveSupportRecoveryBackgroundService>();
builder.Services.AddHostedService<LiveSupportAIRecoveryBackgroundService>();

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
});

// ---------- Rate Limiting ----------
builder.Services.AddRateLimitingPolicies();

// ---------- Controllers + Swagger ----------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
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

var app = builder.Build();

// ---------- Middleware Pipeline ----------
var requireHttps = app.Environment.IsProduction() || app.Configuration.GetValue<bool>("Security:RequireHttps");
if (requireHttps)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestPerformanceLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseStaticFiles();
app.UseCors("FrontendPolicy");
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
}

app.Run();
