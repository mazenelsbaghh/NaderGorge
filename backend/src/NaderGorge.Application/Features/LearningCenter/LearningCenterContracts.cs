using System.ComponentModel.DataAnnotations;

namespace NaderGorge.Application.Features.LearningCenter;

public sealed record LearningFilter(
    Guid? TeacherId = null, Guid? SubjectId = null, Guid? PackageId = null,
    string? Grade = null, int Days = 30, int InactiveDays = 7,
    int DeclinePoints = 15, int RepeatedAttempts = 3);
public sealed record LearningPackageDto(Guid Id, string Name, Guid TeacherId, string TeacherName,
    Guid SubjectId, string SubjectName, string Grade);
public sealed record LearningLessonDto(Guid Id, string Title, Guid PackageId);
public sealed record LearningConceptChoice(Guid LessonId, string Concept);
public sealed record LearningOptionsDto(List<LearningPackageDto> Packages, List<LearningLessonDto> Lessons, List<LearningConceptChoice> Concepts);
public sealed record LearningQuestionDto(Guid Id, string Text, int Type, decimal Points, string Tags,
    Guid TeacherId, Guid SubjectId, Guid? LessonId, string Concept, int Difficulty,
    string? Correction, List<LearningOptionDto> Options);
public sealed record LearningOptionDto(Guid? Id, string Text, bool IsCorrect);
public sealed record SaveLearningQuestion(
    [Required, MaxLength(10000)] string Text, Guid LessonId,
    [Required, MaxLength(160)] string Concept, [Range(1, 3)] int Difficulty,
    [Range(0.5, 100)] decimal Points, [MaxLength(1000)] string Tags,
    [MaxLength(10000)] string? Correction, [MinLength(2), MaxLength(8)] List<LearningOptionDto> Options);
public sealed record ClassifyLearningQuestion(Guid LessonId,
    [Required, MaxLength(160)] string Concept, [Range(1, 3)] int Difficulty);
public sealed record LearningQuestionStats(Guid QuestionId, string Text, int Students, int Attempts,
    decimal? CorrectPercent, string? CommonWrongAnswer, int CommonWrongCount, decimal? Discrimination);
public sealed record LearningConceptDto(Guid LessonId, string Lesson, Guid PackageId, string Package,
    string Concept, int Students, int Attempts, decimal? CorrectPercent,
    List<LearningQuestionStats> Questions, List<LearningStudentScore> StudentsNeedingReview,
    List<LearningTrendPoint> Trend);
public sealed record LearningStudentScore(Guid StudentId, string Name, decimal CorrectPercent);
public sealed record LearningTrendPoint(string Date, int Students, decimal? CorrectPercent);
public sealed record LearningOverviewDto(List<LearningConceptDto> Concepts, int UnclassifiedQuestions,
    int ExcludedAttempts, int MinimumStudents);
public sealed record LearningFollowUpDto(Guid StudentId, string Name, Guid PackageId, string Package,
    string Teacher, DateTime? LastActivityAt, List<string> Reasons, string Status, string Note,
    DateTime? FollowedUpAt, string? FollowedUpBy, decimal? LatestPercent, decimal? ImprovementPoints);
public sealed record SaveLearningFollowUp(Guid StudentId, Guid PackageId,
    [Required, RegularExpression("^(New|InProgress|Completed)$")] string Status,
    [Required, MaxLength(2000)] string Note, [Required, MaxLength(2000)] string Reason);
public sealed record GenerateLearningForms(Guid RequestId, Guid PackageId,
    [Required, MaxLength(180)] string Title, [Range(1, 5)] int Forms,
    [Range(1, 180)] int DurationMinutes, [Range(1, 100)] int PassingPercent,
    [MinLength(1), MaxLength(40)] List<LearningBlueprintRow> Blueprint);
public sealed record LearningBlueprintRow(Guid LessonId, [Required, MaxLength(160)] string Concept,
    [Range(1, 3)] int Difficulty, [Range(1, 50)] int Count);
public sealed record GeneratedLearningForm(Guid ExamId, string Title, int Questions, decimal TotalScore);
