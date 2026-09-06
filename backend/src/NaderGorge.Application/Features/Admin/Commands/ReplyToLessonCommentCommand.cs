using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

// Controllers enforce comments.manage or the teacher's scoped comments permission.
public record ReplyToLessonCommentCommand(Guid CommentId, Guid AuthorUserId, string? Body, Guid? TeacherId = null)
    : IRequest<ApiResponse<ModerationLessonCommentDto>>;

public class ReplyToLessonCommentCommandHandler(IAppDbContext db)
    : IRequestHandler<ReplyToLessonCommentCommand, ApiResponse<ModerationLessonCommentDto>>
{
    public async Task<ApiResponse<ModerationLessonCommentDto>> Handle(ReplyToLessonCommentCommand request, CancellationToken ct)
    {
        var original = await db.LessonComments.Include(c => c.ParentComment)
            .FirstOrDefaultAsync(c => c.Id == request.CommentId
                && (!request.TeacherId.HasValue || c.Lesson.ContentSection.Term.Package.TeacherId == request.TeacherId), ct);
        if (original == null || original.Status == LessonCommentStatus.Rejected
            || original.ParentComment?.Status == LessonCommentStatus.Rejected)
            return ApiResponse<ModerationLessonCommentDto>.Fail("التعليق غير متاح للرد.", ["NOT_FOUND"]);
        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000)
            return ApiResponse<ModerationLessonCommentDto>.Fail("الرد مطلوب وبحد أقصى 2000 حرف.", ["VALIDATION_BODY"]);

        var reply = new LessonComment
        {
            LessonId = original.LessonId, ParentCommentId = original.ParentCommentId ?? original.Id,
            AuthorUserId = request.AuthorUserId, Body = body, Status = LessonCommentStatus.Approved,
            ReviewedAt = DateTime.UtcNow, ReviewedByUserId = request.AuthorUserId,
        };
        db.LessonComments.Add(reply);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "ReplyToLessonComment", EntityType = nameof(LessonComment), EntityId = reply.Id,
            PerformedByUserId = request.AuthorUserId,
            NewValues = $"LessonId={reply.LessonId};ParentCommentId={reply.ParentCommentId};BodyLength={body.Length}",
        });
        db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "LessonCommentApproved", TargetGroup = $"Lesson_{reply.LessonId}",
            PayloadJson = JsonSerializer.Serialize(new { commentId = reply.Id, lessonId = reply.LessonId, parentCommentId = reply.ParentCommentId }),
        });
        await db.SaveChangesAsync(ct);
        var response = await db.LessonComments.Where(c => c.Id == reply.Id)
            .Select(c => new ModerationLessonCommentDto(c.Id, c.LessonId, c.Lesson.Title,
                c.Lesson.ContentSection.Term.Package.Teacher.User.FullName, c.Lesson.ContentSection.Term.Package.Name,
                c.Lesson.ContentSection.Term.Title, c.Lesson.ContentSection.Title, c.AuthorUserId, c.AuthorUser.FullName,
                c.Body, c.Status.ToString(), c.CreatedAt, c.ReviewedAt, c.ReviewedByUser != null ? c.ReviewedByUser.FullName : null) { ParentCommentId = c.ParentCommentId, ParentBody = c.ParentComment != null ? c.ParentComment.Body : null })
            .SingleAsync(ct);
        return ApiResponse<ModerationLessonCommentDto>.Ok(response);
    }
}
