using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public sealed record SetContentArchiveStateCommand(
    ContentArchiveTargetType TargetType,
    Guid TargetId,
    ContentArchiveMode ArchiveMode,
    Guid CurrentUserId) : IRequest<ApiResponse<ContentArchiveStateDto>>;

public sealed record ContentArchiveStateDto(
    ContentArchiveTargetType TargetType,
    Guid TargetId,
    ContentArchiveMode ArchiveMode,
    DateTime? ArchivedAt);

public sealed class SetContentArchiveStateCommandHandler(
    IAppDbContext db,
    TeacherAuthorizationService authorization)
    : IRequestHandler<SetContentArchiveStateCommand, ApiResponse<ContentArchiveStateDto>>
{
    public async Task<ApiResponse<ContentArchiveStateDto>> Handle(
        SetContentArchiveStateCommand request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.TargetType) || !Enum.IsDefined(request.ArchiveMode))
            return ApiResponse<ContentArchiveStateDto>.Fail("حالة الأرشفة غير صالحة.");

        var target = await FindAuthorizedTargetAsync(request, cancellationToken);
        if (target is null)
            return ApiResponse<ContentArchiveStateDto>.Fail("المحتوى غير موجود أو لا تملك صلاحية إدارته.");

        var previousMode = target.ArchiveMode;
        DateTime? archivedAt = request.ArchiveMode == ContentArchiveMode.None ? null : DateTime.UtcNow;
        target.ArchiveMode = request.ArchiveMode;
        target.ArchivedAt = archivedAt;
        target.ArchivedByUserId = archivedAt.HasValue ? request.CurrentUserId : null;

        db.AuditLogs.Add(new AuditLog
        {
            Action = archivedAt.HasValue ? "ContentArchived" : "ContentRestored",
            EntityType = request.TargetType.ToString(),
            EntityId = request.TargetId,
            PerformedByUserId = request.CurrentUserId,
            OldValues = JsonSerializer.Serialize(new { ArchiveMode = previousMode.ToString() }),
            NewValues = JsonSerializer.Serialize(new { ArchiveMode = request.ArchiveMode.ToString() }),
            CreatedAt = DateTime.UtcNow
        });
        db.OutboxEvents.Add(new OutboxEvent
        {
            Type = archivedAt.HasValue ? "ContentArchived" : "ContentRestored",
            TargetGroup = "Role_Student",
            PayloadJson = JsonSerializer.Serialize(new
            {
                targetType = request.TargetType.ToString(),
                targetId = request.TargetId,
                archiveMode = request.ArchiveMode.ToString()
            })
        });

        await db.SaveChangesAsync(cancellationToken);
        return ApiResponse<ContentArchiveStateDto>.Ok(new(
            request.TargetType,
            request.TargetId,
            request.ArchiveMode,
            archivedAt));
    }

    private async Task<IArchivableContent?> FindAuthorizedTargetAsync(
        SetContentArchiveStateCommand request,
        CancellationToken cancellationToken)
    {
        return request.TargetType switch
        {
            ContentArchiveTargetType.Package => await AuthorizedPackageAsync(request, cancellationToken),
            ContentArchiveTargetType.Term => await AuthorizedTermAsync(request, cancellationToken),
            ContentArchiveTargetType.Section => await AuthorizedSectionAsync(request, cancellationToken),
            ContentArchiveTargetType.Lesson => await AuthorizedLessonAsync(request, cancellationToken),
            ContentArchiveTargetType.Video => await AuthorizedVideoAsync(request, cancellationToken),
            ContentArchiveTargetType.Resource => await AuthorizedResourceAsync(request, cancellationToken),
            ContentArchiveTargetType.Exam => await AuthorizedExamAsync(request, cancellationToken),
            ContentArchiveTargetType.Homework => await AuthorizedHomeworkAsync(request, cancellationToken),
            _ => null
        };
    }

    private async Task<IArchivableContent?> AuthorizedPackageAsync(SetContentArchiveStateCommand request, CancellationToken ct)
    {
        if (!await authorization.CanAccessPackageAsync(request.CurrentUserId, request.TargetId, ct)) return null;
        return await db.Packages.FindAsync([request.TargetId], ct);
    }

    private async Task<IArchivableContent?> AuthorizedTermAsync(SetContentArchiveStateCommand request, CancellationToken ct)
    {
        if (!await authorization.CanAccessTermAsync(request.CurrentUserId, request.TargetId, ct)) return null;
        return await db.Terms.FindAsync([request.TargetId], ct);
    }

    private async Task<IArchivableContent?> AuthorizedSectionAsync(SetContentArchiveStateCommand request, CancellationToken ct)
    {
        if (!await authorization.CanAccessSectionAsync(request.CurrentUserId, request.TargetId, ct)) return null;
        return await db.ContentSections.FindAsync([request.TargetId], ct);
    }

    private async Task<IArchivableContent?> AuthorizedLessonAsync(SetContentArchiveStateCommand request, CancellationToken ct)
    {
        if (!await authorization.CanAccessLessonAsync(request.CurrentUserId, request.TargetId, ct)) return null;
        return await db.Lessons.FindAsync([request.TargetId], ct);
    }

    private async Task<IArchivableContent?> AuthorizedVideoAsync(SetContentArchiveStateCommand request, CancellationToken ct)
    {
        var video = await db.LessonVideos.FirstOrDefaultAsync(video => video.Id == request.TargetId, ct);
        if (video is null || !await authorization.CanAccessLessonAsync(request.CurrentUserId, video.LessonId, ct)) return null;
        return video;
    }

    private async Task<IArchivableContent?> AuthorizedResourceAsync(SetContentArchiveStateCommand request, CancellationToken ct)
    {
        var resource = await db.LessonResources.FirstOrDefaultAsync(resource => resource.Id == request.TargetId, ct);
        if (resource is null || !await authorization.CanAccessLessonAsync(request.CurrentUserId, resource.LessonId, ct)) return null;
        return resource;
    }

    private async Task<IArchivableContent?> AuthorizedExamAsync(SetContentArchiveStateCommand request, CancellationToken ct)
    {
        if (!await authorization.CanAccessExamAsync(request.CurrentUserId, request.TargetId, ct)) return null;
        return await db.Exams.FindAsync([request.TargetId], ct);
    }

    private async Task<IArchivableContent?> AuthorizedHomeworkAsync(SetContentArchiveStateCommand request, CancellationToken ct)
    {
        var homework = await db.Homeworks.FirstOrDefaultAsync(homework => homework.Id == request.TargetId, ct);
        if (homework is null || !await authorization.CanAccessLessonAsync(request.CurrentUserId, homework.LessonId, ct)) return null;
        return homework;
    }
}
