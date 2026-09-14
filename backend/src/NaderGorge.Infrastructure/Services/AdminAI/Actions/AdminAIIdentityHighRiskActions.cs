using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using System.Text;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIUpdateUserRolesInput(Guid UserId, string[] Roles);
public sealed record AdminAIUpdateUserStatusInput(Guid UserId, string NewStatus);
public sealed record AdminAIRemoveDeviceInput(Guid DeviceId);
public sealed record AdminAIDisconnectDevicesInput(Guid UserId, Guid? DeviceId);
public sealed record AdminAIAdjustBalanceInput(Guid StudentId, decimal Amount, string Reason, string? Scope, string? Operation, Guid? TeacherId);
public sealed record AdminAIAdjustGamificationInput(Guid UserId, decimal Points, string Reason);
public sealed record AdminAIResetWatchLimitInput(Guid LessonVideoId, Guid StudentId);
public sealed record AdminAISetWatchCountInput(Guid LessonVideoId, Guid StudentId, int NewWatchCount);
public sealed record AdminAIApproveWatchRequestInput(Guid RequestId, string? Reason, int AddedViews = 1);
public sealed record AdminAIRejectWatchRequestInput(Guid RequestId, string Reason);
public sealed record AdminAICancelAccessGrantInput(Guid AccessGrantId, bool RefundBalance, string? Reason);
public sealed record AdminAIToggleSystemAccessInput(Guid UserId, bool IsActive, string Reason);
public sealed record AdminAICreateRoleInput(string Name, List<string> Permissions, string AllowedDomain, List<string> AllowedNavbarItems);
public sealed record AdminAIUpdateRoleInput(Guid RoleId, string Name, List<string> Permissions, string AllowedDomain, List<string> AllowedNavbarItems);
public sealed record AdminAIDeleteRoleInput(Guid RoleId);
public sealed record AdminAIResetPasswordInput(Guid UserId);

public sealed class AdminAIResetPasswordAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAISecureMediatRActionCapability<AdminAIResetPasswordInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.user.password-reset";
    public override string SecureInputKind => "Password";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIResetPasswordInput input, ReadOnlyMemory<byte> secureInput, Guid actorId, string operationId) =>
        new AdminResetPasswordCommand(input.UserId, Encoding.UTF8.GetString(secureInput.Span), actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["users", "credentials", "sessions"]);
}

public sealed class AdminAIUpdateUserRolesAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIUpdateUserRolesInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.user.roles.update";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIUpdateUserRolesInput input, Guid actorId, string operationId) => new UpdateUserRoleCommand(input.UserId, input.Roles, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["users", "roles", "authorization"]);
}

public sealed class AdminAIUpdateUserStatusAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIUpdateUserStatusInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.user.status.update";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIUpdateUserStatusInput input, Guid actorId, string operationId) => new UpdateUserStatusCommand(input.UserId, input.NewStatus, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["users", "authorization", "sessions"]);
}

public sealed class AdminAIRemoveDeviceAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIRemoveDeviceInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.device.remove";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIRemoveDeviceInput input, Guid actorId, string operationId) => new RemoveDeviceCommand(input.DeviceId, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["devices", "sessions"]);
}

public sealed class AdminAIDisconnectDevicesAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIDisconnectDevicesInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.device.disconnect";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIDisconnectDevicesInput input, Guid actorId, string operationId) => new DisconnectStudentDeviceCommand(input.UserId, input.DeviceId, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["devices", "sessions"]);
}

public sealed class AdminAIAdjustBalanceAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIAdjustBalanceInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.student.balance.adjust";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIAdjustBalanceInput input, Guid actorId, string operationId) =>
        new AdjustBalanceCommand(input.StudentId, input.Amount, input.Reason, actorId, input.Scope, input.Operation, input.TeacherId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["balances", "finance", "students"]);
}

public sealed class AdminAIAdjustGamificationAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIAdjustGamificationInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.student.gamification.adjust";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIAdjustGamificationInput input, Guid actorId, string operationId) =>
        new AdjustGamificationPointsCommand(input.UserId, input.Points, input.Reason, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["gamification", "students"]);
}

public sealed class AdminAIResetWatchLimitAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIResetWatchLimitInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.watch-limit.reset";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIResetWatchLimitInput input, Guid actorId, string operationId) => new ResetWatchLimitCommand(input.LessonVideoId, input.StudentId, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["watch-progress", "watch-requests"]);
}

public sealed class AdminAISetWatchCountAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAISetWatchCountInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.watch-count.set";
    protected override IRequest<ApiResponse> CreateCommand(AdminAISetWatchCountInput input, Guid actorId, string operationId) => new SetWatchCountCommand(input.LessonVideoId, input.StudentId, input.NewWatchCount, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["watch-progress", "watch-requests"]);
}

public sealed class AdminAIApproveWatchRequestAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIApproveWatchRequestInput, ApiResponse<bool>>(mediator, preview)
{
    public override string Key => "admin.identity.watch-request.approve";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIApproveWatchRequestInput input, Guid actorId, string operationId) => new ApproveWatchRequestCommand(input.RequestId, actorId, input.Reason, input.AddedViews);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> response) => IdentityOutcome.From(response, ["watch-progress", "watch-requests"]);
}

public sealed class AdminAIRejectWatchRequestAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIRejectWatchRequestInput, ApiResponse<bool>>(mediator, preview)
{
    public override string Key => "admin.identity.watch-request.reject";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIRejectWatchRequestInput input, Guid actorId, string operationId) => new RejectWatchRequestCommand(input.RequestId, input.Reason);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> response) => IdentityOutcome.From(response, ["watch-progress", "watch-requests"]);
}

public sealed class AdminAICancelAccessGrantAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICancelAccessGrantInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.access-grant.cancel";
    protected override IRequest<ApiResponse> CreateCommand(AdminAICancelAccessGrantInput input, Guid actorId, string operationId) => new CancelPackageGrantCommand(input.AccessGrantId, input.RefundBalance, actorId, input.Reason);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["access-grants", "balances", "finance"]);
}

public sealed class AdminAIToggleSystemAccessAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIToggleSystemAccessInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.system-access.toggle";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIToggleSystemAccessInput input, Guid actorId, string operationId) =>
        new ToggleStudentSystemAccessCommand(input.UserId, input.IsActive, input.Reason, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["users", "authorization", "sessions"]);
}

public sealed class AdminAICreateRoleAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateRoleInput, ApiResponse<Guid>>(mediator, preview)
{
    public override string Key => "admin.identity.role.create";
    protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreateRoleInput input, Guid actorId, string operationId) =>
        new CreateRoleCommand(input.Name, input.Permissions, input.AllowedDomain, input.AllowedNavbarItems);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { roleId = response.Data }, 1, ["roles", "authorization"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["roles", "authorization"]);
}

public sealed class AdminAIUpdateRoleAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIUpdateRoleInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.role.update";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIUpdateRoleInput input, Guid actorId, string operationId) =>
        new UpdateRoleCommand(input.RoleId, input.Name, input.Permissions, input.AllowedDomain, input.AllowedNavbarItems, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["roles", "authorization", "sessions"]);
}

public sealed class AdminAIDeleteRoleAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIDeleteRoleInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.role.delete";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIDeleteRoleInput input, Guid actorId, string operationId) => new DeleteRoleCommand(input.RoleId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["roles", "authorization"]);
}

internal static class IdentityOutcome
{
    public static AdminAIActionOutcome From(ApiResponse response, IReadOnlyList<string> scopes) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { response.Message }, 1, scopes)
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, scopes);

    public static AdminAIActionOutcome From<T>(ApiResponse<T> response, IReadOnlyList<string> scopes) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { response.Message }, 1, scopes)
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, scopes);
}
