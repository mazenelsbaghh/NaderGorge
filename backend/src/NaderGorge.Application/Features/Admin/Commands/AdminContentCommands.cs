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

        // Validate the TargetGrade is within the teacher's specialization (grades)
        var teacherProfile = await _db.TeacherProfiles.FirstOrDefaultAsync(tp => tp.Id == teacherId, ct);
        if (teacherProfile == null)
        {
            return ApiResponse<Guid>.Fail("Selected teacher profile not found.");
        }

        if (string.IsNullOrWhiteSpace(request.TargetGrade))
        {
            return ApiResponse<Guid>.Fail("Target grade is required.");
        }

        string normalizedRequestedGrade = request.TargetGrade.Trim();
        if (normalizedRequestedGrade == "1st Secondary") normalizedRequestedGrade = "FirstSecondary";
        else if (normalizedRequestedGrade == "2nd Secondary") normalizedRequestedGrade = "SecondSecondary";
        else if (normalizedRequestedGrade == "3rd Secondary") normalizedRequestedGrade = "SecondaryGrade3";

        var allowedGrades = teacherProfile.Specialization
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(g => {
                if (g == "1st Secondary") return "FirstSecondary";
                if (g == "2nd Secondary") return "SecondSecondary";
                if (g == "3rd Secondary") return "SecondaryGrade3";
                return g;
            })
            .ToList();

        bool isGradeAllowed = allowedGrades.Any(g => string.Equals(g, normalizedRequestedGrade, StringComparison.OrdinalIgnoreCase));
        if (!isGradeAllowed)
        {
            return ApiResponse<Guid>.Fail("The selected grade is not allowed for this teacher.");
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

        var outboxEvent = new OutboxEvent
        {
            Type = "PackageCreated",
            TargetGroup = "Role_Student",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                packageId = pkg.Id,
                name = pkg.Name,
                price = pkg.Price
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(pkg.Id);
    }
}

// --- Terms ---

// --- Toggle Package Visibility ---
public record TogglePackageActiveCommand(Guid PackageId, Guid CurrentUserId) : IRequest<ApiResponse<bool>>;

public class TogglePackageActiveCommandHandler : IRequestHandler<TogglePackageActiveCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public TogglePackageActiveCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<bool>> Handle(TogglePackageActiveCommand request, CancellationToken ct)
    {
        var canAccess = await _auth.CanAccessPackageAsync(request.CurrentUserId, request.PackageId, ct);
        if (!canAccess) return ApiResponse<bool>.Fail("Unauthorized access to this package.");

        var pkg = await _db.Packages.FindAsync(new object[] { request.PackageId }, ct);
        if (pkg == null) return ApiResponse<bool>.Fail("Package not found.");

        pkg.IsActive = !pkg.IsActive;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(pkg.IsActive);
    }
}
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

        var outboxEvent = new OutboxEvent
        {
            Type = "TermCreated",
            TargetGroup = $"Package_{request.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                termId = term.Id,
                packageId = request.PackageId,
                title = term.Title
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        var termPublishedEvent = new OutboxEvent
        {
            Type = "TermPublished",
            TargetGroup = $"Package_{request.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                termId = term.Id,
                packageId = request.PackageId,
                title = term.Title
            })
        };
        _db.OutboxEvents.Add(termPublishedEvent);

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

        var outboxEvent = new OutboxEvent
        {
            Type = "TermUpdated",
            TargetGroup = $"Package_{term.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                termId = term.Id,
                packageId = term.PackageId,
                title = term.Title
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

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

        var outboxEvent = new OutboxEvent
        {
            Type = "TermDeleted",
            TargetGroup = $"Package_{term.PackageId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                termId = term.Id,
                packageId = term.PackageId,
                title = term.Title
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

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

        var term = await _db.Terms.FindAsync(new object[] { request.TermId }, ct);
        if (term != null)
        {
            var sectionOutbox = new OutboxEvent
            {
                Type = "SectionCreated",
                TargetGroup = $"Package_{term.PackageId}",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sectionId = sec.Id,
                    termId = request.TermId,
                    packageId = term.PackageId,
                    title = sec.Title
                })
            };
            _db.OutboxEvents.Add(sectionOutbox);

            var sectionPublishedOutbox = new OutboxEvent
            {
                Type = "SectionPublished",
                TargetGroup = $"Package_{term.PackageId}",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sectionId = sec.Id,
                    termId = request.TermId,
                    packageId = term.PackageId,
                    title = sec.Title
                })
            };
            _db.OutboxEvents.Add(sectionPublishedOutbox);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(sec.Id);
    }
}

