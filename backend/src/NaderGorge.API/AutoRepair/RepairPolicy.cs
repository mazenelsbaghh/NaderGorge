using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NaderGorge.API.AutoRepair;

public static class RepairPolicy
{
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["diagnosing"] = ["repairing", "failed", "needs_evidence", "awaiting_approval"],
        ["repairing"] = ["testing", "failed", "needs_evidence", "awaiting_approval"],
        ["testing"] = ["ready", "failed", "needs_evidence", "awaiting_approval"],
        ["ready"] = ["deploying", "failed", "awaiting_approval"],
        ["deploying"] = ["monitoring", "failed", "rolled_back"],
        ["monitoring"] = ["completed", "failed", "rolled_back"],
    };

    public static bool CanTransition(string current, string next) =>
        Transitions.TryGetValue(current, out var allowed) && allowed.Contains(next);

    public static string Redact(string text)
    {
        var bounded = text[..Math.Min(text.Length, 12_000)];
        bounded = Regex.Replace(bounded, @"(?i)(token|secret|password|authorization|cookie|api[_-]?key)\s*[""']?\s*[:=]\s*[""']?[^\r\n,;]+", "$1=[redacted]");
        bounded = Regex.Replace(bounded, @"https?://\S+|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", "[redacted]");
        bounded = Regex.Replace(bounded, @"\b(?:\d{1,3}\.){3}\d{1,3}\b", "[redacted-ip]");
        return Regex.Replace(bounded, @"\b(?:\+?20)?01[0125]\d{8}\b", "[redacted-phone]");
    }

    public static string Fingerprint(string source, string category, string evidence)
    {
        var stable = Regex.Replace(evidence, @"(?i)\b[0-9a-f]{32}\b|\b\d{4}-\d{2}-\d{2}[T ][\d:.]+Z?\b|\[\d{2}/[A-Za-z]{3}/\d{4}:[^\]]+\]", "#");
        stable = Regex.Replace(stable, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F-]{27,}\b|\b\d{5,}\b|(?i)\b(?:record|id|line)\s+\d+", "#");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{source}|{category}|{stable}")));
    }
}
