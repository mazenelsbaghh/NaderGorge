namespace NaderGorge.Application.Services;

public static class RechargePhoneSimilarity
{
    public const int ConfirmationThreshold = 8;

    public static int LongestCommonDigitSequence(string? first, string? second)
    {
        var left = Normalize(first);
        var right = Normalize(second);
        if (left.Length == 0 || right.Length == 0)
            return 0;

        var previous = new int[right.Length + 1];
        var longest = 0;

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            var current = new int[right.Length + 1];
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                if (left[leftIndex - 1] != right[rightIndex - 1])
                    continue;

                current[rightIndex] = previous[rightIndex - 1] + 1;
                longest = Math.Max(longest, current[rightIndex]);
            }
            previous = current;
        }

        return longest;
    }

    public static bool RequiresConfirmation(string? submittedPhone, string? receivedPhone) =>
        !string.Equals(Normalize(submittedPhone), Normalize(receivedPhone), StringComparison.Ordinal)
        && LongestCommonDigitSequence(submittedPhone, receivedPhone) >= ConfirmationThreshold;

    private static string Normalize(string? value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
