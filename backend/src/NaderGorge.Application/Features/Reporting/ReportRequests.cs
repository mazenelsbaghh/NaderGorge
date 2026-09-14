using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Reporting;

public sealed record GetReportCatalogQuery(bool IsTeacher) : IRequest<ApiResponse<ReportCatalogDto>>;
public sealed record ExecuteReportQuery(Guid ActorUserId, bool IsTeacher, ExecuteReportRequest Request) : IRequest<ApiResponse<ReportResultDto>>;
public sealed record GetReportDefinitionsQuery(Guid OwnerUserId, bool IsTeacher) : IRequest<ApiResponse<IReadOnlyList<ReportDefinitionDto>>>;
public sealed record GetReportDefinitionQuery(Guid Id, Guid OwnerUserId, bool IsTeacher) : IRequest<ApiResponse<ReportDefinitionDto>>;
public sealed record CreateReportDefinitionCommand(Guid OwnerUserId, bool IsTeacher, SaveReportDefinitionRequest Request) : IRequest<ApiResponse<ReportDefinitionDto>>;
public sealed record UpdateReportDefinitionCommand(Guid Id, Guid OwnerUserId, bool IsTeacher, UpdateReportDefinitionRequest Request) : IRequest<ApiResponse<ReportDefinitionDto>>;
public sealed record CopyReportDefinitionCommand(Guid Id, Guid OwnerUserId, bool IsTeacher, CopyReportDefinitionRequest Request) : IRequest<ApiResponse<ReportDefinitionDto>>;
public sealed record DeleteReportDefinitionCommand(Guid Id, Guid OwnerUserId) : IRequest<ApiResponse>;

public sealed class GetReportCatalogQueryHandler : IRequestHandler<GetReportCatalogQuery, ApiResponse<ReportCatalogDto>>
{
    public Task<ApiResponse<ReportCatalogDto>> Handle(GetReportCatalogQuery request, CancellationToken ct) =>
        Task.FromResult(ApiResponse<ReportCatalogDto>.Ok(ReportCatalog.Get(request.IsTeacher)));
}

public sealed class ExecuteReportQueryHandler : IRequestHandler<ExecuteReportQuery, ApiResponse<ReportResultDto>>
{
    private readonly IReportQueryService _reports;
    public ExecuteReportQueryHandler(IReportQueryService reports) => _reports = reports;

    public async Task<ApiResponse<ReportResultDto>> Handle(ExecuteReportQuery request, CancellationToken ct)
    {
        try
        {
            var result = await _reports.ExecuteAsync(request.Request, request.ActorUserId, request.IsTeacher, ct);
            return ApiResponse<ReportResultDto>.Ok(result);
        }
        catch (ArgumentException exception) { return ApiResponse<ReportResultDto>.Fail(exception.Message); }
        catch (UnauthorizedAccessException exception) { return ApiResponse<ReportResultDto>.Fail(exception.Message); }
    }
}

