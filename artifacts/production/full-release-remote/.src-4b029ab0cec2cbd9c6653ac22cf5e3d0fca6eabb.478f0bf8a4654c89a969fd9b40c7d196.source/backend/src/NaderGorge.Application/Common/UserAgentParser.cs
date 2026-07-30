namespace NaderGorge.Application.Common;

/// <summary>
/// Lightweight User-Agent parser — no external dependencies.
/// Detects OS, Browser, and Device Type from the UA string.
/// </summary>
public static class UserAgentParser
{
    public static (string Os, string Browser, string DeviceType) Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return ("Unknown", "Unknown", "Desktop");

        var ua = userAgent.ToLowerInvariant();

        // ── Device Type ────────────────────────────────────────────────
        string deviceType;
        if (ua.Contains("ipad") || ua.Contains("tablet") ||
            (ua.Contains("android") && !ua.Contains("mobile")))
            deviceType = "Tablet";
        else if (ua.Contains("mobile") || ua.Contains("iphone") ||
                 ua.Contains("ipod") || ua.Contains("blackberry") ||
                 ua.Contains("windows phone"))
            deviceType = "Mobile";
        else
            deviceType = "Desktop";

        // ── OS ─────────────────────────────────────────────────────────
        string os;
        if (ua.Contains("windows nt"))
        {
            if (ua.Contains("windows nt 10.0") || ua.Contains("windows nt 11.0"))
                os = "Windows 10/11";
            else if (ua.Contains("windows nt 6.3"))
                os = "Windows 8.1";
            else if (ua.Contains("windows nt 6.2"))
                os = "Windows 8";
            else if (ua.Contains("windows nt 6.1"))
                os = "Windows 7";
            else
                os = "Windows";
        }
        else if (ua.Contains("iphone") || ua.Contains("ipod"))
            os = "iOS";
        else if (ua.Contains("ipad"))
            os = "iPadOS";
        else if (ua.Contains("android"))
            os = "Android";
        else if (ua.Contains("mac os x") || ua.Contains("macos"))
            os = "macOS";
        else if (ua.Contains("linux"))
            os = "Linux";
        else if (ua.Contains("cros"))
            os = "ChromeOS";
        else
            os = "Unknown";

        // ── Browser ────────────────────────────────────────────────────
        string browser;
        if (ua.Contains("edg/") || ua.Contains("edge/"))
            browser = "Edge";
        else if (ua.Contains("opr/") || ua.Contains("opera"))
            browser = "Opera";
        else if (ua.Contains("samsungbrowser"))
            browser = "Samsung Internet";
        else if (ua.Contains("ucbrowser"))
            browser = "UC Browser";
        else if (ua.Contains("firefox/"))
            browser = "Firefox";
        else if (ua.Contains("chrome/") && !ua.Contains("chromium"))
            browser = "Chrome";
        else if (ua.Contains("chromium"))
            browser = "Chromium";
        else if (ua.Contains("safari/") && !ua.Contains("chrome"))
            browser = "Safari";
        else
            browser = "Unknown";

        return (os, browser, deviceType);
    }
}
