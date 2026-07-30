using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.VideoTypes.Commands;

public record UpdateVideoTypeCommand(Guid Id, string Name, int SortOrder, Guid AdminUserId)
    : IRequest<ApiResponse<VideoTypeDto>>;

public sealed class UpdateVideoTypeCommandValidator : AbstractValidator<UpdateVideoTypeCommand>
{
    public UpdateVideoTypeCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name) && VideoTypeRules.CleanName(name).Length is >= VideoTypeRules.MinNameLength and <= VideoTypeRules.MaxNameLength)
            .WithMessage("اسم النوع يجب أن يكون بين حرفين و80 حرفاً.");
        RuleFor(command => command.SortOrder)
            .InclusiveBetween(VideoTypeRules.MinSortOrder, VideoTypeRules.MaxSortOrder);
    }
}

public sealed class UpdateVideoTypeCommandHandler : IRequestHandler<UpdateVideoTypeCommand, ApiResponse<VideoTypeDto>>
{
    private readonly IAppDbContext _db;

    public UpdateVideoTypeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<VideoTypeDto>> Handle(UpdateVideoTypeCommand request, CancellationToken ct)
    {
        var type = await _db.VideoTypes.FirstOrDefaultAsync(item => item.Id == request.Id, ct);
        if (type == null)
        {
            return ApiResponse<VideoTypeDto>.Fail("نوع الفيديو غير موجود.", ["NOT_FOUND"]);
        }

        var name = VideoTypeRules.CleanName(request.Name);
        var normalizedName = VideoTypeRules.NormalizeName(name);
        if (await _db.VideoTypes.AnyAsync(item => item.Id != request.Id && item.NormalizedName == normalizedName, ct))
        {
            return ApiResponse<VideoTypeDto>.Fail("يوجد نوع فيديو بنفس الاسم.", ["VIDEO_TYPE_DUPLICATE"]);
        }

        var oldValues = new { type.Name, type.SortOrder };
        type.Name = name;
        type.NormalizedName = normalizedName;
        type.SortOrder = request.SortOrder;
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "UPDATE_VIDEO_TYPE",
            EntityType = nameof(VideoType),
            EntityId = type.Id,
            PerformedByUserId = request.AdminUserId,
            OldValues = JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(new { type.Name, type.SortOrder })
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (VideoTypeRules.IsDuplicateNameViolation(exception))
        {
            return ApiResponse<VideoTypeDto>.Fail("يوجد نوع فيديو بنفس الاسم.", ["VIDEO_TYPE_DUPLICATE"]);
        }

        var count = await _db.LessonVideos.CountAsync(video => video.VideoTypeId == type.Id, ct);
        return ApiResponse<VideoTypeDto>.Ok(VideoTypeRules.ToDto(type, count), "تم تحديث نوع الفيديو.");
    }
}
