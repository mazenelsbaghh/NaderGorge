using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.LearningCenter;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.LearningCenter;

public sealed class LearningCenterDatabase : IAsyncLifetime
{
    private readonly string connection = Environment.GetEnvironmentVariable("MASSAR_LEARNING_TEST_CONNECTION")
        ?? "Host=127.0.0.1;Port=55441;Database=massar_learning_test;Username=postgres;Password=learning-test-only";
    public AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    public async Task InitializeAsync()
    {
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (settings.Database != "massar_learning_test" || settings.Host is not ("localhost" or "127.0.0.1"))
            throw new InvalidOperationException("Learning tests require their isolated local database.");
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await using var db = Open();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
}

public sealed class LearningCenterTests(LearningCenterDatabase fixture) : IClassFixture<LearningCenterDatabase>
{
    [Fact]
    public async Task Teacher_cannot_read_or_change_another_teachers_question_and_admin_can_see_both()
    {
        await using var db = fixture.Open();
        var first = await SeedCourse(db);
        var second = await SeedCourse(db);
        var bank = new LearningQuestionBank(db);
        var id = await bank.SaveAsync(first.Teacher.UserId, new(null, Input(first.Lesson.Id)), default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bank.ClassifyAsync(second.Teacher.UserId,
            new(id, new(second.Lesson.Id, "فكرة", 2)), default));
        var visible = await bank.ListAsync(second.Teacher.UserId, new(), default);
        Assert.DoesNotContain(visible.Items, q => q.Id == id);
        var admin = await SeedActor(db, RoleType.Admin);
        var options = await bank.OptionsAsync(admin.Id, default);
        Assert.Contains(options.Packages, p => p.Id == first.Package.Id);
        Assert.Contains(options.Packages, p => p.Id == second.Package.Id);
        var orphan = await SeedActor(db, RoleType.Teacher);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bank.OptionsAsync(orphan.Id, default));
    }

