using System.Text.Json;
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

    [Fact]
    public void Every_implemented_action_exposes_a_non_empty_schema_and_validates_arguments()
    {
        foreach (var action in LiveSupportAICatalog.Actions.Keys)
        {
            var schema = LiveSupportAICatalog.GetArgumentsSchema(action);
            Assert.Equal(JsonValueKind.Object, schema.ValueKind);
            Assert.Equal(JsonValueKind.Object, schema.GetProperty("properties").ValueKind);
        }

        LiveSupportAICatalog.ValidateActionArguments("student.devices.disconnect-all", JsonDocument.Parse("{}").RootElement);
        Assert.Throws<InvalidOperationException>(() => LiveSupportAICatalog.ValidateActionArguments(
            "student.lesson.unlock", JsonDocument.Parse("{}").RootElement));
        Assert.Throws<InvalidOperationException>(() => LiveSupportAICatalog.ValidateActionArguments(
            "student.devices.disconnect-all", JsonDocument.Parse("{\"unexpected\":true}").RootElement));
    }

    [Fact]
    public void Catalog_rejects_actions_without_an_implementation()
    {
        Assert.Throws<InvalidOperationException>(() => LiveSupportAICatalog.GetArgumentsSchema("system.some_action"));
        Assert.Throws<InvalidOperationException>(() => LiveSupportAICatalog.ValidateActionArguments(
            "system.some_action", JsonDocument.Parse("{}").RootElement));
    }
}
