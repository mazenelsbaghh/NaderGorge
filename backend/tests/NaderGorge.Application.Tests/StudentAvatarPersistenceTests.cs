using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public sealed class StudentAvatarPersistenceTests
{
    [Fact]
    public async Task SelectedAvatar_PersistsAcrossThemeReadsAndAppearsInAdminList()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await SeedStudentAsync(db, "messi");

        var updateHandler = new UpdateStudentThemePreferencesCommandHandler(db);
        var update = await updateHandler.Handle(
            new UpdateStudentThemePreferencesCommand(
                user.Id,
                "oasis-light",
                "midnight-teal",
                "dark",
                "mohamed-salah"),
            CancellationToken.None);

        Assert.True(update.Success);
        Assert.Equal("mohamed-salah", update.Data?.AvatarSlug);

        db.ChangeTracker.Clear();

        var preferences = await new GetStudentThemePreferencesQueryHandler(db).Handle(
            new GetStudentThemePreferencesQuery(user.Id),
            CancellationToken.None);
        var adminPage = await new ListUsersQueryHandler(db).Handle(
            new ListUsersQuery(Role: "Student"),
            CancellationToken.None);

        Assert.Equal("mohamed-salah", preferences.Data?.AvatarSlug);
        Assert.Equal(
            "mohamed-salah",
            Assert.Single(Assert.IsType<PagedResult<AdminUserListDto>>(adminPage.Data).Items).AvatarSlug);
    }

    [Fact]
    public async Task ThemeOnlyUpdate_DoesNotClearPreviouslySelectedAvatar()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await SeedStudentAsync(db, "ronaldo");

        var result = await new UpdateStudentThemePreferencesCommandHandler(db).Handle(
            new UpdateStudentThemePreferencesCommand(
                user.Id,
                "ruby-light",
                "ember-dark",
                "light",
                null),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("ronaldo", result.Data?.AvatarSlug);
        Assert.Equal(
            "ronaldo",
            db.StudentProfiles.Single(profile => profile.UserId == user.Id).AvatarSlug);
    }

    private static async Task<User> SeedStudentAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        string avatarSlug)
    {
        var role = new Role
        {
            Name = "Student",
            Type = RoleType.Student,
            AllowedDomain = "student",
        };
        var user = new User
        {
            FullName = "Avatar Student",
            PhoneNumber = "01000000000",
            PasswordHash = "test",
            IsProfileComplete = true,
        };
        user.StudentProfile = new StudentProfile
        {
            User = user,
            DateOfBirth = new DateTime(2008, 1, 1),
            Gender = Gender.Male,
            Governorate = "Cairo",
            Address = "Test address",
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary,
            AvatarSlug = avatarSlug,
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
