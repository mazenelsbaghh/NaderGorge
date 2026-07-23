using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Application.Services;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpdatePackageCommand(Guid Id, string Name, string Description, decimal Price, bool IsActive, IReadOnlyList<AcademicScopeDto>? AcademicScopes = null, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class UpdatePackageCommandHandler : IRequestHandler<UpdatePackageCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdatePackageCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdatePackageCommand request, CancellationToken ct)
    {
        var package = await _db.Packages.FindAsync(new object[] { request.Id }, ct);
        if (package == null) return ApiResponse.Fail("Package not found");

        if (request.AcademicScopes != null)
        {
            await ContentAcademicScopeValidation.EnsureExactScopeSubjectEligibilityAsync(_db, request.AcademicScopes, ct);
            var validation = await new AcademicScopeService(_db).ValidateScopeDtosAsync(request.AcademicScopes, ct);
            if (!validation.IsValid)
                return ApiResponse.Fail(validation.Message ?? "نطاق الباقة الأكاديمي غير صالح.", new List<string> { validation.ErrorCode ?? "ACADEMIC_SCOPE_INVALID" });
        }

        bool wasActive = package.IsActive;
        package.Name = request.Name;
        package.Description = request.Description;
        package.Price = request.Price;
        package.IsActive = request.IsActive;
        if (request.AcademicScopes != null)
        {
            package.TargetGrade = string.Join(',', ContentAcademicScopeValidation.GetTargetGrades(
                request.AcademicScopes,
                package.TargetGrade));
        }

        var outboxEvent = new OutboxEvent
        {
            Type = "PackageUpdated",
            TargetGroup = $"Package_{package.Id}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                packageId = package.Id,
                name = package.Name,
                price = package.Price,
                isActive = package.IsActive
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        if (wasActive && !package.IsActive)
        {
            var archiveEvent = new OutboxEvent
            {
                Type = "PackageArchived",
                TargetGroup = $"Package_{package.Id}",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    packageId = package.Id
                })
            };
            _db.OutboxEvents.Add(archiveEvent);
        }
        else if (!wasActive && package.IsActive)
        {
            var publishEvent = new OutboxEvent
            {
                Type = "PackagePublished",
                TargetGroup = "Role_Student",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    packageId = package.Id,
                    name = package.Name,
                    price = package.Price
                })
            };
            _db.OutboxEvents.Add(publishEvent);
        }

        await _db.SaveChangesAsync(ct);
        if (request.AcademicScopes != null)
        {
            await new AcademicScopeService(_db).SyncOwnerScopesAsync(
                StudentFacingScopeOwnerType.Package,
                package.Id,
                request.AcademicScopes,
                request.CurrentUserId,
                ct);
        }

        return ApiResponse.Ok();
    }
}
