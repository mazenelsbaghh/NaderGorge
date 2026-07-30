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
    public string? PublicSlug { get; set; }
    public string? PublicBio { get; set; }
    public string? IntroVideoUrl { get; set; }
    public bool IsPublicProfileEnabled { get; set; }
    public bool ShowOnLanding { get; set; } = true;
    public bool IsVisibleToStudents { get; set; } = true;
    public bool IsContentVisibleToStudents { get; set; } = true;
    public decimal RatingAverage { get; set; }
    public int RatingCount { get; set; }

    // Navigation properties
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<Package> Packages { get; set; } = new List<Package>();
    public ICollection<CodeGroup> CodeGroups { get; set; } = new List<CodeGroup>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    public ICollection<QuestionBankItem> QuestionBankItems { get; set; } = new List<QuestionBankItem>();
    public ICollection<EssaySubmission> EssaySubmissions { get; set; } = new List<EssaySubmission>();
    public ICollection<TeacherFinancialAllocation> FinancialAllocations { get; set; } = new List<TeacherFinancialAllocation>();
    public ICollection<SharedTeacherPackageTeacher> SharedPackageTeachers { get; set; } = new List<SharedTeacherPackageTeacher>();
    public ICollection<SharedTeacherPackageItem> SharedPackageItems { get; set; } = new List<SharedTeacherPackageItem>();
    public ICollection<CommunityPost> CommunityPosts { get; set; } = new List<CommunityPost>();
    public ICollection<TeacherStaffMember> StaffMembers { get; set; } = new List<TeacherStaffMember>();
}
