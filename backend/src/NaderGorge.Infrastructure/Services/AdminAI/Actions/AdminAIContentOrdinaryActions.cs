using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Application.Features.Admin.VideoTypes.Commands;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAICreateVideoTypeInput(string Name, int SortOrder, bool IsActive);

public sealed class AdminAICreateVideoTypeAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateVideoTypeInput, ApiResponse<VideoTypeDto>>(mediator, preview)
{
    public override string Key => "admin.content.video-type.create";
    protected override IRequest<ApiResponse<VideoTypeDto>> CreateCommand(AdminAICreateVideoTypeInput input, Guid actorId, string operationId) =>
        new CreateVideoTypeCommand(input.Name, input.SortOrder, input.IsActive, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<VideoTypeDto> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(response.Data!, 1, ["video-types", "content"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["video-types", "content"]);
}

public sealed record AdminAICreateSubjectInput(string Name, string Description);
public sealed class AdminAICreateSubjectAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateSubjectInput, ApiResponse<Guid>>(mediator, preview)
{
    public override string Key => "admin.content.subject.create";
    protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreateSubjectInput input, Guid actorId, string operationId) =>
        new CreateSubjectCommand(input.Name, input.Description);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { subjectId = response.Data }, 1, ["subjects", "content"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["subjects", "content"]);
}

public sealed record AdminAIUpdateSubjectInput(Guid SubjectId, string Name, string Description);
public sealed class AdminAIUpdateSubjectAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIUpdateSubjectInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.content.subject.update";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIUpdateSubjectInput input, Guid actorId, string operationId) =>
        new UpdateSubjectCommand(input.SubjectId, input.Name, input.Description);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { subjectId = true }, 1, ["subjects", "content"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["subjects", "content"]);
}

public sealed record AdminAIUpdateVideoTypeInput(Guid VideoTypeId, string Name, int SortOrder);
public sealed class AdminAIUpdateVideoTypeAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIUpdateVideoTypeInput, ApiResponse<VideoTypeDto>>(mediator, preview)
{
    public override string Key => "admin.content.video-type.update";
    protected override IRequest<ApiResponse<VideoTypeDto>> CreateCommand(AdminAIUpdateVideoTypeInput input, Guid actorId, string operationId) =>
        new UpdateVideoTypeCommand(input.VideoTypeId, input.Name, input.SortOrder, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<VideoTypeDto> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(response.Data!, 1, ["video-types", "content"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["video-types", "content"]);
}
