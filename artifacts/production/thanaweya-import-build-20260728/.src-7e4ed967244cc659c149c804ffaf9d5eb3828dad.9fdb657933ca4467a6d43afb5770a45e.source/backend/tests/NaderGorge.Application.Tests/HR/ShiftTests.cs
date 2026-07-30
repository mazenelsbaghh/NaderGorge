using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Scheduling;
using NaderGorge.Application.Features.HR.Scheduling.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.HR;

public sealed class ShiftTests
{
    [Fact]
    public void OvernightSegment_UsesNextDayAndConfiguredWorkDate()
    {
        var segment = new ShiftSegment { StartsAt = TimeSpan.FromHours(22), EndsAt = TimeSpan.FromHours(6), WorkDateRule = ShiftWorkDateRule.SegmentStartDate };
        Assert.Equal(TimeSpan.FromHours(8), ShiftScheduleRules.Duration(segment));
        Assert.Equal(new DateOnly(2026, 7, 20), ShiftWorkDateResolver.Resolve(
            new DateTime(2026, 7, 21, 2, 0, 0), segment, TimeZoneInfo.Utc));
    }

    [Fact]
    public void SplitShift_RejectsOverlappingSegmentsAndAllowsSeparatedSegments()
    {
        var overlapping = new[]
        {
            new ShiftSegment { Sequence = 1, StartsAt = TimeSpan.FromHours(8), EndsAt = TimeSpan.FromHours(12) },
            new ShiftSegment { Sequence = 2, StartsAt = TimeSpan.FromHours(11), EndsAt = TimeSpan.FromHours(15) }
        };
        var separated = new[]
        {
            new ShiftSegment { Sequence = 1, StartsAt = TimeSpan.FromHours(8), EndsAt = TimeSpan.FromHours(12) },
            new ShiftSegment { Sequence = 2, StartsAt = TimeSpan.FromHours(14), EndsAt = TimeSpan.FromHours(18) }
        };
        Assert.Contains("SHIFT_SEGMENT_OVERLAP", ShiftScheduleRules.ValidateSegments(overlapping));
        Assert.Empty(ShiftScheduleRules.ValidateSegments(separated));
    }

    [Fact]
    public async Task Publish_RejectsEmployeeDateOverlapWithoutPartialWrite()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db);
        db.ShiftAssignments.Add(new ShiftAssignment
        {
            EmployeeId = seeded.Employee.Id, ShiftTemplateId = seeded.Day.Id,
            EffectiveFrom = new DateOnly(2026, 7, 1), EffectiveTo = new DateOnly(2026, 8, 1),
            Status = ShiftAssignmentStatus.Published, PublishedByUserId = seeded.Actor.Id, Reason = "existing"
        });
        await db.SaveChangesAsync();
        var handler = new PublishShiftAssignmentsCommandHandler(db);

        var result = await handler.Handle(new PublishShiftAssignmentsCommand([
            new ShiftAssignmentInput(seeded.Employee.Id, seeded.Night.Id, new DateOnly(2026, 7, 15), null, "rotation")
        ], seeded.Actor.Id, "publish-1"), default);

        Assert.False(result.Success);
        Assert.Contains("SHIFT_ASSIGNMENT_OVERLAP", result.Errors!);
        Assert.Single(await db.ShiftAssignments.ToListAsync());
    }

    [Fact]
    public async Task CalendarUpdate_PersistsSelectedWorkingAndRestDays()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR Calendar", "01044444444");
        var calendar = new WorkCalendar { Code = "WEEK", Name = "Work week", WorkingDaysMask = 62 };
        db.WorkCalendars.Add(calendar);
        await db.SaveChangesAsync();

        const int sundayThroughThursday = 31;
        var response = await new UpdateWorkCalendarCommandHandler(db).Handle(
            new UpdateWorkCalendarCommand(calendar.Id, sundayThroughThursday, actor.Id),
            default);

        Assert.True(response.Success);
        Assert.Equal(
            sundayThroughThursday,
            await db.WorkCalendars.Where(item => item.Id == calendar.Id).Select(item => item.WorkingDaysMask).SingleAsync());
    }

    [Fact]
    public async Task ApprovedSwap_RetainsOriginalAssignmentsAndPublishesTwoReplacements()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db);
        var targetUser = await TestAppDbContextFactory.SeedUserAsync(db, "Target", "01044444443");
        var target = new EmployeeProfile { UserId = targetUser.Id, User = targetUser };
        target.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(target.Id);
        var first = NewAssignment(seeded.Employee.Id, seeded.Day.Id, seeded.Actor.Id);
        var second = NewAssignment(target.Id, seeded.Night.Id, seeded.Actor.Id);
        db.EmployeeProfiles.Add(target);
        db.ShiftAssignments.AddRange(first, second);
        await db.SaveChangesAsync();
        var submit = await new SubmitShiftSwapCommandHandler(db).Handle(
            new SubmitShiftSwapCommand(seeded.Employee.Id, first.Id, target.Id, second.Id, "family", seeded.Actor.Id), default);
        await new DecideShiftSwapCommandHandler(db).Handle(new DecideShiftSwapCommand(submit.Data, true, "manager ok", seeded.Actor.Id, false, 1), default);
        var final = await new DecideShiftSwapCommandHandler(db).Handle(new DecideShiftSwapCommand(submit.Data, true, "hr ok", seeded.Actor.Id, true, 2), default);

        Assert.True(final.Success);
        Assert.Equal(4, await db.ShiftAssignments.CountAsync());
        Assert.Equal(2, await db.ShiftAssignments.CountAsync(item => item.Status == ShiftAssignmentStatus.Superseded));
        Assert.Equal(2, await db.ShiftAssignments.CountAsync(item => item.ReplacesAssignmentId != null));
    }

    private static ShiftAssignment NewAssignment(Guid employeeId, Guid templateId, Guid actorId) => new()
    {
        EmployeeId = employeeId, ShiftTemplateId = templateId, EffectiveFrom = new DateOnly(2026, 7, 20),
        EffectiveTo = new DateOnly(2026, 7, 21), Status = ShiftAssignmentStatus.Published,
        PublishedByUserId = actorId, PublishedAt = DateTime.UtcNow, Reason = "published"
    };

    private static async Task<(User Actor, EmployeeProfile Employee, ShiftTemplate Day, ShiftTemplate Night)> SeedAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR", "01044444441");
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Employee", "01044444442");
        var employee = new EmployeeProfile { UserId = user.Id, User = user };
        employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        var calendar = new WorkCalendar { Code = "CAI", Name = "Cairo" };
        var day = new ShiftTemplate { Code = "DAY", Name = "Day", WorkCalendarId = calendar.Id, WorkCalendar = calendar };
        day.Segments.Add(new ShiftSegment { ShiftTemplateId = day.Id, Sequence = 1, StartsAt = TimeSpan.FromHours(9), EndsAt = TimeSpan.FromHours(17) });
        var night = new ShiftTemplate { Code = "NIGHT", Name = "Night", WorkCalendarId = calendar.Id, WorkCalendar = calendar };
        night.Segments.Add(new ShiftSegment { ShiftTemplateId = night.Id, Sequence = 1, StartsAt = TimeSpan.FromHours(22), EndsAt = TimeSpan.FromHours(6) });
        db.EmployeeProfiles.Add(employee); db.WorkCalendars.Add(calendar); db.ShiftTemplates.AddRange(day, night);
        await db.SaveChangesAsync();
        return (actor, employee, day, night);
    }
}
