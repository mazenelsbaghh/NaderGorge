namespace NaderGorge.Domain.Entities;

// Drafts never participate in grading or in the detection of a submitted attempt.
public sealed class StudentExamDraft
{
    public Guid AttemptId { get; set; }
    public StudentExamAttempt Attempt { get; set; } = null!;
    public Guid RevisionId { get; set; }
    public long Version { get; set; }
    public Guid LastMutationId { get; set; }
    public string AnswersJson { get; set; } = "{}";
    public DateTime SavedAt { get; set; }
}
