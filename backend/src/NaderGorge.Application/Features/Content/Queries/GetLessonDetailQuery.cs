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
    LessonHomeworkDto? Homework,
    List<VideoDto> Videos,
    bool IsLocked = false,
    string? LockedReason = null,
    Guid? BlockingExamId = null,
    Guid? BlockingHomeworkLessonId = null
);

public record LessonHomeworkDto(Guid Id, string Title, string Instructions, bool IsMandatory, decimal? RequiredPointsToPass, decimal TotalScore, List<LessonHomeworkQuestionDto> Questions);
public record LessonHomeworkQuestionDto(Guid Id, string Text, int Order, int MaxPoints);

public record VideoChapterDto(Guid Id, string Title, int StartTime, int EndTime, string SummaryText, string? MindmapImageUrl, int Order);
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
        var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, request.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<LessonDetailDto>.Fail("You do not have access to this lesson.");

        var isAuthorizedTeacher = await _auth.CanAccessLessonAsync(request.UserId, request.LessonId, ct);
        if (!isAuthorizedTeacher)
            return ApiResponse<LessonDetailDto>.Fail("Unauthorized access to this lesson.");

        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Videos)
                .ThenInclude(v => v.VideoChapters)
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);

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

        if (previousLesson == null)
        {
            var previousSection = await _db.ContentSections
                .Where(s => s.TermId == lesson.ContentSection.TermId && s.Order < lesson.ContentSection.Order)
                .OrderByDescending(s => s.Order)
                .FirstOrDefaultAsync(ct);

            if (previousSection != null)
            {
                previousLesson = await _db.Lessons
                    .Where(l => l.ContentSectionId == previousSection.Id)
                    .OrderByDescending(l => l.Order)
                    .FirstOrDefaultAsync(ct);
            }
        }

        if (previousLesson != null)
        {
            // Check if previous lesson has homework and if it is passed
            var prevHomework = await _db.Homeworks.FirstOrDefaultAsync(h => h.LessonId == previousLesson.Id, ct);
            if (prevHomework != null && prevHomework.IsMandatory)
            {
                var submission = await _db.HomeworkSubmissions
                    .Where(s => s.StudentId == request.UserId && s.HomeworkId == prevHomework.Id)
                    .OrderByDescending(s => s.StartedAt)
                    .FirstOrDefaultAsync(ct);

                if (submission == null)
                {
                    isLocked = true;
                    lockedReason = $"يجب إتمام واجب '{prevHomework.Title}' التابع للحصة '{previousLesson.Title}' أولاً.";
                    blockingHomeworkLessonId = previousLesson.Id;
                }
                else if (submission.Status != NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded &&
                         submission.OverallScore < (prevHomework.PassingScoreThreshold ?? 0))
                {
                    isLocked = true;
                    lockedReason = $"يجب اجتياز واجب '{prevHomework.Title}' التابع للحصة '{previousLesson.Title}' بنجاح.";
                    blockingHomeworkLessonId = previousLesson.Id;
                }
                else if (submission.OverallScore < (prevHomework.PassingScoreThreshold ?? 0))
                {
                    isLocked = true;
                    lockedReason = $"يجب تحقيق درجة النجاح في واجب '{prevHomework.Title}' التابع للحصة '{previousLesson.Title}'.";
                    blockingHomeworkLessonId = previousLesson.Id;
                }
            }

            // Check if previous lesson has an exam and if it is mandatory and passed
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
                        lockedReason = $"يجب اجتياز امتحان '{exam.Title}' التابع للحصة '{previousLesson.Title}' بنجاح.";
                        blockingExamId = exam.Id;
                    }
                }
            }
        }

        // Check the lesson's OWN exam: Exam → Video → Homework flow
        // The student must pass the lesson's exam before accessing its content
        if (!isLocked && lesson.ExamId.HasValue)
        {
            var currentExam = await _db.Exams.FindAsync(new object[] { lesson.ExamId.Value }, ct);
            if (currentExam != null)
            {
                var passedCurrentExam = await _db.StudentExamAttempts
                    .AnyAsync(a => a.UserId == request.UserId && a.ExamId == lesson.ExamId.Value && a.IsPassed, ct);

                if (!passedCurrentExam)
                {
                    isLocked = true;
                    lockedReason = $"يجب اجتياز امتحان '{currentExam.Title}' أولاً للدخول لهذه الحصة.";
                    blockingExamId = currentExam.Id;
                }
            }
        }

        var watchEvents = await _db.VideoWatchEvents
            .AsNoTracking()
            .Where(v => v.UserId == request.UserId && lesson.Videos.Select(x => x.Id).Contains(v.LessonVideoId))
            .ToListAsync(ct);

        var videoExamIds = lesson.Videos
            .Where(v => v.ExamId.HasValue)
            .Select(v => v.ExamId!.Value)
            .Distinct()
            .ToList();

        var passedExamIds = await _db.StudentExamAttempts
            .Where(a => a.UserId == request.UserId && videoExamIds.Contains(a.ExamId) && a.IsPassed)
            .Select(a => a.ExamId)
            .Distinct()
            .ToListAsync(ct);

        var sortedVideos = lesson.Videos.OrderBy(v => v.Order).ToList();
        var videoDtos = new List<VideoDto>();
        bool anyPrecedingExamNotPassed = false;

        foreach (var v in sortedVideos)
        {
            var watchEvent = watchEvents.FirstOrDefault(we => we.LessonVideoId == v.Id);
            var chapters = v.VideoChapters
                .OrderBy(c => c.Order)
                .Select(c => new VideoChapterDto(c.Id, c.Title, c.StartTime, c.EndTime, c.SummaryText, c.MindmapImageUrl, c.Order))
                .ToList();

            bool examPassed = v.ExamId.HasValue && passedExamIds.Contains(v.ExamId.Value);
            bool isExamLocked = anyPrecedingExamNotPassed;

            videoDtos.Add(new VideoDto(
                v.Id,
                v.Title,
                v.Provider,
                v.Order,
                watchEvent?.CustomMaxWatchCount ?? v.MaxWatchCount,
                watchEvent?.WatchCount ?? 0,
                watchEvent?.IsLocked ?? false,
                Math.Max(0, watchEvent?.TimeWatchedInSeconds ?? 0),
                watchEvent?.UpdatedAt,
                v.SubtitleUrl,
                v.IsProcessingAI,
                v.IsProcessingMindmaps,
                v.ExamId,
                examPassed,
                isExamLocked,
                chapters
            ));

            if (v.ExamId.HasValue && !examPassed)
            {
                anyPrecedingExamNotPassed = true;
            }
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
                new LessonHomeworkQuestionDto(q.Id, q.BodyText, q.Order, q.PointsActive)
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

        var detail = new LessonDetailDto(
            lesson.Id,
            lesson.Title,
            lesson.Summary,
            lesson.ContentSection.Term.PackageId,
            lesson.ExamId,
            lessonExamPassed,
            homeworkDto,
            videoDtos,
            isLocked,
            lockedReason,
            blockingExamId,
            blockingHomeworkLessonId
        );
        return ApiResponse<LessonDetailDto>.Ok(detail);
    }
}
