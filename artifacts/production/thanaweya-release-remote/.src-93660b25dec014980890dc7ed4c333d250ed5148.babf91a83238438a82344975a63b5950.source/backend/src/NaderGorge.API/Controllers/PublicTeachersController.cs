using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Community.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/public/teachers")]
public class PublicTeachersController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService _academicScope;

    public PublicTeachersController(IAppDbContext db, IAcademicScopeService academicScope)
    {
        _db = db;
        _academicScope = academicScope;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? subjectId = null, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var query = BuildActivePublicTeacherQuery();

        if (subjectId.HasValue)
        {
            query = query.Where(t => t.TeacherSubjects.Any(ts => ts.SubjectId == subjectId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => t.User.FullName.Contains(search) || t.Specialization.Contains(search));
        }

        return await CreateTeacherListResponse(query, ct);
    }

    [HttpGet("landing")]
    public async Task<IActionResult> ListLandingTeachers(CancellationToken ct)
    {
        var query = BuildActivePublicTeacherQuery()
            .Where(teacher => teacher.ShowOnLanding);

        return await CreateTeacherListResponse(query, ct);
    }

    private IQueryable<TeacherProfile> BuildActivePublicTeacherQuery()
        => _db.TeacherProfiles
            .Include(teacher => teacher.User)
            .Include(teacher => teacher.TeacherSubjects).ThenInclude(link => link.Subject)
            .Where(teacher => teacher.User.IsActive && !teacher.User.IsDeleted && teacher.IsVisibleToStudents);

    private async Task<IActionResult> CreateTeacherListResponse(
        IQueryable<TeacherProfile> query,
        CancellationToken ct)
    {
        var teacherRows = await query
            .OrderBy(t => t.User.FullName)
            .Select(teacher => new PublicTeacherListRow(
                teacher.Id,
                teacher.UserId,
                teacher.PublicSlug,
                teacher.User.FullName,
                teacher.PublicBio ?? teacher.Bio,
                teacher.Specialization,
                teacher.ProfileImageUrl,
                teacher.IntroVideoUrl,
                teacher.RatingAverage,
                teacher.RatingCount,
                teacher.TeacherSubjects.Select(link => link.Subject.Name).ToList(),
                teacher.TeacherSubjects.Select(link => new PublicTeacherSubjectRow(link.SubjectId, link.Subject.Name)).ToList()))
            .ToListAsync(ct);

        var activePhotos = await LoadActiveTeacherPhotosAsync(teacherRows, ct);
        var data = teacherRows.Select(row => CreatePublicTeacherResponse(row, activePhotos));

        return Ok(new { success = true, data });
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadActiveTeacherPhotosAsync(
        IReadOnlyCollection<PublicTeacherListRow> teacherRows,
        CancellationToken ct)
    {
        var teacherIds = teacherRows
            .Where(row => string.IsNullOrWhiteSpace(row.ProfileImageUrl))
            .Select(row => row.UserId)
            .ToList();

        if (teacherIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var photoRows = await _db.TeacherPhotos
            .Where(photo => teacherIds.Contains(photo.TeacherId) && photo.IsActive)
            .OrderByDescending(photo => photo.UploadedAt)
            .Select(photo => new { photo.TeacherId, photo.FileUrl })
            .ToListAsync(ct);

        return photoRows
            .GroupBy(photo => photo.TeacherId)
            .ToDictionary(group => group.Key, group => group.First().FileUrl);
    }

    private static object CreatePublicTeacherResponse(
        PublicTeacherListRow row,
        IReadOnlyDictionary<Guid, string> activePhotos)
    {
        activePhotos.TryGetValue(row.UserId, out var activePhotoUrl);
        return new
        {
            id = row.Id,
            teacherId = row.Id,
            row.Slug,
            fullName = row.FullName,
            displayName = row.FullName,
            bio = row.Bio,
            Specialization = row.Specialization,
            ProfileImageUrl = row.ProfileImageUrl ?? activePhotoUrl,
            row.IntroVideoUrl,
            row.RatingAverage,
            row.RatingCount,
            subjectNames = row.SubjectNames,
            subjects = row.Subjects
        };
    }

    private sealed record PublicTeacherListRow(
        Guid Id,
        Guid UserId,
        string? Slug,
        string FullName,
        string Bio,
        string Specialization,
        string? ProfileImageUrl,
        string? IntroVideoUrl,
        decimal RatingAverage,
        int RatingCount,
        List<string> SubjectNames,
        List<PublicTeacherSubjectRow> Subjects);

    private sealed record PublicTeacherSubjectRow(Guid SubjectId, string Name);

    [HttpGet("{slugOrId}")]
    public async Task<IActionResult> Detail([FromRoute] string slugOrId, CancellationToken ct)
    {
        var isGuid = Guid.TryParse(slugOrId, out var teacherId);
        var teacher = await _db.TeacherProfiles
            .Include(t => t.User)
            .Include(t => t.TeacherSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.Packages)
            .FirstOrDefaultAsync(t => t.IsVisibleToStudents && ((isGuid && t.Id == teacherId) || t.PublicSlug == slugOrId), ct);

        if (teacher == null)
        {
            return NotFound(new { success = false, message = "المدرس غير موجود" });
        }

        var sharedPackages = await _db.SharedTeacherPackageTeachers
            .Include(x => x.SharedTeacherPackage)
            .Where(x => x.TeacherId == teacher.Id && teacher.IsContentVisibleToStudents && x.SharedTeacherPackage.IsPublished)
            .Select(x => new
            {
                x.SharedTeacherPackage.Id,
                x.SharedTeacherPackage.Name,
                x.SharedTeacherPackage.Price,
                x.SharedTeacherPackage.ImageUrl
            })
            .ToListAsync(ct);

        var lessons = await _db.Lessons
            .Where(l => l.ContentSection.Term.Package.TeacherId == teacher.Id && teacher.IsContentVisibleToStudents && l.ContentSection.Term.Package.IsActive)
            .OrderBy(l => l.Order)
            .Take(12)
            .Select(l => new { l.Id, l.Title, l.Price })
            .ToListAsync(ct);

        var profileImageUrl = teacher.ProfileImageUrl
            ?? await _db.TeacherPhotos
                .Where(tp => tp.TeacherId == teacher.UserId && tp.IsActive)
                .OrderByDescending(tp => tp.UploadedAt)
                .Select(tp => tp.FileUrl)
                .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                teacherId = teacher.Id,
                id = teacher.Id,
                slug = teacher.PublicSlug,
                fullName = teacher.User.FullName,
                displayName = teacher.User.FullName,
                bio = teacher.PublicBio ?? teacher.Bio,
                teacher.Specialization,
                ProfileImageUrl = profileImageUrl,
                teacher.IntroVideoUrl,
                teacher.ContactInfo,
                teacher.AssistantPhoneNumbers,
                teacher.FacebookUrl,
                teacher.YouTubeUrl,
                teacher.TelegramUrl,
                teacher.RatingAverage,
                teacher.RatingCount,
                subjectNames = teacher.TeacherSubjects.Select(ts => ts.Subject.Name),
                subjects = teacher.TeacherSubjects.Select(ts => new { id = ts.SubjectId, ts.Subject.Name }),
                packages = teacher.Packages.Where(p => p.IsActive).Select(p => new { p.Id, p.Name, p.Price, p.ImageUrl }),
                sharedPackages,
                lessons
            }
        });
    }

    [HttpGet("{teacherId:guid}/community-posts")]
    public async Task<IActionResult> CommunityPosts([FromRoute] Guid teacherId, CancellationToken ct)
    {
        var postIds = await _db.CommunityPosts
            .Where(p => p.TeacherId == teacherId
                && p.Teacher!.IsVisibleToStudents
                && p.Teacher!.IsContentVisibleToStudents
                && p.Status == CommunityPostStatus.Approved)
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .Select(p => p.Id)
            .ToListAsync(ct);

        Guid? currentUserId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            currentUserId = User.RequireUserId();
        }

        var posts = await _db.CommunityPosts
            .Where(p => postIds.Contains(p.Id)
                && p.Teacher!.IsVisibleToStudents
                && p.Teacher!.IsContentVisibleToStudents)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new CommunityPostFeedDto(
                p.Id,
                p.AuthorUser.FullName,
                p.Body,
                p.CreatedAt,
                _db.CommunityPostLikes.Count(l => l.PostId == p.Id),
                _db.CommunityPostComments.Count(c => c.PostId == p.Id && c.Status == CommunityCommentStatus.Approved),
                currentUserId.HasValue && _db.CommunityPostLikes.Any(l => l.PostId == p.Id && l.UserId == currentUserId.Value),
                p.IsPoll,
                currentUserId.HasValue && p.IsPoll ? _db.CommunityPostPollVotes.Where(v => v.PostId == p.Id && v.UserId == currentUserId.Value).Select(v => (Guid?)v.PollOptionId).FirstOrDefault() : null,
                p.IsPoll ? p.PollOptions.Select(o => new CommunityPostPollOptionDto(
                    o.Id,
                    o.Text,
                    _db.CommunityPostPollVotes.Count(v => v.PollOptionId == o.Id)
                )).ToList() : new List<CommunityPostPollOptionDto>(),
                p.AuthorUser.StudentProfile != null ? p.AuthorUser.StudentProfile.AvatarSlug : null
            ))
            .ToListAsync(ct);

        return Ok(new { success = true, data = posts });
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        if (User.Identity?.IsAuthenticated != true)
            return false;

        userId = User.RequireUserId();
        return true;
    }

    [Authorize(Roles = "Student")]
    [HttpGet("{teacherId:guid}/community-posts/mine")]
    public async Task<IActionResult> MyCommunityPosts([FromRoute] Guid teacherId, CancellationToken ct)
    {
        var userId = User.RequireUserId();
        var posts = await _db.CommunityPosts
            .AsNoTracking()
            .Where(p => p.TeacherId == teacherId && p.AuthorUserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new MyCommunityPostDto(
                p.Id,
                p.Body,
                p.Status.ToString(),
                p.CreatedAt,
                p.IsPoll
            ))
            .ToListAsync(ct);

        return Ok(new { success = true, data = posts });
    }

    [Authorize(Roles = "Student")]
    [HttpPost("{teacherId:guid}/community-posts")]
    public async Task<IActionResult> CreateCommunityPost([FromRoute] Guid teacherId, [FromBody] CreateTeacherCommunityPostDto dto, CancellationToken ct)
    {
        var trimmedBody = dto.Body.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBody))
        {
            return BadRequest(new { success = false, message = "نص المنشور مطلوب" });
        }

        if (trimmedBody.Length > 4000)
        {
            return BadRequest(new { success = false, message = "نص المنشور أطول من المسموح" });
        }

        var exists = await _db.TeacherProfiles.AnyAsync(t => t.Id == teacherId, ct);
        if (!exists)
        {
            return NotFound(new { success = false, message = "المدرس غير موجود" });
        }

        var isPoll = dto.PollOptions != null && dto.PollOptions.Count > 0;
        var validPollOptions = isPoll
            ? dto.PollOptions!.Select(o => o.Trim()).Where(o => !string.IsNullOrWhiteSpace(o)).ToList()
            : new List<string>();

        if (isPoll && validPollOptions.Count < 2)
        {
            return BadRequest(new { success = false, message = "الاستطلاع يحتاج خيارين على الأقل" });
        }

        if (isPoll && validPollOptions.Count > 10)
        {
            return BadRequest(new { success = false, message = "لا يمكن أن يزيد الاستطلاع عن ١٠ خيارات" });
        }

        var post = new CommunityPost
        {
            Id = Guid.NewGuid(),
            AuthorUserId = User.RequireUserId(),
            TeacherId = teacherId,
            Body = trimmedBody,
            Status = CommunityPostStatus.Pending,
            IsPoll = isPoll
        };

        foreach (var option in validPollOptions)
        {
            post.PollOptions.Add(new CommunityPostPollOption
            {
                Text = option
            });
        }

        _db.CommunityPosts.Add(post);
        await _db.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            data = new
            {
                post.Id,
                status = post.Status.ToString(),
                post.CreatedAt,
                message = isPoll ? "تم إرسال الاستطلاع للمراجعة" : "تم إرسال المنشور للمراجعة"
            },
            message = isPoll ? "تم إرسال الاستطلاع للمراجعة" : "تم إرسال المنشور للمراجعة"
        });
    }
}

public record CreateTeacherCommunityPostDto(string Body, List<string>? PollOptions = null);
