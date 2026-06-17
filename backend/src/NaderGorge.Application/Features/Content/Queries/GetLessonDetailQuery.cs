using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetLessonDetailQuery(Guid LessonId, Guid UserId) : IRequest<ApiResponse<LessonDetailDto>>;

public record LessonDetailDto(
    Guid Id,
    string Title,
    string Summary,
    Guid PackageId,
    Guid? ExamId,
    bool ExamPassed,
    Guid? HomeworkId,
    bool HomeworkPassed,
    LessonHomeworkDto? Homework,
    List<VideoDto> Videos,
    bool IsLocked = false,
    string? LockedReason = null,
    Guid? BlockingExamId = null,
    Guid? BlockingHomeworkLessonId = null,
    decimal Price = 0,
    bool HasAccess = true,
    bool IsExamLocked = false,
    string? ExamLockedReason = null,
    string? ExamStatus = null,
    string? HomeworkStatus = null,
    Guid? TermId = null,
    Guid? SectionId = null
);

public record LessonHomeworkDto(Guid Id, string Title, string Instructions, bool IsMandatory, decimal? RequiredPointsToPass, decimal TotalScore, List<LessonHomeworkQuestionDto> Questions);

public record LessonHomeworkQuestionDto(
    Guid Id, 
    string Text, 
    int Order, 
    int MaxPoints,
    string QuestionType,
    string[]? PossibleAnswers = null,
    string? AudioUrl = null,
    string? HintText = null,
    string? BaseText = null,
    int? MistakeStartIndex = null,
    int? MistakeEndIndex = null
    // NO CorrectAnswerKey! NO WrittenCorrection! (security - prevents cheating)
);

public record VideoChapterDto(Guid Id, string Title, int StartTime, int EndTime, string SummaryText, string? MindmapImageUrl, int Order);
public record VideoExamDto(Guid ExamId, string Title, bool Passed, bool IsMandatory);

public record VideoDto(
    Guid Id,
    string Title,
    string Provider,
    int Order,
    int Limit,
    int Watched,
    bool IsLocked,
    int WatchedSeconds,
    DateTime? LastWatchedAt,
    string? SubtitleUrl,
    bool IsProcessingAI,
    bool IsProcessingMindmaps,
    Guid? ExamId,
    bool ExamPassed,
    bool IsExamLocked,
    List<VideoExamDto> Exams,
    List<VideoChapterDto> Chapters
);
public record ResourceDto(Guid Id, string Title, string FileUrl, string Type);

