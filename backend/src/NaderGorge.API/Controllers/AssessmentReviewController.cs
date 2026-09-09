using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;

namespace NaderGorge.API.Controllers;

[ApiController, Authorize, Route("api/admin")]
public class AssessmentReviewController(IMediator mediator) : ControllerBase
{
    public record GradeRequest(List<AssessmentScoreInput> Scores, string? Feedback);
    private AssessmentTarget Target(AssessmentKind kind, Guid id, Guid attemptId) => new(kind, id, attemptId, User.RequireUserId());

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
