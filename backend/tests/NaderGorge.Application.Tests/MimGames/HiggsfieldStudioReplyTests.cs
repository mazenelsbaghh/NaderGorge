using System.Text.Json;
using NaderGorge.Infrastructure.Services.MimStudio;

namespace NaderGorge.Application.Tests.MimGames;

public sealed class HiggsfieldStudioReplyTests
{
    [Theory]
    [InlineData("{\"results\":[]}")]
    [InlineData("{\"results\":[{\"id\":\"60cf35c4-4b63-41d1-8c24-40d3962553f5\"},{\"id\":\"c871e19d-72eb-460d-b744-11b965509d1e\"}]}")]
    public void AmbiguousSubmissionNeverPicksAnArbitraryJob(string json)
    {
        using var reply = JsonDocument.Parse(json);
        Assert.Throws<HiggsfieldMcpException>(() => HiggsfieldStudioReply.SubmissionId(reply.RootElement));
    }

    [Fact]
    public void ProviderRejectionKeepsTheReasonButRemovesCredentialsAndRequestPayload()
    {
        using var reply = JsonDocument.Parse("""
            {"isError":true,"structuredContent":{"error":{"code":"INVALID_MEDIA","message":"Reference image unavailable; access_token=secret-value; Bearer private-token; https://example.test/signed?secret=foo; person@example.test"},"prompt":"PRIVATE_STORY","headers":{"cookie":"private-cookie"}}}
            """);
        var message = HiggsfieldMcpErrors.Rejection(reply.RootElement).Message;
        Assert.Contains("INVALID_MEDIA", message);
        Assert.Contains("Reference image unavailable", message);
        foreach (var secret in new[] { "secret-value", "private-token", "secret=foo", "person@example.test", "PRIVATE_STORY", "private-cookie" }) Assert.DoesNotContain(secret, message);
    }

    [Theory]
    [InlineData("{\"isError\":true,\"content\":[{\"type\":\"text\",\"text\":\"Prompt exceeds the model limit\"}]}", "Prompt exceeds")]
    [InlineData("{\"error\":{\"code\":-32602,\"message\":\"Invalid parameters\"}}", "Invalid parameters")]
    public void TextAndRpcErrorsRetainActionableDetails(string json, string reason)
    {
        using var reply = JsonDocument.Parse(json);
        Assert.Contains(reason, HiggsfieldMcpErrors.Rejection(reply.RootElement).Message);
    }

    [Theory]
    [InlineData("{\"notice\":{\"type\":\"execute_tool\",\"data\":{\"retry_literal_with\":{\"declined_preset_id\":\"24bae836-2c4a-48e0-89b6-49fcc0b21612\"}}}}")]
    [InlineData("{\"notice\":{\"type\":123}}")]
    [InlineData("{\"notice\":{\"type\":\"preset_recommendation\",\"data\":{\"retry_literal_with\":{\"declined_preset_id\":\"not-a-preset\"}}}}")]
    public void UnsupportedRecoveryInstructionsCannotChangeTheRequest(string json)
    {
        using var reply = JsonDocument.Parse(json);
        Assert.Null(HiggsfieldStudioReply.LiteralPresetToDecline(reply.RootElement));
    }

    [Fact]
    public void ReadsObservedHiggsfieldNestedCostWithoutAdjustments()
    {
        using var reply = JsonDocument.Parse("{\"cost\":{\"credits\":210,\"credits_exact\":210},\"adjustments\":{}}");
        Assert.Equal("210 كريديت من رصيد Higgsfield", HiggsfieldStudioReply.Quote(reply.RootElement));
    }

    [Theory]
    [InlineData("{\"error\":\"insufficient credits\"}")]
    [InlineData("{\"credits\":-1}")]
    [InlineData("{\"cost\":10,\"adjustments\":[\"duration 15\"]}")]
    [InlineData("{\"unlim_choice\":\"choose balance\"}")]
    public void RefusedOrAdjustedEstimateCannotAuthorizeSpending(string json) =>
        Assert.Throws<HiggsfieldMcpException>(() => HiggsfieldStudioReply.Quote(JsonDocument.Parse(json).RootElement));

    [Fact]
    public void CompletedJobWithNoUsableVideoRequiresReview()
    {
        var reply = JsonDocument.Parse("{\"status\":\"completed\",\"results\":[{\"url\":\"javascript:alert(1)\"}]}");
        var parsed = HiggsfieldStudioReply.Job(reply.RootElement);
        Assert.Equal("review_required", parsed.State);
        Assert.Empty(parsed.Urls);
    }

    [Fact]
    public void ReadsVideoResultFromStructuredMcpReply()
    {
        var reply = JsonDocument.Parse("{\"structuredContent\":{\"generation\":{\"status\":\"completed\",\"results\":[{\"url\":\"https://cdn.example/scene.mp4\"}]}}}");
        var parsed = HiggsfieldStudioReply.Job(HiggsfieldStudioReply.Payload(reply.RootElement));
        Assert.Equal("completed", parsed.State);
        Assert.Equal("https://cdn.example/scene.mp4", Assert.Single(parsed.Urls));
    }
}
