using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Queries;

public record StartExamQuery(Guid ExamId, Guid UserId) : IRequest<ApiResponse<ExamDto>>;

public record ExamDto(Guid Id, string Title, string Description, decimal TotalScore, List<ExamQuestionDto> Questions);
public record ExamQuestionDto(Guid Id, string Text, decimal Points, List<QuestionOptionDto> Options);
public record QuestionOptionDto(Guid Id, string Text);

public class StartExamQueryHandler : IRequestHandler<StartExamQuery, ApiResponse<ExamDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public StartExamQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<ExamDto>> Handle(StartExamQuery request, CancellationToken ct)
    {
        // Find the lesson this exam belongs to, to verify access
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.ExamId == request.ExamId, ct);
        if (lesson != null)
        {
            var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, lesson.Id, ct);
            if (!hasAccess)
            {
                return ApiResponse<ExamDto>.Fail("You do not have access to this exam's lesson.");
            }
        }

        var exam = await _db.Exams
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);

        if (exam == null)
            return ApiResponse<ExamDto>.Fail("Exam not found");

        var random = new Random();
        
        var questionDtos = exam.ExamQuestions.OrderBy(q => q.Order).Select(eq => 
        {
            var options = eq.Question.Options
                .OrderBy(x => random.Next()) // Shuffle options
                .Select(o => new QuestionOptionDto(o.Id, o.Text))
                .ToList();
                
            return new ExamQuestionDto(eq.Id, eq.Question.Text, eq.Points, options);
        }).ToList();

        var dto = new ExamDto(exam.Id, exam.Title, exam.Description, exam.TotalScore, questionDtos);
        
        return ApiResponse<ExamDto>.Ok(dto);
    }
}
