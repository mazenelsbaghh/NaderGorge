namespace NaderGorge.Domain.Entities;

public class TeacherSubject
{
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;

    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}
