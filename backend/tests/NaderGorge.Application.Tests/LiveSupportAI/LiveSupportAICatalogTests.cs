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
        Assert.Equal("البيانات الأساسية", LiveSupportAICatalog.ReadableData["identity.basic"].Label);
        Assert.Equal("إنشاء حساب وربطه", LiveSupportAICatalog.Actions["student.create-and-link"].Label);
        Assert.All(LiveSupportAICatalog.Actions.Values, action => Assert.True(action.RequiresVerification));

        var snapshot = LiveSupportAICatalog.Snapshot();
        var allCatalogItems = snapshot.ReadableData.Concat(snapshot.Actions)
            .Concat(snapshot.LookupKeys).Concat(snapshot.VerificationQuestions).ToArray();
        Assert.All(allCatalogItems, catalogItem =>
        {
            Assert.NotEqual(catalogItem.Key, catalogItem.Label);
            Assert.NotEqual(catalogItem.Key, catalogItem.Description);
        });

        var allKeys = allCatalogItems.Select(catalogItem => catalogItem.Key);
        Assert.DoesNotContain(allKeys, key => LiveSupportAISafety.IsForbiddenKey(key) || key.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Safety_rejects_raw_verification_answers()
    {
        var values = new Dictionary<string, object?> { ["verificationAnswer"] = "secret" };
        Assert.Throws<ArgumentException>(() => LiveSupportAISafety.SerializeBounded(values));
    }
}
