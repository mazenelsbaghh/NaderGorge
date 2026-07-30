using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Recruitment;

public sealed class RecruitmentService(IAppDbContext db)
{
    public async Task<ApiResponse<Guid>> HireAcceptedCandidateAsync(Guid candidateId, Guid offerId, string passwordHash, Guid actorUserId, CancellationToken ct)
    {
        var candidate = await db.Candidates.Include(item => item.Offers).SingleOrDefaultAsync(item => item.Id == candidateId, ct);
        if (candidate is null) return ApiResponse<Guid>.Fail("المرشح غير موجود", ["CANDIDATE_NOT_FOUND"]);
        if (candidate.EmployeeProfileId.HasValue) return ApiResponse<Guid>.Ok(candidate.EmployeeProfileId.Value);
        var offer = candidate.Offers.SingleOrDefault(item => item.Id == offerId && item.State == OfferState.Accepted);
        if (offer is null) return ApiResponse<Guid>.Fail("لا يوجد عرض مقبول", ["ACCEPTED_OFFER_NOT_FOUND"]);
        if (await db.Users.AnyAsync(item => item.PhoneNumber == candidate.PhoneNumber, ct)) return ApiResponse<Guid>.Fail("رقم الهاتف مستخدم", ["PHONE_ALREADY_EXISTS"]);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var user = new User { FullName = candidate.FullName.Trim(), PhoneNumber = candidate.PhoneNumber.Trim(), PasswordHash = passwordHash,
                IsProfileComplete = true, IsActive = true };
            var employee = new EmployeeProfile { UserId = user.Id, User = user, HireDate = offer.ProposedStartDate,
                EmploymentStatus = EmployeeEmploymentStatus.Probation, BasicSalary = offer.BaseSalary };
            employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
            db.Users.Add(user); db.EmployeeProfiles.Add(employee);
            var role = await db.Roles.SingleOrDefaultAsync(item => item.Name == "Employee", ct);
            if (role is null)
            {
                role = new Role { Name = "Employee", Type = RoleType.Staff, AllowedDomain = "assistant",
                    PermissionsJson = JsonSerializer.Serialize(new[] { "hr.attendance.self", "hr.leave.self", "hr.document.self", "hr.asset.self", "hr.performance.self", "payroll.view" }) };
                db.Roles.Add(role);
            }
            db.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = role.Id, Role = role });
            db.EmployeeCompensations.Add(new EmployeeCompensation { EmployeeId = employee.Id, Employee = employee, BaseSalary = offer.BaseSalary,
                Currency = offer.Currency, EffectiveFrom = offer.ProposedStartDate, Reason = $"Accepted offer {offer.OfferNumber}" });
            foreach (var task in DefaultOnboardingTasks(employee.Id, offer.ProposedStartDate)) db.EmployeeLifecycleTasks.Add(task);
            candidate.EmployeeProfileId = employee.Id; candidate.Stage = CandidateStage.Hired; candidate.Version++; offer.State = OfferState.Converted; offer.Version++;
            db.OutboxEvents.Add(new OutboxEvent { Type = "hr.employee.hired", TargetUserId = user.Id.ToString(),
                PayloadJson = JsonSerializer.Serialize(new { employeeId = employee.Id, employee.EmployeeNumber, candidateId = candidate.Id, offerId = offer.Id, offer.ProposedStartDate }) });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return ApiResponse<Guid>.Ok(employee.Id);
        }
        catch { await transaction.RollbackAsync(ct); throw; }
    }

    private static IEnumerable<EmployeeLifecycleTask> DefaultOnboardingTasks(Guid employeeId, DateOnly startDate)
    {
        var start = startDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        yield return new EmployeeLifecycleTask { EmployeeId = employeeId, Phase = "Onboarding", Title = "استكمال مستندات التعيين", DueAt = start.AddDays(-1) };
        yield return new EmployeeLifecycleTask { EmployeeId = employeeId, Phase = "Onboarding", Title = "تجهيز الحساب والصلاحيات", DueAt = start };
        yield return new EmployeeLifecycleTask { EmployeeId = employeeId, Phase = "Probation", Title = "مراجعة فترة التجربة", DueAt = start.AddDays(90) };
    }
}
