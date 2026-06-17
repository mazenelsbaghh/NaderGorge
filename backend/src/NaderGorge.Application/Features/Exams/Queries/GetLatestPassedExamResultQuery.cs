using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Queries;

public record GetLatestPassedExamResultQuery(Guid ExamId, Guid UserId) : IRequest<ApiResponse<ExamResultDto>>;

public class GetLatestPassedExamResultQueryHandler : IRequestHandler<GetLatestPassedExamResultQuery, ApiResponse<ExamResultDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetLatestPassedExamResultQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<ExamResultDto>> Handle(GetLatestPassedExamResultQuery request, CancellationToken ct)
    {
        var hasAccess = await _access.HasAccessToExamAsync(request.UserId, request.ExamId, ct);
        if (!hasAccess)
        {
            return ApiResponse<ExamResultDto>.Fail("You do not have access to this exam.");
        }

        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(l => l.ExamId == request.ExamId, ct);

        if (lesson == null)
        {
            var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.ExamId == request.ExamId, ct);
            if (video == null)
            {
                var examEntity = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);
                if (examEntity?.LessonVideoId != null)
                {
                    video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == examEntity.LessonVideoId.Value, ct);
                }
            }

            if (video != null)
            {
                lesson = await _db.Lessons
                    .AsNoTracking()
                    .Include(l => l.ContentSection)
                    .ThenInclude(cs => cs.Term)
                    .FirstOrDefaultAsync(l => l.Id == video.LessonId, ct);
            }
        }

        var exam = await _db.Exams
            .AsNoTracking()
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);

        if (exam == null)
            return ApiResponse<ExamResultDto>.Fail("Exam not found");

        var attempt = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId && a.ExamId == request.ExamId && a.IsPassed)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (attempt == null)
            return ApiResponse<ExamResultDto>.Fail("No passed attempt found");

        var answers = await _db.StudentAnswers
            .AsNoTracking()
            .Include(a => a.SelectedOption)
            .Where(a => a.StudentExamAttemptId == attempt.Id)
            .ToListAsync(ct);

        var essays = await _db.EssaySubmissions
            .AsNoTracking()
            .Where(e => e.StudentExamAttemptId == attempt.Id)
            .ToListAsync(ct);

        var snapshots = ExamResultBuilder.BuildQuestionReviewSnapshots(answers);
        var questionIdToExamQuestionId = exam.ExamQuestions.ToDictionary(eq => eq.Question.Id, eq => eq.Id);
        foreach (var essay in essays)
        {
            if (!questionIdToExamQuestionId.TryGetValue(essay.QuestionId, out var examQuestionId))
            {
                continue;
            }

            snapshots[examQuestionId] = new QuestionReviewSnapshot(
                essay.AnswerText,
                !string.IsNullOrWhiteSpace(essay.AnswerText) || !string.IsNullOrWhiteSpace(essay.AudioUrl),
                essay.Status == NaderGorge.Domain.Entities.EssaySubmissionStatus.TeacherGraded,
                essay.Status == NaderGorge.Domain.Entities.EssaySubmissionStatus.TeacherGraded ? essay.TeacherFinalScore ?? 0 : 0,
                essay.AudioUrl
            );
        }

        var result = ExamResultBuilder.Build(
            exam,
            attempt,
            blocksNextLesson: false,
            lesson?.Id,
            lesson?.ContentSection?.Term?.PackageId,
            questionSnapshotsByQuestionId: snapshots,
            revealCorrectAnswers: true,
            resultState: essays.Any() ? "Completed" : "Completed"
        );

        return ApiResponse<ExamResultDto>.Ok(result);
    }
}
