using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpdateStaffProfileCommand(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    Guid AdminId
) : IRequest<ApiResponse>;

public class UpdateStaffProfileCommandValidator : AbstractValidator<UpdateStaffProfileCommand>
{
    public UpdateStaffProfileCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.AdminId).NotEmpty();
    }
}

public class UpdateStaffProfileCommandHandler : IRequestHandler<UpdateStaffProfileCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateStaffProfileCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateStaffProfileCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == request.UserId, ct);

        if (user is null)
            return ApiResponse.Fail("حساب الموظف غير موجود.");

        if (user.UserRoles.Count == 1 && user.UserRoles.First().Role.Name.Equals("Student", StringComparison.OrdinalIgnoreCase))
            return ApiResponse.Fail("هذا المسار مخصص لحسابات الموظفين فقط.");

        var fullName = request.FullName.Trim();
        var phoneNumber = request.PhoneNumber.Trim();

        if (await _db.Users.AnyAsync(item => item.Id != user.Id && item.PhoneNumber == phoneNumber, ct))
            return ApiResponse.Fail("رقم الهاتف مسجل بالفعل.");

        var oldValues = JsonSerializer.Serialize(new
        {
            fullName = user.FullName,
            phoneNumber = user.PhoneNumber
        });

        user.FullName = fullName;
        user.PhoneNumber = phoneNumber;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateStaffProfile",
            EntityType = nameof(User),
            EntityId = user.Id,
            PerformedByUserId = request.AdminId,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { fullName, phoneNumber }),
            IpAddress = "System"
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("تم تحديث بيانات الموظف بنجاح.");
    }
}
