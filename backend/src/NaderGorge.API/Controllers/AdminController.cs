using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps;
using NaderGorge.Application.Common;
using NaderGorge.API.Extensions;
using NaderGorge.Domain.Entities;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.Admin.Teachers.Queries;
using NaderGorge.Application.Features.Admin.Content.Queries;
using SixLabors.ImageSharp;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IContentImageStorage _imageStorage;

    public AdminController(IMediator mediator, IContentImageStorage imageStorage)
    {
        _mediator = mediator;
        _imageStorage = imageStorage;
    }

    private Guid GetUserId() => User.RequireUserId();

    // --- Users ---
    [HttpGet("users")]
    [HasPermission("users.manage")]
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

    [HttpPost("users")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest dto)
    {
        var result = await _mediator.Send(new AdminCreateUserCommand(
            dto.FullName, dto.PhoneNumber, dto.Password, dto.Role, dto.PackageIds));
        return result.Success ? StatusCode(201, result) : BadRequest(result);
    }

    [HttpGet("users/students/{userId:guid}/profile")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetStudentProfile(Guid userId)
        => Ok(await _mediator.Send(new GetStudentProfileDetailQuery(userId)));

    [HttpPut("users/students/{userId:guid}/profile")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> UpdateStudentProfile(Guid userId, [FromBody] UpdateStudentProfileRequest dto)
    {
        var result = await _mediator.Send(new UpdateStudentProfileCommand(
            userId, dto.FullName, dto.Phone, dto.ParentPhone, dto.SecondaryPhone, dto.MotherPhone,
            dto.Governorate, dto.District, dto.Address, dto.SchoolName, dto.DateOfBirth,
            dto.Gender, dto.EducationStage, dto.GradeLevel, dto.StudyTrack, dto.SchoolType,
            dto.IsFatherAlive, dto.IsMotherAlive, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/students/{userId:guid}/reset-password")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> AdminResetPassword(Guid userId, [FromBody] AdminResetPasswordRequest dto)
    {
        var result = await _mediator.Send(new AdminResetPasswordCommand(userId, dto.NewPassword, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/students/{userId:guid}/notes")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> AddStudentNote(Guid userId, [FromBody] AddStudentNoteRequest dto)
    {
        var result = await _mediator.Send(new AddStudentNoteCommand(userId, dto.Content, dto.IsPinned, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("users/students/{userId:guid}/notes/{noteId:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> DeleteStudentNote(Guid userId, Guid noteId)
    {
        var result = await _mediator.Send(new DeleteStudentNoteCommand(noteId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("users/{id:guid}/status")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest dto)
    {
        var result = await _mediator.Send(new UpdateUserStatusCommand(id, dto.Status, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("users/students/{userId:guid}/status")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> ToggleStudentStatus(Guid userId, [FromBody] ToggleStudentStatusRequest dto)
    {
        var result = await _mediator.Send(new ToggleStudentSystemAccessCommand(userId, dto.IsActive, dto.Reason ?? string.Empty, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPut("users/{id:guid}/roles")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> UpdateUserRoles(Guid id, [FromBody] UpdateUserRolesRequest dto)
    {
        var result = await _mediator.Send(new UpdateUserRoleCommand(id, dto.Roles, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("users/{id:guid}/devices")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetUserDevices(Guid id)
        => Ok(await _mediator.Send(new GetUserDevicesQuery(id)));

    [HttpGet("users/{id:guid}/audit-logs")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetUserAuditLogs(Guid id)
        => Ok(await _mediator.Send(new GetUserAuditLogsQuery(id)));

    [HttpDelete("users/students/{userId:guid}/devices/{deviceId:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> DisconnectDevice(Guid userId, Guid deviceId)
    {
        var result = await _mediator.Send(new DisconnectStudentDeviceCommand(userId, deviceId, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpDelete("users/students/{userId:guid}/devices")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> DisconnectAllDevices(Guid userId)
    {
        var result = await _mediator.Send(new DisconnectStudentDeviceCommand(userId, null, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpDelete("devices/{id:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> RemoveDevice(Guid id)
    {
        var result = await _mediator.Send(new RemoveDeviceCommand(id, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Codes ---
    [HttpGet("codes/groups")]
    [HasPermission("codes.manage")]
    public async Task<IActionResult> ListCodeGroups()
        => Ok(await _mediator.Send(new ListCodeGroupsQuery(GetUserId())));

    [HttpGet("codes/groups/{id:guid}/details")]
    [HasPermission("codes.manage")]
    public async Task<IActionResult> GetCodeGroupDetails(Guid id)
    {
        var result = await _mediator.Send(new GetCodeGroupCodesQuery(id));
        return result.Success ? Ok(result) : NotFound(result);
    }

    // --- Student Profile Actions ---
    [HttpPost("users/students/{userId:guid}/overrides")]
    [HasPermission("watch_requests.manage")]
    public async Task<IActionResult> OverrideVideoLimit(Guid userId, [FromBody] OverrideVideoLimitRequest dto)
    {
        var result = await _mediator.Send(new OverrideVideoLimitCommand(userId, dto.VideoId, dto.AddedViews, dto.Reason, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPost("users/students/{userId:guid}/gamification/adjust")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> AdjustGamification(Guid userId, [FromBody] GamificationAdjustmentRequest dto)
    {
        var result = await _mediator.Send(new AdjustGamificationPointsCommand(userId, dto.Points, dto.Reason, GetUserId()));
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPost("users/students/{userId:guid}/balance/adjust")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> AdjustBalance(Guid userId, [FromBody] BalanceAdjustmentRequest dto)
    {
        var result = await _mediator.Send(new AdjustBalanceCommand(userId, dto.Amount, dto.Reason, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/students/{userId:guid}/packages/{accessGrantId:guid}/cancel")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> CancelPackage(Guid userId, Guid accessGrantId, [FromBody] CancelPackageRequest dto)
    {
        var result = await _mediator.Send(new CancelPackageGrantCommand(accessGrantId, dto.RefundBalance, GetUserId(), dto.Reason));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Content ---
    [HttpGet("packages/list")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetPackagesList()
        => Ok(await _mediator.Send(new GetAdminPackagesListQuery(GetUserId())));

    [HttpPost("packages")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CreatePackage(CreatePackageCommand command)
    {
        var result = await _mediator.Send(command with { CurrentUserId = GetUserId() });
        return result.Success ? CreatedAtAction(nameof(CreatePackage), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpGet("packages/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetPackageById(Guid id)
        => Ok(await _mediator.Send(new NaderGorge.Application.Features.Content.Queries.GetPackageByIdQuery(id, GetUserId())));

    [HttpGet("packages/{id:guid}/stats")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetPackageStats(Guid id)
        => Ok(await _mediator.Send(new GetPackageStatsQuery(id)));

    // --- Content Subscribers ---
    [HttpGet("packages/{id:guid}/subscribers")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetPackageSubscribers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _mediator.Send(new GetContentSubscribersQuery("package", id, page, pageSize, search)));

    [HttpGet("packages/{id:guid}/subscribers/export")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> ExportPackageSubscribers(Guid id, [FromQuery] string? search = null)
    {
        var bytes = await _mediator.Send(new ExportContentSubscribersQuery("package", id, search));
        return File(bytes, "text/csv", $"subscribers_package_{id:N}_{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpGet("terms/{id:guid}/subscribers")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetTermSubscribers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _mediator.Send(new GetContentSubscribersQuery("term", id, page, pageSize, search)));

    [HttpGet("terms/{id:guid}/subscribers/export")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> ExportTermSubscribers(Guid id, [FromQuery] string? search = null)
    {
        var bytes = await _mediator.Send(new ExportContentSubscribersQuery("term", id, search));
        return File(bytes, "text/csv", $"subscribers_term_{id:N}_{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpGet("sections/{id:guid}/subscribers")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetSectionSubscribers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _mediator.Send(new GetContentSubscribersQuery("section", id, page, pageSize, search)));

    [HttpGet("sections/{id:guid}/subscribers/export")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> ExportSectionSubscribers(Guid id, [FromQuery] string? search = null)
    {
        var bytes = await _mediator.Send(new ExportContentSubscribersQuery("section", id, search));
        return File(bytes, "text/csv", $"subscribers_section_{id:N}_{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpPut("packages/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] UpdatePackageDto dto)
    {
        var result = await _mediator.Send(new UpdatePackageCommand(id, dto.Name, dto.Description, dto.Price, dto.IsActive));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("content/{contentType}/{id:guid}/image")]
    [HasPermission("content.manage")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadContentImage(
        string contentType,
        Guid id,
        IFormFile image,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ContentImageType>(contentType, true, out var parsedContentType))
        {
            return BadRequest(ApiResponse.Fail("Unsupported content image type"));
        }

        if (image.Length == 0 || image.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse.Fail("Image must be between 1 byte and 10 MB"));
        }

        if (!image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse.Fail("Uploaded file must be an image"));
        }

        await using var imageStream = image.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);

        try
        {
            var result = await _mediator.Send(
                new UploadContentImageCommand(id, parsedContentType, memoryStream.ToArray()),
                cancellationToken);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (UnknownImageFormatException)
        {
            return BadRequest(ApiResponse.Fail("Uploaded file is not a supported image"));
        }
        catch (InvalidImageContentException)
        {
            return BadRequest(ApiResponse.Fail("Uploaded image is invalid or too large"));
        }
    }

    [HttpPost("questions/image")]
    [HasPermission("exams.manage")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadQuestionImage(IFormFile image, CancellationToken cancellationToken)
    {
        if (image.Length == 0 || image.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse.Fail("Image must be between 1 byte and 10 MB"));
        }

        if (!image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse.Fail("Uploaded file must be an image"));
        }

        try
        {
            await using var imageStream = image.OpenReadStream();
            var imageUrl = await _imageStorage.SaveAsWebpAsync(imageStream, "questions", cancellationToken);
            return Ok(ApiResponse<string>.Ok(imageUrl, "Question image uploaded successfully"));
        }
        catch (UnknownImageFormatException)
        {
            return BadRequest(ApiResponse.Fail("Uploaded file is not a supported image"));
        }
        catch (InvalidImageContentException)
        {
            return BadRequest(ApiResponse.Fail("Uploaded image is invalid or too large"));
        }
    }

    [HttpGet("packages/{id:guid}/code-profile")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetPackageCodeProfile(Guid id)
    {
        var result = await _mediator.Send(new GetPackageCodeProfileQuery(id, GetUserId()));
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("packages/{id:guid}/code-profile")]
    [HasPermission("content.manage")]
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
    [HasPermission("content.manage")]
    public async Task<IActionResult> ResetPackageCodeProfile(Guid id)
    {
        var result = await _mediator.Send(new ResetPackageCodeProfileCommand(id, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("terms")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CreateTerm(CreateTermCommand command)
    {
        var result = await _mediator.Send(command with { CurrentUserId = GetUserId() });
        return result.Success ? CreatedAtAction(nameof(CreateTerm), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpGet("terms/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetTermById(Guid id)
        => Ok(await _mediator.Send(new NaderGorge.Application.Features.Content.Queries.GetTermByIdQuery(id)));

    [HttpGet("terms/{id:guid}/stats")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetTermStats(Guid id)
        => Ok(await _mediator.Send(new GetTermStatsQuery(id)));

    [HttpPut("terms/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> UpdateTerm(Guid id, [FromBody] UpdateTermDto dto)
    {
        var result = await _mediator.Send(new UpdateTermCommand(id, dto.Title, dto.Order, dto.Price, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("terms/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> DeleteTerm(Guid id)
    {
        var result = await _mediator.Send(new DeleteTermCommand(id, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("sections/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetSectionById(Guid id)
        => Ok(await _mediator.Send(new NaderGorge.Application.Features.Content.Queries.GetSectionByIdQuery(id)));

    [HttpGet("sections/{id:guid}/stats")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetSectionStats(Guid id)
        => Ok(await _mediator.Send(new GetSectionStatsQuery(id)));

    [HttpPost("sections")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CreateSection(CreateSectionCommand command)
    {
        var result = await _mediator.Send(command with { CurrentUserId = GetUserId() });
        return result.Success ? CreatedAtAction(nameof(CreateSection), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPut("sections/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> UpdateSection(Guid id, [FromBody] UpdateSectionDto dto)
    {
        var result = await _mediator.Send(new UpdateSectionCommand(id, dto.Title, dto.Order, dto.Price, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("lessons")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CreateLesson(CreateLessonCommand command)
    {
        var result = await _mediator.Send(command with { CurrentUserId = GetUserId() });
        return result.Success ? CreatedAtAction(nameof(CreateLesson), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPut("lessons/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> UpdateLesson(Guid id, [FromBody] UpdateLessonDto dto)
    {
        var result = await _mediator.Send(new UpdateLessonCommand(id, dto.Title, dto.Summary, dto.Order, dto.Price, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("lessons/{lessonId:guid}/cockpit")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetLessonCockpit(Guid lessonId)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Content.Queries.GetLessonCockpitQuery(lessonId));
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("videos")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CreateVideo(CreateVideoCommand command)
    {
        var result = await _mediator.Send(command with { CurrentUserId = GetUserId() });
        return result.Success ? CreatedAtAction(nameof(CreateVideo), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPut("videos/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> UpdateVideo(Guid id, [FromBody] UpdateVideoRequest dto)
    {
        var result = await _mediator.Send(new UpdateVideoCommand(id, dto.Title, dto.Provider, dto.UrlOrEmbedCode, dto.Order, dto.Limit, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("videos/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> DeleteVideo(Guid id)
    {
        var result = await _mediator.Send(new DeleteVideoCommand(id, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("videos/{id:guid}/toggle-active")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> ToggleVideoActive(Guid id)
    {
        var result = await _mediator.Send(new ToggleVideoActiveCommand(id, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("videos/{videoId:guid}/analyze-ai")]
    [HasPermission("content.manage")]
    [EnableRateLimiting("ai-analysis")]
    public async Task<IActionResult> RequestAIAnalysis(Guid videoId)
    {
        var result = await _mediator.Send(new AnalyzeVideoAICommand(videoId, GetUserId()));
        return result.Success ? Accepted(result) : BadRequest(result);
    }

    [HttpPost("videos/{videoId:guid}/cancel-ai")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CancelAIAnalysis(Guid videoId)
    {
        var result = await _mediator.Send(new CancelAnalyzeVideoAICommand(videoId, GetUserId()));
        return Ok(new { Success = result });
    }

    [HttpPost("videos/{videoId:guid}/cancel-mindmap")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CancelMindmapGeneration(Guid videoId)
    {
        var result = await _mediator.Send(new CancelAnalyzeVideoAICommand(videoId, GetUserId(), IsMindmapOnly: true));
        return Ok(new { Success = result });
    }

    [HttpPost("videos/{videoId:guid}/generate-mindmaps")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> RequestMindmapGeneration(Guid videoId)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.MindmapOps.GenerateChapterMindmapsCommand(videoId));
        return result.Success ? Accepted(result) : BadRequest(result);
    }

    [HttpPost("chapters/{chapterId:guid}/regenerate-mindmap")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> RegenerateChapterMindmap(Guid chapterId)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.MindmapOps.RegenerateChapterMindmapCommand(chapterId));
        return result.Success ? Accepted(result) : BadRequest(result);
    }

    [HttpPost("resources")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CreateResource(CreateLessonResourceCommand command)
    {
        var result = await _mediator.Send(command with { CurrentUserId = GetUserId() });
        return result.Success ? CreatedAtAction(nameof(CreateResource), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("resources/upload")]
    [HasPermission("content.manage")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadResourceFile(
        IFormFile file,
        [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Fail("No file uploaded"));
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse.Fail("File size must not exceed 10 MB"));
        }

        var allowedMimes = new[]
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/zip",
            "application/x-zip-compressed"
        };

        var isAllowed = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                         allowedMimes.Any(mime => string.Equals(mime, file.ContentType, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            return BadRequest(ApiResponse.Fail("Unsupported file type. Allowed types: Images, PDFs, Word/Excel documents, and ZIP files."));
        }

        var uploadsFolder = Path.Combine(environment.WebRootPath, "uploads", "resources");
        Directory.CreateDirectory(uploadsFolder);

        var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var physicalPath = Path.Combine(uploadsFolder, safeFileName);

        await using (var fileStream = new FileStream(physicalPath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream, cancellationToken);
        }

        var relativeUrl = $"/uploads/resources/{safeFileName}";
        return Ok(ApiResponse<object>.Ok(new { Url = relativeUrl }));
    }

    [HttpPost("teacher-photos/upload")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> UploadTeacherPhoto([FromBody] UploadTeacherPhotoRequest dto)
    {
        var result = await _mediator.Send(new UploadTeacherPhotoCommand(dto.TeacherId, dto.Base64Image, dto.FileName));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("teachers/upload-profile-image")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> UploadTeacherProfileImage([FromBody] UploadTeacherProfileImageRequest dto)
    {
        var result = await _mediator.Send(new UploadTeacherProfileImageCommand(dto.TeacherId, dto.Base64Image, dto.FileName));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("content/lessons/{lessonId:guid}/homework")]
    [HasPermission("content.manage")]
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
            dto.Questions,
            GetUserId());

        var result = await _mediator.Send(cmd);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("lessons/{lessonId:guid}/exam")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> LinkExam(Guid lessonId, [FromBody] LinkLessonExamRequest dto)
    {
        var result = await _mediator.Send(new LinkLessonExamCommand(lessonId, dto.ExamId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("videos/{videoId:guid}/exam")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> LinkVideoExam(Guid videoId, [FromBody] LinkLessonExamRequest dto)
    {
        var result = await _mediator.Send(new LinkVideoExamCommand(videoId, dto.ExamId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("videos/{videoId:guid}/exams/{examId:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> UnlinkVideoExam(Guid videoId, Guid examId)
    {
        var result = await _mediator.Send(new UnlinkVideoExamCommand(videoId, examId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("exams/inline")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> CreateInlineExam([FromBody] CreateInlineExamCommand command)
    {
        command.CurrentUserId = GetUserId();
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("exams/{examId:guid}/questions")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> AddQuestionsToExam(Guid examId, [FromBody] AddQuestionsToExamRequest dto)
    {
        var result = await _mediator.Send(new AddQuestionsToExamCommand { ExamId = examId, Questions = dto.Questions, CurrentUserId = GetUserId() });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("exams/{examId:guid}/dashboard")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> GetExamDashboard(Guid examId)
    {
        var result = await _mediator.Send(new GetExamDashboardQuery(examId));
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("homework/{homeworkId:guid}/dashboard")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetHomeworkDashboard(Guid homeworkId)
    {
        var result = await _mediator.Send(new GetHomeworkDashboardQuery(homeworkId));
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("exams/{examId:guid}/questions/{questionId:guid}")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> DeleteExamQuestion(Guid examId, Guid questionId)
    {
        var result = await _mediator.Send(new DeleteExamQuestionCommand(examId, questionId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("exams/{examId:guid}/questions/{questionId:guid}")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> UpdateExamQuestion(Guid examId, Guid questionId, [FromBody] UpdateExamQuestionCommand command)
    {
        command.ExamId = examId;
        command.ExamQuestionId = questionId;
        command.CurrentUserId = GetUserId();
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Questions ---
    [HttpGet("questions")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> ListQuestions([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _mediator.Send(new ListQuestionsQuery(page, pageSize, search, GetUserId())));

    [HttpPost("questions")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> CreateQuestion(CreateQuestionCommand command)
    {
        var result = await _mediator.Send(command with { CurrentUserId = GetUserId() });
        return result.Success ? CreatedAtAction(nameof(CreateQuestion), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("questions/{id:guid}/audio")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> UploadQuestionAudio(Guid id, [FromForm] Microsoft.AspNetCore.Http.IFormFile audio)
    {
        if (audio == null || audio.Length == 0) return BadRequest(new { Success = false, Message = "No file uploaded" });

        var contentType = audio.ContentType?.ToLowerInvariant() ?? "";
        var extension = Path.GetExtension(audio.FileName)?.ToLowerInvariant() ?? "";

        var allowedExtensions = new[] { ".mp3", ".wav", ".m4a", ".webm", ".ogg", ".aac", ".amr", ".flac" };
        var isAudioExtension = allowedExtensions.Contains(extension);
        var isAudioMime = contentType.StartsWith("audio/");

        if (!isAudioMime || !isAudioExtension)
        {
            return BadRequest(ApiResponse.Fail("عذراً، يجب اختيار ملف صوتي فقط."));
        }

        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());

        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.UploadQuestionAudioCommand(id, base64, audio.FileName, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("essays/{essaySubmissionId:guid}/grade")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> GradeEssay(Guid essaySubmissionId, [FromBody] NaderGorge.Application.Features.Admin.Commands.GradeEssayCommand command)
    {
        if (essaySubmissionId != command.EssaySubmissionId) return BadRequest();
        var result = await _mediator.Send(command with { CurrentUserId = GetUserId() });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("essays/pending")]
    [HasPermission("exams.manage")]
    public async Task<IActionResult> GetPendingEssays([FromServices] NaderGorge.Domain.Interfaces.IAppDbContext db)
    {
        Guid? teacherId = null;
        var userId = GetUserId();
        var user = db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.TeacherProfile)
            .FirstOrDefault(u => u.Id == userId);

        if (user != null && user.UserRoles.Any(ur => ur.Role.Type == NaderGorge.Domain.Enums.RoleType.Teacher))
        {
            teacherId = user.TeacherProfile?.Id;
        }

        var aiQuery = db.EssaySubmissions.AsQueryable();
        if (teacherId.HasValue)
        {
            aiQuery = aiQuery.Where(e => e.Question.CreatedByTeacherId == teacherId.Value);
        }

        var aiScored = aiQuery
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

        var listQuery = db.EssaySubmissions.AsQueryable();
        if (teacherId.HasValue)
        {
            listQuery = listQuery.Where(e => e.Question.CreatedByTeacherId == teacherId.Value);
        }

        var list = listQuery
            .Where(e => e.Status != NaderGorge.Domain.Entities.EssaySubmissionStatus.TeacherGraded)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.StudentId, e.QuestionId, e.AnswerText, e.AudioUrl, e.AiInitialScore, e.AiFeedback, e.Status })
            .ToList();
        return Ok(NaderGorge.Application.Common.ApiResponse<object>.Ok(list));
    }

    // --- Overrides ---
    [HttpPost("overrides/reset-watch")]
    [HasPermission("watch_requests.manage")]
    public async Task<IActionResult> ResetWatchLimit([FromBody] ResetWatchRequest dto)
    {
        var result = await _mediator.Send(new ResetWatchLimitCommand(dto.LessonVideoId, dto.StudentId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("overrides/set-watch-count")]
    [HasPermission("watch_requests.manage")]
    public async Task<IActionResult> SetWatchCount([FromBody] SetWatchCountRequest dto)
    {
        var result = await _mediator.Send(new SetWatchCountCommand(dto.LessonVideoId, dto.StudentId, dto.NewWatchCount, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Codes ---
    [HttpPost("codes/bulk-generate")]
    [HasPermission("codes.manage")]
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
    [HasPermission("watch_requests.manage")]
    public async Task<IActionResult> GetWatchRequests(CancellationToken ct)
    {
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Queries.GetWatchRequestsQuery(), ct);
        return Ok(result);
    }

    [HttpPost("watch-requests/{id}/approve")]
    [HasPermission("watch_requests.manage")]
    public async Task<IActionResult> ApproveWatchRequest(Guid id, [FromBody] ApproveWatchRequestBody? request, CancellationToken ct)
    {
        var reason = request?.Reason;
        var addedViews = request?.AddedViews ?? 1;
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.ApproveWatchRequestCommand(id, GetUserId(), reason, addedViews), ct);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    [HttpPost("watch-requests/{id}/reject")]
    [HasPermission("watch_requests.manage")]
    public async Task<IActionResult> RejectWatchRequest(Guid id, [FromBody] RejectWatchRequestBody? request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(NaderGorge.Application.Common.ApiResponse<bool>.Fail("Rejection reason is required.", new List<string> { "REJECTION_REASON_REQUIRED" }));
        }
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.RejectWatchRequestCommand(id, request.Reason.Trim()), ct);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }
    [HttpGet("settings")]
    [HasPermission("settings.manage")]
    public async Task<IActionResult> GetPlatformSettings()
    {
        var response = await _mediator.Send(new NaderGorge.Application.Features.Admin.Queries.GetPlatformSettingsQuery());
        return Ok(response);
    }

    [HttpPut("settings")]
    [HasPermission("settings.manage")]
    public async Task<IActionResult> UpdatePlatformSettings([FromBody] UpdateSettingsRequest req)
    {
        var response = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.UpdatePlatformSettingsCommand(req.Settings));
        return Ok(response);
    }

    [HttpPost("bunny/uploads/tus")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CreateBunnyTusUpload([FromBody] CreateBunnyTusUploadRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateBunnyTusUploadCommand(
            req.TeacherId,
            req.PackageId,
            req.LessonId,
            req.Title,
            req.Order,
            req.MaxWatchCount,
            req.FileName,
            req.FileSizeBytes,
            GetUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("bunny/uploads/{assetId:guid}/complete")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CompleteBunnyUpload(Guid assetId, CancellationToken ct)
    {
        var response = await _mediator.Send(new CompleteBunnyUploadCommand(assetId, GetUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("bunny/uploads/fetch")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> FetchBunnyVideo([FromBody] FetchBunnyVideoRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(new FetchBunnyVideoCommand(
            req.TeacherId,
            req.PackageId,
            req.LessonId,
            req.Title,
            req.Order,
            req.MaxWatchCount,
            req.SourceUrl,
            GetUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("bunny/videos/{assetId:guid}/refresh-status")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> RefreshBunnyVideoStatus(Guid assetId, CancellationToken ct)
    {
        var response = await _mediator.Send(new RefreshBunnyVideoStatusCommand(assetId, GetUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("bunny/usage/sync")]
    [HasPermission("settings.manage")]
    public async Task<IActionResult> SyncBunnyUsage([FromBody] SyncBunnyUsageRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(new SyncBunnyUsageCommand(
            req.PeriodStart,
            req.PeriodEnd,
            req.TeacherId,
            req.PackageId,
            req.ForceRefresh,
            GetUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("bunny/reports/costs")]
    [HasPermission("settings.manage")]
    public async Task<IActionResult> GetBunnyCostReport([FromQuery] string month, [FromQuery] Guid? teacherId, [FromQuery] Guid? packageId, CancellationToken ct)
    {
        if (!DateTime.TryParse($"{month}-01", out var parsedMonth))
        {
            return BadRequest(ApiResponse.Fail("Month must be in yyyy-MM format."));
        }

        var periodStart = DateTime.SpecifyKind(parsedMonth.Date, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);
        var response = await _mediator.Send(new GetBunnyCostReportQuery(periodStart, periodEnd, teacherId, packageId), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    // --- Roles CRUD ---
    [HttpGet("roles")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> ListRoles(CancellationToken ct)
        => Ok(await _mediator.Send(new ListRolesQuery(), ct));

    [HttpPost("roles")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateRoleCommand(dto.Name, dto.Permissions), ct);
        return result.Success ? StatusCode(201, result) : BadRequest(result);
    }

    [HttpPut("roles/{id:guid}")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateRoleCommand(id, dto.Name, dto.Permissions), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("roles/{id:guid}")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteRoleCommand(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Subjects ---
    [HttpGet("subjects")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetSubjects([FromQuery] Guid? teacherId)
        => Ok(await _mediator.Send(new GetSubjectsQuery(teacherId)));

    [HttpGet("subjects/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetSubjectById(Guid id)
        => Ok(await _mediator.Send(new GetSubjectByIdQuery(id)));

    [HttpPost("subjects")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateSubject), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPut("subjects/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] UpdateSubjectRequest dto)
    {
        var result = await _mediator.Send(new UpdateSubjectCommand(id, dto.Name, dto.Description));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("subjects/{id:guid}")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> DeleteSubject(Guid id)
    {
        var result = await _mediator.Send(new DeleteSubjectCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Teachers ---
    [HttpGet("teachers")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetTeachers()
        => Ok(await _mediator.Send(new GetTeachersQuery()));

    [HttpGet("teachers/{id:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetTeacherById(Guid id)
        => Ok(await _mediator.Send(new GetTeacherByIdQuery(id)));

    [HttpGet("teachers/{id:guid}/active-photo")]
    [HasPermission("content.manage")]
    public async Task<IActionResult> GetActiveTeacherPhoto(Guid id)
        => Ok(await _mediator.Send(new GetActiveTeacherPhotoQuery(id)));

    [HttpPost("teachers")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> CreateTeacher([FromBody] CreateTeacherProfileCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateTeacher), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPut("teachers/{id:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> UpdateTeacher(Guid id, [FromBody] UpdateTeacherProfileRequestDto dto)
    {
        var result = await _mediator.Send(new UpdateTeacherProfileCommand(
            id, dto.Bio, dto.Specialization, dto.CommissionRate, dto.ProfileImageUrl, dto.ContactInfo, dto.SubjectIds,
            dto.AssistantPhoneNumbers, dto.FacebookUrl, dto.YouTubeUrl, dto.TelegramUrl));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("teachers/{id:guid}/stats")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetTeacherProfileStats(Guid id)
        => Ok(await _mediator.Send(new GetTeacherProfileStatsQuery(id)));

    [HttpGet("teachers/{id:guid}/students")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetTeacherStudents(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetTeacherStudentsQuery(id, page, pageSize)));

    [HttpGet("teachers/{id:guid}/essays")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetTeacherEssays(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetTeacherEssaysQuery(id, page, pageSize)));

    [HttpGet("teachers/{id:guid}/activations")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetTeacherActivations(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetTeacherActivationsQuery(id, page, pageSize)));

}

public record UpdateSubjectRequest(string Name, string Description);
public record UpdateTeacherProfileRequestDto(
    string Bio,
    string Specialization,
    decimal CommissionRate,
    string? ProfileImageUrl,
    string ContactInfo,
    List<Guid> SubjectIds,
    string? AssistantPhoneNumbers = null,
    string? FacebookUrl = null,
    string? YouTubeUrl = null,
    string? TelegramUrl = null);

public record CreateRoleDto(string Name, List<string> Permissions);
public record UpdateRoleDto(string Name, List<string> Permissions);

public record UpdateUserStatusRequest(string Status);
public record UpdateUserRolesRequest(string[] Roles);
public record ResetWatchRequest(Guid LessonVideoId, Guid StudentId);
public record SetWatchCountRequest(Guid LessonVideoId, Guid StudentId, int NewWatchCount);
public record RejectWatchRequestBody(string? Reason);
public record ApproveWatchRequestBody(string? Reason, int? AddedViews = null);

public record ToggleStudentStatusRequest(bool IsActive, string? Reason);
public record OverrideVideoLimitRequest(Guid VideoId, int AddedViews, string Reason);
public record GamificationAdjustmentRequest(int Points, string Reason);
public record BalanceAdjustmentRequest(decimal Amount, string Reason);
public record CancelPackageRequest(bool RefundBalance, string? Reason = null);
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
public record CreateBunnyTusUploadRequest(Guid? TeacherId, Guid? PackageId, Guid LessonId, string Title, int Order, int MaxWatchCount, string? FileName, long? FileSizeBytes);
public record FetchBunnyVideoRequest(Guid? TeacherId, Guid? PackageId, Guid LessonId, string Title, int Order, int MaxWatchCount, string SourceUrl);
public record SyncBunnyUsageRequest(DateTime PeriodStart, DateTime PeriodEnd, Guid? TeacherId, Guid? PackageId, bool ForceRefresh);
public record UpdateVideoRequest(string Title, string Provider, string UrlOrEmbedCode, int Order, int Limit);
public record AttachHomeworkRequest(string Title, string Instructions, bool IsMandatory, bool IsRandomized, int RequiredPointsToPass, decimal TotalScore, List<AttachHomeworkQuestionDto> Questions);
public record LinkLessonExamRequest(Guid? ExamId);
public record UpdateTermDto(string Title, int Order, decimal Price);
public record UpdateSectionDto(string Title, int Order, decimal Price);
public record UpdateLessonDto(string Title, string Summary, int Order, decimal Price);
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
public record UploadTeacherProfileImageRequest(Guid TeacherId, string Base64Image, string FileName);
public record UpdateSettingsRequest(Dictionary<string, string> Settings);
public record UpdateStudentProfileRequest(
    string? FullName, string? Phone, string? ParentPhone, string? SecondaryPhone, string? MotherPhone,
    string? Governorate, string? District, string? Address, string? SchoolName, string? DateOfBirth,
    string? Gender, string? EducationStage, string? GradeLevel, string? StudyTrack, string? SchoolType,
    bool? IsFatherAlive, bool? IsMotherAlive
);
public record AdminResetPasswordRequest(string NewPassword);
public record AddStudentNoteRequest(string Content, bool IsPinned);
public record AdminCreateUserRequest(
    string FullName,
    string PhoneNumber,
    string Password,
    string Role,
    List<Guid>? PackageIds
);
