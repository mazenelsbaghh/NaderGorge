using NaderGorge.Application.Features.LiveSupportAI.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Infrastructure.Services.LiveSupportAI;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIContractParityTests
{
    [Fact]
    public void Canonical_decision_hash_matches_worker_for_unicode_reply()
    {
        var decision = new LiveSupportAIWorkerDecisionDto("1", "reply", "تحت أمرك", null, null, null, null, null);
        Assert.Equal("54b18a923d30b24cd7fe70821a6fea1f8150f257d9f25fa9e48295c9ac14ed6a", LiveSupportAITurnOrchestrator.ComputeDecisionHash(decision));
    }

    [Fact]
    public void Decision_pending_and_mode_contracts_have_the_expected_stable_members()
    {
        Assert.Equal(
            ["Reply", "ProposeAction", "RequestVerification", "ProposeAccountCreation", "RequestResolution", "Handoff"],
            Enum.GetNames<LiveSupportAIDecisionType>());
        Assert.Equal(
            ["Action", "Handoff", "AccountCreation", "Resolution"],
            Enum.GetNames<LiveSupportAIPendingDecisionKind>());
        Assert.Contains("AiActive", Enum.GetNames<LiveSupportAIMode>());
        Assert.Contains("HumanQueued", Enum.GetNames<LiveSupportAIMode>());
        Assert.Contains("HumanAssigned", Enum.GetNames<LiveSupportAIMode>());
        Assert.Contains("AiResolved", Enum.GetNames<LiveSupportAIMode>());
    }

    [Fact]
    public void Catalog_keys_are_non_empty_unique_and_safe_for_contract_transport()
    {
        var allKeys = LiveSupportAICatalog.ReadableData.Keys
            .Concat(LiveSupportAICatalog.Actions.Keys)
            .Concat(LiveSupportAICatalog.LookupKeys.Keys)
            .Concat(LiveSupportAICatalog.VerificationQuestions.Keys)
            .ToArray();

        Assert.All(allKeys, key => Assert.Matches("^[a-z0-9][a-z0-9._-]{1,99}$", key));
        Assert.Equal(allKeys.Length, allKeys.Distinct(StringComparer.Ordinal).Count());
    }
}
