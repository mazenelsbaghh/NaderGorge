using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

// ---- Register Command (Step 1) ----
public record RegisterCommand(string FullName, string PhoneNumber, string Password, string? Grade, string? Track) : IRequest<ApiResponse<RegisterResponse>>;
public record RegisterResponse(Guid UserId, string Message);

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20).Matches(@"^01[0125]\d{8}$").WithMessage("Invalid Egyptian phone number");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, ApiResponse<RegisterResponse>>
{
    private readonly IAppDbContext _db;

    public RegisterCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<RegisterResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber, ct))
            throw new InvalidOperationException("Phone number already registered");

        var studentRole = await _db.Roles.FirstAsync(r => r.Type == RoleType.Student, ct);

        var user = new User
        {
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsProfileComplete = false
        };

        // Create StudentProfile with Step 1 fields
        user.StudentProfile = new StudentProfile
        {
            Grade = request.Grade,
            Track = request.Track
        };

        _db.Users.Add(user);
        _db.UserRoles.Add(new UserRole { User = user, Role = studentRole });
        await _db.SaveChangesAsync(ct);

        return ApiResponse<RegisterResponse>.Ok(new RegisterResponse(user.Id, "Registration successful. Complete your profile to activate codes."));
    }
}
