using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class StudentGradesTests
{
    [Fact]
    public async Task Grades_OwnerIsolationPaginationAndHistoricalScores_TranslateOnRelationalDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "grades-student");
        var other = await TestAppDbContextFactory.SeedUserAsync(db, "Other", "grades-other");
        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher", "grades-teacher");
        var teacher = new TeacherProfile { User = teacherUser };
        var subject = new Subject { Name = "Physics", NormalizedName = "PHYSICS" };
        var package = new Package { Name = "Physics", Subject = subject, Teacher = teacher };
        var term = new Term { Title = "Term", Package = package };
        var section = new ContentSection { Title = "Section", Term = term };
        var exam = new Exam { Title = "Original exam", TotalScore = 10, CreatedByTeacher = teacher };
        var lesson = new Lesson { Title = "Lesson", ContentSection = section, ExamId = exam.Id };
        var snapshot = AssessmentDefinitionSnapshot.FromExam(exam).ToJson();
        exam.Title = "Changed exam";
        exam.TotalScore = 100;
        var homework = new Homework { Title = "Homework", LessonId = lesson.Id, TotalScore = 50 };
        db.AddRange(exam, lesson, homework);
        var started = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 22; index++)
            db.StudentExamAttempts.Add(new StudentExamAttempt { User = student, Exam = exam,
                StartedAt = started.AddMinutes(index), Evaluation = "ممتاز", ScoreAchieved = 8, DefinitionSnapshotJson = snapshot });
        db.StudentExamAttempts.Add(new StudentExamAttempt { User = other, Exam = exam, Evaluation = "ممتاز", ScoreAchieved = 10 });
        db.StudentExamAttempts.Add(new StudentExamAttempt { User = student, Exam = exam, Evaluation = null });
        db.StudentExamAttempts.Add(new StudentExamAttempt { User = student, Exam = exam, Evaluation = "قيد التصحيح", ScoreAchieved = 6 });
        db.HomeworkSubmissions.Add(new HomeworkSubmission { Student = student, Homework = homework,
            Status = SubmissionStatus.PendingReview, OverallScore = 12, SubmittedAt = started.AddDays(1) });
        var secondLesson = new Lesson { Title = "Second lesson", ContentSection = section };
        db.Lessons.Add(secondLesson);
        var gradedHomework = new Homework { Title = "Graded homework", LessonId = secondLesson.Id, TotalScore = 50 };
        db.HomeworkSubmissions.Add(new HomeworkSubmission { Student = student, Homework = gradedHomework,
            Status = SubmissionStatus.Graded, OverallScore = 7, TotalScoreSnapshot = 10, SubmittedAt = started.AddDays(2) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var handler = new GetStudentGradesQueryHandler(db);
        var all = await handler.Handle(new(student.Id), default);
        Assert.True(all.Success, all.Message);
        Assert.Equal(25, all.Data!.TotalCount);
        Assert.Equal(20, all.Data.Items.Count);
        Assert.All(all.Data.Items.Where(row => row.Status == "PendingReview"), row => Assert.Null(row.Score));
        var exams = await handler.Handle(new(student.Id, "exam", 2), default);
        Assert.Equal(23, exams.Data!.TotalCount);
        Assert.Equal(3, exams.Data.Items.Count);
        Assert.All(exams.Data.Items, row => { Assert.Equal("Original exam", row.Title); Assert.Equal(10, row.TotalScore); Assert.Equal(8, row.Score); });
        var homeworks = await handler.Handle(new(student.Id, "homework"), default);
        Assert.Equal(2, homeworks.Data!.TotalCount);
        Assert.Equal(7, homeworks.Data.Items[0].Score);
        Assert.Equal(10, homeworks.Data.Items[0].TotalScore);
        Assert.Null(homeworks.Data.Items[1].Score);
        var empty = await handler.Handle(new(Guid.NewGuid()), default);
        Assert.Empty(empty.Data!.Items);
    }

    [Theory]
    [InlineData("invalid", 1)]
    [InlineData("all", 0)]
    public async Task InvalidPagingOrKind_DoesNotReturnResults(string kind, int page)
    {
        await using var db = TestAppDbContextFactory.Create();
        Assert.False((await new GetStudentGradesQueryHandler(db).Handle(new(Guid.NewGuid(), kind, page), default)).Success);
    }
}
