using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class Subject : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<Program> Programs { get; set; } = new List<Program>();
    public ICollection<QuestionBankItem> QuestionBankItems { get; set; } = new List<QuestionBankItem>();
}
