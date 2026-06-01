using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Community.Commands;

public record CreateCommunityPostResponse(
    Guid Id,
    string Status,
    DateTime CreatedAt,
    string Message
);

public record CreateCommunityPostCommand(Guid UserId, string Body, List<string>? PollOptions = null)
    : IRequest<ApiResponse<CreateCommunityPostResponse>>;

public class CreateCommunityPostCommandHandler : IRequestHandler<CreateCommunityPostCommand, ApiResponse<CreateCommunityPostResponse>>
{
    private const int MaxPostLength = 4000;

    private readonly IAppDbContext _db;

    public CreateCommunityPostCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<CreateCommunityPostResponse>> Handle(CreateCommunityPostCommand request, CancellationToken ct)
    {
        var trimmedBody = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBody))
            return ApiResponse<CreateCommunityPostResponse>.Fail("Post body is required.", new List<string> { "VALIDATION_EMPTY_BODY" });

        if (trimmedBody.Length > MaxPostLength)
            return ApiResponse<CreateCommunityPostResponse>.Fail($"Post body must be {MaxPostLength} characters or fewer.", new List<string> { "VALIDATION_BODY_TOO_LONG" });

        var isPoll = request.PollOptions != null && request.PollOptions.Count > 0;
        var validPollOptions = isPoll 
            ? request.PollOptions!.Select(o => o.Trim()).Where(o => !string.IsNullOrWhiteSpace(o)).ToList() 
            : new List<string>();

        if (isPoll && validPollOptions.Count < 2)
            return ApiResponse<CreateCommunityPostResponse>.Fail("A poll must have at least two options.", new List<string> { "VALIDATION_POLL_TOO_FEW_OPTIONS" });
            
        if (isPoll && validPollOptions.Count > 10)
            return ApiResponse<CreateCommunityPostResponse>.Fail("A poll cannot have more than 10 options.", new List<string> { "VALIDATION_POLL_TOO_MANY_OPTIONS" });

        var post = new CommunityPost
        {
            AuthorUserId = request.UserId,
            Body = trimmedBody,
            Status = CommunityPostStatus.Pending,
            IsPoll = isPoll
        };

        if (isPoll)
        {
            foreach (var opt in validPollOptions)
            {
                post.PollOptions.Add(new CommunityPostPollOption
                {
                    Text = opt
                });
            }
        }

        _db.CommunityPosts.Add(post);
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CreateCommunityPost",
            EntityType = nameof(CommunityPost),
            EntityId = post.Id,
            PerformedByUserId = request.UserId,
            NewValues = $"Status={CommunityPostStatus.Pending};BodyLength={trimmedBody.Length};IsPoll={isPoll}",
        });

        await _db.SaveChangesAsync(ct);

        return ApiResponse<CreateCommunityPostResponse>.Ok(
            new CreateCommunityPostResponse(
                post.Id,
                post.Status.ToString(),
                post.CreatedAt,
                $"تم إرسال {(isPoll ? "الاستطلاع" : "البوست")} وهو الآن في انتظار المراجعة."
            )
        );
    }
}
