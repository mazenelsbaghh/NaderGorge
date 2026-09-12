using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class SupportBlockingTests
{
    [Fact]
    public async Task BlockingStudent_ShowsReasonAndPreventsNewConversationsUntilUnblocked()
    {
        await using var db = TestAppDbContextFactory.Create();
        var conversation = LiveSupportTestData.Conversation();
        db.LiveSupportConversations.Add(conversation);
        db.LiveSupportQueueEntries.Add(new() { ConversationId = conversation.Id });
        await db.SaveChangesAsync();
        var blocking = new LiveSupportBlockingService(db, new LiveSupportEventWriter(db));
        var support = new LiveSupportService(db, new LiveSupportEnabledSettings());
        var identity = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, conversation.StudentUserId, null);

        var status = await blocking.SetAsync(conversation.Id, LiveSupportTestData.AdminId,
            new(true, "رسائل مسيئة", conversation.Version), CancellationToken.None);
        var snapshot = await support.GetParticipantConversationAsync(identity, conversation.Id, CancellationToken.None);
        Assert.True(snapshot!.IsSupportBlocked);
        Assert.Equal("رسائل مسيئة", snapshot.SupportBlockReason);
        Assert.False(snapshot.CanSend);
        Assert.Equal(LiveSupportConversationStatus.Closed, snapshot.Status);
        Assert.NotNull((await db.LiveSupportQueueEntries.SingleAsync()).DequeuedAt);
        var rejected = await Assert.ThrowsAsync<LiveSupportException>(() => support.CreateConversationAsync(identity, "طلب جديد", null, CancellationToken.None));
        Assert.Equal("SUPPORT_BLOCKED", rejected.Code);

        await blocking.SetAsync(conversation.Id, LiveSupportTestData.AdminId,
            new(false, null, status.ConversationVersion), CancellationToken.None);
        Assert.Null(await LiveSupportBlockPolicy.FindAsync(db, conversation, CancellationToken.None));
        Assert.NotNull((await db.LiveSupportContactBlocks.SingleAsync()).UnblockedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task EmptyReason_DoesNotBlockOrQueueNotification(string reason)
    {
        await using var db = TestAppDbContextFactory.Create();
        var conversation = LiveSupportTestData.Conversation();
        db.LiveSupportConversations.Add(conversation);
        await db.SaveChangesAsync();
        var service = new LiveSupportBlockingService(db, new LiveSupportEventWriter(db));
        var error = await Assert.ThrowsAsync<LiveSupportException>(() => service.SetAsync(conversation.Id,
            LiveSupportTestData.AdminId, new(true, reason, conversation.Version), CancellationToken.None));
        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Empty(db.LiveSupportContactBlocks);
        Assert.Empty(db.LiveSupportBlockDeliveries);
    }

    [Fact]
    public async Task SamePhoneOnMetaAndBaileys_BlocksBothWithoutBlockingUnrelatedGuest()
    {
        await using var db = TestAppDbContextFactory.Create();
        var account = new LiveSupportWhatsAppAccount { Name = "دعم", InstanceName = "test-session" };
        db.LiveSupportWhatsAppAccounts.Add(account);
        var conversations = Enumerable.Range(0, 3).Select(_ => new LiveSupportConversation
        { ParticipantType = LiveSupportParticipantType.Guest, GuestSessionId = Guid.NewGuid(), Status = LiveSupportConversationStatus.Waiting, Version = 1 }).ToArray();
        db.LiveSupportConversations.AddRange(conversations);
        for (var index = 0; index < 2; index++) db.LiveSupportWhatsAppBindings.Add(new()
        {
            ConversationId = conversations[index].Id, GuestSessionId = conversations[index].GuestSessionId!.Value,
            WhatsAppUserId = "201099999999", PhoneNumber = "01099999999", AccountId = index == 0 ? null : account.Id
        });
        await db.SaveChangesAsync();
        var service = new LiveSupportBlockingService(db, new LiveSupportEventWriter(db));
        var status = await service.SetAsync(conversations[0].Id, LiveSupportTestData.AdminId,
            new(true, "إساءة متكررة", 1), CancellationToken.None);
        Assert.Equal(2, status.Deliveries.Count);
        Assert.Contains(status.Deliveries, delivery => delivery.AccountId == null);
        Assert.Contains(status.Deliveries, delivery => delivery.AccountId == account.Id);
        Assert.NotNull(await LiveSupportBlockPolicy.FindAsync(db, conversations[1], CancellationToken.None));
        Assert.Null(await LiveSupportBlockPolicy.FindAsync(db, conversations[2], CancellationToken.None));
        Assert.Equal(LiveSupportConversationStatus.Waiting, conversations[2].Status);
    }

    [Fact]
    public async Task MetaReturnsHttpSuccessWithRejectedUser_IsNotReportedAsBlocked()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["WhatsAppCloudApi:AccessToken"] = "test", ["WhatsAppCloudApi:PhoneNumberId"] = "123" }).Build();
        using var client = new HttpClient(new RejectedMetaUser());
        var cloud = new WhatsAppCloudService(client, config, NullLogger<WhatsAppCloudService>.Instance);
        var error = await Assert.ThrowsAsync<WhatsAppCloudService.WhatsAppCloudException>(() =>
            cloud.SetBlockedAsync("201099999999", true, CancellationToken.None));
        Assert.Equal("WHATSAPP_BLOCK_NOT_CONFIRMED", error.ErrorCode);
    }

    private sealed class RejectedMetaUser : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"block_users\":{\"failed_users\":[{\"input\":\"201099999999\"}]}}", Encoding.UTF8, "application/json") });
    }
}
