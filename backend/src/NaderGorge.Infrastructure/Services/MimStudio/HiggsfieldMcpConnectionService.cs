using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public sealed record HiggsfieldOAuthCallback(string Code, string State, string? Issuer);
public sealed record HiggsfieldConnectionStatus(bool Connected, bool Configured, string Endpoint);
public sealed record HiggsfieldTool(string Name, string Description, JsonElement InputSchema);
public sealed class HiggsfieldMcpException(string message) : Exception(message);

public sealed class HiggsfieldMcpConnectionService(
    AppDbContext db, IHttpClientFactory clients, IDataProtectionProvider protection, IConfiguration configuration)
{
    public const string Endpoint = "https://mcp.higgsfield.ai/mcp";
    private const string Issuer = "https://clerk.higgsfield.ai";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record Session(string? AccessToken = null, string? RefreshToken = null, DateTimeOffset? Expires = null,
        string? State = null, string? Verifier = null, DateTimeOffset? LoginExpires = null, string? RedirectUri = null);

    public async Task<HiggsfieldConnectionStatus> StatusAsync(Guid actor, CancellationToken ct)
    {
        var row = await FindAsync(actor, ct);
        var session = row is null ? null : Read(row);
        return new(session?.AccessToken is not null && (session.Expires > DateTimeOffset.UtcNow || session.RefreshToken is not null),
            RedirectUri() is not null, Endpoint);
    }

    public async Task<object> StartAsync(Guid actor, CancellationToken ct)
    {
        var redirect = RedirectUri() ?? throw new HiggsfieldMcpException("رابط الرجوع من Higgsfield محتاج يتضبط في إعدادات السيرفر أولاً.");
        var row = await FindAsync(actor, ct);
        if (row is null) { row = new HiggsfieldMcpConnection { AdminUserId = actor }; db.Add(row); }
        if (string.IsNullOrEmpty(row.ClientId))
        {
            var configuredClient = configuration["HiggsfieldMcp:ClientId"];
            if (!string.IsNullOrWhiteSpace(configuredClient)) row.ClientId = configuredClient;
            else
            {
                using var reply = await PostAsync("/oauth/register", JsonContent.Create(new
                {
                    client_name = "Massar Meem Studio", redirect_uris = new[] { redirect },
                    grant_types = new[] { "authorization_code", "refresh_token" }, response_types = new[] { "code" },
                    token_endpoint_auth_method = "none", scope = "openid email offline_access"
                }), ct);
                row.ClientId = RequiredString(reply.RootElement, "client_id");
                if (row.ClientId.Length > 2048) throw new HiggsfieldMcpException("تعذر تسجيل اتصال Higgsfield.");
            }
        }
        var state = RandomSecret();
        var verifier = RandomSecret();
        var previous = string.IsNullOrEmpty(row.ProtectedSession) ? new Session() : Read(row);
        Write(row, previous with { State = state, Verifier = verifier, LoginExpires = DateTimeOffset.UtcNow.AddMinutes(10), RedirectUri = redirect });
        await db.SaveChangesAsync(ct);
        var values = new Dictionary<string, string>
        {
            ["client_id"] = row.ClientId, ["redirect_uri"] = redirect, ["response_type"] = "code",
            ["scope"] = "openid email offline_access", ["state"] = state, ["resource"] = Endpoint,
            ["code_challenge_method"] = "S256", ["code_challenge"] = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
        };
        return new { authorizationUrl = Issuer + "/oauth/authorize?" + string.Join('&', values.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value))) };
    }

    public async Task<HiggsfieldConnectionStatus> CompleteAsync(Guid actor, HiggsfieldOAuthCallback request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 4096 || string.IsNullOrWhiteSpace(request.State) ||
            request.State.Length > 256 || (request.Issuer is not null && request.Issuer.TrimEnd('/') != Issuer))
            throw new ArgumentException("بيانات ربط Higgsfield غير صالحة.");
        var row = await FindAsync(actor, ct) ?? throw new ArgumentException("ابدأ ربط الحساب من الاستوديو أولاً.");
        var session = Read(row);
        if (session.State is null || session.LoginExpires <= DateTimeOffset.UtcNow || session.Verifier is null || session.RedirectUri is null ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(session.State), Encoding.UTF8.GetBytes(request.State)))
            throw new ArgumentException("انتهت جلسة الربط أو لا تخص حسابك. ابدأ الربط من جديد.");
        // Consume the one-time state before exchanging a code, including across application nodes.
        Write(row, session with { State = null, Verifier = null, LoginExpires = null, RedirectUri = null });
        await db.SaveChangesAsync(ct);
        using var token = await PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = request.Code, ["client_id"] = row.ClientId,
            ["redirect_uri"] = session.RedirectUri, ["code_verifier"] = session.Verifier, ["resource"] = Endpoint
        }), ct);
        Write(row, Tokens(token.RootElement));
        await db.SaveChangesAsync(ct);
        return await StatusAsync(actor, ct);
    }

    public async Task<bool> DisconnectAsync(Guid actor, CancellationToken ct)
    {
        var row = await FindAsync(actor, ct);
        if (row is null) return true;
        // Removing the encrypted local grant immediately prevents future calls from this platform.
        db.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<HiggsfieldTool>> DiscoverAsync(Guid actor, CancellationToken ct)
    {
        var token = await AccessTokenAsync(actor, ct);
        using var client = clients.CreateClient("HiggsfieldMcp");
        var transport = new HiggsfieldMcpTransport(client, token);
        await transport.InitializeAsync(ct);
        var result = new List<HiggsfieldTool>();
        string? cursor = null;
        for (var page = 0; page < 10; page++)
        {
            var reply = await transport.RequestAsync("tools/list", cursor is null ? new { } : (object)new { cursor }, ct);
            if (!reply.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array)
                throw new HiggsfieldMcpException("Higgsfield لم يُرجع قائمة أدوات صالحة.");
            foreach (var tool in tools.EnumerateArray())
            {
                if (!tool.TryGetProperty("inputSchema", out var schema)) continue;
                result.Add(new(RequiredString(tool, "name"), tool.TryGetProperty("description", out var description) ? description.GetString() ?? "" : "", schema.Clone()));
            }
            cursor = reply.TryGetProperty("nextCursor", out var next) ? next.GetString() : null;
            if (string.IsNullOrEmpty(cursor)) return result;
        }
        throw new HiggsfieldMcpException("قائمة أدوات Higgsfield أكبر من الحد المسموح. حاول لاحقًا.");
    }

    internal async Task<JsonElement> CallStudioToolAsync(Guid actor, string name, object arguments, CancellationToken ct)
    {
        if (name is not ("models_explore" or "media_import_url" or "generate_video" or "job_status"))
            throw new ArgumentException("أداة غير مسموحة لاستوديو ميم.");
        using var client = clients.CreateClient("HiggsfieldMcp");
        var transport = new HiggsfieldMcpTransport(client, await AccessTokenAsync(actor, ct));
        await transport.InitializeAsync(ct);
        var reply = await transport.RequestAsync("tools/call", new { name, arguments }, ct);
        if (reply.TryGetProperty("isError", out var error) && error.ValueKind == JsonValueKind.True)
            throw HiggsfieldMcpErrors.Rejection(reply);
        return reply;
    }

    private async Task<string> AccessTokenAsync(Guid actor, CancellationToken ct)
    {
        var row = await FindAsync(actor, ct) ?? throw new HiggsfieldMcpException("اربط حساب Higgsfield أولاً.");
        var session = Read(row);
        if (session.AccessToken is not null && session.Expires > DateTimeOffset.UtcNow.AddMinutes(1)) return session.AccessToken;
        if (session.RefreshToken is null) throw new HiggsfieldMcpException("جلسة Higgsfield انتهت. اربط الحساب من جديد.");
        // Claim renewal durably; concurrent refreshes must not reuse a rotating refresh token.
        Write(row, session with { RefreshToken = null });
        await db.SaveChangesAsync(ct);
        using var response = await PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = session.RefreshToken,
            ["client_id"] = row.ClientId, ["resource"] = Endpoint
        }), ct);
        var refreshed = Tokens(response.RootElement);
        Write(row, refreshed with { RefreshToken = refreshed.RefreshToken ?? session.RefreshToken });
        await db.SaveChangesAsync(ct);
        return refreshed.AccessToken!;
    }

    private Task<HiggsfieldMcpConnection?> FindAsync(Guid actor, CancellationToken ct) =>
        db.Set<HiggsfieldMcpConnection>().SingleOrDefaultAsync(x => x.AdminUserId == actor, ct);
    private IDataProtector Protector(Guid actor) => protection.CreateProtector("Massar.HiggsfieldMcp.v1", actor.ToString("N"));
    private Session Read(HiggsfieldMcpConnection row)
    {
        try { return JsonSerializer.Deserialize<Session>(Protector(row.AdminUserId).Unprotect(row.ProtectedSession), JsonOptions)!; }
        catch (Exception error) when (error is CryptographicException or JsonException)
        { throw new HiggsfieldMcpException("تعذر فتح جلسة Higgsfield المحفوظة. افصل الربط واربط الحساب من جديد."); }
    }
    private void Write(HiggsfieldMcpConnection row, Session session)
    {
        row.ProtectedSession = Protector(row.AdminUserId).Protect(JsonSerializer.Serialize(session, JsonOptions));
        row.Version = Guid.NewGuid(); row.UpdatedAt = DateTime.UtcNow;
    }
    private string? RedirectUri()
    {
        var value = configuration["HiggsfieldMcp:RedirectUri"];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
            uri.AbsolutePath != "/admin/mim-studio/connect") return null;
        return uri.AbsoluteUri;
    }
    private async Task<JsonDocument> PostAsync(string path, HttpContent body, CancellationToken ct)
    {
        using var client = clients.CreateClient("HiggsfieldMcp");
        try
        {
            using var response = await client.PostAsync(Issuer + path, body, ct);
            if (!response.IsSuccessStatusCode) throw new HiggsfieldMcpException("تعذر إكمال ربط Higgsfield. حاول الربط من جديد.");
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is TaskCanceledException && !ct.IsCancellationRequested)
        { throw new HiggsfieldMcpException("تعذر الاتصال بـHiggsfield. حاول لاحقًا."); }
    }
    private static Session Tokens(JsonElement value) => new(RequiredString(value, "access_token"),
        value.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null,
        DateTimeOffset.UtcNow.AddSeconds(value.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds) ? Math.Clamp(seconds, 1, 86400) : 300));
    private static string RequiredString(JsonElement json, string key) =>
        json.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString())
            ? value.GetString()! : throw new HiggsfieldMcpException("رد Higgsfield غير مكتمل.");
    private static string RandomSecret() => Base64Url(RandomNumberGenerator.GetBytes(32));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
