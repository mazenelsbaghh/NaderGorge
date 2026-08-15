using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Gifts.Queries;

public sealed record GetGiftStudentsLookupQuery(
    string? Search = null,
    GiftTargetType? TargetType = null,
    Guid? TargetId = null) : IRequest<ApiResponse<IReadOnlyList<GiftLookupDto>>>;
public sealed record GetGiftTeachersLookupQuery(string? Search = null) : IRequest<ApiResponse<IReadOnlyList<GiftLookupDto>>>;
public sealed record GetGiftTargetsLookupQuery(GiftTargetType TargetType, Guid? TeacherId = null, string? Search = null) : IRequest<ApiResponse<IReadOnlyList<GiftLookupDto>>>;

public sealed class GetGiftStudentsLookupQueryHandler : IRequestHandler<GetGiftStudentsLookupQuery, ApiResponse<IReadOnlyList<GiftLookupDto>>>
{
    private readonly IAppDbContext _db;
    public GetGiftStudentsLookupQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<IReadOnlyList<GiftLookupDto>>> Handle(GetGiftStudentsLookupQuery request, CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking().Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Type == RoleType.Student));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(search) || x.PhoneNumber.Contains(search));
        }
        var rows = await query.OrderBy(x => x.FullName).Take(50)
            .Select(x => new GiftLookupDto(x.Id, x.FullName, x.PhoneNumber, null, null)).ToListAsync(ct);

        if (!request.TargetType.HasValue || !request.TargetId.HasValue ||
            request.TargetType is GiftTargetType.GeneralBalance or GiftTargetType.TeacherBalance)
            return ApiResponse<IReadOnlyList<GiftLookupDto>>.Ok(rows);

        var studentIds = rows.Select(x => x.Id).ToList();
        var previouslyGifted = await _db.GiftRecipients
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) &&
                        x.Status != GiftRecipientStatus.Failed &&
                        x.GiftIssuance.TargetType == request.TargetType &&
                        (request.TargetType == GiftTargetType.Package && x.GiftIssuance.PackageId == request.TargetId ||
                         request.TargetType == GiftTargetType.Term && x.GiftIssuance.TermId == request.TargetId ||
                         request.TargetType == GiftTargetType.ContentSection && x.GiftIssuance.ContentSectionId == request.TargetId ||
                         request.TargetType == GiftTargetType.Lesson && x.GiftIssuance.LessonId == request.TargetId ||
                         request.TargetType == GiftTargetType.Video && x.GiftIssuance.LessonVideoId == request.TargetId ||
                         request.TargetType == GiftTargetType.Exam && x.GiftIssuance.ExamId == request.TargetId))
            .GroupBy(x => x.StudentId)
            .Select(x => new { StudentId = x.Key, LastGiftedAt = x.Max(recipient => recipient.GiftIssuance.CreatedAt) })
            .ToDictionaryAsync(x => x.StudentId, x => x.LastGiftedAt, ct);

        rows = rows.Select(row => previouslyGifted.TryGetValue(row.Id, out var giftedAt)
            ? row with { PreviouslyGiftedAt = giftedAt }
            : row).ToList();
        return ApiResponse<IReadOnlyList<GiftLookupDto>>.Ok(rows);
    }
}

public sealed class GetGiftTeachersLookupQueryHandler : IRequestHandler<GetGiftTeachersLookupQuery, ApiResponse<IReadOnlyList<GiftLookupDto>>>
{
    private readonly IAppDbContext _db;
    public GetGiftTeachersLookupQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<IReadOnlyList<GiftLookupDto>>> Handle(GetGiftTeachersLookupQuery request, CancellationToken ct)
    {
        var query = _db.TeacherProfiles.AsNoTracking().Where(x => x.User.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.User.FullName.ToLower().Contains(search));
        }
        var rows = await query.OrderBy(x => x.User.FullName).Take(50)
            .Select(x => new GiftLookupDto(x.Id, x.User.FullName, x.Specialization, null, null)).ToListAsync(ct);
        return ApiResponse<IReadOnlyList<GiftLookupDto>>.Ok(rows);
    }
}

