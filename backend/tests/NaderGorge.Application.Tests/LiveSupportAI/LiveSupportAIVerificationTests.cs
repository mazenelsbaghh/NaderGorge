using System.Reflection;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIVerificationTests
{
    [Fact]
    public void Verification_storage_has_digest_and_outcome_only_without_raw_lookup_or_answer()
    {
        var sessionProperties = typeof(LiveSupportAIVerificationSession).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var attemptProperties = typeof(LiveSupportAIVerificationAttempt).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(sessionProperties, property => property.Name == nameof(LiveSupportAIVerificationSession.LookupValueHash));
        Assert.DoesNotContain(sessionProperties, property => property.Name is "LookupValue" or "ExpectedAnswer");
        Assert.DoesNotContain(attemptProperties, property => property.Name.Contains("Answer", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attemptProperties, property => property.Name == nameof(LiveSupportAIVerificationAttempt.OutcomeCodesJson));
    }
}
