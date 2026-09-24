using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using Xunit;

public class AdminEmthntakControllerTests
{
    [Fact]
    public async Task StaffWithoutDedicatedPermissionsCannotForward()
    {
        var controller = Controller([], new ConfigurationBuilder().Build(), new ClientFactory(new HttpClient()));
        Assert.IsType<ForbidResult>(await controller.ForwardRequest(new("me", "GET", null), default));
    }

    [Fact]
    public async Task ForwardedEnvelopeBindsCurrentStaffIdentityAndOnlyGrantedCapabilities()
    {
        var keyFile = Path.GetTempFileName();
        const string key = "test-only-bridge-key-with-at-least-32-bytes";
        await File.WriteAllTextAsync(keyFile, key);
        try {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["EmthntakAdmin:ApiUrl"] = "http://127.0.0.1:8080/api/massar",
                ["EmthntakAdmin:KeyFile"] = keyFile
            }).Build();
            using var handler = new RecordingHandler();
            using var client = new HttpClient(handler);
            var controller = Controller([new("permission", "emthntak.finance"), new("permission", "content.manage")], configuration, new ClientFactory(client));
            var response = Assert.IsType<ContentResult>(await controller.ForwardRequest(new("v3/admin/overview", "GET", null), default));
            Assert.Equal(200, response.StatusCode);
            using var body = JsonDocument.Parse(handler.Body!);
            var envelope = body.RootElement.GetProperty("envelope").GetString()!;
            Assert.Equal(Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(envelope))).ToLowerInvariant(), handler.Signature);
            using var signed = JsonDocument.Parse(envelope);
            Assert.Equal("finance", Assert.Single(signed.RootElement.GetProperty("permissions").EnumerateArray()).GetString());
            Assert.Equal("11111111-1111-1111-1111-111111111111", signed.RootElement.GetProperty("subject").GetString());
            Assert.Equal("v3/admin/overview", signed.RootElement.GetProperty("path").GetString());
        } finally { File.Delete(keyFile); }
    }

    private static AdminEmthntakController Controller(Claim[] claims, IConfiguration configuration, IHttpClientFactory factory) => new(configuration,factory) {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,"11111111-1111-1111-1111-111111111111"), ..claims],"test")) } }
    };
    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class RecordingHandler : HttpMessageHandler {
        public string? Body, Signature;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
            Body=await request.Content!.ReadAsStringAsync(cancellationToken);
            Signature=request.Headers.GetValues("X-Massar-Signature").Single();
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"permissions\":[\"finance\"]}") };
        }
    }
}
