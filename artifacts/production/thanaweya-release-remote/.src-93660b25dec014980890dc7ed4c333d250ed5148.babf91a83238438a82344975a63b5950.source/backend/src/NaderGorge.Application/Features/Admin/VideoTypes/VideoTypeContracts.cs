using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.VideoTypes;

public record VideoTypeDto(
    Guid Id,
    string Name,
    int SortOrder,
    bool IsActive,
    int AssignedVideoCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public static class VideoTypeRules
{
    private const string NormalizedNameIndex = "IX_video_types_NormalizedName";
    public const int MinNameLength = 2;
    public const int MaxNameLength = 80;
    public const int MinSortOrder = 0;
    public const int MaxSortOrder = 10_000;

    public static string CleanName(string name) => string.Join(' ',
        name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static string NormalizeName(string name) => CleanName(name).ToUpperInvariant();

    public static readonly Expression<Func<VideoType, VideoTypeDto>> Projection = type => new VideoTypeDto(
        type.Id,
        type.Name,
        type.SortOrder,
        type.IsActive,
        type.Videos.Count,
        type.CreatedAt,
        type.UpdatedAt);

    public static VideoTypeDto ToDto(VideoType type, int assignedVideoCount = 0) => new(
        type.Id,
        type.Name,
        type.SortOrder,
        type.IsActive,
        assignedVideoCount,
        type.CreatedAt,
        type.UpdatedAt);

    public static Task<bool> IsActiveAsync(IAppDbContext db, Guid videoTypeId, CancellationToken ct) =>
        db.VideoTypes.AnyAsync(type => type.Id == videoTypeId && type.IsActive, ct);

    public static bool IsDuplicateNameViolation(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(NormalizedNameIndex, StringComparison.Ordinal) == true;
}
