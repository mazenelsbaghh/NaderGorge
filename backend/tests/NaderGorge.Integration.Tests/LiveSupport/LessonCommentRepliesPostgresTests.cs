using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Content.Commands;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class LessonCommentRepliesPostgresTests
{
    [Fact]
    public async Task RepliesAreThreadedAndOnlyApprovedOrOwnPendingAreVisible()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var (root, student, other, teacher) = await SeedAsync(fixture.Db);
        var db = fixture.Db;
        var create = new CreateLessonCommentCommandHandler(db, new AccessCheckService(db));
        var first = await create.Handle(new(root.LessonId, student.Id, "  رد الطالب  ", root.Id), default);
        var privateReply = await create.Handle(new(root.LessonId, other.Id, "رد خاص قيد المراجعة", root.Id), default);
        Assert.True(first.Success);
        Assert.True(privateReply.Success);
        var published = await new ReplyToLessonCommentCommandHandler(db)
            .Handle(new(root.Id, teacher.UserId, "رد المدرس", teacher.Id), default);
        Assert.True(published.Success);
        Assert.Equal(root.Id, published.Data!.ParentCommentId);
        Assert.Equal(root.Body, published.Data.ParentBody);

        db.ChangeTracker.Clear();
        var stored = await db.LessonComments.SingleAsync(c => c.Id == first.Data!.Id);
        Assert.Equal("رد الطالب", stored.Body);
        Assert.Equal(root.Id, stored.ParentCommentId);
        Assert.Equal(LessonCommentStatus.Pending, stored.Status);
        var read = new GetLessonCommentsQueryHandler(db, new AccessCheckService(db));
        var roots = await read.Handle(new(root.LessonId, student.Id), default);
        Assert.Equal(root.Id, Assert.Single(roots.Data!).Id);
        Assert.Equal(2, roots.Data![0].ReplyCount);
        var replies = await read.Handle(new(root.LessonId, student.Id, ParentCommentId: root.Id), default);
        Assert.Equal(new[] { first.Data!.Id, published.Data.Id }, replies.Data!.Select(c => c.Id));
        Assert.DoesNotContain(replies.Data!, c => c.Id == privateReply.Data!.Id);
        var moderation = await new GetLessonCommentsForModerationQueryHandler(db).Handle(new(root.LessonId), default);
        Assert.Equal(root.Body, moderation.Data!.Single(c => c.Id == first.Data.Id).ParentBody);
        Assert.True(await db.OutboxEvents.AnyAsync(e => e.Type == "LessonCommentApproved"
            && e.TargetGroup == $"Lesson_{root.LessonId}"));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("rejected")]
    [InlineData("other-lesson")]
    [InlineData("nested-reply")]
    public async Task StudentCannotReadOrReplyToUnavailableParent(string unavailable)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var (root, student, _, _) = await SeedAsync(fixture.Db);
        var targetLessonId = root.LessonId;
        if (unavailable == "pending") root.Status = LessonCommentStatus.Pending;
        if (unavailable == "rejected") root.Status = LessonCommentStatus.Rejected;
        if (unavailable == "other-lesson")
        {
            var otherLesson = new Lesson { Title = "Other", ContentSectionId = root.Lesson.ContentSectionId };
            fixture.Db.Lessons.Add(otherLesson);
            fixture.Db.StudentAccessGrants.Add(new() { UserId = student.Id, LessonId = otherLesson.Id, GrantType = CodeType.Lesson });
            targetLessonId = otherLesson.Id;
        }
        if (unavailable == "nested-reply")
        {
            var parent = new LessonComment { LessonId = root.LessonId, AuthorUserId = student.Id, Body = "Root", Status = LessonCommentStatus.Approved };
            fixture.Db.LessonComments.Add(parent);
            root.ParentCommentId = parent.Id;
        }
        await fixture.Db.SaveChangesAsync();
        var count = await fixture.Db.LessonComments.CountAsync();
        var access = new AccessCheckService(fixture.Db);
        var create = await new CreateLessonCommentCommandHandler(fixture.Db, access)
            .Handle(new(targetLessonId, student.Id, "must not save", root.Id), default);
        var read = await new GetLessonCommentsQueryHandler(fixture.Db, access)
            .Handle(new(targetLessonId, student.Id, ParentCommentId: root.Id), default);
        Assert.False(create.Success);
        Assert.Contains("NOT_FOUND", create.Errors!);
        Assert.False(read.Success);
        Assert.Null(read.Data);
        Assert.Equal(count, await fixture.Db.LessonComments.CountAsync());
    }

    [Fact]
    public async Task AccessRevocationAndWrongTeacherCannotReadOrWriteReplies()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var (root, student, _, teacher) = await SeedAsync(fixture.Db);
        await fixture.Db.StudentAccessGrants.Where(g => g.UserId == student.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsActive, false));
        var access = new AccessCheckService(fixture.Db);
        var create = await new CreateLessonCommentCommandHandler(fixture.Db, access)
            .Handle(new(root.LessonId, student.Id, "No access", root.Id), default);
        var read = await new GetLessonCommentsQueryHandler(fixture.Db, access)
            .Handle(new(root.LessonId, student.Id, ParentCommentId: root.Id), default);
        var wrongTeacher = await new ReplyToLessonCommentCommandHandler(fixture.Db)
            .Handle(new(root.Id, teacher.UserId, "Wrong scope", Guid.NewGuid()), default);
        Assert.Contains("FORBIDDEN", create.Errors!);
        Assert.Contains("FORBIDDEN", read.Errors!);
        Assert.Contains("NOT_FOUND", wrongTeacher.Errors!);
        Assert.False(await fixture.Db.LessonComments.AnyAsync(c => c.ParentCommentId == root.Id));
    }

    [Fact]
    public async Task ApprovingReplyPublishesItButCannotPublishIntoRejectedThread()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var (root, student, other, teacher) = await SeedAsync(fixture.Db);
        var db = fixture.Db;
        var create = new CreateLessonCommentCommandHandler(db, new AccessCheckService(db));
        var reply = await create.Handle(new(root.LessonId, student.Id, "Publish me", root.Id), default);
        var approve = new ApproveLessonCommentCommandHandler(db);
        Assert.True((await approve.Handle(new(reply.Data!.Id, teacher.UserId), default)).Success);
        var read = await new GetLessonCommentsQueryHandler(db, new AccessCheckService(db))
            .Handle(new(root.LessonId, other.Id, ParentCommentId: root.Id), default);
        Assert.Equal(reply.Data.Id, Assert.Single(read.Data!).Id);
        var pending = await create.Handle(new(root.LessonId, student.Id, "Keep private", root.Id), default);
        root.Status = LessonCommentStatus.Rejected;
        await db.SaveChangesAsync();
        var eventsBefore = await db.OutboxEvents.CountAsync();
        var blocked = await approve.Handle(new(pending.Data!.Id, teacher.UserId), default);
        Assert.False(blocked.Success);
        Assert.Contains("PARENT_NOT_APPROVED", blocked.Errors!);
        Assert.Equal(eventsBefore, await db.OutboxEvents.CountAsync());
        Assert.Equal(LessonCommentStatus.Pending, (await db.LessonComments.FindAsync(pending.Data.Id))!.Status);
    }

    [Fact]
    public async Task AdminReplyToReplyStaysInRootThreadAndParentDeletionCascades()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var (root, student, _, teacher) = await SeedAsync(fixture.Db);
        var command = new ReplyToLessonCommentCommandHandler(fixture.Db);
        var first = await command.Handle(new(root.Id, teacher.UserId, "One"), default);
        var second = await command.Handle(new(first.Data!.Id, teacher.UserId, "Two"), default);
        Assert.True(second.Success);
        Assert.Equal(root.Id, second.Data!.ParentCommentId);
        var query = new GetLessonCommentsQueryHandler(fixture.Db, new AccessCheckService(fixture.Db));
        var pageOne = await query.Handle(new(root.LessonId, student.Id, 0, 1, root.Id), default);
        var pageTwo = await query.Handle(new(root.LessonId, student.Id, 1, 1, root.Id), default);
        Assert.Equal(first.Data.Id, Assert.Single(pageOne.Data!).Id);
        Assert.Equal(second.Data.Id, Assert.Single(pageTwo.Data!).Id);
        root.Status = LessonCommentStatus.Rejected;
        await fixture.Db.SaveChangesAsync();
        Assert.False((await query.Handle(new(root.LessonId, student.Id, ParentCommentId: root.Id), default)).Success);
        Assert.False((await command.Handle(new(first.Data.Id, teacher.UserId, "Rejected parent"), default)).Success);
        await fixture.Db.LessonComments.Where(c => c.Id == root.Id).ExecuteDeleteAsync();
        Assert.False(await fixture.Db.LessonComments.AnyAsync(c => c.LessonId == root.LessonId));
    }

    private static async Task<(LessonComment Root, User Student, User Other, TeacherProfile Teacher)> SeedAsync(AppDbContext db)
    {
        var student = NewUser("Student");
        var other = NewUser("Other student");
        var teacher = new TeacherProfile { User = NewUser("Teacher"), IsContentVisibleToStudents = true };
        var package = new Package { Name = "Comments", Teacher = teacher,
            Subject = new Subject { Name = "Comments", NormalizedName = Guid.NewGuid().ToString("N") } };
        var lesson = new Lesson { Title = "Discussion", ContentSection = new ContentSection
            { Title = "Section", Term = new Term { Title = "Term", Package = package } } };
        var root = new LessonComment { Lesson = lesson, AuthorUser = student, Body = "Original question", Status = LessonCommentStatus.Approved };
        db.LessonComments.Add(root);
        db.Users.Add(other);
        db.StudentAccessGrants.AddRange(new StudentAccessGrant { User = student, LessonId = lesson.Id, GrantType = CodeType.Lesson },
            new StudentAccessGrant { User = other, LessonId = lesson.Id, GrantType = CodeType.Lesson });
        await db.SaveChangesAsync();
        return (root, student, other, teacher);
    }

    private static User NewUser(string name) => new() { FullName = name, PasswordHash = "not-used",
        PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}" };
}
