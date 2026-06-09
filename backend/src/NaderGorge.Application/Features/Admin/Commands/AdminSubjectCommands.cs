using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateSubjectCommand(string Name, string Description) : IRequest<ApiResponse<Guid>>;

public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateSubjectCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateSubjectCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<Guid>.Fail("Subject name cannot be empty");

        var normalized = request.Name.Trim().ToUpperInvariant();
        var exists = await _db.Subjects.AnyAsync(s => s.NormalizedName == normalized, ct);
        if (exists)
            return ApiResponse<Guid>.Fail("A subject with this name already exists");

        var subject = new Subject
        {
            Name = request.Name.Trim(),
            NormalizedName = normalized,
            Description = request.Description?.Trim() ?? string.Empty
        };

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(ct);

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

        // Verify if linked to programs
        var linkedToProgram = await _db.Programs.AnyAsync(p => p.SubjectId == request.Id, ct);
        if (linkedToProgram)
            return ApiResponse.Fail("Cannot delete subject because it is linked to one or more programs.");

        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok();
    }
}