    [Fact]
    public async Task New_question_version_preserves_existing_exam_and_attempt_definition()
    {
        await using var db = fixture.Open();
        var course = await SeedCourse(db);
        var bank = new LearningQuestionBank(db);
        var oldId = await bank.SaveAsync(course.Teacher.UserId, new(null, Input(course.Lesson.Id)), default);
        var question = await db.QuestionBankItems.Include(q => q.Options).SingleAsync(q => q.Id == oldId);
        var exam = await SeedExam(db, course, question);
        var student = await SeedActor(db, RoleType.Student);
        var attempt = await Answer(db, exam, student, true);
        var before = attempt.DefinitionSnapshotJson;
        var newId = await bank.SaveAsync(course.Teacher.UserId, new(oldId, Input(course.Lesson.Id) with { Text = "النص الجديد" }), default);
        db.ChangeTracker.Clear();
        Assert.Equal(oldId, (await db.ExamQuestions.SingleAsync(q => q.ExamId == exam.Id)).QuestionBankItemId);
        Assert.Equal(AssessmentDefinitionSnapshot.Read(before!, "exam", exam.Id).ToJson(),
            AssessmentDefinitionSnapshot.Read((await db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id)).DefinitionSnapshotJson!, "exam", exam.Id).ToJson());
        Assert.Equal("السؤال الأصلي", (await db.QuestionBankItems.SingleAsync(q => q.Id == oldId)).Text);
        Assert.Equal(newId, (await db.QuestionBankItems.SingleAsync(q => q.Id == oldId)).SupersededByQuestionId);
        Assert.Contains((await bank.ListAsync(course.Teacher.UserId, new(), default)).Items, q => q.Id == newId);
    }

    [Fact]
    public async Task Map_counts_latest_answer_per_student_and_excludes_pending_attempts()
    {
        await using var db = fixture.Open();
        var course = await SeedCourse(db);
        var bank = new LearningQuestionBank(db);
        var id = await bank.SaveAsync(course.Teacher.UserId, new(null, Input(course.Lesson.Id)), default);
        var exam = await SeedExam(db, course, await db.QuestionBankItems.Include(q => q.Options).SingleAsync(q => q.Id == id));
        for (var index = 0; index < 5; index++)
        {
            var student = await SeedActor(db, RoleType.Student);
            var old = await Answer(db, exam, student, true);
            old.CreatedAt = DateTime.UtcNow.AddDays(-2);
            await db.SaveChangesAsync();
            if (index == 0) await Answer(db, exam, student, false);
        }
        var pendingStudent = await SeedActor(db, RoleType.Student);
        var pending = await Answer(db, exam, pendingStudent, false);
        pending.Evaluation = null;
        await db.SaveChangesAsync();
        var overview = await new LearningOverview(db).ReadAsync(course.Teacher.UserId, new(PackageId: course.Package.Id), default);
        var concept = Assert.Single(overview.Concepts);
        Assert.Equal(5, concept.Students);
        Assert.Equal(80, concept.CorrectPercent);
        Assert.Equal(6, concept.Attempts);
        Assert.Equal(1, overview.ExcludedAttempts);
        Assert.Single(concept.StudentsNeedingReview);
        Assert.Equal("خطأ", Assert.Single(concept.Questions).CommonWrongAnswer);
    }

    [Fact]
    public async Task Repeated_attempts_by_one_student_do_not_satisfy_minimum_sample()
    {
        await using var db = fixture.Open();
        var course = await SeedCourse(db);
        var id = await new LearningQuestionBank(db).SaveAsync(course.Teacher.UserId, new(null, Input(course.Lesson.Id)), default);
        var exam = await SeedExam(db, course, await db.QuestionBankItems.Include(q => q.Options).SingleAsync(q => q.Id == id));
        var student = await SeedActor(db, RoleType.Student);
        for (var index = 0; index < 6; index++) await Answer(db, exam, student, true);
        var map = await new LearningOverview(db).ReadAsync(course.Teacher.UserId, new(PackageId: course.Package.Id), default);
        Assert.Null(Assert.Single(map.Concepts).CorrectPercent);
    }

    [Fact]
    public async Task Forms_are_balanced_disjoint_inactive_and_retry_does_not_duplicate_them()
    {
        await using var db = fixture.Open();
        var course = await SeedCourse(db);
        var bank = new LearningQuestionBank(db);
        for (var index = 0; index < 8; index++) await bank.SaveAsync(course.Teacher.UserId,
            new(null, Input(course.Lesson.Id) with { Points = index < 4 ? 1 : 2 }), default);
        var request = new GenerateLearningForms(Guid.NewGuid(), course.Package.Id, "اختبار", 2, 30, 60,
            [new(course.Lesson.Id, "الاتزان", 2, 3)]);
        var generator = new LearningFormGenerator(db);
        var generated = await generator.GenerateAsync(course.Teacher.UserId, request, default);
        var repeated = await generator.GenerateAsync(course.Teacher.UserId, request, default);
        Assert.Equal(generated.Select(f => f.ExamId), repeated.Select(f => f.ExamId));
        Assert.Equal(generated[0].TotalScore, generated[1].TotalScore);
        var ids = generated.Select(f => f.ExamId).ToArray();
        var exams = await db.Exams.Include(e => e.ExamQuestions).Where(e => ids.Contains(e.Id)).ToListAsync();
        Assert.All(exams, e => Assert.False(e.IsActive));
        Assert.Equal(6, exams.SelectMany(e => e.ExamQuestions).Select(q => q.QuestionBankItemId).Distinct().Count());
        await Assert.ThrowsAsync<ArgumentException>(() => generator.GenerateAsync(course.Teacher.UserId,
            request with { RequestId = Guid.NewGuid(), Blueprint = [new(course.Lesson.Id, "الاتزان", 2, 10)] }, default));
        Assert.Equal(2, await db.Exams.CountAsync(e => e.CreatedByTeacherId == course.Teacher.Id));
    }

    [Fact]
    public async Task Follow_up_is_durable_scoped_and_active_exam_prevents_false_inactivity()
    {
        await using var db = fixture.Open();
        var course = await SeedCourse(db);
        var student = await SeedActor(db, RoleType.Student);
        db.StudentAccessGrants.Add(new() { UserId = student.Id, PackageId = course.Package.Id,
            GrantType = CodeType.Package, GrantedAt = DateTime.UtcNow.AddDays(-20) });
        await db.SaveChangesAsync();
        var followUps = new LearningFollowUps(db);
        var filter = new LearningFilter(PackageId: course.Package.Id);
        Assert.Single(await followUps.ReadAsync(course.Teacher.UserId, filter, default));
        await followUps.SaveAsync(course.Teacher.UserId, new(student.Id, course.Package.Id, "InProgress", "مراجعة الاتزان", "توقف عن المذاكرة"), default);
        db.ChangeTracker.Clear();
        var saved = Assert.Single(await followUps.ReadAsync(course.Teacher.UserId, filter, default));
        Assert.Equal("InProgress", saved.Status);
        Assert.Equal("مراجعة الاتزان", saved.Note);
        var questionId = await new LearningQuestionBank(db).SaveAsync(course.Teacher.UserId, new(null, Input(course.Lesson.Id)), default);
        var exam = await SeedExam(db, course, await db.QuestionBankItems.Include(q => q.Options).SingleAsync(q => q.Id == questionId));
        db.StudentExamAttempts.Add(new() { UserId = student.Id, ExamId = exam.Id, StartedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var active = Assert.Single(await followUps.ReadAsync(course.Teacher.UserId, filter, default));
        Assert.Empty(active.Reasons);
        Assert.Single(await followUps.HistoryAsync(course.Teacher.UserId, new(course.Package.Id, student.Id), default));
        var stranger = await SeedCourse(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => followUps.SaveAsync(stranger.Teacher.UserId,
            new(student.Id, course.Package.Id, "Completed", "غير مسموح", "سبب"), default));
    }

    [Fact]
    public async Task Invalid_import_rolls_back_all_questions()
    {
        await using var db = fixture.Open();
        var course = await SeedCourse(db);
        var bank = new LearningQuestionBank(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bank.ImportAsync(course.Teacher.UserId,
            [Input(course.Lesson.Id), Input(Guid.NewGuid())], default));
        db.ChangeTracker.Clear();
        Assert.Empty((await bank.ListAsync(course.Teacher.UserId, new(), default)).Items);
    }

    private static SaveLearningQuestion Input(Guid lessonId) => new("السؤال الأصلي", lessonId, "الاتزان", 2, 1, "", "شرح",
        [new(null, "صح", true), new(null, "خطأ", false)]);

    private static async Task<User> SeedActor(AppDbContext db, RoleType roleType)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Type == roleType);
        role ??= new Role { Name = roleType.ToString(), Type = roleType };
        var actor = new User { FullName = $"{roleType} test", PhoneNumber = Guid.NewGuid().ToString("N")[..18], PasswordHash = "test-only" };
        actor.UserRoles.Add(new UserRole { User = actor, Role = role });
        db.Users.Add(actor);
        await db.SaveChangesAsync();
        return actor;
    }

    private static async Task<CourseFixture> SeedCourse(AppDbContext db)
    {
        var user = await SeedActor(db, RoleType.Teacher);
        var teacher = new TeacherProfile { User = user };
        var subject = new Subject { Name = "مادة " + Guid.NewGuid(), NormalizedName = Guid.NewGuid().ToString() };
        var package = new Package { Name = "كورس", Teacher = teacher, Subject = subject };
        var lesson = new Lesson { Title = "درس", ContentSection = new ContentSection { Title = "قسم", Term = new Term { Title = "ترم", Package = package } } };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();
        return new(teacher, package, lesson);
    }

    private static async Task<Exam> SeedExam(AppDbContext db, CourseFixture course, QuestionBankItem question)
    {
        var exam = new Exam { Title = "تقييم", CreatedByTeacherId = course.Teacher.Id, TotalScore = 1,
            LessonVideo = new LessonVideo { Title = "فيديو", LessonId = course.Lesson.Id, Provider = "youtube", ProviderVideoId = "test", VideoTypeId = (await db.VideoTypes.FirstAsync()).Id } };
        exam.ExamQuestions.Add(new ExamQuestion { Question = question, Points = 1 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        return exam;
    }

    private static async Task<StudentExamAttempt> Answer(AppDbContext db, Exam exam, User student, bool correct)
    {
        var question = exam.ExamQuestions.Single();
        var option = question.Question.Options.First(o => o.IsCorrect == correct);
        var attempt = new StudentExamAttempt { ExamId = exam.Id, UserId = student.Id, Evaluation = "مكتمل",
            ScoreAchieved = correct ? 1 : 0, DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson() };
        attempt.Answers.Add(new StudentAnswer { ExamQuestionId = question.Id, IsCorrect = correct,
            PointsAwarded = correct ? 1 : 0, SelectedOptionId = option.Id });
        db.StudentExamAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return attempt;
    }
    private sealed record CourseFixture(TeacherProfile Teacher, Package Package, Lesson Lesson);
}
