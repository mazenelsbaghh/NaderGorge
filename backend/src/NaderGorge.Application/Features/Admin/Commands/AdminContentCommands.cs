using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreatePackageCommand(string Name, string Description, decimal Price, Guid? ProgramId) : IRequest<ApiResponse<Guid>>;

public class CreatePackageCommandHandler : IRequestHandler<CreatePackageCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreatePackageCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreatePackageCommand request, CancellationToken ct)
    {
        var pId = request.ProgramId ?? Guid.Empty;
        if (pId == Guid.Empty)
        {
            var prog = await _db.Programs.FirstOrDefaultAsync(ct);
            if (prog == null)
            {
                prog = new Program { Name = "General Program", Description = "Default Program", TargetGrade = "All grades" };
                _db.Programs.Add(prog);
                await _db.SaveChangesAsync(ct);
            }
            pId = prog.Id;
        }

        var pkg = new Package
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            ProgramId = pId
        };
        _db.Packages.Add(pkg);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(pkg.Id);
    }
}

// --- Terms ---
public record CreateTermCommand(string Title, int Order, Guid PackageId, decimal Price) : IRequest<ApiResponse<Guid>>;

public class CreateTermCommandHandler : IRequestHandler<CreateTermCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    public CreateTermCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateTermCommand request, CancellationToken ct)
    {
        var term = new Term
        {
            Title = request.Title,
            Order = request.Order,
            PackageId = request.PackageId,
            Price = request.Price
        };
        _db.Terms.Add(term);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(term.Id);
    }
}

public record UpdateTermCommand(Guid Id, string Title, int Order, decimal Price) : IRequest<ApiResponse>;

public class UpdateTermCommandHandler : IRequestHandler<UpdateTermCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    public UpdateTermCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateTermCommand request, CancellationToken ct)
    {
        var term = await _db.Terms.FindAsync(new object[] { request.Id }, ct);
        if (term == null) return ApiResponse.Fail("Term not found");

        term.Title = request.Title;
        term.Order = request.Order;
        term.Price = request.Price;
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record DeleteTermCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteTermCommandHandler : IRequestHandler<DeleteTermCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    public DeleteTermCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(DeleteTermCommand request, CancellationToken ct)
    {
        var term = await _db.Terms.Include(t => t.Sections).FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (term == null) return ApiResponse.Fail("Term not found");

        if (term.Sections.Any())
            return ApiResponse.Fail("Cannot delete term because it has sections. Remove sections first.");

        _db.Terms.Remove(term);
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

// --- Sections ---
public record CreateSectionCommand(string Title, int Order, Guid TermId, decimal Price) : IRequest<ApiResponse<Guid>>;

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
            TermId = request.TermId,
            Price = request.Price
        };
        _db.ContentSections.Add(sec);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(sec.Id);
    }
}

public record CreateLessonCommand(string Title, string Summary, int Order, Guid SectionId, Guid? ExamId, decimal Price) : IRequest<ApiResponse<Guid>>;

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
            ExamId = request.ExamId,
            Price = request.Price
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

public record AttachHomeworkCommand(
    Guid LessonId,
    string Title,
    string Instructions,
    bool IsMandatory,
    int RequiredPointsToPass,
    decimal TotalScore,
    List<AttachHomeworkQuestionDto> Questions) : IRequest<ApiResponse<Guid>>;

public record AttachHomeworkQuestionDto(string Text, int Order, int MaxPoints);

public class AttachHomeworkCommandHandler : IRequestHandler<AttachHomeworkCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public AttachHomeworkCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(AttachHomeworkCommand request, CancellationToken ct)
    {
        var lesson = await _db.Lessons
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);
            
        if (lesson == null) return ApiResponse<Guid>.Fail("Lesson not found");

        var hw = await _db.Homeworks
            .Include(h => h.Questions)
            .FirstOrDefaultAsync(h => h.LessonId == request.LessonId, ct);
            
        if (hw == null)
        {
            hw = new NaderGorge.Domain.Entities.Homework.Homework
            {
                LessonId = lesson.Id,
                Title = request.Title,
                Description = request.Instructions,
                IsMandatory = request.IsMandatory,
                PassingScoreThreshold = request.RequiredPointsToPass,
                TotalScore = request.TotalScore
            };
            _db.Homeworks.Add(hw);
        }
        else
        {
            hw.Title = request.Title;
            hw.Description = request.Instructions;
            hw.IsMandatory = request.IsMandatory;
            hw.PassingScoreThreshold = request.RequiredPointsToPass;
            hw.TotalScore = request.TotalScore;
            _db.HomeworkQuestions.RemoveRange(hw.Questions);
            hw.Questions.Clear();
        }

        foreach (var q in request.Questions)
        {
            hw.Questions.Add(new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
            {
                HomeworkId = hw.Id,
                BodyText = q.Text,
                Order = q.Order,
                PointsActive = q.MaxPoints
            });
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(hw.Id);
    }
}

public record CreateLessonResourceCommand(Guid LessonId, string Title, string FileUrl, string ResourceType) : IRequest<ApiResponse<Guid>>;

public class CreateLessonResourceCommandHandler : IRequestHandler<CreateLessonResourceCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateLessonResourceCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateLessonResourceCommand request, CancellationToken ct)
    {
        var resource = new LessonResource
        {
            LessonId = request.LessonId,
            Title = request.Title,
            FileUrl = request.FileUrl,
            ResourceType = request.ResourceType
        };

        _db.LessonResources.Add(resource);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(resource.Id);
    }
}

public record LinkLessonExamCommand(Guid LessonId, Guid? ExamId) : IRequest<ApiResponse>;

public class LinkLessonExamCommandHandler : IRequestHandler<LinkLessonExamCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public LinkLessonExamCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(LinkLessonExamCommand request, CancellationToken ct)
    {
        var lesson = await _db.Lessons.FindAsync(new object[] { request.LessonId }, ct);
        if (lesson == null) return ApiResponse.Fail("Lesson not found");

        lesson.ExamId = request.ExamId;
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
