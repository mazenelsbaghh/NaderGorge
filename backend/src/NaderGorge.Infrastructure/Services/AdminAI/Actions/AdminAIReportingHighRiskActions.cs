using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.Reporting;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIDeleteReportDefinitionInput(Guid DefinitionId);

public sealed class AdminAIDeleteReportDefinitionAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIDeleteReportDefinitionInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.reporting.definition.delete";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIDeleteReportDefinitionInput input, Guid actorId, string operationId) => new DeleteReportDefinitionCommand(input.DefinitionId, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["reports"]);
}

/// <summary>
/// A permanent, non-registerable refusal boundary. Protected Admin AI evidence
/// has no mutation command and therefore cannot be weakened by a model proposal.
/// </summary>
public sealed class AdminAIProtectedAuditMutationRefusal : IAdminAIActionCapability
{
    public string Key => "admin.reporting.protected-audit.mutation-refused";
    public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken cancellationToken) =>
        Task.FromException<AdminAIActionPreview>(Refusal());
    public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken cancellationToken) =>
        Task.FromException<AdminAIActionOutcome>(Refusal());
    private static NotSupportedException Refusal() => new("Protected Admin AI audit evidence is append-only and has no mutation capability.");
}
