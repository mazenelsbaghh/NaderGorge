using NaderGorge.Domain.Entities;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;
using HomeworkQuestion = NaderGorge.Domain.Entities.Homework.HomeworkQuestion;

namespace NaderGorge.Application.Features.Assessments;

public sealed record ExamAuthoringContext(Guid TeacherId, Guid SubjectId);

/// <summary>Updates a tracked definition without deleting question/option rows referenced by old attempts.</summary>
public static class AssessmentDefinitionWriter
{
    public static void ApplyHomework(HomeworkEntity homework, AssessmentDefinitionSnapshot definition)
    {
        Validate(definition, "homework", homework.Id);
        ValidateQuestionTypes(definition, homework.Questions.ToDictionary(q => q.Id, q => (int)q.QuestionType));
        homework.Title = definition.Title.Trim();
        homework.Description = definition.Description;
        homework.TotalScore = definition.TotalScore;
        homework.DurationMinutes = definition.DurationMinutes;
        homework.PassingScoreThreshold = definition.PassingScore;
        homework.IsMandatory = definition.IsMandatory;
        homework.IsRandomized = definition.IsRandomized;
        homework.UpdatedAt = DateTime.UtcNow;
        var included = definition.Questions.Select(q => q.Id).ToHashSet();
        foreach (var question in homework.Questions) question.IsRetired = !included.Contains(question.Id);
        foreach (var proposed in definition.Questions)
        {
            var question = homework.Questions.SingleOrDefault(q => q.Id == proposed.Id);
            if (question is null)
            {
                question = new HomeworkQuestion { Id = proposed.Id, HomeworkId = homework.Id };
                homework.Questions.Add(question);
            }
            UpdateHomeworkQuestion(question, proposed);
        }
        homework.IsActive = definition.IsActive && definition.Questions.Length > 0;
    }

    public static void ApplyExam(Exam exam, AssessmentDefinitionSnapshot definition, ExamAuthoringContext author)
    {
        Validate(definition, "exam", exam.Id);
        ValidateQuestionTypes(definition, exam.ExamQuestions.ToDictionary(q => q.Id, q => (int)q.Question.Type));
        exam.Title = definition.Title.Trim();
        exam.Description = definition.Description ?? string.Empty;
        exam.TotalScore = definition.TotalScore;
        exam.PassingScore = definition.PassingScore ?? 0;
        exam.DurationMinutes = definition.DurationMinutes;
        exam.IsMandatory = definition.IsMandatory;
        exam.IsRandomized = definition.IsRandomized;
        exam.DisplayQuestionCount = definition.DisplayQuestionCount;
        exam.UpdatedAt = DateTime.UtcNow;
        var included = definition.Questions.Select(q => q.Id).ToHashSet();
        foreach (var question in exam.ExamQuestions) question.IsRetired = !included.Contains(question.Id);
        foreach (var proposed in definition.Questions) UpdateExamQuestion(exam, proposed, author);
        exam.IsActive = definition.IsActive && definition.Questions.Length > 0;
    }

    public static void Validate(AssessmentDefinitionSnapshot definition, string kind, Guid assessmentId)
    {
        if (definition.SchemaVersion != 1 || definition.Kind != kind || definition.AssessmentId != assessmentId)
            throw new ArgumentException("تعريف الواجب أو الامتحان لا يطابق العنصر المطلوب.");
        if (string.IsNullOrWhiteSpace(definition.Title) || definition.Title.Length > 255
            || definition.TotalScore < 0 || definition.TotalScore > 9999999999999999.99m
            || definition.TotalScore != decimal.Round(definition.TotalScore, 2)
            || definition.PassingScore < 0 || definition.PassingScore > definition.TotalScore
            || (definition.PassingScore is decimal passingScore && passingScore != decimal.Round(passingScore, 2))
            || definition.DurationMinutes <= 0 || definition.DurationMinutes > int.MaxValue / 60 || definition.DisplayQuestionCount <= 0
            || definition.DisplayQuestionCount > definition.Questions.Length || definition.Questions.Length > 500)
            throw new ArgumentException("راجع الاسم والدرجات والمدة وعدد الأسئلة المعروضة.");
        if (definition.Questions.Select(q => q.Id).Distinct().Count() != definition.Questions.Length)
            throw new ArgumentException("لا يمكن تكرار نفس السؤال في التعريف.");
        foreach (var question in definition.Questions) ValidateQuestion(question, kind);
    }

