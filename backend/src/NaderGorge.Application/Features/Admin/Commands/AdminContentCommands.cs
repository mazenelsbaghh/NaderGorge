using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreatePackageCommand(string Name, string Description, decimal Price, Guid SubjectId, string TargetGrade, Guid? TeacherId = null, Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public class CreatePackageCommandHandler : IRequestHandler<CreatePackageCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreatePackageCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreatePackageCommand request, CancellationToken ct)
    {
        if (request.SubjectId == Guid.Empty)
        {
            return ApiResponse<Guid>.Fail("Subject is required.");
        }

        var subjectExists = await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId, ct);
        if (!subjectExists)
        {
            return ApiResponse<Guid>.Fail("Subject not found.");
        }

        var teacherId = Guid.Empty;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, ct);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                if (user.TeacherProfile == null)
                    return ApiResponse<Guid>.Fail("Teacher profile not onboarded.");
                teacherId = user.TeacherProfile.Id;

                // Verify the teacher can access the subject
                var teachesThisSubject = await _db.TeacherSubjects.AnyAsync(ts => ts.TeacherId == teacherId && ts.SubjectId == request.SubjectId, ct);
                if (!teachesThisSubject)
                    return ApiResponse<Guid>.Fail("Unauthorized access to this subject.");
            }
        }

        if (teacherId == Guid.Empty)
        {
            if (!request.TeacherId.HasValue || request.TeacherId.Value == Guid.Empty)
            {
                return ApiResponse<Guid>.Fail("Teacher is required.");
            }

            var teacherExists = await _db.TeacherProfiles.AnyAsync(tp => tp.Id == request.TeacherId.Value, ct);
            if (!teacherExists)
                return ApiResponse<Guid>.Fail("Selected teacher not found.");

            teacherId = request.TeacherId.Value;
        }

        // Verify that the resolved teacher actually teaches the subject
        var teachesSubject = await _db.TeacherSubjects.AnyAsync(ts => ts.TeacherId == teacherId && ts.SubjectId == request.SubjectId, ct);
        if (!teachesSubject)
        {
            return ApiResponse<Guid>.Fail("Selected teacher does not teach this subject.");
        }

        var pkg = new Package
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            SubjectId = request.SubjectId,
            TargetGrade = string.IsNullOrWhiteSpace(request.TargetGrade) ? "All" : request.TargetGrade,
            TeacherId = teacherId
        };
        _db.Packages.Add(pkg);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(pkg.Id);
    }
}

