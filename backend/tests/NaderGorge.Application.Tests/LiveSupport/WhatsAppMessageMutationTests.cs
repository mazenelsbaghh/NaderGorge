using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class WhatsAppMessageMutationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProviderResultControlsWhetherEditedMessageIsSaved(bool accepted)
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var db = fixture.Db;
        var conversation = await db.LiveSupportConversations.SingleAsync();
        var account = new LiveSupportWhatsAppAccount { Name = "Support", InstanceName = "test-session" };
        var message = new LiveSupportMessage { ConversationId = conversation.Id, Content = "Original", ClientMessageId = "outbound-original",
            SenderUserId = LiveSupportTestData.StaffAId, SenderType = LiveSupportSenderType.Staff, Type = LiveSupportMessageType.Text, SentAt = DateTime.UtcNow };
        db.LiveSupportWhatsAppAccounts.Add(account);
        db.LiveSupportMessages.Add(message);
        db.LiveSupportWhatsAppBindings.Add(new() { ConversationId = conversation.Id, AccountId = account.Id, WhatsAppUserId = "201099999999" });
        db.LiveSupportWhatsAppMessages.Add(new() { ConversationId = conversation.Id, LiveSupportMessageId = message.Id,
            Direction = "Outbound", Status = "Sent", MetaMessageId = "baileys:test-session:ABC123" });
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Baileys:BaseUrl"] = "https://bridge.test", ["Baileys:ApiKey"] = "test-only" }).Build();
        using var http = new HttpClient(new MutationResponse(accepted));
        var service = new LiveSupportService(db, new LiveSupportEnabledSettings(), messageMutations:
            new WhatsAppMessageMutationService(db, new BaileysWhatsAppClient(http, config)));
        var edit = service.UpdateStaffMessageAsync(LiveSupportTestData.StaffAId, false, conversation.Id, message.Id, "Edited", CancellationToken.None);
        if (accepted) Assert.Equal("Edited", (await edit).Content);
        else await Assert.ThrowsAsync<LiveSupportException>(() => edit);
        db.ChangeTracker.Clear();
        Assert.Equal(accepted ? "Edited" : "Original", (await db.LiveSupportMessages.SingleAsync()).Content);
    }

    private sealed class MutationResponse(bool accepted) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(accepted ? HttpStatusCode.OK : HttpStatusCode.BadGateway)
            { Content = new StringContent(accepted ? "{\"accepted\":true}" : "{}", Encoding.UTF8, "application/json") });
    }
}
