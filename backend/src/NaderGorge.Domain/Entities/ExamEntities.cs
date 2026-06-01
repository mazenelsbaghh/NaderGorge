using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public enum QuestionType
{
    MCQ = 0,
    Essay = 1,
    FindTheMistake = 2
}

public class Exam : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Passing score criteria
    public decimal PassingScore { get; set; }
    
    // Total possible score
    public decimal TotalScore { get; set; }
    
    // Timer properties
    public int? DurationMinutes { get; set; }
    
    // Config properties
    public bool IsMandatory { get; set; } = true;
    public bool IsRandomized { get; set; } = false;
    public int? DisplayQuestionCount { get; set; }
    
    public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
    public ICollection<StudentExamAttempt> Attempts { get; set; } = new List<StudentExamAttempt>();
}

public class QuestionBankItem : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; } = QuestionType.MCQ;
    public decimal DefaultPoints { get; set; } = 1.0m;
    
    // Tags for categorization
    public string Tags { get; set; } = string.Empty;

    // Optional audio explanation
    public string? AudioUrl { get; set; }
    // Written correction for student review
    public string? WrittenCorrection { get; set; }
    // Optional hint to display without penalty
    public string? HintText { get; set; }

    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}

public class EssayQuestion : QuestionBankItem
{
}

public class QuestionOption : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    
    public Guid QuestionBankItemId { get; set; }
    public QuestionBankItem Question { get; set; } = null!;
}

// Junction table for Exams and Questions
public class ExamQuestion : BaseEntity
{
    public Guid ExamId { get; set; }
    public Exam Exam { get; set; } = null!;

    public Guid QuestionBankItemId { get; set; }
    public QuestionBankItem Question { get; set; } = null!;

    public int Order { get; set; }
    public decimal Points { get; set; } // Can override default points
}

public class StudentExamAttempt : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid ExamId { get; set; }
    public Exam Exam { get; set; } = null!;

    public decimal ScoreAchieved { get; set; }
    public bool IsPassed { get; set; }
    
    // Auto-calculated evaluation string (e.g. "ممتاز")
    public string? Evaluation { get; set; }
    
    // Timer enforcement
    public DateTime? StartedAt { get; set; }
    public bool IsTimeExpired { get; set; }

    public ICollection<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
}

public class StudentAnswer : BaseEntity
{
    public Guid StudentExamAttemptId { get; set; }
    public StudentExamAttempt Attempt { get; set; } = null!;

    public Guid ExamQuestionId { get; set; }
    public ExamQuestion ExamQuestion { get; set; } = null!;

    public Guid? SelectedOptionId { get; set; }
    public QuestionOption? SelectedOption { get; set; }
    public string? SubmittedText { get; set; }

    public bool HintUsed { get; set; }
    public bool IsCorrect { get; set; }
    public decimal PointsAwarded { get; set; }
}
