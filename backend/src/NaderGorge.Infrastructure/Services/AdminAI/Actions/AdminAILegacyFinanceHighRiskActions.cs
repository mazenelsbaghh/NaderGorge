using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Finance.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIGenerateLegacyPayrollInput(int Month, int Year);
public sealed record AdminAIApproveLegacyPayrollInput(Guid PayrollId);
public sealed record AdminAIAddLegacyPayrollAdjustmentInput(Guid PayrollId, PayrollAdjustmentType Type, decimal Amount, string Reason);
public sealed record AdminAIDeleteLegacyPayrollAdjustmentInput(Guid PayrollId, Guid AdjustmentId);
public sealed record AdminAIResolveLegacyPayoutInput(Guid PayoutId, PayoutStatus Status, string? RejectionReason);

public sealed class AdminAIGenerateLegacyPayrollAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIGenerateLegacyPayrollInput, ApiResponse<int>>(mediator, preview)
{
    public override string Key => "admin.finance.legacy-payroll.generate";
    protected override IRequest<ApiResponse<int>> CreateCommand(AdminAIGenerateLegacyPayrollInput i, Guid actor, string operationId) => new GeneratePayrollCommand(i.Month, i.Year, actor);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<int> r) => LegacyFinanceOutcome.From(r, r.Data, ["legacy-payroll", "finance"]);
}

public sealed class AdminAIApproveLegacyPayrollAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIApproveLegacyPayrollInput, ApiResponse<bool>>(mediator, preview)
{
    public override string Key => "admin.finance.legacy-payroll.approve";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIApproveLegacyPayrollInput i, Guid actor, string operationId) => new ApprovePayrollCommand(i.PayrollId, actor);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => LegacyFinanceOutcome.From(r, r.Success ? 1 : 0, ["legacy-payroll", "finance"]);
}

public sealed class AdminAIAddLegacyPayrollAdjustmentAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIAddLegacyPayrollAdjustmentInput, ApiResponse<PayrollAdjustmentDto>>(mediator, preview)
{
    public override string Key => "admin.finance.legacy-payroll.adjustment.add";
    protected override IRequest<ApiResponse<PayrollAdjustmentDto>> CreateCommand(AdminAIAddLegacyPayrollAdjustmentInput i, Guid actor, string operationId) => new AddPayrollAdjustmentCommand(i.PayrollId, i.Type, i.Amount, i.Reason, actor);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<PayrollAdjustmentDto> r) => LegacyFinanceOutcome.From(r, r.Success ? 1 : 0, ["legacy-payroll", "finance"]);
}

public sealed class AdminAIDeleteLegacyPayrollAdjustmentAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIDeleteLegacyPayrollAdjustmentInput, ApiResponse<bool>>(mediator, preview)
{
    public override string Key => "admin.finance.legacy-payroll.adjustment.delete";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIDeleteLegacyPayrollAdjustmentInput i, Guid actor, string operationId) => new DeletePayrollAdjustmentCommand(i.PayrollId, i.AdjustmentId, actor);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => LegacyFinanceOutcome.From(r, r.Success ? 1 : 0, ["legacy-payroll", "finance"]);
}

public sealed class AdminAIResolveLegacyPayoutAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIResolveLegacyPayoutInput, ApiResponse<bool>>(mediator, preview)
{
    public override string Key => "admin.finance.legacy-payout.resolve";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIResolveLegacyPayoutInput i, Guid actor, string operationId) => new ResolvePayoutCommand(i.PayoutId, i.Status, i.RejectionReason, actor);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => LegacyFinanceOutcome.From(r, r.Success ? 1 : 0, ["teacher-payouts", "finance"]);
}

internal static class LegacyFinanceOutcome
{
    public static AdminAIActionOutcome From<T>(ApiResponse<T> response, int affected, IReadOnlyList<string> scopes) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { response.Message }, affected, scopes)
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, scopes);
}
