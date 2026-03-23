using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class Exam : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Passing score criteria
    public decimal PassingScore { get; set; }
    
    // Total possible score
    public decimal TotalScore { get; set; }
    
    public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
    public ICollection<StudentExamAttempt> Attempts { get; set; } = new List<StudentExamAttempt>();
}

public class QuestionBankItem : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public decimal DefaultPoints { get; set; } = 1.0m;
    
    // Tags for categorization
    public string Tags { get; set; } = string.Empty;

    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
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

    public ICollection<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
}

public class StudentAnswer : BaseEntity
{
    public Guid StudentExamAttemptId { get; set; }
    public StudentExamAttempt Attempt { get; set; } = null!;

    public Guid ExamQuestionId { get; set; }
    public ExamQuestion ExamQuestion { get; set; } = null!;

    public Guid SelectedOptionId { get; set; }
    public QuestionOption SelectedOption { get; set; } = null!;
    
    public bool IsCorrect { get; set; }
    public decimal PointsAwarded { get; set; }
}
