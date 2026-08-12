using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIDeleteSubjectInput(Guid SubjectId);
public sealed record AdminAIDeleteTermInput(Guid TermId);
public sealed record AdminAIDeleteVideoInput(Guid VideoId);
public sealed record AdminAITogglePackageInput(Guid PackageId);
public sealed record AdminAIToggleVideoInput(Guid VideoId);
public sealed record AdminAISetAssessmentActiveInput(Guid AssessmentId, bool IsActive);
public sealed record AdminAICancelAiJobInput(Guid VideoId, bool MindmapOnly);

public sealed class AdminAIDeleteSubjectAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIDeleteSubjectInput, ApiResponse>(m, p)
{
    public override string Key => "admin.content.subject.delete";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIDeleteSubjectInput i, Guid a, string o) => new DeleteSubjectCommand(i.SubjectId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse r) => IdentityOutcome.From(r, ["subjects", "content"]);
}
public sealed class AdminAIDeleteTermAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIDeleteTermInput, ApiResponse>(m, p)
{
    public override string Key => "admin.content.term.delete";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIDeleteTermInput i, Guid a, string o) => new DeleteTermCommand(i.TermId, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse r) => IdentityOutcome.From(r, ["terms", "content"]);
}
public sealed class AdminAIDeleteVideoAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIDeleteVideoInput, ApiResponse>(m, p)
{
    public override string Key => "admin.content.video.delete";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIDeleteVideoInput i, Guid a, string o) => new DeleteVideoCommand(i.VideoId, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse r) => IdentityOutcome.From(r, ["videos", "content"]);
}
public sealed class AdminAITogglePackageAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAITogglePackageInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.content.package.activation.toggle";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAITogglePackageInput i, Guid a, string o) => new TogglePackageActiveCommand(i.PackageId, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["packages", "content"]);
}
public sealed class AdminAIToggleVideoAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIToggleVideoInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.content.video.activation.toggle";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIToggleVideoInput i, Guid a, string o) => new ToggleVideoActiveCommand(i.VideoId, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["videos", "content"]);
}
public sealed class AdminAISetExamActiveAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAISetAssessmentActiveInput, ApiResponse>(m, p)
{
    public override string Key => "admin.content.exam.activation.set";
    protected override IRequest<ApiResponse> CreateCommand(AdminAISetAssessmentActiveInput i, Guid a, string o) => new SetExamActiveStatusCommand(i.AssessmentId, i.IsActive, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse r) => IdentityOutcome.From(r, ["exams", "content"]);
}
public sealed class AdminAISetHomeworkActiveAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAISetAssessmentActiveInput, ApiResponse>(m, p)
{
    public override string Key => "admin.content.homework.activation.set";
    protected override IRequest<ApiResponse> CreateCommand(AdminAISetAssessmentActiveInput i, Guid a, string o) => new SetHomeworkActiveStatusCommand(i.AssessmentId, i.IsActive, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse r) => IdentityOutcome.From(r, ["homework", "content"]);
}
public sealed class AdminAICancelAiJobAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAICancelAiJobInput, bool>(m, p)
{
    public override string Key => "admin.content.ai-job.cancel";
    protected override IRequest<bool> CreateCommand(AdminAICancelAiJobInput i, Guid a, string o) => new CancelAnalyzeVideoAICommand(i.VideoId, a, i.MindmapOnly);
    protected override AdminAIActionOutcome ToOutcome(bool r) => r ? AdminAIActionOutcomeFactory.Success(new { cancelled = true }, 1, ["ai-jobs", "videos"]) : AdminAIActionOutcomeFactory.Rejected(new { cancelled = false }, ["ai-jobs", "videos"]);
}
