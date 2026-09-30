using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using FluentValidation;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateFormCommand(
    string Title,
    string Description,
    string Slug,
    bool IsActive,
    string? CoverImageUrl,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    string FieldsJson
) : IRequest<ApiResponse<Guid>>
{
    public Guid PerformedByUserId { get; init; }
    public string? OperationId { get; init; }
}

public class CreateFormCommandValidator : AbstractValidator<CreateFormCommand>
{
    public CreateFormCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches(@"^[a-zA-Z0-9-_]+$").WithMessage("Slug must contain only alphanumeric characters, dashes, or underscores.");
        RuleFor(x => x.FieldsJson).NotEmpty();
    }
}

public class CreateFormCommandHandler : IRequestHandler<CreateFormCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    public CreateFormCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateFormCommand request, CancellationToken ct)
    {
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<Guid>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await CreateOnceAsync(request, null, null, ct);
        if (request.PerformedByUserId == Guid.Empty)
            return ApiResponse<Guid>.Fail("Operation actor is required.", ["INVALID_OPERATION_ACTOR"]);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.Title, request.Description, request.Slug,
                request.IsActive, request.CoverImageUrl, request.StartsAt, request.ExpiresAt,
                request.FieldsJson, request.PerformedByUserId }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
            return prior.Scope == "form.create" && prior.ActorUserId == request.PerformedByUserId
                && prior.RequestHash == requestHash
                ? ApiResponse<Guid>.Ok(prior.ResultEntityId)
                : ApiResponse<Guid>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);
        if (request.IsActive)
            return ApiResponse<Guid>.Fail("Form activation requires a high-risk action.",
                ["RISK_REQUIRES_STRONG_CONFIRMATION"]);

        var created = await CreateOnceAsync(request, operationId, requestHash, ct);
        await transaction.CommitAsync(ct);
        return created;
    }

    private async Task<ApiResponse<Guid>> CreateOnceAsync(CreateFormCommand request,
        string? operationId, string? requestHash, CancellationToken ct)
    {
        var normalizedSlug = request.Slug.ToLowerInvariant();
        var slugExists = await _db.CustomForms.AnyAsync(f => f.Slug == normalizedSlug, ct);
        if (slugExists) return ApiResponse<Guid>.Fail("الرابط المختصر (slug) مستخدم بالفعل في نموذج آخر.");

        // Basic JSON validation
        try
        {
            using var doc = JsonDocument.Parse(request.FieldsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ApiResponse<Guid>.Fail("حقول النموذج (FieldsJson) يجب أن تكون مصفوفة JSON.");
            }
        }
        catch (JsonException)
        {
            return ApiResponse<Guid>.Fail("تنسيق حقول النموذج (FieldsJson) غير صالح.");
        }

        var form = new CustomForm
        {
            Title = request.Title,
            Description = request.Description,
            Slug = normalizedSlug,
            IsActive = request.IsActive,
            CoverImageUrl = request.CoverImageUrl,
            StartsAt = request.StartsAt,
            ExpiresAt = request.ExpiresAt,
            FieldsJson = request.FieldsJson
        };

        _db.CustomForms.Add(form);
        if (operationId is not null)
            _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
            {
                OperationId = operationId, Scope = "form.create",
                ActorUserId = request.PerformedByUserId, RequestHash = requestHash!, ResultEntityId = form.Id
            });
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(form.Id);
    }
}

// --- Update Form ---
public record UpdateFormCommand(
    Guid Id,
    string Title,
    string Description,
    string Slug,
    bool IsActive,
    string? CoverImageUrl,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    string FieldsJson
) : IRequest<ApiResponse>
{
    public Guid PerformedByUserId { get; init; }
    public string? OperationId { get; init; }
}

public class UpdateFormCommandValidator : AbstractValidator<UpdateFormCommand>
{
    public UpdateFormCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches(@"^[a-zA-Z0-9-_]+$").WithMessage("Slug must contain only alphanumeric characters, dashes, or underscores.");
        RuleFor(x => x.FieldsJson).NotEmpty();
    }
}

