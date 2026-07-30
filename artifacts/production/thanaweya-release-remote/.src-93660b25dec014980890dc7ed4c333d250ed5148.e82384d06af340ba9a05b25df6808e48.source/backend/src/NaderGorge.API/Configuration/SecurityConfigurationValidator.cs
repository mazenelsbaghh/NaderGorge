namespace NaderGorge.API.Configuration;

public static class SecurityConfigurationValidator
{
    private static readonly string[] UnsafeSecretValues =
    {
        "change_me",
        "changeme",
        "CHANGE_ME",
        "CHANGE_ME_IN_LOCAL_ENV",
        "AIzaSyB..."
    };

    public static void Validate(WebApplicationBuilder builder)
    {
        var env = builder.Environment;
        var config = builder.Configuration;

        ValidateJwtSecret(config, env);

        if (!env.IsDevelopment())
        {
            RequireStrongSecret(config, "API_CALLBACK_SECRET", "API callback secret");
            RequireStrongSecret(config, "AI_CALLBACK_SECRET", "AI callback secret", allowMissingIf: "API_CALLBACK_SECRET");
            RequireStrongSecret(config, "ParentReports:SigningSecret", "parent report signing secret");
        }
    }

    public static bool IsUnsafeSecret(string? value, int minLength = 24)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.Trim().Length < minLength) return true;
        return UnsafeSecretValues.Any(unsafeValue =>
            string.Equals(value.Trim(), unsafeValue, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateJwtSecret(IConfiguration config, IWebHostEnvironment env)
    {
        var secret = config["JwtSettings:Secret"];
        if (IsUnsafeSecret(secret, minLength: env.IsDevelopment() ? 24 : 32))
        {
            throw new InvalidOperationException("JWT secret is missing, weak, or uses an unsafe placeholder.");
        }

        if (!env.IsDevelopment() &&
            int.TryParse(config["JwtSettings:ExpirationMinutes"], out var expirationMinutes) &&
            expirationMinutes > 120)
        {
            throw new InvalidOperationException("JWT access-token expiration must not exceed 120 minutes outside Development.");
        }
    }

    private static void RequireStrongSecret(
        IConfiguration config,
        string key,
        string label,
        string? allowMissingIf = null)
    {
        var value = config[key];
        if (string.IsNullOrWhiteSpace(value) && allowMissingIf != null)
        {
            value = config[allowMissingIf];
        }

        if (IsUnsafeSecret(value, minLength: 32))
        {
            throw new InvalidOperationException($"{label} is missing, weak, or uses an unsafe placeholder.");
        }
    }
}
