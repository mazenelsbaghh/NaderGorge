using System;

namespace NaderGorge.Application.Services;

public static class GradingEvaluationService
{
    /// <summary>
    /// Calculates the scaled score by mapping the earned points against the available points, scaled to the target TotalScore.
    /// </summary>
    public static decimal CalculateScaledScore(decimal rawPointsEarned, decimal rawPointsPossible, decimal targetTotalScore)
    {
        if (rawPointsPossible == 0 || targetTotalScore == 0) return 0;
        
        // Example: 15 earned out of 20 possible, scale is 100
        // (15 / 20) * 100 = 75
        var ratio = rawPointsEarned / rawPointsPossible;
        var scaled = ratio * targetTotalScore;
        
        return Math.Round(scaled, 2);
    }

    /// <summary>
    /// Determines the evaluation string based on percentage of scaled score against total score.
    /// </summary>
    public static string DetermineEvaluation(decimal scaledScore, decimal passingScore, decimal totalScore)
    {
        if (totalScore == 0) return "غير مقيم";

        if (scaledScore < passingScore)
        {
            return "ضعيف";
        }
        
        var percentage = (scaledScore / totalScore) * 100;
        
        if (percentage >= 90) return "ممتاز";
        if (percentage >= 80) return "جيد جداً";
        if (percentage >= 65) return "جيد";
        
        return "مقبول";
    }
}
