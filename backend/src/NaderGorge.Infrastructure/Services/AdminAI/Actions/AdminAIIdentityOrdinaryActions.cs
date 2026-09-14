using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIAddStudentNoteInput(Guid StudentId, string Content, bool IsPinned);

public sealed class AdminAIAddStudentNoteAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIAddStudentNoteInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.identity.student-note.create";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIAddStudentNoteInput input, Guid actorId, string operationId) =>
        new AddStudentNoteCommand(input.StudentId, input.Content, input.IsPinned, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { response.Message }, 1, ["students", "student-notes"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["students", "student-notes"]);
}
