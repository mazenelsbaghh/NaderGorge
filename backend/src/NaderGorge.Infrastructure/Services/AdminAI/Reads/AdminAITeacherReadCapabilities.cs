using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAITeacherSummary(int Teachers, int StaffMembers, int Subjects, int SubjectAssignments, int Photos, int StudentProfiles, int EssaySubmissions, int ActivationLogs, DateTime DataAsOf);

public sealed class AdminAITeacherSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "teacher.summary";
    public Type OutputType => typeof(AdminAITeacherSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAITeacherSummary(
            await db.TeacherProfiles.AsNoTracking().CountAsync(ct),
            await db.TeacherStaffMembers.AsNoTracking().CountAsync(ct),
            await db.Subjects.AsNoTracking().CountAsync(ct),
            await db.TeacherSubjects.AsNoTracking().CountAsync(ct),
            await db.TeacherPhotos.AsNoTracking().CountAsync(ct),
            await db.StudentProfiles.AsNoTracking().CountAsync(ct),
            await db.EssaySubmissions.AsNoTracking().CountAsync(ct),
            await db.AccessCodeActivationLogs.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.teachers"]);
    }
}
