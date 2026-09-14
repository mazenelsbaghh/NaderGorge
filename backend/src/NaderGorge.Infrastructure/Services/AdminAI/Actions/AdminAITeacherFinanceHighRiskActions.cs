using MediatR;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAICreateTeacherAgreementInput(Guid TeacherId, TeacherAgreementTerms Terms);
public sealed record AdminAIReplaceTeacherAgreementInput(Guid AgreementId, TeacherAgreementTerms Terms);
public sealed record AdminAISetCodeGroupFinancialTermsInput(Guid CodeGroupId, TeacherAgreementTrigger Trigger, Guid? AgreementId, string? Recipient);
public sealed record AdminAIConfirmCodeGroupDeliveryInput(Guid CodeGroupId, string Recipient, string? AttachmentUrl, DateTime? DeliveredAt);
public sealed record AdminAICreateTeacherSettlementInput(SettlementCreationInput Settlement);
public sealed record AdminAITransitionTeacherSettlementInput(Guid SettlementId, TeacherSettlementStatus Expected, TeacherSettlementStatus Next);
public sealed record AdminAIPayTeacherSettlementInput(Guid SettlementId, SettlementPaymentInput Payment);
public sealed record AdminAICancelTeacherSettlementInput(Guid SettlementId);
public sealed record AdminAIReverseTeacherAllocationInput(IReadOnlyList<ReversalLineInput> Lines, string Reason, TeacherReversalDisposition Disposition);
public sealed record AdminAIAttachTeacherInvoiceInput(Guid InvoiceId, string AttachmentUrl);

public sealed class AdminAICreateTeacherAgreementAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateTeacherAgreementInput, TeacherFinanceCommandResult>(mediator, preview)
{
    public override string Key => "admin.teacher-finance.agreement.create";
    protected override IRequest<TeacherFinanceCommandResult> CreateCommand(AdminAICreateTeacherAgreementInput i, Guid actor, string operationId) => new CreateTeacherAgreementCommand(actor, i.TeacherId, i.Terms);
    protected override AdminAIActionOutcome ToOutcome(TeacherFinanceCommandResult r) => TeacherFinanceOutcome.From(r, ["teacher-agreements", "teacher-finance"]);
}

public sealed class AdminAIReplaceTeacherAgreementAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIReplaceTeacherAgreementInput, TeacherFinanceCommandResult>(mediator, preview)
{
    public override string Key => "admin.teacher-finance.agreement.replace";
    protected override IRequest<TeacherFinanceCommandResult> CreateCommand(AdminAIReplaceTeacherAgreementInput i, Guid actor, string operationId) => new ReplaceTeacherAgreementCommand(actor, i.AgreementId, i.Terms);
    protected override AdminAIActionOutcome ToOutcome(TeacherFinanceCommandResult r) => TeacherFinanceOutcome.From(r, ["teacher-agreements", "teacher-finance"]);
}

public sealed class AdminAISetCodeGroupFinancialTermsAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAISetCodeGroupFinancialTermsInput, TeacherFinanceCommandResult>(mediator, preview)
{
    public override string Key => "admin.teacher-finance.code-group.terms.set";
    protected override IRequest<TeacherFinanceCommandResult> CreateCommand(AdminAISetCodeGroupFinancialTermsInput i, Guid actor, string operationId) => new SetCodeGroupFinancialTermsCommand(actor, i.CodeGroupId, i.Trigger, i.AgreementId, i.Recipient);
    protected override AdminAIActionOutcome ToOutcome(TeacherFinanceCommandResult r) => TeacherFinanceOutcome.From(r, ["code-groups", "teacher-finance"]);
}

public sealed class AdminAIConfirmCodeGroupDeliveryAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIConfirmCodeGroupDeliveryInput, TeacherFinanceCommandResult>(mediator, preview)
{
    public override string Key => "admin.teacher-finance.code-group.delivery.confirm";
    protected override IRequest<TeacherFinanceCommandResult> CreateCommand(AdminAIConfirmCodeGroupDeliveryInput i, Guid actor, string operationId) => new ConfirmCodeGroupDeliveryCommand(actor, i.CodeGroupId, i.Recipient, i.AttachmentUrl, i.DeliveredAt);
    protected override AdminAIActionOutcome ToOutcome(TeacherFinanceCommandResult r) => TeacherFinanceOutcome.From(r, ["code-groups", "teacher-finance"]);
}

