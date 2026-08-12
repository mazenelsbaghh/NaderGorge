using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIAssessmentSummary(int Exams, int Questions, int Homework, int HomeworkSubmissions, int ExamAttempts, int StudentAnswers, int EssaySubmissions, DateTime DataAsOf);
public sealed class AdminAIAssessmentSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "assessment.summary"; public Type OutputType => typeof(AdminAIAssessmentSummary);
    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAIAssessmentSummary(
            await db.Exams.AsNoTracking().CountAsync(ct),
            await db.QuestionBankItems.AsNoTracking().CountAsync(ct),
            await db.Homeworks.AsNoTracking().CountAsync(ct),
            await db.HomeworkSubmissions.AsNoTracking().CountAsync(ct),
            await db.StudentExamAttempts.AsNoTracking().CountAsync(ct),
            await db.StudentAnswers.AsNoTracking().CountAsync(ct),
            await db.EssaySubmissions.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.exams", "admin.homework"]);
    }
}
