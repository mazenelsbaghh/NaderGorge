using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Teacher;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Admin.Content.Queries;
using NaderGorge.Application.Features.Admin.Sales;
using NaderGorge.Application.Features.Community.Commands;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize(Roles = "Teacher")]
public class TeacherController : ControllerBase
{
    private static class TeacherStaffPermissions
    {
        public const string Dashboard = "dashboard";
        public const string Activity = "activity";
        public const string Students = "students";
        public const string Content = "content";
        public const string Codes = "codes";
        public const string PublicExams = "publicExams";
        public const string Community = "community";
        public const string Essays = "essays";
        public const string Finance = "finance";
        public const string Profile = "profile";
        public const string Chat = "chat";
        public const string Comments = "comments";
        public const string Reports = "reports";

        public static readonly string[] All =
        [
            Dashboard,
            Activity,
            Students,
            Content,
            Codes,
            PublicExams,
            Community,
            Essays,
            Finance,
            Profile,
            Chat,
            Comments,
            Reports
        ];
    }

    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public TeacherController(IMediator mediator, IAppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    private Guid GetUserId() => User.RequireUserId();

    private sealed record TeacherContext(Guid TeacherId, Guid TeacherUserId, bool IsOwner, IReadOnlySet<string> PermissionKeys);

    private async Task<TeacherContext?> ResolveTeacherContextAsync(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var teacher = await _db.TeacherProfiles.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (teacher != null) return new TeacherContext(teacher.Id, teacher.UserId, true, TeacherStaffPermissions.All.ToHashSet(StringComparer.OrdinalIgnoreCase));

        var membership = await _db.TeacherStaffMembers
            .AsNoTracking()
            .Include(m => m.Teacher)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsActive && m.User.IsActive, ct);

        return membership == null
            ? null
            : new TeacherContext(membership.TeacherId, membership.Teacher.UserId, false, ParsePermissionKeys(membership.PermissionKeys));
    }

