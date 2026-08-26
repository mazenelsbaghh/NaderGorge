using System.Text;
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
            new Dictionary<WhatsAppTemplateParameterKey, string>
            {
                [new WhatsAppTemplateParameterKey("BODY", 0, 1)] = "Student"
            });

        Assert.Equal("Hello Student\nMassar Academy", preview);
    }

    [Fact]
    public void ProductionRegression_20260826_StaticUrlButtons_AreAcceptedWithoutProviderParameters()
    {
        var parsed = WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(Template("UTILITY", """
            [
              {"type":"HEADER","format":"TEXT","text":"Student progress"},
              {"type":"BODY","text":"Hello {{1}}, score {{2}}"},
              {"type":"BUTTONS","buttons":[
                {"type":"URL","text":"Open report","url":"https://massar-academy.net/report"},
                {"type":"URL","text":"Open platform","url":"https://massar-academy.net"}
              ]}
            ]
            """));
        var mappings = new[]
        {
            Mapping("BODY", 1, 1),
            Mapping("BODY", 1, 2)
        };

        var canonicalMappings = WhatsAppCampaignTemplatePolicy.ValidateMappings(parsed, mappings);
        var resolvedParameters = canonicalMappings.ToDictionary(
            entry => entry.Requirement.Key,
            entry => entry.Requirement.Key.Position == 1 ? "Mazen" : "95%");
        var providerComponents = WhatsAppCampaignTemplatePolicy.ProviderComponents(
            parsed, resolvedParameters);

        var body = Assert.Single(providerComponents);
        Assert.Equal("BODY", body.Type);
        Assert.Equal(["Mazen", "95%"], body.Parameters);
        Assert.DoesNotContain(providerComponents, component => component.Type == "BUTTON");
    }

    [Fact]
    public void TemplatePolicy_DynamicUrlButton_UsesItsComponentAndButtonIdentity()
    {
        var parsed = WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(Template("MARKETING", """
            [
              {"type":"BODY","text":"Open your report"},
              {"type":"BUTTONS","buttons":[
                {"type":"URL","text":"View","url":"https://massar-academy.net/report/{{1}}"}
              ]}
            ]
            """));
        var mapping = new WhatsAppCampaignVariableMappingDto(
            "BUTTON", 1, "Literal", "student-token", ComponentIndex: 1, ButtonIndex: 0);

        var canonicalMapping = Assert.Single(
            WhatsAppCampaignTemplatePolicy.ValidateMappings(parsed, [mapping]));
        var providerComponent = Assert.Single(WhatsAppCampaignTemplatePolicy.ProviderComponents(parsed,
            new Dictionary<WhatsAppTemplateParameterKey, string>
            {
                [canonicalMapping.Requirement.Key] = "student-token"
            }));

        Assert.Equal("BUTTON", providerComponent.Type);
        Assert.Equal("url", providerComponent.SubType);
        Assert.Equal(0, providerComponent.Index);
        Assert.Equal(["student-token"], providerComponent.Parameters);
    }

    [Fact]
    public void TemplatePolicy_DynamicUrlButton_RejectsPersonalizedSource()
    {
        var parsed = WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(Template("MARKETING", """
            [
              {"type":"BODY","text":"Open your report"},
              {"type":"BUTTONS","buttons":[
                {"type":"URL","text":"View","url":"https://massar-academy.net/report/{{1}}"}
              ]}
            ]
            """));
        var mapping = new WhatsAppCampaignVariableMappingDto(
            "BUTTON", 1, "StudentFullName", ComponentIndex: 1, ButtonIndex: 0);

        Assert.Throws<WhatsAppCampaignException>(() =>
            WhatsAppCampaignTemplatePolicy.ValidateMappings(parsed, [mapping]));
    }

    [Fact]
    public void TemplatePolicy_StaticPhoneButton_IsAcceptedWithoutProviderParameters()
    {
        var parsed = WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(Template("UTILITY", """
            [
              {"type":"BODY","text":"Call support"},
              {"type":"BUTTONS","buttons":[
                {"type":"PHONE_NUMBER","text":"Call","phone_number":"+201000000000"}
              ]}
            ]
            """));

        var mappings = WhatsAppCampaignTemplatePolicy.ValidateMappings(parsed, []);
        var providerComponents = WhatsAppCampaignTemplatePolicy.ProviderComponents(parsed,
            new Dictionary<WhatsAppTemplateParameterKey, string>());

        Assert.Empty(mappings);
        Assert.Empty(providerComponents);
    }

    [Theory]
    [InlineData("{\"type\":\"HEADER\",\"format\":\"IMAGE\"}")]
    [InlineData("{\"type\":\"BUTTONS\",\"buttons\":[{\"type\":\"QUICK_REPLY\",\"text\":\"Reply\"}]}")]
    public void TemplatePolicy_UnsafeRuntimeComponent_IsRejected(string unsafeComponent)
    {
        var template = Template("UTILITY", $"[{{\"type\":\"BODY\",\"text\":\"Hello\"}},{unsafeComponent}]");

        Assert.Throws<WhatsAppCampaignException>(() =>
            WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(template));
    }

    [Fact]
    public void FrozenRecipientPayload_CurrentCamelCaseRoundTripsWithTypedComponents()
    {
        var original = new WhatsAppCampaignService.FrozenRecipientPayload(
            "201012345678",
            [
                new WhatsAppCloudService.TemplateComponent("BODY", ["Mazen", "95%"]),
                new WhatsAppCloudService.TemplateComponent(
                    "BUTTON", ["opaque-token"], "text", "url", 1)
            ]);

        var serialized = WhatsAppCampaignService.SerializeFrozenRecipientPayload(original);
        var payload = WhatsAppCampaignService.DeserializeFrozenRecipientPayload(
            Encoding.UTF8.GetBytes(serialized));

        Assert.Contains("\"destination\"", serialized);
        Assert.Contains("\"parameterType\"", serialized);
        Assert.DoesNotContain("\"Destination\"", serialized);
        var parsed = Assert.IsType<WhatsAppCampaignService.FrozenRecipientPayload>(payload);
        Assert.Equal(original.Destination, parsed.Destination);
        Assert.Collection(parsed.Components,
            body =>
            {
                Assert.Equal("BODY", body.Type);
                Assert.Equal(["Mazen", "95%"], body.Parameters);
                Assert.Equal("text", body.ParameterType);
                Assert.Null(body.SubType);
                Assert.Null(body.Index);
            },
            button =>
            {
                Assert.Equal("BUTTON", button.Type);
                Assert.Equal(["opaque-token"], button.Parameters);
                Assert.Equal("text", button.ParameterType);
                Assert.Equal("url", button.SubType);
                Assert.Equal(1, button.Index);
            });
    }

    [Fact]
    public void FrozenRecipientPayload_LegacyTextComponentsRemainDeserializable()
    {
        const string legacyPayload = """
            {"Destination":"201012345678","Components":[{"Type":"BODY","Parameters":["Mazen"]}]}
            """;

        var payload = WhatsAppCampaignService.DeserializeFrozenRecipientPayload(
            Encoding.UTF8.GetBytes(legacyPayload));

        var parsed = Assert.IsType<WhatsAppCampaignService.FrozenRecipientPayload>(payload);
        Assert.Equal("201012345678", parsed.Destination);
        var component = Assert.Single(parsed.Components);
        Assert.Equal("text", component.ParameterType);
        Assert.Null(component.SubType);
        Assert.Null(component.Index);
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

    private static WhatsAppCampaignVariableMappingDto Mapping(
        string componentType,
        int componentIndex,
        int position) =>
        new(componentType, position, "Literal", $"value-{position}", ComponentIndex: componentIndex);

    private static WhatsAppCampaignRecipient Recipient(
        WhatsAppCampaignRecipientStatus status,
        DateTime providerTimestamp) => new()
    {
        Status = status,
        ProviderTimestamp = providerTimestamp,
        Version = 1
    };
}
