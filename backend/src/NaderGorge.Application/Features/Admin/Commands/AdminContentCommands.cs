using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreatePackageCommand(string Name, string Description, decimal Price, Guid ProgramId) : IRequest<ApiResponse<Guid>>;

public class CreatePackageCommandHandler : IRequestHandler<CreatePackageCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreatePackageCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreatePackageCommand request, CancellationToken ct)
    {
        var pkg = new Package
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            ProgramId = request.ProgramId
        };
        _db.Packages.Add(pkg);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(pkg.Id);
    }
}

public record CreateSectionCommand(string Title, int Order, Guid PackageId) : IRequest<ApiResponse<Guid>>;

public class CreateSectionCommandHandler : IRequestHandler<CreateSectionCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateSectionCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateSectionCommand request, CancellationToken ct)
    {
        var sec = new ContentSection
        {
            Title = request.Title,
            Order = request.Order,
            PackageId = request.PackageId
        };
        _db.ContentSections.Add(sec);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(sec.Id);
    }
}

public record CreateLessonCommand(string Title, string Summary, int Order, Guid SectionId, Guid? ExamId) : IRequest<ApiResponse<Guid>>;

public class CreateLessonCommandHandler : IRequestHandler<CreateLessonCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateLessonCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateLessonCommand request, CancellationToken ct)
    {
        var lesson = new Lesson
        {
            Title = request.Title,
            Summary = request.Summary,
            Order = request.Order,
            ContentSectionId = request.SectionId,
            ExamId = request.ExamId
        };
        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(lesson.Id);
    }
}

public record CreateVideoCommand(string Title, string Provider, string UrlOrEmbedCode, int Order, int Limit, Guid LessonId) : IRequest<ApiResponse<Guid>>;

public class CreateVideoCommandHandler : IRequestHandler<CreateVideoCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateVideoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateVideoCommand request, CancellationToken ct)
    {
        var video = new LessonVideo
        {
            Title = request.Title,
            Provider = request.Provider,
            ProviderVideoId = request.UrlOrEmbedCode,
            Order = request.Order,
            MaxWatchCount = request.Limit,
            LessonId = request.LessonId
        };
        _db.LessonVideos.Add(video);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(video.Id);
    }
}
