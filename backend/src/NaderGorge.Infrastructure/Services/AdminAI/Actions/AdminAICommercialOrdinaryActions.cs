using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAICreateFormInput(string Title, string Description, string Slug, bool IsActive, string? CoverImageUrl, DateTime? StartsAt, DateTime? ExpiresAt, string FieldsJson);

public sealed class AdminAICreateFormAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateFormInput, ApiResponse<Guid>>(mediator, preview)
{
    public override string Key => "admin.commercial.form.create";
    protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreateFormInput input, Guid actorId, string operationId) =>
        new CreateFormCommand(input.Title, input.Description, input.Slug, input.IsActive, input.CoverImageUrl, input.StartsAt, input.ExpiresAt, input.FieldsJson);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { formId = response.Data }, 1, ["forms"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["forms"]);
}

public sealed record AdminAIUpdateFormInput(Guid FormId, string Title, string Description, string Slug, bool IsActive, string? CoverImageUrl, DateTime? StartsAt, DateTime? ExpiresAt, string FieldsJson);
public sealed class AdminAIUpdateFormAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIUpdateFormInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.commercial.form.update";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIUpdateFormInput input, Guid actorId, string operationId) =>
        new UpdateFormCommand(input.FormId, input.Title, input.Description, input.Slug, input.IsActive, input.CoverImageUrl, input.StartsAt, input.ExpiresAt, input.FieldsJson);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { formId = true }, 1, ["forms"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["forms"]);
}
