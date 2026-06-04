using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

// --- Add Note ---
public record AddStudentNoteCommand(Guid StudentId, string Content, bool IsPinned, Guid AdminId) : IRequest<ApiResponse>;

public class AddStudentNoteCommandHandler : IRequestHandler<AddStudentNoteCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    public AddStudentNoteCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(AddStudentNoteCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return ApiResponse.Fail("Note content cannot be empty.");

        _db.StudentNotes.Add(new StudentNote
        {
            StudentId = request.StudentId,
            AdminId = request.AdminId,
            Content = request.Content,
            IsPinned = request.IsPinned
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("Note added successfully.");
    }
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
