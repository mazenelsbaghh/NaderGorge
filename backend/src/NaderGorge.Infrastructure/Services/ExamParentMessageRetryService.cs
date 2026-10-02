using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services;

public sealed class ExamParentMessageRetryService(AppDbContext db) : IExamParentMessageRetryService
{
    public const string EventType = "AssessmentParentRetry";
    internal const string AuditAction = "ExamParentRetryQueued";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public async Task<ExamGradeMessageList> ListAsync(Guid actorId, string? search, int page, CancellationToken ct)
    {
        await RequireAdminAsync(actorId, ct);
        const int pageSize = 20;
        if (page < 1 || page > 100000 || search?.Length > 160)
            throw new ArgumentException("راجع رقم الصفحة أو اختصر نص البحث.");
        var query = db.Exams.AsNoTracking();
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(exam => exam.Title.Contains(term) || exam.CreatedByTeacher.User.FullName.Contains(term));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(exam => exam.CreatedAt).ThenByDescending(exam => exam.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(exam => new ExamGradeMessageListItem(exam.Id, exam.Title, exam.CreatedByTeacher.User.FullName,
                exam.CreatedAt, exam.Attempts.Count(attempt => attempt.DefinitionSnapshotJson != null
                    && attempt.Evaluation != null && attempt.Evaluation.Trim() != "" && attempt.Evaluation != "قيد التصحيح")))
            .ToListAsync(ct);
        return new(page, pageSize, total, items);
    }

    public async Task<ExamParentMessageSummary> SummaryAsync(Guid actorId, Guid examId, CancellationToken ct)
    {
        await RequireAdminAsync(actorId, ct);
        return await ReadSummaryAsync(examId, ct);
    }

    public async Task<ExamParentMessageRetryResult> QueueAsync(Guid actorId, Guid examId,
        ExamParentMessageRetryRequest request, CancellationToken ct)
    {
        await RequireAdminAsync(actorId, ct);
        if (request.OperationId == Guid.Empty || request.AttemptId == Guid.Empty)
            throw new ArgumentException("راجع طلب إعادة إرسال الرسائل.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"exam-parent-retry-operation:" + request.OperationId}, 0))", ct);
        // Serialize retry clicks for this exam without holding a lock while contacting Meta.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"exam-parent-retry:" + examId}, 0))", ct);
        var correlation = request.OperationId.ToString("N");
        var previous = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Action == AuditAction && item.CorrelationId == correlation, ct);
        if (previous is not null)
        {
            if (previous.EntityId != examId || previous.PerformedByUserId != actorId)
                throw new ArgumentException("طلب إعادة الإرسال لا يطابق هذا الامتحان.");
            using var values = JsonDocument.Parse(previous.NewValues!);
            var priorAttempt = values.RootElement.GetProperty("requestedAttemptId");
            if ((priorAttempt.ValueKind == JsonValueKind.Null ? (Guid?)null : priorAttempt.GetGuid()) != request.AttemptId)
                throw new ArgumentException("طلب إعادة الإرسال لا يطابق المحاولة.");
            var priorFailedOnly = values.RootElement.TryGetProperty("failedOnly", out var failedOnly) && failedOnly.GetBoolean();
            if (priorFailedOnly != request.FailedOnly)
                throw new ArgumentException("طلب إعادة الإرسال لا يطابق نوع الرسائل.");
            await transaction.CommitAsync(ct);
            return new(request.OperationId, values.RootElement.GetProperty("attemptIds").GetArrayLength(), true);
        }
        var summary = await ReadSummaryAsync(examId, ct);
        if (!summary.Enabled || summary.ConfigurationError is not null)
            throw new ArgumentException(summary.ConfigurationError ?? "فعّل رسالة النتيجة لولي الأمر من إعدادات الامتحان أولًا.");
        var ids = summary.Attempts.Where(item => item.CanRetry
                && (!request.FailedOnly || item.Status == "Failed")
                && (!request.AttemptId.HasValue || item.AttemptId == request.AttemptId))
            .Select(item => item.AttemptId).ToArray();
        var students = await db.StudentExamAttempts.AsNoTracking()
            .Where(item => item.ExamId == examId && ids.Contains(item.Id))
            .Select(item => new { item.Id, item.UserId }).ToListAsync(ct);
        foreach (var student in students)
            db.OutboxEvents.Add(new OutboxEvent
            {
                Type = EventType, TargetUserId = student.UserId.ToString(),
                PayloadJson = JsonSerializer.Serialize(new ExamParentMessageRetryEnvelope(examId, student.Id, request.OperationId), WebJson)
            });
        db.AuditLogs.Add(new AuditLog
        {
            Action = AuditAction, EntityType = "Exam", EntityId = examId, PerformedByUserId = actorId,
            CorrelationId = correlation, Reason = "إعادة إرسال رسائل نتائج الامتحان التي فشلت أو لم تُرسل",
            NewValues = JsonSerializer.Serialize(new { requestedAttemptId = request.AttemptId, failedOnly = request.FailedOnly, attemptIds = ids })
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(request.OperationId, ids.Length, false);
    }

    private async Task RequireAdminAsync(Guid actorId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == actorId && user.IsActive && !user.IsDeleted
                && user.UserRoles.Any(role => role.Role.Type == RoleType.Admin), ct))
            throw new UnauthorizedAccessException("إعادة إرسال نتائج واتساب متاحة للأدمن فقط.");
    }

