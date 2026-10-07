using System.Net;
using System.Text;
using System.Text.Json;
using NaderGorge.Infrastructure.Services.MimStudio;

namespace NaderGorge.Application.Tests.MimGames;

public sealed class HiggsfieldMcpTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoversToolsFromJsonOrEventStreamWithoutCallingGeneration(bool eventStream)
    {
        using var client = new HttpClient(new McpReplyHandler(eventStream));
        var transport = new HiggsfieldMcpTransport(client, "test-token");
        await transport.InitializeAsync(default);
        var response = await transport.RequestAsync("tools/list", new { }, default);
        Assert.Equal("generate_video", response.GetProperty("tools")[0].GetProperty("name").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task DoesNotPresentProviderFailureAsAnEmptySuccessfulCatalog(HttpStatusCode status)
    {
        using var client = new HttpClient(new FailureHandler(status));
        var transport = new HiggsfieldMcpTransport(client, "test-token");
        var exception = await Assert.ThrowsAsync<HiggsfieldMcpException>(() => transport.InitializeAsync(default));
        Assert.DoesNotContain("test-token", exception.Message);
        Assert.DoesNotContain("private-provider-body", exception.Message);
    }

    private sealed class FailureHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private-provider-body") });
    }

    private sealed class McpReplyHandler(bool eventStream) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var method = body.RootElement.GetProperty("method").GetString();
            if (method == "notifications/initialized") return new(HttpStatusCode.Accepted);
            Assert.Contains(method, new[] { "initialize", "tools/list" });
            var id = body.RootElement.GetProperty("id").GetString();
            object result = method == "initialize" ? new { protocolVersion = "2025-11-25", capabilities = new { } } :
                new { tools = new[] { new { name = "generate_video", inputSchema = new { type = "object" } } } };
            var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result });
            var content = eventStream ? $": heartbeat\n\ndata: {{\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\"}}\n\ndata: {json}\n\n" : json;
            return new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, eventStream ? "text/event-stream" : "application/json") };
        }
    }
}
