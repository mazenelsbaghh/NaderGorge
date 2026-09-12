using System.Text.Json;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class BaileysWebhookTests
{
    [Theory]
    [InlineData("201099999999@s.whatsapp.net", null)]
    [InlineData("999999@lid", "201099999999@s.whatsapp.net")]
    public void InboundPhoneIdentity_IsNormalizedAndMessageIdIsScopedToAccount(string jid, string? alternate)
    {
        var message = JsonSerializer.SerializeToElement(new
        {
            key = new { id = "message-1", remoteJid = jid, remoteJidAlt = alternate, fromMe = false },
            message = new { conversation = "محتاج مساعدة" }, messageTimestamp = "1789236000"
        });
        var first = BaileysWebhookService.NormalizeMessage("account-a", message)!.Value;
        var second = BaileysWebhookService.NormalizeMessage("account-b", message)!.Value;
        Assert.Equal("201099999999", first.GetProperty("from").GetString());
        Assert.Equal("محتاج مساعدة", first.GetProperty("text").GetProperty("body").GetString());
        Assert.NotEqual(first.GetProperty("id").GetString(), second.GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("201099999999@s.whatsapp.net", true)]
    [InlineData("123@g.us", false)]
    [InlineData("status@broadcast", false)]
    public void OutboundEchoesAndGroups_DoNotCreateSupportConversations(string jid, bool fromMe)
    {
        var message = JsonSerializer.SerializeToElement(new
        { key = new { id = "message-1", remoteJid = jid, fromMe }, message = new { conversation = "hello" } });
        Assert.Null(BaileysWebhookService.NormalizeMessage("account-a", message));
    }
}
