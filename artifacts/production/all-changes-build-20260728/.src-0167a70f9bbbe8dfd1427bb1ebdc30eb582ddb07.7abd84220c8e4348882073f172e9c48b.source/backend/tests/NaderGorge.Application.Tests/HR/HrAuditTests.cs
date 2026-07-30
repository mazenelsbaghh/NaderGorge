using System.Text.Json;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.HR;

public sealed class HrAuditTests
{
    [Fact]
    public async Task MutationAudit_RequiresActorOrNamedSystemIdentity()
    {
        await using var db = TestAppDbContextFactory.Create();
        var writer = new HrAuditWriter(db, new StubContext(null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => writer.WriteMutationAsync(
            "Update", "Employee", Guid.NewGuid(), null, new { status = "Active" }, "status change", default));
    }

    [Fact]
    public async Task MutationAudit_CapturesActorContextAndRedactsSensitiveValues()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = new User { FullName = "HR Reviewer", PhoneNumber = "01000000000", PasswordHash = "hash", IsActive = true };
        db.Users.Add(actor);
        await db.SaveChangesAsync();
        var writer = new HrAuditWriter(db, new StubContext(actor.Id));

        await writer.WriteMutationAsync(
            "UpdateEmployee", "EmployeeProfile", Guid.NewGuid(),
            new { basicSalary = 1000, status = "Probation" },
            new { basicSalary = 1500, status = "Active", phoneNumber = "01111111111" },
            "probation passed", default);
        await db.SaveChangesAsync();

        var audit = Assert.Single(db.AuditLogs);
        Assert.Equal(actor.Id, audit.PerformedByUserId);
        Assert.Equal("corr-1", audit.CorrelationId);
        Assert.Equal("request-1", audit.RequestId);
        Assert.Equal("127.0.0.1", audit.IpAddress);
        Assert.Equal("probation passed", audit.Reason);
        Assert.Contains("HR Reviewer", audit.ActorSnapshot);
        Assert.Equal("[REDACTED]", JsonDocument.Parse(audit.NewValues!).RootElement.GetProperty("basicSalary").GetString());
        Assert.Equal("[REDACTED]", JsonDocument.Parse(audit.NewValues!).RootElement.GetProperty("phoneNumber").GetString());
        Assert.Equal("Active", JsonDocument.Parse(audit.NewValues!).RootElement.GetProperty("status").GetString());
    }

    private sealed class StubContext(Guid? actorId) : IHrRequestContext
    {
        public Guid? ActorUserId => actorId;
        public string CorrelationId => "corr-1";
        public string? IpAddress => "127.0.0.1";
        public string RequestId => "request-1";
    }
}