// --- Terms ---
public record CreateTermCommand(string Title, int Order, Guid PackageId, decimal Price, Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public class CreateTermCommandHandler : IRequestHandler<CreateTermCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreateTermCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateTermCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessPackageAsync(request.CurrentUserId.Value, request.PackageId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this package.");
        }

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

public record UpdateTermCommand(Guid Id, string Title, int Order, decimal Price, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class UpdateTermCommandHandler : IRequestHandler<UpdateTermCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public UpdateTermCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UpdateTermCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessTermAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this term.");
        }

        var term = await _db.Terms.FindAsync(new object[] { request.Id }, ct);
        if (term == null) return ApiResponse.Fail("Term not found");

        term.Title = request.Title;
        term.Order = request.Order;
        term.Price = request.Price;
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record DeleteTermCommand(Guid Id, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class DeleteTermCommandHandler : IRequestHandler<DeleteTermCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public DeleteTermCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(DeleteTermCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessTermAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this term.");
        }

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
public record CreateSectionCommand(string Title, int Order, Guid TermId, decimal Price, Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public class CreateSectionCommandHandler : IRequestHandler<CreateSectionCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreateSectionCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateSectionCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessTermAsync(request.CurrentUserId.Value, request.TermId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this term.");
        }

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

public record CreateLessonCommand(string Title, string Summary, int Order, Guid SectionId, Guid? ExamId, decimal Price, Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public class CreateLessonCommandHandler : IRequestHandler<CreateLessonCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreateLessonCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateLessonCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessSectionAsync(request.CurrentUserId.Value, request.SectionId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this section.");
        }

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

public record CreateVideoCommand(string Title, string Provider, string UrlOrEmbedCode, int Order, int Limit, Guid LessonId, Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public class CreateVideoCommandHandler : IRequestHandler<CreateVideoCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly IEnumerable<IVideoProvider> _providers;
    private readonly TeacherAuthorizationService _auth;

    public CreateVideoCommandHandler(IAppDbContext db, IEnumerable<IVideoProvider> providers, TeacherAuthorizationService auth)
    {
        _db = db;
        _providers = providers;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateVideoCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.LessonId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this lesson.");
        }

        if (!string.Equals(request.Provider, "youtube", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Provider, "vk", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<Guid>.Fail("Invalid provider. Supported: youtube, vk");
        }

        var providerImpl = _providers.FirstOrDefault(p => p.Name.Equals(request.Provider, StringComparison.OrdinalIgnoreCase));
        string extractedId = request.UrlOrEmbedCode;
        if (providerImpl != null)
        {
            extractedId = providerImpl.ExtractVideoId(request.UrlOrEmbedCode);
        }

        var video = new LessonVideo
        {
            Title = request.Title,
            Provider = request.Provider,
            ProviderVideoId = extractedId,
            Order = request.Order,
            MaxWatchCount = request.Limit,
            LessonId = request.LessonId
        };
        _db.LessonVideos.Add(video);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(video.Id);
    }
}

public record UpdateVideoCommand(Guid Id, string Title, string Provider, string UrlOrEmbedCode, int Order, int Limit, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class UpdateVideoCommandHandler : IRequestHandler<UpdateVideoCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly IEnumerable<IVideoProvider> _providers;
    private readonly TeacherAuthorizationService _auth;

    public UpdateVideoCommandHandler(IAppDbContext db, IEnumerable<IVideoProvider> providers, TeacherAuthorizationService auth)
    {
        _db = db;
        _providers = providers;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UpdateVideoCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.Id, ct);
        if (video == null) return ApiResponse.Fail("Video not found");

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, video.LessonId, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this video.");
        }

        if (!string.Equals(request.Provider, "youtube", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Provider, "vk", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse.Fail("Invalid provider. Supported: youtube, vk");
        }

        var providerImpl = _providers.FirstOrDefault(p => p.Name.Equals(request.Provider, StringComparison.OrdinalIgnoreCase));
        var extractedId = request.UrlOrEmbedCode;
        if (providerImpl != null)
        {
            extractedId = providerImpl.ExtractVideoId(request.UrlOrEmbedCode);
        }

        video.Title = request.Title;
        video.Provider = request.Provider;
        video.ProviderVideoId = extractedId;
        video.Order = request.Order;
        video.MaxWatchCount = request.Limit;

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record DeleteVideoCommand(Guid Id, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class DeleteVideoCommandHandler : IRequestHandler<DeleteVideoCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public DeleteVideoCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(DeleteVideoCommand request, CancellationToken ct)
    {
        var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.Id, ct);
        if (video == null) return ApiResponse.Fail("Video not found");

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, video.LessonId, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this video.");
        }

        _db.LessonVideos.Remove(video);
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record AttachHomeworkCommand(
    Guid LessonId,
    string Title,
    string Instructions,
    bool IsMandatory,
    bool IsRandomized,
    int RequiredPointsToPass,
    decimal TotalScore,
    List<AttachHomeworkQuestionDto> Questions,
    Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public record AttachHomeworkQuestionDto(string Text, int Order, int MaxPoints);

public class AttachHomeworkCommandHandler : IRequestHandler<AttachHomeworkCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public AttachHomeworkCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(AttachHomeworkCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.LessonId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this lesson.");
        }

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
                IsRandomized = request.IsRandomized,
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
            hw.IsRandomized = request.IsRandomized;
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

public record CreateLessonResourceCommand(Guid LessonId, string Title, string FileUrl, string ResourceType, Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

public class CreateLessonResourceCommandHandler : IRequestHandler<CreateLessonResourceCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public CreateLessonResourceCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateLessonResourceCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.LessonId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this lesson.");
        }

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

public record LinkLessonExamCommand(Guid LessonId, Guid? ExamId, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class LinkLessonExamCommandHandler : IRequestHandler<LinkLessonExamCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public LinkLessonExamCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(LinkLessonExamCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccessLesson = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.LessonId, ct);
            if (!canAccessLesson) return ApiResponse.Fail("Unauthorized access to this lesson.");

            if (request.ExamId.HasValue)
            {
                var canAccessExam = await _auth.CanAccessExamAsync(request.CurrentUserId.Value, request.ExamId.Value, ct);
                if (!canAccessExam) return ApiResponse.Fail("Unauthorized access to this exam.");
            }
        }

        var lesson = await _db.Lessons.FindAsync(new object[] { request.LessonId }, ct);
        if (lesson == null) return ApiResponse.Fail("Lesson not found");

        lesson.ExamId = request.ExamId;
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
