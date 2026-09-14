namespace NaderGorge.Domain.Enums;

public enum ContentArchiveMode
{
    None = 0,
    ActiveSubscribersOnly = 1,
    HiddenFromEveryone = 2
}

public enum ContentArchiveTargetType
{
    Package = 0,
    Term = 1,
    Section = 2,
    Lesson = 3,
    Video = 4,
    Resource = 5,
    Exam = 6,
    Homework = 7
}
