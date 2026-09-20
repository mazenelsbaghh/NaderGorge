namespace NaderGorge.Application.Features.Assessments;

public sealed record AssessmentParentRecoveryRequest(
    Guid OperationId,
    string ExpectedCohortFingerprint,
    int MaxBatchSize = 10);

public sealed record AssessmentParentRecoveryPreview(
    int EligibleCount,
    string CohortFingerprint,
    IReadOnlyDictionary<string, int> ExcludedByReason,
    bool AlreadyApplied = false);

public sealed record AssessmentParentRecoveryEnvelope(
    Guid AttemptId,
    Guid DeliveryId,
    string GradeVersion,
    Guid OperationId);

public sealed record AssessmentParentRecoveryStatus(
    Guid OperationId, int Total, int Pending, int Sending, int Sent,
    int Failed, int Skipped, int Uncertain);

public interface IAssessmentParentNotificationRecoveryService
{
    Task<AssessmentParentRecoveryPreview> PreviewAsync(Guid actorId, int maxBatchSize, CancellationToken ct);
    Task<AssessmentParentRecoveryPreview> ApplyAsync(Guid actorId, AssessmentParentRecoveryRequest request, CancellationToken ct);
    Task<AssessmentParentRecoveryStatus> StatusAsync(Guid actorId, Guid operationId, CancellationToken ct);
}
