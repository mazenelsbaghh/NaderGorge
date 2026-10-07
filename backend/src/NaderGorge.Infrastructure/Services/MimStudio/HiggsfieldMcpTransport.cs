using System.Net.Http.Json;
using System.Text.Json;

namespace NaderGorge.Infrastructure.Services.MimStudio;

// The official server is fixed; no user-supplied hosts or arbitrary tool calls are exposed.
public sealed class HiggsfieldMcpTransport(HttpClient client, string accessToken)
{
    private string protocol = "2025-11-25";
    private string? sessionId;

    public async Task InitializeAsync(CancellationToken ct)
    {
        var result = await RequestAsync("initialize", new
        {
            protocolVersion = protocol, capabilities = new { },
            clientInfo = new { name = "massar-meem-studio", version = "1.0.0" }
        }, ct);
        if (!result.TryGetProperty("protocolVersion", out var version) ||
            version.GetString() is not ("2025-11-25" or "2025-06-18" or "2025-03-26"))
            throw new HiggsfieldMcpException("إصدار اتصال Higgsfield غير مدعوم في الاستوديو بعد.");
        protocol = version.GetString()!;
        using var request = Message(new { jsonrpc = "2.0", method = "notifications/initialized" });
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new HiggsfieldMcpException("تعذر تهيئة اتصال Higgsfield.");
    }

    public async Task<JsonElement> RequestAsync(string method, object arguments, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        using var request = Message(new { jsonrpc = "2.0", id, method, @params = arguments });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new HiggsfieldMcpException("Higgsfield طلب تسجيل الدخول من جديد.");
            if (!response.IsSuccessStatusCode) throw new HiggsfieldMcpException("تعذر قراءة أدوات Higgsfield.");
            if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessions)) sessionId = sessions.Single();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(40));
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            {
                using var reader = new StreamReader(stream);
                var data = new System.Text.StringBuilder();
                var size = 0;
                while (await reader.ReadLineAsync(deadline.Token) is { } line)
                {
                    size += line.Length;
                    if (size > 1_000_000) throw new HiggsfieldMcpException("رد Higgsfield أكبر من المسموح.");
                    if (line.StartsWith("data:", StringComparison.Ordinal)) data.AppendLine(line[5..].TrimStart());
                    if (line.Length == 0 && data.Length > 0)
                    {
                        using var message = JsonDocument.Parse(data.ToString()); data.Clear();
                        if (Matches(message.RootElement, id)) return Result(message.RootElement);
                    }
                }
                throw new HiggsfieldMcpException("انتهى اتصال Higgsfield قبل اكتمال الرد.");
            }
            using var buffer = new MemoryStream();
            var bytes = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(bytes, deadline.Token)) > 0)
            {
                if (buffer.Length + count > 1_000_000) throw new HiggsfieldMcpException("رد Higgsfield أكبر من المسموح.");
                buffer.Write(bytes, 0, count);
            }
            using var json = JsonDocument.Parse(buffer.ToArray());
            if (!Matches(json.RootElement, id)) throw new HiggsfieldMcpException("رد Higgsfield لا يطابق الطلب.");
            return Result(json.RootElement);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new HiggsfieldMcpException("تعذر الاتصال بـHiggsfield. حاول لاحقًا."); }
    }

    private HttpRequestMessage Message(object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, HiggsfieldMcpConnectionService.Endpoint) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", protocol);
        if (sessionId is not null) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        return request;
    }
    private static bool Matches(JsonElement json, string id) => json.TryGetProperty("id", out var found) && found.ValueKind == JsonValueKind.String && found.GetString() == id;
    private static JsonElement Result(JsonElement json) => json.TryGetProperty("result", out var result) ? result.Clone() :
        throw new HiggsfieldMcpException("Higgsfield لم يتمكن من تنفيذ طلب الاتصال.");
}
