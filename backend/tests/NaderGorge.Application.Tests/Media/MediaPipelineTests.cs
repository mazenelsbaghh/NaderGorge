using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Media.Commands;
using NaderGorge.Application.Features.Operations.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using Xunit;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Application.Tests.Media;

public class MediaPipelineTests
{
    [Fact]
    public async Task UpdatePipeline_TransitionsToPublished_Fails_IfStageIsNotApproved()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Media Pipeline in Filming stage
        var pipeline = new MediaProductionPipeline
        {
            Id = Guid.NewGuid(),
            Title = "Test Video",
            Stage = MediaStage.Filming,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaProductionPipelines.Add(pipeline);
        await db.SaveChangesAsync();

        var handler = new UpdateMediaPipelineCommandHandler(db);

        // Act
        var result = await handler.Handle(
            new UpdateMediaPipelineCommand(
                pipeline.Id,
                Guid.NewGuid(), // random user
                "Test Video",
                "Description",
                null,
                "folder",
                0,
                MediaStage.Published,
                null
            ),
            CancellationToken.None
        );

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Cannot publish content that has not been approved.", result.Message);

        var dbPipeline = await db.MediaProductionPipelines.FindAsync(pipeline.Id);
        Assert.Equal(MediaStage.Filming, dbPipeline!.Stage);
    }

    [Fact]
    public async Task UpdatePipeline_TransitionsToReview_CreatesTaskItem_LinkedToPipeline()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Media Pipeline in Editing stage
        var pipeline = new MediaProductionPipeline
        {
            Id = Guid.NewGuid(),
            Title = "Editing Video",
            Stage = MediaStage.Editing,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaProductionPipelines.Add(pipeline);

        // 2. Seed Supervisor (Manager)
        var supervisor = await TestAppDbContextFactory.SeedUserAsync(db, "Supervisor User", "01099999999");
        var supervisorRole = new Role { Id = Guid.NewGuid(), Name = "Supervisor", Type = RoleType.Supervisor };
        db.Roles.Add(supervisorRole);
        db.UserRoles.Add(new UserRole { UserId = supervisor.Id, RoleId = supervisorRole.Id });

        await db.SaveChangesAsync();

        var handler = new UpdateMediaPipelineCommandHandler(db);

        // Act
        var result = await handler.Handle(
            new UpdateMediaPipelineCommand(
                pipeline.Id,
                Guid.NewGuid(),
                "Editing Video",
                "Description",
                null,
                "http://folder.url",
                0,
                MediaStage.Review,
                supervisor.Id
            ),
            CancellationToken.None
        );

        // Assert
        Assert.True(result.Success);
        
        var dbPipeline = await db.MediaProductionPipelines.FindAsync(pipeline.Id);
        Assert.Equal(MediaStage.Review, dbPipeline!.Stage);

        // Verify TaskItem was created
        var task = await db.TaskItems.FirstOrDefaultAsync(t => t.MediaPipelineId == pipeline.Id);
        Assert.NotNull(task);
        Assert.Equal($"مراجعة محتوى: {pipeline.Title}", task!.Title);
        Assert.Equal(TaskStatus.Review, task.Status);
        Assert.Equal(supervisor.Id, task.AssigneeId);
        Assert.Contains("http://folder.url", task.Description);
    }

    [Fact]
    public async Task ResolveApproval_Approve_TransitionsLinkedMediaPipeline_ToApproved()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Media Pipeline in Review stage
        var pipeline = new MediaProductionPipeline
        {
            Id = Guid.NewGuid(),
            Title = "Under Review Video",
            Stage = MediaStage.Review,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaProductionPipelines.Add(pipeline);

        // 2. Seed Task in Review linked to Pipeline
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Task to Review Video",
            AssigneeId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
            Status = TaskStatus.Review,
            MediaPipelineId = pipeline.Id
        };
        db.TaskItems.Add(task);

        // 3. Seed Supervisor User
        var supervisor = await TestAppDbContextFactory.SeedUserAsync(db, "Supervisor User", "01099999999");
        var supervisorRole = new Role { Id = Guid.NewGuid(), Name = "Supervisor", Type = RoleType.Supervisor };
        db.Roles.Add(supervisorRole);
        db.UserRoles.Add(new UserRole { UserId = supervisor.Id, RoleId = supervisorRole.Id });

        await db.SaveChangesAsync();

        var handler = new AdminResolveApprovalCommandHandler(db);

        // Act
        var result = await handler.Handle(
            new AdminResolveApprovalCommand(task.Id, supervisor.Id, Approve: true),
            CancellationToken.None
        );

        // Assert
        Assert.True(result.Success);
        
        var dbPipeline = await db.MediaProductionPipelines.FindAsync(pipeline.Id);
        Assert.Equal(MediaStage.Approved, dbPipeline!.Stage);

        var dbTask = await db.TaskItems.FindAsync(task.Id);
        Assert.Equal(TaskStatus.Completed, dbTask!.Status);
    }

    [Fact]
    public async Task ResolveApproval_Reject_RevertsLinkedMediaPipeline_ToEditing()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Media Pipeline in Review stage
        var pipeline = new MediaProductionPipeline
        {
            Id = Guid.NewGuid(),
            Title = "Under Review Video",
            Stage = MediaStage.Review,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaProductionPipelines.Add(pipeline);

        // 2. Seed Task in Review linked to Pipeline
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Task to Review Video",
            AssigneeId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
            Status = TaskStatus.Review,
            MediaPipelineId = pipeline.Id
        };
        db.TaskItems.Add(task);

        // 3. Seed Supervisor User
        var supervisor = await TestAppDbContextFactory.SeedUserAsync(db, "Supervisor User", "01099999999");
        var supervisorRole = new Role { Id = Guid.NewGuid(), Name = "Supervisor", Type = RoleType.Supervisor };
        db.Roles.Add(supervisorRole);
        db.UserRoles.Add(new UserRole { UserId = supervisor.Id, RoleId = supervisorRole.Id });

        await db.SaveChangesAsync();

        var handler = new AdminResolveApprovalCommandHandler(db);

        // Act
        var result = await handler.Handle(
            new AdminResolveApprovalCommand(task.Id, supervisor.Id, Approve: false, RejectionReason: "Sound quality issues"),
            CancellationToken.None
        );

        // Assert
        Assert.True(result.Success);
        
        var dbPipeline = await db.MediaProductionPipelines.FindAsync(pipeline.Id);
        Assert.Equal(MediaStage.Editing, dbPipeline!.Stage);

        var dbTask = await db.TaskItems.FindAsync(task.Id);
        Assert.Equal(TaskStatus.InProgress, dbTask!.Status);
    }
}
