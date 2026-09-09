using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Assessments;

namespace NaderGorge.API.Controllers;

[ApiController, Authorize, Route("api/admin")]
public class AssessmentReviewController(IMediator mediator) : ControllerBase
{
    public record GradeRequest(List<AssessmentScoreInput> Scores, string? Feedback);
    public record RevisionPreviewRequest(AssessmentDefinitionSnapshot Definition, AssessmentRevisionPolicy Policy);
    public record RevisionSaveRequest(AssessmentDefinitionSnapshot Definition, AssessmentRevisionPolicy Policy,
        string RevisionToken, Guid OperationId, Guid? SubjectId, bool ConfirmPreviousAttempts = false);
    private AssessmentTarget Target(AssessmentKind kind, Guid id, Guid attemptId) => new(kind, id, attemptId, User.RequireUserId());

    [HttpGet("homework/{id:guid}/editor"), HasPermission("content.manage")]
    public async Task<IActionResult> HomeworkEditor(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAssessmentEditorQuery(Target(AssessmentKind.Homework, id, Guid.Empty)), ct));

    [HttpGet("exams/{id:guid}/editor"), HasPermission("exams.manage")]
    public async Task<IActionResult> ExamEditor(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAssessmentEditorQuery(Target(AssessmentKind.Exam, id, Guid.Empty)), ct));

    [HttpPost("homework/{id:guid}/revision-preview"), HasPermission("content.manage")]
    public Task<IActionResult> PreviewHomeworkRevision(Guid id, RevisionPreviewRequest request, CancellationToken ct) =>
        PreviewRevision(AssessmentKind.Homework, id, request, ct);

    [HttpPost("exams/{id:guid}/revision-preview"), HasPermission("exams.manage")]
    public Task<IActionResult> PreviewExamRevision(Guid id, RevisionPreviewRequest request, CancellationToken ct) =>
        PreviewRevision(AssessmentKind.Exam, id, request, ct);

    [HttpPut("homework/{id:guid}/definition"), HasPermission("content.manage")]
    public Task<IActionResult> SaveHomeworkRevision(Guid id, RevisionSaveRequest request, CancellationToken ct) =>
        SaveRevision(AssessmentKind.Homework, id, request, ct);

    [HttpPut("exams/{id:guid}/definition"), HasPermission("exams.manage")]
    public Task<IActionResult> SaveExamRevision(Guid id, RevisionSaveRequest request, CancellationToken ct) =>
        SaveRevision(AssessmentKind.Exam, id, request, ct);

    private async Task<IActionResult> PreviewRevision(AssessmentKind kind, Guid id, RevisionPreviewRequest request, CancellationToken ct)
    {
        var response = await mediator.Send(new PreviewAssessmentRevisionQuery(Target(kind, id, Guid.Empty), request.Definition, request.Policy), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    private async Task<IActionResult> SaveRevision(AssessmentKind kind, Guid id, RevisionSaveRequest request, CancellationToken ct)
    {
        var response = await mediator.Send(new SaveAssessmentRevisionCommand(Target(kind, id, Guid.Empty), request.Definition,
            request.Policy, request.RevisionToken, request.OperationId, request.SubjectId, request.ConfirmPreviousAttempts), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("homework/{id:guid}/submissions/{attemptId:guid}/review"), HasPermission("content.manage")]
    public async Task<IActionResult> HomeworkReview(Guid id, Guid attemptId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAssessmentReviewQuery(Target(AssessmentKind.Homework, id, attemptId)), ct));

    [HttpGet("exams/{id:guid}/attempts/{attemptId:guid}/assessment-review"), HasPermission("exams.manage")]
    public async Task<IActionResult> ExamReview(Guid id, Guid attemptId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAssessmentReviewQuery(Target(AssessmentKind.Exam, id, attemptId)), ct));

    [HttpPut("homework/{id:guid}/submissions/{attemptId:guid}/grade"), HasPermission("content.manage")]
    public async Task<IActionResult> GradeHomework(Guid id, Guid attemptId, GradeRequest request, CancellationToken ct)
    {
        var response = await mediator.Send(new GradeAssessmentCommand(Target(AssessmentKind.Homework, id, attemptId), request.Scores, request.Feedback), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPut("exams/{id:guid}/attempts/{attemptId:guid}/grade"), HasPermission("exams.manage")]
    public async Task<IActionResult> GradeExam(Guid id, Guid attemptId, GradeRequest request, CancellationToken ct)
    {
        var response = await mediator.Send(new GradeAssessmentCommand(Target(AssessmentKind.Exam, id, attemptId), request.Scores, request.Feedback), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpDelete("homework/{id:guid}/submissions/{attemptId:guid}"), HasPermission("content.manage")]
    public async Task<IActionResult> DeleteHomework(Guid id, Guid attemptId, CancellationToken ct)
    {
        var response = await mediator.Send(new DeleteHomeworkAttemptCommand(id, attemptId, User.RequireUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("homework/{id:guid}/missing-students"), HasPermission("content.manage")]
    public async Task<IActionResult> MissingStudents(Guid id, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var response = await mediator.Send(new GetMissingHomeworkStudentsQuery(id, User.RequireUserId(), page), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
