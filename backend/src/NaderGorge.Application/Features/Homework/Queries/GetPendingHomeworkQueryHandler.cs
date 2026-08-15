using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Homework.Queries;

public class GetPendingHomeworkQueryHandler : IRequestHandler<GetPendingHomeworkQuery, ApiResponse<List<PendingHomeworkDto>>>
{
    private readonly IAppDbContext _dbContext;
    private readonly IContentArchiveAccessService _archiveAccess;

    public GetPendingHomeworkQueryHandler(IAppDbContext dbContext, IContentArchiveAccessService? archiveAccess = null)
    {
        _dbContext = dbContext;
        _archiveAccess = archiveAccess ?? new ContentArchiveAccessService(dbContext);
    }

    public async Task<ApiResponse<List<PendingHomeworkDto>>> Handle(GetPendingHomeworkQuery request, CancellationToken cancellationToken)
    {
        // For MVP, "pending" homeworks are all homeworks that do NOT have a submission from this student,
        // or have an "InProgress" submission. Or simply, assigned homeworks for the active package.
        // We'll approximate this by finding homeworks assigned to lessons the student has access to
        // that are not fully submitted yet.

        // This is a simplified version. A real version might use a StudentSession concept.
        var pendingSubmissions = await _dbContext.HomeworkSubmissions
            .Include(s => s.Homework)
            .ThenInclude(h => h.Questions)
            .Where(s => s.StudentId == request.StudentId && s.Status == Domain.Entities.Homework.SubmissionStatus.InProgress)
            .ToListAsync(cancellationToken);

        var dtos = new List<PendingHomeworkDto>();
        foreach (var submission in pendingSubmissions)
        {
            if (!await _archiveAccess.CanViewAsync(request.StudentId, ContentArchiveTargetType.Homework, submission.HomeworkId, cancellationToken))
                continue;
            dtos.Add(new PendingHomeworkDto(
                submission.HomeworkId,
                submission.Homework.Title,
                submission.Homework.Description,
                null,
                submission.Homework.Questions.Count));
        }

        // Also fetch missing submissions (Homeworks that exist for their accessible content but no submission row exists)
        // Leaving it simple for now, as MVP logic will rely on students creating a submission when they start.

        return ApiResponse<List<PendingHomeworkDto>>.Ok(dtos);
    }
}
