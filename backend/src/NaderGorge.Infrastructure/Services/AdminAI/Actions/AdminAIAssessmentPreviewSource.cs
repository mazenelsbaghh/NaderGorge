using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

/// <summary>Read-only moderation previews before public visibility changes.</summary>
public sealed class AdminAIAssessmentPreviewSource(IAppDbContext db, IAcademicScopeService academicScope) : IAdminAIActionPreviewSource
{
    public Task<AdminAIActionPreview> PreviewAsync<TInput>(
        string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class =>
        input switch
        {
            AdminAIApproveLessonCommentInput comment when capabilityKey == "admin.assessment.lesson-comment.approve" =>
                PreviewLessonCommentAsync(capabilityKey, comment, ct),
            AdminAIApproveCommunityPostInput post when capabilityKey == "admin.assessment.community-post.approve" =>
                PreviewCommunityPostAsync(capabilityKey, post, ct),
            AdminAIApproveCommunityCommentInput comment when capabilityKey == "admin.assessment.community-comment.approve" =>
                PreviewCommunityCommentAsync(capabilityKey, comment, ct),
            _ => throw new NotSupportedException("Admin AI action preview capability is unavailable.")
        };

    private async Task<AdminAIActionPreview> PreviewLessonCommentAsync(
        string capabilityKey, AdminAIApproveLessonCommentInput input, CancellationToken ct)
    {
        if (input.CommentId == Guid.Empty)
            throw new ArgumentException("Lesson comment id is required.", nameof(input));
        var comment = await db.LessonComments.AsNoTracking()
            .Where(item => item.Id == input.CommentId)
            .Select(item => new
            {
                item.Id, item.LessonId, item.AuthorUserId, item.ParentCommentId,
                item.Body, item.Status, item.UpdatedAt,
                ParentStatus = item.ParentComment == null
                    ? (LessonCommentStatus?)null : item.ParentComment.Status
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The lesson comment is unavailable.");
        if (comment.Status != LessonCommentStatus.Pending)
            throw new AdminAIActionPreviewUnavailableException("The lesson comment is no longer pending.");
        if (comment.ParentCommentId.HasValue && comment.ParentStatus != LessonCommentStatus.Approved)
            throw new AdminAIActionPreviewUnavailableException("The parent comment must be approved first.");

        var state = new
        {
            comment.Id, comment.LessonId, comment.AuthorUserId, comment.ParentCommentId,
            comment.Body, comment.Status, comment.UpdatedAt, comment.ParentStatus
        };
        return new AdminAIActionPreview(
            "lesson-comment", $"lesson-comment:{comment.Id:D}",
            new { comment.LessonId, comment.ParentCommentId, untrustedBody = comment.Body, comment.Status },
            new { status = LessonCommentStatus.Approved },
            new { visibleInLesson = true, lessonAndAuthorNotificationsWillBeQueued = true, affected = 1 },
            new { valid = true, parentApproved = !comment.ParentCommentId.HasValue || comment.ParentStatus == LessonCommentStatus.Approved },
            Fingerprint(capabilityKey, state));
    }

    private async Task<AdminAIActionPreview> PreviewCommunityPostAsync(
        string capabilityKey, AdminAIApproveCommunityPostInput input, CancellationToken ct)
    {
        if (input.PostId == Guid.Empty)
            throw new ArgumentException("Community post id is required.", nameof(input));
        var post = await db.CommunityPosts.AsNoTracking()
            .Where(item => item.Id == input.PostId)
            .Select(item => new
            {
                item.Id, item.AuthorUserId, item.TeacherId, item.Body,
                item.Status, item.IsPoll, item.UpdatedAt
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The community post is unavailable.");
        if (post.Status != CommunityPostStatus.Pending)
            throw new AdminAIActionPreviewUnavailableException("The community post is no longer pending.");

        IReadOnlyList<StudentFacingAcademicScope> scopes = post.TeacherId.HasValue
            ? [] : await academicScope.ResolveEffectiveScopesAsync(StudentFacingScopeOwnerType.CommunityPost, post.Id, ct);
        if (!post.TeacherId.HasValue && scopes.Count == 0)
            throw new AdminAIActionPreviewUnavailableException("The community post has no academic scope.");
        if (scopes.Count > 32)
            throw new AdminAIActionPreviewUnavailableException("The post has too many academic scopes for a bounded preview.");
        var scopeState = scopes.OrderBy(item => item.Id).Select(item => new
        {
            item.Id, item.OwnerType, item.OwnerId, item.ScopeLevel,
            item.EducationStage, item.GradeLevel, item.SubjectId
        }).ToArray();
        CommunityPostPollOptionSnapshot[] options = post.IsPoll
            ? await db.CommunityPostPollOptions.AsNoTracking()
                .Where(item => item.PostId == post.Id).OrderBy(item => item.Id)
                .Select(item => new CommunityPostPollOptionSnapshot(item.Id, item.Text)).Take(21).ToArrayAsync(ct)
            : [];
        if (options.Length > 20)
            throw new AdminAIActionPreviewUnavailableException("The post has too many poll options for a bounded preview.");

        var state = new
        {
            post.Id, post.AuthorUserId, post.TeacherId, post.Body, post.Status,
            post.IsPoll, post.UpdatedAt, scopeState, options
        };
        return new AdminAIActionPreview(
            "community-post", $"community-post:{post.Id:D}",
            new { untrustedBody = post.Body, post.Status, post.IsPoll, options, post.TeacherId, academicScopes = scopeState },
            new { status = CommunityPostStatus.Approved },
            new { publiclyVisible = true, publicNotificationWillBeQueued = true, affected = 1 },
            new { valid = true, academicScopePresent = post.TeacherId.HasValue || scopes.Count > 0 },
            Fingerprint(capabilityKey, state));
    }

    private async Task<AdminAIActionPreview> PreviewCommunityCommentAsync(
        string capabilityKey, AdminAIApproveCommunityCommentInput input, CancellationToken ct)
    {
        if (input.CommentId == Guid.Empty)
            throw new ArgumentException("Community comment id is required.", nameof(input));
        var comment = await db.CommunityPostComments.AsNoTracking()
            .Where(item => item.Id == input.CommentId)
            .Select(item => new
            {
                item.Id, item.PostId, item.AuthorUserId, item.ParentCommentId,
                item.Body, item.Status, item.RejectionReason, item.UpdatedAt,
                ParentStatus = item.ParentComment == null
                    ? (CommunityCommentStatus?)null : item.ParentComment.Status,
                ParentPostId = item.ParentComment == null
                    ? (Guid?)null : item.ParentComment.PostId,
                PostStatus = item.Post.Status, PostBody = item.Post.Body,
                PostUpdatedAt = item.Post.UpdatedAt, item.Post.TeacherId
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The community comment is unavailable.");
        if (comment.Status != CommunityCommentStatus.Pending)
            throw new AdminAIActionPreviewUnavailableException("The community comment is no longer pending.");
        if (comment.PostStatus != CommunityPostStatus.Approved)
            throw new AdminAIActionPreviewUnavailableException("The parent post must be approved first.");
        if (comment.ParentCommentId.HasValue && (comment.ParentStatus != CommunityCommentStatus.Approved
            || comment.ParentPostId != comment.PostId))
            throw new AdminAIActionPreviewUnavailableException("The parent comment must be approved first.");

        IReadOnlyList<StudentFacingAcademicScope> scopes = comment.TeacherId.HasValue
            ? [] : await academicScope.ResolveEffectiveScopesAsync(StudentFacingScopeOwnerType.CommunityPost, comment.PostId, ct);
        if (!comment.TeacherId.HasValue && scopes.Count == 0)
            throw new AdminAIActionPreviewUnavailableException("The parent post has no academic scope.");
        if (scopes.Count > 32)
            throw new AdminAIActionPreviewUnavailableException("The parent post has too many academic scopes for a bounded preview.");
        var scopeState = scopes.OrderBy(item => item.Id).Select(item => new
        {
            item.Id, item.OwnerType, item.OwnerId, item.ScopeLevel,
            item.EducationStage, item.GradeLevel, item.SubjectId
        }).ToArray();
        var state = new
        {
            comment.Id, comment.PostId, comment.AuthorUserId, comment.ParentCommentId,
            comment.Body, comment.Status, comment.RejectionReason, comment.UpdatedAt,
            comment.ParentStatus, comment.ParentPostId, comment.PostStatus,
            comment.PostBody, comment.PostUpdatedAt, comment.TeacherId, scopeState
        };
        return new AdminAIActionPreview(
            "community-comment", $"community-comment:{comment.Id:D}",
            new
            {
                comment.PostId, comment.ParentCommentId, untrustedBody = comment.Body,
                untrustedPostBody = comment.PostBody, comment.Status, comment.PostStatus,
                comment.TeacherId, academicScopes = scopeState
            },
            new { status = CommunityCommentStatus.Approved },
            new { publiclyVisible = true, publicNotificationWillBeQueued = true, affected = 1 },
            new { valid = true, parentApproved = !comment.ParentCommentId.HasValue
                || comment.ParentStatus == CommunityCommentStatus.Approved },
            Fingerprint(capabilityKey, state));
    }

    private static string Fingerprint(string capabilityKey, object state) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { capabilityKey, state })));

    private sealed record CommunityPostPollOptionSnapshot(Guid Id, string Text);
}
