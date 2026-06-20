using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps;

namespace NaderGorge.Application.Tests.MultiTeacher;

public class TeacherProfileTests
{
    [Fact]
    public async Task CreateTeacherProfile_CreatesProfileAndLinksSubjects()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher John", "01099999999");
        
        var subject1 = new Subject { Id = Guid.NewGuid(), Name = "Math" };
        var subject2 = new Subject { Id = Guid.NewGuid(), Name = "Science" };
        db.Subjects.AddRange(subject1, subject2);
        await db.SaveChangesAsync();

        var handler = new CreateTeacherProfileCommandHandler(db);
        var command = new CreateTeacherProfileCommand(
            user.Id,
            "Bio text",
            "Mathematics",
            10.5m,
            "http://image.url",
            "contact@teacher.com",
            new List<Guid> { subject1.Id, subject2.Id },
            "01012345678,01098765432",
            "https://facebook.com",
            "https://youtube.com",
            "https://telegram.me"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.Data);

        var profile = await db.TeacherProfiles
            .Include(tp => tp.TeacherSubjects)
            .FirstOrDefaultAsync(tp => tp.Id == result.Data);

        Assert.NotNull(profile);
        Assert.Equal(user.Id, profile!.UserId);
        Assert.Equal("Bio text", profile.Bio);
        Assert.Equal("Mathematics", profile.Specialization);
        Assert.Equal(10.5m, profile.CommissionRate);
        Assert.Equal("http://image.url", profile.ProfileImageUrl);
        Assert.Equal("contact@teacher.com", profile.ContactInfo);
        Assert.Equal("01012345678,01098765432", profile.AssistantPhoneNumbers);
        Assert.Equal("https://facebook.com", profile.FacebookUrl);
        Assert.Equal("https://youtube.com", profile.YouTubeUrl);
        Assert.Equal("https://telegram.me", profile.TelegramUrl);
        Assert.Equal(2, profile.TeacherSubjects.Count);
        Assert.Contains(profile.TeacherSubjects, ts => ts.SubjectId == subject1.Id);
        Assert.Contains(profile.TeacherSubjects, ts => ts.SubjectId == subject2.Id);
    }

    [Fact]
    public async Task UpdateTeacherProfile_UpdatesProfileAndSyncsSubjects()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher John", "01099999999");
        
        var subject1 = new Subject { Id = Guid.NewGuid(), Name = "Math" };
        var subject2 = new Subject { Id = Guid.NewGuid(), Name = "Science" };
        var subject3 = new Subject { Id = Guid.NewGuid(), Name = "History" };
        db.Subjects.AddRange(subject1, subject2, subject3);
        await db.SaveChangesAsync();

        var profile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Bio = "Old Bio",
            Specialization = "Old Spec",
            CommissionRate = 5m,
            ContactInfo = "old@contact.com"
        };
        profile.TeacherSubjects.Add(new TeacherSubject { TeacherId = profile.Id, SubjectId = subject1.Id });
        profile.TeacherSubjects.Add(new TeacherSubject { TeacherId = profile.Id, SubjectId = subject2.Id });
        db.TeacherProfiles.Add(profile);
        await db.SaveChangesAsync();

        var handler = new UpdateTeacherProfileCommandHandler(db);
        var command = new UpdateTeacherProfileCommand(
            profile.Id,
            "New Bio",
            "New Spec",
            8.5m,
            "http://new.image",
            "new@contact.com",
            new List<Guid> { subject2.Id, subject3.Id }, // Removing subject1, adding subject3
            "01088888888",
            "https://new.facebook.com",
            "https://new.youtube.com",
            "https://new.telegram.me"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);

        var updatedProfile = await db.TeacherProfiles
            .Include(tp => tp.TeacherSubjects)
            .FirstOrDefaultAsync(tp => tp.Id == profile.Id);

        Assert.NotNull(updatedProfile);
        Assert.Equal("New Bio", updatedProfile!.Bio);
        Assert.Equal("New Spec", updatedProfile.Specialization);
        Assert.Equal(8.5m, updatedProfile.CommissionRate);
        Assert.Equal("http://new.image", updatedProfile.ProfileImageUrl);
        Assert.Equal("new@contact.com", updatedProfile.ContactInfo);
        Assert.Equal("01088888888", updatedProfile.AssistantPhoneNumbers);
        Assert.Equal("https://new.facebook.com", updatedProfile.FacebookUrl);
        Assert.Equal("https://new.youtube.com", updatedProfile.YouTubeUrl);
        Assert.Equal("https://new.telegram.me", updatedProfile.TelegramUrl);
        Assert.Equal(2, updatedProfile.TeacherSubjects.Count);
        Assert.Contains(updatedProfile.TeacherSubjects, ts => ts.SubjectId == subject2.Id);
        Assert.Contains(updatedProfile.TeacherSubjects, ts => ts.SubjectId == subject3.Id);
        Assert.DoesNotContain(updatedProfile.TeacherSubjects, ts => ts.SubjectId == subject1.Id);
    }

    [Fact]
    public async Task UploadTeacherPhoto_SavesCompressedWebpAndAddsPhoto()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher John", "01099999999");

        var expectedUrl = "/uploads/content/teacher/photo-123.webp";
        var stubStorage = new StubContentImageStorage(expectedUrl);
        var handler = new UploadTeacherPhotoCommandHandler(db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UploadTeacherPhotoCommandHandler>(), stubStorage);

        var result = await handler.Handle(
            new UploadTeacherPhotoCommand(user.Id, "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==", "photo.png"),
            CancellationToken.None);

        Assert.True(result.Success);
        var photo = await db.TeacherPhotos.FirstOrDefaultAsync(p => p.TeacherId == user.Id);
        Assert.NotNull(photo);
        Assert.Equal(expectedUrl, photo!.FileUrl);
    }

    [Fact]
    public async Task GetActiveTeacherPhoto_ReturnsActivePhoto()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher John", "01099999999");

        var photo = new TeacherPhoto
        {
            TeacherId = user.Id,
            FileUrl = "/uploads/content/teacher/photo-123.webp",
            IsActive = true,
            UploadedAt = DateTime.UtcNow
        };
        db.TeacherPhotos.Add(photo);
        await db.SaveChangesAsync();

        var handler = new NaderGorge.Application.Features.Admin.Queries.GetActiveTeacherPhotoQueryHandler(db);
        var result = await handler.Handle(new NaderGorge.Application.Features.Admin.Queries.GetActiveTeacherPhotoQuery(user.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("/uploads/content/teacher/photo-123.webp", result.Data!.Url);
    }

    [Fact]
    public async Task UploadTeacherProfileImage_SavesCompressedWebpAndUpdatesProfile()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher John", "01099999999");
        var profile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Bio = "Bio",
            Specialization = "Spec",
            CommissionRate = 5m
        };
        db.TeacherProfiles.Add(profile);
        await db.SaveChangesAsync();

        var expectedUrl = "/uploads/content/teacher/profile-123.webp";
        var stubStorage = new StubContentImageStorage(expectedUrl);
        var handler = new UploadTeacherProfileImageCommandHandler(db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<UploadTeacherProfileImageCommandHandler>(), stubStorage);

        var result = await handler.Handle(
            new UploadTeacherProfileImageCommand(user.Id, "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==", "photo.png"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedUrl, result.Data);

        var updatedProfile = await db.TeacherProfiles.FirstOrDefaultAsync(p => p.Id == profile.Id);
        Assert.NotNull(updatedProfile);
        Assert.Equal(expectedUrl, updatedProfile!.ProfileImageUrl);
    }

    private sealed class StubContentImageStorage(string imageUrl) : IContentImageStorage
    {
        public Task<string> SaveAsWebpAsync(
            Stream imageStream,
            string contentFolder,
            CancellationToken cancellationToken) => Task.FromResult(imageUrl);
    }
}
