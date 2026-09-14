using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.AdminAI.Security;
using NaderGorge.Infrastructure.Services.AdminAI.Reads;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIReadCapabilityContractTests
{
    [Fact]
    public void Registration_RequiresExactOneAdapterPerActiveReadAndSafeOutput()
    {
        var definition = Read("safe.read"); var adapter = new SafeRead("safe.read");
        Assert.Single(AdminAIReadCapabilityRegistration.Validate([adapter], new AdminAICapabilityRegistry([definition]), new AdminAISensitiveDataPolicy()));
        Assert.Throws<InvalidOperationException>(() => AdminAIReadCapabilityRegistration.Validate([], new AdminAICapabilityRegistry([definition]), new AdminAISensitiveDataPolicy()));
        Assert.Throws<InvalidOperationException>(() => AdminAIReadCapabilityRegistration.Validate([adapter, adapter], new AdminAICapabilityRegistry([definition]), new AdminAISensitiveDataPolicy()));
        Assert.Throws<InvalidOperationException>(() => AdminAIReadCapabilityRegistration.Validate([new UnsafeRead()], new AdminAICapabilityRegistry([Read("unsafe.read")]), new AdminAISensitiveDataPolicy()));
    }

    [Fact]
    public void ProductionCatalog_ExposesNoIncompleteRepresentativeAdapters()
    {
        var registry = new AdminAICapabilityRegistry([]);
        Assert.Empty(registry.All);
        Assert.Throws<InvalidOperationException>(() => AdminAIReadCapabilityRegistration.Validate([new SafeRead("identity.users.summary")], registry, new AdminAISensitiveDataPolicy()));
    }

    [Fact]
    public void RepresentativeReadFamilies_HaveUniqueKeysAndSchemaAllowlists()
    {
        using var db = AdminAIStrongConfirmationTests.CreateDb();
        IAdminAIReadCapability[] adapters =
        [
            new AdminAIIdentitySummaryRead(db), new AdminAITeacherSummaryRead(db), new AdminAIContentSummaryRead(db),
            new AdminAIAssessmentSummaryRead(db), new AdminAICodeSummaryRead(db), new AdminAISalesSummaryRead(db),
            new AdminAIFormsSettingsSummaryRead(db), new AdminAIWalletRechargeSummaryRead(db),
            new AdminAILegacyFinanceSummaryRead(db), new AdminAITeacherFinanceSummaryRead(db),
            new AdminAIPlatformFinanceSummaryRead(db), new AdminAIHrPeopleSummaryRead(db),
            new AdminAIHrOperationsSummaryRead(db), new AdminAIHrLifecycleSummaryRead(db),
            new AdminAIOperationsSummaryRead(db), new AdminAICommunitySummaryRead(db),
            new AdminAILiveSupportSummaryRead(db), new AdminAIReportingSummaryRead(db),
            new AdminAITeacherSearchRead(db), new AdminAITeacherSubscribersSummaryRead(db),
            new AdminAIStudentSearchRead(db), new AdminAIStudentSnapshotRead(db)
        ];
        var definitions = adapters.Select(adapter => Read(adapter.Key)).ToArray();

        var registered = AdminAIReadCapabilityRegistration.Validate(
            adapters,
            new AdminAICapabilityRegistry(definitions),
            new AdminAISensitiveDataPolicy());

        Assert.Equal(adapters.Length, registered.Count);
        Assert.Equal(adapters.Length, registered.Select(adapter => adapter.Key).Distinct(StringComparer.Ordinal).Count());
    }

    private static AdminAICapabilityDefinition Read(string key) => new(key, "1", "read", "read", "none", "{}", "{}", 100, 65_536, 5_000, "Query", []);
    private sealed record SafeOutput(int Count);
    private sealed record UnsafeOutput(string PasswordHash);
    private sealed class SafeRead(string key) : IAdminAIReadCapability { public string Key => key; public Type OutputType => typeof(SafeOutput); public Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct) => throw new NotSupportedException(); }
    private sealed class UnsafeRead : IAdminAIReadCapability { public string Key => "unsafe.read"; public Type OutputType => typeof(UnsafeOutput); public Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct) => throw new NotSupportedException(); }
}
