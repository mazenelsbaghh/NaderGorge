namespace NaderGorge.Application.Common;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static string ValidationMessage =>
        $"كلمة المرور يجب ألا تقل عن {MinimumLength} أحرف.";

    public static bool IsValid(string? password)
    {
        return !string.IsNullOrWhiteSpace(password) && password.Length >= MinimumLength;
    }
}
