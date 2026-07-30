using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Commands;

public record CreateLessonCommentResponse(
    Guid Id,
    string Status,
    DateTime CreatedAt,
    string Message
);

public record CreateLessonCommentCommand(Guid LessonId, Guid UserId, string Body)
    : IRequest<ApiResponse<CreateLessonCommentResponse>>;

public class CreateLessonCommentCommandHandler : IRequestHandler<CreateLessonCommentCommand, ApiResponse<CreateLessonCommentResponse>>
{
    private const int MaxCommentLength = 2000;

    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public CreateLessonCommentCommandHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<CreateLessonCommentResponse>> Handle(CreateLessonCommentCommand request, CancellationToken ct)
    {
        var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, request.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<CreateLessonCommentResponse>.Fail("You do not have access to this lesson.", new List<string> { "FORBIDDEN" });

        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == request.LessonId, ct);
        if (!lessonExists)
            return ApiResponse<CreateLessonCommentResponse>.Fail("Lesson not found", new List<string> { "NOT_FOUND" });

        var trimmedBody = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBody))
            return ApiResponse<CreateLessonCommentResponse>.Fail("Comment body is required.", new List<string> { "VALIDATION_EMPTY_BODY" });

        if (trimmedBody.Length > MaxCommentLength)
            return ApiResponse<CreateLessonCommentResponse>.Fail($"Comment body must be {MaxCommentLength} characters or fewer.", new List<string> { "VALIDATION_BODY_TOO_LONG" });

        var comment = new LessonComment
        {
            LessonId = request.LessonId,
            AuthorUserId = request.UserId,
            Body = trimmedBody,
            Status = LessonCommentStatus.Pending,
        };

        _db.LessonComments.Add(comment);
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CreateLessonComment",
            EntityType = nameof(LessonComment),
            EntityId = comment.Id,
            PerformedByUserId = request.UserId,
            NewValues = $"LessonId={request.LessonId};Status={LessonCommentStatus.Pending};BodyLength={trimmedBody.Length}",
        });

        var teacherUserId = await _db.Lessons
            .Where(l => l.Id == request.LessonId)
            .Select(l => (string?)l.ContentSection.Term.Package.Teacher.UserId.ToString())
            .FirstOrDefaultAsync(ct);

        var adminEvent = new OutboxEvent
        {
            Type = "LessonCommentCreated",
            TargetGroup = "Role_Admin",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                commentId = comment.Id,
                lessonId = request.LessonId,
                authorUserId = request.UserId,
                body = trimmedBody,
                status = comment.Status.ToString()
            })
        };
        _db.OutboxEvents.Add(adminEvent);

        if (teacherUserId != null)
        {
            var teacherEvent = new OutboxEvent
            {
                Type = "LessonCommentCreated",
                TargetUserId = teacherUserId,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    commentId = comment.Id,
                    lessonId = request.LessonId,
                    authorUserId = request.UserId,
                    body = trimmedBody,
                    status = comment.Status.ToString()
                })
            };
            _db.OutboxEvents.Add(teacherEvent);
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<CreateLessonCommentResponse>.Ok(
            new CreateLessonCommentResponse(
                comment.Id,
                comment.Status.ToString(),
                comment.CreatedAt,
                "تم إرسال التعليق وهو الآن في انتظار المراجعة."
            )
        );
    }
}
