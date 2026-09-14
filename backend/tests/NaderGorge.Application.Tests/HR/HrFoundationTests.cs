using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.HR;

public class HrFoundationTests
{
    [Fact]
    public void EmployeeNumber_HasUniqueDatabaseConstraint()
    {
        using var db = TestAppDbContextFactory.Create();
        var entity = db.Model.FindEntityType(typeof(EmployeeProfile));

        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(EmployeeProfile.EmployeeNumber)]));
    }

    [Fact]
    public void HrSafetyModel_UsesUniqueIdempotencyAndSingleModuleRows()
    {
        using var db = TestAppDbContextFactory.Create();
        var idempotency = db.Model.FindEntityType(typeof(HrIdempotencyRecord));
        var rollout = db.Model.FindEntityType(typeof(HrModuleRollout));

        Assert.NotNull(idempotency);
        Assert.Contains(idempotency!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "Scope", "ActorUserId", "Key" }));
        Assert.NotNull(rollout);
        Assert.Contains(rollout!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == "Module");
    }

    [Theory]
    [InlineData(typeof(EmployeeProfile), nameof(EmployeeProfile.UserId))]
    [InlineData(typeof(AttendanceLog), nameof(AttendanceLog.EmployeeId))]
    [InlineData(typeof(PayrollRecord), nameof(PayrollRecord.EmployeeProfileId))]
    [InlineData(typeof(PayrollAdjustment), nameof(PayrollAdjustment.PayrollRecordId))]
    public void HrHistoryRelationships_AreNotCascadeDeleted(Type entityType, string foreignKeyProperty)
    {
        using var db = TestAppDbContextFactory.Create();
        var metadata = db.Model.FindEntityType(entityType);
        var foreignKey = metadata!.GetForeignKeys().Single(item =>
            item.Properties.Any(property => property.Name == foreignKeyProperty));

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }
}
