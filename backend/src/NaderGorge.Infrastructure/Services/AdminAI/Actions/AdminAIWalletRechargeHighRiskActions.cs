using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Recharge;
using NaderGorge.Application.Features.Admin.Wallets;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAICreateWalletInput(string PhoneNumber, string Label, decimal DailyLimit, decimal MonthlyLimit, List<string> SmsSenderFilters);
public sealed record AdminAIToggleWalletInput(Guid WalletId, bool IsActive);
public sealed record AdminAIUpdateWalletLimitsInput(Guid WalletId, string Label, decimal DailyLimit, decimal MonthlyLimit, List<string> SmsSenderFilters);
public sealed record AdminAIRegenerateWalletTokenInput(Guid WalletId);
public sealed record AdminAIResolveRechargeInput(Guid RechargeRequestId, bool Approve, string? RejectionReason, Guid? SmsLogId, Guid? WalletId);
public sealed record AdminAIReassignRechargeSmsInput(Guid TargetRechargeRequestId, Guid SmsLogId, string Reason);
public sealed record AdminAIReverseRechargeCreditInput(Guid RechargeRequestId, string Reason, bool PreserveWalletBalance);

public sealed class AdminAICreateWalletAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAICreateWalletInput, ApiResponse<WalletDto>>(m, p)
{
    public override string Key => "admin.wallet.create";
    protected override IRequest<ApiResponse<WalletDto>> CreateCommand(AdminAICreateWalletInput i, Guid a, string o) => new CreateWalletCommand(i.PhoneNumber, i.Label, i.DailyLimit, i.MonthlyLimit, i.SmsSenderFilters);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<WalletDto> r) => r.Success
        ? AdminAIActionOutcomeFactory.Success(new { walletId = r.Data!.Id, r.Data.Label, r.Data.IsActive }, 1, ["wallets", "recharge"])
        : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["wallets", "recharge"]);
}
public sealed class AdminAIToggleWalletAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIToggleWalletInput, ApiResponse>(m, p)
{
    public override string Key => "admin.wallet.activation.toggle";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIToggleWalletInput i, Guid a, string o) => new ToggleWalletActiveCommand(i.WalletId, i.IsActive);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse r) => IdentityOutcome.From(r, ["wallets", "recharge"]);
}
public sealed class AdminAIUpdateWalletLimitsAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIUpdateWalletLimitsInput, ApiResponse>(m, p)
{
    public override string Key => "admin.wallet.limits.update";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIUpdateWalletLimitsInput i, Guid a, string o) => new UpdateWalletLimitsCommand(i.WalletId, i.Label, i.DailyLimit, i.MonthlyLimit, i.SmsSenderFilters);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse r) => IdentityOutcome.From(r, ["wallets", "recharge"]);
}
public sealed class AdminAIRegenerateWalletTokenAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIRegenerateWalletTokenInput, ApiResponse<string>>(m, p)
{
    public override string Key => "admin.wallet.token.regenerate";
    protected override IRequest<ApiResponse<string>> CreateCommand(AdminAIRegenerateWalletTokenInput i, Guid a, string o) => new RegenerateWalletTokenCommand(i.WalletId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<string> r) => r.Success
        ? AdminAIActionOutcomeFactory.Success(new { rotated = true }, 1, ["wallets", "wallet-devices"])
        : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["wallets", "wallet-devices"]);
}
public sealed class AdminAIResolveRechargeAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIResolveRechargeInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.recharge.request.resolve";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIResolveRechargeInput i, Guid a, string o) => new ResolveRechargeRequestCommand(i.RechargeRequestId, i.Approve, a, i.RejectionReason, i.SmsLogId, i.WalletId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["recharge", "wallets", "balances", "finance"]);
}
public sealed class AdminAIReassignRechargeSmsAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIReassignRechargeSmsInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.recharge.sms.reassign";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIReassignRechargeSmsInput i, Guid a, string o) => new ReassignRechargeSmsCommand(i.TargetRechargeRequestId, i.SmsLogId, a, i.Reason);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["recharge", "wallets", "balances", "finance"]);
}
public sealed class AdminAIReverseRechargeCreditAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIReverseRechargeCreditInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.recharge.credit.reverse";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIReverseRechargeCreditInput i, Guid a, string o) => new ReverseRechargeCreditCommand(i.RechargeRequestId, a, i.Reason, i.PreserveWalletBalance);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["recharge", "wallets", "balances", "finance"]);
}
