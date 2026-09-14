using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class LiveSupportModelTests
{
    [Fact]
    public void Model_EnforcesCriticalUniquenessAndFieldLimits()
    {
        using var db = TestAppDbContextFactory.Create();
        var conversation = db.Model.FindEntityType(typeof(LiveSupportConversation))!;
        var assignment = db.Model.FindEntityType(typeof(LiveSupportAssignment))!;
        var queue = db.Model.FindEntityType(typeof(LiveSupportQueueEntry))!;
        var rating = db.Model.FindEntityType(typeof(LiveSupportRating))!;
        var message = db.Model.FindEntityType(typeof(LiveSupportMessage))!;

        Assert.Contains(conversation.GetIndexes(), index => index.IsUnique && index.GetFilter()?.Contains("Status") == true);
        Assert.Contains(assignment.GetIndexes(), index => index.IsUnique && index.GetFilter()?.Contains("EndedAt") == true);
        Assert.Contains(queue.GetIndexes(), index => index.IsUnique && index.GetFilter()?.Contains("DequeuedAt") == true);
        Assert.Contains(rating.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual([nameof(LiveSupportRating.ConversationId)]));
        Assert.Equal(4000, message.FindProperty(nameof(LiveSupportMessage.Content))!.GetMaxLength());
    }

    [Fact]
    public void WhatsAppChannelModel_EnforcesWebhookDeduplicationAndSingleConversationBinding()
    {
        using var db = TestAppDbContextFactory.Create();
        var binding = db.Model.FindEntityType(typeof(LiveSupportWhatsAppBinding))!;
        var delivery = db.Model.FindEntityType(typeof(LiveSupportWhatsAppMessage))!;
        var pendingReceipt = db.Model.FindEntityType(typeof(LiveSupportWhatsAppPendingReceipt))!;

        Assert.Contains(binding.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(LiveSupportWhatsAppBinding.ConversationId)]));
        Assert.Contains(delivery.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(LiveSupportWhatsAppMessage.MetaMessageId)]));
        Assert.Contains(delivery.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(LiveSupportWhatsAppMessage.LiveSupportMessageId)]));
        Assert.Contains(pendingReceipt.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(LiveSupportWhatsAppPendingReceipt.MetaMessageId)]));
    }
}
