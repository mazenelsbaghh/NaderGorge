using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.People;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.HR;

public sealed class HrLifecycleNotificationTests
{
    [Fact]
    public async Task DueContractAndProbationAlerts_AreOutboxedOnlyOnce()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Employee Alert", "01033333331");
        var profile = new EmployeeProfile { UserId = user.Id, User = user, BasicSalary = 0 };
        profile.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(profile.Id);
        var contract = new EmploymentContract
        {
            EmployeeId = profile.Id, Employee = profile, ContractNumber = "C-ALERT", Type = EmploymentContractType.Permanent,
            Status = EmploymentContractStatus.Active, StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 8, 15), ProbationEndDate = new DateOnly(2026, 8, 10), Currency = "EGP"
        };
        db.EmployeeProfiles.Add(profile);
        db.EmploymentContracts.Add(contract);
        await db.SaveChangesAsync();
        var audit = new HrAuditWriter(db, DetachedHrRequestContext.Instance);
        var service = new HrLifecycleNotificationService(db, audit);

        var first = await service.EnqueueDueAsync(new DateOnly(2026, 8, 1), 20, default);
        var replay = await service.EnqueueDueAsync(new DateOnly(2026, 8, 1), 20, default);

        Assert.Equal(2, first);
        Assert.Equal(0, replay);
        Assert.Equal(2, db.NotificationEvents.Count());
        Assert.Equal(2, db.OutboxEvents.Count(item => item.Type == "NotificationCreated"));
        Assert.All(db.AuditLogs.Where(item => item.EntityId == contract.Id), item => Assert.Equal("System", item.ActorType));
    }
}
