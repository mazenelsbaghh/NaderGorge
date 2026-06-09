using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Operations.Commands;
using NaderGorge.Application.Features.Operations.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Application.Tests.Operations;

public class TaskTests
{
    [Fact]
    public async Task CreateTask_ThrowsException_ForStudentAssignee()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Student Assignee
        var studentUser = await TestAppDbContextFactory.SeedUserAsync(db, "Student User", "01011111111");
        var studentRole = new Role { Id = Guid.NewGuid(), Name = "Student", Type = RoleType.Student };
        db.Roles.Add(studentRole);
        db.UserRoles.Add(new UserRole { UserId = studentUser.Id, RoleId = studentRole.Id });

        // 2. Seed Creator (Admin)
        var creatorUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin Creator", "01022222222");
        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
        db.Roles.Add(adminRole);
        db.UserRoles.Add(new UserRole { UserId = creatorUser.Id, RoleId = adminRole.Id });

        await db.SaveChangesAsync();

        var handler = new CreateTaskCommandHandler(db);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await handler.Handle(
                new CreateTaskCommand("Test Task", "Description", studentUser.Id, TaskPriority.High, DateTime.UtcNow.AddDays(1), creatorUser.Id),
                CancellationToken.None);
        });

        Assert.Equal("Cannot assign tasks to student users.", exception.Message);
    }

    [Fact]
    public async Task CreateTask_Succeeds_ForStaffAssignee()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Staff Assignee
        var staffUser = await TestAppDbContextFactory.SeedUserAsync(db, "Staff User", "01033333333");
        var staffRole = new Role { Id = Guid.NewGuid(), Name = "Staff", Type = RoleType.Staff };
        db.Roles.Add(staffRole);
        db.UserRoles.Add(new UserRole { UserId = staffUser.Id, RoleId = staffRole.Id });

        // 2. Seed Creator (Admin)
        var creatorUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin Creator", "01044444444");
        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
        db.Roles.Add(adminRole);
        db.UserRoles.Add(new UserRole { UserId = creatorUser.Id, RoleId = adminRole.Id });

        await db.SaveChangesAsync();

        var handler = new CreateTaskCommandHandler(db);

        // Act
        var result = await handler.Handle(
            new CreateTaskCommand("Staff Task", "Description", staffUser.Id, TaskPriority.Medium, DateTime.UtcNow.AddDays(1), creatorUser.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.Data);

        var task = await db.TaskItems.FirstOrDefaultAsync(t => t.Id == result.Data);
        Assert.NotNull(task);
        Assert.Equal("Staff Task", task!.Title);
        Assert.Equal(staffUser.Id, task.AssigneeId);
        Assert.Equal(TaskStatus.New, task.Status);
    }

    [Fact]
    public async Task UpdateTaskStatus_RestrictsNonManagers_FromDowngradingReviewOrCompleted()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Assistant User (Non-manager)
        var assistantUser = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant User", "01055555555");
        var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };
        db.Roles.Add(assistantRole);
        db.UserRoles.Add(new UserRole { UserId = assistantUser.Id, RoleId = assistantRole.Id });

        // 2. Seed Task in Review
        var task = new TaskItem
        {
            Title = "Task in Review",
            AssigneeId = assistantUser.Id,
            CreatedById = Guid.NewGuid(),
            Status = TaskStatus.Review
        };
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        var handler = new UpdateTaskStatusCommandHandler(db);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await handler.Handle(
                new UpdateTaskStatusCommand(task.Id, TaskStatus.InProgress, assistantUser.Id),
                CancellationToken.None);
        });

        Assert.Contains("Only managers can change the status of tasks in Review or Completed status", exception.Message);
    }

    [Fact]
    public async Task UpdateTaskStatus_RestrictsNonManagers_FromDirectCompletion()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Assistant User (Non-manager)
        var assistantUser = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant User", "01066666666");
        var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };
        db.Roles.Add(assistantRole);
        db.UserRoles.Add(new UserRole { UserId = assistantUser.Id, RoleId = assistantRole.Id });

        // 2. Seed Task in InProgress
        var task = new TaskItem
        {
            Title = "Active Task",
            AssigneeId = assistantUser.Id,
            CreatedById = Guid.NewGuid(),
            Status = TaskStatus.InProgress
        };
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        var handler = new UpdateTaskStatusCommandHandler(db);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await handler.Handle(
                new UpdateTaskStatusCommand(task.Id, TaskStatus.Completed, assistantUser.Id),
                CancellationToken.None);
        });

        Assert.Contains("Task completion requires manager approval", exception.Message);
    }

    [Fact]
    public async Task AdminResolveApproval_Approve_TransitionsToCompleted()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Supervisor (Manager)
        var supervisorUser = await TestAppDbContextFactory.SeedUserAsync(db, "Supervisor User", "01077777777");
        var supervisorRole = new Role { Id = Guid.NewGuid(), Name = "Supervisor", Type = RoleType.Supervisor };
        db.Roles.Add(supervisorRole);
        db.UserRoles.Add(new UserRole { UserId = supervisorUser.Id, RoleId = supervisorRole.Id });

        // 2. Seed Task in Review
        var task = new TaskItem
        {
            Title = "Task to Approve",
            AssigneeId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
            Status = TaskStatus.Review
        };
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        var handler = new AdminResolveApprovalCommandHandler(db);

        // Act
        var result = await handler.Handle(
            new AdminResolveApprovalCommand(task.Id, supervisorUser.Id, Approve: true),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var updatedTask = await db.TaskItems.FirstOrDefaultAsync(t => t.Id == task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Completed, updatedTask!.Status);
        Assert.NotNull(updatedTask.CompletedAt);
        Assert.Equal(supervisorUser.Id, updatedTask.ApprovedById);
    }

    [Fact]
    public async Task AdminResolveApproval_Reject_TransitionsToInProgress_AndAddsComment()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Admin (Manager)
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01088888888");
        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
        db.Roles.Add(adminRole);
        db.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });

        // 2. Seed Task in Review
        var task = new TaskItem
        {
            Title = "Task to Reject",
            AssigneeId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
            Status = TaskStatus.Review
        };
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        var handler = new AdminResolveApprovalCommandHandler(db);

        // Act
        var result = await handler.Handle(
            new AdminResolveApprovalCommand(task.Id, adminUser.Id, Approve: false, RejectionReason: "Fix typo"),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var updatedTask = await db.TaskItems
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.InProgress, updatedTask!.Status);
        Assert.Null(updatedTask.CompletedAt);
        Assert.Null(updatedTask.ApprovedById);

        Assert.Single(updatedTask.Comments);
        var comment = updatedTask.Comments.First();
        Assert.Contains("Task completion rejected by Admin User", comment.Content);
        Assert.Contains("Reason: Fix typo", comment.Content);
    }

    [Fact]
    public async Task GetTaskDetailsQuery_ThrowsException_ForUnauthorizedAssistant()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Assistant User
        var assistantUser = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant User", "01099999999");
        var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };
        db.Roles.Add(assistantRole);
        db.UserRoles.Add(new UserRole { UserId = assistantUser.Id, RoleId = assistantRole.Id });

        // 2. Seed other users for Assignee and Creator
        var otherAssignee = await TestAppDbContextFactory.SeedUserAsync(db, "Other Assignee", "01099999991");
        var otherCreator = await TestAppDbContextFactory.SeedUserAsync(db, "Other Creator", "01099999992");

        // 3. Seed Task assigned to someone else
        var task = new TaskItem
        {
            Title = "Task For Someone Else",
            AssigneeId = otherAssignee.Id,
            CreatedById = otherCreator.Id,
            Status = TaskStatus.New
        };
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        var handler = new GetTaskDetailsQueryHandler(db);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await handler.Handle(new GetTaskDetailsQuery(task.Id, assistantUser.Id, IsAdminOrSupervisor: false), CancellationToken.None);
        });
    }

    [Fact]
    public async Task AddTaskCommentCommand_ThrowsException_ForUnauthorizedAssistant()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Assistant User
        var assistantUser = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant User", "01100000000");
        var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };
        db.Roles.Add(assistantRole);
        db.UserRoles.Add(new UserRole { UserId = assistantUser.Id, RoleId = assistantRole.Id });

        // 2. Seed Task assigned to someone else
        var task = new TaskItem
        {
            Title = "Task For Someone Else",
            AssigneeId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
            Status = TaskStatus.New
        };
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        var handler = new AddTaskCommentCommandHandler(db);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await handler.Handle(new AddTaskCommentCommand(task.Id, assistantUser.Id, "Hello"), CancellationToken.None);
        });
    }

    [Fact]
    public async Task UpdateTaskStatus_ThrowsException_ForUnauthorizedAssistantUpdatingOtherTask()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Assistant User
        var assistantUser = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant User", "01122222222");
        var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };
        db.Roles.Add(assistantRole);
        db.UserRoles.Add(new UserRole { UserId = assistantUser.Id, RoleId = assistantRole.Id });

        // 2. Seed Task assigned to someone else
        var task = new TaskItem
        {
            Title = "Task For Someone Else",
            AssigneeId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
            Status = TaskStatus.InProgress
        };
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        var handler = new UpdateTaskStatusCommandHandler(db);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await handler.Handle(new UpdateTaskStatusCommand(task.Id, TaskStatus.Review, assistantUser.Id), CancellationToken.None);
        });
    }
}
