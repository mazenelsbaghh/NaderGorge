using System.Data;
using MediatR;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CancelAnalyzeVideoAICommand(Guid VideoId, Guid AdminId, bool IsMindmapOnly = false) : IRequest<bool>;

public class CancelAnalyzeVideoAICommandHandler : IRequestHandler<CancelAnalyzeVideoAICommand, bool>
{
    private readonly IAppDbContext _context;
    private readonly IAiJobCancellationStore _cancellations;

    public CancelAnalyzeVideoAICommandHandler(IAppDbContext context, IAiJobCancellationStore cancellations)
    {
        _context = context;
        _cancellations = cancellations;
    }

    public async Task<bool> Handle(CancelAnalyzeVideoAICommand request, CancellationToken cancellationToken)
    {
        var capturedState = await _context.LessonVideos
            .AsNoTracking()
            .Where(candidate => candidate.Id == request.VideoId)
            .Select(candidate => new
            {
                candidate.IsProcessingAI,
                candidate.IsProcessingMindmaps,
                candidate.CurrentAiAnalysisRunId,
                candidate.CurrentMindmapGenerationRunId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (capturedState == null)
            return false;

        var hasActiveMindmapRun =
            capturedState.IsProcessingMindmaps ||
            capturedState.CurrentMindmapGenerationRunId.HasValue;
        var hasAnyActiveRun =
            capturedState.IsProcessingAI ||
            capturedState.CurrentAiAnalysisRunId.HasValue ||
            hasActiveMindmapRun;
        if (request.IsMindmapOnly ? !hasActiveMindmapRun : !hasAnyActiveRun)
            return false;

        await using var transaction = await _context.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var now = DateTime.UtcNow;
            int videoRows;
            if (request.IsMindmapOnly)
            {
                videoRows = await _context.LessonVideos
                    .Where(candidate =>
                        candidate.Id == request.VideoId &&
                        candidate.IsProcessingMindmaps == capturedState.IsProcessingMindmaps &&
                        candidate.CurrentMindmapGenerationRunId == capturedState.CurrentMindmapGenerationRunId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(candidate => candidate.IsProcessingMindmaps, false)
                        .SetProperty(candidate => candidate.CurrentMindmapGenerationRunId, (Guid?)null)
                        .SetProperty(candidate => candidate.UpdatedAt, now), cancellationToken);
            }
            else
            {
                videoRows = await _context.LessonVideos
                    .Where(candidate =>
                        candidate.Id == request.VideoId &&
                        candidate.IsProcessingAI == capturedState.IsProcessingAI &&
                        candidate.IsProcessingMindmaps == capturedState.IsProcessingMindmaps &&
                        candidate.CurrentAiAnalysisRunId == capturedState.CurrentAiAnalysisRunId &&
                        candidate.CurrentMindmapGenerationRunId == capturedState.CurrentMindmapGenerationRunId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(candidate => candidate.IsProcessingAI, false)
                        .SetProperty(candidate => candidate.IsProcessingMindmaps, false)
                        .SetProperty(candidate => candidate.CurrentAiAnalysisRunId, (Guid?)null)
                        .SetProperty(candidate => candidate.CurrentMindmapGenerationRunId, (Guid?)null)
                        .SetProperty(candidate => candidate.SubtitleUrl, (string?)null)
                        .SetProperty(candidate => candidate.UpdatedAt, now), cancellationToken);
            }

            // The conditional update is deliberately first: it fences the captured run and
            // holds the LessonVideo row lock until the cancellation marker and outbox commit.
            if (videoRows != 1)
                return false;

            if (request.IsMindmapOnly)
            {
                await _context.VideoChapters
                    .Where(candidate =>
                        candidate.LessonVideoId == request.VideoId &&
                        (candidate.IsRegeneratingMindmap ||
                         candidate.CurrentMindmapGenerationRunId.HasValue))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(candidate => candidate.IsRegeneratingMindmap, false)
                        .SetProperty(candidate => candidate.CurrentMindmapGenerationRunId, (Guid?)null),
                        cancellationToken);

                await _cancellations.RequestMindmapCancellationAsync(request.VideoId);
            }
            else
            {
                await _context.VideoChapters
                    .Where(candidate => candidate.LessonVideoId == request.VideoId)
                    .ExecuteDeleteAsync(cancellationToken);

                await _cancellations.RequestVideoAnalysisCancellationAsync(request.VideoId);
                await _cancellations.RequestMindmapCancellationAsync(request.VideoId);
            }

            var teacherUserId = await _context.LessonVideos
                .Where(candidate => candidate.Id == request.VideoId)
                .Select(candidate => (string?)candidate.Lesson.ContentSection.Term.Package.Teacher.UserId.ToString())
                .SingleAsync(cancellationToken);
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonVideoId = request.VideoId,
                isMindmapOnly = request.IsMindmapOnly
            });
            _context.OutboxEvents.Add(new OutboxEvent
            {
                Type = "AiJobCancelled",
                TargetGroup = "Role_Admin",
                PayloadJson = payload
            });

            if (teacherUserId != null)
            {
                _context.OutboxEvents.Add(new OutboxEvent
                {
                    Type = "AiJobCancelled",
                    TargetUserId = teacherUserId,
                    PayloadJson = payload
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
