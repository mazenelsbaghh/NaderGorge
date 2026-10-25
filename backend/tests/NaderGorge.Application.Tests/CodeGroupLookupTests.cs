using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public class CodeGroupLookupTests
{
    [Theory]
    [InlineData("EXACT-CODE")]
    [InlineData("12345")]
    [InlineData("١٢٣٤٥")]
    [InlineData("۱۲۳۴۵")]
    [InlineData("480123456789")]
    [InlineData("٤٨٠١٢٣٤٥٦٧٨٩")]
    [InlineData("۴۸۰۱۲۳۴۵۶۷۸۹")]
    public async Task CodeOrSerial_ReturnsItsGroupWithoutExposingOtherGroups(string search)
    {
        await using var db = TestAppDbContextFactory.Create();
        var group = new CodeGroup { Name = "Target" };
        group.AccessCodes.Add(new AccessCode { CodePlaintext = "EXACT-CODE", CodeHash = "test-hash", SerialNumber = 12345 });
        group.AccessCodes.Add(new AccessCode { CodePlaintext = "480123456789", CodeHash = "numeric-test-hash", SerialNumber = 54321 });
        db.CodeGroups.AddRange(group, new CodeGroup { Name = "Other" });
        await db.SaveChangesAsync();
        var response = await new ListCodeGroupsQueryHandler(db).Handle(new(Search: search), default);
        Assert.True(response.Success);
        Assert.Equal(group.Id, Assert.Single(response.Data!).Id);
    }

    // Arabic code search must retain the same teacher boundary as ASCII search.
    [Theory]
    [InlineData("٤٨٠١٢٣٤٥٦٧٨٩", true)]
    [InlineData("٤٨٠١٢٣٤٥٦٧٩٠", false)]
    public async Task TeacherCodeSearch_ReturnsOnlyTheTeachersOwnMatchingGroup(string search, bool ownCode)
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Code Search Teacher", "01000000001");
        var teacher = new TeacherProfile { User = user };
        db.TeacherProfiles.Add(teacher);
        db.UserRoles.Add(new UserRole { UserId = user.Id, Role = new Role { Name = "Teacher", Type = RoleType.Teacher } });
        var ownGroup = new CodeGroup { Name = "Own", TeacherId = teacher.Id };
        ownGroup.AccessCodes.Add(new AccessCode { CodePlaintext = "480123456789", CodeHash = "own", SerialNumber = 1 });
        var otherGroup = new CodeGroup { Name = "Other", TeacherId = Guid.NewGuid() };
        otherGroup.AccessCodes.Add(new AccessCode { CodePlaintext = "480123456790", CodeHash = "other", SerialNumber = 2 });
        db.CodeGroups.AddRange(ownGroup, otherGroup);
        await db.SaveChangesAsync();

        var response = await new ListCodeGroupsQueryHandler(db).Handle(new(user.Id, search), default);

        Assert.True(response.Success);
        if (ownCode) Assert.Equal(ownGroup.Id, Assert.Single(response.Data!).Id);
        else Assert.Empty(response.Data!);
    }
}
