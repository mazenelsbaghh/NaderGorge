namespace NaderGorge.Application.Features.Assessments;

public sealed record ExamParentMessageState(Guid AttemptId, string Status, string? FailureCode, bool CanRetry);
public sealed record ExamParentMessageSummary(bool Enabled, string? ConfigurationError,
    int RetryableCount, int PendingCount, int DeliveredCount, int FailedCount, int NotSentCount,
    int AwaitingDeliveryCount, int UncertainCount, IReadOnlyList<ExamParentMessageState> Attempts);
public sealed record ExamParentMessageRetryRequest(Guid OperationId, Guid? AttemptId = null, bool FailedOnly = false);
public sealed record ExamParentMessageRetryResult(Guid OperationId, int QueuedCount, bool AlreadyQueued);
public sealed record ExamParentMessageRetryEnvelope(Guid ExamId, Guid AttemptId, Guid OperationId);
public sealed record ExamGradeMessageListItem(Guid ExamId, string Title, string TeacherName,
    DateTime CreatedAt, int FinalResultCount);
public sealed record ExamGradeMessageList(int Page, int PageSize, int TotalCount,
    IReadOnlyList<ExamGradeMessageListItem> Items);

public interface IExamParentMessageRetryService
{
    Task<ExamGradeMessageList> ListAsync(Guid actorId, string? search, int page, CancellationToken ct);
    Task<ExamParentMessageSummary> SummaryAsync(Guid actorId, Guid examId, CancellationToken ct);
    Task<ExamParentMessageRetryResult> QueueAsync(Guid actorId, Guid examId, ExamParentMessageRetryRequest request, CancellationToken ct);
}
