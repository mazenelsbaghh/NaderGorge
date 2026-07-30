using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Lifecycle;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController, Route("api/hr"), Authorize]
public sealed class HrDocumentsAssetsController(IAppDbContext db, DocumentAssetService service) : ControllerBase
{
    [HttpGet("self/documents"), HasPermission(HrPermissions.DocumentSelf)]
    public async Task<IActionResult> MyDocuments(CancellationToken ct)
    {
        var userId = User.RequireUserId(); return Ok(await db.EmployeeDocuments.AsNoTracking().Where(item => item.Employee!.UserId == userId && !item.IsArchived)
            .OrderBy(item => item.Name).Select(item => new { item.Id, category = item.Category.ToString(), item.Name, item.IssuedOn, item.ExpiresOn,
                latestVersion = item.Versions.Max(version => (int?)version.Version), latestHash = item.Versions.OrderByDescending(version => version.Version).Select(version => version.ContentHash).FirstOrDefault() }).ToListAsync(ct));
    }

    [HttpGet("self/documents/{documentId:guid}/download"), HasPermission(HrPermissions.DocumentSelf)]
    public async Task<IActionResult> DownloadMine(Guid documentId, CancellationToken ct)
    {
        var result = await service.AuthorizeDownloadAsync(documentId, User.RequireUserId(), false, ct); return result.Success ? Ok(result) : Forbid();
    }

    [HttpGet("admin/documents/{documentId:guid}/download"), HasPermission(HrPermissions.DocumentRead)]
    public async Task<IActionResult> DownloadAdmin(Guid documentId, CancellationToken ct)
    {
        var result = await service.AuthorizeDownloadAsync(documentId, User.RequireUserId(), true, ct); return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("admin/documents"), HasPermission(HrPermissions.DocumentManage)]
    public async Task<IActionResult> AddDocument(AddEmployeeDocumentRequest request, CancellationToken ct)
    {
        var result = await service.AddDocumentVersionAsync(request.EmployeeId, request.DocumentId, request.Category, request.Name, request.AssetReference,
            request.ContentHash, request.MimeType, request.SizeBytes, User.RequireUserId(), request.ExpiresOn, ct); return Ok(result);
    }

    [HttpGet("self/assets"), HasPermission(HrPermissions.AssetSelf)]
    public async Task<IActionResult> MyAssets(CancellationToken ct)
    {
        var userId = User.RequireUserId(); return Ok(await db.AssetCustodies.AsNoTracking().Where(item => item.Employee!.UserId == userId)
            .OrderByDescending(item => item.AssignedAt).Select(item => new { item.Id, item.AssetId, asset = item.Asset!.Name, item.Asset.Code, item.Asset.SerialNumber,
                state = item.State.ToString(), item.AssignedAt, item.AssignedCondition, item.ReturnedAt, item.ReturnCondition }).ToListAsync(ct));
    }

    [HttpGet("admin/assets"), HasPermission(HrPermissions.AssetRead)]
    public async Task<IActionResult> Assets(CancellationToken ct) => Ok(await db.HrAssets.AsNoTracking().OrderBy(item => item.Code)
        .Select(item => new { item.Id, item.Code, item.Name, item.SerialNumber, item.Value, status = item.Status.ToString() }).ToListAsync(ct));

    [HttpPost("admin/assets"), HasPermission(HrPermissions.AssetManage)]
    public async Task<IActionResult> CreateAsset(CreateAssetRequest request, CancellationToken ct)
    {
        var asset = new HrAsset { Code = request.Code.Trim().ToUpper(), Name = request.Name.Trim(), SerialNumber = request.SerialNumber, Value = request.Value };
        db.HrAssets.Add(asset); await db.SaveChangesAsync(ct); return Ok(new { asset.Id });
    }

    [HttpPost("admin/assets/{assetId:guid}/assign"), HasPermission(HrPermissions.AssetManage)]
    public async Task<IActionResult> Assign(Guid assetId, AssignAssetRequest request, CancellationToken ct)
    {
        var result = await service.AssignAssetAsync(assetId, request.EmployeeId, User.RequireUserId(), request.Condition, ct); return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("admin/assets/custodies/{custodyId:guid}/return"), HasPermission(HrPermissions.AssetManage)]
    public async Task<IActionResult> Return(Guid custodyId, CloseCustodyRequest request, CancellationToken ct) => Ok(await service.ReturnAssetAsync(custodyId, User.RequireUserId(), request.Reason, ct));

    [HttpPost("admin/assets/custodies/{custodyId:guid}/waive"), HasPermission(HrPermissions.AssetManage)]
    public async Task<IActionResult> Waive(Guid custodyId, CloseCustodyRequest request, CancellationToken ct) => Ok(await service.WaiveCustodyAsync(custodyId, User.RequireUserId(), request.Reason, ct));

    [HttpGet("admin/assets/offboarding-check/{employeeId:guid}"), HasPermission(HrPermissions.AssetManage)]
    public async Task<IActionResult> OffboardingCheck(Guid employeeId, CancellationToken ct) => Ok(new { allowed = await service.CanOffboardAsync(employeeId, ct) });
}

public sealed record AddEmployeeDocumentRequest(Guid EmployeeId, Guid? DocumentId, EmployeeDocumentCategory Category, string Name, string AssetReference, string ContentHash, string MimeType, long SizeBytes, DateOnly? ExpiresOn);
public sealed record CreateAssetRequest(string Code, string Name, string? SerialNumber, decimal Value);
public sealed record AssignAssetRequest(Guid EmployeeId, string Condition);
public sealed record CloseCustodyRequest(string Reason);
