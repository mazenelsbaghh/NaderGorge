using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests;

public sealed class AssessmentExportTests
{
    [Theory]
    [InlineData(AssessmentKind.Exam)]
    [InlineData(AssessmentKind.Homework)]
    public async Task ExactAssessmentExportKeepsSameNamedCopiesSeparateAndPreservesPendingAndHistoricalScores(AssessmentKind kind)
    {
        await using var fixture = await ExportFixture.Create(kind);
        var report = await fixture.Read(fixture.Teacher.UserId, AssessmentExportSelection.Grades);
        Assert.True(report.Success, report.Message);
        Assert.Equal(kind == AssessmentKind.Exam ? 4 : 2, report.Data!.Rows.Count);
        Assert.DoesNotContain(report.Data.Rows, r => r.Student.Id == fixture.CopyStudent.Id);
        Assert.All(report.Data.Rows, row => Assert.Equal(10, row.Maximum));
        Assert.Contains(report.Data.Rows, row => row.Status == "بانتظار التصحيح" && !row.IsFinal);

        using var workbook = new XLWorkbook(new MemoryStream(AssessmentExportWorkbook.Build(report.Data, default)));
        var summary = workbook.Worksheet("درجات الطلاب");
        Assert.True(summary.RightToLeft);
        Assert.Equal(3, summary.LastRowUsed()!.RowNumber());
        Assert.Equal(kind == AssessmentKind.Exam ? 5 : 3, workbook.Worksheet("كل المحاولات").LastRowUsed()!.RowNumber());
        var bestRow = summary.RowsUsed().Skip(1).Single(r => r.Cell(13).GetString() == fixture.Student.Id.ToString());
        Assert.Equal(8, bestRow.Cell(7).GetValue<decimal>());
        Assert.Equal("=1+1", bestRow.Cell(1).GetString());
        Assert.False(bestRow.Cell(1).HasFormula);
        Assert.Equal("00001", bestRow.Cell(2).GetString());
        Assert.Equal(XLDataType.Text, bestRow.Cell(3).DataType);
        Assert.Equal(fixture.Student.PhoneNumber, bestRow.Cell(3).GetString());
        Assert.Equal(fixture.TargetId.ToString(), workbook.Worksheet("بيانات الكشف").Cell(3, 2).GetString());
        Assert.Equal(8, report.Data.Rows.Where(r => r.Student.Id == fixture.Student.Id && r.IsFinal).Max(r => r.Score));
    }

    [Theory]
    [InlineData(AssessmentKind.Exam)]
    [InlineData(AssessmentKind.Homework)]
    public async Task AbsenceUsesEffectiveTargetGrantsAndDoesNotCountPendingSubmissionsAsAbsent(AssessmentKind kind)
    {
        await using var fixture = await ExportFixture.Create(kind);
        var report = await fixture.Read(fixture.Admin.Id, AssessmentExportSelection.Absent);
        Assert.True(report.Success, report.Message);
        Assert.Equal(fixture.AbsentIds.Order(), report.Data!.Rows.Select(r => r.Student.Id).Order());
        Assert.All(report.Data.Rows, row => { Assert.Null(row.Score); Assert.Null(row.AttemptId); });
        using var workbook = new XLWorkbook(new MemoryStream(AssessmentExportWorkbook.Build(report.Data, default)));
        Assert.Equal(fixture.AbsentIds.Length + 1, workbook.Worksheet("الغياب").LastRowUsed()!.RowNumber());
        Assert.DoesNotContain(report.Data.Rows, row => row.Student.Id == fixture.CopyStudent.Id);
    }

