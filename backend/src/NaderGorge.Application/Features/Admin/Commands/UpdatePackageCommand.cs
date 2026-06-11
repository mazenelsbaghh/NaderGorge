using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpdatePackageCommand(Guid Id, string Name, string Description, decimal Price, bool IsActive) : IRequest<ApiResponse>;

public class UpdatePackageCommandHandler : IRequestHandler<UpdatePackageCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdatePackageCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdatePackageCommand request, CancellationToken ct)
    {
        var package = await _db.Packages.FindAsync(new object[] { request.Id }, ct);
        if (package == null) return ApiResponse.Fail("Package not found");

        package.Name = request.Name;
        package.Description = request.Description;
        package.Price = request.Price;
        package.IsActive = request.IsActive;

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

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
