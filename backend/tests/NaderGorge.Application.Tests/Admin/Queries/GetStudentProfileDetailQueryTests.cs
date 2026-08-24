using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Admin.Queries;

public sealed class GetStudentProfileDetailQueryTests
{
    [Fact]
    public async Task Packages_IncludeOwningTeacherForEveryContentGrantLevel()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var teacherUser = UserFor("مستر أحمد", "01091000001");
        var student = UserFor("الطالب", "01091000002");
        var teacher = new TeacherProfile { User = teacherUser };
        var subject = new Subject { Name = "الفيزياء", NormalizedName = "PHYSICS" };
        var package = new Package
        {
            Name = "فيزياء الصف الثاني الثانوي",
            Price = 200m,
            Teacher = teacher,
            Subject = subject
        };
        var term = new Term { Title = "الترم الأول", Price = 150m, Package = package };
        var section = new ContentSection { Title = "الشهر الأول", Price = 90m, Term = term };
        var lesson = new Lesson { Title = "الحصة الأولى", Price = 40m, ContentSection = section };

        db.AddRange(teacher, subject, package, term, section, lesson, student);
        var grants = new[]
        {
            PackageGrant(student.Id, package.Id),
            TermGrant(student.Id, term.Id),
            SectionGrant(student.Id, section.Id),
            LessonGrant(student.Id, lesson.Id)
        };
        db.StudentAccessGrants.AddRange(grants);
        await db.SaveChangesAsync();

        var studentProfile = await new GetStudentProfileDetailQueryHandler(db)
            .Handle(new GetStudentProfileDetailQuery(student.Id), CancellationToken.None);

        Assert.Equal(4, studentProfile.Packages.Count);
        foreach (var grant in grants)
        {
            var enrolledContent = Assert.Single(
                studentProfile.Packages,
                packageEnrollment => packageEnrollment.AccessGrantId == grant.Id);
            Assert.Equal(teacher.Id, enrolledContent.TeacherId);
            Assert.Equal(teacherUser.FullName, enrolledContent.TeacherName);
        }
    }

    [Fact]
    public async Task CodeGrant_PrefersCodeGroupTeacherOverCurrentContentOwner()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var codeTeacherUser = UserFor("مدرس الكود", "01092000001");
        var contentTeacherUser = UserFor("مدرس المحتوى الحالي", "01092000002");
        var student = UserFor("طالب الكود", "01092000003");
        var codeTeacher = new TeacherProfile { User = codeTeacherUser };
        var contentTeacher = new TeacherProfile { User = contentTeacherUser };
        var subject = new Subject { Name = "الأحياء", NormalizedName = "BIOLOGY" };
        var package = new Package
        {
            Name = "أحياء الصف الثاني الثانوي",
            Teacher = contentTeacher,
            Subject = subject
        };
        var codeGroup = new CodeGroup
        {
            Name = "باكدج المدرس العام",
            CodeType = CodeType.Package,
            PackageId = package.Id,
            CreatedByUser = codeTeacherUser,
            Teacher = codeTeacher
        };
        var code = new AccessCode
        {
            CodeGroup = codeGroup,
            CodeHash = "code-hash",
            SerialNumber = 1
        };
        var grant = PackageGrant(student.Id, package.Id);
        grant.AccessCode = code;

        db.AddRange(codeTeacher, contentTeacher, subject, package, student, codeGroup, code, grant);
        await db.SaveChangesAsync();

        var studentProfile = await new GetStudentProfileDetailQueryHandler(db)
            .Handle(new GetStudentProfileDetailQuery(student.Id), CancellationToken.None);

        var enrolledContent = Assert.Single(studentProfile.Packages);
        Assert.Equal(codeTeacher.Id, enrolledContent.TeacherId);
        Assert.Equal(codeTeacherUser.FullName, enrolledContent.TeacherName);
    }

    [Fact]
    public async Task BalancePurchase_PrefersFinancialEffectTeacherOverCurrentContentOwner()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var contentTeacherUser = UserFor("مدرس المحتوى الحالي", "01093000001");
        var financialTeacherUser = UserFor("مدرس العملية المالية", "01093000002");
        var student = UserFor("طالب الرصيد", "01093000003");
        var contentTeacher = new TeacherProfile { User = contentTeacherUser };
        var financialTeacher = new TeacherProfile { User = financialTeacherUser };
        var subject = new Subject { Name = "الكيمياء", NormalizedName = "CHEMISTRY" };
        var package = new Package
        {
            Name = "كيمياء الصف الثاني الثانوي",
            Price = 180m,
            Teacher = contentTeacher,
            Subject = subject
        };
        var grant = PackageGrant(student.Id, package.Id);

        db.AddRange(contentTeacher, financialTeacher, subject, package, student, grant);
        db.SalesFinancialEffects.Add(new SalesFinancialEffect
        {
            PurchaseOperationId = Guid.NewGuid(),
            StudentId = student.Id,
            TargetType = SalesTargetType.Package,
            TargetId = package.Id,
            GrossAmount = package.Price,
            PaidAmount = package.Price,
            Teacher = financialTeacher
        });
        await db.SaveChangesAsync();

        var studentProfile = await new GetStudentProfileDetailQueryHandler(db)
            .Handle(new GetStudentProfileDetailQuery(student.Id), CancellationToken.None);

        var enrolledContent = Assert.Single(studentProfile.Packages);
        Assert.Equal(financialTeacher.Id, enrolledContent.TeacherId);
        Assert.Equal(financialTeacherUser.FullName, enrolledContent.TeacherName);
    }

    private static User UserFor(string name, string phone) => new()
    {
        FullName = name,
        PhoneNumber = phone,
        PasswordHash = "test-hash"
    };

    private static StudentAccessGrant Grant(Guid studentId, CodeType type) => new()
    {
        UserId = studentId,
        GrantType = type,
        IsActive = true
    };

    private static StudentAccessGrant PackageGrant(Guid studentId, Guid packageId)
    {
        var grant = Grant(studentId, CodeType.Package);
        grant.PackageId = packageId;
        return grant;
    }

    private static StudentAccessGrant TermGrant(Guid studentId, Guid termId)
    {
        var grant = Grant(studentId, CodeType.Term);
        grant.TermId = termId;
        return grant;
    }

    private static StudentAccessGrant SectionGrant(Guid studentId, Guid sectionId)
    {
        var grant = Grant(studentId, CodeType.Month);
        grant.ContentSectionId = sectionId;
        return grant;
    }

    private static StudentAccessGrant LessonGrant(Guid studentId, Guid lessonId)
    {
        var grant = Grant(studentId, CodeType.Lesson);
        grant.LessonId = lessonId;
        return grant;
    }
}
