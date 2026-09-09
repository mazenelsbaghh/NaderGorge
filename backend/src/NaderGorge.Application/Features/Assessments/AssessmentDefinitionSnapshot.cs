using System.Text.Json;
using NaderGorge.Domain.Entities;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;
using HomeworkQuestion = NaderGorge.Domain.Entities.Homework.HomeworkQuestion;

namespace NaderGorge.Application.Features.Assessments;

public sealed record AssessmentOptionSnapshot(Guid Id, string Text, bool IsCorrect);

public sealed record AssessmentQuestionSnapshot(
    Guid Id, Guid BankQuestionId, int Order, int Type, string Text, decimal Points,
    string? AudioUrl, string? ImageUrl, string? WrittenCorrection, string? HintText,
    string? BaseText, int? MistakeStartIndex, int? MistakeEndIndex,
    AssessmentOptionSnapshot[] Options, string? CorrectAnswerKey);

public sealed record AssessmentDefinitionSnapshot(
    int SchemaVersion, string Kind, Guid AssessmentId, string Title, string? Description,
    decimal TotalScore, decimal? PassingScore, int? DurationMinutes,
    bool IsMandatory, bool IsRandomized, int? DisplayQuestionCount,
    AssessmentQuestionSnapshot[] Questions)
{
    public AssessmentQuestionSnapshot[] ReserveQuestions { get; init; } = [];
    public AttemptRevisionPlan? Revision { get; init; }
    public DateTime? CompletionStartedAt { get; init; }
    public Guid? RevisionId { get; init; }
    public bool IsActive { get; init; } = true;

    public string ToJson() => JsonSerializer.Serialize(this);

    public AssessmentDefinitionSnapshot WithGrades(IReadOnlyDictionary<Guid, (decimal Points, bool Manual)> scores) => Revision is null
        ? this
        : this with
        {
            Revision = Revision with { Answers = Revision.Answers.Select(answer =>
                !answer.Excluded && !answer.RequiresCompletion && scores.TryGetValue(answer.QuestionId, out var score)
                    ? answer with { AwardedPoints = score.Points, ManuallyGraded = score.Manual }
                    : answer).ToArray() }
        };

    public static bool ContainsEssay(string snapshotJson, string kind, Guid assessmentId) =>
        Read(snapshotJson, kind, assessmentId).Questions.Any(q => q.Type == (int)QuestionType.Essay);

    public static AssessmentDefinitionSnapshot FromExam(Exam exam, IEnumerable<ExamQuestion>? assignedQuestions = null) => new(
        1, "exam", exam.Id, exam.Title, exam.Description, exam.TotalScore, exam.PassingScore,
        exam.DurationMinutes, exam.IsMandatory, exam.IsRandomized, exam.DisplayQuestionCount,
        (assignedQuestions ?? exam.ExamQuestions).Where(x => !x.IsRetired).OrderBy(x => x.Order).ThenBy(x => x.Id).Select(FromExamQuestion).ToArray())
    {
        IsActive = exam.IsActive,
        ReserveQuestions = assignedQuestions is null ? [] : exam.ExamQuestions
            .Where(q => !q.IsRetired && !assignedQuestions.Any(assigned => assigned.Id == q.Id))
            .OrderBy(q => q.Order).ThenBy(q => q.Id).Select(FromExamQuestion).ToArray()
    };

    public static IReadOnlyList<ExamQuestion> ResolveSwapCandidates(Exam current, string? snapshotJson)
    {
        if (snapshotJson is null) return current.ExamQuestions.Where(q => !q.IsRetired).ToList();
        return Read(snapshotJson, "exam", current.Id).ReserveQuestions
            .Select(q => ToExamQuestion(q, current.Id)).ToArray();
    }

    public static string SwapAssignedQuestion(string snapshotJson, Guid examId, Guid previousId, Guid replacementId)
    {
        var snapshot = Read(snapshotJson, "exam", examId);
        var previous = snapshot.Questions.Single(q => q.Id == previousId);
        var replacement = snapshot.ReserveQuestions.Single(q => q.Id == replacementId);
        return (snapshot with
        {
            Questions = snapshot.Questions.Select(q => q.Id == previousId ? replacement : q).ToArray(),
            ReserveQuestions = snapshot.ReserveQuestions.Where(q => q.Id != replacementId).Append(previous).ToArray(),
            Revision = snapshot.Revision is null ? null : snapshot.Revision with { Answers = snapshot.Revision.Answers.Select(answer =>
                answer.QuestionId == previousId
                    ? new RevisionAnswer(replacement.Id, replacement.Points, null, false, RequiresCompletion: true)
                    : answer).ToArray() }
        }).ToJson();
    }

    public static AssessmentDefinitionSnapshot FromHomework(HomeworkEntity homework) => new(
        1, "homework", homework.Id, homework.Title, homework.Description, homework.TotalScore,
        homework.PassingScoreThreshold, homework.DurationMinutes, homework.IsMandatory, homework.IsRandomized, null,
        homework.Questions.Where(x => !x.IsRetired).OrderBy(x => x.Order).ThenBy(x => x.Id).Select(FromHomeworkQuestion).ToArray())
    { IsActive = homework.IsActive };

    public static Exam ResolveExam(Exam current, string? snapshotJson)
    {
        if (snapshotJson is null) return current;
        var snapshot = Read(snapshotJson, "exam", current.Id);
        return new Exam
        {
            Id = current.Id, CreatedAt = current.CreatedAt, UpdatedAt = current.UpdatedAt,
            CreatedByTeacherId = current.CreatedByTeacherId, LessonVideoId = current.LessonVideoId,
            IsActive = current.IsActive, ArchiveMode = current.ArchiveMode,
            Title = snapshot.Title, Description = snapshot.Description ?? string.Empty,
            TotalScore = snapshot.TotalScore, PassingScore = snapshot.PassingScore ?? 0,
            DurationMinutes = snapshot.DurationMinutes, IsMandatory = snapshot.IsMandatory,
            IsRandomized = snapshot.IsRandomized, DisplayQuestionCount = snapshot.DisplayQuestionCount,
            ExamQuestions = snapshot.Questions.Select(q => ToExamQuestion(q, current.Id)).ToList()
        };
    }

    public static HomeworkEntity ResolveHomework(HomeworkEntity current, string? snapshotJson)
    {
        if (snapshotJson is null) return current;
        var snapshot = Read(snapshotJson, "homework", current.Id);
        return new HomeworkEntity
        {
            Id = current.Id, LessonId = current.LessonId, CreatedAt = current.CreatedAt,
            UpdatedAt = current.UpdatedAt, IsActive = current.IsActive, ArchiveMode = current.ArchiveMode,
            Title = snapshot.Title, Description = snapshot.Description, TotalScore = snapshot.TotalScore,
            PassingScoreThreshold = snapshot.PassingScore, IsMandatory = snapshot.IsMandatory,
            DurationMinutes = snapshot.DurationMinutes,
            IsRandomized = snapshot.IsRandomized,
            Questions = snapshot.Questions.Select(q => ToHomeworkQuestion(q, current.Id)).ToList()
        };
    }

    public static AssessmentDefinitionSnapshot Read(string json, string kind, Guid assessmentId)
    {
        var snapshot = JsonSerializer.Deserialize<AssessmentDefinitionSnapshot>(json)
            ?? throw new InvalidOperationException("The saved assessment definition is empty.");
        if (snapshot.SchemaVersion != 1 || snapshot.Kind != kind || snapshot.AssessmentId != assessmentId)
            throw new InvalidOperationException("The saved assessment definition does not match this attempt.");
        return snapshot;
    }

    private static AssessmentQuestionSnapshot FromExamQuestion(ExamQuestion question)
    {
        var bank = question.Question;
        var mistake = bank as FindTheMistakeQuestion;
        return new(question.Id, bank.Id, question.Order, (int)bank.Type, bank.Text, question.Points,
            bank.AudioUrl, bank.ImageUrl, bank.WrittenCorrection, bank.HintText,
            mistake?.BaseText, mistake?.MistakeStartIndex, mistake?.MistakeEndIndex,
            bank.Options.Where(o => !o.IsRetired).OrderBy(o => o.Id).Select(o => new AssessmentOptionSnapshot(o.Id, o.Text, o.IsCorrect)).ToArray(), null);
    }

    private static AssessmentQuestionSnapshot FromHomeworkQuestion(HomeworkQuestion question) => new(
        question.Id, question.Id, question.Order, (int)question.QuestionType, question.BodyText,
        question.PointsActive, question.AudioUrl, question.ImageUrl, question.WrittenCorrection,
        question.HintText, question.BaseText, question.MistakeStartIndex, question.MistakeEndIndex,
        (question.PossibleAnswers ?? []).Select(text => new AssessmentOptionSnapshot(
            Guid.Empty, text, text == question.CorrectAnswerKey)).ToArray(), question.CorrectAnswerKey);

    private static ExamQuestion ToExamQuestion(AssessmentQuestionSnapshot snapshot, Guid examId)
    {
        QuestionBankItem bank = (QuestionType)snapshot.Type switch
        {
            QuestionType.Essay => new EssayQuestion(),
            QuestionType.FindTheMistake => new FindTheMistakeQuestion
            {
                BaseText = snapshot.BaseText ?? string.Empty,
                MistakeStartIndex = snapshot.MistakeStartIndex ?? 0,
                MistakeEndIndex = snapshot.MistakeEndIndex ?? 0
            },
            QuestionType.MCQ => new QuestionBankItem(),
            _ => throw new InvalidOperationException("Unknown question type in saved exam definition.")
        };
        bank.Id = snapshot.BankQuestionId;
        bank.Type = (QuestionType)snapshot.Type;
        bank.Text = snapshot.Text;
        bank.DefaultPoints = snapshot.Points;
        bank.AudioUrl = snapshot.AudioUrl;
        bank.ImageUrl = snapshot.ImageUrl;
        bank.WrittenCorrection = snapshot.WrittenCorrection;
        bank.HintText = snapshot.HintText;
        bank.Options = snapshot.Options.Select(o => new QuestionOption
        {
            Id = o.Id, Text = o.Text, IsCorrect = o.IsCorrect, QuestionBankItemId = bank.Id
        }).ToList();
        return new ExamQuestion
        {
            Id = snapshot.Id, ExamId = examId, QuestionBankItemId = bank.Id,
            Question = bank, Order = snapshot.Order, Points = snapshot.Points
        };
    }

    private static HomeworkQuestion ToHomeworkQuestion(AssessmentQuestionSnapshot snapshot, Guid homeworkId) => new()
    {
        Id = snapshot.Id, HomeworkId = homeworkId, Order = snapshot.Order,
        QuestionType = (NaderGorge.Domain.Entities.Homework.QuestionType)snapshot.Type,
        BodyText = snapshot.Text, PointsActive = checked((int)snapshot.Points),
        AudioUrl = snapshot.AudioUrl, ImageUrl = snapshot.ImageUrl,
        WrittenCorrection = snapshot.WrittenCorrection, HintText = snapshot.HintText,
        BaseText = snapshot.BaseText, MistakeStartIndex = snapshot.MistakeStartIndex,
        MistakeEndIndex = snapshot.MistakeEndIndex, CorrectAnswerKey = snapshot.CorrectAnswerKey,
        PossibleAnswers = snapshot.Options.Select(o => o.Text).ToArray()
    };
}
