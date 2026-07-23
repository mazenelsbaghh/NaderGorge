using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities.Student;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Parent.Queries;

public record GetStudentAcademicDetailsQuery(Guid StudentProfileId) : IRequest<ApiResponse<StudentAcademicDetailsDto>>;

public record StudentAcademicDetailsDto(
    string StudentName,
    string Grade,
    string? School,
    string? AvatarSlug,
    AttendanceDetailsDto Attendance,
    List<ExamDetailDto> Exams,
    List<HomeworkDetailDto> Homeworks,
    List<WarningDetailDto> Warnings,
    List<TeacherSummaryDto> Teachers,
    List<WatchLessonDetailDto> WatchLessons,
    BalanceDetailsDto Balance,
    List<CourseSummaryDto> Courses
);

public record AttendanceDetailsDto(
    int TotalLessons,
    int WatchedLessons,
    double CompletionRate
);

public record ExamDetailDto(
    Guid ExamId,
    Guid? AttemptId,
    Guid PackageId,
    string PackageName,
    Guid TermId,
    string TermTitle,
    Guid TeacherId,
    string TeacherName,
    string ExamTitle,
    decimal Score,
    decimal TotalScore,
    double Percentage,
    DateTime? SubmittedAt,
    string Status,
    List<QuestionReviewDto> Mistakes
);

public record HomeworkDetailDto(
    Guid HomeworkId,
    Guid TeacherId,
    string TeacherName,
    Guid PackageId,
    string PackageName,
    Guid TermId,
    string TermTitle,
    string Title,
    bool IsSubmitted,
    string SubmissionState,
    string? Grade,
    DateTime? SubmittedAt,
    List<HomeworkAnswerReviewDto> Mistakes
);

public record WarningDetailDto(
    string Reason,
    string Severity,
    DateTime CreatedAt
);

public record TeacherSummaryDto(
    Guid TeacherId,
    string TeacherName,
    string? Specialization,
    string? ProfileImageUrl
);

public record CourseSummaryDto(
    Guid PackageId,
    string PackageName,
    Guid TeacherId,
    string TeacherName,
    List<CourseTermSummaryDto> Terms
);

public record CourseTermSummaryDto(
    Guid TermId,
    string TermTitle,
    int LessonCount,
    int ExamCount
);

public record WatchLessonDetailDto(
    Guid PackageId,
    string PackageName,
    Guid TermId,
    string TermTitle,
    Guid TeacherId,
    string TeacherName,
    Guid LessonId,
    string LessonTitle,
    int TotalVideos,
    int WatchedVideos,
    int WatchCount,
    int WatchedSeconds,
    bool IsCompleted,
    DateTime? LastWatchedAt
);

public record QuestionReviewDto(
    string QuestionText,
    string? StudentAnswer,
    string? CorrectAnswer,
    string? WrittenCorrection,
    decimal PointsAwarded,
    decimal Points
);

public record HomeworkAnswerReviewDto(
    string QuestionText,
    string StudentAnswer,
    string? CorrectAnswer,
    string? WrittenCorrection,
    int? ScoreReceived,
    int Points
);

public record BalanceDetailsDto(
    decimal CurrentBalance,
    List<BalanceTransactionDetailDto> Transactions
);

public record BalanceTransactionDetailDto(
    decimal Amount,
    decimal BalanceAfter,
    string TransactionType,
    string Description,
    DateTime CreatedAt
);

public class GetStudentAcademicDetailsQueryHandler : IRequestHandler<GetStudentAcademicDetailsQuery, ApiResponse<StudentAcademicDetailsDto>>
{
    private readonly IAppDbContext _db;

    private sealed record PurchasedLessonRow(
        Guid LessonId,
        string LessonTitle,
        Guid PackageId,
        string PackageName,
        Guid TermId,
        string TermTitle,
        Guid TeacherId,
        string TeacherName,
        string? Specialization,
        string? ProfileImageUrl
    );

    private sealed record ExamLessonLink(Guid ExamId, Guid LessonId);

    public GetStudentAcademicDetailsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<StudentAcademicDetailsDto>> Handle(GetStudentAcademicDetailsQuery request, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == request.StudentProfileId, ct);

        if (profile == null)
        {
            return ApiResponse<StudentAcademicDetailsDto>.Fail("ملف الطالب غير موجود");
        }

        var now = DateTime.UtcNow;

        var activeGrants = await _db.StudentAccessGrants
            .AsNoTracking()
            .Where(g =>
                g.UserId == profile.UserId &&
                g.IsActive &&
                g.CancelledAt == null &&
                (g.ExpiresAt == null || g.ExpiresAt > now))
            .Select(g => new
            {
                g.PackageId,
                g.TermId,
                g.ContentSectionId,
                g.LessonId,
                g.LessonVideoId
            })
            .ToListAsync(ct);

        var packageIds = activeGrants.Where(g => g.PackageId.HasValue).Select(g => g.PackageId!.Value).Distinct().ToList();
        var termIds = activeGrants.Where(g => g.TermId.HasValue).Select(g => g.TermId!.Value).Distinct().ToList();
        var sectionIds = activeGrants.Where(g => g.ContentSectionId.HasValue).Select(g => g.ContentSectionId!.Value).Distinct().ToList();
        var directLessonIds = activeGrants.Where(g => g.LessonId.HasValue).Select(g => g.LessonId!.Value).Distinct().ToList();
        var directVideoIds = activeGrants.Where(g => g.LessonVideoId.HasValue).Select(g => g.LessonVideoId!.Value).Distinct().ToList();

        if (directVideoIds.Count > 0)
        {
            var lessonIdsFromVideoGrants = await _db.LessonVideos
                .AsNoTracking()
                .Where(v => directVideoIds.Contains(v.Id))
                .Select(v => v.LessonId)
                .ToListAsync(ct);

            directLessonIds.AddRange(lessonIdsFromVideoGrants);
            directLessonIds = directLessonIds.Distinct().ToList();
        }

        if (packageIds.Count > 0)
        {
            var termIdsFromPackages = await _db.Terms
                .AsNoTracking()
                .Where(t => packageIds.Contains(t.PackageId))
                .Select(t => t.Id)
                .ToListAsync(ct);

            termIds.AddRange(termIdsFromPackages);
            termIds = termIds.Distinct().ToList();
        }

        if (termIds.Count > 0)
        {
            var sectionIdsFromTerms = await _db.ContentSections
                .AsNoTracking()
                .Where(s => termIds.Contains(s.TermId))
                .Select(s => s.Id)
                .ToListAsync(ct);

            sectionIds.AddRange(sectionIdsFromTerms);
            sectionIds = sectionIds.Distinct().ToList();
        }

        if (sectionIds.Count > 0)
        {
            var lessonIdsFromSections = await _db.Lessons
                .AsNoTracking()
                .Where(l => sectionIds.Contains(l.ContentSectionId))
                .Select(l => l.Id)
                .ToListAsync(ct);

            directLessonIds.AddRange(lessonIdsFromSections);
            directLessonIds = directLessonIds.Distinct().ToList();
        }

