using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    // --- Users ---
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20, 
        [FromQuery] string? search = null,
        [FromQuery] string? educationStage = null,
        [FromQuery] string? gradeLevel = null,
        [FromQuery] string? studyTrack = null,
        [FromQuery] string? gender = null,
        [FromQuery] string? governorate = null
    )
        => Ok(await _mediator.Send(new ListUsersQuery(page, pageSize, search, educationStage, gradeLevel, studyTrack, gender, governorate)));

    [HttpGet("users/students/{userId:guid}/profile")]
    public async Task<IActionResult> GetStudentProfile(Guid userId)
        => Ok(await _mediator.Send(new GetStudentProfileDetailQuery(userId)));


    [HttpPut("users/{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest dto)
    {
        var result = await _mediator.Send(new UpdateUserStatusCommand(id, dto.Status, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("users/students/{userId:guid}/status")]
    public async Task<IActionResult> ToggleStudentStatus(Guid userId, [FromBody] ToggleStudentStatusRequest dto)
    {
        var result = await _mediator.Send(new ToggleStudentSystemAccessCommand(userId, dto.IsActive, dto.Reason, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPut("users/{id:guid}/roles")]
    public async Task<IActionResult> UpdateUserRoles(Guid id, [FromBody] UpdateUserRolesRequest dto)
    {
        var result = await _mediator.Send(new UpdateUserRoleCommand(id, dto.Roles, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("users/{id:guid}/devices")]
    public async Task<IActionResult> GetUserDevices(Guid id)
        => Ok(await _mediator.Send(new GetUserDevicesQuery(id)));

    [HttpDelete("users/students/{userId:guid}/devices/{deviceId:guid}")]
    public async Task<IActionResult> DisconnectDevice(Guid userId, Guid deviceId)
    {
        var result = await _mediator.Send(new DisconnectStudentDeviceCommand(userId, deviceId, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpDelete("users/students/{userId:guid}/devices")]
    public async Task<IActionResult> DisconnectAllDevices(Guid userId)
    {
        var result = await _mediator.Send(new DisconnectStudentDeviceCommand(userId, null, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpDelete("devices/{id:guid}")]
    public async Task<IActionResult> RemoveDevice(Guid id)
    {
        var result = await _mediator.Send(new RemoveDeviceCommand(id, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Codes ---
    [HttpGet("codes/groups")]
    public async Task<IActionResult> ListCodeGroups()
        => Ok(await _mediator.Send(new ListCodeGroupsQuery()));

    [HttpGet("codes/groups/{id:guid}/details")]
    public async Task<IActionResult> GetCodeGroupDetails(Guid id)
    {
        var result = await _mediator.Send(new GetCodeGroupCodesQuery(id));
        return result.Success ? Ok(result) : NotFound(result);
    }

    // --- Student Profile Actions ---
    [HttpPost("users/students/{userId:guid}/overrides")]
    public async Task<IActionResult> OverrideVideoLimit(Guid userId, [FromBody] OverrideVideoLimitRequest dto)
    {
        var result = await _mediator.Send(new OverrideVideoLimitCommand(userId, dto.VideoId, dto.AddedViews, dto.Reason, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPost("users/students/{userId:guid}/gamification/adjust")]
    public async Task<IActionResult> AdjustGamification(Guid userId, [FromBody] GamificationAdjustmentRequest dto)
    {
        var result = await _mediator.Send(new AdjustGamificationPointsCommand(userId, dto.Points, dto.Reason, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    // --- Content ---
    [HttpPost("packages")]
    public async Task<IActionResult> CreatePackage(CreatePackageCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreatePackage), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpGet("packages/{id:guid}")]
    public async Task<IActionResult> GetPackageById(Guid id)
        => Ok(await _mediator.Send(new NaderGorge.Application.Features.Content.Queries.GetPackageByIdQuery(id)));

    [HttpPut("packages/{id:guid}")]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] UpdatePackageDto dto)
    {
        var result = await _mediator.Send(new UpdatePackageCommand(id, dto.Name, dto.Description, dto.Price, dto.IsActive));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("terms")]
    public async Task<IActionResult> CreateTerm(CreateTermCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateTerm), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpGet("terms/{id:guid}")]
    public async Task<IActionResult> GetTermById(Guid id)
        => Ok(await _mediator.Send(new NaderGorge.Application.Features.Content.Queries.GetTermByIdQuery(id)));

    [HttpPut("terms/{id:guid}")]
    public async Task<IActionResult> UpdateTerm(Guid id, [FromBody] UpdateTermDto dto)
    {
        var result = await _mediator.Send(new UpdateTermCommand(id, dto.Title, dto.Order, dto.Price));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("terms/{id:guid}")]
    public async Task<IActionResult> DeleteTerm(Guid id)
    {
        var result = await _mediator.Send(new DeleteTermCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("sections/{id:guid}")]
    public async Task<IActionResult> GetSectionById(Guid id)
        => Ok(await _mediator.Send(new NaderGorge.Application.Features.Content.Queries.GetSectionByIdQuery(id)));

    [HttpPost("sections")]
    public async Task<IActionResult> CreateSection(CreateSectionCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateSection), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("lessons")]
    public async Task<IActionResult> CreateLesson(CreateLessonCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateLesson), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpGet("lessons/{lessonId:guid}/cockpit")]
    public async Task<IActionResult> GetLessonCockpit(Guid lessonId)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Content.Queries.GetLessonCockpitQuery(lessonId));
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("videos")]
    public async Task<IActionResult> CreateVideo(CreateVideoCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateVideo), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("resources")]
    public async Task<IActionResult> CreateResource(CreateLessonResourceCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateResource), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("content/lessons/{lessonId:guid}/homework")]
    public async Task<IActionResult> AttachHomework(Guid lessonId, [FromBody] AttachHomeworkRequest dto)
    {
        var cmd = new AttachHomeworkCommand(
            lessonId, 
            dto.Title, 
            dto.Instructions, 
            dto.IsMandatory, 
            dto.RequiredPointsToPass, 
            dto.TotalScore,
            dto.Questions);
            
        var result = await _mediator.Send(cmd);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("lessons/{lessonId:guid}/exam")]
    public async Task<IActionResult> LinkExam(Guid lessonId, [FromBody] LinkLessonExamRequest dto)
    {
        var result = await _mediator.Send(new LinkLessonExamCommand(lessonId, dto.ExamId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("exams/inline")]
    public async Task<IActionResult> CreateInlineExam([FromBody] CreateInlineExamCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    
    [HttpPost("exams/{examId:guid}/questions")]
    public async Task<IActionResult> AddQuestionsToExam(Guid examId, [FromBody] AddQuestionsToExamRequest dto)
    {
        var result = await _mediator.Send(new AddQuestionsToExamCommand { ExamId = examId, Questions = dto.Questions });
        return result.Success ? Ok(result) : BadRequest(result);
    }
    
    [HttpGet("exams/{examId:guid}/dashboard")]
    public async Task<IActionResult> GetExamDashboard(Guid examId)
    {
        var result = await _mediator.Send(new GetExamDashboardQuery(examId));
        return result.Success ? Ok(result) : NotFound(result);
    }

    // --- Questions ---
    [HttpGet("questions")]
    public async Task<IActionResult> ListQuestions([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _mediator.Send(new ListQuestionsQuery(page, pageSize, search)));

    [HttpPost("questions")]
    public async Task<IActionResult> CreateQuestion(CreateQuestionCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateQuestion), new { id = result.Data }, result) : BadRequest(result);
    }

    // --- Overrides ---
    [HttpPost("overrides/reset-watch")]
    public async Task<IActionResult> ResetWatchLimit([FromBody] ResetWatchRequest dto)
    {
        var result = await _mediator.Send(new ResetWatchLimitCommand(dto.LessonVideoId, dto.StudentId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Codes ---
    [HttpPost("codes/bulk-generate")]
    public async Task<IActionResult> BulkGenerateCodes([FromBody] BulkGenerateRequest dto)
    {
        var result = await _mediator.Send(new BulkGenerateCodesCommand(
            GroupName: dto.GroupName,
            CodeType: dto.CodeType,
            Count: dto.Count,
            CodeLength: dto.CodeLength,
            AdminId: GetUserId(),
            PackageId: dto.PackageId,
            TermId: dto.TermId,
            ContentSectionId: dto.ContentSectionId,
            LessonId: dto.LessonId,
            ExamId: dto.ExamId,
            VideoTargetIds: dto.VideoTargetIds,
            BalanceAmount: dto.BalanceAmount,
            DiscountPercentage: dto.DiscountPercentage,
            ExpiresAt: dto.ExpiresAt
        ));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record UpdateUserStatusRequest(string Status);
public record UpdateUserRolesRequest(string[] Roles);
public record ResetWatchRequest(Guid LessonVideoId, Guid StudentId);
public record ToggleStudentStatusRequest(bool IsActive, string? Reason);
public record OverrideVideoLimitRequest(Guid VideoId, int AddedViews, string Reason);
public record GamificationAdjustmentRequest(int Points, string Reason);
public record BulkGenerateRequest(
    string GroupName,
    Domain.Enums.CodeType CodeType,
    int Count,
    int CodeLength,
    Guid? PackageId = null,
    Guid? TermId = null,
    Guid? ContentSectionId = null,
    Guid? LessonId = null,
    Guid? ExamId = null,
    List<Guid>? VideoTargetIds = null,
    decimal? BalanceAmount = null,
    decimal? DiscountPercentage = null,
    DateTime? ExpiresAt = null
);
public record AttachHomeworkRequest(string Title, string Instructions, bool IsMandatory, int RequiredPointsToPass, decimal TotalScore, List<AttachHomeworkQuestionDto> Questions);
public record LinkLessonExamRequest(Guid? ExamId);
public record UpdateTermDto(string Title, int Order, decimal Price);
public record UpdatePackageDto(string Name, string Description, decimal Price, bool IsActive);
public record AddQuestionsToExamRequest(List<InlineExamQuestionDto> Questions);

