using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

internal sealed record ExamQuestionDraw(
    bool IsRandomized, int? DisplayQuestionCount, decimal TotalScore, ExamEssayGradingMode EssayGradingMode)
{
    public const string InvalidMixMessage = "توزيع أسئلة الامتحان ودرجاتها غير مطابق للإعدادات. تواصل مع إدارة الامتحان.";

    public static string MultipleChoiceCountKey(Guid examId) => $"ExamDraw.MultipleChoiceCount.{examId:D}";

    public Task<string?> ReadMultipleChoiceCountAsync(IAppDbContext db, Guid examId, CancellationToken ct)
    {
        if (!IsRandomized)
            return Task.FromResult<string?>(null);

        var key = MultipleChoiceCountKey(examId);
        return db.PlatformSettings.AsNoTracking().Where(setting => setting.Key == key)
            .Select(setting => (string?)setting.Value).SingleOrDefaultAsync(ct);
    }

    public int? ResolveMultipleChoiceCount(string configuredCount, IReadOnlyCollection<ExamDrawQuestion> pool)
    {
        if (!int.TryParse(configuredCount, out var count) || count <= 0 ||
            EssayGradingMode == ExamEssayGradingMode.ModelAnswerOnly)
            return null;

        var choicePoints = pool.Where(question => question.Type == QuestionType.MCQ).Select(question => question.Points).ToArray();
        var essays = pool.Where(question => question.Type == QuestionType.Essay).ToArray();
        if (count > choicePoints.Length || choicePoints.Length + essays.Length != pool.Count ||
            count + essays.Length != DisplayQuestionCount || choicePoints[0] <= 0 ||
            choicePoints.Any(points => points != choicePoints[0]) || essays.Any(essay => essay.Points <= 0))
            return null;

        return count * choicePoints[0] + essays.Sum(essay => essay.Points) == TotalScore ? count : null;
    }

    public static List<ExamQuestion> Sample(IEnumerable<ExamQuestion> pool, int multipleChoiceCount, Random random)
    {
        var choices = pool.Where(question => question.Question.Type == QuestionType.MCQ).ToArray();
        random.Shuffle(choices.AsSpan());
        return choices.Take(multipleChoiceCount)
            .Concat(pool.Where(question => question.Question.Type == QuestionType.Essay)).ToList();
    }
}

internal sealed record ExamDrawQuestion(QuestionType Type, decimal Points);
