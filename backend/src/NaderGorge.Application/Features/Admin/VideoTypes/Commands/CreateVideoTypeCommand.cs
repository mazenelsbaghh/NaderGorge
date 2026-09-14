using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.VideoTypes.Commands;

public record CreateVideoTypeCommand(string Name, int SortOrder, bool IsActive, Guid AdminUserId)
    : IRequest<ApiResponse<VideoTypeDto>>;

public sealed class CreateVideoTypeCommandValidator : AbstractValidator<CreateVideoTypeCommand>
{
    public CreateVideoTypeCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name) && VideoTypeRules.CleanName(name).Length is >= VideoTypeRules.MinNameLength and <= VideoTypeRules.MaxNameLength)
            .WithMessage("اسم النوع يجب أن يكون بين حرفين و80 حرفاً.");
        RuleFor(command => command.SortOrder)
            .InclusiveBetween(VideoTypeRules.MinSortOrder, VideoTypeRules.MaxSortOrder);
    }
}

public sealed class CreateVideoTypeCommandHandler : IRequestHandler<CreateVideoTypeCommand, ApiResponse<VideoTypeDto>>
{
    private readonly IAppDbContext _db;

    public CreateVideoTypeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<VideoTypeDto>> Handle(CreateVideoTypeCommand request, CancellationToken ct)
    {
        var name = VideoTypeRules.CleanName(request.Name);
        var normalizedName = VideoTypeRules.NormalizeName(name);
        if (await _db.VideoTypes.AnyAsync(type => type.NormalizedName == normalizedName, ct))
        {
            return ApiResponse<VideoTypeDto>.Fail("يوجد نوع فيديو بنفس الاسم.", ["VIDEO_TYPE_DUPLICATE"]);
        }

        var type = new VideoType
        {
            Name = name,
            NormalizedName = normalizedName,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        _db.VideoTypes.Add(type);
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CREATE_VIDEO_TYPE",
            EntityType = nameof(VideoType),
            EntityId = type.Id,
            PerformedByUserId = request.AdminUserId,
            NewValues = JsonSerializer.Serialize(new { type.Name, type.SortOrder, type.IsActive })
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (VideoTypeRules.IsDuplicateNameViolation(exception))
        {
            return ApiResponse<VideoTypeDto>.Fail("يوجد نوع فيديو بنفس الاسم.", ["VIDEO_TYPE_DUPLICATE"]);
        }

        return ApiResponse<VideoTypeDto>.Ok(VideoTypeRules.ToDto(type), "تم إنشاء نوع الفيديو.");
    }
}