public class UpdateFormCommandHandler : IRequestHandler<UpdateFormCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    public UpdateFormCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateFormCommand request, CancellationToken ct)
    {
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await UpdateOnceAsync(request, null, null, ct);
        if (request.PerformedByUserId == Guid.Empty)
            return ApiResponse.Fail("Operation actor is required.", ["INVALID_OPERATION_ACTOR"]);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.Id, request.Title, request.Description, request.Slug,
                request.IsActive, request.CoverImageUrl, request.StartsAt, request.ExpiresAt,
                request.FieldsJson, request.PerformedByUserId }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
            return prior.Scope == "form.update" && prior.ActorUserId == request.PerformedByUserId
                && prior.RequestHash == requestHash && prior.ResultEntityId == request.Id
                ? ApiResponse.Ok()
                : ApiResponse.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);
        if (request.IsActive)
            return ApiResponse.Fail("Form activation requires a high-risk action.",
                ["RISK_REQUIRES_STRONG_CONFIRMATION"]);

        var updated = await UpdateOnceAsync(request, operationId, requestHash, ct);
        await transaction.CommitAsync(ct);
        return updated;
    }

    private async Task<ApiResponse> UpdateOnceAsync(UpdateFormCommand request,
        string? operationId, string? requestHash, CancellationToken ct)
    {
        var form = await _db.CustomForms.FindAsync(new object[] { request.Id }, ct);
        if (form == null) return ApiResponse.Fail("النموذج غير موجود.");

        if (operationId is not null && (form.IsActive
            || await _db.FormSubmissions.AnyAsync(item => item.CustomFormId == request.Id, ct)))
            return ApiResponse.Fail("Active forms or forms with responses require a high-risk action.",
                ["RISK_REQUIRES_STRONG_CONFIRMATION"]);

        var normalizedSlug = request.Slug.ToLowerInvariant();
        var slugExists = await _db.CustomForms.AnyAsync(f => f.Slug == normalizedSlug && f.Id != request.Id, ct);
        if (slugExists) return ApiResponse.Fail("الرابط المختصر (slug) مستخدم بالفعل في نموذج آخر.");

        // Basic JSON validation
        try
        {
            using var doc = JsonDocument.Parse(request.FieldsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ApiResponse.Fail("حقول النموذج (FieldsJson) يجب أن تكون مصفوفة JSON.");
            }
        }
        catch (JsonException)
        {
            return ApiResponse.Fail("تنسيق حقول النموذج (FieldsJson) غير صالح.");
        }

        form.Title = request.Title;
        form.Description = request.Description;
        form.Slug = normalizedSlug;
        form.IsActive = request.IsActive;
        form.CoverImageUrl = request.CoverImageUrl;
        form.StartsAt = request.StartsAt;
        form.ExpiresAt = request.ExpiresAt;
        form.FieldsJson = request.FieldsJson;

        if (operationId is not null)
            _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
            {
                OperationId = operationId, Scope = "form.update",
                ActorUserId = request.PerformedByUserId, RequestHash = requestHash!, ResultEntityId = request.Id,
                SafeResultJson = "{\"updated\":true}"
            });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

// --- Delete Form ---
public record DeleteFormCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteFormCommandHandler : IRequestHandler<DeleteFormCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    public DeleteFormCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(DeleteFormCommand request, CancellationToken ct)
    {
        var form = await _db.CustomForms.FindAsync(new object[] { request.Id }, ct);
        if (form == null) return ApiResponse.Fail("النموذج غير موجود.");

        _db.CustomForms.Remove(form);
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

// --- Update Submission Status ---
public record UpdateSubmissionStatusCommand(
    Guid SubmissionId,
    FormSubmissionStatus Status,
    string? AdminNotes
) : IRequest<ApiResponse>;

public class UpdateSubmissionStatusCommandHandler : IRequestHandler<UpdateSubmissionStatusCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    public UpdateSubmissionStatusCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateSubmissionStatusCommand request, CancellationToken ct)
    {
        var submission = await _db.FormSubmissions.FindAsync(new object[] { request.SubmissionId }, ct);
        if (submission == null) return ApiResponse.Fail("الطلب غير موجود.");

        submission.Status = request.Status;
        submission.AdminNotes = request.AdminNotes;

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
