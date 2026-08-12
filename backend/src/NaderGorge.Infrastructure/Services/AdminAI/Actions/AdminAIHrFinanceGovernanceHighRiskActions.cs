using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Migration;
using NaderGorge.Application.Features.HR.Retention;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAICreateEmployeeCompensationInput(Guid EmployeeId, decimal BaseSalary, string Currency, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Reason);
public sealed record AdminAICreatePayComponentInput(string Code, string Name, PayComponentClass Classification, bool IsTaxable, bool IsInsurable);
public sealed record AdminAICreatePayrollRuleInput(Guid PayComponentId, string Name, string Expression, decimal Rate, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int Priority);
public sealed record AdminAIPrepareHrPayrollInput(DateOnly PeriodStart, DateOnly PeriodEnd, DateTime CutoffAt);
public sealed record AdminAIGrantLeaveBalanceInput(Guid EmployeeId, Guid LeaveTypeId, int Year, decimal Amount, string Reason);
public sealed record AdminAIDecideHrApprovalInput(Guid InstanceId, bool Approve, string Reason, int ExpectedVersion);
public sealed record AdminAICreateHrContractInput(Guid EmployeeId, string ContractNumber, EmploymentContractType Type, DateOnly StartDate, DateOnly? EndDate, DateOnly? ProbationEndDate, decimal BaseSalary, string Currency, string? TermsJson);
public sealed record AdminAIHrRetentionInput(DateOnly Today, int CandidateRetentionYears, string Reason);
public sealed record AdminAIHrMigrationInput(string Module, Guid BatchId, IReadOnlyCollection<HrMigrationRow> Rows, string Reason);

public sealed class AdminAICreateEmployeeCompensationAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAICreateEmployeeCompensationInput, ApiResponse<Guid>>(mediator, preview)
{ public override string Key => "admin.hr.compensation.create"; protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreateEmployeeCompensationInput i, Guid actor, string op) => new CreateEmployeeCompensationCommand(i.EmployeeId, i.BaseSalary, i.Currency, i.EffectiveFrom, i.EffectiveTo, i.Reason); protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> r) => HrGovernanceOutcome.From(r, ["hr-compensation", "hr-payroll"]); }
public sealed class AdminAICreatePayComponentAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAICreatePayComponentInput, ApiResponse<Guid>>(mediator, preview)
{ public override string Key => "admin.hr.pay-component.create"; protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreatePayComponentInput i, Guid actor, string op) => new CreatePayComponentCommand(i.Code, i.Name, i.Classification, i.IsTaxable, i.IsInsurable); protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> r) => HrGovernanceOutcome.From(r, ["hr-payroll"]); }
public sealed class AdminAICreatePayrollRuleAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAICreatePayrollRuleInput, ApiResponse<Guid>>(mediator, preview)
{ public override string Key => "admin.hr.payroll-rule.create"; protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreatePayrollRuleInput i, Guid actor, string op) => new CreatePayrollRuleCommand(i.PayComponentId, i.Name, i.Expression, i.Rate, i.EffectiveFrom, i.EffectiveTo, i.Priority); protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> r) => HrGovernanceOutcome.From(r, ["hr-payroll"]); }
public sealed class AdminAIPrepareHrPayrollAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAIPrepareHrPayrollInput, ApiResponse<Guid>>(mediator, preview)
{ public override string Key => "admin.hr.payroll.prepare"; protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAIPrepareHrPayrollInput i, Guid actor, string op) => new PreparePayrollCommand(i.PeriodStart, i.PeriodEnd, i.CutoffAt, actor); protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> r) => HrGovernanceOutcome.From(r, ["hr-payroll", "hr-finance"]); }
public sealed class AdminAIGrantLeaveBalanceAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAIGrantLeaveBalanceInput, ApiResponse<Guid>>(mediator, preview)
{ public override string Key => "admin.hr.leave-balance.grant"; protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAIGrantLeaveBalanceInput i, Guid actor, string op) => new GrantLeaveBalanceCommand(actor, i.EmployeeId, i.LeaveTypeId, i.Year, i.Amount, i.Reason); protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> r) => HrGovernanceOutcome.From(r, ["hr-leave-balances"]); }
public sealed class AdminAIDecideHrApprovalAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAIDecideHrApprovalInput, ApiResponse<bool>>(mediator, preview)
{ public override string Key => "admin.hr.approval.decide"; protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIDecideHrApprovalInput i, Guid actor, string op) => new DecideApprovalCommand(i.InstanceId, actor, i.Approve, i.Reason, i.ExpectedVersion); protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => HrGovernanceOutcome.From(r, ["hr-approvals", "hr-leave"]); }
public sealed class AdminAIExecuteHrRetentionAction(HrRetentionService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIHrRetentionInput>(preview)
{ public override string Key => "admin.hr.retention.execute"; protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIHrRetentionInput i, string op, CancellationToken ct) { var r = await service.ExecuteAsync(i.Today, i.CandidateRetentionYears, actor, i.Reason, ct); return AdminAIActionOutcomeFactory.Success(new { r }, null, ["hr-retention"]); } }
public sealed class AdminAIApplyHrMigrationAction(HrMigrationService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIHrMigrationInput>(preview)
{ public override string Key => "admin.hr.migration.apply"; protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIHrMigrationInput i, string op, CancellationToken ct) => HrGovernanceOutcome.From(await service.ApplyAndReconcileAsync(i.BatchId, i.Rows, actor, ct), ["hr-migration"]); }
public sealed class AdminAIActivateHrMigrationAction(HrMigrationService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIHrMigrationInput>(preview)
{ public override string Key => "admin.hr.migration.activate"; protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIHrMigrationInput i, string op, CancellationToken ct) => HrGovernanceOutcome.From(await service.ActivateAsync(i.Module, i.BatchId, actor, i.Reason, ct), ["hr-migration", "hr"]); }

internal static class HrGovernanceOutcome
{
    public static AdminAIActionOutcome From<T>(ApiResponse<T> r, IReadOnlyList<string> scopes) => r.Success
        ? AdminAIActionOutcomeFactory.Success(new { r.Data, r.Message }, 1, scopes)
        : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, scopes);
}
