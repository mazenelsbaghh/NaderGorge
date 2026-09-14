using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIDeleteExamQuestionInput(Guid ExamId, Guid QuestionId);
public sealed record AdminAIDeleteExamAttemptInput(Guid ExamId, Guid AttemptId);
public sealed record AdminAIRejectModerationInput(Guid TargetId, string Reason);

public sealed class AdminAIDeleteExamQuestionAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIDeleteExamQuestionInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.assessment.exam-question.delete";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIDeleteExamQuestionInput i, Guid a, string o) => new DeleteExamQuestionCommand(i.ExamId, i.QuestionId, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["exams", "questions"]);
}
public sealed class AdminAIDeleteExamAttemptAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIDeleteExamAttemptInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.assessment.exam-attempt.delete";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIDeleteExamAttemptInput i, Guid a, string o) => new DeleteExamAttemptCommand(i.ExamId, i.AttemptId, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["exams", "attempts"]);
}
public sealed class AdminAIRejectCommunityCommentAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIRejectModerationInput, ApiResponse<ModerateCommunityCommentResponse>>(m, p)
{
    public override string Key => "admin.assessment.community-comment.reject";
    protected override IRequest<ApiResponse<ModerateCommunityCommentResponse>> CreateCommand(AdminAIRejectModerationInput i, Guid a, string o) => new RejectCommunityCommentCommand(i.TargetId, a, i.Reason);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<ModerateCommunityCommentResponse> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { moderated = true }, 1, ["community", "moderation"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["community", "moderation"]);
}
public sealed class AdminAIRejectCommunityPostAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIRejectModerationInput, ApiResponse<ModerateCommunityPostResponse>>(m, p)
{
    public override string Key => "admin.assessment.community-post.reject";
    protected override IRequest<ApiResponse<ModerateCommunityPostResponse>> CreateCommand(AdminAIRejectModerationInput i, Guid a, string o) => new RejectCommunityPostCommand(i.TargetId, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<ModerateCommunityPostResponse> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { moderated = true }, 1, ["community", "moderation"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["community", "moderation"]);
}
public sealed class AdminAIRejectLessonCommentAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIRejectModerationInput, ApiResponse<ModerateLessonCommentResponse>>(m, p)
{
    public override string Key => "admin.assessment.lesson-comment.reject";
    protected override IRequest<ApiResponse<ModerateLessonCommentResponse>> CreateCommand(AdminAIRejectModerationInput i, Guid a, string o) => new RejectLessonCommentCommand(i.TargetId, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<ModerateLessonCommentResponse> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { moderated = true }, 1, ["lesson-comments", "moderation"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["lesson-comments", "moderation"]);
}
