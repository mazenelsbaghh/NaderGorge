using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

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
            "teacher@example.com",
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
        Assert.Equal("teacher@example.com", profile.Email);
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
            "newteacher@example.com",
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
        Assert.Equal("newteacher@example.com", updatedProfile.Email);
        Assert.Equal("01088888888", updatedProfile.AssistantPhoneNumbers);
        Assert.Equal("https://new.facebook.com", updatedProfile.FacebookUrl);
        Assert.Equal("https://new.youtube.com", updatedProfile.YouTubeUrl);
        Assert.Equal("https://new.telegram.me", updatedProfile.TelegramUrl);
        Assert.Equal(2, updatedProfile.TeacherSubjects.Count);
        Assert.Contains(updatedProfile.TeacherSubjects, ts => ts.SubjectId == subject2.Id);
        Assert.Contains(updatedProfile.TeacherSubjects, ts => ts.SubjectId == subject3.Id);
        Assert.DoesNotContain(updatedProfile.TeacherSubjects, ts => ts.SubjectId == subject1.Id);
    }
}
