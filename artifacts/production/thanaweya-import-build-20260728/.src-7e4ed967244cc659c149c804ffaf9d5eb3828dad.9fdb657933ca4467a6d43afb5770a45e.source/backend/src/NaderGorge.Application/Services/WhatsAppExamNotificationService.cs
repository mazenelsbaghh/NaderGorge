using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class WhatsAppExamNotificationService
{
    private readonly IAppDbContext _db;
    private readonly WhatsAppCloudService _whatsAppCloudService;
    private readonly ILogger<WhatsAppExamNotificationService> _logger;

    public WhatsAppExamNotificationService(
        IAppDbContext db,
        WhatsAppCloudService whatsAppCloudService,
        ILogger<WhatsAppExamNotificationService> logger)
    {
        _db = db;
        _whatsAppCloudService = whatsAppCloudService;
        _logger = logger;
    }

    public sealed record ExamResultMessagePreview(
        Guid AttemptId,
        string RecipientPhoneNumber,
        string ParentName,
        string StudentName,
        string Score,
        string TotalScore,
        string Subject,
        string Lecture,
        bool IsResultReady);

    public sealed record SendExamResultMessageResult(
        bool Success,
        string Message,
        string RecipientPhoneNumber,
        string? MetaMessageId,
        int StatusCode,
        string? ErrorCode,
        ExamResultMessagePreview? Preview);

    public async Task<SendExamResultMessageResult> SendExamResultAsync(
        Guid attemptId,
        string? overrideRecipientPhoneNumber,
        CancellationToken cancellationToken)
    {
        var preview = await BuildPreviewAsync(attemptId, overrideRecipientPhoneNumber, cancellationToken);
        if (preview is null)
        {
            return new SendExamResultMessageResult(
                false,
                "Exam attempt was not found.",
                string.Empty,
                null,
                404,
                "EXAM_ATTEMPT_NOT_FOUND",
                null);
        }

        if (!preview.IsResultReady)
        {
            return new SendExamResultMessageResult(
                false,
                "Exam result is not ready for this attempt yet.",
                preview.RecipientPhoneNumber,
                null,
                409,
                "EXAM_RESULT_NOT_READY",
                preview);
        }

        if (string.IsNullOrWhiteSpace(preview.RecipientPhoneNumber))
        {
            return new SendExamResultMessageResult(
                false,
                "No parent phone number is available for this student.",
                string.Empty,
                null,
                400,
                "PARENT_PHONE_NOT_FOUND",
                preview);
        }

        var result = await _whatsAppCloudService.SendStudentResultTemplateAsync(
            preview.RecipientPhoneNumber,
            new WhatsAppCloudService.StudentResultTemplateData(
                preview.ParentName,
                preview.StudentName,
                preview.Score,
                preview.TotalScore,
                preview.Subject,
                preview.Lecture),
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "WhatsApp exam result message failed for attempt {AttemptId}. Status={StatusCode}, ErrorCode={ErrorCode}",
                attemptId,
                result.StatusCode,
                result.ErrorCode);
        }

        return new SendExamResultMessageResult(
            result.Success,
            result.Message,
            result.RecipientPhoneNumber,
            result.MetaMessageId,
            result.StatusCode,
            result.ErrorCode,
            preview);
    }

    private async Task<ExamResultMessagePreview?> BuildPreviewAsync(
        Guid attemptId,
        string? overrideRecipientPhoneNumber,
        CancellationToken cancellationToken)
    {
        var attempt = await _db.StudentExamAttempts
            .AsNoTracking()
            .Include(item => item.User)
            .ThenInclude(user => user.StudentProfile)
            .Include(item => item.Exam)
            .FirstOrDefaultAsync(item => item.Id == attemptId, cancellationToken);

        if (attempt is null)
        {
            return null;
        }

        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(item => item.ContentSection)
            .ThenInclude(section => section.Term)
            .ThenInclude(term => term.Package)
            .ThenInclude(package => package.Subject)
            .FirstOrDefaultAsync(item => item.ExamId == attempt.ExamId, cancellationToken);

        var lessonVideo = lesson is null
            ? await _db.LessonVideos
                .AsNoTracking()
                .Include(item => item.Lesson)
                .ThenInclude(item => item.ContentSection)
                .ThenInclude(section => section.Term)
                .ThenInclude(term => term.Package)
                .ThenInclude(package => package.Subject)
                .FirstOrDefaultAsync(item => item.ExamId == attempt.ExamId, cancellationToken)
            : null;

        var studentProfile = attempt.User.StudentProfile;
        var recipient = FirstNonBlank(
            overrideRecipientPhoneNumber,
            studentProfile?.ParentPhone,
            studentProfile?.SecondaryParentPhone,
            studentProfile?.MotherPhone);

        var subject = lesson?.ContentSection.Term.Package.Subject.Name
            ?? lessonVideo?.Lesson.ContentSection.Term.Package.Subject.Name
            ?? attempt.Exam.Title;

        var lecture = lesson?.Title
            ?? lessonVideo?.Lesson.Title
            ?? attempt.Exam.Title;

        var studentName = attempt.User.FullName;
        var isResultReady = !string.IsNullOrWhiteSpace(attempt.Evaluation)
            && !string.Equals(attempt.Evaluation, "قيد التصحيح", StringComparison.Ordinal);

        return new ExamResultMessagePreview(
            attempt.Id,
            recipient,
            $"ولي أمر {studentName}",
            studentName,
            FormatDecimal(attempt.ScoreAchieved),
            FormatDecimal(attempt.Exam.TotalScore),
            subject,
            lecture,
            isResultReady);
    }

    private static string FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string FormatDecimal(decimal value)
    {
        return value % 1 == 0
            ? decimal.Truncate(value).ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