public class GetLessonDetailQueryHandler : IRequestHandler<GetLessonDetailQuery, ApiResponse<LessonDetailDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;
    private readonly TeacherAuthorizationService _auth;

    public GetLessonDetailQueryHandler(IAppDbContext db, IAccessCheckService access, TeacherAuthorizationService auth)
    {
        _db = db;
        _access = access;
        _auth = auth;
    }

    public async Task<ApiResponse<LessonDetailDto>> Handle(GetLessonDetailQuery request, CancellationToken ct)
    {
        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Videos.Where(v => v.IsActive))
                .ThenInclude(v => v.VideoChapters)
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);

        if (lesson == null)
            return ApiResponse<LessonDetailDto>.Fail("Lesson not found");

        var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, request.LessonId, ct);
        if (!hasAccess)
        {
            var minimalDetail = new LessonDetailDto(
                lesson.Id,
                lesson.Title,
                lesson.Summary,
                lesson.ContentSection.Term.PackageId,
                null,
                false,
                null,
                false,
                null,
                new List<VideoDto>(),
                false,
                null,
                null,
                null,
                lesson.Price,
                false,
                false,
                null,
                null,
                null,
                lesson.ContentSection?.TermId,
                lesson.ContentSectionId
            );
            return ApiResponse<LessonDetailDto>.Ok(minimalDetail);
        }

        var isAuthorizedTeacher = await _auth.CanAccessLessonAsync(request.UserId, request.LessonId, ct);
        if (!isAuthorizedTeacher)
            return ApiResponse<LessonDetailDto>.Fail("Unauthorized access to this lesson.");

        if (lesson == null)
            return ApiResponse<LessonDetailDto>.Fail("Lesson not found");

        bool isLocked = false;
        string? lockedReason = null;
        Guid? blockingExamId = null;
        Guid? blockingHomeworkLessonId = null;

        // Find the previous lesson
        var previousLesson = await _db.Lessons
            .Where(l => l.ContentSectionId == lesson.ContentSectionId && l.Order < lesson.Order)
            .OrderByDescending(l => l.Order)
            .FirstOrDefaultAsync(ct);

        if (previousLesson != null)
        {
            // 1. Check if previous lesson has an exam and if it is mandatory and passed
            if (!isLocked && previousLesson.ExamId.HasValue)
            {
                var exam = await _db.Exams.FindAsync(new object[] { previousLesson.ExamId.Value }, ct);

                if (exam != null && exam.IsMandatory)
                {
                    var passedExam = await _db.StudentExamAttempts
                        .AnyAsync(a => a.UserId == request.UserId && a.ExamId == previousLesson.ExamId.Value && a.IsPassed, ct);

                    if (!passedExam)
                    {
                        isLocked = true;
                        var attemptedExam = await _db.StudentExamAttempts
                            .AnyAsync(a => a.UserId == request.UserId && a.ExamId == previousLesson.ExamId.Value, ct);

                        if (!attemptedExam)
                        {
                            lockedReason = $"يجب حل امتحان '{exam.Title}' التابع للحصة '{previousLesson.Title}' أولاً لفتح هذه الحصة.";
                        }
                        else
                        {
                            lockedReason = $"يجب اجتياز امتحان '{exam.Title}' التابع للحصة '{previousLesson.Title}' بنجاح لفتح هذه الحصة.";
                        }
                        blockingExamId = exam.Id;
                    }
                }
            }

            // 1b. Check if any video in the previous lesson has a mandatory exam and if it is passed
            if (!isLocked)
            {
                var prevVideoExams = await _db.Exams
                    .Where(e => e.IsMandatory && (
                        (e.LessonVideo != null && e.LessonVideo.LessonId == previousLesson.Id) ||
                        _db.LessonVideos.Any(lv => lv.LessonId == previousLesson.Id && lv.ExamId == e.Id)
                    ))
                    .ToListAsync(ct);

                if (prevVideoExams.Any())
                {
                    var prevVideoExamIds = prevVideoExams.Select(e => e.Id).ToList();
                    var passedPrevVideoExamIds = await _db.StudentExamAttempts
                        .Where(a => a.UserId == request.UserId && prevVideoExamIds.Contains(a.ExamId) && a.IsPassed)
                        .Select(a => a.ExamId)
                        .ToListAsync(ct);

                    var unpassedVideoExam = prevVideoExams.FirstOrDefault(e => !passedPrevVideoExamIds.Contains(e.Id));
                    if (unpassedVideoExam != null)
                    {
                        isLocked = true;
                        var attemptedExam = await _db.StudentExamAttempts
                            .AnyAsync(a => a.UserId == request.UserId && a.ExamId == unpassedVideoExam.Id, ct);

                        if (!attemptedExam)
                        {
                            lockedReason = $"يجب حل امتحان الفيديو '{unpassedVideoExam.Title}' التابع للحصة السابقة '{previousLesson.Title}' أولاً لفتح هذه الحصة.";
                        }
                        else
                        {
                            lockedReason = $"يجب اجتياز امتحان الفيديو '{unpassedVideoExam.Title}' التابع للحصة السابقة '{previousLesson.Title}' بنجاح لفتح هذه الحصة.";
                        }
                        blockingExamId = unpassedVideoExam.Id;
                    }
                }
            }

            // 2. Check if previous lesson's mandatory homework is passed
            if (!isLocked)
            {
                var prevHomework = await _db.Homeworks.FirstOrDefaultAsync(h => h.LessonId == previousLesson.Id, ct);
                if (prevHomework != null && prevHomework.IsMandatory)
                {
                    var prevHwSubmission = await _db.HomeworkSubmissions
                        .Where(s => s.StudentId == request.UserId && s.HomeworkId == prevHomework.Id)
                        .OrderByDescending(s => s.SubmittedAt)
                        .FirstOrDefaultAsync(ct);

                    bool prevHwPassed = prevHwSubmission != null 
                                      && prevHwSubmission.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded 
                                      && prevHwSubmission.OverallScore >= (prevHomework.PassingScoreThreshold ?? 0);
                    if (!prevHwPassed)
                    {
                        isLocked = true;
                        if (prevHwSubmission == null)
                        {
                            lockedReason = $"يجب حل واجب الحصة السابقة '{prevHomework.Title}' أولاً لفتح هذه الحصة.";
                        }
                        else
                        {
                            lockedReason = $"يجب اجتياز واجب الحصة السابقة '{prevHomework.Title}' أولاً لفتح هذه الحصة.";
                        }
                        blockingHomeworkLessonId = previousLesson.Id;
                    }
                }
            }
        }

        // 3. Check if current lesson's own exam is passed
        if (!isLocked && lesson.ExamId.HasValue)
        {
            var exam = await _db.Exams.FindAsync(new object[] { lesson.ExamId.Value }, ct);
            if (exam != null && exam.IsMandatory)
            {
                var passedExam = await _db.StudentExamAttempts
                    .AnyAsync(a => a.UserId == request.UserId && a.ExamId == lesson.ExamId.Value && a.IsPassed, ct);

                if (!passedExam)
                {
                    isLocked = true;
                    var attemptedExam = await _db.StudentExamAttempts
                        .AnyAsync(a => a.UserId == request.UserId && a.ExamId == lesson.ExamId.Value, ct);

                    if (!attemptedExam)
                    {
                        lockedReason = $"يجب حل امتحان الحصة الحالية '{exam.Title}' لفتح هذه الحصة.";
                    }
                    else
                    {
                        lockedReason = $"يجب اجتياز امتحان الحصة الحالية '{exam.Title}' لفتح هذه الحصة.";
                    }
                    blockingExamId = exam.Id;
                }
            }
        }


        var watchEvents = await _db.VideoWatchEvents
            .AsNoTracking()
            .Where(v => v.UserId == request.UserId && lesson.Videos.Select(x => x.Id).Contains(v.LessonVideoId))
            .ToListAsync(ct);

        var videoIds = lesson.Videos.Select(v => v.Id).ToList();
        var allVideoExams = await _db.Exams
            .Where(e => videoIds.Contains(e.LessonVideoId ?? Guid.Empty) || (e.LessonVideoId == null && lesson.Videos.Select(v => v.ExamId).Contains(e.Id)))
            .Select(e => new { e.Id, e.Title, e.LessonVideoId, e.IsMandatory })
            .ToListAsync(ct);

        var allVideoExamIds = allVideoExams.Select(e => e.Id).Distinct().ToList();

        var passedExamIds = await _db.StudentExamAttempts
            .Where(a => a.UserId == request.UserId && allVideoExamIds.Contains(a.ExamId) && a.IsPassed)
            .Select(a => a.ExamId)
            .Distinct()
            .ToListAsync(ct);

        var sortedVideos = lesson.Videos.OrderBy(v => v.Order).ToList();
        var videoDtos = new List<VideoDto>();

        foreach (var v in sortedVideos)
        {
            var watchEvent = watchEvents.FirstOrDefault(we => we.LessonVideoId == v.Id);
            var chapters = v.VideoChapters
                .OrderBy(c => c.Order)
                .Select(c => new VideoChapterDto(c.Id, c.Title, c.StartTime, c.EndTime, c.SummaryText, c.MindmapImageUrl, c.Order))
                .ToList();

            var examsForVideo = allVideoExams
                .Where(e => e.LessonVideoId == v.Id || (e.LessonVideoId == null && v.ExamId == e.Id))
                .Select(e => new VideoExamDto(e.Id, e.Title, passedExamIds.Contains(e.Id), e.IsMandatory))
                .ToList();

            bool examPassed = v.ExamId.HasValue && passedExamIds.Contains(v.ExamId.Value);
            bool isExamLocked = examsForVideo.Any(e => e.IsMandatory && !e.Passed);

            videoDtos.Add(new VideoDto(
                v.Id,
                v.Title,
                v.Provider,
                v.Order,
                watchEvent?.CustomMaxWatchCount ?? v.MaxWatchCount,
                watchEvent?.WatchCount ?? 0,
                (watchEvent?.IsLocked ?? false) && (watchEvent?.WatchCount ?? 0) >= (watchEvent?.CustomMaxWatchCount ?? v.MaxWatchCount),
                Math.Max(0, watchEvent?.TimeWatchedInSeconds ?? 0),
                watchEvent?.UpdatedAt,
                v.SubtitleUrl,
                v.IsProcessingAI,
                v.IsProcessingMindmaps,
                v.ExamId,
                examPassed,
                isExamLocked,
                examsForVideo,
                chapters
            ));
        }

        var hw = await _db.Homeworks
            .Include(h => h.Questions)
            .FirstOrDefaultAsync(h => h.LessonId == request.LessonId, ct);

        LessonHomeworkDto? homeworkDto = null;
        if (hw != null)
        {
            var baseQuery = hw.Questions.AsEnumerable();
            if (hw.IsRandomized)
            {
                baseQuery = baseQuery.OrderBy(x => Guid.NewGuid());
            }
            else
            {
                baseQuery = baseQuery.OrderBy(q => q.Order);
            }

            var hwQuestions = baseQuery.Select(q =>
                new LessonHomeworkQuestionDto(
                    q.Id, 
                    q.BodyText, 
                    q.Order, 
                    q.PointsActive,
                    q.QuestionType.ToString(),
                    q.PossibleAnswers,
                    q.AudioUrl,
                    q.HintText,
                    q.BaseText,
                    q.MistakeStartIndex,
                    q.MistakeEndIndex
                )
            ).ToList();

            homeworkDto = new LessonHomeworkDto(hw.Id, hw.Title, hw.Description ?? "", hw.IsMandatory, hw.PassingScoreThreshold, hw.TotalScore, hwQuestions);
        }
        // Check if the lesson's own exam has been passed
        bool lessonExamPassed = false;
        if (lesson.ExamId.HasValue)
        {
            lessonExamPassed = await _db.StudentExamAttempts
                .AnyAsync(a => a.UserId == request.UserId && a.ExamId == lesson.ExamId.Value && a.IsPassed, ct);
        }

        // Check if the lesson's homework has been passed
        bool homeworkPassed = false;
        Guid? homeworkId = hw?.Id;
        if (hw != null)
        {
            var hwSubmission = await _db.HomeworkSubmissions
                .Where(s => s.StudentId == request.UserId && s.HomeworkId == hw.Id && s.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded)
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefaultAsync(ct);

            homeworkPassed = hwSubmission != null && hwSubmission.OverallScore >= (hw.PassingScoreThreshold ?? 0);
        }

        bool lessonExamLocked = false;
        string? examLockedReason = null;

        if (previousLesson != null)
        {
            var prevHomework = await _db.Homeworks.FirstOrDefaultAsync(h => h.LessonId == previousLesson.Id, ct);
            if (prevHomework != null && prevHomework.IsMandatory)
            {
                var prevHwSubmission = await _db.HomeworkSubmissions
                    .Where(s => s.StudentId == request.UserId && s.HomeworkId == prevHomework.Id)
                    .OrderByDescending(s => s.SubmittedAt)
                    .FirstOrDefaultAsync(ct);

                bool prevHwPassed = prevHwSubmission != null 
                                  && prevHwSubmission.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded 
                                  && prevHwSubmission.OverallScore >= (prevHomework.PassingScoreThreshold ?? 0);
                if (!prevHwPassed)
                {
                    lessonExamLocked = true;
                    if (prevHwSubmission == null)
                    {
                        examLockedReason = $"يجب حل واجب الحصة السابقة '{prevHomework.Title}' أولاً لفتح اختبار هذه الحصة.";
                    }
                    else
                    {
                        examLockedReason = $"يجب اجتياز واجب الحصة السابقة '{prevHomework.Title}' أولاً لفتح اختبار هذه الحصة.";
                    }
                }
            }

            if (!lessonExamLocked && previousLesson.ExamId.HasValue)
            {
                var prevExam = await _db.Exams.FindAsync(new object[] { previousLesson.ExamId.Value }, ct);
                if (prevExam != null && prevExam.IsMandatory)
                {
                    var passedPrevExam = await _db.StudentExamAttempts
                        .AnyAsync(a => a.UserId == request.UserId && a.ExamId == previousLesson.ExamId.Value && a.IsPassed, ct);

                    if (!passedPrevExam)
                    {
                        lessonExamLocked = true;
                        var attemptedPrevExam = await _db.StudentExamAttempts
                            .AnyAsync(a => a.UserId == request.UserId && a.ExamId == previousLesson.ExamId.Value, ct);

                        if (!attemptedPrevExam)
                        {
                            examLockedReason = $"يجب حل امتحان الحصة السابقة '{prevExam.Title}' أولاً لفتح اختبار هذه الحصة.";
                        }
                        else
                        {
                            examLockedReason = $"يجب اجتياز امتحان الحصة السابقة '{prevExam.Title}' أولاً لفتح اختبار هذه الحصة.";
                        }
                    }
                }
            }
        }



        string? examStatus = null;
        if (lesson.ExamId.HasValue)
        {
            if (lessonExamPassed)
            {
                examStatus = "Passed";
            }
            else
            {
                var latestAttempt = await _db.StudentExamAttempts
                    .Where(a => a.UserId == request.UserId && a.ExamId == lesson.ExamId.Value)
                    .OrderByDescending(a => a.StartedAt)
                    .FirstOrDefaultAsync(ct);

                if (latestAttempt == null)
                {
                    examStatus = "NotAttempted";
                }
                else if (latestAttempt.Evaluation != null)
                {
                    examStatus = "Failed";
                }
                else
                {
                    examStatus = "InProgress";
                }
            }
        }

        string? homeworkStatus = null;
        if (hw != null)
        {
            var latestHwSubmission = await _db.HomeworkSubmissions
                .Where(s => s.StudentId == request.UserId && s.HomeworkId == hw.Id)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(ct);

            if (latestHwSubmission == null)
            {
                homeworkStatus = "NotAttempted";
            }
            else if (latestHwSubmission.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded)
            {
                homeworkStatus = latestHwSubmission.OverallScore >= (hw.PassingScoreThreshold ?? 0) ? "Passed" : "Failed";
            }
            else if (latestHwSubmission.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.PendingReview)
            {
                homeworkStatus = "PendingReview";
            }
            else
            {
                homeworkStatus = "InProgress";
            }
        }

        var detail = new LessonDetailDto(
            lesson.Id,
            lesson.Title,
            lesson.Summary,
            lesson.ContentSection.Term.PackageId,
            lesson.ExamId,
            lessonExamPassed,
            homeworkId,
            homeworkPassed,
            homeworkDto,
            videoDtos,
            isLocked,
            lockedReason,
            blockingExamId,
            blockingHomeworkLessonId,
            lesson.Price,
            true,
            lessonExamLocked,
            examLockedReason,
            examStatus,
            homeworkStatus,
            lesson.ContentSection?.TermId,
            lesson.ContentSectionId
        );
        return ApiResponse<LessonDetailDto>.Ok(detail);
    }
}
