using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetStudentGradesQuery(Guid UserId, string Kind = "all", int Page = 1)
    : IRequest<ApiResponse<StudentGradesDto>>;
public record StudentGradeDto(Guid Id, string Kind, string Title, string? LessonTitle,
    string Status, decimal? Score, decimal TotalScore, DateTime AttemptedAt);
public record StudentGradesDto(List<StudentGradeDto> Items, int TotalCount, int Page, int PageSize);

public sealed class GetStudentGradesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetStudentGradesQuery, ApiResponse<StudentGradesDto>>
{
    private const int PageSize = 20;

    public async Task<ApiResponse<StudentGradesDto>> Handle(GetStudentGradesQuery request, CancellationToken ct)
    {
        if (request.Kind is not ("all" or "exam" or "homework") || request.Page < 1 || request.Page > 100000)
            return ApiResponse<StudentGradesDto>.Fail("اختيار نوع النتائج أو رقم الصفحة غير صحيح.");
        // Historical grades belong to their owner even after an assessment is archived.
        // This projection grants no access to assessment questions or other students.
        var records = ExamRecords(request.UserId).Concat(HomeworkRecords(request.UserId));
        if (request.Kind != "all") records = records.Where(row => row.Kind == request.Kind);
        var count = await records.CountAsync(ct);
        var page = await records.OrderByDescending(row => row.AttemptedAt).ThenBy(row => row.Kind)
            .ThenBy(row => row.Id).Skip((request.Page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return ApiResponse<StudentGradesDto>.Ok(new(page.Select(ToGrade).ToList(), count, request.Page, PageSize));
    }

    private IQueryable<GradeRecord> ExamRecords(Guid userId) => db.StudentExamAttempts.AsNoTracking()
        .Where(attempt => attempt.UserId == userId && attempt.Evaluation != null && attempt.Evaluation != "")
        .Select(attempt => new GradeRecord
        {
            Id = attempt.Id, AssessmentId = attempt.ExamId, Kind = "exam", Title = attempt.Exam.Title,
            Snapshot = attempt.DefinitionSnapshotJson, TotalScore = attempt.Exam.TotalScore,
            Score = attempt.ScoreAchieved,
            AwardedPoints = attempt.Answers.Sum(answer => answer.PointsAwarded),
            IsPassed = attempt.IsPassed, IsTimeExpired = attempt.IsTimeExpired,
            Status = attempt.Evaluation == "قيد التصحيح" ? "PendingReview" : "Graded",
            AttemptedAt = attempt.StartedAt ?? attempt.CreatedAt,
            LessonTitle = attempt.Exam.LessonVideo != null ? attempt.Exam.LessonVideo.Lesson.Title
                : db.Lessons.Where(lesson => lesson.ExamId == attempt.ExamId)
                    .OrderBy(lesson => lesson.Id).Select(lesson => lesson.Title).FirstOrDefault()
        });

    private IQueryable<GradeRecord> HomeworkRecords(Guid userId) => db.HomeworkSubmissions.AsNoTracking()
        .Where(submission => submission.StudentId == userId && submission.Status != SubmissionStatus.InProgress)
        .Select(submission => new GradeRecord
        {
            Id = submission.Id, AssessmentId = submission.HomeworkId, Kind = "homework", Title = submission.Homework.Title,
            Snapshot = submission.DefinitionSnapshotJson,
            TotalScore = submission.TotalScoreSnapshot ?? submission.Homework.TotalScore,
            Score = submission.OverallScore,
            AwardedPoints = 0, IsPassed = false, IsTimeExpired = false,
            Status = submission.Status == SubmissionStatus.Graded ? "Graded"
                : submission.Status == SubmissionStatus.Missed ? "Missed" : "PendingReview",
            AttemptedAt = submission.SubmittedAt ?? submission.StartedAt,
            LessonTitle = db.Lessons.Where(lesson => lesson.Id == submission.Homework.LessonId)
                .Select(lesson => lesson.Title).FirstOrDefault()
        });

    private static StudentGradeDto ToGrade(GradeRecord row)
    {
        if (row.Kind == "exam" && string.IsNullOrWhiteSpace(row.Snapshot))
            return new(row.Id, row.Kind, row.Title, row.LessonTitle, "ManualReconciliationRequired",
                null, 0, row.AttemptedAt);
        var snapshot = string.IsNullOrWhiteSpace(row.Snapshot) ? null
            : AssessmentDefinitionSnapshot.Read(row.Snapshot, row.Kind, row.AssessmentId);
        var scale = row.Kind == "exam"
            ? AssessmentAttemptScaleNormalizer.Project(row.Snapshot, row.AssessmentId, row.Score,
                row.IsPassed, row.IsTimeExpired, row.AwardedPoints)
            : null;
        return new(row.Id, row.Kind, snapshot?.Title ?? row.Title, row.LessonTitle, row.Status,
            row.Status == "Graded" ? scale?.ScoreAchieved ?? row.Score : null,
            scale?.Definition.TotalScore ?? snapshot?.TotalScore ?? row.TotalScore, row.AttemptedAt);
    }

    private sealed class GradeRecord
    {
        public Guid Id { get; init; }
        public Guid AssessmentId { get; init; }
        public string Kind { get; init; } = "";
        public string Title { get; init; } = "";
        public string? Snapshot { get; init; }
        public string? LessonTitle { get; init; }
        public string Status { get; init; } = "";
        public decimal Score { get; init; }
        public decimal AwardedPoints { get; init; }
        public bool IsPassed { get; init; }
        public bool IsTimeExpired { get; init; }
        public decimal TotalScore { get; init; }
        public DateTime AttemptedAt { get; init; }
    }
}
