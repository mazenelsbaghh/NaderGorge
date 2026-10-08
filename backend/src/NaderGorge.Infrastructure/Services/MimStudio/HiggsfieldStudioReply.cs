using System.Text.Json;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public static class HiggsfieldStudioReply
{
    public static JsonElement Payload(JsonElement reply)
    {
        if (reply.TryGetProperty("structuredContent", out var structured) && structured.ValueKind == JsonValueKind.Object) return structured.Clone();
        if (reply.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            foreach (var block in content.EnumerateArray())
                if (block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    try { using var doc = JsonDocument.Parse(text.GetString()!); if (doc.RootElement.ValueKind == JsonValueKind.Object) return doc.RootElement.Clone(); }
                    catch (JsonException) { }
        throw new HiggsfieldMcpException("رد Higgsfield غير قابل للقراءة الآمنة. لم تتم إعادة إرسال الطلب.");
    }

    public static Guid RequiredId(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
            if (payload.TryGetProperty(name, out var id) && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out var parsed) && parsed != Guid.Empty)
                return parsed;
        throw new HiggsfieldMcpException("Higgsfield لم يرجع معرّف الطلب. راجع سجل الحساب قبل أي إعادة إرسال.");
    }

    public static string Quote(JsonElement payload)
    {
        if (payload.TryGetProperty("adjustments", out var adjustments) && adjustments.ValueKind switch
            { JsonValueKind.Array => adjustments.GetArrayLength() > 0, JsonValueKind.Object => adjustments.EnumerateObject().Any(), JsonValueKind.String => !string.IsNullOrWhiteSpace(adjustments.GetString()), _ => false })
            throw new HiggsfieldMcpException("Higgsfield اقترح تغيير إعدادات المشهد. لم يتم التوليد؛ يلزم مراجعة توافق المدة والمراجع.");
        // A numeric cost is required; an error, recommendation or credit-choice message cannot authorize spending.
        var cost = FindCost(payload, 0);
        if (cost is null) throw new HiggsfieldMcpException("لم تصل تكلفة واضحة من Higgsfield. لم يتم التوليد.");
        return $"{cost.Value:0.####} كريديت من رصيد Higgsfield";
    }

    public static Guid? LiteralPresetToDecline(JsonElement payload)
    {
        if (!payload.TryGetProperty("notice", out var notice) || notice.ValueKind != JsonValueKind.Object ||
            !notice.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "preset_recommendation" ||
            !notice.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("retry_literal_with", out var retry) || retry.ValueKind != JsonValueKind.Object ||
            !retry.TryGetProperty("declined_preset_id", out var id) || id.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(id.GetString(), out var preset) || preset == Guid.Empty) return null;
        return preset;
    }

    private static decimal? FindCost(JsonElement node, int depth)
    {
        if (depth > 3 || node.ValueKind != JsonValueKind.Object) return null;
        foreach (var key in new[] { "total_credits", "cost_credits", "total_cost", "cost", "credits" })
            if (node.TryGetProperty(key, out var cost) && cost.ValueKind == JsonValueKind.Number && cost.TryGetDecimal(out var amount) && amount >= 0) return amount;
        foreach (var key in new[] { "data", "cost", "estimate", "pricing" })
            if (node.TryGetProperty(key, out var nested) && FindCost(nested, depth + 1) is decimal amount) return amount;
        return null;
    }

    public static (string State, string[] Urls) Job(JsonElement payload)
    {
        if (payload.TryGetProperty("generation", out var generation)) payload = generation;
        var status = payload.TryGetProperty("status", out var raw) && raw.ValueKind == JsonValueKind.String ? raw.GetString() : null;
        var state = status switch { "completed" or "succeeded" => "completed", "failed" or "cancelled" or "canceled" => "failed", "ip_detected" => "review_required", _ => "running" };
        var urls = new List<string>();
        if (payload.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            foreach (var result in results.EnumerateArray())
            {
                string? url = result.ValueKind == JsonValueKind.String ? result.GetString() :
                    result.ValueKind == JsonValueKind.Object && result.TryGetProperty("url", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)) urls.Add(uri.AbsoluteUri);
            }
        if (state == "completed" && urls.Count == 0) state = "review_required";
        return (state, urls.ToArray());
    }
}
