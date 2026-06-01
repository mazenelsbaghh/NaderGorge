using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps;
using NaderGorge.Domain.Entities;
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
        var result = await _mediator.Send(new ToggleStudentSystemAccessCommand(userId, dto.IsActive, dto.Reason ?? string.Empty, GetUserId()));
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

    [HttpGet("packages/{id:guid}/code-profile")]
    public async Task<IActionResult> GetPackageCodeProfile(Guid id)
    {
        var result = await _mediator.Send(new GetPackageCodeProfileQuery(id));
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("packages/{id:guid}/code-profile")]
    public async Task<IActionResult> UpsertPackageCodeProfile(Guid id, [FromBody] UpsertPackageCodeProfileRequest dto)
    {
        var result = await _mediator.Send(new UpsertPackageCodeProfileCommand(
            id,
            dto.Status,
            dto.HeroEyebrow,
            dto.HeroTitle,
            dto.HeroDescription,
            dto.OfferTitle,
            dto.OfferDescription,
            dto.ActivationTitle,
            dto.ActivationDescription,
            dto.SupportTitle,
            dto.SupportDescription,
            dto.ThemeAccentKey,
            GetUserId()));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("packages/{id:guid}/code-profile")]
    public async Task<IActionResult> ResetPackageCodeProfile(Guid id)
    {
        var result = await _mediator.Send(new ResetPackageCodeProfileCommand(id, GetUserId()));
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

    [HttpPut("videos/{id:guid}")]
    public async Task<IActionResult> UpdateVideo(Guid id, [FromBody] UpdateVideoRequest dto)
    {
        var result = await _mediator.Send(new UpdateVideoCommand(id, dto.Title, dto.Provider, dto.UrlOrEmbedCode, dto.Order, dto.Limit));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("videos/{id:guid}")]
    public async Task<IActionResult> DeleteVideo(Guid id)
    {
        var result = await _mediator.Send(new DeleteVideoCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("videos/{videoId:guid}/analyze-ai")]
    public async Task<IActionResult> RequestAIAnalysis(Guid videoId)
    {
        var result = await _mediator.Send(new AnalyzeVideoAICommand(videoId));
        return result.Success ? Accepted(result) : BadRequest(result);
    }

    [HttpPost("videos/{videoId:guid}/cancel-ai")]
    public async Task<IActionResult> CancelAIAnalysis(Guid videoId)
    {
        var result = await _mediator.Send(new CancelAnalyzeVideoAICommand(videoId));
        return Ok(new { Success = result });
    }

    [HttpPost("videos/{videoId:guid}/cancel-mindmap")]
    public async Task<IActionResult> CancelMindmapGeneration(Guid videoId)
    {
        var result = await _mediator.Send(new CancelAnalyzeVideoAICommand(videoId, IsMindmapOnly: true));
        return Ok(new { Success = result });
    }

    [HttpPost("videos/{videoId:guid}/generate-mindmaps")]
    public async Task<IActionResult> RequestMindmapGeneration(Guid videoId)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.MindmapOps.GenerateChapterMindmapsCommand(videoId));
        return result.Success ? Accepted(result) : BadRequest(result);
    }

    [HttpPost("chapters/{chapterId:guid}/regenerate-mindmap")]
    public async Task<IActionResult> RegenerateChapterMindmap(Guid chapterId)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.MindmapOps.RegenerateChapterMindmapCommand(chapterId));
        return result.Success ? Accepted(result) : BadRequest(result);
    }

    [HttpPost("resources")]
    public async Task<IActionResult> CreateResource(CreateLessonResourceCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateResource), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("teacher-photos/upload")]
    public async Task<IActionResult> UploadTeacherPhoto([FromBody] UploadTeacherPhotoRequest dto)
    {
        var result = await _mediator.Send(new UploadTeacherPhotoCommand(dto.TeacherId, dto.Base64Image, dto.FileName));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("content/lessons/{lessonId:guid}/homework")]
    public async Task<IActionResult> AttachHomework(Guid lessonId, [FromBody] AttachHomeworkRequest dto)
    {
        var cmd = new AttachHomeworkCommand(
            lessonId, 
            dto.Title, 
            dto.Instructions, 
            dto.IsMandatory,
            dto.IsRandomized,
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

    [HttpDelete("exams/{examId:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteExamQuestion(Guid examId, Guid questionId)
    {
        var result = await _mediator.Send(new DeleteExamQuestionCommand(examId, questionId));
        return result.Success ? Ok(result) : BadRequest(result);
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

    [HttpPost("questions/{id:guid}/audio")]
    public async Task<IActionResult> UploadQuestionAudio(Guid id, [FromForm] Microsoft.AspNetCore.Http.IFormFile audio)
    {
        if (audio == null || audio.Length == 0) return BadRequest(new { Success = false, Message = "No file uploaded" });
        
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.UploadQuestionAudioCommand(id, base64, audio.FileName));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("essays/{essaySubmissionId:guid}/grade")]
    public async Task<IActionResult> GradeEssay(Guid essaySubmissionId, [FromBody] NaderGorge.Application.Features.Admin.Commands.GradeEssayCommand command)
    {
        if (essaySubmissionId != command.EssaySubmissionId) return BadRequest();
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("essays/pending")]
    public async Task<IActionResult> GetPendingEssays([FromServices] NaderGorge.Domain.Interfaces.IAppDbContext db)
    {
        var aiScored = db.EssaySubmissions
            .Where(e => e.Status == NaderGorge.Domain.Entities.EssaySubmissionStatus.AIScored)
            .ToList();

        if (aiScored.Count > 0)
        {
            foreach (var essay in aiScored)
            {
                essay.Status = NaderGorge.Domain.Entities.EssaySubmissionStatus.WaitTeacher;
            }

            await db.SaveChangesAsync();
        }

        var list = db.EssaySubmissions
            .Where(e => e.Status != NaderGorge.Domain.Entities.EssaySubmissionStatus.TeacherGraded)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.StudentId, e.QuestionId, e.AnswerText, e.AiInitialScore, e.AiFeedback, e.Status })
            .ToList();
        return Ok(NaderGorge.Application.Common.ApiResponse<object>.Ok(list));
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

    // Tracking endpoints

    [HttpGet("watch-requests")]
    public async Task<IActionResult> GetWatchRequests(CancellationToken ct)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Queries.GetWatchRequestsQuery(), ct);
        return Ok(result);
    }

    [HttpPost("watch-requests/{id}/approve")]
    public async Task<IActionResult> ApproveWatchRequest(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.ApproveWatchRequestCommand(id), ct);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    [HttpPost("watch-requests/{id}/reject")]
    public async Task<IActionResult> RejectWatchRequest(Guid id, [FromBody] RejectWatchRequestBody request, CancellationToken ct)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.RejectWatchRequestCommand(id, request.Reason), ct);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }
    [HttpGet("settings")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetPlatformSettings()
    {
        var response = await _mediator.Send(new NaderGorge.Application.Features.Admin.Queries.GetPlatformSettingsQuery());
        return Ok(response);
    }

    [HttpPut("settings")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> UpdatePlatformSettings([FromBody] UpdateSettingsRequest req)
    {
        var response = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.UpdatePlatformSettingsCommand(req.Settings));
        return Ok(response);
    }

}

