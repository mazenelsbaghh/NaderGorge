using System.Text.Json;
using NaderGorge.Application.Features.AdminAI.Dtos;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ClosedRequests_RejectAdditionalProperties()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateAdminAIConversationRequest>(
            "{\"title\":null,\"password\":\"must-not-pass\"}", Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AdminAIInternalClaimRequest>(
            "{\"schemaVersion\":\"1\",\"workerInstanceId\":\"worker-1\",\"extra\":true}", Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AdminAIInternalFailRequest>(
            "{\"schemaVersion\":\"1\",\"leaseToken\":\"opaque\",\"callbackIdempotencyKey\":\"key\",\"failureCode\":\"AI_PROVIDER_TIMEOUT\",\"provider\":null,\"model\":null,\"latencyMs\":1,\"rawProviderError\":\"secret\"}", Json));
    }

    [Fact]
    public void TurnAdmission_UsesDocumentedMessageField()
    {
        var request = JsonSerializer.Deserialize<SendAdminAIMessageRequest>(
            "{\"message\":\"سؤال\",\"expectedConversationVersion\":1}", Json);

        Assert.Equal("سؤال", request!.Content);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SendAdminAIMessageRequest>(
            "{\"content\":\"wrong contract\",\"expectedConversationVersion\":1}", Json));
    }

    [Fact]
    public void PublicErrorCodes_AreClosedUniqueAndSafe()
    {
        var codes = AdminAIErrorCodes.All;
        Assert.NotEmpty(codes);
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches("^[A-Za-z0-9_]{1,64}$", code));
        Assert.DoesNotContain(codes, code => code.Contains("exception", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InternalFailureCodes_AreClosedAndNeverAcceptRawFailureText()
    {
        var values = Enum.GetNames<AdminAIInternalFailureCode>();
        Assert.Equal(7, values.Length);
        Assert.Contains("AI_QUEUE_STALE", values);
        Assert.DoesNotContain(typeof(AdminAIInternalFailRequest).GetProperties(), property =>
            property.Name.Contains("Message", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Body", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InternalRequests_ExposeOnlyProtocolFields()
    {
        Assert.Equal(
            ["SchemaVersion", "WorkerInstanceId"],
            typeof(AdminAIInternalClaimRequest).GetProperties().Select(x => x.Name).ToArray());
        Assert.Equal(7, typeof(AdminAIInternalReadRequest).GetProperties().Length);
        Assert.Equal(15, typeof(AdminAIInternalCompleteRequest).GetProperties().Length);
    }

}
