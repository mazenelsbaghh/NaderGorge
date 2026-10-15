using NaderGorge.Domain.Entities.Homework;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;

namespace NaderGorge.Application.Features.Homework;

public static class HomeworkProgressPolicy
{
    public static bool AllowsContinuation(HomeworkEntity homework, HomeworkSubmission? submission) =>
        submission is not null && (
            submission.Status == SubmissionStatus.Graded
                && submission.OverallScore >= (submission.PassingScoreSnapshot ?? homework.PassingScoreThreshold ?? 0)
            || homework.ShowResultsWhileEssayPending && submission.Status == SubmissionStatus.PendingReview
                && submission.SubmittedAt.HasValue);
}