    private static void ValidateQuestion(AssessmentQuestionSnapshot question, string kind)
    {
        if (question.Id == Guid.Empty || string.IsNullOrWhiteSpace(question.Text) || question.Points < 0 || question.Points > int.MaxValue
            || question.Points != decimal.Round(question.Points, 2)
            || !Enum.IsDefined((QuestionType)question.Type) || question.Options.Length > 30
            || (kind == "homework" && question.Points != decimal.Truncate(question.Points)))
            throw new ArgumentException("راجع محتوى السؤال ونوعه ودرجته.");
        if (question.Type == (int)QuestionType.MCQ
            && (question.Options.Length < 2 || question.Options.Count(o => o.IsCorrect) != 1
                || question.Options.Any(o => string.IsNullOrWhiteSpace(o.Text))))
            throw new ArgumentException("سؤال الاختيار يحتاج اختيارين على الأقل وإجابة صحيحة واحدة.");
        if (kind == "exam" && (question.Options.Any(o => o.Id == Guid.Empty)
            || question.Options.Select(o => o.Id).Distinct().Count() != question.Options.Length))
            throw new ArgumentException("معرّفات الاختيارات غير صالحة أو مكررة.");
        if (question.Type == (int)QuestionType.FindTheMistake
            && (string.IsNullOrEmpty(question.BaseText) || question.MistakeStartIndex is null || question.MistakeEndIndex is null
                || question.MistakeStartIndex < 0 || question.MistakeEndIndex > question.BaseText.Length
                || question.MistakeEndIndex <= question.MistakeStartIndex))
            throw new ArgumentException("حدد موضع الخطأ داخل النص بشكل صحيح.");
    }

    private static void ValidateQuestionTypes(AssessmentDefinitionSnapshot definition, IReadOnlyDictionary<Guid, int> previousTypes)
    {
        if (definition.Questions.Any(q => previousTypes.TryGetValue(q.Id, out var previousType) && previousType != q.Type))
            throw new ArgumentException("تغيير نوع السؤال يُحفظ كسؤال جديد لتطبيق اختيارات الأسئلة المحذوفة والجديدة على المحاولات القديمة.");
    }

    private static void UpdateHomeworkQuestion(HomeworkQuestion question, AssessmentQuestionSnapshot proposed)
    {
        question.IsRetired = false;
        question.Order = proposed.Order;
        question.QuestionType = (NaderGorge.Domain.Entities.Homework.QuestionType)proposed.Type;
        question.BodyText = proposed.Text;
        question.PointsActive = checked((int)proposed.Points);
        question.PossibleAnswers = proposed.Options.Select(o => o.Text).ToArray();
        question.CorrectAnswerKey = proposed.Type == (int)QuestionType.MCQ
            ? proposed.Options.FirstOrDefault(o => o.IsCorrect)?.Text : proposed.CorrectAnswerKey;
        question.AudioUrl = proposed.AudioUrl;
        question.ImageUrl = proposed.ImageUrl;
        question.WrittenCorrection = proposed.WrittenCorrection;
        question.HintText = proposed.HintText;
        question.BaseText = proposed.BaseText;
        question.MistakeStartIndex = proposed.MistakeStartIndex;
        question.MistakeEndIndex = proposed.MistakeEndIndex;
    }

    private static void UpdateExamQuestion(Exam exam, AssessmentQuestionSnapshot proposed, ExamAuthoringContext author)
    {
        var question = exam.ExamQuestions.SingleOrDefault(q => q.Id == proposed.Id);
        if (question is null)
        {
            question = new ExamQuestion { Id = proposed.Id, ExamId = exam.Id, Question = CreateBankQuestion(proposed.Type, author) };
            exam.ExamQuestions.Add(question);
        }
        question.QuestionBankItemId = question.Question.Id;
        question.IsRetired = false;
        question.Order = proposed.Order;
        question.Points = proposed.Points;
        UpdateBankQuestion(question.Question, proposed);
    }

    private static QuestionBankItem CreateBankQuestion(int type, ExamAuthoringContext author)
    {
        QuestionBankItem question = (QuestionType)type switch
        {
            QuestionType.Essay => new EssayQuestion(),
            QuestionType.FindTheMistake => new FindTheMistakeQuestion(),
            _ => new QuestionBankItem()
        };
        question.Type = (QuestionType)type;
        question.CreatedByTeacherId = author.TeacherId;
        question.SubjectId = author.SubjectId;
        question.Tags = "Inline";
        return question;
    }

    private static void UpdateBankQuestion(QuestionBankItem question, AssessmentQuestionSnapshot proposed)
    {
        question.Text = proposed.Text;
        question.DefaultPoints = proposed.Points;
        question.AudioUrl = proposed.AudioUrl;
        question.ImageUrl = proposed.ImageUrl;
        question.WrittenCorrection = proposed.WrittenCorrection;
        question.HintText = proposed.HintText;
        if (question is FindTheMistakeQuestion mistake)
        {
            mistake.BaseText = proposed.BaseText!;
            mistake.MistakeStartIndex = proposed.MistakeStartIndex!.Value;
            mistake.MistakeEndIndex = proposed.MistakeEndIndex!.Value;
        }
        var included = proposed.Options.Select(o => o.Id).ToHashSet();
        foreach (var option in question.Options) option.IsRetired = !included.Contains(option.Id);
        foreach (var proposedOption in proposed.Options)
        {
            var option = question.Options.SingleOrDefault(o => o.Id == proposedOption.Id);
            if (option is null)
            {
                option = new QuestionOption { Id = proposedOption.Id, QuestionBankItemId = question.Id };
                question.Options.Add(option);
            }
            option.Text = proposedOption.Text;
            option.IsCorrect = proposedOption.IsCorrect;
            option.IsRetired = false;
        }
    }
}
