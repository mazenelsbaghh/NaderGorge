using NaderGorge.Application.Features.LiveSupportAI.Services;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAICatalogTests
{
    [Fact]
    public void Catalog_contains_contract_keys_and_no_secret_keys()
    {
        Assert.Contains("identity.basic", LiveSupportAICatalog.ReadableData.Keys);
        Assert.Contains("student.create-and-link", LiveSupportAICatalog.Actions.Keys);
        Assert.Contains("phone.full", LiveSupportAICatalog.LookupKeys.Keys);
        Assert.Contains("profile.birth_date", LiveSupportAICatalog.VerificationQuestions.Keys);

        var allKeys = LiveSupportAICatalog.Snapshot().ReadableData.Concat(LiveSupportAICatalog.Snapshot().Actions)
            .Concat(LiveSupportAICatalog.Snapshot().LookupKeys).Concat(LiveSupportAICatalog.Snapshot().VerificationQuestions).Select(x => x.Key);
        Assert.DoesNotContain(allKeys, key => LiveSupportAISafety.IsForbiddenKey(key) || key.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Safety_rejects_raw_verification_answers()
    {
        var values = new Dictionary<string, object?> { ["verificationAnswer"] = "secret" };
        Assert.Throws<ArgumentException>(() => LiveSupportAISafety.SerializeBounded(values));
    }
}
