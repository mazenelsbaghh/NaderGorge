using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIIdentityContentPreviewTests
{
    [Fact]
    public async Task FiveIdentityAndContentPreviews_ReadAuthoritativeStateWithoutBusinessWrites()
    {
        await using var db = CreateDb();
        var user = new User { FullName = "Preview target", PhoneNumber = "01000000171", PasswordHash = "test" };
        var subject = new Subject { Name = "Math", NormalizedName = "MATH", Description = "Old" };
        var videoType = new VideoType { Name = "Lesson", NormalizedName = "LESSON", SortOrder = 1 };
        db.AddRange(user, subject, videoType);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var preview = new AdminAIIdentityContentPreviewSource(db);
        var actor = Guid.NewGuid();

        var results = new[]
        {
            await preview.PreviewAsync("admin.identity.student-note.create", actor,
                new AdminAIAddStudentNoteInput(user.Id, "Reviewed note", true), default),
            await preview.PreviewAsync("admin.content.subject.create", actor,
                new AdminAICreateSubjectInput("Physics", "New"), default),
            await preview.PreviewAsync("admin.content.subject.update", actor,
                new AdminAIUpdateSubjectInput(subject.Id, "Advanced Math", "Updated"), default),
            await preview.PreviewAsync("admin.content.video-type.create", actor,
                new AdminAICreateVideoTypeInput("Revision", 2, true), default),
            await preview.PreviewAsync("admin.content.video-type.update", actor,
                new AdminAIUpdateVideoTypeInput(videoType.Id, "Lecture", 3), default)
        };

        Assert.All(results, result => Assert.Equal(64, result.StateFingerprint.Length));
        Assert.All(results, result => Assert.NotEmpty(result.TargetReference));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Empty(await db.StudentNotes.ToListAsync());
        Assert.Equal(1, await db.Subjects.CountAsync());
        Assert.Equal(1, await db.VideoTypes.CountAsync());
        Assert.Equal("Math", (await db.Subjects.AsNoTracking().SingleAsync()).Name);
        Assert.Equal("Lesson", (await db.VideoTypes.AsNoTracking().SingleAsync()).Name);
    }

    [Fact]
    public async Task ExistingOrRemovedTarget_RejectsPreviewAndChangedStateChangesFingerprint()
    {
        await using var db = CreateDb();
        var user = new User { FullName = "Preview target", PhoneNumber = "01000000172", PasswordHash = "test" };
        var subject = new Subject { Name = "Math", NormalizedName = "MATH", Description = "Old" };
        db.AddRange(user, subject);
        await db.SaveChangesAsync();
        var preview = new AdminAIIdentityContentPreviewSource(db);
        var actor = Guid.NewGuid();

        var before = await preview.PreviewAsync("admin.content.subject.update", actor,
            new AdminAIUpdateSubjectInput(subject.Id, "Advanced Math", "Updated"), default);
        subject.Description = "Changed by another Admin";
        await db.SaveChangesAsync();
        var after = await preview.PreviewAsync("admin.content.subject.update", actor,
            new AdminAIUpdateSubjectInput(subject.Id, "Advanced Math", "Updated"), default);
        Assert.NotEqual(before.StateFingerprint, after.StateFingerprint);

        await Assert.ThrowsAsync<AdminAIActionPreviewUnavailableException>(() =>
            preview.PreviewAsync("admin.content.subject.create", actor,
                new AdminAICreateSubjectInput("Math", "Duplicate"), default));
        user.IsDeleted = true;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<AdminAIActionPreviewUnavailableException>(() =>
            preview.PreviewAsync("admin.identity.student-note.create", actor,
                new AdminAIAddStudentNoteInput(user.Id, "Note", false), default));
        Assert.Empty(await db.StudentNotes.ToListAsync());
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-ai-preview-{Guid.NewGuid():N}").Options);
}
