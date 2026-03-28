using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// ----------// Redis cache configuration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Singleton ConnectionMultiplexer for raw queue pushing (BulkGenerateCodesCommand)
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
    StackExchange.Redis.ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379")
);

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
builder.Services.AddScoped<IAccessCheckService, AccessCheckService>();
builder.Services.AddScoped<IVideoEncryptionService, VideoEncryptionService>();
builder.Services.AddScoped<IJobEnqueuer, RedisJobEnqueuer>();
builder.Services.AddScoped<BalanceService>();
builder.Services.AddScoped<AcademicValidationService>();

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
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAssistantReviewer", policy =>
        policy.RequireRole("Admin", "Assistant", "AssistantReviewer"));

    options.AddPolicy("RequireAcademicAssistant", policy =>
        policy.RequireRole("Admin", "Teacher", "AssistantAcademic"));
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

// ---------- CORS ----------
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// ---------- Middleware Pipeline ----------
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


app.Run();
