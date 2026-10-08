using System.Text.Json;
using NaderGorge.Infrastructure.Services.MimStudio;

namespace NaderGorge.Application.Tests.MimGames;

public sealed class HiggsfieldStudioReplyTests
{
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
