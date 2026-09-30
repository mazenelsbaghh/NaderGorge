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

// --- Add Note ---
public record AddStudentNoteCommand(Guid StudentId, string Content, bool IsPinned, Guid AdminId) : IRequest<ApiResponse>
{
    public string? OperationId { get; init; }
}

public class AddStudentNoteCommandHandler : IRequestHandler<AddStudentNoteCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    public AddStudentNoteCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(AddStudentNoteCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return ApiResponse.Fail("Note content cannot be empty.");

        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);

        if (operationId is not null)
        {
            var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new { request.StudentId, request.AdminId, request.Content, request.IsPinned }))));
            return await AddWithReceiptAsync(request, operationId, requestHash, ct);
        }

        _db.StudentNotes.Add(CreateNote(request));

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("Note added successfully.");
    }

    private async Task<ApiResponse> AddWithReceiptAsync(
        AddStudentNoteCommand request, string operationId, string requestHash, CancellationToken ct)
    {
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await FindReceiptAsync(operationId, ct);
        if (prior is not null)
            return Replay(prior, request, requestHash);

        var note = CreateNote(request);
        _db.StudentNotes.Add(note);
        _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
        {
            OperationId = operationId,
            Scope = "student-note.create",
            ActorUserId = request.AdminId,
            RequestHash = requestHash,
            ResultEntityId = note.Id
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ApiResponse.Ok("Note added successfully.");
    }

    private Task<AuthoritativeOperationReceipt?> FindReceiptAsync(string operationId, CancellationToken ct) =>
        _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);

    private static StudentNote CreateNote(AddStudentNoteCommand request) => new()
    {
        StudentId = request.StudentId,
        AdminId = request.AdminId,
        Content = request.Content,
        IsPinned = request.IsPinned
    };

    private static ApiResponse Replay(
        AuthoritativeOperationReceipt receipt, AddStudentNoteCommand request, string requestHash) =>
        receipt.Scope == "student-note.create" && receipt.ActorUserId == request.AdminId
        && receipt.RequestHash == requestHash
            ? ApiResponse.Ok("Note added successfully.")
            : ApiResponse.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);

}

// --- Delete Note ---
public record DeleteStudentNoteCommand(Guid NoteId, Guid AdminId) : IRequest<ApiResponse>;

public class DeleteStudentNoteCommandHandler : IRequestHandler<DeleteStudentNoteCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    public DeleteStudentNoteCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(DeleteStudentNoteCommand request, CancellationToken ct)
    {
        var note = await _db.StudentNotes.FirstOrDefaultAsync(n => n.Id == request.NoteId, ct);
        if (note == null) return ApiResponse.Fail("Note not found.");

        _db.StudentNotes.Remove(note);
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("Note deleted.");
    }
}
