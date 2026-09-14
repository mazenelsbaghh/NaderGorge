using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.HR.People;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Lifecycle;
using NaderGorge.Application.Features.HR.Performance;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIDeleteEmployeeInput(Guid EmployeeId);
public sealed record AdminAICompleteEmployeeExitInput(Guid EmployeeId, DateOnly TerminationDate);
public sealed record AdminAITransitionEmploymentContractInput(Guid ContractId, EmploymentContractStatus NextStatus);
public sealed record AdminAICreateEmploymentContractInput(Guid EmployeeId, string ContractNumber, EmploymentContractType Type, DateOnly StartDate, DateOnly? EndDate, DateOnly? ProbationEndDate, decimal BaseSalary, string Currency, string? TermsJson);
public sealed record AdminAICreateCandidateOfferInput(Guid CandidateId, decimal BaseSalary, string Currency, DateOnly ProposedStartDate);
public sealed record AdminAIAcceptCandidateOfferInput(Guid OfferId, int ExpectedVersion);
public sealed record AdminAIOpenEmployeeCaseInput(Guid EmployeeId, string Title, string Description, bool Confidential);
public sealed record AdminAIAddEmployeeCaseEvidenceInput(Guid CaseId, string AssetReference, string ContentHash);
public sealed record AdminAIDecideEmployeeCaseInput(Guid CaseId, DisciplinaryActionType Type, decimal? FinancialAmount, string Reason, int ExpectedVersion);
public sealed record AdminAIAddEmployeeDocumentInput(Guid EmployeeId, Guid? DocumentId, EmployeeDocumentCategory Category, string Name, string AssetReference, string ContentHash, string MimeType, long SizeBytes, DateOnly? ExpiresOn);
public sealed record AdminAIAssignHrAssetInput(Guid AssetId, Guid EmployeeId, string Condition);
public sealed record AdminAIReturnHrAssetInput(Guid CustodyId, string Condition);
public sealed record AdminAIWaiveHrAssetInput(Guid CustodyId, string Reason);

public sealed class AdminAIDeleteEmployeeAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIDeleteEmployeeInput, ApiResponse<Guid>>(mediator, preview)
{
    public override string Key => "admin.hr.employee.delete";
    protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAIDeleteEmployeeInput input, Guid actorId, string operationId) => new DeleteEmployeeProfileCommand(input.EmployeeId, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> response) => HrLifecycleOutcome.From(response, ["hr-employees", "hr-organization"]);
}

public sealed class AdminAICompleteEmployeeExitAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICompleteEmployeeExitInput, ApiResponse<bool>>(mediator, preview)
{
    public override string Key => "admin.hr.employee.offboard.complete";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAICompleteEmployeeExitInput input, Guid actorId, string operationId) => new CompleteEmployeeExitCommand(input.EmployeeId, input.TerminationDate, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> response) => HrLifecycleOutcome.From(response, ["hr-employees", "authorization", "sessions"]);
}

public sealed class AdminAITransitionEmploymentContractAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAITransitionEmploymentContractInput, ApiResponse<bool>>(mediator, preview)
{
    public override string Key => "admin.hr.contract.status.transition";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAITransitionEmploymentContractInput input, Guid actorId, string operationId) => new TransitionEmploymentContractCommand(input.ContractId, input.NextStatus, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> response) => HrLifecycleOutcome.From(response, ["hr-contracts", "hr-employees"]);
}

public sealed class AdminAICreateEmploymentContractAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateEmploymentContractInput, ApiResponse<Guid>>(mediator, preview)
{
    public override string Key => "admin.hr.contract.create";
    protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreateEmploymentContractInput input, Guid actorId, string operationId) =>
        new CreateEmploymentContractCommand(input.EmployeeId, input.ContractNumber, input.Type, input.StartDate, input.EndDate, input.ProbationEndDate, input.BaseSalary, input.Currency, input.TermsJson, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> response) => HrLifecycleOutcome.From(response, ["hr-contracts", "hr-employees", "hr-finance"]);
}

public sealed class AdminAICreateCandidateOfferAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateCandidateOfferInput, ApiResponse<Guid>>(mediator, preview)
{ public override string Key => "admin.hr.candidate-offer.create"; protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreateCandidateOfferInput i, Guid actor, string op) => new CreateCandidateOfferCommand(i.CandidateId, i.BaseSalary, i.Currency, i.ProposedStartDate); protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> r) => HrLifecycleOutcome.From(r, ["hr-recruitment", "hr-offers"]); }

