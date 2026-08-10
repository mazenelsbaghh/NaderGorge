namespace NaderGorge.Application.Services;

public readonly record struct RechargePhoneSimilarityAnalysis(
    int LongestCommonDigitSequence,
    int AlignedMatchingDigits,
    bool HasSingleDigitMismatchPattern,
    int MatchingDigitsBeforeMismatch,
    int MatchingDigitsAfterMismatch,
    bool IsExactMatch)
{
    public bool RequiresConfirmation =>
        !IsExactMatch
        && (LongestCommonDigitSequence >= RechargePhoneSimilarity.ConfirmationThreshold
            || HasSingleDigitMismatchPattern);
}

public static class RechargePhoneSimilarity
{
    public const int ConfirmationThreshold = 8;
    public const int SingleMismatchContextThreshold = 4;

    public static int LongestCommonDigitSequence(string? first, string? second)
    {
        var left = Normalize(first);
        var right = Normalize(second);
        return ComputeLongestCommonDigitSequence(left, right);
    }

    public static RechargePhoneSimilarityAnalysis Analyze(string? first, string? second)
    {
        var left = Normalize(first);
        var right = Normalize(second);
        var isExactMatch = string.Equals(left, right, StringComparison.Ordinal);
        var longestSequence = ComputeLongestCommonDigitSequence(left, right);

        if (left.Length == 0 || left.Length != right.Length)
        {
            return new RechargePhoneSimilarityAnalysis(
                longestSequence,
                AlignedMatchingDigits: 0,
                HasSingleDigitMismatchPattern: false,
                MatchingDigitsBeforeMismatch: 0,
                MatchingDigitsAfterMismatch: 0,
                isExactMatch);
        }

        var mismatchCount = 0;
        var mismatchIndex = -1;
        for (var index = 0; index < left.Length; index++)
        {
            if (left[index] == right[index])
                continue;

            mismatchCount++;
            mismatchIndex = index;
        }

        var alignedMatchingDigits = left.Length - mismatchCount;
        if (mismatchCount != 1)
        {
            return new RechargePhoneSimilarityAnalysis(
                longestSequence,
                alignedMatchingDigits,
                HasSingleDigitMismatchPattern: false,
                MatchingDigitsBeforeMismatch: 0,
                MatchingDigitsAfterMismatch: 0,
                isExactMatch);
        }

        var matchingBefore = mismatchIndex;
        var matchingAfter = left.Length - mismatchIndex - 1;
        var hasSingleMismatchPattern =
            matchingBefore >= SingleMismatchContextThreshold
            && matchingAfter >= SingleMismatchContextThreshold;

        return new RechargePhoneSimilarityAnalysis(
            longestSequence,
            alignedMatchingDigits,
            hasSingleMismatchPattern,
            matchingBefore,
            matchingAfter,
            isExactMatch);
    }

    public static bool RequiresConfirmation(string? submittedPhone, string? receivedPhone) =>
        Analyze(submittedPhone, receivedPhone).RequiresConfirmation;

    private static int ComputeLongestCommonDigitSequence(string left, string right)
    {
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

    private static string Normalize(string? value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
