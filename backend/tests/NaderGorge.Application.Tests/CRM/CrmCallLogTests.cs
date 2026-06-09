using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.CRM.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.CRM;

public class CrmCallLogTests
{
    private async Task<(User Student, User Assistant, Role StudentRole, Role AssistantRole)> SeedBasicUsersAsync(AppDbContext db)
    {
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student User", "01234567800");
        var assistant = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant User", "01234567801");

        var studentRole = new Role { Id = Guid.NewGuid(), Name = "Student", Type = RoleType.Student };
        var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };

        db.Roles.AddRange(studentRole, assistantRole);
        db.UserRoles.AddRange(
            new UserRole { UserId = student.Id, RoleId = studentRole.Id },
            new UserRole { UserId = assistant.Id, RoleId = assistantRole.Id }
        );
        await db.SaveChangesAsync();

        return (student, assistant, studentRole, assistantRole);
    }

    [Fact]
    public async Task AssignStudentToAgent_CreatesNewCrmStudentStatus_WhenNoneExists()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student, assistant, _, _) = await SeedBasicUsersAsync(db);

        var handler = new AssignStudentToAgentCommandHandler(db);
        var command = new AssignStudentToAgentCommand(student.Id, assistant.Id, CrmPriority.High, "Initial notes");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        var status = await db.CrmStudentStatuses.FirstOrDefaultAsync(s => s.StudentId == student.Id);
        Assert.NotNull(status);
        Assert.Equal(CrmStatus.Assigned, status!.Status);
        Assert.Equal(assistant.Id, status.AssignedAgentId);
        Assert.Equal(CrmPriority.High, status.Priority);
        Assert.Equal("Initial notes", status.Notes);
    }

    [Fact]
    public async Task AssignStudentToAgent_UpdatesCrmStudentStatus_WhenAlreadyExists()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student, assistant, _, _) = await SeedBasicUsersAsync(db);

        db.CrmStudentStatuses.Add(new CrmStudentStatus
        {
            StudentId = student.Id,
            Status = CrmStatus.Unassigned,
            AssignedAgentId = null,
            Priority = CrmPriority.Low,
            Notes = "Old notes"
        });
        await db.SaveChangesAsync();

        var handler = new AssignStudentToAgentCommandHandler(db);
        var command = new AssignStudentToAgentCommand(student.Id, assistant.Id, CrmPriority.Critical, "Updated notes");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        var status = await db.CrmStudentStatuses.FirstOrDefaultAsync(s => s.StudentId == student.Id);
        Assert.NotNull(status);
        Assert.Equal(CrmStatus.Assigned, status!.Status);
        Assert.Equal(assistant.Id, status.AssignedAgentId);
        Assert.Equal(CrmPriority.Critical, status.Priority);
        Assert.Equal("Updated notes", status.Notes);
    }

    [Fact]
    public async Task LogCrmCall_CreatesLogAndUpdatesStatusToInProgress_WhenOutcomeIsNotClosed()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student, assistant, _, _) = await SeedBasicUsersAsync(db);

        db.CrmStudentStatuses.Add(new CrmStudentStatus
        {
            StudentId = student.Id,
            Status = CrmStatus.Assigned,
            AssignedAgentId = assistant.Id,
            Priority = CrmPriority.Medium
        });
        await db.SaveChangesAsync();

        var nextFollowUp = DateTime.UtcNow.AddDays(3);
        var handler = new LogCrmCallCommandHandler(db);
        var command = new LogCrmCallCommand(student.Id, assistant.Id, CallOutcome.Completed, "Called mother, agreed to study plan", nextFollowUp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        // Verify Call Log
        var callLog = await db.CrmCallLogs.FirstOrDefaultAsync(c => c.StudentId == student.Id);
        Assert.NotNull(callLog);
        Assert.Equal(assistant.Id, callLog!.AgentId);
        Assert.Equal(CallOutcome.Completed, callLog.Outcome);
        Assert.Equal("Called mother, agreed to study plan", callLog.Notes);
        Assert.Equal(nextFollowUp, callLog.NextFollowUpDate);

        // Verify status transition
        var status = await db.CrmStudentStatuses.FirstOrDefaultAsync(s => s.StudentId == student.Id);
        Assert.NotNull(status);
        Assert.Equal(CrmStatus.InProgress, status!.Status);
        Assert.Equal(assistant.Id, status.AssignedAgentId);
        Assert.NotNull(status.LastCalledAt);
        Assert.Equal(nextFollowUp, status.NextFollowUpDate);
    }

    [Fact]
    public async Task LogCrmCall_CreatesLogAndUpdatesStatusToClosed_WhenOutcomeIsClosed()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student, assistant, _, _) = await SeedBasicUsersAsync(db);

        db.CrmStudentStatuses.Add(new CrmStudentStatus
        {
            StudentId = student.Id,
            Status = CrmStatus.InProgress,
            AssignedAgentId = assistant.Id,
            Priority = CrmPriority.Medium
        });
        await db.SaveChangesAsync();

        var handler = new LogCrmCallCommandHandler(db);
        var command = new LogCrmCallCommand(student.Id, assistant.Id, CallOutcome.Closed, "Parent requested stop follow-up", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        // Verify status transition
        var status = await db.CrmStudentStatuses.FirstOrDefaultAsync(s => s.StudentId == student.Id);
        Assert.NotNull(status);
        Assert.Equal(CrmStatus.Closed, status!.Status);
        Assert.Null(status.NextFollowUpDate);
    }

    [Fact]
    public async Task LogCrmCall_AssignsAgentOnTheFly_IfStudentWasUnassigned()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student, assistant, _, _) = await SeedBasicUsersAsync(db);

        db.CrmStudentStatuses.Add(new CrmStudentStatus
        {
            StudentId = student.Id,
            Status = CrmStatus.Unassigned,
            AssignedAgentId = null,
            Priority = CrmPriority.Medium
        });
        await db.SaveChangesAsync();

        var handler = new LogCrmCallCommandHandler(db);
        var command = new LogCrmCallCommand(student.Id, assistant.Id, CallOutcome.NoAnswer, "No answer", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        var status = await db.CrmStudentStatuses.FirstOrDefaultAsync(s => s.StudentId == student.Id);
        Assert.NotNull(status);
        Assert.Equal(assistant.Id, status!.AssignedAgentId);
        Assert.Equal(CrmStatus.InProgress, status.Status);
    }
}
