namespace NaderGorge.API.CenterDesktop;

public sealed class CenterDesktopSupportOptions
{
    public string BaseUrl { get; set; } = "";
    public string AdminToken { get; set; } = "";

    internal bool TryGetOrigin(out Uri? origin)
    {
        origin = null;
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var candidate) || candidate.Scheme != Uri.UriSchemeHttps ||
            candidate.UserInfo.Length != 0 || candidate.AbsolutePath != "/" || candidate.Query.Length != 0 ||
            candidate.Fragment.Length != 0 || AdminToken.Length is < 16 or > 1017 ||
            AdminToken.Any(c => c < 33 || c > 126)) return false;
        origin = candidate;
        return true;
    }
}

public static class CenterDesktopSupportRegistration
{
    public static IServiceCollection AddCenterDesktopSupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CenterDesktopSupportOptions>(configuration.GetSection("CenterDesktopSupport"));
        services.AddHttpClient<CenterDesktopSupportClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            })
            .RemoveAllLoggers();
        return services;
    }
}

public sealed class DesktopSupportException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
