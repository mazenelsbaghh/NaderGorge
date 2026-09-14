namespace NaderGorge.Application.Features.VideoLearning;

public static class LearningRules
{
    public static readonly string[] Kinds = ["question", "card", "term", "experiment", "concept"];
    public static bool Enabled(LearningTools tools, string kind) => kind switch
    {
        "question" or "answer" => tools.Questions,
        "understanding" => tools.Understanding || tools.Timeline,
        "ask" => tools.AskTeacher,
        "note" => tools.Notes,
        "bookmark" => tools.Bookmarks,
        "card" => tools.Cards,
        "term" => tools.Glossary,
        "experiment" => tools.Experiments,
        "concept" or "mastery" => tools.Mastery,
        _ => false
    };

    public static void Validate(LearningDocument document, int? duration)
    {
        if (document is null || document.Tools is null || document.Activities is null || document.Activities.Length > 150 || document.Activities.Any(a => a is null) ||
            document.Tools.AiDailyLimit is < 1 or > 50)
            throw new ArgumentException("راجع إعدادات الأدوات وحد الاستخدام.");
        if (document.Activities.Select(a => a.Id).Distinct().Count() != document.Activities.Length)
            throw new ArgumentException("يوجد نشاط مكرر.");
        foreach (var a in document.Activities)
        {
            if (a.Id == Guid.Empty || !Kinds.Contains(a.Kind) ||
                a.Placement is not ("moment" or "chapter" or "end") ||
                a.Seconds < 0 || a.EndSeconds < a.Seconds || a.EndSeconds > (duration ?? 86400) ||
                string.IsNullOrWhiteSpace(a.Title) || a.Title.Length > 300 ||
                a.Body is null || a.Body.Length > 2000 || a.Answer is null || a.Answer.Length > 2000 ||
                a.Concept is null || a.Concept.Length > 160 || a.Options is null || a.Options.Length > 6 ||
                a.Options.Any(o => string.IsNullOrWhiteSpace(o) || o.Length > 500))
                throw new ArgumentException("راجع عنوان النشاط ونوعه وتوقيته ومحتواه.");
            if (a.Kind == "question" && (a.Options.Length < 2 || a.CorrectOption is null ||
                a.CorrectOption < 0 || a.CorrectOption >= a.Options.Length))
                throw new ArgumentException("السؤال يحتاج اختيارين على الأقل وإجابة صحيحة.");
            if (a.Kind == "experiment" && (a.Experiment is not ("linear" or "product" or "ratio") ||
                !double.IsFinite(a.Factor) || !double.IsFinite(a.Offset) ||
                !double.IsFinite(a.Minimum) || !double.IsFinite(a.Maximum) ||
                a.Minimum >= a.Maximum || Math.Abs(a.Minimum) > 100000 || Math.Abs(a.Maximum) > 100000 ||
                Math.Abs(a.Factor) > 100000 || Math.Abs(a.Offset) > 100000 ||
                (a.Experiment == "ratio" && a.Minimum <= 0 && a.Maximum >= 0)))
                throw new ArgumentException("راجع نطاق التجربة وأرقامها، المقام لا يمكن أن يساوي صفرًا.");
        }
    }

    public static LearningDocument ForStudent(LearningDocument document) => document with
    {
        Activities = document.Activities.Where(a => Enabled(document.Tools, a.Kind))
            .Select(a => a.Kind == "question" ? a with { CorrectOption = null, Answer = "", QuestionBankId = null } : a with { QuestionBankId = null }).ToArray()
    };
}
