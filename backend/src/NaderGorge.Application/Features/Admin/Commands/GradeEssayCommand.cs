using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record GradeEssayCommand(Guid EssaySubmissionId, decimal TeacherScore, string? TeacherFeedback, Guid? CurrentUserId = null) : IRequest<ApiResponse<bool>>;

public class GradeEssayCommandHandler : IRequestHandler<GradeEssayCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public GradeEssayCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<bool>> Handle(GradeEssayCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessEssaySubmissionAsync(request.CurrentUserId.Value, request.EssaySubmissionId, ct);
            if (!canAccess) return ApiResponse<bool>.Fail("Unauthorized access to grade this essay submission.");
        }

        var submission = await _db.EssaySubmissions.FindAsync(new object[] { request.EssaySubmissionId }, ct);
        if (submission == null) return ApiResponse<bool>.Fail("Essay submission not found.");

        if (submission.Status != EssaySubmissionStatus.WaitTeacher)
        {
            return ApiResponse<bool>.Fail("Essay is not ready for teacher grading.");
        }

        Guid? teacherId = null;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, ct);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                if (user.TeacherProfile != null)
                {
                    teacherId = user.TeacherProfile.Id;
                }
            }
        }

        submission.TeacherFinalScore = request.TeacherScore;
        submission.TeacherFeedback = request.TeacherFeedback;
        submission.Status = EssaySubmissionStatus.TeacherGraded;
        submission.GradedByTeacherId = teacherId;

        await _db.SaveChangesAsync(ct);

        var attempt = _db.StudentExamAttempts.Local.FirstOrDefault(a => a.Id == submission.StudentExamAttemptId)
            ?? await _db.StudentExamAttempts.FirstOrDefaultAsync(a => a.Id == submission.StudentExamAttemptId, ct);
        var exam = attempt == null
            ? null
            : _db.Exams.Local.FirstOrDefault(e => e.Id == attempt.ExamId)
                ?? await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId, ct);
        if (attempt != null && exam != null)
        {
            var objectiveAnswers = await _db.StudentAnswers
                .Where(a => a.StudentExamAttemptId == attempt.Id)
                .ToListAsync(ct);

            var essaySubmissions = await _db.EssaySubmissions
                .Where(e => e.StudentExamAttemptId == attempt.Id)
                .ToListAsync(ct);

            var latestEssaySubmissions = essaySubmissions
                .GroupBy(e => e.QuestionId)
                .Select(g => g
                    .OrderByDescending(e => e.UpdatedAt ?? e.CreatedAt)
                    .First())
                .ToList();

            var rawPointsEarned = objectiveAnswers.Sum(a => a.PointsAwarded)
                + latestEssaySubmissions.Sum(e => e.Id == submission.Id ? request.TeacherScore : e.TeacherFinalScore ?? 0m);
            var rawPointsPossible = await _db.ExamQuestions
                .Where(eq => eq.ExamId == exam.Id)
                .SumAsync(eq => eq.Points, ct);

            var hasPendingEssayQuestions = latestEssaySubmissions
                .Where(e => e.QuestionId != submission.QuestionId)
                .Any(e => e.Status != EssaySubmissionStatus.TeacherGraded);

            var allTeacherGraded = !hasPendingEssayQuestions;
            if (allTeacherGraded)
            {
                var scaledScore = NaderGorge.Application.Services.GradingEvaluationService.CalculateScaledScore(rawPointsEarned, rawPointsPossible, exam.TotalScore);
                attempt.ScoreAchieved = scaledScore;
                attempt.IsPassed = scaledScore >= exam.PassingScore;
                attempt.Evaluation = NaderGorge.Application.Services.GradingEvaluationService.DetermineEvaluation(scaledScore, exam.PassingScore, exam.TotalScore);
            }
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}
