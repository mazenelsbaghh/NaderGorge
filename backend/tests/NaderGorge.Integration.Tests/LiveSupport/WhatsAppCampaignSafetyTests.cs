using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class WhatsAppCampaignSafetyTests
{
    [Theory]
    [InlineData("01012345678", "201012345678")]
    [InlineData("+20 (10) 1234-5678", "201012345678")]
    [InlineData("00201012345678", "201012345678")]
    public void NormalizeE164_AcceptsOnlyCanonicalEgyptMobileFormats(string input, string expected) =>
        Assert.Equal(expected, WhatsAppCampaignService.NormalizeE164(input));

    [Theory]
    [InlineData("201612345678")]
    [InlineData("+966501234567")]
    [InlineData("0101234567a")]
    [InlineData("20+1012345678")]
    [InlineData("٠١٠١٢٣٤٥٦٧٨")]
    public void NormalizeE164_RejectsUnsupportedOrAmbiguousDestinations(string input) =>
        Assert.Null(WhatsAppCampaignService.NormalizeE164(input));

    [Fact]
    public void TemplatePolicy_RejectsNamedOrMixedPlaceholders()
    {
        var template = Template("MARKETING", """
            [{"type":"BODY","text":"Hello {{name}} {{1}}"}]
            """);

        var exception = Assert.Throws<WhatsAppCampaignException>(() =>
            WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(template));

        Assert.Equal(WhatsAppCampaignErrorCodes.TemplateInvalid, exception.Code);
    }

    [Theory]
    [InlineData("AUTHENTICATION", "[{\"type\":\"BODY\",\"text\":\"Code {{1}}\"}]")]
    [InlineData("MARKETING", "[{\"type\":\"BODY\",\"text\":\"Hello\"},{\"type\":\"BUTTONS\",\"buttons\":[]}]")]
    public void TemplatePolicy_NonCampaignSafeTemplate_IsRejected(
        string category,
        string components)
    {
        var template = Template(category, components);

        Assert.Throws<WhatsAppCampaignException>(() =>
            WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(template));
    }

    [Fact]
    public void TemplatePolicy_StrongPreviewIncludesStaticFooter()
    {
        var parsed = WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(Template("UTILITY", """
            [{"type":"BODY","text":"Hello {{1}}"},{"type":"FOOTER","text":"Massar Academy"}]
            """));

        var preview = WhatsAppCampaignTemplatePolicy.RenderPreview(parsed,
            new Dictionary<(string Type, int Position), string> { [("BODY", 1)] = "Student" });

        Assert.Equal("Hello Student\nMassar Academy", preview);
    }

    [Fact]
    public void CampaignReceipt_OlderSentCannotResurrectNewerFailure()
    {
        var now = DateTime.UtcNow;
        var recipient = Recipient(WhatsAppCampaignRecipientStatus.Failed, now);

        var changed = WhatsAppCampaignService.ApplyCampaignReceipt(
            recipient, "sent", now.AddSeconds(-1), null);

        Assert.False(changed);
        Assert.Equal(WhatsAppCampaignRecipientStatus.Failed, recipient.Status);
    }

    [Fact]
    public void CampaignReceipt_OlderFailureCannotDowngradeNewerSent()
    {
        var now = DateTime.UtcNow;
        var recipient = Recipient(WhatsAppCampaignRecipientStatus.Sent, now);

        var changed = WhatsAppCampaignService.ApplyCampaignReceipt(
            recipient, "failed", now.AddSeconds(-1), "META_FAILED");

        Assert.False(changed);
        Assert.Equal(WhatsAppCampaignRecipientStatus.Sent, recipient.Status);
    }

    [Fact]
    public void CampaignReceipt_DeliveredRemainsMonotonicAcrossOlderObservations()
    {
        var now = DateTime.UtcNow;
        var recipient = Recipient(WhatsAppCampaignRecipientStatus.Failed, now);

        var changed = WhatsAppCampaignService.ApplyCampaignReceipt(
            recipient, "delivered", now.AddSeconds(-1), null);

        Assert.True(changed);
        Assert.Equal(WhatsAppCampaignRecipientStatus.Delivered, recipient.Status);
    }

    [Theory]
    [InlineData(408, "META_TIMEOUT", true)]
    [InlineData(500, "META_TRANSIENT", true)]
    [InlineData(503, "WHATSAPP_CLOUD_REQUEST_FAILED", true)]
    [InlineData(502, "WHATSAPP_CLOUD_INVALID_RESPONSE", true)]
    [InlineData(429, "META_RATE_LIMIT", false)]
    [InlineData(503, "WHATSAPP_CLOUD_NOT_CONFIGURED", false)]
    [InlineData(400, "META_INVALID", false)]
    public void Dispatcher_ProviderResponse_ClassifiesAmbiguityConservatively(
        int statusCode,
        string errorCode,
        bool expectedAmbiguous)
    {
        var providerResponse = new WhatsAppCloudService.SendTestMessageResult(
            false, "failure", "masked", null, statusCode, errorCode, true);

        Assert.Equal(expectedAmbiguous, WhatsAppCampaignDispatcher.IsAmbiguous(providerResponse));
    }

    private static LiveSupportWhatsAppTemplate Template(string category, string components) => new()
    {
        MetaTemplateId = "meta-1",
        Name = "campaign_template",
        Language = "ar",
        Category = category,
        Status = "APPROVED",
        ComponentsJson = components,
        Fingerprint = new string('a', 64),
        Version = 1
    };

    private static WhatsAppCampaignRecipient Recipient(
        WhatsAppCampaignRecipientStatus status,
        DateTime providerTimestamp) => new()
    {
        Status = status,
        ProviderTimestamp = providerTimestamp,
        Version = 1
    };
}
