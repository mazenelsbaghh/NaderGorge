using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

internal sealed record AvailableExamCodeTarget(Guid ExamId, string Title);

internal static class ExamCodeAvailability
{
    internal const string UnavailableMessage = "هذا الامتحان غير متاح حالياً.";
    internal const string UnavailableErrorCode = "EXAM_UNAVAILABLE";

    internal static Task<AvailableExamCodeTarget?> ResolveAsync(
        IAppDbContext db,
        Guid? examId,
        Guid? publicExamProductId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (publicExamProductId.HasValue)
        {
            return db.PublicExamProducts
                .AsNoTracking()
                .Where(product => product.Id == publicExamProductId.Value
                    && product.Exam.IsActive
                    && product.IsPublished
                    && product.DisabledAt == null
                    && (!product.AvailableFrom.HasValue || product.AvailableFrom <= now)
                    && (!product.AvailableUntil.HasValue || product.AvailableUntil > now))
                .Select(product => new AvailableExamCodeTarget(product.ExamId, product.Exam.Title))
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!examId.HasValue)
            return Task.FromResult<AvailableExamCodeTarget?>(null);

        return db.Exams
            .AsNoTracking()
            .Where(exam => exam.Id == examId.Value && exam.IsActive)
            .Select(exam => new AvailableExamCodeTarget(exam.Id, exam.Title))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