public sealed class GetGiftTargetsLookupQueryHandler : IRequestHandler<GetGiftTargetsLookupQuery, ApiResponse<IReadOnlyList<GiftLookupDto>>>
{
    private readonly IAppDbContext _db;
    public GetGiftTargetsLookupQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<IReadOnlyList<GiftLookupDto>>> Handle(GetGiftTargetsLookupQuery request, CancellationToken ct)
    {
        var search = request.Search?.Trim().ToLower();
        List<GiftLookupDto> rows = request.TargetType switch
        {
            GiftTargetType.Package => await _db.Packages.AsNoTracking()
                .Where(x => x.IsActive && x.ArchiveMode == ContentArchiveMode.None && (!request.TeacherId.HasValue || x.TeacherId == request.TeacherId) && (search == null || x.Name.ToLower().Contains(search)))
                .OrderBy(x => x.Name).Take(50).Select(x => new GiftLookupDto(x.Id, x.Name, x.Teacher.User.FullName, null, null)).ToListAsync(ct),
            GiftTargetType.Term => await _db.Terms.AsNoTracking()
                .Where(x => x.Package.IsActive && x.Package.ArchiveMode == ContentArchiveMode.None && x.ArchiveMode == ContentArchiveMode.None &&
                    (!x.IsSystemContainer || x.Package.ContentMode == PackageContentMode.SectionWithLessons) &&
                    (!request.TeacherId.HasValue || x.Package.TeacherId == request.TeacherId) &&
                    (search == null || x.Title.ToLower().Contains(search) || x.Package.Name.ToLower().Contains(search)))
                .OrderBy(x => x.Package.Name).ThenBy(x => x.Order).Take(50)
                .Select(x => new GiftLookupDto(x.Id, x.IsSystemContainer ? x.Package.Name : x.Title, x.Package.Name, null, null)).ToListAsync(ct),
            GiftTargetType.ContentSection => await _db.ContentSections.AsNoTracking()
                .Where(x => x.Term.Package.IsActive && x.Term.Package.ArchiveMode == ContentArchiveMode.None && x.Term.ArchiveMode == ContentArchiveMode.None && x.ArchiveMode == ContentArchiveMode.None &&
                    (!x.IsSystemContainer || x.Term.Package.ContentMode == PackageContentMode.LessonsOnly) &&
                    (!request.TeacherId.HasValue || x.Term.Package.TeacherId == request.TeacherId) &&
                    (search == null || x.Title.ToLower().Contains(search) || x.Term.Title.ToLower().Contains(search) || x.Term.Package.Name.ToLower().Contains(search)))
                .OrderBy(x => x.Term.Package.Name).ThenBy(x => x.Term.Order).ThenBy(x => x.Order).Take(50)
                .Select(x => new GiftLookupDto(x.Id, x.IsSystemContainer ? x.Term.Package.Name : x.Title, x.Term.Package.Name, null, null)).ToListAsync(ct),
            GiftTargetType.Lesson => await _db.Lessons.AsNoTracking()
                .Where(x => x.ContentSection.Term.Package.ArchiveMode == ContentArchiveMode.None && x.ContentSection.Term.ArchiveMode == ContentArchiveMode.None && x.ContentSection.ArchiveMode == ContentArchiveMode.None && x.ArchiveMode == ContentArchiveMode.None && (!request.TeacherId.HasValue || x.ContentSection.Term.Package.TeacherId == request.TeacherId) && (search == null || x.Title.ToLower().Contains(search) || x.InternalCode.ToLower().Contains(search)))
                .OrderBy(x => x.Title).Take(50).Select(x => new GiftLookupDto(x.Id, x.Title, x.ContentSection.Term.Package.Name, null, null)).ToListAsync(ct),
            GiftTargetType.Video => await _db.LessonVideos.AsNoTracking()
                .Where(x => x.IsActive && x.Lesson.ContentSection.Term.Package.ArchiveMode == ContentArchiveMode.None && x.Lesson.ContentSection.Term.ArchiveMode == ContentArchiveMode.None && x.Lesson.ContentSection.ArchiveMode == ContentArchiveMode.None && x.Lesson.ArchiveMode == ContentArchiveMode.None && x.ArchiveMode == ContentArchiveMode.None && (!request.TeacherId.HasValue || x.Lesson.ContentSection.Term.Package.TeacherId == request.TeacherId) && (search == null || x.Title.ToLower().Contains(search) || x.InternalCode.ToLower().Contains(search)))
                .OrderBy(x => x.Title).Take(50).Select(x => new GiftLookupDto(x.Id, x.Title, x.Lesson.Title, null, null)).ToListAsync(ct),
            GiftTargetType.Exam => await _db.Exams.AsNoTracking()
                .Where(x => x.ArchiveMode == ContentArchiveMode.None && (!request.TeacherId.HasValue || x.CreatedByTeacherId == request.TeacherId) && (search == null || x.Title.ToLower().Contains(search) || x.InternalCode.ToLower().Contains(search)))
                .OrderBy(x => x.Title).Take(50).Select(x => new GiftLookupDto(x.Id, x.Title, x.CreatedByTeacher.User.FullName, null, null)).ToListAsync(ct),
            _ => new List<GiftLookupDto>()
        };

        var rowsWithScopes = new List<GiftLookupDto>(rows.Count);
        foreach (var row in rows)
        {
            rowsWithScopes.Add(row with { AcademicScopes = await ResolveScopeSummariesAsync(_db, request.TargetType, row.Id, ct) });
        }

        return ApiResponse<IReadOnlyList<GiftLookupDto>>.Ok(rowsWithScopes);
    }

    private static async Task<IReadOnlyList<AcademicScopeSummaryDto>?> ResolveScopeSummariesAsync(
        IAppDbContext db,
        GiftTargetType targetType,
        Guid targetId,
        CancellationToken ct)
    {
        var ownerType = targetType switch
        {
            GiftTargetType.Package => StudentFacingScopeOwnerType.Package,
            GiftTargetType.Term => StudentFacingScopeOwnerType.Term,
            GiftTargetType.ContentSection => StudentFacingScopeOwnerType.ContentSection,
            GiftTargetType.Lesson => StudentFacingScopeOwnerType.Lesson,
            GiftTargetType.Video => StudentFacingScopeOwnerType.LessonVideo,
            GiftTargetType.Exam => StudentFacingScopeOwnerType.Exam,
            _ => (StudentFacingScopeOwnerType?)null
        };

        if (!ownerType.HasValue)
            return null;

        var scopes = await db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == ownerType.Value && x.OwnerId == targetId)
            .ToListAsync(ct);

        return AcademicScopeService.ToScopeSummaries(scopes);
    }
}
