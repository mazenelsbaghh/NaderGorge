using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.VideoTypes.Commands;

public record DeleteVideoTypeCommand(Guid Id, Guid AdminUserId) : IRequest<ApiResponse>;

public sealed class DeleteVideoTypeCommandHandler : IRequestHandler<DeleteVideoTypeCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public DeleteVideoTypeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(DeleteVideoTypeCommand request, CancellationToken ct)
    {
        var type = await _db.VideoTypes.FirstOrDefaultAsync(item => item.Id == request.Id, ct);
        if (type == null)
        {
            return ApiResponse.Fail("نوع الفيديو غير موجود.", ["NOT_FOUND"]);
        }

        var assignedVideoCount = await _db.LessonVideos.CountAsync(video => video.VideoTypeId == type.Id, ct);
        if (assignedVideoCount > 0)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "DELETE_VIDEO_TYPE_BLOCKED",
                EntityType = nameof(VideoType),
                EntityId = type.Id,
                PerformedByUserId = request.AdminUserId,
                OldValues = JsonSerializer.Serialize(new { type.Name, AssignedVideoCount = assignedVideoCount })
            });
            await _db.SaveChangesAsync(ct);
            return ApiResponse.Fail("النوع مستخدم في فيديوهات حالية. عطّله بدلاً من حذفه.", ["VIDEO_TYPE_IN_USE"]);
        }

        _db.VideoTypes.Remove(type);
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "DELETE_VIDEO_TYPE",
            EntityType = nameof(VideoType),
            EntityId = type.Id,
            PerformedByUserId = request.AdminUserId,
            OldValues = JsonSerializer.Serialize(new { type.Name, type.SortOrder, type.IsActive })
        });
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("تم حذف نوع الفيديو.");
    }
}
