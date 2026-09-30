using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAIActionPreviewUnavailableException(string message) : InvalidOperationException(message);

/// <summary>Read-only, authoritative previews for the implemented identity/content actions.</summary>
public sealed class AdminAIIdentityContentPreviewSource(IAppDbContext db) : IAdminAIActionPreviewSource
{
    public Task<AdminAIActionPreview> PreviewAsync<TInput>(
        string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class =>
        input switch
        {
            AdminAIAddStudentNoteInput note when capabilityKey == "admin.identity.student-note.create" => PreviewNoteAsync(note, ct),
            AdminAIApproveWatchRequestInput watch when capabilityKey == "admin.identity.watch-request.approve" => PreviewWatchRequestAsync(watch, ct),
            AdminAICreateSubjectInput subject when capabilityKey == "admin.content.subject.create" => PreviewCreateSubjectAsync(subject, ct),
            AdminAIUpdateSubjectInput subject when capabilityKey == "admin.content.subject.update" => PreviewUpdateSubjectAsync(subject, ct),
            AdminAICreateVideoTypeInput type when capabilityKey == "admin.content.video-type.create" => PreviewCreateVideoTypeAsync(type, ct),
            AdminAIUpdateVideoTypeInput type when capabilityKey == "admin.content.video-type.update" => PreviewUpdateVideoTypeAsync(type, ct),
            _ => throw new NotSupportedException("Admin AI action preview capability is unavailable.")
        };

    private async Task<AdminAIActionPreview> PreviewNoteAsync(AdminAIAddStudentNoteInput input, CancellationToken ct)
    {
        if (input.StudentId == Guid.Empty || string.IsNullOrWhiteSpace(input.Content) || input.Content.Length > 4000)
            throw new ArgumentException("Student note input is invalid.", nameof(input));
        var user = await db.Users.AsNoTracking()
            .Where(item => item.Id == input.StudentId)
            .Select(item => new { item.Id, item.IsActive, item.IsDeleted, item.SecurityStampVersion })
            .SingleOrDefaultAsync(ct);
        if (user is null || user.IsDeleted)
            throw new AdminAIActionPreviewUnavailableException("The note target is unavailable.");
        return new AdminAIActionPreview(
            "user", $"user:{user.Id:D}",
            new { user.IsActive, user.IsDeleted },
            new { input.Content, input.IsPinned },
            new { noteWillBeAdded = true, affected = 1 },
            new { valid = true },
            Fingerprint("admin.identity.student-note.create", user));
    }

    private async Task<AdminAIActionPreview> PreviewWatchRequestAsync(AdminAIApproveWatchRequestInput input, CancellationToken ct)
    {
        if (input.RequestId == Guid.Empty || input.AddedViews is < 1 or > 1000
            || input.Reason?.Trim().Length > 1000)
            throw new ArgumentException("Watch request approval input is invalid.", nameof(input));
        var request = await db.ExtraWatchRequests.AsNoTracking()
            .Where(item => item.Id == input.RequestId)
            .Select(item => new
            {
                item.Id, item.UserId, item.LessonVideoId, item.Status,
                item.ResolvedAt, item.RejectionReason, item.RequestReason,
                VideoTitle = item.LessonVideo.Title,
                item.LessonVideo.LessonId,
                BaseMaxWatchCount = item.LessonVideo.MaxWatchCount
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The watch request is unavailable.");
        var watch = await db.VideoWatchEvents.AsNoTracking()
            .Where(item => item.UserId == request.UserId && item.LessonVideoId == request.LessonVideoId)
            .Select(item => new
            {
                item.Id, item.WatchCount, item.IsLocked,
                item.CustomMaxWatchCount, item.TimeWatchedInSeconds
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The student's video watch state is unavailable.");
        var currentLimit = watch.CustomMaxWatchCount ?? request.BaseMaxWatchCount;
        if (currentLimit <= 0 || currentLimit > int.MaxValue - input.AddedViews)
            throw new AdminAIActionPreviewUnavailableException("The video watch limit cannot be safely increased.");
        var state = new
        {
            request.Id, request.UserId, request.LessonVideoId, request.Status,
            request.ResolvedAt, request.RejectionReason, request.RequestReason,
            request.VideoTitle, request.LessonId, request.BaseMaxWatchCount,
            WatchEventId = watch.Id, watch.WatchCount, watch.IsLocked, watch.CustomMaxWatchCount,
            watch.TimeWatchedInSeconds
        };
        return new AdminAIActionPreview(
            "watch-request", $"watch-request:{request.Id:D}",
            new
            {
                request.VideoTitle, request.Status, untrustedRequestReason = request.RequestReason,
                watch.WatchCount, watch.IsLocked, currentLimit
            },
            new { input.AddedViews, input.Reason },
            new { statusAfter = "Approved", limitAfter = currentLimit + input.AddedViews,
                studentWillBeUnlocked = true, watchProgressWillBeReset = true,
                studentAndStaffNotificationsWillBeQueued = true, affected = 1 },
            new { valid = true, previouslyApproved = request.Status == NaderGorge.Domain.Enums.RequestStatus.Approved },
            Fingerprint("admin.identity.watch-request.approve", state));
    }

    private async Task<AdminAIActionPreview> PreviewCreateSubjectAsync(AdminAICreateSubjectInput input, CancellationToken ct)
    {
        var name = CleanSubjectName(input.Name);
        var normalized = name.ToUpperInvariant();
        if (await db.Subjects.AsNoTracking().AnyAsync(item => item.NormalizedName == normalized, ct))
            throw new AdminAIActionPreviewUnavailableException("A subject with that name already exists.");
        return new AdminAIActionPreview(
            "subject", $"subject-name:{Fingerprint("subject-name", normalized)[..16]}",
            new { exists = false },
            new { name, description = input.Description?.Trim() ?? string.Empty },
            new { subjectWillBeCreated = true, affected = 1 },
            new { valid = true },
            Fingerprint("admin.content.subject.create", new { normalized, exists = false }));
    }

    private async Task<AdminAIActionPreview> PreviewUpdateSubjectAsync(AdminAIUpdateSubjectInput input, CancellationToken ct)
    {
        if (input.SubjectId == Guid.Empty) throw new ArgumentException("Subject id is required.", nameof(input));
        var name = CleanSubjectName(input.Name);
        var normalized = name.ToUpperInvariant();
        var current = await db.Subjects.AsNoTracking().SingleOrDefaultAsync(item => item.Id == input.SubjectId, ct)
            ?? throw new AdminAIActionPreviewUnavailableException("Subject is unavailable.");
        if (await db.Subjects.AsNoTracking().AnyAsync(item => item.Id != input.SubjectId && item.NormalizedName == normalized, ct))
            throw new AdminAIActionPreviewUnavailableException("Another subject has that name.");
        var state = new { current.Id, current.Name, current.Description, current.NormalizedName };
        return new AdminAIActionPreview(
            "subject", $"subject:{current.Id:D}",
            new { current.Name, current.Description },
            new { name, description = input.Description?.Trim() ?? string.Empty },
            new { subjectWillBeUpdated = true, affected = 1 },
            new { valid = true },
            Fingerprint("admin.content.subject.update", state));
    }

    private async Task<AdminAIActionPreview> PreviewCreateVideoTypeAsync(AdminAICreateVideoTypeInput input, CancellationToken ct)
    {
        var name = CleanVideoTypeName(input.Name, input.SortOrder);
        var normalized = VideoTypeRules.NormalizeName(name);
        if (await db.VideoTypes.AsNoTracking().AnyAsync(item => item.NormalizedName == normalized, ct))
            throw new AdminAIActionPreviewUnavailableException("A video type with that name already exists.");
        return new AdminAIActionPreview(
            "video-type", $"video-type-name:{Fingerprint("video-type-name", normalized)[..16]}",
            new { exists = false },
            new { name, input.SortOrder, input.IsActive },
            new { videoTypeWillBeCreated = true, affected = 1 },
            new { valid = true },
            Fingerprint("admin.content.video-type.create", new { normalized, exists = false }));
    }

    private async Task<AdminAIActionPreview> PreviewUpdateVideoTypeAsync(AdminAIUpdateVideoTypeInput input, CancellationToken ct)
    {
        if (input.VideoTypeId == Guid.Empty) throw new ArgumentException("Video type id is required.", nameof(input));
        var name = CleanVideoTypeName(input.Name, input.SortOrder);
        var normalized = VideoTypeRules.NormalizeName(name);
        var current = await db.VideoTypes.AsNoTracking().SingleOrDefaultAsync(item => item.Id == input.VideoTypeId, ct)
            ?? throw new AdminAIActionPreviewUnavailableException("Video type is unavailable.");
        if (await db.VideoTypes.AsNoTracking().AnyAsync(item => item.Id != input.VideoTypeId && item.NormalizedName == normalized, ct))
            throw new AdminAIActionPreviewUnavailableException("Another video type has that name.");
        var state = new { current.Id, current.Name, current.NormalizedName, current.SortOrder, current.IsActive };
        return new AdminAIActionPreview(
            "video-type", $"video-type:{current.Id:D}",
            new { current.Name, current.SortOrder, current.IsActive },
            new { name, input.SortOrder },
            new { videoTypeWillBeUpdated = true, affected = 1 },
            new { valid = true },
            Fingerprint("admin.content.video-type.update", state));
    }

    private static string CleanSubjectName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new ArgumentException("Subject name is invalid.", nameof(name));
        return name.Trim();
    }

    private static string CleanVideoTypeName(string? name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Video type name is invalid.", nameof(name));
        var clean = VideoTypeRules.CleanName(name);
        if (clean.Length is < VideoTypeRules.MinNameLength or > VideoTypeRules.MaxNameLength
            || sortOrder is < VideoTypeRules.MinSortOrder or > VideoTypeRules.MaxSortOrder)
            throw new ArgumentException("Video type input is outside its authoritative bounds.", nameof(name));
        return clean;
    }

    private static string Fingerprint(string capabilityKey, object state) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { capabilityKey, state })));
}
