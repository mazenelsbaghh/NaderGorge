using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Media.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAICreateMediaPipelineInput(string Title, string? Description, Guid? AssignedAgentId, string? AssetFolderUrl);

public sealed class AdminAICreateMediaPipelineAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateMediaPipelineInput, ApiResponse<Guid>>(mediator, preview)
{
    public override string Key => "admin.tools.media-pipeline.create";
    protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreateMediaPipelineInput input, Guid actorId, string operationId) =>
        new CreateMediaPipelineCommand(input.Title, input.Description, input.AssignedAgentId, input.AssetFolderUrl, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { pipelineId = response.Data }, 1, ["media-pipelines"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["media-pipelines"]);
}

public sealed record AdminAICreateSocialPlanInput(string Title, string? Description, string? Script, SocialPlatform Platform, SocialPlanStatus Status, DateTime ScheduledDate, Guid? MediaProductionPipelineId);
public sealed class AdminAICreateSocialPlanAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateSocialPlanInput, ApiResponse<Guid>>(mediator, preview)
{
    public override string Key => "admin.tools.social-plan.create";
    protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreateSocialPlanInput input, Guid actorId, string operationId) =>
        new CreateSocialPlanCommand(input.Title, input.Description, input.Script, input.Platform, input.Status, input.ScheduledDate, input.MediaProductionPipelineId, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { socialPlanId = response.Data }, 1, ["social-plans"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["social-plans"]);
}
