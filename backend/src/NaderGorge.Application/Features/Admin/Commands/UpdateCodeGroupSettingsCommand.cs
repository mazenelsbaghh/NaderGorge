using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpdateCodeGroupSettingsCommand(
    Guid GroupId,
    Guid AdminId,
    string? Name,
    Guid? TeacherId,
    DateTime? ExpiresAt,
    SalesOwnerType? RevenueOwner,
    TeacherAllocationMode? RevenueAllocationMode,
    decimal? RevenueAllocationValue,
    CodeAccountingTiming AccountingTiming
) : IRequest<ApiResponse>;

public class UpdateCodeGroupSettingsCommandHandler : IRequestHandler<UpdateCodeGroupSettingsCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;

    public UpdateCodeGroupSettingsCommandHandler(IAppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse> Handle(UpdateCodeGroupSettingsCommand request, CancellationToken ct)
    {
        await using var transaction = await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var expiresAt = request.ExpiresAt.HasValue ? CairoTime.ToUtc(request.ExpiresAt.Value) : (DateTime?)null;
        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            return ApiResponse.Fail("تاريخ انتهاء الأكواد يجب أن يكون في المستقبل.");

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.AdminId, ct);

        if (user == null)
            return ApiResponse.Fail("User not found.");

        var permissions = user.UserRoles
            .SelectMany(ur =>
            {
                if (string.IsNullOrWhiteSpace(ur.Role.PermissionsJson)) return Array.Empty<string>();
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<string[]>(ur.Role.PermissionsJson)
                        ?? Array.Empty<string>();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var isAdmin = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Admin);
        if (!isAdmin && !permissions.Contains("codes.manage"))
            return ApiResponse.Fail("Unauthorized: You do not have permission to manage codes.");

        var group = await _db.CodeGroups.FirstOrDefaultAsync(cg => cg.Id == request.GroupId, ct);
        if (group == null)
            return ApiResponse.Fail("Code Group not found");

        Guid? teacherId = null;
        if (request.TeacherId.HasValue && request.TeacherId.Value != Guid.Empty)
        {
            var teacherExists = await _db.TeacherProfiles.AnyAsync(tp => tp.Id == request.TeacherId.Value, ct);
            if (!teacherExists)
                return ApiResponse.Fail("Selected teacher was not found.");

            teacherId = request.TeacherId.Value;
        }

        if (request.RevenueOwner == SalesOwnerType.Teacher && !teacherId.HasValue)
            return ApiResponse.Fail("يجب اختيار مدرس عند جعل الربح تابعاً للمدرس.");

        if (request.RevenueAllocationValue.HasValue && request.RevenueAllocationValue.Value < 0)
            return ApiResponse.Fail("قيمة توزيع الربح لا يمكن أن تكون سالبة.");

        if (request.RevenueAllocationMode == TeacherAllocationMode.Percentage
            && request.RevenueAllocationValue.HasValue
            && request.RevenueAllocationValue.Value > 100)
            return ApiResponse.Fail("النسبة لا يمكن أن تزيد عن 100%.");

        if (request.AccountingTiming == CodeAccountingTiming.Immediate && await _db.TeacherProfiles.AnyAsync(x => x.Id == teacherId && x.FinancePreset == TeacherFinancePreset.Nader, ct))
            return ApiResponse.Fail("أكواد نادر تُحسب عند الاستخدام فقط.");
        var financialTerms = await _db.CodeGroupFinancialTerms.FirstOrDefaultAsync(x => x.CodeGroupId == group.Id, ct);
        var financialChange = teacherId != group.TeacherId || request.RevenueOwner != group.RevenueOwner
            || request.RevenueAllocationMode != group.RevenueAllocationMode || request.RevenueAllocationValue != group.RevenueAllocationValue
            || request.AccountingTiming != group.AccountingTiming;
        if (financialChange && await CodeGroupAccountingGuard.HasStartedAsync(_db, group, ct))
            return ApiResponse.Fail("لا يمكن تغيير الشروط المالية بعد بدء استخدام أو حساب دفعة الأكواد.");
        var oldValues = new
        {
            group.Name,
            group.TeacherId,
            group.ExpiresAt,
            group.RevenueOwner,
            group.RevenueAllocationMode,
            group.RevenueAllocationValue,
            group.AccountingTiming
        };

        group.Name = string.IsNullOrWhiteSpace(request.Name)
            ? group.Name
            : request.Name.Trim();
        group.TeacherId = teacherId;
        group.ExpiresAt = expiresAt;
        // Access codes also carry their own expiry. Keep unused codes synchronized so an
        // administrator can genuinely extend or shorten a batch after it was generated.
        var unusedCodes = await _db.AccessCodes
            .Where(code => code.CodeGroupId == group.Id && !code.IsConsumed)
            .ToListAsync(ct);
        foreach (var code in unusedCodes)
        {
            code.ExpiresAt = expiresAt;
            code.UpdatedAt = DateTime.UtcNow;
        }

        if (group.ExpireActivatedAccess)
        {
            var activeGrants = await _db.StudentAccessGrants
                .Where(grant => grant.AccessCodeId.HasValue
                    && grant.AccessCode!.CodeGroupId == group.Id
                    && grant.IsActive)
                .ToListAsync(ct);
            foreach (var grant in activeGrants)
            {
                grant.ExpiresAt = expiresAt;
                grant.UpdatedAt = DateTime.UtcNow;
            }
        }

        group.RevenueOwner = request.RevenueOwner;
        group.RevenueAllocationMode = request.RevenueAllocationMode;
        group.RevenueAllocationValue = request.RevenueAllocationValue;
        group.AccountingTiming = group.AccountingRecordedAt.HasValue
            ? group.AccountingTiming
            : request.AccountingTiming;
        if (financialTerms is not null && financialChange)
        {
            financialTerms.Trigger = group.AccountingTiming == CodeAccountingTiming.Immediate ? TeacherAgreementTrigger.CodeDelivery : TeacherAgreementTrigger.CodeActivation;
            financialTerms.AgreementId = null;
            financialTerms.UpdatedByUserId = request.AdminId;
            financialTerms.UpdatedAt = DateTime.UtcNow;
        }
        group.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            action: "UpdateCodeGroupSettings",
            entityType: "CodeGroup",
            entityId: group.Id,
            userId: request.AdminId,
            oldValues: oldValues,
            newValues: new
            {
                group.Name,
                group.TeacherId,
                group.ExpiresAt,
                group.RevenueOwner,
                group.RevenueAllocationMode,
                group.RevenueAllocationValue,
                group.AccountingTiming
            });

        await transaction.CommitAsync(ct);
        return ApiResponse.Ok();
    }
}
