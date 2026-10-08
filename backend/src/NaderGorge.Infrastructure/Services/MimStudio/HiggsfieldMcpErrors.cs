using System.Text.Json;
using System.Text.RegularExpressions;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public static class HiggsfieldMcpErrors
{
    public static HiggsfieldMcpException Rejection(JsonElement reply)
    {
        var messages = new List<string>();
        if (reply.TryGetProperty("structuredContent", out var structured)) Collect(structured, messages, 0);
        if (messages.Count == 0 && reply.TryGetProperty("error", out var error)) Collect(error, messages, 0);
        if (messages.Count == 0 && reply.TryGetProperty("content", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            foreach (var block in blocks.EnumerateArray())
                if (block.ValueKind == JsonValueKind.Object && block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    CollectText(text.GetString()!, messages);
        var description = Redact(string.Join(" | ", messages.Distinct()));
        return new HiggsfieldMcpException(string.IsNullOrWhiteSpace(description)
            ? "Higgsfield رفض الطلب بدون توضيح السبب. راجع سجل التوليد والرصيد في حسابك."
            : "Higgsfield رفض الطلب. تفاصيل الرد: " + description);
    }

    private static void Collect(JsonElement node, List<string> messages, int depth)
    {
        if (depth > 3) return;
        if (node.ValueKind == JsonValueKind.String) { messages.Add(node.GetString()!); return; }
        if (node.ValueKind != JsonValueKind.Object) return;
        // Never include prompts, media, request headers, or arbitrary provider payload fields.
        foreach (var key in new[] { "code", "message", "detail", "error", "reason", "data" })
            if (node.TryGetProperty(key, out var field)) Collect(field, messages, depth + 1);
    }

    private static void CollectText(string text, List<string> messages)
    {
        try { using var document = JsonDocument.Parse(text); Collect(document.RootElement, messages, 0); }
        catch (JsonException) { messages.Add(text); }
    }

    private static string Redact(string message)
    {
        if (message.Length > 32_000) return "رد الرفض طويل؛ راجع تفاصيله في حساب Higgsfield.";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        message = Regex.Replace(message, @"https?://[^\s""<>]+", "[رابط محجوب]", options);
        message = Regex.Replace(message, @"\bBearer\s+[^\s""',;]+|\beyJ[A-Za-z0-9_.-]+", "[محجوب]", options);
        message = Regex.Replace(message, @"[""']?(?:access[_-]?token|refresh[_-]?token|api[_-]?key|authorization|cookie|password|secret)[""']?\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;}]+)", "[بيانات محجوبة]", options);
        message = Regex.Replace(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", "[بريد محجوب]", options);
        message = Regex.Replace(message, @"\s+", " ").Trim();
        return message.Length <= 1500 ? message : message[..1500] + "…";
    }
}
