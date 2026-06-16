using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetLessonsQuery(Guid SectionId, Guid UserId) : IRequest<ApiResponse<List<LessonSummaryDto>>>;

public record LessonSummaryDto(
    Guid Id,
    string Title,
    string Summary,
    int Order,
    bool HasAccess,
    bool IsCompleted,
    decimal Price,
    bool IsLocked = false,
    string? LockedReason = null,
    Guid? BlockingExamId = null,
    Guid? BlockingHomeworkLessonId = null
);

public class GetLessonsQueryHandler : IRequestHandler<GetLessonsQuery, ApiResponse<List<LessonSummaryDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetLessonsQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<List<LessonSummaryDto>>> Handle(GetLessonsQuery request, CancellationToken ct)
    {
        var section = await _db.ContentSections
            .Include(cs => cs.Lessons)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct);

        if (section == null)
            return ApiResponse<List<LessonSummaryDto>>.Fail("Section not found");

        var progresses = await _db.LessonProgresses
            .Where(lp => lp.UserId == request.UserId && section.Lessons.Select(l => l.Id).Contains(lp.LessonId))
            .ToListAsync(ct);

        var passedExamIds = await _db.StudentExamAttempts
            .Where(a => a.UserId == request.UserId && a.IsPassed)
            .Select(a => a.ExamId)
            .Distinct()
            .ToListAsync(ct);

        var dtos = new List<LessonSummaryDto>();
        foreach (var lesson in section.Lessons.OrderBy(l => l.Order))
        {
            var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, lesson.Id, ct);
            var isCompleted = progresses.Any(p => p.LessonId == lesson.Id && p.IsCompleted);
            var blockingState = await GetBlockingStateAsync(lesson, section, request.UserId, passedExamIds, ct);

            dtos.Add(new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.Summary,
                lesson.Order,
                hasAccess,
                isCompleted,
                lesson.Price,
                blockingState.IsLocked,
                blockingState.LockedReason,
                blockingState.BlockingExamId,
                blockingState.BlockingHomeworkLessonId
            ));
        }

        return ApiResponse<List<LessonSummaryDto>>.Ok(dtos);
    }

    private async Task<(bool IsLocked, string? LockedReason, Guid? BlockingExamId, Guid? BlockingHomeworkLessonId)> GetBlockingStateAsync(
        Lesson lesson,
        ContentSection section,
        Guid userId,
        List<Guid> passedExamIds,
        CancellationToken ct)
    {
        var previousLesson = await _db.Lessons
            .Where(l => l.ContentSectionId == lesson.ContentSectionId && l.Order < lesson.Order)
            .OrderByDescending(l => l.Order)
            .FirstOrDefaultAsync(ct);

        if (previousLesson != null)
        {
            // 1. Check if previous lesson has a mandatory exam and if it is passed
            if (previousLesson.ExamId.HasValue)
            {
                var exam = await _db.Exams.FindAsync(new object[] { previousLesson.ExamId.Value }, ct);
                if (exam != null && exam.IsMandatory)
                {
                    var passedExam = await _db.StudentExamAttempts
                        .AnyAsync(a => a.UserId == userId && a.ExamId == previousLesson.ExamId.Value && a.IsPassed, ct);

                    if (!passedExam)
                    {
                        return (
                            true,
                            $"يجب اجتياز امتحان '{exam.Title}' التابع للحصة '{previousLesson.Title}' بنجاح.",
                            exam.Id,
                            null
                        );
                    }
                }
            }

            // 1b. Check if any video in the previous lesson has a mandatory exam and if it is passed
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
                    .Where(a => a.UserId == userId && prevVideoExamIds.Contains(a.ExamId) && a.IsPassed)
                    .Select(a => a.ExamId)
                    .ToListAsync(ct);

                var unpassedVideoExam = prevVideoExams.FirstOrDefault(e => !passedPrevVideoExamIds.Contains(e.Id));
                if (unpassedVideoExam != null)
                {
                    return (
                        true,
                        $"يجب اجتياز امتحان الفيديو '{unpassedVideoExam.Title}' التابع للحصة السابقة '{previousLesson.Title}' بنجاح.",
                        unpassedVideoExam.Id,
                        null
                    );
                }
            }

            // 2. Check if previous lesson's mandatory homework is passed
            var prevHomework = await _db.Homeworks.FirstOrDefaultAsync(h => h.LessonId == previousLesson.Id, ct);
            if (prevHomework != null && prevHomework.IsMandatory)
            {
                var prevHwSubmission = await _db.HomeworkSubmissions
                    .Where(s => s.StudentId == userId && s.HomeworkId == prevHomework.Id && s.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded)
                    .OrderByDescending(s => s.SubmittedAt)
                    .FirstOrDefaultAsync(ct);

                bool prevHwPassed = prevHwSubmission != null && prevHwSubmission.OverallScore >= (prevHomework.PassingScoreThreshold ?? 0);
                if (!prevHwPassed)
                {
                    return (
                        true,
                        $"يجب اجتياز واجب الحصة السابقة '{prevHomework.Title}' أولاً لفتح هذه الحصة.",
                        null,
                        previousLesson.Id
                    );
                }
            }

        }

        // 3. Check if current lesson's own exam is passed
        if (lesson.ExamId.HasValue)
        {
            var exam = await _db.Exams.FindAsync(new object[] { lesson.ExamId.Value }, ct);
            if (exam != null && exam.IsMandatory)
            {
                var passedExam = await _db.StudentExamAttempts
                    .AnyAsync(a => a.UserId == userId && a.ExamId == lesson.ExamId.Value && a.IsPassed, ct);

                if (!passedExam)
                {
                    return (
                        true,
                        $"يجب اجتياز امتحان الحصة الحالية '{exam.Title}' لفتح هذه الحصة.",
                        exam.Id,
                        null
                    );
                }
            }
        }


        return (false, null, null, null);
    }
}
