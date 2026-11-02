namespace NaderGorge.Application.Features.Assessments;

public static class StudentAnswerPolicy
{
    public const string AudioDisabledMessage = "الإجابات الصوتية غير مسموح بها في الواجب أو الامتحان. اكتب إجابتك ثم أرسلها.";

    public static bool IsAudioReference(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        var reference = answer.Trim();
        if (reference.StartsWith("data:audio/", StringComparison.OrdinalIgnoreCase)
            || reference.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)) return true;
        if (Uri.TryCreate(reference, UriKind.Absolute, out var uri)) reference = Uri.UnescapeDataString(uri.AbsolutePath);
        return reference.Replace('\\', '/').StartsWith("/uploads/audio/", StringComparison.OrdinalIgnoreCase);
    }
}
