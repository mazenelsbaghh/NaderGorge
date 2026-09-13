namespace NaderGorge.Application.Features.VideoLearning;

public sealed record LearningTools(
    bool Questions = false, bool Understanding = false, bool AskTeacher = false,
    bool Notes = false, bool Bookmarks = false, bool Timeline = false,
    bool Cards = false, bool Glossary = false, bool Experiments = false,
    bool Mastery = false, bool Review = false, bool AiAuthoring = false,
    bool AiTutor = false, int AiDailyLimit = 10, bool ChapterAids = false);

public sealed record LearningActivity(
    Guid Id, string Kind, string Placement, int Seconds, int EndSeconds,
    string Title, string Body, string Answer, string Concept,
    string[] Options, int? CorrectOption, bool Required = false,
    Guid? QuestionBankId = null, string Experiment = "linear",
    double Factor = 1, double Offset = 0, double Minimum = 0, double Maximum = 10);

public sealed record LearningDocument(LearningTools Tools, LearningActivity[] Activities);
public sealed record SaveLearningDocument(Guid Version, int SourceRevision, LearningDocument Document);
public sealed record LearningEntryRequest(Guid Id, Guid Version, string Kind, int Seconds,
    string Text = "", string Title = "", Guid? ActivityId = null);
public sealed record LearningAiRequest(Guid Id, Guid Version, string Mode, int Seconds, string Text);
public sealed record LearningEntryDto(Guid Id, string Kind, int Seconds, string Text, string Title,
    Guid? ActivityId, bool? Correct, Guid? CommentId);
public sealed record TimelineDensity(int Seconds, int Understood, int Confused, int Example);
public sealed record LearningSnapshot(Guid Version, int SourceRevision, bool Stale,
    LearningDocument Document, IReadOnlyList<LearningEntryDto> Entries, IReadOnlyList<TimelineDensity> Density);
public sealed record LearningAnswerResult(LearningEntryDto Entry, string Explanation);
public sealed record LearningAiResult(string Text, LearningActivity[] Activities);

public interface IVideoLearningAi
{
    Task<LearningAiResult> GenerateAsync(string mode, string question, string context, CancellationToken ct);
}
