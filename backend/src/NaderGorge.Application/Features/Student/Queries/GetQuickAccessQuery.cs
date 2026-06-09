using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetQuickAccessQuery(Guid UserId) : IRequest<ApiResponse<List<QuickAccessItemDto>>>;

public record QuickAccessItemDto(string Title, string PathBreadcrumb, string Url, CodeType AccessType);

public class GetQuickAccessQueryHandler : IRequestHandler<GetQuickAccessQuery, ApiResponse<List<QuickAccessItemDto>>>
{
    private readonly IAppDbContext _db;
    public GetQuickAccessQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<QuickAccessItemDto>>> Handle(GetQuickAccessQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var grants = await _db.StudentAccessGrants
            .Where(g => g.UserId == request.UserId && g.IsActive &&
                        (g.ExpiresAt == null || g.ExpiresAt > now) &&
                        g.GrantType != CodeType.Package)
            .OrderByDescending(g => g.GrantedAt)
            .ToListAsync(ct);

        var list = new List<QuickAccessItemDto>();

        foreach (var grant in grants)
        {
            if (grant.GrantType == CodeType.Term && grant.TermId.HasValue)
            {
                var term = await _db.Terms
                    .Include(t => t.Package)
                    .FirstOrDefaultAsync(t => t.Id == grant.TermId.Value, ct);

                if (term != null)
                {
                    list.Add(new QuickAccessItemDto(
                        term.Title,
                        $"{term.Package?.Name ?? "بدون باقة"} > {term.Title}",
                        $"/student/packages/{term.PackageId}/terms/{term.Id}",
                        CodeType.Term
                    ));
                }
            }
            else if (grant.GrantType == CodeType.Month && grant.ContentSectionId.HasValue)
            {
                var section = await _db.ContentSections
                    .Include(s => s.Term)
                        .ThenInclude(t => t.Package)
                    .FirstOrDefaultAsync(s => s.Id == grant.ContentSectionId.Value, ct);

                if (section != null)
                {
                    list.Add(new QuickAccessItemDto(
                        section.Title,
                        $"{section.Term?.Package?.Name ?? "بدون باقة"} > {section.Term?.Title ?? "بدون ترم"} > {section.Title}",
                        $"/student/packages/{section.Term?.PackageId}/terms/{section.TermId}/sections/{section.Id}",
                        CodeType.Month
                    ));
                }
            }
            else if (grant.GrantType == CodeType.Lesson && grant.LessonId.HasValue)
            {
                var lesson = await _db.Lessons
                    .Include(l => l.ContentSection)
                        .ThenInclude(s => s.Term)
                            .ThenInclude(t => t.Package)
                    .FirstOrDefaultAsync(l => l.Id == grant.LessonId.Value, ct);

                if (lesson != null)
                {
                    list.Add(new QuickAccessItemDto(
                        lesson.Title,
                        $"{lesson.ContentSection?.Term?.Package?.Name ?? "بدون باقة"} > {lesson.ContentSection?.Term?.Title ?? "بدون ترم"} > {lesson.ContentSection?.Title ?? "بدون شهر"} > {lesson.Title}",
                        $"/student/packages/{lesson.ContentSection?.Term?.PackageId}/terms/{lesson.ContentSection?.TermId}/sections/{lesson.ContentSectionId}/lessons/{lesson.Id}",
                        CodeType.Lesson
                    ));
                }
            }
            // For now, Video and Exam grants are skipped for Quick Access or can be added later if needed.
        }

        return ApiResponse<List<QuickAccessItemDto>>.Ok(list);
    }
}
