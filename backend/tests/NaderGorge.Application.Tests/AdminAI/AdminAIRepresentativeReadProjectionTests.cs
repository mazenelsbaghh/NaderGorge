using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Infrastructure.Services.AdminAI.Reads;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIRepresentativeReadProjectionTests
{
    [Theory]
    [InlineData(typeof(AdminAIIdentitySummary))]
    [InlineData(typeof(AdminAIContentSummary))]
    [InlineData(typeof(AdminAIAssessmentSummary))]
    [InlineData(typeof(AdminAICommunitySummary))]
    [InlineData(typeof(AdminAIFormsSettingsSummary))]
    [InlineData(typeof(AdminAIReportingSummary))]
    [InlineData(typeof(AdminAITeacherSummary))]
    [InlineData(typeof(AdminAICodeSummary))]
    [InlineData(typeof(AdminAISalesSummary))]
    [InlineData(typeof(AdminAIWalletRechargeSummary))]
    [InlineData(typeof(AdminAILegacyFinanceSummary))]
    [InlineData(typeof(AdminAITeacherFinanceSummary))]
    [InlineData(typeof(AdminAIPlatformFinanceSummary))]
    [InlineData(typeof(AdminAIHrPeopleSummary))]
    [InlineData(typeof(AdminAIHrOperationsSummary))]
    [InlineData(typeof(AdminAIHrLifecycleSummary))]
    [InlineData(typeof(AdminAIOperationsSummary))]
    [InlineData(typeof(AdminAILiveSupportSummary))]
    public void ProjectionDtos_ContainOnlyBoundedAggregateFields(Type projection)
    {
        var properties = projection.GetProperties();
        Assert.InRange(properties.Length, 2, 20);
        Assert.All(properties, property => Assert.True(property.PropertyType == typeof(int) || property.PropertyType == typeof(decimal) || property.PropertyType == typeof(DateTime)));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("Values", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EmptyCommunityProjection_IsCompleteAndContainsNoStoredText()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var projection = await new AdminAICommunitySummaryRead(db).ExecuteAsync(Guid.NewGuid(), new { }, default);
        Assert.Equal(1, projection.ResultCount); Assert.True(projection.IsComplete); Assert.False(projection.IsTruncated);
        var value = Assert.IsType<AdminAICommunitySummary>(projection.Data); Assert.Equal(0, value.Posts); Assert.Equal(0, value.Comments);
    }

    [Fact]
    public async Task EmptyAggregateProjections_AreBoundedCompleteAndContainNoText()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        IAdminAIReadCapability[] adapters =
        [
            new AdminAITeacherSummaryRead(db),
            new AdminAICodeSummaryRead(db),
            new AdminAISalesSummaryRead(db),
            new AdminAIWalletRechargeSummaryRead(db),
            new AdminAILegacyFinanceSummaryRead(db),
            new AdminAITeacherFinanceSummaryRead(db),
            new AdminAIPlatformFinanceSummaryRead(db),
            new AdminAIHrPeopleSummaryRead(db),
            new AdminAIHrOperationsSummaryRead(db),
            new AdminAIHrLifecycleSummaryRead(db),
            new AdminAIOperationsSummaryRead(db),
            new AdminAILiveSupportSummaryRead(db)
        ];

        foreach (var adapter in adapters)
        {
            var projection = await adapter.ExecuteAsync(Guid.NewGuid(), new { }, default);
            Assert.Equal(1, projection.ResultCount);
            Assert.True(projection.IsComplete);
            Assert.False(projection.IsTruncated);
            Assert.NotEmpty(projection.References);
            Assert.All(adapter.OutputType.GetProperties(), property =>
                Assert.True(property.PropertyType == typeof(int) || property.PropertyType == typeof(decimal) || property.PropertyType == typeof(DateTime)));
        }
    }

    [Fact]
    public async Task FinanceProjections_ComputeDeterministicDecimalTotalsFromAuthoritativeColumns()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        db.PayrollRecords.Add(new NaderGorge.Domain.Entities.PayrollRecord { Id = Guid.NewGuid(), EmployeeProfileId = Guid.NewGuid(), BasicSalary = 1250.25m });
        db.TeacherFinancialEvents.Add(new NaderGorge.Domain.Entities.TeacherFinancialEvent { Id = Guid.NewGuid(), SourceId = Guid.NewGuid(), TargetId = Guid.NewGuid(), GrossAmount = 200m, Currency = "EGP", IdempotencyKey = "read-test" });
        db.JournalLines.Add(new NaderGorge.Domain.Entities.JournalLine { Id = Guid.NewGuid(), JournalEntryId = Guid.NewGuid(), FinancialAccountId = Guid.NewGuid(), Debit = 75.10m, Credit = 75.10m });
        db.FinanceBudgetLines.Add(new NaderGorge.Domain.Entities.FinanceBudgetLine { Id = Guid.NewGuid(), FinanceBudgetPlanId = Guid.NewGuid(), FinancialAccountId = Guid.NewGuid(), PlannedAmount = 500m });
        await db.SaveChangesAsync();

        var legacyProjection = await new AdminAILegacyFinanceSummaryRead(db).ExecuteAsync(Guid.NewGuid(), new { }, default);
        var legacy = Assert.IsType<AdminAILegacyFinanceSummary>(legacyProjection.Data);
        Assert.Equal(1250.25m, legacy.BasicSalariesEgp);
        Assert.Equal(200m, legacy.GrossSalesEgp);

        var platformProjection = await new AdminAIPlatformFinanceSummaryRead(db).ExecuteAsync(Guid.NewGuid(), new { }, default);
        var platform = Assert.IsType<AdminAIPlatformFinanceSummary>(platformProjection.Data);
        Assert.Equal(75.10m, platform.DebitsEgp);
        Assert.Equal(75.10m, platform.CreditsEgp);
        Assert.Equal(500m, platform.BudgetedEgp);
    }
}
