using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIDeleteFormInput(Guid FormId);
public sealed record AdminAIUpdatePlatformSettingsInput(Dictionary<string, string> Settings);

public sealed class AdminAIDeleteFormAction(IMediator m, IAdminAIActionPreviewSource p)
    : AdminAIMediatRActionCapability<AdminAIDeleteFormInput, ApiResponse>(m, p)
{
    public override string Key => "admin.forms.form.delete";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIDeleteFormInput i, Guid a, string o) => new DeleteFormCommand(i.FormId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse r) => IdentityOutcome.From(r, ["forms", "submissions"]);
}

public sealed class AdminAIUpdatePlatformSettingsAction(IMediator m, IAdminAIActionPreviewSource p)
    : AdminAIMediatRActionCapability<AdminAIUpdatePlatformSettingsInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.settings.platform.update";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIUpdatePlatformSettingsInput i, Guid a, string o) =>
        new UpdatePlatformSettingsCommand(i.Settings);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["platform-settings", "public-shell"]);
}
