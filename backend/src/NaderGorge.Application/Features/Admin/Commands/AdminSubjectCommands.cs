using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateSubjectCommand(string Name, string Description) : IRequest<ApiResponse<Guid>>
{
    public string? OperationId { get; init; }
    public Guid? ActorUserId { get; init; }
}

public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateSubjectCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateSubjectCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<Guid>.Fail("Subject name cannot be empty");

        var subject = new Subject
        {
            Name = request.Name.Trim(),
            NormalizedName = request.Name.Trim().ToUpperInvariant(),
            Description = request.Description?.Trim() ?? string.Empty
        };
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<Guid>.Fail("Invalid operation identifier", ["INVALID_OPERATION_ID"]);
        if (operationId is not null)
        {
            if (request.ActorUserId is null || request.ActorUserId == Guid.Empty)
                return ApiResponse<Guid>.Fail("Operation actor is required", ["ACTOR_REQUIRED"]);
            var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new { subject.Name, subject.Description, request.ActorUserId }))));
            return await CreateWithReceiptAsync(subject, operationId, request.ActorUserId.Value, requestHash, ct);
        }

        if (await _db.Subjects.AnyAsync(s => s.NormalizedName == subject.NormalizedName, ct))
            return ApiResponse<Guid>.Fail("A subject with this name already exists");
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(subject.Id);
    }

    private async Task<ApiResponse<Guid>> CreateWithReceiptAsync(
        Subject subject, string operationId, Guid actorId, string requestHash, CancellationToken ct)
    {
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
            return prior.Scope == "subject.create" && prior.ActorUserId == actorId
                && prior.RequestHash == requestHash
                    ? ApiResponse<Guid>.Ok(prior.ResultEntityId)
                    : ApiResponse<Guid>.Fail("Operation identifier already used for another action", ["IDEMPOTENCY_CONFLICT"]);

        if (await _db.Subjects.AnyAsync(item => item.NormalizedName == subject.NormalizedName, ct))
            return ApiResponse<Guid>.Fail("A subject with this name already exists");
        _db.Subjects.Add(subject);
        _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
        {
            OperationId = operationId,
            Scope = "subject.create",
            ActorUserId = actorId,
            RequestHash = requestHash,
            ResultEntityId = subject.Id
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ApiResponse<Guid>.Ok(subject.Id);
    }
}

public record UpdateSubjectCommand(Guid Id, string Name, string Description) : IRequest<ApiResponse>;

public class UpdateSubjectCommandHandler : IRequestHandler<UpdateSubjectCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateSubjectCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateSubjectCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse.Fail("Subject name cannot be empty");

        var subject = await _db.Subjects.FindAsync(new object[] { request.Id }, ct);
        if (subject == null)
            return ApiResponse.Fail("Subject not found");

        var normalized = request.Name.Trim().ToUpperInvariant();
        var exists = await _db.Subjects.AnyAsync(s => s.NormalizedName == normalized && s.Id != request.Id, ct);
        if (exists)
            return ApiResponse.Fail("Another subject with this name already exists");

        subject.Name = request.Name.Trim();
        subject.NormalizedName = normalized;
        subject.Description = request.Description?.Trim() ?? string.Empty;

        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok();
    }
}

public record DeleteSubjectCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteSubjectCommandHandler : IRequestHandler<DeleteSubjectCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public DeleteSubjectCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(DeleteSubjectCommand request, CancellationToken ct)
    {
        var subject = await _db.Subjects.FindAsync(new object[] { request.Id }, ct);
        if (subject == null)
            return ApiResponse.Fail("Subject not found");

        // Verify if linked to packages
        var linkedToPackage = await _db.Packages.AnyAsync(p => p.SubjectId == request.Id, ct);
        if (linkedToPackage)
            return ApiResponse.Fail("Cannot delete subject because it is linked to one or more packages.");

        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok();
    }
}