public record UpdateUserStatusRequest(string Status);
public record UpdateUserRolesRequest(string[] Roles);
public record ResetWatchRequest(Guid LessonVideoId, Guid StudentId);
public record RejectWatchRequestBody(string Reason);
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
public record UpdateVideoRequest(string Title, string Provider, string UrlOrEmbedCode, int Order, int Limit);
public record AttachHomeworkRequest(string Title, string Instructions, bool IsMandatory, bool IsRandomized, int RequiredPointsToPass, decimal TotalScore, List<AttachHomeworkQuestionDto> Questions);
public record LinkLessonExamRequest(Guid? ExamId);
public record UpdateTermDto(string Title, int Order, decimal Price);
public record UpdatePackageDto(string Name, string Description, decimal Price, bool IsActive);
public record UpsertPackageCodeProfileRequest(
    PackageCodePageProfileStatus Status,
    string? HeroEyebrow,
    string? HeroTitle,
    string? HeroDescription,
    string? OfferTitle,
    string? OfferDescription,
    string? ActivationTitle,
    string? ActivationDescription,
    string? SupportTitle,
    string? SupportDescription,
    string? ThemeAccentKey
);
public record AddQuestionsToExamRequest(List<InlineExamQuestionDto> Questions);
public record UploadTeacherPhotoRequest(Guid TeacherId, string Base64Image, string FileName);
public record UpdateSettingsRequest(Dictionary<string, string> Settings);
