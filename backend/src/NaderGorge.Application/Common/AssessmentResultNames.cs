namespace NaderGorge.Application.Common;

public static class AssessmentResultNames
{
    public static string StudentName(string fullName) =>
        string.Join(' ', fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(2));
}
