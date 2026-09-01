using Microsoft.Extensions.Configuration;

namespace NaderGorge.Infrastructure.Services;

public sealed record FacebookMessengerPageConfiguration(
    string PageId,
    string DisplayName,
    string AccessToken,
    bool HumanAgentEnabled);

public sealed class FacebookMessengerConfiguration
{
    private const int MaximumPageCount = 3;
    private readonly IReadOnlyDictionary<string, FacebookMessengerPageConfiguration> _pages;

    public FacebookMessengerConfiguration(IConfiguration configuration)
    {
        VerifyToken = configuration["FacebookMessenger:VerifyToken"]?.Trim() ?? string.Empty;
        AppSecret = configuration["FacebookMessenger:AppSecret"]?.Trim() ?? string.Empty;
        var configuredApiVersion = configuration["FacebookMessenger:ApiVersion"]?.Trim();
        ApiVersion = string.IsNullOrWhiteSpace(configuredApiVersion) ? "v26.0" : configuredApiVersion;
        if (!IsValidApiVersion(ApiVersion))
            throw new FacebookMessengerConfigurationException("MESSENGER_API_VERSION_INVALID");
        _pages = LoadPages(configuration.GetSection("FacebookMessenger:Pages"));
        ValidateFeatureConfiguration();
    }

    public string VerifyToken { get; }
    public string AppSecret { get; }
    public string ApiVersion { get; }
    public IReadOnlyCollection<FacebookMessengerPageConfiguration> Pages => _pages.Values.ToArray();

    public bool TryGetPage(string pageId, out FacebookMessengerPageConfiguration page) =>
        _pages.TryGetValue(pageId, out page!);

    public FacebookMessengerPageConfiguration RequirePage(string pageId) =>
        TryGetPage(pageId, out var page)
            ? page
            : throw new FacebookMessengerConfigurationException("MESSENGER_PAGE_NOT_CONFIGURED");

    private static IReadOnlyDictionary<string, FacebookMessengerPageConfiguration> LoadPages(
        IConfigurationSection pagesSection)
    {
        var configuredPages = pagesSection.GetChildren()
            .Where(section => !IsEmpty(section))
            .Select(ParsePage)
            .ToArray();
        if (configuredPages.Length > MaximumPageCount)
            throw new FacebookMessengerConfigurationException("MESSENGER_PAGE_LIMIT_EXCEEDED");
        if (configuredPages.GroupBy(page => page.PageId, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new FacebookMessengerConfigurationException("MESSENGER_PAGE_ID_DUPLICATED");
        return configuredPages.ToDictionary(page => page.PageId, StringComparer.Ordinal);
    }

    private static FacebookMessengerPageConfiguration ParsePage(IConfigurationSection pageSection)
    {
        var pageId = Required(pageSection, "PageId");
        if (pageId.Length > 64)
            throw new FacebookMessengerConfigurationException("MESSENGER_PAGE_ID_TOO_LONG");
        var displayName = pageSection["DisplayName"]?.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName)
            ? pageSection["PageName"]?.Trim()
            : displayName;
        if (string.IsNullOrWhiteSpace(displayName))
            throw new FacebookMessengerConfigurationException("MESSENGER_DISPLAY_NAME_REQUIRED");
        if (displayName.Length > 120)
            throw new FacebookMessengerConfigurationException("MESSENGER_DISPLAY_NAME_TOO_LONG");
        var accessToken = Required(pageSection, "AccessToken");
        var humanAgentEnabled = bool.TryParse(pageSection["HumanAgentEnabled"], out var enabled) && enabled;
        return new FacebookMessengerPageConfiguration(pageId, displayName, accessToken, humanAgentEnabled);
    }

    private static bool IsEmpty(IConfigurationSection section)
    {
        var identityIsEmpty = new[] { "PageId", "DisplayName", "PageName", "AccessToken" }
            .All(key => string.IsNullOrWhiteSpace(section[key]));
        var humanAgentEnabled = bool.TryParse(
            section["HumanAgentEnabled"],
            out var enabled) && enabled;
        return identityIsEmpty && !humanAgentEnabled;
    }

    private void ValidateFeatureConfiguration()
    {
        var hasCredentials = VerifyToken.Length > 0 || AppSecret.Length > 0;
        if (_pages.Count == 0 && !hasCredentials) return;
        if (VerifyToken.Length == 0)
            throw new FacebookMessengerConfigurationException("MESSENGER_VERIFY_TOKEN_REQUIRED");
        if (AppSecret.Length == 0)
            throw new FacebookMessengerConfigurationException("MESSENGER_APP_SECRET_REQUIRED");
        if (_pages.Count == 0)
            throw new FacebookMessengerConfigurationException("MESSENGER_PAGE_REQUIRED");
    }

    internal static bool IsValidApiVersion(string apiVersion)
    {
        if (!apiVersion.StartsWith('v')) return false;
        var numericParts = apiVersion[1..].Split('.', StringSplitOptions.None);
        return numericParts.Length == 2 && numericParts.All(part =>
            part.Length > 0 && part.All(char.IsAsciiDigit));
    }

    private static string Required(IConfigurationSection section, string key)
    {
        var configuredValue = section[key]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredValue))
            throw new FacebookMessengerConfigurationException($"MESSENGER_{key.ToUpperInvariant()}_REQUIRED");
        return configuredValue;
    }
}

public sealed class FacebookMessengerConfigurationException(string errorCode)
    : Exception("Facebook Messenger configuration is invalid.")
{
    public string ErrorCode { get; } = errorCode;
}
