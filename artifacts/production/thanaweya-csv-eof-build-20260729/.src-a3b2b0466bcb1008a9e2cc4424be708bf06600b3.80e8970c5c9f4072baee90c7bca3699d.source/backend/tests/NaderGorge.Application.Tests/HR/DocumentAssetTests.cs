using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Lifecycle;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.HR;

public sealed class DocumentAssetTests
{
    [Fact]
    public async Task DocumentVersionsIncrementAndCrossEmployeeDownloadIsDeniedAndAudited()
    {
        await using var db = TestAppDbContextFactory.Create(); var seed = await SeedAsync(db); var service = new DocumentAssetService(db);
        var documentId = await service.AddDocumentVersionAsync(seed.Employee.Id, null, EmployeeDocumentCategory.Identity, "ID", "secure/id-v1.pdf", "hash1", "application/pdf", 10, seed.User.Id, null, default);
        await service.AddDocumentVersionAsync(seed.Employee.Id, documentId.Data, EmployeeDocumentCategory.Identity, "ID", "secure/id-v2.pdf", "hash2", "application/pdf", 11, seed.User.Id, null, default);
        Assert.Equal([1, 2], db.EmployeeDocumentVersions.OrderBy(item => item.Version).Select(item => item.Version));
        var denied = await service.AuthorizeDownloadAsync(documentId.Data, seed.Other.Id, false, default); Assert.False(denied.Success);
        var allowed = await service.AuthorizeDownloadAsync(documentId.Data, seed.User.Id, false, default); Assert.True(allowed.Success); Assert.Equal("secure/id-v2.pdf", allowed.Data);
        Assert.Contains(db.AuditLogs, item => item.Action == "DownloadEmployeeDocument" && item.PerformedByUserId == seed.User.Id);
    }

    [Fact]
    public async Task LegalHoldPreventsRetentionAndOpenCustodyBlocksOffboardingUnlessWaived()
    {
        await using var db = TestAppDbContextFactory.Create(); var seed = await SeedAsync(db); var service = new DocumentAssetService(db);
        var held = new EmployeeDocument { EmployeeId = seed.Employee.Id, Name = "Held", LegalHold = true, RetainUntil = new DateOnly(2025, 1, 1) };
        var expired = new EmployeeDocument { EmployeeId = seed.Employee.Id, Name = "Expired", RetainUntil = new DateOnly(2025, 1, 1) };
        db.EmployeeDocuments.AddRange(held, expired); var asset = new HrAsset { Code = "LAP-1", Name = "Laptop" }; db.HrAssets.Add(asset); await db.SaveChangesAsync();
        await service.AssignAssetAsync(asset.Id, seed.Employee.Id, seed.Other.Id, "new", default);
        Assert.Equal([expired.Id], await service.RetentionCandidatesAsync(new DateOnly(2026, 1, 1), default)); Assert.False(await service.CanOffboardAsync(seed.Employee.Id, default));
        var custody = await db.AssetCustodies.SingleAsync(); await service.WaiveCustodyAsync(custody.Id, seed.Other.Id, "approved loss settlement", default);
        Assert.True(await service.CanOffboardAsync(seed.Employee.Id, default));
    }

    private static async Task<(User User, User Other, EmployeeProfile Employee)> SeedAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Document Employee", "01075555551"); var other = await TestAppDbContextFactory.SeedUserAsync(db, "Other", "01075555552");
        var employee = new EmployeeProfile { UserId = user.Id, User = user }; employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id); db.EmployeeProfiles.Add(employee); await db.SaveChangesAsync(); return (user, other, employee);
    }
}
