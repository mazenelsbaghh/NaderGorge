using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Webhooks.Commands;

public record WebhookEssayGradedResultDto(Guid EssaySubmissionId, string Status);

public record WebhookEssayGradedCommand(Guid EssaySubmissionId, decimal AiScore, string? AiFeedback)
    : IRequest<ApiResponse<WebhookEssayGradedResultDto>>;

public class WebhookEssayGradedCommandHandler
    : IRequestHandler<WebhookEssayGradedCommand, ApiResponse<WebhookEssayGradedResultDto>>
{
    private readonly IAppDbContext _db;

    public WebhookEssayGradedCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<WebhookEssayGradedResultDto>> Handle(WebhookEssayGradedCommand request, CancellationToken ct)
    {
        var submission = await _db.EssaySubmissions.FindAsync(new object[] { request.EssaySubmissionId }, ct);
        if (submission == null)
        {
            return ApiResponse<WebhookEssayGradedResultDto>.Fail("Essay submission not found.");
        }

        if (submission.Status != EssaySubmissionStatus.WaitAI)
        {
            return ApiResponse<WebhookEssayGradedResultDto>.Ok(
                new WebhookEssayGradedResultDto(submission.Id, submission.Status.ToString()),
                "Essay submission has already processed or left WaitAI state.");
        }

        submission.AiInitialScore = request.AiScore;
        submission.AiFeedback = request.AiFeedback;

        if (submission.Status == EssaySubmissionStatus.WaitAI)
        {
            submission.Status = EssaySubmissionStatus.AIScored;
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<WebhookEssayGradedResultDto>.Ok(
            new WebhookEssayGradedResultDto(submission.Id, submission.Status.ToString()));
    }
}
