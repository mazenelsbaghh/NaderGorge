using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAICommunityPostApprovalOperationPostgresTests
{
    [Fact]
    public async Task ReplayAfterLaterModeration_ReturnsOriginalResultWithoutAnotherPublication()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var admin = new User
        {
            FullName = "Community review admin", PhoneNumber = "01000007911", PasswordHash = "test"
        };
        var teacherUser = new User
        {
            FullName = "Community review teacher", PhoneNumber = "01000007912", PasswordHash = "test"
        };
        var teacher = new TeacherProfile { User = teacherUser };
        var post = new CommunityPost
        {
            AuthorUser = teacherUser, Teacher = teacher, Body = "Public post", Status = CommunityPostStatus.Pending
        };
        db.AddRange(admin, teacherUser, teacher, post);
        await db.SaveChangesAsync();

        var operationId = Guid.NewGuid().ToString("N");
        var command = new ApproveCommunityPostCommand(post.Id, admin.Id) { OperationId = operationId };
        var handler = new ApproveCommunityPostCommandHandler(db, new AcademicScopeService(db));
        var approved = await handler.Handle(command, default);
        Assert.True(approved.Success);
        Assert.Equal("Approved", approved.Data?.Status);

        post.Status = CommunityPostStatus.Rejected;
        await db.SaveChangesAsync();
        await using var retryDb = fixture.CreateDbContext();
        var retryHandler = new ApproveCommunityPostCommandHandler(retryDb, new AcademicScopeService(retryDb));
        var replay = await retryHandler.Handle(command, default);
        Assert.True(replay.Success);
        Assert.Equal(approved.Data, replay.Data);
        var conflict = await retryHandler.Handle(command with { ReviewerUserId = teacherUser.Id }, default);
        Assert.False(conflict.Success);
        Assert.Contains("IDEMPOTENCY_CONFLICT", conflict.Errors!);

        await using var verifyDb = fixture.CreateDbContext();
        Assert.Equal(CommunityPostStatus.Rejected,
            (await verifyDb.CommunityPosts.AsNoTracking().SingleAsync(item => item.Id == post.Id)).Status);
        Assert.Equal(1, await verifyDb.AuditLogs.CountAsync(item => item.Action == "ApproveCommunityPost"));
        Assert.Equal(1, await verifyDb.OutboxEvents.CountAsync(item => item.Type == "CommunityPostApproved"));
        Assert.Equal(1, await verifyDb.AuthoritativeOperationReceipts.CountAsync(item => item.OperationId == operationId));
        var resolver = new AdminAICommunityPostApprovalResultResolver(verifyDb);
        var resolved = await resolver.ResolveAsync(operationId, operationId, default);
        Assert.Equal(approved.Data, Assert.IsType<ModerateCommunityPostResponse>(resolved?.SafeResult));
        Assert.Null(await resolver.ResolveAsync(operationId, "wrong-execution", default));
    }
}
