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

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateFormCommand(
    string Title,
    string Description,
    string Slug,
    bool IsActive,
    string FieldsJson
) : IRequest<ApiResponse<Guid>>;

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
        var slugExists = await _db.CustomForms.AnyAsync(f => f.Slug == request.Slug, ct);
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
            Slug = request.Slug.ToLowerInvariant(),
            IsActive = request.IsActive,
            FieldsJson = request.FieldsJson
        };

        _db.CustomForms.Add(form);
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
    string FieldsJson
) : IRequest<ApiResponse>;

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
        var form = await _db.CustomForms.FindAsync(new object[] { request.Id }, ct);
        if (form == null) return ApiResponse.Fail("النموذج غير موجود.");

        var slugExists = await _db.CustomForms.AnyAsync(f => f.Slug == request.Slug && f.Id != request.Id, ct);
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
        form.Slug = request.Slug.ToLowerInvariant();
        form.IsActive = request.IsActive;
        form.FieldsJson = request.FieldsJson;

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
