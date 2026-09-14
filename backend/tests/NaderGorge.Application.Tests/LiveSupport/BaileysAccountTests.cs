using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class BaileysAccountTests
{
    [Theory]
    [InlineData("connect", false)]
    [InlineData("connect", true)]
    [InlineData("refresh", false)]
    [InlineData("refresh", true)]
    [InlineData("disconnect", true)]
    public async Task BridgeResponse_PreservesNewerWebhookStateAndAppliesAccountEnablement(
        string operation, bool concurrentWebhook)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var actor = LiveSupportTestData.User(LiveSupportTestData.AdminId, "مدير الدعم", "01011111111");
        var account = new LiveSupportWhatsAppAccount
        {
            Name = "Support", InstanceName = "test-session", CreatedByUserId = actor.Id,
            Status = "Disconnected", IsEnabled = false, Version = 1
        };
        db.Users.Add(actor);
        db.LiveSupportWhatsAppAccounts.Add(account);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        using var http = new HttpClient(new BridgeHandler(async () =>
        {
            if (!concurrentWebhook) return;
            await using var webhookDb = new AppDbContext(options);
            var updated = await webhookDb.LiveSupportWhatsAppAccounts.SingleAsync();
            updated.Status = "Connected";
            updated.PhoneNumber = "201099999999";
            updated.Version++;
            await webhookDb.SaveChangesAsync();
        }, operation));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Baileys:BaseUrl"] = "https://bridge.test", ["Baileys:ApiKey"] = "test" }).Build();
        var service = new BaileysAccountService(db, new BaileysWhatsAppClient(http, config));

        BaileysAccountDto result;
        if (operation == "connect")
        {
            var connected = await service.ConnectAsync(account.Id, CancellationToken.None);
            result = connected.Account;
            Assert.Equal(concurrentWebhook ? null : "data:image/png;base64,dGVzdA==", connected.QrDataUrl);
        }
        else result = operation == "refresh"
            ? await service.RefreshAsync(account.Id, CancellationToken.None)
            : await service.DisconnectAsync(account.Id, CancellationToken.None);

        var expectedStatus = operation == "disconnect" ? "Disconnected" : concurrentWebhook
            ? "Connected" : operation == "connect" ? "AwaitingQr" : "Connecting";
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(operation == "connect", result.IsEnabled);
        Assert.Equal(concurrentWebhook ? "201099999999" : null, result.PhoneNumber);
        var stored = await db.LiveSupportWhatsAppAccounts.AsNoTracking().SingleAsync();
        Assert.Equal(result.Status, stored.Status);
        Assert.Equal(result.IsEnabled, stored.IsEnabled);
    }

    private sealed class BridgeHandler(Func<Task> webhook, string operation) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (operation != "connect" || request.Method == HttpMethod.Post) await webhook();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"instance":{"state":"connecting"},"base64":"data:image/png;base64,dGVzdA=="}""",
                    Encoding.UTF8, "application/json")
            };
        }
    }
}
