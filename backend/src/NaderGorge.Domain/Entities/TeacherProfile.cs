using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class TeacherProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Bio { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public decimal CommissionRate { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string ContactInfo { get; set; } = string.Empty;
    public string? AssistantPhoneNumbers { get; set; }
    public string? FacebookUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? TelegramUrl { get; set; }

    // Navigation properties
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<Package> Packages { get; set; } = new List<Package>();
    public ICollection<CodeGroup> CodeGroups { get; set; } = new List<CodeGroup>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    public ICollection<QuestionBankItem> QuestionBankItems { get; set; } = new List<QuestionBankItem>();
    public ICollection<EssaySubmission> EssaySubmissions { get; set; } = new List<EssaySubmission>();
}