    [Theory]
    [InlineData(AssessmentKind.Exam)]
    [InlineData(AssessmentKind.Homework)]
    public async Task OtherTeachersRestrictedStaffAndStudentsCannotExportAssessmentData(AssessmentKind kind)
    {
        await using var fixture = await ExportFixture.Create(kind);
        foreach (var actor in new[] { fixture.OtherTeacher.UserId, fixture.RestrictedStaff.Id, fixture.ContentStaff.Id, fixture.Student.Id })
        {
            var report = await fixture.Read(actor, AssessmentExportSelection.Grades);
            Assert.False(report.Success);
            Assert.Null(report.Data);
            Assert.Contains("ASSESSMENT_EXPORT_FORBIDDEN", report.Errors!);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VideoSpecificExamAbsenceRecognizesBothExamLinkDirections(bool videoOwnsLink)
    {
        await using var fixture = await ExportFixture.Create(AssessmentKind.Exam);
        fixture.Lesson.ExamId = null;
        var video = new LessonVideo { Lesson = fixture.Lesson, Title = "Video", Provider = "youtube", ProviderVideoId = "synthetic",
            VideoType = new VideoType { Name = "Lesson", NormalizedName = "LESSON" }, ExamId = videoOwnsLink ? fixture.TargetId : null };
        fixture.Db.LessonVideos.Add(video);
        await fixture.Db.SaveChangesAsync();
        if (!videoOwnsLink) (await fixture.Db.Exams.FindAsync(fixture.TargetId))!.LessonVideoId = video.Id;
        fixture.Db.StudentAccessGrants.Add(new() { User = fixture.CopyStudent, GrantType = CodeType.Video, LessonVideoId = video.Id });
        await fixture.Db.SaveChangesAsync();
        var report = await fixture.Read(fixture.Teacher.UserId, AssessmentExportSelection.Absent);
        Assert.True(report.Success, report.Message);
        Assert.Contains(report.Data!.Rows, row => row.Student.Id == fixture.CopyStudent.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublicExamAbsenceUsesExactPaidEntitlementsOrTheTeachersEligibleFreeAudience(bool paid)
    {
        await using var fixture = await ExportFixture.Create(AssessmentKind.Exam);
        fixture.Lesson.ExamId = null;
        var product = new PublicExamProduct { ExamId = fixture.TargetId, Teacher = fixture.Teacher,
            IsPaid = paid, Price = paid ? 10 : 0, IsPublished = true, Slug = Guid.NewGuid().ToString("N"), CreatedByUser = fixture.Admin };
        fixture.Db.PublicExamProducts.Add(product);
        fixture.Db.StudentFacingAcademicScopes.Add(new() { OwnerType = StudentFacingScopeOwnerType.PublicExamProduct,
            OwnerId = product.Id, ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondaryGrade3 });
        fixture.Db.StudentAccessGrants.Add(new() { User = fixture.CopyStudent, GrantType = CodeType.Exam, ExamId = fixture.TargetId, PublicExamProduct = product });
        await fixture.Db.SaveChangesAsync();
        var report = await fixture.Read(fixture.Teacher.UserId, AssessmentExportSelection.Absent);
        Assert.True(report.Success, report.Message);
        var expected = paid ? new[] { fixture.CopyStudent.Id } : fixture.AbsentIds.Append(fixture.CopyStudent.Id).ToArray();
        Assert.Equal(expected.Order(), report.Data!.Rows.Select(r => r.Student.Id).Order());
    }

    private sealed class ExportFixture(SqliteConnection connection, AppDbContext db) : IAsyncDisposable
    {
        public AppDbContext Db { get; } = db;
        public required TeacherProfile Teacher { get; init; }
        public required TeacherProfile OtherTeacher { get; init; }
        public required User Admin { get; init; }
        public required User RestrictedStaff { get; init; }
        public required User ContentStaff { get; init; }
        public required User Student { get; init; }
        public required User CopyStudent { get; init; }
        public required Lesson Lesson { get; init; }
        public required Guid[] AbsentIds { get; init; }
        public Guid TargetId { get; private set; }
        private AssessmentKind Kind { get; set; }

        public Task<NaderGorge.Application.Common.ApiResponse<AssessmentExportReport>> Read(Guid actor, AssessmentExportSelection selection) =>
            new GetAssessmentExportQueryHandler(Db, new TeacherAuthorizationService(Db),
                new AccessCheckService(Db, new AcademicScopeService(Db)))
                .Handle(new(new(Kind, TargetId, Guid.Empty, actor), selection), default);

        public static async Task<ExportFixture> Create(AssessmentKind kind)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var teacherRole = new Role { Name = "Teacher", Type = RoleType.Teacher };
            var studentRole = new Role { Name = "Student", Type = RoleType.Student };
            var admin = NewUser("Admin", new Role { Name = "Admin", Type = RoleType.Admin });
            var teacher = new TeacherProfile { User = NewUser("Teacher", teacherRole) };
            var otherTeacher = new TeacherProfile { User = NewUser("Other teacher", teacherRole) };
            var restricted = NewUser("Staff", teacherRole);
            var contentStaff = NewUser("Content staff", teacherRole);
            var subject = new Subject { Name = "History", NormalizedName = "HISTORY" };
            var package = new Package { Name = "Course", Teacher = teacher, Subject = subject };
            var term = new Term { Title = "Term", Package = package };
            var section = new ContentSection { Title = "Month", Term = term };
            var lesson = new Lesson { Title = "Lesson", ContentSection = section };
            var copyLesson = new Lesson { Title = "Lesson", ContentSection = section };
            db.AddRange(admin, otherTeacher, package, term, section, lesson, copyLesson,
                new TeacherStaffMember { Teacher = teacher, User = restricted, CreatedByTeacherUser = teacher.User, PermissionKeys = "students" },
                new TeacherStaffMember { Teacher = teacher, User = contentStaff, CreatedByTeacherUser = teacher.User, PermissionKeys = "content" },
                new StudentFacingAcademicScope { OwnerType = StudentFacingScopeOwnerType.Package, OwnerId = package.Id,
                    ScopeLevel = AcademicScopeLevel.GradeAllSubjects, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondaryGrade3 });
            var student = NewStudent("=1+1", studentRole);
            student.StudentProfile!.StudentCode = "00001";
            var copyStudent = NewStudent("Copy only", studentRole);
            var pending = NewStudent("Pending", studentRole);
            var missing = Enumerable.Range(0, 4).Select(n => NewStudent("Absent " + n, studentRole)).ToArray();
            var excluded = Enumerable.Range(0, 4).Select(n => NewStudent("Excluded " + n, studentRole)).ToArray();
            excluded[3].StudentProfile!.GradeLevel = GradeLevel.FirstSecondary;
            db.Users.AddRange(new[] { student, copyStudent, pending }.Concat(missing).Concat(excluded));
            db.StudentAccessGrants.AddRange(
                new() { User = student, GrantType = CodeType.Package, PackageId = package.Id },
                new() { User = pending, GrantType = CodeType.Package, PackageId = package.Id },
                new() { User = copyStudent, GrantType = CodeType.Lesson, LessonId = copyLesson.Id },
                new() { User = missing[0], GrantType = CodeType.Lesson, LessonId = lesson.Id },
                new() { User = missing[1], GrantType = CodeType.Package, PackageId = package.Id, MaxUses = 1, UsesConsumed = 1 },
                new() { User = missing[2], GrantType = CodeType.Term, TermId = term.Id },
                new() { User = missing[3], GrantType = CodeType.Month, ContentSectionId = section.Id },
                new() { User = missing[1], GrantType = CodeType.Month, ContentSectionId = section.Id },
                new() { User = excluded[0], GrantType = CodeType.Package, PackageId = package.Id, CancelledAt = DateTime.UtcNow.AddDays(-1) },
                new() { User = excluded[1], GrantType = CodeType.Package, PackageId = package.Id, ExpiresAt = DateTime.UtcNow.AddDays(-1) },
                new() { User = excluded[2], GrantType = CodeType.Package, PackageId = package.Id, GrantedAt = DateTime.UtcNow.AddDays(1) },
                new() { User = excluded[3], GrantType = CodeType.Package, PackageId = package.Id });
            var fixture = new ExportFixture(connection, db) { Teacher = teacher, OtherTeacher = otherTeacher, Admin = admin,
                RestrictedStaff = restricted, ContentStaff = contentStaff, Student = student, CopyStudent = copyStudent, Lesson = lesson, AbsentIds = missing.Select(u => u.Id).ToArray(), Kind = kind };
            await fixture.SeedAssessments(copyLesson, pending, missing[0]);
            await db.SaveChangesAsync();
            return fixture;
        }

        private async Task SeedAssessments(Lesson copyLesson, User pending, User started)
        {
            if (Kind == AssessmentKind.Homework)
            {
                var homework = new Homework { Title = "Same title", LessonId = Lesson.Id, TotalScore = 20 };
                var copy = new Homework { Title = homework.Title, LessonId = copyLesson.Id, TotalScore = 10 };
                Db.Homeworks.AddRange(homework, copy);
                TargetId = homework.Id;
                foreach (var (user, score, status, target) in new[] { (Student, 8m, SubmissionStatus.Graded, homework),
                    (pending, 4m, SubmissionStatus.PendingReview, homework), (CopyStudent, 10m, SubmissionStatus.Graded, copy) })
                    Db.HomeworkSubmissions.Add(new() { Homework = target, Student = user, OverallScore = score,
                        TotalScoreSnapshot = 10, SubmittedAt = DateTime.UtcNow, Status = status });
                Db.HomeworkSubmissions.Add(new() { Homework = homework, Student = started, Status = SubmissionStatus.InProgress });
                return;
            }
            var exam = new Exam { Title = "Same title", CreatedByTeacher = Teacher, TotalScore = 10 };
            var clone = new Exam { Title = exam.Title, CreatedByTeacher = Teacher, TotalScore = 10 };
            Db.Exams.AddRange(exam, clone);
            Lesson.ExamId = exam.Id;
            copyLesson.ExamId = clone.Id;
            TargetId = exam.Id;
            var question = new QuestionBankItem { Text = "Essay", Type = NaderGorge.Domain.Entities.QuestionType.Essay,
                CreatedByTeacher = Teacher, SubjectId = Lesson.ContentSection.Term.Package.SubjectId };
            Db.QuestionBankItems.Add(question);
            foreach (var (user, score, isPending, target) in new[] { (Student, 3m, false, exam), (Student, 8m, false, exam),
                (Student, 9m, true, exam), (pending, 4m, true, exam), (CopyStudent, 10m, false, clone) })
            {
                var attempt = new StudentExamAttempt { Exam = target, User = user, ScoreAchieved = score,
                    DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(target).ToJson(), StartedAt = DateTime.UtcNow.AddMinutes(-3),
                    SubmittedAt = DateTime.UtcNow, Evaluation = isPending ? "بانتظار التصحيح" : "تم التصحيح" };
                Db.StudentExamAttempts.Add(attempt);
                if (isPending) Db.EssaySubmissions.Add(new() { Attempt = attempt, Student = user, Question = question, Status = EssaySubmissionStatus.WaitAI });
            }
            exam.TotalScore = 20;
            Db.StudentExamAttempts.Add(new() { Exam = exam, User = started, StartedAt = DateTime.UtcNow });
            await Task.CompletedTask;
        }

        private static User NewUser(string name, Role role) => new()
        {
            FullName = name, PhoneNumber = "010" + Guid.NewGuid().ToString("N")[..8], PasswordHash = "synthetic",
            UserRoles = [new() { Role = role }]
        };

        private static User NewStudent(string name, Role role)
        {
            var user = NewUser(name, role);
            user.StudentProfile = new() { User = user, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondaryGrade3 };
            return user;
        }

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
