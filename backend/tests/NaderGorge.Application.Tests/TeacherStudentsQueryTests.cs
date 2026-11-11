using System.Security.Claims;
using ClosedXML.Excel;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.Teacher;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class TeacherStudentsQueryTests
{
    [Fact]
    public async Task StudentsList_UsesDistinctEffectiveFactsAcrossAllContentLevels()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var teacherUser = User("Teacher", "01093000001");
        var teacher = new TeacherProfile { User = teacherUser };
        var subject = new Subject { Name = "Math", NormalizedName = "MATH" };
        var firstPackage = new Package { Name = "First package", Subject = subject, Teacher = teacher };
        var firstTerm = new Term { Title = "First term", Package = firstPackage };
        var firstSection = new ContentSection { Title = "First section", Term = firstTerm };
        var firstLesson = new Lesson { Title = "First lesson", ContentSection = firstSection };
        var secondPackage = new Package { Name = "Second package", Subject = subject, Teacher = teacher };
        var secondTerm = new Term { Title = "Second term", Package = secondPackage };
        var secondSection = new ContentSection { Title = "Second section", Term = secondTerm };
        var secondLesson = new Lesson { Title = "Second lesson", ContentSection = secondSection };
        var multiGrantStudent = User("Multi grant", "01093000002");
        var lessonOnlyStudent = User("Lesson only", "01093000003");
        var excludedStudent = User("Excluded", "01093000004");
        var profile = new StudentProfile
        {
            User = multiGrantStudent,
            StudentCode = "ST-100",
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondaryGrade3
        };
        var now = DateTime.UtcNow;
        var cancelledGrant = Grant(excludedStudent, CodeType.Package, firstPackage.Id, now.AddDays(-3));
        cancelledGrant.CancelledAt = now.AddDays(-2);
        var expiredGrant = Grant(excludedStudent, CodeType.Term, firstTerm.Id, now.AddDays(-3));
        expiredGrant.ExpiresAt = now.AddDays(-1);
        var inactiveGrant = Grant(excludedStudent, CodeType.Lesson, firstLesson.Id, now.AddDays(-3));
        inactiveGrant.IsActive = false;

        db.AddRange(
            teacher,
            subject,
            firstPackage,
            firstTerm,
            firstSection,
            firstLesson,
            secondPackage,
            secondTerm,
            secondSection,
            secondLesson,
            profile,
            lessonOnlyStudent,
            excludedStudent);
        db.StudentAccessGrants.AddRange(
            Grant(multiGrantStudent, CodeType.Package, firstPackage.Id, now.AddDays(-4)),
            Grant(multiGrantStudent, CodeType.Term, firstTerm.Id, now.AddDays(-3)),
            Grant(multiGrantStudent, CodeType.Month, firstSection.Id, now.AddDays(-2)),
            Grant(multiGrantStudent, CodeType.Lesson, secondLesson.Id, now.AddDays(-1)),
            Grant(lessonOnlyStudent, CodeType.Lesson, firstLesson.Id, now),
            cancelledGrant,
            expiredGrant,
            inactiveGrant);
        await db.SaveChangesAsync();

        var response = await new GetTeacherStudentsQueryHandler(db)
            .Handle(new GetTeacherStudentsQuery(teacherUser.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.Count);
        var multiGrantRow = Assert.Single(response.Data, student => student.Id == multiGrantStudent.Id);
        Assert.Equal((4, 2), (multiGrantRow.ActiveGrantCount, multiGrantRow.ActivePackageCount));
        Assert.Equal((now.AddDays(-4), now.AddDays(-1)),
            (multiGrantRow.ActivatedAt, multiGrantRow.LastActivationAt));
        Assert.Equal(secondPackage.Name, multiGrantRow.ActivatedPackageName);
        Assert.Equal(new[] { firstPackage.Id, secondPackage.Id }, multiGrantRow.ActivePackages.Select(package => package.Id));
        Assert.Equal("ST-100", multiGrantRow.StudentCode);
        Assert.Null(multiGrantRow.ExportDetails);
        var lessonOnlyRow = Assert.Single(response.Data, student => student.Id == lessonOnlyStudent.Id);
        Assert.Equal((1, 1), (lessonOnlyRow.ActiveGrantCount, lessonOnlyRow.ActivePackageCount));
        Assert.DoesNotContain(response.Data, student => student.Id == excludedStudent.Id);
    }

    [Fact]
    public async Task StudentsExport_IncludesAllEligibleStudentsAndPreservesRecordedContactDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var teacher = new TeacherProfile { User = User("Teacher", "01094000001") };
        var otherTeacher = new TeacherProfile { User = User("Other teacher", "01094000002") };
        var subject = new Subject { Name = "History", NormalizedName = "HISTORY" };
        var package = new Package { Name = "History", Subject = subject, Teacher = teacher };
        var otherPackage = new Package { Name = "Other history", Subject = subject, Teacher = otherTeacher };
        var student = User("=1+1", "01094000003");
        var secondStudent = User("Second student", "01094000010");
        var otherStudent = User("Other student", "01094000004");
        var assistant = User("Assistant", "01094000005");
        db.AddRange(package, otherPackage, new TeacherStaffMember
        {
            Teacher = teacher, User = assistant, CreatedByTeacherUser = teacher.User, PermissionKeys = "content"
        }, new StudentProfile
        {
            User = student, Gender = Gender.Female, StudentCode = "00021",
            EducationStage = EducationStage.Baccalaureate, GradeLevel = GradeLevel.SecondBaccalaureate,
            StudyTrack = StudyTrack.Business, ParentPhone = "01094000007",
            SecondaryParentPhone = "01094000006", MotherPhone = "01094000008",
            Governorate = "القاهرة", District = "مدينة نصر", Address = "عنوان اختبار",
            DateOfBirth = new DateTime(2010, 4, 3), FatherDateOfBirth = new DateTime(8, 4, 3)
        }, new StudentProfile
        {
            User = secondStudent, StudentCode = "00022", ParentPhone = "01094000011",
            EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondaryGrade3,
            Governorate = "الجيزة"
        });
        var grantedAt = new DateTime(2026, 1, 1, 23, 0, 0, DateTimeKind.Utc);
        db.StudentAccessGrants.AddRange(Grant(student, CodeType.Package, package.Id, grantedAt),
            Grant(secondStudent, CodeType.Package, package.Id, grantedAt),
            Grant(otherStudent, CodeType.Package, otherPackage.Id, grantedAt));
        const int expectedStudentCount = 2345;
        db.StudentAccessGrants.AddRange(Enumerable.Range(0, expectedStudentCount - 2).Select(index =>
            Grant(User($"Synthetic student {index:D4}", $"019{index:D8}"), CodeType.Package, package.Id, grantedAt)));
        await db.SaveChangesAsync();

        await using var services = new ServiceCollection().AddSingleton<IAppDbContext>(db)
            .AddMediatR(config => config.RegisterServicesFromAssemblyContaining<GetTeacherStudentsQuery>())
            .BuildServiceProvider();
        var controller = new TeacherController(services.GetRequiredService<IMediator>(), db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        controller.HttpContext.User = TeacherIdentity(assistant.Id);
        Assert.IsType<ForbidResult>(await controller.ExportStudents(new(true, null), default));

        controller.HttpContext.User = TeacherIdentity(teacher.User.Id);
        Assert.IsType<ConflictObjectResult>(await controller.ExportStudents(new(false, [student.Id, otherStudent.Id]), default));
        foreach (var selection in new TeacherController.StudentExportRequest[] { new(true, null), new(false, [student.Id]) })
        {
            var file = Assert.IsType<FileContentResult>(await controller.ExportStudents(selection, default));
            using var stream = new MemoryStream(file.FileContents);
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheet("الطلاب");
            Assert.True(sheet.RightToLeft);
            Assert.Equal(31, sheet.LastColumnUsed()!.ColumnNumber());
            Assert.Equal(selection.All ? expectedStudentCount + 1 : 2, sheet.LastRowUsed()!.RowNumber());
            Assert.Equal(student.Id.ToString(), sheet.Cell("AE2").GetString());
            Assert.False(sheet.Cell("A2").HasFormula);
            Assert.Equal("=1+1", sheet.Cell("A2").GetString());
            Assert.Equal(XLDataType.Text, sheet.Cell("C2").DataType);
            Assert.Equal("01094000003", sheet.Cell("C2").GetString());
            Assert.Equal("01094000007", sheet.Cell("E2").GetString());
            Assert.Equal("01094000006", sheet.Cell("F2").GetString());
            Assert.Equal("01094000008", sheet.Cell("G2").GetString());
            Assert.Equal(new DateTime(2010, 4, 3), sheet.Cell("J2").GetDateTime());
            Assert.Equal(XLDataType.Text, sheet.Cell("K2").DataType);
            Assert.Equal("0008-04-03", sheet.Cell("K2").GetString());
            Assert.True(sheet.Cell("L2").IsEmpty());
            Assert.Equal("قطاع الأعمال", sheet.Cell("Q2").GetString());
            Assert.Equal("القاهرة", sheet.Cell("T2").GetString());
            Assert.Equal("مدينة نصر", sheet.Cell("U2").GetString());
            Assert.Equal("عنوان اختبار", sheet.Cell("V2").GetString());
            Assert.Equal(new DateTime(2026, 1, 2), sheet.Cell("AC2").GetDateTime());
            if (selection.All)
            {
                Assert.Equal(secondStudent.Id.ToString(), sheet.Cell("AE3").GetString());
                Assert.Equal("الجيزة", sheet.Cell("T3").GetString());
                Assert.Equal("01094000011", sheet.Cell("E3").GetString());
                Assert.True(sheet.Cell("F3").IsEmpty());
            }
        }
    }

    private static ClaimsPrincipal TeacherIdentity(Guid userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, "Teacher")], "test"));

    private static User User(string fullName, string phoneNumber) => new()
    {
        FullName = fullName,
        PhoneNumber = phoneNumber,
        PasswordHash = "test-hash"
    };

    private static StudentAccessGrant Grant(
        User student,
        CodeType grantType,
        Guid targetId,
        DateTime grantedAt)
    {
        var grant = new StudentAccessGrant
        {
            User = student,
            GrantType = grantType,
            GrantedAt = grantedAt,
            IsActive = true
        };

        switch (grantType)
        {
            case CodeType.Package: grant.PackageId = targetId; break;
            case CodeType.Term: grant.TermId = targetId; break;
            case CodeType.Month: grant.ContentSectionId = targetId; break;
            case CodeType.Lesson: grant.LessonId = targetId; break;
            default: throw new ArgumentOutOfRangeException(nameof(grantType), grantType, "Unsupported test grant type.");
        }

        return grant;
    }
}
