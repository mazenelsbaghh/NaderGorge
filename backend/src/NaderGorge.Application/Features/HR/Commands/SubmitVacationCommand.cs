using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public record SubmitVacationCommand(
    Guid UserId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason
) : IRequest<ApiResponse<Guid>>;

public class SubmitVacationCommandValidator : AbstractValidator<SubmitVacationCommand>
{
    public SubmitVacationCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x).Must(x => x.EndDate >= x.StartDate).WithMessage("End date must be on or after start date.");
    }
}

public class SubmitVacationCommandHandler : IRequestHandler<SubmitVacationCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public SubmitVacationCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<Guid>> Handle(SubmitVacationCommand request, CancellationToken ct)
    {
        var profile = await _db.EmployeeProfiles
            .FirstOrDefaultAsync(ep => ep.UserId == request.UserId, ct);

        if (profile == null)
        {
            throw new KeyNotFoundException("No employee profile found for this user.");
        }

        // Verify no overlapping vacation requests (Pending or Approved)
        var hasOverlap = await _db.EmployeeVacations
            .AnyAsync(ev => ev.EmployeeId == profile.Id
                            && ev.Status != VacationStatus.Rejected
                            && request.StartDate <= ev.EndDate
                            && request.EndDate >= ev.StartDate, ct);

        if (hasOverlap)
        {
            throw new InvalidOperationException("You have an overlapping vacation request for this date range.");
        }

        var vacation = new EmployeeVacation
        {
            EmployeeId = profile.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason,
            Status = VacationStatus.Pending
        };

        _db.EmployeeVacations.Add(vacation);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(vacation.Id);
    }
}