    private static IReadOnlySet<string> ParsePermissionKeys(string? permissionKeys) =>
        (permissionKeys ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(key => TeacherStaffPermissions.All.Contains(key, StringComparer.OrdinalIgnoreCase))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string NormalizePermissionKeys(IEnumerable<string>? permissionKeys) =>
        string.Join(',', (permissionKeys ?? [])
            .Where(key => TeacherStaffPermissions.All.Contains(key, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase));

    private static bool Can(TeacherContext context, string permission) =>
        context.IsOwner || context.PermissionKeys.Contains(permission);

    private static bool CanAny(TeacherContext context, params string[] permissions) =>
        context.IsOwner || permissions.Any(context.PermissionKeys.Contains);

    private static bool MissingPermission([NotNullWhen(false)] TeacherContext? context, string permission) =>
        context == null || !Can(context, permission);

    private static bool MissingAnyPermission([NotNullWhen(false)] TeacherContext? context, params string[] permissions) =>
        context == null || !CanAny(context, permissions);

    [HttpGet("context")]
    public async Task<IActionResult> GetContext(CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (context == null) return Forbid();
        return Ok(NaderGorge.Application.Common.ApiResponse<TeacherWorkspaceContextDto>.Ok(
            new TeacherWorkspaceContextDto(context.IsOwner, context.PermissionKeys.Order(StringComparer.OrdinalIgnoreCase).ToArray(), TeacherStaffPermissions.All)));
    }

    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Dashboard)) return Forbid();
        var result = await _mediator.Send(new GetTeacherDashboardStatsQuery(context.TeacherUserId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("content/summary")]
    public async Task<IActionResult> GetContentSummary([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Content)) return Forbid();
        var result = await _mediator.Send(new GetContentSummaryQuery(context.TeacherUserId, fromUtc, toUtc));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("content/{contentType}/{contentId:guid}/subscribers")]
    public async Task<IActionResult> GetContentSubscribers(string contentType, Guid contentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Content)) return Forbid();
        if (!await OwnsContentAsync(context.TeacherId, contentType, contentId, ct)) return NotFound();

        return Ok(await _mediator.Send(new GetContentSubscribersQuery(contentType, contentId, page, pageSize, search), ct));
    }

    [HttpGet("content/{contentType}/{contentId:guid}/subscribers/export")]
    public async Task<IActionResult> ExportContentSubscribers(string contentType, Guid contentId, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Content)) return Forbid();
        if (!await OwnsContentAsync(context.TeacherId, contentType, contentId, ct)) return NotFound();

        var bytes = await _mediator.Send(new ExportContentSubscribersQuery(contentType, contentId, search), ct);
        return File(bytes, "text/csv", $"subscribers_{contentType}_{contentId:N}_{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    private Task<bool> OwnsContentAsync(Guid teacherId, string contentType, Guid contentId, CancellationToken ct) =>
        contentType.ToLowerInvariant() switch
        {
            "package" => _db.Packages.AnyAsync(package => package.Id == contentId && package.TeacherId == teacherId, ct),
            "term" => _db.Terms.AnyAsync(term => term.Id == contentId && term.Package.TeacherId == teacherId, ct),
            "section" => _db.ContentSections.AnyAsync(section => section.Id == contentId && section.Term.Package.TeacherId == teacherId, ct),
            "lesson" => _db.Lessons.AnyAsync(lesson => lesson.Id == contentId && lesson.ContentSection.Term.Package.TeacherId == teacherId, ct),
            _ => Task.FromResult(false)
        };

    [HttpGet("students")]
    public async Task<IActionResult> GetStudents()
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Students)) return Forbid();
        var result = await _mediator.Send(new GetTeacherStudentsQuery(context.TeacherUserId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("essays")]
    public async Task<IActionResult> GetEssays()
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Essays)) return Forbid();
        var result = await _mediator.Send(new GetPendingTeacherEssaysQuery(context.TeacherUserId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("essays/{id}/grade")]
    public async Task<IActionResult> GradeEssay(Guid id, [FromBody] GradeEssayRequestDto dto)
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Essays)) return Forbid();
        var result = await _mediator.Send(new GradeEssayCommand(id, dto.Score, dto.Feedback, context.TeacherUserId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity()
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Activity)) return Forbid();
        var result = await _mediator.Send(new GetTeacherActivityQuery(context.TeacherUserId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Profile)) return Forbid();
        var result = await _mediator.Send(new GetTeacherProfileQuery(context.TeacherUserId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] TeacherUpdateProfileRequestDto dto)
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Profile)) return Forbid();
        var result = await _mediator.Send(new NaderGorge.Application.Features.Teacher.UpdateTeacherProfileCommand(
            context.TeacherUserId,
            dto.Bio,
            dto.Specialization,
            dto.ContactInfo,
            dto.ProfileImageUrl,
            dto.AssistantPhoneNumbers,
            dto.FacebookUrl,
            dto.YouTubeUrl,
            dto.TelegramUrl
        ));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("profile/upload-image")]
    public async Task<IActionResult> UploadProfileImage([FromBody] UploadImageRequestDto dto)
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Profile)) return Forbid();
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps.UploadTeacherProfileImageCommand(context.TeacherUserId, dto.Base64Image, dto.FileName));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("profile/upload-ai-photo")]
    public async Task<IActionResult> UploadAiPhoto([FromBody] UploadImageRequestDto dto)
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Profile)) return Forbid();
        var result = await _mediator.Send(new NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps.UploadTeacherPhotoCommand(context.TeacherUserId, dto.Base64Image, dto.FileName));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("profile/active-photo")]
    public async Task<IActionResult> GetMyActiveTeacherPhoto()
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Profile)) return Forbid();
        return Ok(await _mediator.Send(new NaderGorge.Application.Features.Admin.Queries.GetActiveTeacherPhotoQuery(context.TeacherUserId)));
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingAnyPermission(context, TeacherStaffPermissions.Content, TeacherStaffPermissions.PublicExams)) return Forbid();
        return Ok(await _mediator.Send(new GetSubjectsQuery(context.TeacherUserId)));
    }

    [HttpGet("staff")]
    public async Task<IActionResult> ListStaff(CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (context == null || !context.IsOwner) return Forbid();

        var staff = await _db.TeacherStaffMembers
            .AsNoTracking()
            .Include(member => member.User)
            .Where(member => member.TeacherId == context.TeacherId)
            .OrderByDescending(member => member.CreatedAt)
            .Select(member => new TeacherStaffMemberDto(
                member.Id,
                member.UserId,
                member.User.FullName,
                member.User.PhoneNumber,
                member.IsActive && member.User.IsActive,
                member.CreatedAt,
                member.Notes,
                ParsePermissionKeys(member.PermissionKeys).Order(StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToListAsync(ct);

        return Ok(NaderGorge.Application.Common.ApiResponse<List<TeacherStaffMemberDto>>.Ok(staff));
    }

    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff([FromBody] CreateTeacherStaffRequest request, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (context == null || !context.IsOwner) return Forbid();

        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(NaderGorge.Application.Common.ApiResponse<object>.Fail("اسم الاستاف ورقم الهاتف مطلوبين."));
        if (request.FullName.Trim().Length > 200)
            return BadRequest(NaderGorge.Application.Common.ApiResponse<object>.Fail("اسم الاستاف يجب ألا يتجاوز 200 حرف."));
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8 || !request.Password.Any(char.IsLetter) || !request.Password.Any(char.IsDigit))
            return BadRequest(NaderGorge.Application.Common.ApiResponse<object>.Fail("كلمة السر يجب أن تكون 8 أحرف على الأقل وتحتوي على حرف ورقم."));

        var phone = request.PhoneNumber.Trim();
        if (phone.Length is < 10 or > 20 || phone.Skip(phone.StartsWith('+') ? 1 : 0).Any(character => !char.IsDigit(character)))
            return BadRequest(NaderGorge.Application.Common.ApiResponse<object>.Fail("رقم الهاتف غير صحيح."));
        if (request.Notes?.Trim().Length > 500)
            return BadRequest(NaderGorge.Application.Common.ApiResponse<object>.Fail("الملاحظة يجب ألا تتجاوز 500 حرف."));
        if (await _db.Users.AnyAsync(user => user.PhoneNumber == phone, ct))
            return BadRequest(NaderGorge.Application.Common.ApiResponse<object>.Fail("رقم الهاتف مسجل بالفعل."));

        var teacherRole = await _db.Roles.FirstOrDefaultAsync(role => role.Type == RoleType.Teacher, ct);
        if (teacherRole == null)
            return BadRequest(NaderGorge.Application.Common.ApiResponse<object>.Fail("دور المدرس غير موجود في النظام."));

        var user = new NaderGorge.Domain.Entities.User
        {
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            IsProfileComplete = true
        };

        _db.Users.Add(user);
        _db.UserRoles.Add(new NaderGorge.Domain.Entities.UserRole { User = user, Role = teacherRole });
        var staffMember = new NaderGorge.Domain.Entities.TeacherStaffMember
        {
            TeacherId = context.TeacherId,
            User = user,
            CreatedByTeacherUserId = context.TeacherUserId,
            IsActive = true,
            Notes = request.Notes?.Trim(),
            PermissionKeys = NormalizePermissionKeys(request.PermissionKeys)
        };
        _db.TeacherStaffMembers.Add(staffMember);

        await _db.SaveChangesAsync(ct);

        return Ok(NaderGorge.Application.Common.ApiResponse<TeacherStaffMemberDto>.Ok(
            new TeacherStaffMemberDto(staffMember.Id, user.Id, user.FullName, user.PhoneNumber, true, staffMember.CreatedAt, request.Notes?.Trim(), ParsePermissionKeys(staffMember.PermissionKeys).ToArray()),
            "تم إضافة الاستاف بنجاح."));
    }

    [HttpPatch("staff/{staffMemberId:guid}/status")]
    public async Task<IActionResult> SetStaffStatus(Guid staffMemberId, [FromBody] UpdateTeacherStaffStatusRequest request, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (context == null || !context.IsOwner) return Forbid();

        var staff = await _db.TeacherStaffMembers
            .Include(member => member.User)
            .FirstOrDefaultAsync(member => member.Id == staffMemberId && member.TeacherId == context.TeacherId, ct);
        if (staff == null) return NotFound();

        var statusChanged = staff.IsActive != request.IsActive;
        staff.IsActive = request.IsActive;
        staff.UpdatedAt = DateTime.UtcNow;
        if (statusChanged)
        {
            staff.User.SecurityStampVersion += 1;
            var activeRefreshTokens = await _db.RefreshTokens
                .Where(token => token.UserId == staff.UserId && !token.IsRevoked)
                .ToListAsync(ct);
            foreach (var refreshToken in activeRefreshTokens)
                refreshToken.IsRevoked = true;
        }
        await _db.SaveChangesAsync(ct);

        return Ok(NaderGorge.Application.Common.ApiResponse<TeacherStaffMemberDto>.Ok(
            new TeacherStaffMemberDto(staff.Id, staff.UserId, staff.User.FullName, staff.User.PhoneNumber, staff.IsActive, staff.CreatedAt, staff.Notes, ParsePermissionKeys(staff.PermissionKeys).ToArray())));
    }

    [HttpPatch("staff/{staffMemberId:guid}/permissions")]
    public async Task<IActionResult> SetStaffPermissions(Guid staffMemberId, [FromBody] UpdateTeacherStaffPermissionsRequest request, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (context == null || !context.IsOwner) return Forbid();

        var staff = await _db.TeacherStaffMembers
            .Include(member => member.User)
            .FirstOrDefaultAsync(member => member.Id == staffMemberId && member.TeacherId == context.TeacherId, ct);
        if (staff == null) return NotFound();

        var permissionKeys = NormalizePermissionKeys(request.PermissionKeys);
        var permissionsChanged = !string.Equals(staff.PermissionKeys, permissionKeys, StringComparison.Ordinal);
        staff.PermissionKeys = permissionKeys;
        staff.UpdatedAt = DateTime.UtcNow;
        if (permissionsChanged)
        {
            staff.User.SecurityStampVersion += 1;
            var activeRefreshTokens = await _db.RefreshTokens
                .Where(token => token.UserId == staff.UserId && !token.IsRevoked)
                .ToListAsync(ct);
            foreach (var refreshToken in activeRefreshTokens)
                refreshToken.IsRevoked = true;
        }
        await _db.SaveChangesAsync(ct);

        return Ok(NaderGorge.Application.Common.ApiResponse<TeacherStaffMemberDto>.Ok(
            new TeacherStaffMemberDto(staff.Id, staff.UserId, staff.User.FullName, staff.User.PhoneNumber, staff.IsActive && staff.User.IsActive, staff.CreatedAt, staff.Notes, ParsePermissionKeys(staff.PermissionKeys).ToArray())));
    }

    [HttpGet("public-exams")]
    public async Task<IActionResult> ListPublicExams(CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.PublicExams)) return Forbid();

        var response = await _mediator.Send(new GetPublicExamProductsQuery(PublishedOnly: false), ct);
        if (!response.Success || response.Data == null) return Ok(response);

        response.Data = response.Data.Where(exam => exam.TeacherId == context.TeacherId).ToList();
        return Ok(response);
    }

    [HttpPost("public-exams/new")]
    public async Task<IActionResult> CreatePublicExam([FromBody] CreatePublicExamRequest request, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.PublicExams)) return Forbid();
        var teacher = await _db.TeacherProfiles
            .Include(t => t.TeacherSubjects)
            .FirstOrDefaultAsync(t => t.Id == context.TeacherId, ct);
        if (teacher == null) return BadRequest("حساب المعلم غير موجود");
        if (teacher.TeacherSubjects.All(subject => subject.SubjectId != request.SubjectId))
            return BadRequest("المادة المحددة غير تابعة لهذا المدرس.");

        var teacherRequest = request with { TeacherId = context.TeacherId };
        var response = await _mediator.Send(new CreatePublicExamProductCommand(teacherRequest, GetUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("community/posts")]
    public async Task<IActionResult> GetCommunityPosts([FromQuery] string? status = null, CancellationToken ct = default)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Community)) return Forbid();

        CommunityPostStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<CommunityPostStatus>(status.Trim(), ignoreCase: true, out var nextStatus))
                return BadRequest("حالة المنشور غير صحيحة.");
            parsedStatus = nextStatus;
        }

        var query = _db.CommunityPosts
            .AsNoTracking()
            .Include(p => p.AuthorUser)
            .Include(p => p.ReviewedByUser)
            .Where(p => p.TeacherId == context.TeacherId);

        if (parsedStatus.HasValue)
            query = query.Where(p => p.Status == parsedStatus.Value);

        var posts = await query
            .OrderBy(p => p.Status == CommunityPostStatus.Pending ? 0 : 1)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new ModerationCommunityPostDto(
                p.Id,
                p.AuthorUserId,
                p.AuthorUser.FullName,
                p.Body,
                p.Status.ToString(),
                p.CreatedAt,
                p.ReviewedAt,
                p.ReviewedByUser != null ? p.ReviewedByUser.FullName : null,
                p.Comments.Count,
                p.Likes.Count))
            .ToListAsync(ct);

        return Ok(NaderGorge.Application.Common.ApiResponse<List<ModerationCommunityPostDto>>.Ok(posts));
    }

    [HttpGet("community/comments/pending")]
    public async Task<IActionResult> GetPendingCommunityComments(CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Community)) return Forbid();

        var comments = await _db.CommunityPostComments
            .AsNoTracking()
            .Include(c => c.AuthorUser)
            .Include(c => c.ReviewedByUser)
            .Where(c => c.Status == CommunityCommentStatus.Pending && c.Post.TeacherId == context.TeacherId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ModerationCommunityCommentDto(
                c.Id,
                c.PostId,
                c.AuthorUserId,
                c.AuthorUser.FullName,
                c.Body,
                c.Status.ToString(),
                c.CreatedAt,
                c.ReviewedAt,
                c.ReviewedByUser != null ? c.ReviewedByUser.FullName : null,
                c.RejectionReason))
            .ToListAsync(ct);

        return Ok(NaderGorge.Application.Common.ApiResponse<List<ModerationCommunityCommentDto>>.Ok(comments));
    }

    [HttpGet("lessons/{lessonId:guid}/comments")]
    public async Task<IActionResult> GetLessonComments(Guid lessonId, [FromQuery] string? status, CancellationToken ct)
    {
        if (!await CanModerateLessonAsync(lessonId, TeacherStaffPermissions.Comments, ct)) return Forbid();
        var response = await _mediator.Send(new GetLessonCommentsForModerationQuery(lessonId, status), ct);
        return response.Success ? Ok(response) : response.Errors?.Contains("NOT_FOUND") == true ? NotFound(response) : BadRequest(response);
    }

    [HttpPost("comments/{commentId:guid}/approve")]
    public async Task<IActionResult> ApproveLessonComment(Guid commentId, CancellationToken ct)
    {
        if (!await CanModerateLessonCommentAsync(commentId, TeacherStaffPermissions.Comments, ct)) return Forbid();
        var response = await _mediator.Send(new ApproveLessonCommentCommand(commentId, GetUserId()), ct);
        return response.Success ? Ok(response) : response.Errors?.Contains("NOT_FOUND") == true ? NotFound(response) : BadRequest(response);
    }

    [HttpPost("comments/{commentId:guid}/reject")]
    public async Task<IActionResult> RejectLessonComment(Guid commentId, CancellationToken ct)
    {
        if (!await CanModerateLessonCommentAsync(commentId, TeacherStaffPermissions.Comments, ct)) return Forbid();
        var response = await _mediator.Send(new RejectLessonCommentCommand(commentId, GetUserId()), ct);
        return response.Success ? Ok(response) : response.Errors?.Contains("NOT_FOUND") == true ? NotFound(response) : BadRequest(response);
    }

    [HttpPost("community/posts/{postId:guid}/approve")]
    public async Task<IActionResult> ApproveCommunityPost(Guid postId, CancellationToken ct)
    {
        if (!await CanModerateCommunityPost(postId, ct)) return Forbid();
        var response = await _mediator.Send(new ApproveCommunityPostCommand(postId, GetUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("community/posts/{postId:guid}/reject")]
    public async Task<IActionResult> RejectCommunityPost(Guid postId, CancellationToken ct)
    {
        if (!await CanModerateCommunityPost(postId, ct)) return Forbid();
        var response = await _mediator.Send(new RejectCommunityPostCommand(postId, GetUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("community/comments/{commentId:guid}/approve")]
    public async Task<IActionResult> ApproveCommunityComment(Guid commentId, CancellationToken ct)
    {
        if (!await CanModerateCommunityComment(commentId, ct)) return Forbid();
        var response = await _mediator.Send(new ApproveCommunityCommentCommand(commentId, GetUserId()), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("community/comments/{commentId:guid}/reject")]
    public async Task<IActionResult> RejectCommunityComment(Guid commentId, [FromBody] RejectCommunityCommentRequest request, CancellationToken ct)
    {
        if (!await CanModerateCommunityComment(commentId, ct)) return Forbid();
        var response = await _mediator.Send(new RejectCommunityCommentCommand(commentId, GetUserId(), request.Reason ?? string.Empty), ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("codes/groups")]
    public async Task<IActionResult> ListCodeGroups()
    {
        var context = await ResolveTeacherContextAsync();
        if (MissingPermission(context, TeacherStaffPermissions.Codes)) return Forbid();
        return Ok(await _mediator.Send(new ListCodeGroupsQuery(context.TeacherUserId)));
    }

    [HttpGet("codes/groups/{id:guid}/details")]
    public async Task<IActionResult> GetCodeGroupDetails(Guid id)
    {
        var group = await _db.CodeGroups.FindAsync(id);
        if (group == null) return NotFound();

        var context = await ResolveTeacherContextAsync();
        if (context == null || !Can(context, TeacherStaffPermissions.Codes) || !group.TeacherId.HasValue || group.TeacherId.Value != context.TeacherId)
        {
            return Forbid();
        }

        var result = await _mediator.Send(new GetCodeGroupCodesQuery(id));
        return result.Success ? Ok(result) : NotFound(result);
    }

    private async Task<bool> CanModerateCommunityPost(Guid postId, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        return context != null && Can(context, TeacherStaffPermissions.Community) && await _db.CommunityPosts.AnyAsync(p => p.Id == postId && p.TeacherId == context.TeacherId, ct);
    }

    private async Task<bool> CanModerateCommunityComment(Guid commentId, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        return context != null && Can(context, TeacherStaffPermissions.Community) && await _db.CommunityPostComments.AnyAsync(c => c.Id == commentId && c.Post.TeacherId == context.TeacherId, ct);
    }

    [HttpGet("comments")]
    public async Task<IActionResult> GetAllLessonComments([FromQuery] string? status, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Comments)) return Forbid();
        LessonCommentStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<LessonCommentStatus>(status, true, out var parsed)) return BadRequest();
            parsedStatus = parsed;
        }
        var query = _db.LessonComments.AsNoTracking().Where(comment => comment.Lesson.ContentSection.Term.Package.TeacherId == context.TeacherId);
        if (parsedStatus.HasValue) query = query.Where(comment => comment.Status == parsedStatus.Value);
        var comments = await query.OrderByDescending(comment => comment.CreatedAt).Select(comment => new ModerationLessonCommentDto(comment.Id, comment.LessonId, comment.Lesson.Title, comment.Lesson.ContentSection.Term.Package.Teacher.User.FullName, comment.Lesson.ContentSection.Term.Package.Name, comment.Lesson.ContentSection.Term.Title, comment.Lesson.ContentSection.Title, comment.AuthorUserId, comment.AuthorUser.FullName, comment.Body, comment.Status.ToString(), comment.CreatedAt, comment.ReviewedAt, comment.ReviewedByUser != null ? comment.ReviewedByUser.FullName : null)).ToListAsync(ct);
        return Ok(NaderGorge.Application.Common.ApiResponse<List<ModerationLessonCommentDto>>.Ok(comments));
    }

    [HttpPost("comments/{commentId:guid}/reply")]
    public async Task<IActionResult> ReplyToLessonComment(Guid commentId, [FromBody] ReplyToLessonCommentRequest request, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Comments)) return Forbid();
        var original = await _db.LessonComments.FirstOrDefaultAsync(comment => comment.Id == commentId && comment.Lesson.ContentSection.Term.Package.TeacherId == context.TeacherId, ct);
        if (original == null) return NotFound();
        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000) return BadRequest(NaderGorge.Application.Common.ApiResponse.Fail("الرد مطلوب وبحد أقصى 2000 حرف."));
        var reply = new NaderGorge.Domain.Entities.LessonComment { LessonId = original.LessonId, AuthorUserId = context.TeacherUserId, Body = body, Status = LessonCommentStatus.Approved, ReviewedAt = DateTime.UtcNow, ReviewedByUserId = context.TeacherUserId };
        _db.LessonComments.Add(reply);
        await _db.SaveChangesAsync(ct);
        var lessonContext = await _db.Lessons
            .Where(item => item.Id == reply.LessonId)
            .Select(item => new { item.Title, TeacherName = item.ContentSection.Term.Package.Teacher.User.FullName, PackageName = item.ContentSection.Term.Package.Name, TermTitle = item.ContentSection.Term.Title, SectionTitle = item.ContentSection.Title })
            .FirstAsync(ct);
        return Ok(NaderGorge.Application.Common.ApiResponse<ModerationLessonCommentDto>.Ok(new ModerationLessonCommentDto(reply.Id, reply.LessonId, lessonContext.Title, lessonContext.TeacherName, lessonContext.PackageName, lessonContext.TermTitle, lessonContext.SectionTitle, reply.AuthorUserId, "", reply.Body, reply.Status.ToString(), reply.CreatedAt, reply.ReviewedAt, null)));
    }

    private async Task<bool> CanModerateLessonAsync(Guid lessonId, string permission, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        return context != null && Can(context, permission) && await _db.Lessons
            .AnyAsync(lesson => lesson.Id == lessonId && lesson.ContentSection.Term.Package.TeacherId == context.TeacherId, ct);
    }

    private async Task<bool> CanModerateLessonCommentAsync(Guid commentId, string permission, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        return context != null && Can(context, permission) && await _db.LessonComments
            .AnyAsync(comment => comment.Id == commentId && comment.Lesson.ContentSection.Term.Package.TeacherId == context.TeacherId, ct);
    }

}

public class GradeEssayRequestDto
{
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
}

public record TeacherStaffMemberDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string PhoneNumber,
    bool IsActive,
    DateTime CreatedAt,
    string? Notes,
    IReadOnlyList<string> PermissionKeys);

public record CreateTeacherStaffRequest(
    string FullName,
    string PhoneNumber,
    string Password,
    string? Notes,
    IReadOnlyList<string>? PermissionKeys);

public record UpdateTeacherStaffStatusRequest(bool IsActive);

public record UpdateTeacherStaffPermissionsRequest(IReadOnlyList<string>? PermissionKeys);
public record ReplyToLessonCommentRequest(string? Body);

public record TeacherWorkspaceContextDto(bool IsOwner, IReadOnlyList<string> PermissionKeys, IReadOnlyList<string> AvailablePermissionKeys);

public class TeacherUpdateProfileRequestDto
{
    public string Bio { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string? AssistantPhoneNumbers { get; set; }
    public string? FacebookUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? TelegramUrl { get; set; }
}

public class UploadImageRequestDto
{
    public string Base64Image { get; set; } = null!;
    public string FileName { get; set; } = null!;
}
