using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.VideoTypes.Commands;

public record SetVideoTypeStatusCommand(Guid Id, bool IsActive, Guid AdminUserId)
    : IRequest<ApiResponse<VideoTypeDto>>;

public sealed class SetVideoTypeStatusCommandHandler : IRequestHandler<SetVideoTypeStatusCommand, ApiResponse<VideoTypeDto>>
{
    private readonly IAppDbContext _db;

    public SetVideoTypeStatusCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<VideoTypeDto>> Handle(SetVideoTypeStatusCommand request, CancellationToken ct)
    {
        var type = await _db.VideoTypes.FirstOrDefaultAsync(item => item.Id == request.Id, ct);
        if (type == null)
        {
            return ApiResponse<VideoTypeDto>.Fail("نوع الفيديو غير موجود.", ["NOT_FOUND"]);
        }

        var previous = type.IsActive;
        type.IsActive = request.IsActive;
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "SET_VIDEO_TYPE_STATUS",
            EntityType = nameof(VideoType),
            EntityId = type.Id,
            PerformedByUserId = request.AdminUserId,
            OldValues = JsonSerializer.Serialize(new { IsActive = previous }),
            NewValues = JsonSerializer.Serialize(new { type.IsActive })
        });
        await _db.SaveChangesAsync(ct);

        var count = await _db.LessonVideos.CountAsync(video => video.VideoTypeId == type.Id, ct);
        return ApiResponse<VideoTypeDto>.Ok(VideoTypeRules.ToDto(type, count), "تم تحديث حالة نوع الفيديو.");
    }
}
