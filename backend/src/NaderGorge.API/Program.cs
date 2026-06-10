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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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
builder.Services.AddScoped<IAccessCheckService, AccessCheckService>();
builder.Services.AddScoped<IVideoEncryptionService, VideoEncryptionService>();
builder.Services.AddScoped<IJobEnqueuer, RedisJobEnqueuer>();
builder.Services.AddScoped<ICachedPlatformSettingsReader, CachedPlatformSettingsReader>();
builder.Services.AddScoped<BalanceService>();
builder.Services.AddScoped<AcademicValidationService>();
builder.Services.AddScoped<NaderGorge.Application.Services.TeacherAuthorizationService>();
builder.Services.AddHttpClient<WhatsAppVerificationService>();
builder.Services.AddSignalR();

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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

if (app.Environment.EnvironmentName != "E2e")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NaderGorge.Infrastructure.Data.AppDbContext>();
    var canSeedDefaults = app.Configuration.GetValue<bool>("SeedDefaults:Enabled") && app.Environment.IsDevelopment();
    await NaderGorge.Infrastructure.Data.Seeder.SeedAsync(db, canSeedDefaults);
}

app.Run();
