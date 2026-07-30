using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Lifecycle;

public sealed class DocumentAssetService(IAppDbContext db)
{
    public async Task<ApiResponse<Guid>> AddDocumentVersionAsync(Guid employeeId, Guid? documentId, EmployeeDocumentCategory category,
        string name, string assetReference, string contentHash, string mimeType, long sizeBytes, Guid actorUserId, DateOnly? expiresOn, CancellationToken ct)
    {
        EmployeeDocument document;
        if (documentId.HasValue)
        {
            document = await db.EmployeeDocuments.Include(item => item.Versions).SingleOrDefaultAsync(item => item.Id == documentId && item.EmployeeId == employeeId, ct)
                ?? throw new InvalidOperationException("DOCUMENT_NOT_FOUND");
        }
        else
        {
            document = new EmployeeDocument { EmployeeId = employeeId, Category = category, Name = name.Trim(), ExpiresOn = expiresOn };
            db.EmployeeDocuments.Add(document);
        }
        var version = (document.Versions.Count == 0 ? 0 : document.Versions.Max(item => item.Version)) + 1;
        var file = new EmployeeDocumentVersion { EmployeeDocumentId = document.Id, EmployeeDocument = document, Version = version,
            AssetReference = assetReference, ContentHash = contentHash, MimeType = mimeType, SizeBytes = sizeBytes, UploadedByUserId = actorUserId };
        db.EmployeeDocumentVersions.Add(file); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(document.Id);
    }

    public async Task<ApiResponse<string>> AuthorizeDownloadAsync(Guid documentId, Guid actorUserId, bool canReadAll, CancellationToken ct)
    {
        var document = await db.EmployeeDocuments.Include(item => item.Employee).Include(item => item.Versions).SingleOrDefaultAsync(item => item.Id == documentId && !item.IsArchived, ct);
        if (document is null) return ApiResponse<string>.Fail("المستند غير موجود", ["DOCUMENT_NOT_FOUND"]);
        if (!canReadAll && document.Employee?.UserId != actorUserId) return ApiResponse<string>.Fail("غير مصرح", ["DOCUMENT_ACCESS_DENIED"]);
        var latest = document.Versions.OrderByDescending(item => item.Version).FirstOrDefault();
        if (latest is null) return ApiResponse<string>.Fail("لا توجد نسخة", ["DOCUMENT_VERSION_NOT_FOUND"]);
        db.AuditLogs.Add(new AuditLog { Action = "DownloadEmployeeDocument", EntityType = nameof(EmployeeDocument), EntityId = document.Id,
            PerformedByUserId = actorUserId, ActorSnapshot = actorUserId.ToString(), Reason = "authorized download",
            NewValues = JsonSerializer.Serialize(new { document.EmployeeId, latest.Version, latest.ContentHash }) });
        await db.SaveChangesAsync(ct); return ApiResponse<string>.Ok(latest.AssetReference);
    }

    public async Task<IReadOnlyList<Guid>> RetentionCandidatesAsync(DateOnly today, CancellationToken ct) => await db.EmployeeDocuments.AsNoTracking()
        .Where(item => !item.LegalHold && !item.IsArchived && item.RetainUntil.HasValue && item.RetainUntil < today).Select(item => item.Id).ToListAsync(ct);

    public async Task<int> QueueExpiryAlertsAsync(DateOnly today, int daysAhead, CancellationToken ct)
    {
        var cutoff = today.AddDays(daysAhead); var documents = await db.EmployeeDocuments.Include(item => item.Employee)
            .Where(item => !item.IsArchived && item.ExpiresOn >= today && item.ExpiresOn <= cutoff).ToListAsync(ct); var count = 0;
        foreach (var document in documents)
        {
            var key = $"{document.Id:N}:{document.ExpiresOn:yyyyMMdd}";
            if (await db.HrIdempotencyRecords.AnyAsync(item => item.Scope == "document-expiry" && item.Key == key, ct)) continue;
            db.HrIdempotencyRecords.Add(new HrIdempotencyRecord { Scope = "document-expiry", Key = key, ActorUserId = Guid.Empty, RequestHash = key, ResultEntityId = document.Id, ExpiresAt = DateTime.UtcNow.AddYears(2) });
            db.OutboxEvents.Add(new OutboxEvent { Type = "hr.document.expiring", TargetUserId = document.Employee?.UserId.ToString(),
                PayloadJson = JsonSerializer.Serialize(new { document.Id, document.Name, document.ExpiresOn }) }); count++;
        }
        if (count > 0) await db.SaveChangesAsync(ct); return count;
    }

    public async Task<ApiResponse<Guid>> AssignAssetAsync(Guid assetId, Guid employeeId, Guid actorUserId, string condition, CancellationToken ct)
    {
        var asset = await db.HrAssets.SingleOrDefaultAsync(item => item.Id == assetId, ct);
        if (asset is null || asset.Status != HrAssetStatus.Available) return ApiResponse<Guid>.Fail("العهدة غير متاحة", ["ASSET_NOT_AVAILABLE"]);
        var custody = new AssetCustody { AssetId = assetId, EmployeeId = employeeId, AssignedAt = DateTime.UtcNow,
            AssignedByUserId = actorUserId, AssignedCondition = condition.Trim() }; asset.Status = HrAssetStatus.Assigned; db.AssetCustodies.Add(custody);
        await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(custody.Id);
    }

    public Task<bool> CanOffboardAsync(Guid employeeId, CancellationToken ct) => db.AssetCustodies.AsNoTracking()
        .AllAsync(item => item.EmployeeId != employeeId || item.State != AssetCustodyState.Active || item.ExceptionApprovedByUserId.HasValue, ct);

    public async Task<ApiResponse<bool>> ReturnAssetAsync(Guid custodyId, Guid actorUserId, string condition, CancellationToken ct)
    {
        var custody = await db.AssetCustodies.Include(item => item.Asset).SingleOrDefaultAsync(item => item.Id == custodyId, ct);
        if (custody is null || custody.State != AssetCustodyState.Active) return ApiResponse<bool>.Fail("العهدة غير مفتوحة", ["CUSTODY_NOT_ACTIVE"]);
        custody.State = AssetCustodyState.Returned; custody.ReturnedAt = DateTime.UtcNow; custody.ClosedByUserId = actorUserId; custody.ReturnCondition = condition.Trim(); custody.Asset!.Status = HrAssetStatus.Available;
        await db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> WaiveCustodyAsync(Guid custodyId, Guid actorUserId, string reason, CancellationToken ct)
    {
        var custody = await db.AssetCustodies.Include(item => item.Asset).SingleOrDefaultAsync(item => item.Id == custodyId, ct);
        if (custody is null || custody.State != AssetCustodyState.Active || string.IsNullOrWhiteSpace(reason)) return ApiResponse<bool>.Fail("لا يمكن اعتماد الاستثناء", ["CUSTODY_WAIVER_INVALID"]);
        custody.State = AssetCustodyState.Waived; custody.ExceptionApprovedByUserId = actorUserId; custody.ExceptionReason = reason.Trim(); custody.ClosedByUserId = actorUserId; custody.Asset!.Status = HrAssetStatus.Retired;
        await db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }
}
