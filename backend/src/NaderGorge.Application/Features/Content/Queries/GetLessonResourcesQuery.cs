using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetLessonResourcesQuery(Guid LessonId, Guid UserId) : IRequest<ApiResponse<List<ResourceDto>>>;

public class GetLessonResourcesQueryHandler : IRequestHandler<GetLessonResourcesQuery, ApiResponse<List<ResourceDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetLessonResourcesQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<List<ResourceDto>>> Handle(GetLessonResourcesQuery request, CancellationToken ct)
    {
        var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, request.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<List<ResourceDto>>.Fail("You do not have access to this lesson.", new List<string> { "FORBIDDEN" });

        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == request.LessonId, ct);
        if (!lessonExists)
            return ApiResponse<List<ResourceDto>>.Fail("Lesson not found", new List<string> { "NOT_FOUND" });

        var resources = await _db.LessonResources
            .AsNoTracking()
            .Where(r => r.LessonId == request.LessonId)
            .Select(r => new ResourceDto(r.Id, r.Title, r.FileUrl, r.ResourceType))
            .ToListAsync(ct);

        return ApiResponse<List<ResourceDto>>.Ok(resources);
    }
}
