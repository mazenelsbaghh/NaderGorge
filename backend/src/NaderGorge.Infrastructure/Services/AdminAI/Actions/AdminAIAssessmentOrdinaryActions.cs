using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIApproveLessonCommentInput(Guid CommentId);

public sealed class AdminAIApproveLessonCommentAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIApproveLessonCommentInput, ApiResponse<ModerateLessonCommentResponse>>(mediator, preview)
{
    public override string Key => "admin.assessment.lesson-comment.approve";
    protected override IRequest<ApiResponse<ModerateLessonCommentResponse>> CreateCommand(AdminAIApproveLessonCommentInput input, Guid actorId, string operationId) =>
        new ApproveLessonCommentCommand(input.CommentId, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<ModerateLessonCommentResponse> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(response.Data!, 1, ["lesson-comments", "moderation"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["lesson-comments", "moderation"]);
}

public sealed record AdminAIApproveCommunityPostInput(Guid PostId);
public sealed class AdminAIApproveCommunityPostAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIApproveCommunityPostInput, ApiResponse<ModerateCommunityPostResponse>>(mediator, preview)
{
    public override string Key => "admin.assessment.community-post.approve";
    protected override IRequest<ApiResponse<ModerateCommunityPostResponse>> CreateCommand(AdminAIApproveCommunityPostInput input, Guid actorId, string operationId) =>
        new ApproveCommunityPostCommand(input.PostId, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<ModerateCommunityPostResponse> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(response.Data!, 1, ["community-posts", "moderation"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["community-posts", "moderation"]);
}
