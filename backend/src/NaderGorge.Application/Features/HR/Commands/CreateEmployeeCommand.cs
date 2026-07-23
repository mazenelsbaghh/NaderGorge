using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public record CreateEmployeeCommand(
    string FullName,
    string PhoneNumber,
    string Password,
    string Role,
    decimal BasicSalary,
    string StandardStartTime,
    int TargetDailyHours,
    Guid ActorUserId,
    string IdempotencyKey) : IRequest<ApiResponse<CreateEmployeeResult>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.EmployeeManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
}

public record CreateEmployeeResult(
    Guid EmployeeId,
    string EmployeeNumber,
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string Role,
    DateTime? UpdatedAt);

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(item => item.FullName).NotEmpty().MaximumLength(200);
        RuleFor(item => item.PhoneNumber).NotEmpty().Matches("^01[0125][0-9]{8}$");
        RuleFor(item => item.Password).MinimumLength(6);
        RuleFor(item => item.Role).NotEmpty();
        RuleFor(item => item.BasicSalary).GreaterThanOrEqualTo(0);
        RuleFor(item => item.StandardStartTime)
            .Must(value => TimeSpan.TryParse(value, out _))
            .WithMessage("Time must be in format hh:mm or hh:mm:ss");
        RuleFor(item => item.TargetDailyHours).InclusiveBetween(1, 24);
        RuleFor(item => item.ActorUserId).NotEmpty();
        RuleFor(item => item.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateEmployeeCommandHandler
    : IRequestHandler<CreateEmployeeCommand, ApiResponse<CreateEmployeeResult>>
{
    private readonly IAppDbContext _db;
    private readonly IHrRequestContext? _requestContext;
    private readonly IHrAuditWriter _audit;

    public CreateEmployeeCommandHandler(IAppDbContext db, IHrRequestContext? requestContext = null, IHrAuditWriter? audit = null)
    {
        _db = db;
        _requestContext = requestContext;
        _audit = audit ?? new HrAuditWriter(db, requestContext ?? DetachedHrRequestContext.Instance);
    }

    public async Task<ApiResponse<CreateEmployeeResult>> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        var requestJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            request.FullName,
            request.PhoneNumber,
            request.Role,
            request.BasicSalary,
            request.StandardStartTime,
            request.TargetDailyHours
        });
        var requestHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(requestJson)));
        var replay = await _db.HrIdempotencyRecords.FirstOrDefaultAsync(item =>
            item.Scope == "employee.provision" &&
            item.ActorUserId == request.ActorUserId &&
            item.Key == request.IdempotencyKey, ct);
        if (replay is not null)
        {
            if (!string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return ApiResponse<CreateEmployeeResult>.Fail(
                    "مفتاح إعادة المحاولة مستخدم لطلب مختلف",
                    new List<string> { "IDEMPOTENCY_KEY_REUSED" });
            }

            var replayResult = replay.ResponseJson is null
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<CreateEmployeeResult>(replay.ResponseJson);
            return replayResult is null
                ? ApiResponse<CreateEmployeeResult>.Fail("تعذر استعادة نتيجة الطلب", new List<string> { "IDEMPOTENCY_RESULT_MISSING" })
                : ApiResponse<CreateEmployeeResult>.Ok(replayResult, "تم استعادة نتيجة إنشاء الموظف السابقة");
        }

        var phone = request.PhoneNumber.Trim();
        if (await _db.Users.AnyAsync(user => user.PhoneNumber == phone, ct))
        {
            return ApiResponse<CreateEmployeeResult>.Fail(
                "رقم الهاتف مسجل بالفعل",
                new List<string> { "PHONE_ALREADY_EXISTS" });
        }

        var role = await _db.Roles.FirstOrDefaultAsync(
            item => item.Name.ToLower() == request.Role.Trim().ToLower(),
            ct);
        if (role is null)
        {
            return ApiResponse<CreateEmployeeResult>.Fail(
                "الدور المحدد غير موجود",
                new List<string> { "ROLE_NOT_FOUND" });
        }

        if (!TimeSpan.TryParse(request.StandardStartTime, out var startTime))
        {
            return ApiResponse<CreateEmployeeResult>.Fail(
                "وقت بدء العمل غير صالح",
                new List<string> { "EMPLOYEE_START_TIME_INVALID" });
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            IsProfileComplete = true
        };
        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            User = user,
            BasicSalary = request.BasicSalary,
            StandardStartTime = startTime,
            TargetDailyHours = request.TargetDailyHours
        };
        profile.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(profile.Id);

        _db.Users.Add(user);
        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        _db.EmployeeProfiles.Add(profile);
        var result = new CreateEmployeeResult(
            profile.Id,
            profile.EmployeeNumber,
            user.Id,
            user.FullName,
            user.PhoneNumber,
            role.Name,
            profile.UpdatedAt);
        await _audit.WriteMutationAsync("CreateEmployee", nameof(EmployeeProfile), profile.Id, null, new
        {
            userId = user.Id,
            profile.EmployeeNumber,
            role = role.Name,
            profile.BasicSalary,
            standardStartTime = profile.StandardStartTime.ToString(),
            profile.TargetDailyHours
        }, "Provision employee account and profile", ct, request.ActorUserId);
        _db.HrIdempotencyRecords.Add(new HrIdempotencyRecord
        {
            Scope = "employee.provision",
            ActorUserId = request.ActorUserId,
            Key = request.IdempotencyKey,
            RequestHash = requestHash,
            ResultEntityId = profile.Id,
            ResponseJson = System.Text.Json.JsonSerializer.Serialize(result),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await _db.SaveChangesAsync(ct);

        return ApiResponse<CreateEmployeeResult>.Ok(
            result,
            "تم إنشاء حساب الموظف وملفه الوظيفي بنجاح");
    }
}