public sealed class AdminAICreateTeacherSettlementAction(TeacherSettlementAuthorityService service, IAdminAIActionPreviewSource preview)
    : AdminAIServiceActionCapability<AdminAICreateTeacherSettlementInput>(preview)
{
    public override string Key => "admin.teacher-finance.settlement.create";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAICreateTeacherSettlementInput i, string operationId, CancellationToken ct) => TeacherFinanceOutcome.From(await service.CreateAsync(actor, i.Settlement, ct), ["teacher-settlements", "teacher-finance"]);
}

public sealed class AdminAITransitionTeacherSettlementAction(TeacherSettlementAuthorityService service, IAdminAIActionPreviewSource preview)
    : AdminAIServiceActionCapability<AdminAITransitionTeacherSettlementInput>(preview)
{
    public override string Key => "admin.teacher-finance.settlement.transition";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAITransitionTeacherSettlementInput i, string operationId, CancellationToken ct) => TeacherFinanceOutcome.From(await service.TransitionAsync(actor, i.SettlementId, i.Expected, i.Next, ct), ["teacher-settlements", "teacher-invoices", "teacher-finance"]);
}

public sealed class AdminAIPayTeacherSettlementAction(TeacherSettlementAuthorityService service, IAdminAIActionPreviewSource preview)
    : AdminAIServiceActionCapability<AdminAIPayTeacherSettlementInput>(preview)
{
    public override string Key => "admin.teacher-finance.settlement.pay";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIPayTeacherSettlementInput i, string operationId, CancellationToken ct) => TeacherFinanceOutcome.From(await service.PayAsync(actor, i.SettlementId, i.Payment, ct), ["teacher-settlements", "teacher-invoices", "teacher-balances", "teacher-finance"]);
}

public sealed class AdminAICancelTeacherSettlementAction(TeacherSettlementAuthorityService service, IAdminAIActionPreviewSource preview)
    : AdminAIServiceActionCapability<AdminAICancelTeacherSettlementInput>(preview)
{
    public override string Key => "admin.teacher-finance.settlement.cancel";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAICancelTeacherSettlementInput i, string operationId, CancellationToken ct) => TeacherFinanceOutcome.From(await service.CancelAsync(i.SettlementId, ct), ["teacher-settlements", "teacher-invoices", "teacher-balances", "teacher-finance"]);
}

public sealed class AdminAIReverseTeacherAllocationAction(TeacherSettlementAuthorityService service, IAdminAIActionPreviewSource preview)
    : AdminAIServiceActionCapability<AdminAIReverseTeacherAllocationInput>(preview)
{
    public override string Key => "admin.teacher-finance.allocation.reverse";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIReverseTeacherAllocationInput i, string operationId, CancellationToken ct) => TeacherFinanceOutcome.From(await service.ReverseAsync(new(i.Lines, i.Reason, i.Disposition, operationId), ct), ["teacher-allocations", "teacher-balances", "teacher-finance"]);
}

public sealed class AdminAIAttachTeacherInvoiceAction(TeacherSettlementAuthorityService service, IAdminAIActionPreviewSource preview)
    : AdminAIServiceActionCapability<AdminAIAttachTeacherInvoiceInput>(preview)
{
    public override string Key => "admin.teacher-finance.invoice.attach";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIAttachTeacherInvoiceInput i, string operationId, CancellationToken ct) => TeacherFinanceOutcome.From(await service.AttachInvoiceAsync(i.InvoiceId, i.AttachmentUrl, ct), ["teacher-invoices", "teacher-finance"]);
}

internal static class TeacherFinanceOutcome
{
    public static AdminAIActionOutcome From(TeacherFinanceCommandResult response, IReadOnlyList<string> scopes) => response.Status == TeacherFinanceCommandStatus.Success
        ? AdminAIActionOutcomeFactory.Success(new { response.Id, response.OccurredAt, response.AlreadyApplied, response.Message }, response.AlreadyApplied ? 0 : 1, scopes)
        : AdminAIActionOutcomeFactory.Rejected(new { status = response.Status.ToString(), response.Message }, scopes);
}