        var lessonRows = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.ContentSection)
                .ThenInclude(s => s.Term)
                    .ThenInclude(t => t.Package)
                        .ThenInclude(p => p.Teacher)
                            .ThenInclude(t => t.User)
            .Where(l => directLessonIds.Contains(l.Id))
            .Select(l => new PurchasedLessonRow(
                l.Id,
                l.Title,
                l.ContentSection.Term.PackageId,
                l.ContentSection.Term.Package.Name,
                l.ContentSection.TermId,
                l.ContentSection.Term.Title,
                l.ContentSection.Term.Package.TeacherId,
                l.ContentSection.Term.Package.Teacher.User.FullName,
                l.ContentSection.Term.Package.Teacher.Specialization,
                l.ContentSection.Term.Package.Teacher.ProfileImageUrl
            ))
            .ToListAsync(ct);

        lessonRows = lessonRows
            .GroupBy(l => l.LessonId)
            .Select(g => g.First())
            .ToList();

        var lessonIds = lessonRows.Select(l => l.LessonId).ToList();
        var totalLessons = lessonIds.Count;
        var watchedLessons = await _db.LessonProgresses
            .AsNoTracking()
            .Where(lp => lp.UserId == profile.UserId && lp.IsCompleted && lessonIds.Contains(lp.LessonId))
            .CountAsync(ct);

        var completionRate = totalLessons > 0 ? Math.Round((double)watchedLessons / totalLessons * 100, 2) : 0.0;

        var attendance = new AttendanceDetailsDto(totalLessons, watchedLessons, completionRate);

        var teachers = lessonRows
            .GroupBy(l => new { l.TeacherId, l.TeacherName, l.Specialization, l.ProfileImageUrl })
            .Select(g => new TeacherSummaryDto(g.Key.TeacherId, g.Key.TeacherName, g.Key.Specialization, g.Key.ProfileImageUrl))
            .OrderBy(t => t.TeacherName)
            .ToList();

        var lessonIdsByTeacher = lessonRows.ToDictionary(l => l.LessonId);

        var watchEvents = await _db.VideoWatchEvents
            .AsNoTracking()
            .Where(w => w.UserId == profile.UserId && lessonIds.Contains(w.LessonVideo.LessonId))
            .Select(w => new
            {
                w.LessonVideo.LessonId,
                w.LessonVideoId,
                w.TimeWatchedInSeconds,
                w.WatchCount,
                LastWatchedAt = w.UpdatedAt ?? w.CreatedAt
            })
            .ToListAsync(ct);

        var videoCounts = await _db.LessonVideos
            .AsNoTracking()
            .Where(v => lessonIds.Contains(v.LessonId) && v.IsActive)
            .GroupBy(v => v.LessonId)
            .Select(g => new { LessonId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LessonId, x => x.Count, ct);

        var progressByLesson = await _db.LessonProgresses
            .AsNoTracking()
            .Where(lp => lp.UserId == profile.UserId && lessonIds.Contains(lp.LessonId))
            .ToDictionaryAsync(lp => lp.LessonId, lp => lp.IsCompleted, ct);

        var watchLessons = lessonRows
            .Select(lesson =>
            {
                var lessonWatchEvents = watchEvents.Where(w => w.LessonId == lesson.LessonId).ToList();
                return new WatchLessonDetailDto(
                    lesson.PackageId,
                    lesson.PackageName,
                    lesson.TermId,
                    lesson.TermTitle,
                    lesson.TeacherId,
                    lesson.TeacherName,
                    lesson.LessonId,
                    lesson.LessonTitle,
                    videoCounts.GetValueOrDefault(lesson.LessonId),
                    lessonWatchEvents.Select(w => w.LessonVideoId).Distinct().Count(),
                    lessonWatchEvents.Sum(w => w.WatchCount),
                    lessonWatchEvents.Sum(w => w.TimeWatchedInSeconds),
                    progressByLesson.GetValueOrDefault(lesson.LessonId),
                    lessonWatchEvents.Count == 0 ? null : lessonWatchEvents.Max(w => w.LastWatchedAt)
                );
            })
            .OrderBy(w => w.TeacherName)
            .ThenBy(w => w.LessonTitle)
            .ToList();

        var lessonExamLinks = await _db.Lessons
            .AsNoTracking()
            .Where(l => lessonIds.Contains(l.Id) && l.ExamId != null)
            .Select(l => new ExamLessonLink(l.ExamId!.Value, l.Id))
            .ToListAsync(ct);

        var videoExamLinks = await _db.LessonVideos
            .AsNoTracking()
            .Where(v => lessonIds.Contains(v.LessonId) && v.ExamId != null)
            .Select(v => new ExamLessonLink(v.ExamId!.Value, v.LessonId))
            .ToListAsync(ct);

        var examVideoLinks = await _db.Exams
            .AsNoTracking()
            .Where(e => e.LessonVideoId != null && lessonIds.Contains(e.LessonVideo!.LessonId))
            .Select(e => new ExamLessonLink(e.Id, e.LessonVideo!.LessonId))
            .ToListAsync(ct);

        var examLessonLinks = lessonExamLinks
            .Concat(videoExamLinks)
            .Concat(examVideoLinks)
            .GroupBy(x => x.ExamId)
            .Select(g => g.First())
            .ToList();

        var visibleExamIds = examLessonLinks.Select(x => x.ExamId).Distinct().ToList();
        var examLessonByExamId = examLessonLinks.ToDictionary(x => x.ExamId, x => x.LessonId);

        var examEntities = await _db.Exams
            .AsNoTracking()
            .Where(e => visibleExamIds.Contains(e.Id))
            .Include(e => e.ExamQuestions)
                .ThenInclude(eq => eq.Question)
                    .ThenInclude(q => q.Options)
            .ToListAsync(ct);

        var examAttempts = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == profile.UserId && visibleExamIds.Contains(a.ExamId))
            .Include(a => a.Exam)
            .Include(a => a.Answers)
                .ThenInclude(answer => answer.ExamQuestion)
                    .ThenInclude(eq => eq.Question)
                        .ThenInclude(q => q.Options)
            .Include(a => a.Answers)
                .ThenInclude(answer => answer.SelectedOption)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var latestAttemptByExamId = examAttempts
            .GroupBy(a => a.ExamId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).First());

        var exams = examEntities
            .Select(exam =>
            {
                var lesson = lessonIdsByTeacher[examLessonByExamId[exam.Id]];
                latestAttemptByExamId.TryGetValue(exam.Id, out var attempt);

                return new ExamDetailDto(
                    exam.Id,
                    attempt?.Id,
                    lesson.PackageId,
                    lesson.PackageName,
                    lesson.TermId,
                    lesson.TermTitle,
                    lesson.TeacherId,
                    lesson.TeacherName,
                    exam.Title,
                    attempt?.ScoreAchieved ?? 0m,
                    exam.TotalScore,
                    attempt != null && exam.TotalScore > 0 ? (double)Math.Round((attempt.ScoreAchieved / exam.TotalScore) * 100, 2) : 0.0,
                    attempt?.CreatedAt,
                    attempt == null ? "NotStarted" : attempt.IsPassed ? "Passed" : "Failed",
                    attempt?.Answers
                        .Where(answer => !answer.IsCorrect)
                        .OrderBy(answer => answer.ExamQuestion.Order)
                        .Select(answer => new QuestionReviewDto(
                            answer.ExamQuestion.Question.Text,
                            answer.SelectedOption?.Text ?? answer.SubmittedText,
                            answer.ExamQuestion.Question.Options.FirstOrDefault(o => o.IsCorrect)?.Text,
                            answer.ExamQuestion.Question.WrittenCorrection,
                            answer.PointsAwarded,
                            answer.ExamQuestion.Points
                        ))
                        .ToList() ?? new List<QuestionReviewDto>()
                );
            })
            .OrderBy(e => e.TeacherName)
            .ThenBy(e => e.ExamTitle)
            .ToList();

        // Fetch Homework submissions
        var homeworksRaw = await _db.Homeworks
            .AsNoTracking()
            .Where(h => lessonIds.Contains(h.LessonId))
            .Include(h => h.Questions)
            .Include(h => h.Submissions.Where(s => s.StudentId == profile.UserId))
                .ThenInclude(s => s.Answers)
                    .ThenInclude(a => a.Question)
            .Select(h => new
            {
                h.Id,
                h.LessonId,
                h.Title,
                Submission = h.Submissions.FirstOrDefault(s => s.StudentId == profile.UserId)
            })
            .ToListAsync(ct);

        var homeworks = homeworksRaw.Select(h =>
        {
            var teacher = lessonIdsByTeacher[h.LessonId];
            return new HomeworkDetailDto(
                h.Id,
                teacher.TeacherId,
                teacher.TeacherName,
                teacher.PackageId,
                teacher.PackageName,
                teacher.TermId,
                teacher.TermTitle,
                h.Title,
                h.Submission != null && h.Submission.SubmittedAt != null,
                h.Submission == null ? "NotSubmitted" : h.Submission.Status.ToString(),
                h.Submission != null ? (h.Submission.Evaluation ?? h.Submission.OverallScore.ToString("G29")) : null,
                h.Submission?.SubmittedAt,
                h.Submission?.Answers
                    .Where(answer => answer.ScoreReceived == null || answer.ScoreReceived < answer.Question.PointsActive)
                    .OrderBy(answer => answer.Question.Order)
                    .Select(answer => new HomeworkAnswerReviewDto(
                        answer.Question.BodyText,
                        answer.ProvidedAnswer,
                        answer.Question.CorrectAnswerKey,
                        answer.Question.WrittenCorrection,
                        answer.ScoreReceived,
                        answer.Question.PointsActive
                    ))
                    .ToList() ?? new List<HomeworkAnswerReviewDto>()
            );
        }).ToList();

        // Fetch Warning events
        var warnings = await _db.WarningEvents
            .AsNoTracking()
            .Where(w => w.StudentId == profile.UserId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WarningDetailDto(
                w.TriggerReason,
                w.Severity.ToString(),
                w.CreatedAt
            ))
            .ToListAsync(ct);

        var gradeAr = MapGradeLevelAr(profile.GradeLevel.ToString());

        var balance = await _db.StudentBalances
            .AsNoTracking()
            .Where(b => b.UserId == profile.UserId)
            .Select(b => new BalanceDetailsDto(
                b.CurrentBalance,
                b.Transactions
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(20)
                    .Select(t => new BalanceTransactionDetailDto(
                        t.Amount,
                        t.BalanceAfter,
                        t.TransactionType,
                        t.Description,
                        t.CreatedAt
                    ))
                    .ToList()
            ))
            .FirstOrDefaultAsync(ct) ?? new BalanceDetailsDto(0m, new List<BalanceTransactionDetailDto>());

        var examCountsByTerm = exams
            .GroupBy(e => e.TermId)
            .ToDictionary(g => g.Key, g => g.Count());

        var courses = lessonRows
            .GroupBy(l => new { l.PackageId, l.PackageName, l.TeacherId, l.TeacherName })
            .Select(packageGroup => new CourseSummaryDto(
                packageGroup.Key.PackageId,
                packageGroup.Key.PackageName,
                packageGroup.Key.TeacherId,
                packageGroup.Key.TeacherName,
                packageGroup
                    .GroupBy(l => new { l.TermId, l.TermTitle })
                    .Select(termGroup => new CourseTermSummaryDto(
                        termGroup.Key.TermId,
                        termGroup.Key.TermTitle,
                        termGroup.Select(l => l.LessonId).Distinct().Count(),
                        examCountsByTerm.GetValueOrDefault(termGroup.Key.TermId)
                    ))
                    .OrderBy(t => t.TermTitle)
                    .ToList()
            ))
            .OrderBy(c => c.TeacherName)
            .ThenBy(c => c.PackageName)
            .ToList();

        var dto = new StudentAcademicDetailsDto(
            profile.User.FullName,
            gradeAr,
            profile.SchoolName,
            profile.AvatarSlug,
            attendance,
            exams,
            homeworks,
            warnings,
            teachers,
            watchLessons,
            balance,
            courses
        );

        return ApiResponse<StudentAcademicDetailsDto>.Ok(dto);
    }

    private static string MapGradeLevelAr(string grade)
    {
        return grade switch
        {
            "FirstPrimary" => "أولى ابتدائي",
            "SecondPrimary" => "ثانية ابتدائي",
            "ThirdPrimary" => "ثالثة ابتدائي",
            "FourthPrimary" => "رابعة ابتدائي",
            "FifthPrimary" => "خامسة ابتدائي",
            "SixthPrimary" => "سادسة ابتدائي",
            "FirstPreparatory" => "أولى إعدادي",
            "SecondPreparatory" => "ثانية إعدادي",
            "ThirdPreparatory" => "ثالثة إعدادي",
            "FirstSecondary" => "أولى ثانوي",
            "SecondSecondary" => "ثانية ثانوي",
            "ThirdSecondary" => "ثالثة ثانوي",
            _ => grade
        };
    }
}