public sealed class AdminAIAcceptCandidateOfferAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIAcceptCandidateOfferInput, ApiResponse<Guid>>(mediator, preview)
{ public override string Key => "admin.hr.candidate-offer.accept"; protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAIAcceptCandidateOfferInput i, Guid actor, string op) => new AcceptCandidateOfferCommand(i.OfferId, i.ExpectedVersion); protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> r) => HrLifecycleOutcome.From(r, ["hr-recruitment", "hr-offers"]); }

public sealed class AdminAIOpenEmployeeCaseAction(PerformanceCaseService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIOpenEmployeeCaseInput>(preview)
{ public override string Key => "admin.hr.case.open"; protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIOpenEmployeeCaseInput i, string op, CancellationToken ct) => HrLifecycleOutcome.From(await service.OpenCaseAsync(i.EmployeeId, actor, i.Title, i.Description, i.Confidential, ct), ["hr-cases"]); }
public sealed class AdminAIAddEmployeeCaseEvidenceAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAIAddEmployeeCaseEvidenceInput, ApiResponse<Guid>>(mediator, preview)
{ public override string Key => "admin.hr.case.evidence.add"; protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAIAddEmployeeCaseEvidenceInput i, Guid actor, string op) => new AddCaseEvidenceCommand(i.CaseId, i.AssetReference, i.ContentHash, actor); protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> r) => HrLifecycleOutcome.From(r, ["hr-cases", "hr-documents"]); }
public sealed class AdminAIDecideEmployeeCaseAction(PerformanceCaseService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIDecideEmployeeCaseInput>(preview)
{ public override string Key => "admin.hr.case.discipline.decide"; protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIDecideEmployeeCaseInput i, string op, CancellationToken ct) => HrLifecycleOutcome.From(await service.DecideCaseAsync(i.CaseId, i.Type, i.FinancialAmount, i.Reason, actor, i.ExpectedVersion, ct), ["hr-cases", "hr-payroll"]); }
public sealed class AdminAIAddEmployeeDocumentAction(DocumentAssetService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIAddEmployeeDocumentInput>(preview)
{ public override string Key => "admin.hr.document.version.add"; protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIAddEmployeeDocumentInput i, string op, CancellationToken ct) => HrLifecycleOutcome.From(await service.AddDocumentVersionAsync(i.EmployeeId, i.DocumentId, i.Category, i.Name, i.AssetReference, i.ContentHash, i.MimeType, i.SizeBytes, actor, i.ExpiresOn, ct), ["hr-documents"]); }
public sealed class AdminAIAssignHrAssetAction(DocumentAssetService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIAssignHrAssetInput>(preview)
{ public override string Key => "admin.hr.asset.assign"; protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIAssignHrAssetInput i, string op, CancellationToken ct) => HrLifecycleOutcome.From(await service.AssignAssetAsync(i.AssetId, i.EmployeeId, actor, i.Condition, ct), ["hr-assets", "hr-employees"]); }
public sealed class AdminAIReturnHrAssetAction(DocumentAssetService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIReturnHrAssetInput>(preview)
{ public override string Key => "admin.hr.asset.return"; protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIReturnHrAssetInput i, string op, CancellationToken ct) => HrLifecycleOutcome.From(await service.ReturnAssetAsync(i.CustodyId, actor, i.Condition, ct), ["hr-assets", "hr-employees"]); }
public sealed class AdminAIWaiveHrAssetAction(DocumentAssetService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIWaiveHrAssetInput>(preview)
{ public override string Key => "admin.hr.asset.waive"; protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIWaiveHrAssetInput i, string op, CancellationToken ct) => HrLifecycleOutcome.From(await service.WaiveCustodyAsync(i.CustodyId, actor, i.Reason, ct), ["hr-assets", "hr-employees"]); }

internal static class HrLifecycleOutcome
{
    public static AdminAIActionOutcome From<T>(ApiResponse<T> response, IReadOnlyList<string> scopes) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { response.Message }, 1, scopes)
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, scopes);
}
