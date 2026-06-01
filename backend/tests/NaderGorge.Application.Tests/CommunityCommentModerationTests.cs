using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Community.Commands;
using NaderGorge.Application.Features.Community.Queries;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class CommunityCommentModerationTests
{
    [Fact]
    public async Task CreateCommunityComment_SetsPendingAndHidesItFromStudentQuery()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var author = await TestAppDbContextFactory.SeedUserAsync(db, "Author", "100");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "200");
        var post = await TestAppDbContextFactory.SeedApprovedCommunityPostAsync(db, author);

        var createHandler = new CreateCommunityPostCommentCommandHandler(db);
        var createResult = await createHandler.Handle(new CreateCommunityPostCommentCommand(post.Id, student.Id, "Needs review"), CancellationToken.None);

        Assert.True(createResult.Success);
        Assert.Equal("Pending", createResult.Data!.Status);

        var savedComment = db.CommunityPostComments.Single();
        Assert.Equal(CommunityCommentStatus.Pending, savedComment.Status);

        var getCommentsHandler = new GetCommunityPostCommentsQueryHandler(db);
        var studentView = await getCommentsHandler.Handle(new GetCommunityPostCommentsQuery(post.Id, student.Id), CancellationToken.None);

        Assert.True(studentView.Success);
        Assert.Empty(studentView.Data!);
    }

    [Fact]
    public async Task ApproveAndRejectCommunityComment_UpdateModerationQueueAndVisibility()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var author = await TestAppDbContextFactory.SeedUserAsync(db, "Author", "101");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "202");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "303");
        var post = await TestAppDbContextFactory.SeedApprovedCommunityPostAsync(db, author);

        var comment = new NaderGorge.Domain.Entities.CommunityPostComment
        {
            PostId = post.Id,
            AuthorUserId = student.Id,
            Body = "Pending comment",
            Status = CommunityCommentStatus.Pending
        };
        db.CommunityPostComments.Add(comment);
        await db.SaveChangesAsync();

        var pendingHandler = new GetCommunityCommentsForModerationQueryHandler(db);
        var pending = await pendingHandler.Handle(new GetCommunityCommentsForModerationQuery(), CancellationToken.None);
        Assert.Single(pending.Data!);

        var approveHandler = new ApproveCommunityCommentCommandHandler(db);
        var approve = await approveHandler.Handle(new ApproveCommunityCommentCommand(comment.Id, admin.Id), CancellationToken.None);
        Assert.True(approve.Success);

        var approvedStudentView = await new GetCommunityPostCommentsQueryHandler(db)
            .Handle(new GetCommunityPostCommentsQuery(post.Id, student.Id), CancellationToken.None);
        Assert.Single(approvedStudentView.Data!);

        comment.Status = CommunityCommentStatus.Pending;
        comment.RejectionReason = null;
        await db.SaveChangesAsync();

        var rejectHandler = new RejectCommunityCommentCommandHandler(db);
        var reject = await rejectHandler.Handle(new RejectCommunityCommentCommand(comment.Id, admin.Id, "Not appropriate"), CancellationToken.None);
        Assert.True(reject.Success);

        var rejectedStudentView = await new GetCommunityPostCommentsQueryHandler(db)
            .Handle(new GetCommunityPostCommentsQuery(post.Id, student.Id), CancellationToken.None);
        Assert.Empty(rejectedStudentView.Data!);
        Assert.Equal("Not appropriate", db.CommunityPostComments.Single().RejectionReason);
    }
}