// --- Update Section ---
public record UpdateSectionCommand(Guid Id, string Title, int Order, decimal Price, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class UpdateSectionCommandHandler : IRequestHandler<UpdateSectionCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public UpdateSectionCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UpdateSectionCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessSectionAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this section.");
        }

        var section = await _db.ContentSections.FindAsync(new object[] { request.Id }, ct);
        if (section == null) return ApiResponse.Fail("Section not found");

        section.Title = request.Title;
        section.Order = request.Order;
        section.Price = request.Price;

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

// --- Update Lesson ---
public record UpdateLessonCommand(Guid Id, string Title, string Summary, int Order, decimal Price, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class UpdateLessonCommandHandler : IRequestHandler<UpdateLessonCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public UpdateLessonCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UpdateLessonCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this lesson.");
        }

        var lesson = await _db.Lessons.FindAsync(new object[] { request.Id }, ct);
        if (lesson == null) return ApiResponse.Fail("Lesson not found");

        lesson.Title = request.Title;
        lesson.Summary = request.Summary;
        lesson.Order = request.Order;
        lesson.Price = request.Price;

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
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

        var section = await _db.ContentSections
            .Include(s => s.Term)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, ct);

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

        if (section?.Term != null)
        {
            var outboxEvent = new OutboxEvent
            {
                Type = "LessonPublished",
                TargetGroup = $"Package_{section.Term.PackageId}",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    lessonId = lesson.Id,
                    sectionId = lesson.ContentSectionId,
                    title = lesson.Title,
                    packageId = section.Term.PackageId,
                    order = lesson.Order
                })
            };
            _db.OutboxEvents.Add(outboxEvent);
        }

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

        var outboxEvent = new OutboxEvent
        {
            Type = "VideoProcessingStarted",
            TargetGroup = $"Lesson_{request.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = request.LessonId,
                videoId = video.Id,
                status = "Started"
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

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

        var outboxEvent = new OutboxEvent
        {
            Type = "VideoUpdated",
            TargetGroup = $"Lesson_{video.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = video.LessonId,
                videoId = video.Id,
                title = video.Title
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

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

        var outboxEvent = new OutboxEvent
        {
            Type = "VideoDeleted",
            TargetGroup = $"Lesson_{video.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = video.LessonId,
                videoId = video.Id
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

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

public record AttachHomeworkOptionDto(string Text, bool IsCorrect);

public record AttachHomeworkQuestionDto(
    string Text, 
    int Order, 
    decimal Points,
    string Type,
    List<AttachHomeworkOptionDto>? Options = null,
    string? AudioUrl = null,
    string? WrittenCorrection = null,
    string? HintText = null,
    string? BaseText = null,
    int? MistakeStartIndex = null,
    int? MistakeEndIndex = null
);

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
            .Include(l => l.ContentSection).ThenInclude(s => s.Term)
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
            var qType = q.Type switch
            {
                "Essay" => NaderGorge.Domain.Entities.Homework.QuestionType.Essay,
                "FindTheMistake" => NaderGorge.Domain.Entities.Homework.QuestionType.FindTheMistake,
                _ => NaderGorge.Domain.Entities.Homework.QuestionType.MCQ
            };

            string[]? possibleAnswers = null;
            string? correctAnswerKey = null;

            if ((qType == NaderGorge.Domain.Entities.Homework.QuestionType.MCQ || qType == NaderGorge.Domain.Entities.Homework.QuestionType.FindTheMistake) && q.Options != null)
            {
                possibleAnswers = q.Options.Select(o => o.Text).ToArray();
                correctAnswerKey = q.Options.FirstOrDefault(o => o.IsCorrect)?.Text;
            }

            hw.Questions.Add(new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
            {
                HomeworkId = hw.Id,
                BodyText = q.Text,
                Order = q.Order,
                PointsActive = (int)q.Points,
                QuestionType = qType,
                PossibleAnswers = possibleAnswers,
                CorrectAnswerKey = correctAnswerKey,
                AudioUrl = q.AudioUrl,
                WrittenCorrection = q.WrittenCorrection,
                HintText = q.HintText,
                BaseText = q.BaseText,
                MistakeStartIndex = q.MistakeStartIndex,
                MistakeEndIndex = q.MistakeEndIndex
            });
        }

        if (lesson.ContentSection?.Term != null)
        {
            var outboxEvent = new OutboxEvent
            {
                Type = "HomeworkPublished",
                TargetGroup = $"Package_{lesson.ContentSection.Term.PackageId}",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    lessonId = lesson.Id,
                    homeworkId = hw.Id,
                    title = hw.Title,
                    packageId = lesson.ContentSection.Term.PackageId
                })
            };
            _db.OutboxEvents.Add(outboxEvent);
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

        var resourceProcessingStartedEvent = new OutboxEvent
        {
            Type = "ResourceProcessingStarted",
            TargetGroup = $"Lesson_{request.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = request.LessonId,
                title = request.Title
            })
        };
        _db.OutboxEvents.Add(resourceProcessingStartedEvent);

        _db.LessonResources.Add(resource);

        var outboxEvent = new OutboxEvent
        {
            Type = "ResourceReady",
            TargetGroup = $"Lesson_{request.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = request.LessonId,
                resourceId = resource.Id,
                title = resource.Title,
                fileUrl = resource.FileUrl
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

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

        var lesson = await _db.Lessons
            .Include(l => l.ContentSection).ThenInclude(s => s.Term)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);
        if (lesson == null) return ApiResponse.Fail("Lesson not found");

        lesson.ExamId = request.ExamId;

        if (request.ExamId.HasValue && lesson.ContentSection?.Term != null)
        {
            var outboxEvent = new OutboxEvent
            {
                Type = "ExamPublished",
                TargetGroup = $"Package_{lesson.ContentSection.Term.PackageId}",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    lessonId = lesson.Id,
                    examId = request.ExamId.Value,
                    packageId = lesson.ContentSection.Term.PackageId
                })
            };
            _db.OutboxEvents.Add(outboxEvent);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record LinkVideoExamCommand(Guid VideoId, Guid? ExamId, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class LinkVideoExamCommandHandler : IRequestHandler<LinkVideoExamCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public LinkVideoExamCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(LinkVideoExamCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
            if (video == null) return ApiResponse.Fail("Video not found");

            var canAccessLesson = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, video.LessonId, ct);
            if (!canAccessLesson) return ApiResponse.Fail("Unauthorized access to this video.");

            if (request.ExamId.HasValue)
            {
                var canAccessExam = await _auth.CanAccessExamAsync(request.CurrentUserId.Value, request.ExamId.Value, ct);
                if (!canAccessExam) return ApiResponse.Fail("Unauthorized access to this exam.");
            }
        }

        var videoEntity = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
        if (videoEntity == null) return ApiResponse.Fail("Video not found");

        videoEntity.ExamId = request.ExamId;

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

public record UnlinkVideoExamCommand(Guid VideoId, Guid ExamId, Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public class UnlinkVideoExamCommandHandler : IRequestHandler<UnlinkVideoExamCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public UnlinkVideoExamCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse> Handle(UnlinkVideoExamCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
            if (video == null) return ApiResponse.Fail("Video not found");

            var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, video.LessonId, ct);
            if (!canAccess) return ApiResponse.Fail("Unauthorized access to this video.");
        }

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);
        if (exam != null)
        {
            exam.LessonVideoId = null;
        }

        var videoEntity = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == request.VideoId, ct);
        if (videoEntity != null && videoEntity.ExamId == request.ExamId)
        {
            videoEntity.ExamId = null;
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
