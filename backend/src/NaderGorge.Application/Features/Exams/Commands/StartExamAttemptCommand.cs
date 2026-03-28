using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Commands;

public record StartExamAttemptCommand(Guid ExamId, Guid UserId) : IRequest<ApiResponse<ActiveExamAttemptDto>>;

public record ActiveExamAttemptDto(
    Guid AttemptId,
    string Title,
    string Description,
    DateTime StartedAt,
    int? DurationMinutes,
    decimal TotalScore,
    List<ExamQuestionViewDto> Questions
);

public record ExamQuestionViewDto(Guid Id, string Text, string Type, decimal Points, int? DurationSeconds, List<QuestionOptionViewDto> Options);
public record QuestionOptionViewDto(Guid Id, string Text);

public class StartExamAttemptCommandHandler : IRequestHandler<StartExamAttemptCommand, ApiResponse<ActiveExamAttemptDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public StartExamAttemptCommandHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<ActiveExamAttemptDto>> Handle(StartExamAttemptCommand request, CancellationToken ct)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.ExamId == request.ExamId, ct);
        if (lesson != null)
        {
            var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, lesson.Id, ct);
            if (!hasAccess)
            {
                return ApiResponse<ActiveExamAttemptDto>.Fail("You do not have access to this exam's lesson.");
            }
        }

        var exam = await _db.Exams
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);

        if (exam == null)
            return ApiResponse<ActiveExamAttemptDto>.Fail("Exam not found");

        // Create the attempt to lock in the StartedAt time
        var attempt = new StudentExamAttempt
        {
            UserId = request.UserId,
            ExamId = request.ExamId,
            StartedAt = DateTime.UtcNow,
            ScoreAchieved = 0,
            IsTimeExpired = false,
            Answers = new List<StudentAnswer>()
        };

        _db.StudentExamAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        var random = new Random();
        
        var questionDtos = exam.ExamQuestions.OrderBy(q => q.Order).Select(eq => 
        {
            var options = eq.Question.Options
                .OrderBy(x => random.Next()) // Shuffle options
                .Select(o => new QuestionOptionViewDto(o.Id, o.Text))
                .ToList();
                
            return new ExamQuestionViewDto(eq.Id, eq.Question.Text, eq.Question.Type.ToString(), eq.Points, eq.DurationSeconds, options);
        }).ToList();

        var dto = new ActiveExamAttemptDto(
            attempt.Id,
            exam.Title,
            exam.Description,
            attempt.StartedAt.Value,
            exam.DurationMinutes,
            exam.TotalScore,
            questionDtos
        );
        
        return ApiResponse<ActiveExamAttemptDto>.Ok(dto);
    }
}
