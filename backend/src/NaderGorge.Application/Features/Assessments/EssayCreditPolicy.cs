using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Assessments;

public static class EssayCreditPolicy
{
    public static bool IsAutomatic(ExamEssayGradingMode mode) => mode is
        ExamEssayGradingMode.Automatic or ExamEssayGradingMode.AutomaticFullCredit
        or ExamEssayGradingMode.AutomaticFullOrHalfCredit;

    public static decimal AwardedPoints(ExamEssayGradingMode mode, decimal maximumPoints, decimal assessedRatio)
    {
        var awardedRatio = mode switch
        {
            ExamEssayGradingMode.Automatic => assessedRatio,
            ExamEssayGradingMode.AutomaticFullCredit => assessedRatio == 1m ? 1m : 0m,
            ExamEssayGradingMode.AutomaticFullOrHalfCredit => assessedRatio == 1m ? 1m : assessedRatio > 0m ? 0.5m : 0m,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Automatic essay grading is required.")
        };
        return Math.Round(maximumPoints * awardedRatio, 2, MidpointRounding.AwayFromZero);
    }

    public static string Explain(ExamEssayGradingMode mode, string assessmentFeedback) => mode switch
    {
        ExamEssayGradingMode.AutomaticFullCredit => assessmentFeedback
            + "\nإعداد تصحيح المقالي: الدرجة كاملة للإجابة الصحيحة بالكامل، وصفر للإجابة الناقصة أو الخاطئة.",
        ExamEssayGradingMode.AutomaticFullOrHalfCredit => assessmentFeedback
            + "\nإعداد تصحيح المقالي: الدرجة كاملة للإجابة الصحيحة بالكامل، ونصف الدرجة للإجابة الصحيحة جزئيًا، وصفر للإجابة الخاطئة.",
        _ => assessmentFeedback
    };
}
