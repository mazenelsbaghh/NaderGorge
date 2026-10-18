using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Assessments;

public static class EssayCreditPolicy
{
    public static bool IsAutomatic(ExamEssayGradingMode mode) => mode is
        ExamEssayGradingMode.Automatic or ExamEssayGradingMode.AutomaticFullCredit
        or ExamEssayGradingMode.AutomaticFullOrHalfCredit
        or ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent;

    public static decimal AwardedPoints(ExamEssayGradingMode mode, decimal maximumPoints, decimal assessedRatio) =>
        Math.Round(maximumPoints * AwardedRatio(mode, assessedRatio), 2, MidpointRounding.AwayFromZero);

    public static bool IsFullyCorrect(ExamEssayGradingMode mode, decimal assessedRatio) =>
        AwardedRatio(mode, assessedRatio) >= 1m;

    private static decimal AwardedRatio(ExamEssayGradingMode mode, decimal assessedRatio) => mode switch
    {
        ExamEssayGradingMode.Automatic => assessedRatio,
        ExamEssayGradingMode.AutomaticFullCredit => assessedRatio == 1m ? 1m : 0m,
        ExamEssayGradingMode.AutomaticFullOrHalfCredit => assessedRatio == 1m ? 1m : assessedRatio > 0m ? 0.5m : 0m,
        ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent => assessedRatio >= 0.7m ? 1m : assessedRatio > 0m ? 0.5m : 0m,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Automatic essay grading is required.")
    };

    public static string Explain(ExamEssayGradingMode mode, string assessmentFeedback) => mode switch
    {
        ExamEssayGradingMode.AutomaticFullCredit => assessmentFeedback
            + "\nإعداد تصحيح المقالي: الدرجة كاملة للإجابة الصحيحة بالكامل، وصفر للإجابة الناقصة أو الخاطئة.",
        ExamEssayGradingMode.AutomaticFullOrHalfCredit => assessmentFeedback
            + "\nإعداد تصحيح المقالي: الدرجة كاملة للإجابة الصحيحة بالكامل، ونصف الدرجة للإجابة الصحيحة جزئيًا، وصفر للإجابة الخاطئة.",
        ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent => assessmentFeedback
            + "\nإعداد تصحيح المقالي: الدرجة كاملة عند صحة ٧٠٪ من الإجابة فأكثر، ونصف الدرجة لما هو صحيح جزئيًا وأقل من ٧٠٪، وصفر للإجابة الخاطئة أو الفارغة.",
        _ => assessmentFeedback
    };
}
