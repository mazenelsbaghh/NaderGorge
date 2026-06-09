using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public enum EssaySubmissionStatus
{
    WaitAI = 0,
    AIScored = 1,
    WaitTeacher = 2,
    TeacherGraded = 3
}

public class EssaySubmission : BaseEntity
{
    // The student who submitted this essay.
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    // The question being answered.
    public Guid QuestionId { get; set; }
    public QuestionBankItem Question { get; set; } = null!;

    // Link to the specific exam attempt. (Added to tie it to the attempt properly)
    public Guid StudentExamAttemptId { get; set; }
    public StudentExamAttempt Attempt { get; set; } = null!;

    // Student's answer text
    public string AnswerText { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }

    // AI grading fields
    public decimal? AiInitialScore { get; set; }
    public string? AiFeedback { get; set; }

    // Teacher grading fields
    public decimal? TeacherFinalScore { get; set; }
    public string? TeacherFeedback { get; set; }
    public Guid? GradedByTeacherId { get; set; }
    public TeacherProfile? GradedByTeacher { get; set; }

    public EssaySubmissionStatus Status { get; set; } = EssaySubmissionStatus.WaitAI;
}
