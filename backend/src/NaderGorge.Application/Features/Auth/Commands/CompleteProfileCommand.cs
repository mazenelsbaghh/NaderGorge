using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

public record CompleteProfileCommand(Guid UserId, string ParentPhone, string Governorate) : IRequest<ApiResponse>;

public class CompleteProfileCommandValidator : AbstractValidator<CompleteProfileCommand>
{
    public CompleteProfileCommandValidator()
    {
        RuleFor(x => x.ParentPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Governorate).NotEmpty().MaximumLength(100);
    }
}

public class CompleteProfileCommandHandler : IRequestHandler<CompleteProfileCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public CompleteProfileCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(CompleteProfileCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.StudentProfile)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new KeyNotFoundException("User not found");

        if (user.IsProfileComplete)
            return ApiResponse.Ok("Profile already complete");

        if (user.StudentProfile == null)
        {
            user.StudentProfile = new Domain.Entities.StudentProfile { UserId = user.Id };
            _db.StudentProfiles.Add(user.StudentProfile);
        }

        user.StudentProfile.ParentPhone = request.ParentPhone;
        user.StudentProfile.Governorate = request.Governorate;
        user.IsProfileComplete = true;

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("Profile completed successfully");
    }
}
