using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.MultiTeacher;

public class TeacherIsolationTests
{
    private async Task<(User User, TeacherProfile Profile)> OnboardTeacherAsync(AppDbContext db, string name, string phone)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, name, phone);
        var role = new Role { Id = Guid.NewGuid(), Name = "Teacher", Type = RoleType.Teacher };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        var profile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Bio = $"{name} Bio",
            Specialization = "Science",
            ContactInfo = $"{name.ToLower()}@test.com"
        };
        db.TeacherProfiles.Add(profile);
        await db.SaveChangesAsync();

        return (user, profile);
    }

    private async Task<(User User, Role Role)> OnboardAdminAsync(AppDbContext db, string name, string phone)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, name, phone);
        var role = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        return (user, role);
    }

    [Fact]
    public async Task CanAccessPackage_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");
        var (admin, _) = await OnboardAdminAsync(db, "Admin User", "01033333333");

        var packageA = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Package A",
            Description = "A description",
            TeacherId = profileA.Id
        };
        db.Packages.Add(packageA);
        await db.SaveChangesAsync();

        var authService = new TeacherAuthorizationService(db);

        // Teacher A should access their own package
        Assert.True(await authService.CanAccessPackageAsync(userA.Id, packageA.Id, CancellationToken.None));

        // Teacher B should NOT access Teacher A's package
        Assert.False(await authService.CanAccessPackageAsync(userB.Id, packageA.Id, CancellationToken.None));

        // Admin should access Teacher A's package
        Assert.True(await authService.CanAccessPackageAsync(admin.Id, packageA.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CanAccessProgram_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var subjectA = new Subject { Id = Guid.NewGuid(), Name = "Math" };
        var subjectB = new Subject { Id = Guid.NewGuid(), Name = "Science" };
        db.Subjects.AddRange(subjectA, subjectB);

        // Link Teacher A to Subject A only
        db.TeacherSubjects.Add(new TeacherSubject { TeacherId = profileA.Id, SubjectId = subjectA.Id });
        // Link Teacher B to Subject B only
        db.TeacherSubjects.Add(new TeacherSubject { TeacherId = profileB.Id, SubjectId = subjectB.Id });

        var programA = new Program { Id = Guid.NewGuid(), Name = "Program A", SubjectId = subjectA.Id };
        db.Programs.Add(programA);
        await db.SaveChangesAsync();

        var authService = new TeacherAuthorizationService(db);

        // Teacher A teaches Subject A, so they can access Program A
        Assert.True(await authService.CanAccessProgramAsync(userA.Id, programA.Id, CancellationToken.None));

        // Teacher B does not teach Subject A, so they cannot access Program A
        Assert.False(await authService.CanAccessProgramAsync(userB.Id, programA.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CanAccessCodeGroup_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var codeGroupA = new CodeGroup
        {
            Id = Guid.NewGuid(),
            Name = "Group A",
            TeacherId = profileA.Id
        };
        db.CodeGroups.Add(codeGroupA);
        await db.SaveChangesAsync();

        var authService = new TeacherAuthorizationService(db);

        Assert.True(await authService.CanAccessCodeGroupAsync(userA.Id, codeGroupA.Id, CancellationToken.None));
        Assert.False(await authService.CanAccessCodeGroupAsync(userB.Id, codeGroupA.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CanAccessExam_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var examA = new Exam
        {
            Id = Guid.NewGuid(),
            Title = "Exam A",
            CreatedByTeacherId = profileA.Id
        };
        db.Exams.Add(examA);
        await db.SaveChangesAsync();

        var authService = new TeacherAuthorizationService(db);

        Assert.True(await authService.CanAccessExamAsync(userA.Id, examA.Id, CancellationToken.None));
        Assert.False(await authService.CanAccessExamAsync(userB.Id, examA.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CreateInlineExam_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var subject = new Subject { Id = Guid.NewGuid(), Name = "Math" };
        db.Subjects.Add(subject);

        // Teacher A has Subject, Program, Package, Term, Section, Lesson
        db.TeacherSubjects.Add(new TeacherSubject { TeacherId = profileA.Id, SubjectId = subject.Id });
        var program = new Program { Id = Guid.NewGuid(), Name = "Program A", SubjectId = subject.Id };
        db.Programs.Add(program);
        var package = new Package { Id = Guid.NewGuid(), Name = "Package A", TeacherId = profileA.Id, ProgramId = program.Id };
        db.Packages.Add(package);
        var term = new Term { Id = Guid.NewGuid(), Title = "Term A", PackageId = package.Id };
        db.Terms.Add(term);
        var section = new ContentSection { Id = Guid.NewGuid(), Title = "Section A", TermId = term.Id };
        db.ContentSections.Add(section);
        var lesson = new Lesson { Id = Guid.NewGuid(), Title = "Lesson A", ContentSectionId = section.Id };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();

        var auth = new TeacherAuthorizationService(db);
        var handler = new CreateInlineExamCommandHandler(db, auth);

        // Teacher B tries to create inline exam on Teacher A's lesson -> fails
        var cmdB = new CreateInlineExamCommand
        {
            Title = "Exam B",
            Target = new ExamTargetDto { Type = "Lesson", Id = lesson.Id },
            CurrentUserId = userB.Id
        };
        var resultB = await handler.Handle(cmdB, CancellationToken.None);
        Assert.False(resultB.Success);

        // Teacher A tries to create inline exam -> succeeds
        var cmdA = new CreateInlineExamCommand
        {
            Title = "Exam A",
            Target = new ExamTargetDto { Type = "Lesson", Id = lesson.Id },
            CurrentUserId = userA.Id
        };
        var resultA = await handler.Handle(cmdA, CancellationToken.None);
        Assert.True(resultA.Success);
    }

    [Fact]
    public async Task AddQuestionsToExam_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var examA = new Exam { Id = Guid.NewGuid(), Title = "Exam A", CreatedByTeacherId = profileA.Id };
        db.Exams.Add(examA);
        await db.SaveChangesAsync();

        var auth = new TeacherAuthorizationService(db);
        var handler = new AddQuestionsToExamCommandHandler(db, auth);

        // Teacher B tries to add questions to Teacher A's exam -> fails
        var resultB = await handler.Handle(new AddQuestionsToExamCommand
        {
            ExamId = examA.Id,
            CurrentUserId = userB.Id,
            Questions = new List<InlineExamQuestionDto> { new InlineExamQuestionDto { Text = "Q", Points = 1, Order = 1 } }
        }, CancellationToken.None);
        Assert.False(resultB.Success);

        // Teacher A tries to add questions -> succeeds
        var resultA = await handler.Handle(new AddQuestionsToExamCommand
        {
            ExamId = examA.Id,
            CurrentUserId = userA.Id,
            Questions = new List<InlineExamQuestionDto> { new InlineExamQuestionDto { Text = "Q", Points = 1, Order = 1 } }
        }, CancellationToken.None);
        Assert.True(resultA.Success);
    }

    [Fact]
    public async Task GradeEssay_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var question = new QuestionBankItem { Id = Guid.NewGuid(), CreatedByTeacherId = profileA.Id };
        db.QuestionBankItems.Add(question);

        var submission = new EssaySubmission
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Status = EssaySubmissionStatus.WaitTeacher,
            StudentExamAttemptId = Guid.NewGuid()
        };
        db.EssaySubmissions.Add(submission);
        await db.SaveChangesAsync();

        var auth = new TeacherAuthorizationService(db);
        var handler = new GradeEssayCommandHandler(db, auth);

        // Teacher B tries to grade Teacher A's essay submission -> fails
        var resultB = await handler.Handle(new GradeEssayCommand(submission.Id, 5m, "FB", userB.Id), CancellationToken.None);
        Assert.False(resultB.Success);

        // Teacher A tries to grade -> succeeds
        var resultA = await handler.Handle(new GradeEssayCommand(submission.Id, 5m, "FB", userA.Id), CancellationToken.None);
        Assert.True(resultA.Success);
    }
}