    private async Task<ExamParentMessageSummary> ReadSummaryAsync(Guid examId, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == examId, ct)
            ?? throw new ArgumentException("الامتحان غير موجود.");
        var settings = AssessmentParentNotificationSettings.Read(exam.ParentNotificationSettingsJson);
        var configurationError = await settings.ValidateAsync(db, ct);
        var attempts = await db.StudentExamAttempts.AsNoTracking().Where(item => item.ExamId == examId)
            .Select(item => new { item.Id, item.Evaluation, HasSnapshot = item.DefinitionSnapshotJson != null,
                ActiveStudent = item.User.IsActive && !item.User.IsDeleted }).ToListAsync(ct);
        var deliveries = await db.AssessmentParentDeliveries.AsNoTracking()
            .Where(item => item.AssessmentKind == "exam" && item.AssessmentId == examId)
            .Select(item => new { item.AttemptId, item.Status, item.FailureCode,
                Receipt = db.LiveSupportWhatsAppPendingReceipts.Where(receipt => receipt.MetaMessageId == item.MetaMessageId)
                    .Select(receipt => receipt.Status).FirstOrDefault(),
                ReceiptFailure = db.LiveSupportWhatsAppPendingReceipts.Where(receipt => receipt.MetaMessageId == item.MetaMessageId)
                    .Select(receipt => receipt.FailureCode).FirstOrDefault() }).ToDictionaryAsync(item => item.AttemptId, ct);
        var examText = examId.ToString();
        var events = await db.OutboxEvents.AsNoTracking().Where(item =>
                (item.Type == "ExamGraded" || item.Type == EventType && item.ProcessedAt == null && !item.IsDeadLetter)
                && item.PayloadJson.Contains(examText))
            .Select(item => new { item.Type, item.PayloadJson, item.CreatedAt }).ToListAsync(ct);
        var eligibleGraded = new HashSet<Guid>();
        var queued = new HashSet<Guid>();
        foreach (var item in events)
        {
            using var payload = JsonDocument.Parse(item.PayloadJson);
            if (!payload.RootElement.TryGetProperty("attemptId", out var id) || !id.TryGetGuid(out var attemptId)) continue;
            if (item.Type == EventType) queued.Add(attemptId);
            else if (exam.ParentNotificationEnabledAt.HasValue && item.CreatedAt >= exam.ParentNotificationEnabledAt.Value)
                eligibleGraded.Add(attemptId);
        }
        var rows = attempts.Select(attempt =>
        {
            var ready = attempt.ActiveStudent && attempt.HasSnapshot && !string.IsNullOrWhiteSpace(attempt.Evaluation)
                && attempt.Evaluation != "قيد التصحيح";
            var delivery = deliveries.GetValueOrDefault(attempt.Id);
            var status = delivery is null ? (ready ? "NotSent" : "NotReady") : delivery.Status.ToString();
            var failure = delivery?.FailureCode;
            if (delivery?.Receipt is "Delivered" or "Read") { status = delivery.Receipt; failure = null; }
            else if (delivery?.Receipt == "Failed") { status = "Failed"; failure = delivery.ReceiptFailure; }
            if (queued.Contains(attempt.Id)) status = "Pending";
            var retryable = settings.Enabled && configurationError is null && ready
                && (status == "Failed" || status == "NotSent" && eligibleGraded.Contains(attempt.Id));
            return new ExamParentMessageState(attempt.Id, status, failure, retryable);
        }).ToArray();
        return new(settings.Enabled, configurationError, rows.Count(item => item.CanRetry),
            rows.Count(item => item.Status is "Pending" or "Sending"),
            rows.Count(item => item.Status is "Delivered" or "Read"), rows.Count(item => item.Status == "Failed"),
            rows.Count(item => item.Status == "NotSent"), rows.Count(item => item.Status == "Sent"),
            rows.Count(item => item.Status == "Uncertain"), rows);
    }
}
