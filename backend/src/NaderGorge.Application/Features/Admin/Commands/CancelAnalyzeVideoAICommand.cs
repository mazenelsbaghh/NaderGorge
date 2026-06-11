using MediatR;
using NaderGorge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CancelAnalyzeVideoAICommand(Guid VideoId, Guid AdminId, bool IsMindmapOnly = false) : IRequest<bool>;

public class CancelAnalyzeVideoAICommandHandler : IRequestHandler<CancelAnalyzeVideoAICommand, bool>
{
    private readonly IAppDbContext _context;

    public CancelAnalyzeVideoAICommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(CancelAnalyzeVideoAICommand request, CancellationToken cancellationToken)
    {
        var video = await _context.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, cancellationToken);

        if (video == null)
            return false;

        if (request.IsMindmapOnly)
        {
            video.IsProcessingMindmaps = false;
        }
        else
        {
            video.IsProcessingAI = false;
            video.IsProcessingMindmaps = false;
            video.SubtitleUrl = null;

            var existingChapters = await _context.VideoChapters
                .Where(c => c.LessonVideoId == request.VideoId)
                .ToListAsync(cancellationToken);

            if (existingChapters.Any())
            {
                _context.VideoChapters.RemoveRange(existingChapters);
            }
        }

        var teacherUserId = await _context.LessonVideos
            .Where(v => v.Id == video.Id)
            .Select(v => (string?)v.Lesson.ContentSection.Term.Package.Teacher.UserId.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        var adminEvent = new OutboxEvent
        {
            Type = "AiJobCancelled",
            TargetGroup = "Role_Admin",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonVideoId = video.Id,
                isMindmapOnly = request.IsMindmapOnly
            })
        };
        _context.OutboxEvents.Add(adminEvent);

        if (teacherUserId != null)
        {
            var teacherEvent = new OutboxEvent
            {
                Type = "AiJobCancelled",
                TargetUserId = teacherUserId,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    lessonVideoId = video.Id,
                    isMindmapOnly = request.IsMindmapOnly
                })
            };
            _context.OutboxEvents.Add(teacherEvent);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