public sealed class ReportDefinitionRequestHandler :
    IRequestHandler<GetReportDefinitionsQuery, ApiResponse<IReadOnlyList<ReportDefinitionDto>>>,
    IRequestHandler<GetReportDefinitionQuery, ApiResponse<ReportDefinitionDto>>,
    IRequestHandler<CreateReportDefinitionCommand, ApiResponse<ReportDefinitionDto>>,
    IRequestHandler<UpdateReportDefinitionCommand, ApiResponse<ReportDefinitionDto>>,
    IRequestHandler<CopyReportDefinitionCommand, ApiResponse<ReportDefinitionDto>>,
    IRequestHandler<DeleteReportDefinitionCommand, ApiResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAppDbContext _db;
    private readonly IReportQueryService _reports;

    public ReportDefinitionRequestHandler(IAppDbContext db, IReportQueryService reports)
    {
        _db = db;
        _reports = reports;
    }

    public async Task<ApiResponse<IReadOnlyList<ReportDefinitionDto>>> Handle(GetReportDefinitionsQuery request, CancellationToken ct)
    {
        var allowedDomains = ReportCatalog.Get(request.IsTeacher).Domains.Select(domain => domain.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entities = await _db.ReportDefinitions.AsNoTracking()
            .Where(report => report.OwnerUserId == request.OwnerUserId)
            .OrderByDescending(report => report.UpdatedAt ?? report.CreatedAt)
            .ToListAsync(ct);
        var definitions = entities.Where(entity => allowedDomains.Contains(entity.Domain)).Select(ToDto).ToArray();
        return ApiResponse<IReadOnlyList<ReportDefinitionDto>>.Ok(definitions);
    }

    public async Task<ApiResponse<ReportDefinitionDto>> Handle(GetReportDefinitionQuery request, CancellationToken ct)
    {
        var entity = await FindOwnedAsync(request.Id, request.OwnerUserId, ct);
        if (entity == null) return ApiResponse<ReportDefinitionDto>.Fail("التقرير المحفوظ غير موجود.");
        if (ReportCatalog.Find(entity.Domain, request.IsTeacher) == null)
            return ApiResponse<ReportDefinitionDto>.Fail("التقرير لم يعد متاحًا لصلاحيات الحساب الحالية.");
        return ApiResponse<ReportDefinitionDto>.Ok(ToDto(entity));
    }

    public async Task<ApiResponse<ReportDefinitionDto>> Handle(CreateReportDefinitionCommand request, CancellationToken ct)
    {
        try { await _reports.ValidateAsync(request.Request.Configuration, request.IsTeacher, ct); }
        catch (ArgumentException exception) { return ApiResponse<ReportDefinitionDto>.Fail(exception.Message); }

        var entity = new ReportDefinition
        {
            OwnerUserId = request.OwnerUserId,
            Name = request.Request.Name.Trim(),
            Domain = request.Request.Configuration.Domain,
            ConfigurationJson = JsonSerializer.Serialize(request.Request.Configuration, JsonOptions)
        };
        _db.ReportDefinitions.Add(entity);
        AddAudit("ReportDefinitionCreated", entity, request.OwnerUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<ReportDefinitionDto>.Ok(ToDto(entity), "تم حفظ التقرير.");
    }

    public async Task<ApiResponse<ReportDefinitionDto>> Handle(UpdateReportDefinitionCommand request, CancellationToken ct)
    {
        var entity = await FindOwnedAsync(request.Id, request.OwnerUserId, ct);
        if (entity == null) return ApiResponse<ReportDefinitionDto>.Fail("التقرير المحفوظ غير موجود.");
        if (request.Request.Version.HasValue && request.Request.Version.Value != entity.Version)
            return ApiResponse<ReportDefinitionDto>.Fail("تم تعديل التقرير من جلسة أخرى. أعد تحميله ثم حاول مجددًا.");
        try { await _reports.ValidateAsync(request.Request.Configuration, request.IsTeacher, ct); }
        catch (ArgumentException exception) { return ApiResponse<ReportDefinitionDto>.Fail(exception.Message); }

        entity.Name = request.Request.Name.Trim();
        entity.Domain = request.Request.Configuration.Domain;
        entity.ConfigurationJson = JsonSerializer.Serialize(request.Request.Configuration, JsonOptions);
        entity.UpdatedAt = DateTime.UtcNow;
        AddAudit("ReportDefinitionUpdated", entity, request.OwnerUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<ReportDefinitionDto>.Ok(ToDto(entity), "تم تحديث التقرير.");
    }

    public async Task<ApiResponse> Handle(DeleteReportDefinitionCommand request, CancellationToken ct)
    {
        var entity = await FindOwnedAsync(request.Id, request.OwnerUserId, ct);
        if (entity == null) return ApiResponse.Fail("التقرير المحفوظ غير موجود.");
        _db.ReportDefinitions.Remove(entity);
        AddAudit("ReportDefinitionDeleted", entity, request.OwnerUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("تم حذف التقرير.");
    }

    public async Task<ApiResponse<ReportDefinitionDto>> Handle(CopyReportDefinitionCommand request, CancellationToken ct)
    {
        var source = await FindOwnedAsync(request.Id, request.OwnerUserId, ct);
        if (source == null) return ApiResponse<ReportDefinitionDto>.Fail("التقرير المحفوظ غير موجود.");
        if (ReportCatalog.Find(source.Domain, request.IsTeacher) == null)
            return ApiResponse<ReportDefinitionDto>.Fail("التقرير لم يعد متاحًا لصلاحيات الحساب الحالية.");
        var copy = new ReportDefinition
        {
            OwnerUserId = request.OwnerUserId,
            Name = string.IsNullOrWhiteSpace(request.Request.Name) ? $"نسخة من {source.Name}" : request.Request.Name.Trim(),
            Domain = source.Domain,
            ConfigurationJson = source.ConfigurationJson,
            SchemaVersion = source.SchemaVersion
        };
        _db.ReportDefinitions.Add(copy);
        AddAudit("ReportDefinitionCopied", copy, request.OwnerUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<ReportDefinitionDto>.Ok(ToDto(copy), "تم نسخ التقرير.");
    }

    private Task<ReportDefinition?> FindOwnedAsync(Guid id, Guid ownerUserId, CancellationToken ct) =>
        _db.ReportDefinitions.FirstOrDefaultAsync(report => report.Id == id && report.OwnerUserId == ownerUserId, ct);

    private void AddAudit(string action, ReportDefinition entity, Guid actorUserId) => _db.AuditLogs.Add(new AuditLog
    {
        Action = action,
        EntityType = nameof(ReportDefinition),
        EntityId = entity.Id,
        PerformedByUserId = actorUserId,
        NewValues = JsonSerializer.Serialize(new { entity.Name, entity.Domain, entity.SchemaVersion }, JsonOptions)
    });

    private static ReportDefinitionDto ToDto(ReportDefinition entity) => new(
        entity.Id,
        entity.Name,
        entity.Domain,
        JsonSerializer.Deserialize<ExecuteReportRequest>(entity.ConfigurationJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored report configuration is invalid."),
        entity.SchemaVersion,
        entity.Version,
        entity.CreatedAt,
        entity.UpdatedAt);
}
