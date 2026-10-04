using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NaderGorge.API.CenterDesktop;
using NaderGorge.API.Controllers;

namespace NaderGorge.Integration.Tests.CenterDesktop;

public sealed class CenterDesktopSupportTests
{
    private const string UploadId = "ecda8818-7c72-4f9a-9468-7b64845473c9";
    private const string PrivateValue = "synthetic-private-payload-must-not-leak";
    private static readonly string[] Routes = ["status", "uploads", $"uploads/{UploadId}/diagnostics", $"uploads/{UploadId}/download", "releases"];

    [Theory]
    [InlineData(null, 401)]
    [InlineData("Student", 403)]
    [InlineData("Assistant", 403)]
    [InlineData("Supervisor", 403)]
    [InlineData("Staff", 403)]
    public async Task EveryRouteRejectsNonAdminBeforeContactingPrivateSupport(string? role, int status)
    {
        using var fixture = new Fixture((_, _) => throw new Xunit.Sdk.XunitException("Unauthorized request reached support."));
        using var client = fixture.Client(role);
        foreach (var route in Routes.Append($"uploads/{UploadId}/students?q=test"))
        {
            using var response = await client.GetAsync($"/api/admin/center-desktop/{route}");
            Assert.Equal(status, (int)response.StatusCode);
        }
        Assert.Equal(0, fixture.Handler.Requests);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 400)]
    public async Task StudentSearchReadsOnlyDatabaseBundles(bool database, int expected)
    {
        using var fixture = new Fixture((_, _) => Task.FromResult(Json(new {
            kind = database ? "database" : "diagnostics",
            data = new { data = new { students = new[] { new { id = "s1", name = "طالب", code = "00123", secret = PrivateValue } }, credentials = PrivateValue } }
        })));
        using var client = fixture.Client("Admin");
        using var response = await client.GetAsync($"/api/admin/center-desktop/uploads/{UploadId}/students?q=00123");
        Assert.Equal(expected, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(PrivateValue, body);
        if (database) Assert.Contains("00123", body);
    }

    [Fact]
    public async Task AdminMetadataUsesFixedOriginAndWhitelistedDtosWhileDownloadStreamsSeparateBundle()
    {
        using var fixture = new Fixture((request, _) =>
        {
            Assert.Equal("https://desktop-support.invalid", request.RequestUri!.GetLeftPart(UriPartial.Authority));
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal(Fixture.AdminToken, request.Headers.Authorization.Parameter);
            var path = request.RequestUri.AbsolutePath;
            object body = path switch
            {
                "/v1/uploads" => new { uploads = new[] { Receipt() }, nextCursor = "", data = PrivateValue },
                $"/v1/uploads/{UploadId}/diagnostics" => new { receipt = Receipt(), kind = "database", events = new[] { new
                { schema = 1, kind = "error", id = Guid.NewGuid(), session = Guid.NewGuid(), time = "2026-10-03T12:00:00Z",
                  version = "1.0.0", platform = "windows", operation = "cloud.upload", errors = new[] { new { type = "SocketException", code = 1, message = PrivateValue } },
                  frames = Array.Empty<object>(), data = PrivateValue, message = PrivateValue } }, total = 1, truncated = false, data = PrivateValue },
                "/v1/admin/releases" => new { releases = new[]
                {
                    new { platform = "windows-x64", role = "host", status = "available", manifest = (object?)new
                    { releaseId = "test-release", version = "1.0.0", build = "1234567812345678", platform = "windows-x64", role = "host",
                      size = 1234L, sha256 = new string('a', 64), downloadPath = "/v1/releases/host.zip", notes = "Synthetic release" } },
                    new { platform = "windows-x64", role = "client", status = "missing", manifest = (object?)null },
                    new { platform = "macos-arm64", role = "host", status = "invalid", manifest = (object?)null },
                    new { platform = "macos-arm64", role = "client", status = "missing", manifest = (object?)null }
                } },
                $"/v1/uploads/{UploadId}" => new { data = new { synthetic = PrivateValue } },
                _ => throw new Xunit.Sdk.XunitException("Unexpected upstream path")
            };
            return Task.FromResult(Json(body));
        });
        using var client = fixture.Client("Admin");
        foreach (var route in Routes.Where(r => !r.EndsWith("/download")))
        {
            using var response = await client.GetAsync($"/api/admin/center-desktop/{route}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(PrivateValue, body);
            using var json = JsonDocument.Parse(body);
            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            var data = json.RootElement.GetProperty("data");
            if (route == "status") Assert.True(data.GetProperty("available").GetBoolean());
            if (route == "uploads") Assert.Equal(UploadId, data.GetProperty("uploads")[0].GetProperty("uploadId").GetString());
            if (route == "releases") Assert.Equal("available", data.GetProperty("releases")[0].GetProperty("status").GetString());
            if (route.EndsWith("diagnostics")) Assert.Equal("SocketException", data.GetProperty("events")[0].GetProperty("errors")[0].GetProperty("type").GetString());
        }
        using var download = await client.GetAsync($"/api/admin/center-desktop/uploads/{UploadId}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.True(download.Headers.CacheControl!.NoStore);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("application/json", download.Content.Headers.ContentType!.MediaType);
        Assert.Contains(PrivateValue, await download.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("uploads?limit=0")]
    [InlineData("uploads?limit=501")]
    [InlineData("uploads?after=https://unexpected.invalid")]
    [InlineData("uploads/not-a-uuid/diagnostics")]
    [InlineData("uploads/ecda8818-7c72-1f9a-9468-7b64845473c9/download")]
    [InlineData("uploads/00000000-0000-0000-0000-000000000000/download")]
    [InlineData("uploads/ecda8818-7c72-4f9a-9468-7b64845473c9/diagnostics?limit=-1")]
    public async Task InvalidQueriesCannotSelectArbitraryUpstreamOrDownload(string route)
    {
        using var fixture = new Fixture((_, _) => throw new Xunit.Sdk.XunitException("Invalid input reached upstream."));
        using var client = fixture.Client("Admin");
        using var response = await client.GetAsync($"/api/admin/center-desktop/{route}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.Handler.Requests);
    }

    [Theory]
    [InlineData(302, "application/json", "{}")]
    [InlineData(401, "application/json", "{\"token\":\"secret\"}")]
    [InlineData(500, "application/json", "{\"path\":\"private\"}")]
    [InlineData(200, "text/html", "private")]
    [InlineData(200, "application/json", "not-json")]
    [InlineData(200, "application/json", "{\"uploads\":null,\"nextCursor\":\"\"}")]
    public async Task InvalidUpstreamRepliesReturnGenericFailureWithoutPrivateBody(int status, string type, string body)
    {
        using var fixture = new Fixture((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent(body + "\n", Encoding.UTF8, type) }));
        using var client = fixture.Client("Admin");
        using var response = await client.GetAsync("/api/admin/center-desktop/uploads");
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var actual = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", actual);
        Assert.DoesNotContain("secret", actual);
        Assert.DoesNotContain("private", actual);
        Assert.Equal(1, fixture.Handler.Requests);
    }

    [Theory]
    [InlineData("uploads", "{\"uploads\":[null],\"nextCursor\":\"\"}")]
    [InlineData("releases", "{\"releases\":[null,null,null,null]}")]
    public async Task NullMetadataEntriesFailClosedInsteadOfThrowingInternalErrors(string route, string body)
    {
        using var fixture = new Fixture((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        using var client = fixture.Client("Admin");
        using var response = await client.GetAsync($"/api/admin/center-desktop/{route}");
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task MissingConfigurationIsExplicitAndDoesNotContactNetwork()
    {
        using var fixture = new Fixture((_, _) => throw new Xunit.Sdk.XunitException("Unconfigured request reached upstream."), configured: false);
        using var client = fixture.Client("Admin");
        using var status = await client.GetAsync("/api/admin/center-desktop/status");
        using var value = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.False(value.RootElement.GetProperty("data").GetProperty("configured").GetBoolean());
        Assert.False(value.RootElement.GetProperty("data").GetProperty("available").GetBoolean());
        using var list = await client.GetAsync("/api/admin/center-desktop/uploads");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, list.StatusCode);
        Assert.Equal(0, fixture.Handler.Requests);
    }

    [Theory]
    [InlineData("http://support.invalid", "synthetic-admin-token-0123456789")]
    [InlineData("https://support.invalid/path", "synthetic-admin-token-0123456789")]
    [InlineData("https://user:password@support.invalid", "synthetic-admin-token-0123456789")]
    [InlineData("https://support.invalid?target=other", "synthetic-admin-token-0123456789")]
    [InlineData("https://support.invalid", "invalid token with spaces")]
    public async Task UnsafeServerConfigurationStaysUnconfigured(string origin, string token)
    {
        using var boundary = new Boundary((_, _) => throw new Xunit.Sdk.XunitException("Unsafe config reached upstream."));
        using var http = new HttpClient(boundary);
        var client = new CenterDesktopSupportClient(http, Options.Create(new CenterDesktopSupportOptions { BaseUrl = origin, AdminToken = token }));
        var status = await client.StatusAsync(CancellationToken.None);
        Assert.False(status.Configured);
        Assert.False(status.Available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedMetadataIsRejectedWithAndWithoutDeclaredLength(bool declared)
    {
        using var boundary = new Boundary((_, _) =>
        {
            HttpContent content = declared ? new StringContent("{}") : new StreamContent(new MemoryStream(new byte[8 * 1024 * 1024 + 1]));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            if (declared) content.Headers.ContentLength = 8 * 1024 * 1024 + 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var http = new HttpClient(boundary);
        var client = Support(http);
        var error = await Assert.ThrowsAsync<DesktopSupportException>(() => client.UploadsAsync(50, null, CancellationToken.None));
        Assert.Equal(502, error.StatusCode);
    }

    [Fact]
    public async Task UppercaseUuidIsNormalizedBeforeServiceLookup()
    {
        using var fixture = new Fixture((request, _) =>
        {
            Assert.Equal($"/v1/uploads/{UploadId}", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json(new { synthetic = "private download" }));
        });
        using var client = fixture.Client("Admin");
        using var response = await client.GetAsync($"/api/admin/center-desktop/uploads/{UploadId.ToUpperInvariant()}/download");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("private download", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RequestCancellationReachesUpstreamReadWithoutBecomingPublicSupportFailure()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var boundary = new Boundary(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new Xunit.Sdk.XunitException("Cancellation did not interrupt upstream.");
        });
        using var http = new HttpClient(boundary);
        using var cancellation = new CancellationTokenSource();
        var read = Support(http).UploadsAsync(50, null, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    }

    private static CenterDesktopSupportClient Support(HttpClient http) => new(http, Options.Create(new CenterDesktopSupportOptions
    { BaseUrl = "https://desktop-support.invalid", AdminToken = Fixture.AdminToken }));
    private static object Receipt() => new { receiptId = "c252861d-6176-460c-b1aa-e3a52f9b0c69", uploadId = UploadId,
        centerId = "synthetic-center", sha256 = new string('a', 64), bundleSha256 = new string('b', 64),
        createdAt = "2026-10-03T11:00:00Z", receivedAt = "2026-10-03T12:00:00Z", size = 1234L,
        app = new { version = "1.0.0", build = "1234567812345678", role = "host", os = "windows" } };
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };

    private sealed class Boundary(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Requests++; return response(request, token); }
    }

    // Real MVC routing, JWT authentication, and role authorization; only the external support HTTP boundary is replaced.
    private sealed class Fixture : IDisposable
    {
        public const string AdminToken = "synthetic-admin-token-0123456789";
        private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("synthetic-test-signing-key-with-at-least-sixty-four-safe-characters"));
        private readonly TestServer _server;
        public Boundary Handler { get; }
        public Fixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response, bool configured = true)
        {
            Handler = new Boundary(response);
            _server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                    options.TokenValidationParameters = new TokenValidationParameters
                    { ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                      IssuerSigningKey = Key, RoleClaimType = ClaimTypes.Role });
                services.AddAuthorization();
                services.AddControllers().AddApplicationPart(typeof(AdminCenterDesktopController).Assembly)
                    .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new OnlyDesktopController()));
                services.Configure<CenterDesktopSupportOptions>(options =>
                { options.BaseUrl = configured ? "https://desktop-support.invalid" : ""; options.AdminToken = configured ? AdminToken : ""; });
                services.AddTransient(provider => new CenterDesktopSupportClient(new HttpClient(Handler, disposeHandler: false),
                    provider.GetRequiredService<IOptions<CenterDesktopSupportOptions>>()));
            }).Configure(app =>
            { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(endpoints => endpoints.MapControllers()); }));
        }
        public HttpClient Client(string? role)
        {
            var client = _server.CreateClient();
            if (role is not null)
            {
                var token = new JwtSecurityToken(claims: [new Claim(ClaimTypes.Role, role)], expires: DateTime.UtcNow.AddMinutes(5),
                    signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            }
            return client;
        }
        public void Dispose() { _server.Dispose(); Handler.Dispose(); }
    }

    private sealed class OnlyDesktopController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var controller in feature.Controllers.Where(type => type.AsType() != typeof(AdminCenterDesktopController)).ToArray())
                feature.Controllers.Remove(controller);
        }
    }
}
