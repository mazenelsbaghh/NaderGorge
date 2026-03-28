namespace NaderGorge.Domain.Enums;

public enum CodeType
{
    Package = 0,    // Full package/year access
    Term = 1,       // Specific term access
    Month = 2,      // Specific content section/month access
    Lesson = 3,     // Specific lesson access
    Video = 4,      // Specific video(s) access
    Exam = 5,       // Specific exam access
    Balance = 6     // Credit added to student balance
}
