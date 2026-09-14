namespace NaderGorge.Application.Features.Admin.Commands;

internal static class DelegatedRoleDomainPolicy
{
    private static readonly HashSet<string> SupportedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "assistant"
    };

    public static bool TryNormalize(string? requestedDomain, out string allowedDomain)
    {
        allowedDomain = requestedDomain?.Trim().ToLowerInvariant() ?? string.Empty;
        return SupportedDomains.Contains(allowedDomain);
    }
}
