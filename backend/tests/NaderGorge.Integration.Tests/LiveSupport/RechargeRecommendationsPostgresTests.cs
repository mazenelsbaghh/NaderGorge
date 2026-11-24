using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Codes.Commands;
using NaderGorge.Application.Features.Codes.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class RechargeRecommendationsPostgresTests
{
    [Fact]
    public async Task Recommendations_use_teacher_budget_grade_and_exact_price_then_newest_order()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var scenario = await Seed(fixture.Db);
        var section = scenario.Section;
        var exactOld = Lesson(section, "بنفس الاسم", 100, -10);
        var exactNew = Lesson(section, "بنفس الاسم", 100, -2);
        var lowerOld = Lesson(section, "أقل قديمة", 50, -6);
        var lowerNew = Lesson(section, "أقل جديدة", 75, -1);
        var higher = Lesson(section, "ضمن الإجمالي", 120, 0);
        var unavailable = Lesson(section, "مقفولة", 100, 1);
        unavailable.Videos.Single().IsActive = false;
        var owned = Lesson(section, "مشترك فيها", 100, 2);
        var wrongGrade = Lesson(section, "صف آخر", 100, 3);
        fixture.Db.AddRange(exactOld, exactNew, lowerOld, lowerNew, higher, unavailable, owned, wrongGrade, Lesson(section, "أغلى من الرصيد", 151, 4));
        fixture.Db.StudentAccessGrants.Add(new StudentAccessGrant { UserId = scenario.Student.Id,
            GrantType = CodeType.Lesson, LessonId = owned.Id, IsActive = true });
        fixture.Db.StudentFacingAcademicScopes.Add(Scope(StudentFacingScopeOwnerType.Lesson, wrongGrade.Id, GradeLevel.SecondSecondary));
        var otherTeacher = new TeacherProfile { User = User("مدرس آخر") };
        var otherPackage = new Package { Name = "باقة أخرى", Teacher = otherTeacher, SubjectId = section.Term.Package.SubjectId, Price = 100 };
        fixture.Db.Add(otherPackage);
        fixture.Db.StudentFacingAcademicScopes.Add(Scope(StudentFacingScopeOwnerType.Package, otherPackage.Id));
        fixture.Db.PromotionalBalanceAllocations.Add(Allocation(scenario.Student, otherTeacher, 1000));
        var expired = Allocation(scenario.Student, section.Term.Package.Teacher, 1000);
        expired.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        fixture.Db.Add(expired);
        var limited = Allocation(scenario.Student, section.Term.Package.Teacher, 1000);
        limited.MaxPurchaseCount = 1; limited.PurchaseCount = 1;
        fixture.Db.Add(limited);
        await fixture.Db.SaveChangesAsync();

        var response = await Recommendations(fixture.Db, scenario);

        Assert.True(response.Success, response.Message);
        Assert.Equal(150, response.Data!.AvailableBalance);
        Assert.Equal(100, response.Data.RechargedAmount);
        Assert.Equal([exactNew.Id, exactOld.Id, lowerNew.Id, lowerOld.Id, higher.Id], response.Data.Items.Select(item => item.ContentId));
        Assert.All(response.Data.Items, item => Assert.Equal(section.Term.Package.Name, item.PackageName));
        Assert.Equal(50, (await fixture.Db.StudentBalances.SingleAsync(balance => balance.UserId == scenario.Student.Id)).CurrentBalance);
        Assert.Single(await fixture.Db.StudentAccessGrants.Where(grant => grant.UserId == scenario.Student.Id).ToListAsync());
    }

    [Fact]
    public async Task Ownership_and_current_profile_are_rechecked_and_pagination_does_not_skip_eligible_items()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var scenario = await Seed(fixture.Db);
        var lessons = Enumerable.Range(1, 26).Select(index => Lesson(scenario.Section, $"حصة {index}", 100, -index)).ToList();
        fixture.Db.AddRange(lessons);
        await fixture.Db.SaveChangesAsync();
        var first = (await Recommendations(fixture.Db, scenario)).Data!;
        var second = (await Recommendations(fixture.Db, scenario, 2)).Data!;
        Assert.True(first.HasMore);
        Assert.False(second.HasMore);
        Assert.Equal(24, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Equal(lessons.Select(lesson => lesson.Id), first.Items.Concat(second.Items).Select(item => item.ContentId));
        var handler = new GetRechargeRecommendationsQueryHandler(fixture.Db, new AcademicScopeService(fixture.Db));
        Assert.False((await handler.Handle(new(Guid.NewGuid(), scenario.Recharge.Id), default)).Success);
        (await fixture.Db.StudentProfiles.SingleAsync(profile => profile.UserId == scenario.Student.Id)).GradeLevel = GradeLevel.SecondSecondary;
        await fixture.Db.SaveChangesAsync();
        Assert.Empty((await Recommendations(fixture.Db, scenario)).Data!.Items);
    }

    [Theory]
    [InlineData(PackageContentMode.TermWithSections, CodeType.Package)]
    [InlineData(PackageContentMode.SectionWithLessons, CodeType.Term)]
    [InlineData(PackageContentMode.LessonsOnly, CodeType.Month)]
    [InlineData(PackageContentMode.SingleLesson, CodeType.Lesson)]
    public async Task Course_shapes_use_the_actual_purchase_target_and_root_price(PackageContentMode mode, CodeType expectedType)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var scenario = await Seed(fixture.Db);
        var package = scenario.Section.Term.Package;
        package.ContentMode = mode;
        package.Price = 100;
        scenario.Section.Term.IsSystemContainer = mode != PackageContentMode.TermWithSections;
        scenario.Section.IsSystemContainer = mode is PackageContentMode.LessonsOnly or PackageContentMode.SingleLesson;
        var lesson = Lesson(scenario.Section, "درس الكورس", 999, -1);
        fixture.Db.Add(lesson);
        await fixture.Db.SaveChangesAsync();
        var item = Assert.Single((await Recommendations(fixture.Db, scenario)).Data!.Items);
        Assert.Equal(expectedType, item.ContentType);
        Assert.Equal(100, item.Price);
        Assert.Equal(expectedType switch { CodeType.Package => package.Id, CodeType.Term => scenario.Section.TermId,
            CodeType.Month => scenario.Section.Id, _ => lesson.Id }, item.ContentId);
    }

    [Fact]
    public async Task Archived_ancestors_and_closed_package_purchase_are_not_recommended()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var scenario = await Seed(fixture.Db);
        scenario.Section.Term.Package.Price = 100;
        scenario.Section.Term.Package.AllowFullPackagePurchase = false;
        scenario.Section.ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly;
        fixture.Db.Add(Lesson(scenario.Section, "مؤرشفة", 100, 0));
        await fixture.Db.SaveChangesAsync();
        Assert.Empty((await Recommendations(fixture.Db, scenario)).Data!.Items);
    }

    private static Task<NaderGorge.Application.Common.ApiResponse<RechargeRecommendationsDto>> Recommendations(
        AppDbContext db, Scenario scenario, int page = 1) =>
        new GetRechargeRecommendationsQueryHandler(db, new AcademicScopeService(db))
            .Handle(new(scenario.Student.Id, scenario.Recharge.Id, page), default);

    private static async Task<Scenario> Seed(AppDbContext db)
    {
        var student = User("طالب الاقتراحات");
        var teacher = new TeacherProfile { User = User("مدرس الاقتراحات"), IsContentVisibleToStudents = true };
        var section = new ContentSection { Title = "الشهر", Price = 999, Term = new Term { Title = "الترم", Price = 999,
            Package = new Package { Name = $"كورس {Guid.NewGuid():N}", Teacher = teacher, Price = 999,
                Subject = new Subject { Name = "تاريخ", NormalizedName = Guid.NewGuid().ToString("N") } } } };
        var recharge = Allocation(student, teacher, 100);
        db.AddRange(student, section, recharge);
        db.StudentBalances.Add(new StudentBalance { User = student, CurrentBalance = 50 });
        db.StudentProfiles.Add(new StudentProfile { User = student, DateOfBirth = DateTime.UtcNow.AddYears(-16),
            EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.FirstSecondary,
            Governorate = "Cairo", Address = "عنوان", Gender = Gender.Male });
        db.StudentFacingAcademicScopes.Add(Scope(StudentFacingScopeOwnerType.Package, section.Term.PackageId));
        await db.SaveChangesAsync();
        return new(student, section, recharge);
    }

    private static User User(string name) => new() { FullName = name, PasswordHash = "test-only",
        PhoneNumber = $"010{Random.Shared.NextInt64(10000000, 99999999)}" };

    private static PromotionalBalanceAllocation Allocation(User student, TeacherProfile teacher, decimal amount) => new()
    {
        Student = student, Teacher = teacher, OriginalAmount = amount, AvailableAmount = amount,
        GiftRecipient = new GiftRecipient { Student = student, GiftIssuance = new GiftIssuance {
            RequestId = Guid.NewGuid(), IssuedByUser = teacher.User, Teacher = teacher,
            TargetType = GiftTargetType.TeacherBalance, Amount = amount, Reason = "Test credit" } }
    };

    private static Lesson Lesson(ContentSection section, string title, decimal price, int days) => new()
    {
        Title = title, ContentSection = section, Price = price, CreatedAt = DateTime.UtcNow.Date.AddDays(days),
        Videos = [new LessonVideo { Title = "الشرح", Provider = "youtube", ProviderVideoId = "test-only",
            VideoType = new VideoType { Name = "شرح", NormalizedName = Guid.NewGuid().ToString("N") } }]
    };

    private static StudentFacingAcademicScope Scope(StudentFacingScopeOwnerType ownerType, Guid ownerId,
        GradeLevel grade = GradeLevel.FirstSecondary) => new() { OwnerType = ownerType, OwnerId = ownerId,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects, EducationStage = EducationStage.Secondary, GradeLevel = grade };

    private sealed record Scenario(User Student, ContentSection Section, PromotionalBalanceAllocation Recharge);
}
