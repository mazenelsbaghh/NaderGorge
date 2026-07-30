using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Sales;

public sealed record PublicExamAttemptDto(
    Guid AttemptId,
    Guid StudentId,
    string StudentName,
    string StudentPhone,
    DateTime? StartedAt,
    DateTime SubmittedAt,
    decimal ScoreAchieved,
    bool IsPassed,
    bool IsTimeExpired,
    string Evaluation);

public sealed record PublicExamQuestionReportDto(
    Guid ExamQuestionId,
    string Text,
    decimal Points,
    int TotalAnswers,
    int CorrectAnswers,
    decimal CorrectPercentage);

public sealed record PublicExamResultsDto(
    Guid ProductId,
    Guid ExamId,
    string ExamTitle,
    string Slug,
    decimal Price,
    bool IsPaid,
    int AttemptCount,
    int PassedCount,
    decimal AverageScore,
    IReadOnlyList<PublicExamAttemptDto> Attempts,
    IReadOnlyList<PublicExamQuestionReportDto> Questions);

public sealed record GetPublicExamResultsQuery(Guid ProductId) : IRequest<ApiResponse<PublicExamResultsDto>>;

public sealed class GetPublicExamResultsQueryHandler : IRequestHandler<GetPublicExamResultsQuery, ApiResponse<PublicExamResultsDto>>
{
    private readonly IAppDbContext _db;

    public GetPublicExamResultsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<PublicExamResultsDto>> Handle(GetPublicExamResultsQuery request, CancellationToken ct)
    {
        var product = await _db.PublicExamProducts
            .Include(x => x.Exam)
            .FirstOrDefaultAsync(x => x.Id == request.ProductId, ct);

        if (product == null)
            return ApiResponse<PublicExamResultsDto>.Fail("الامتحان العام غير موجود.", new List<string> { "NOT_FOUND" });

        var attempts = await _db.StudentExamAttempts
            .Include(x => x.User)
            .Where(x => x.ExamId == product.ExamId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PublicExamAttemptDto(
                x.Id,
                x.UserId,
                x.User.FullName,
                x.User.PhoneNumber,
                x.StartedAt,
                x.UpdatedAt ?? x.CreatedAt,
                x.ScoreAchieved,
                x.IsPassed,
                x.IsTimeExpired,
                x.Evaluation ?? "لم يقيّم"))
            .ToListAsync(ct);

        var answers = await _db.StudentAnswers
            .Where(x => x.ExamQuestion.ExamId == product.ExamId)
            .Select(x => new { x.ExamQuestionId, x.IsCorrect })
            .ToListAsync(ct);

        var questions = await _db.ExamQuestions
            .Include(x => x.Question)
            .Where(x => x.ExamId == product.ExamId)
            .OrderBy(x => x.Order)
            .Select(x => new { x.Id, x.Question.Text, x.Points })
            .ToListAsync(ct);

        var questionReports = questions.Select(question =>
        {
            var questionAnswers = answers.Where(answer => answer.ExamQuestionId == question.Id).ToList();
            var correct = questionAnswers.Count(answer => answer.IsCorrect);
            var total = questionAnswers.Count;
            return new PublicExamQuestionReportDto(
                question.Id,
                question.Text,
                question.Points,
                total,
                correct,
                total == 0 ? 0 : Math.Round((decimal)correct / total * 100m, 2));
        }).ToList();

        var submittedAttempts = attempts.Where(x => x.Evaluation != "لم يقيّم").ToList();
        var dto = new PublicExamResultsDto(
            product.Id,
            product.ExamId,
            product.Exam.Title,
            product.Slug,
            product.Price,
            product.IsPaid,
            attempts.Count,
            attempts.Count(x => x.IsPassed),
            submittedAttempts.Count == 0 ? 0 : Math.Round(submittedAttempts.Average(x => x.ScoreAchieved), 2),
            attempts,
            questionReports);

        return ApiResponse<PublicExamResultsDto>.Ok(dto);
    }
}
