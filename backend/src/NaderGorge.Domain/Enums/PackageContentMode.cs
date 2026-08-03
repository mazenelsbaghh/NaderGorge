namespace NaderGorge.Domain.Enums;

/// <summary>
/// Describes the visible shape of a package's course content.
/// System containers keep the existing database hierarchy compatible while
/// allowing sections and lessons to be presented directly under a package.
/// </summary>
public enum PackageContentMode
{
    TermOnly = 0,
    SectionOnly = 1,
    TermWithSections = 2,
    SectionWithLessons = 3,
    LessonsOnly = 4
}
