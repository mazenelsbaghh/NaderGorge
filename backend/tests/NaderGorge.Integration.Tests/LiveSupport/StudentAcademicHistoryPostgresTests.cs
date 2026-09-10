using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class StudentAcademicHistoryPostgresTests
{
    [Fact]
    public async Task StudentProfileShowsEveryAssessmentAttemptWithItsHistoricalGradeAndContext()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var student = User("طالب السجل", $"010{Random.Shared.NextInt64(10000000, 99999999)}");
        var teacher = new TeacherProfile { User = User("مدرس السجل", $"011{Random.Shared.NextInt64(10000000, 99999999)}") };
        var package = new Package
        {
            Name = $"باقة السجل {suffix}",
            Teacher = teacher,
            Subject = new Subject { Name = "التاريخ", NormalizedName = suffix }
        };
        var lesson = new Lesson
        {
            Title = "الحصة الأولى",
            ContentSection = new ContentSection
            {
                Title = "الشهر الأول",
                Term = new Term { Title = "الترم الأول", Package = package }
            }
        };
        var exam = new Exam { Title = "الامتحان الحالي", TotalScore = 30, PassingScore = 5, CreatedByTeacher = teacher };
        lesson.ExamId = exam.Id;
        var gradedAttempt = new StudentExamAttempt
        {
            User = student,
            Exam = exam,
            ScoreAchieved = 8,
            IsPassed = true,
            Evaluation = "جيد جدًا",
            StartedAt = DateTime.UtcNow.AddDays(-2),
            DefinitionSnapshotJson = (AssessmentDefinitionSnapshot.FromExam(exam) with
                { Title = "امتحان النسخة وقت الحل", TotalScore = 10 }).ToJson()
        };
        var pendingAttempt = new StudentExamAttempt
        {
            User = student,
            Exam = exam,
            Evaluation = "قيد التصحيح",
            StartedAt = DateTime.UtcNow.AddDays(-1)
        };
        var homework = new Homework { LessonId = lesson.Id, Title = "واجب الحصة", TotalScore = 20 };
        var secondHomework = new Homework { LessonId = lesson.Id, Title = "واجب إضافي", TotalScore = 10 };
        var gradedHomework = new HomeworkSubmission
        {
            Student = student,
            Homework = secondHomework,
            Status = SubmissionStatus.Graded,
            OverallScore = 12,
            TotalScoreSnapshot = 15,
            Evaluation = "ممتاز",
            SubmittedAt = DateTime.UtcNow.AddHours(-4)
        };
        var openedHomework = new HomeworkSubmission
        {
            Student = student,
            Homework = homework,
            Status = SubmissionStatus.InProgress,
            StartedAt = DateTime.UtcNow.AddHours(-2)
        };
        fixture.Db.AddRange(lesson, gradedAttempt, pendingAttempt, gradedHomework, openedHomework);
        await fixture.Db.SaveChangesAsync();

        var profile = await new GetStudentProfileDetailQueryHandler(fixture.Db)
            .Handle(new GetStudentProfileDetailQuery(student.Id), default);

        Assert.Equal(2, profile.ExamHistory.Count);
        var gradedExam = Assert.Single(profile.ExamHistory, item => item.AttemptId == gradedAttempt.Id);
        Assert.Equal("امتحان النسخة وقت الحل", gradedExam.Title);
        Assert.Equal((8m, 10m, true, "Graded"),
            (gradedExam.Score, gradedExam.TotalScore, gradedExam.HasFinalGrade, gradedExam.Status));
        Assert.Equal(package.Name, gradedExam.PackageName);
        Assert.Equal(lesson.Title, gradedExam.LessonTitle);
        Assert.Equal("PendingReview", profile.ExamHistory.Single(item => item.AttemptId == pendingAttempt.Id).Status);

        Assert.Equal(2, profile.HomeworkHistory.Count);
        var gradedSubmission = Assert.Single(profile.HomeworkHistory, item => item.SubmissionId == gradedHomework.Id);
        Assert.Equal((12m, 15m, true, "Graded"),
            (gradedSubmission.Score, gradedSubmission.TotalScore, gradedSubmission.HasFinalGrade, gradedSubmission.Status));
        Assert.Equal(package.Name, gradedSubmission.PackageName);
        Assert.Equal(lesson.Title, gradedSubmission.LessonTitle);
        Assert.Equal("InProgress", profile.HomeworkHistory.Single(item => item.SubmissionId == openedHomework.Id).Status);
    }

    private static User User(string name, string phone) => new()
    {
        FullName = name,
        PhoneNumber = phone,
        PasswordHash = "test-only",
        IsActive = true
    };
}
