using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Content;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Teacher;

public record GetTeacherDashboardStatsQuery(Guid TeacherUserId) : IRequest<ApiResponse<TeacherDashboardStatsDto>>;

public record TeacherDashboardStatsDto(
    int ActiveStudentsCount,
    int PackagesCount,
    int ExamsCount,
    int PendingEssaysCount,
    IReadOnlyList<TeacherPackageSalesBreakdownDto> PackageSales
);

public record TeacherPackageSalesBreakdownDto(
    Guid PackageId,
    string PackageName,
    int PackageBuyers,
    int TermBuyers,
    int SectionBuyers,
    int LessonBuyers,
    int PurchasedStudents,
    int GiftStudents
);

public record GetTeacherStudentsQuery(
    Guid TeacherUserId,
    Guid[]? StudentIds = null,
    bool IncludeExportDetails = false) : IRequest<ApiResponse<List<TeacherStudentDto>>>;

public record TeacherStudentDto(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string ActivatedPackageName,
    DateTime ActivatedAt,
    string? StudentCode,
    string? SecondaryPhone,
    string? ParentPhone,
    string? MotherPhone,
    string? Governorate,
    string? District,
    string? Address,
    string EducationStage,
    string GradeLevel,
    string? StudyTrack,
    string? SchoolName,
    string? SchoolType,
    int ActivePackageCount,
    int ActiveGrantCount,
    DateTime? LastActivationAt,
    string? Gender,
    IReadOnlyList<TeacherStudentPackageDto> ActivePackages,
    TeacherStudentExportDetails? ExportDetails
);

public record TeacherStudentPackageDto(Guid Id, string Name);

public record TeacherStudentExportDetails(
    string? SecondaryParentPhone,
    string? Nationality,
    DateTime? DateOfBirth,
    DateTime? FatherDateOfBirth,
    DateTime? MotherDateOfBirth,
    bool? IsFatherAlive,
    bool? IsMotherAlive,
    bool IsActive,
    DateTime CreatedAt);

public record GetPendingTeacherEssaysQuery(Guid TeacherUserId) : IRequest<ApiResponse<List<PendingEssayDto>>>;

public record PendingEssayDto(
    Guid Id,
    string StudentName,
    string QuestionText,
    string ExamTitle,
    DateTime SubmittedAt,
    string Status,
    string AnswerText,
    string? AudioUrl,
    decimal? AiInitialScore,
    string? AiFeedback,
    decimal MaxPoints
);

public record GetTeacherProfileQuery(Guid TeacherUserId) : IRequest<ApiResponse<TeacherProfileDto>>;

public record TeacherProfileDto(
    Guid Id,
    Guid UserId,
    string Bio,
    string Specialization,
    string? ProfileImageUrl,
    string ContactInfo,
    string? AssistantPhoneNumbers,
    string? FacebookUrl,
    string? YouTubeUrl,
    string? TelegramUrl,
    string? IntroVideoUrl
);

public record UpdateTeacherProfileCommand(
    Guid TeacherUserId,
    string Bio,
    string Specialization,
    string ContactInfo,
    string? ProfileImageUrl,
    string? AssistantPhoneNumbers,
    string? FacebookUrl,
    string? YouTubeUrl,
    string? TelegramUrl
) : IRequest<ApiResponse<bool>>;

public class GetTeacherDashboardStatsQueryHandler : IRequestHandler<GetTeacherDashboardStatsQuery, ApiResponse<TeacherDashboardStatsDto>>
{
    private readonly IAppDbContext _db;
    private readonly ContentGrantFactSource _factSource;

    public GetTeacherDashboardStatsQueryHandler(IAppDbContext db)
    {
        _db = db;
        _factSource = new ContentGrantFactSource(db);
    }

    public async Task<ApiResponse<TeacherDashboardStatsDto>> Handle(GetTeacherDashboardStatsQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<TeacherDashboardStatsDto>.Fail("حساب المعلم غير موجود");
        }

        var teacherPackages = await _db.Packages.AsNoTracking()
            .Where(package => package.TeacherId == teacherProfile.Id)
            .OrderBy(package => package.Name)
            .Select(package => new { package.Id, package.Name })
            .ToListAsync(ct);
        var packagesCount = teacherPackages.Count;

        var examsCount = await _db.Exams.CountAsync(e => e.CreatedByTeacherId == teacherProfile.Id, ct);

        var pendingEssaysCount = await _db.EssaySubmissions
            .Include(es => es.Question)
            .CountAsync(es => es.Status == EssaySubmissionStatus.WaitTeacher && es.Question.CreatedByTeacherId == teacherProfile.Id, ct);

        var packageIds = teacherPackages.Select(package => package.Id).ToArray();
        var grantFacts = await _factSource.LoadAsync(new ContentGrantFactScope(packageIds), ct);
        var activeStudentsCount = ContentAcquisitionCalculator.CountActiveStudents(grantFacts, DateTime.UtcNow);
        var acquisitionsByPackage = ContentAcquisitionCalculator.SummarizePackages(packageIds, grantFacts);
        var packageSales = teacherPackages.Select(package =>
        {
            var acquisitions = acquisitionsByPackage[package.Id];
            return new TeacherPackageSalesBreakdownDto(
                package.Id,
                package.Name,
                acquisitions.Package.Total,
                acquisitions.Term.Total,
                acquisitions.Section.Total,
                acquisitions.Lesson.Total,
                acquisitions.Overall.Purchased,
                acquisitions.Overall.GiftOnly);
        }).ToArray();

        var dto = new TeacherDashboardStatsDto(activeStudentsCount, packagesCount, examsCount, pendingEssaysCount, packageSales);
        return ApiResponse<TeacherDashboardStatsDto>.Ok(dto);
    }
}

public class GetTeacherStudentsQueryHandler : IRequestHandler<GetTeacherStudentsQuery, ApiResponse<List<TeacherStudentDto>>>
{
    private readonly IAppDbContext _db;
    private readonly ContentGrantFactSource _factSource;

    public GetTeacherStudentsQueryHandler(IAppDbContext db)
    {
        _db = db;
        _factSource = new ContentGrantFactSource(db);
    }

    public async Task<ApiResponse<List<TeacherStudentDto>>> Handle(GetTeacherStudentsQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<List<TeacherStudentDto>>.Fail("حساب المعلم غير موجود");
        }

        var teacherPackages = await _db.Packages.AsNoTracking()
            .Where(package => package.TeacherId == teacherProfile.Id)
            .Select(package => new { package.Id, package.Name })
            .ToListAsync(ct);
        var packageIds = teacherPackages.Select(package => package.Id).ToArray();
        var packagesById = teacherPackages.ToDictionary(package => package.Id,
            package => new TeacherStudentPackageDto(package.Id, package.Name));
        var grantFacts = await _factSource.LoadAsync(new ContentGrantFactScope(packageIds), ct);
        var requestedStudents = request.StudentIds?.ToHashSet();
        var studentGrantSummaries = ContentAcquisitionCalculator
            .WhereEffectiveAt(grantFacts, DateTime.UtcNow)
            .Where(fact => requestedStudents == null || requestedStudents.Contains(fact.UserId))
            .GroupBy(fact => fact.UserId)
            .Select(SummarizeStudentGrants)
            .ToArray();
        var studentIds = studentGrantSummaries.Select(summary => summary.UserId).ToArray();
        var users = await _db.Users.AsNoTracking()
            .Include(user => user.StudentProfile)
            .Where(user => studentIds.Contains(user.Id))
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .ToListAsync(ct);
        var students = users
            .Join(studentGrantSummaries, user => user.Id, summary => summary.UserId,
                (user, summary) => ToDto(summary, user, packagesById, request.IncludeExportDetails))
            .ToList();

        return ApiResponse<List<TeacherStudentDto>>.Ok(students);
    }

    private static ActiveStudentGrantSummary SummarizeStudentGrants(IGrouping<Guid, ContentGrantFact> facts)
    {
        var factRows = facts.ToArray();
        var latestFact = factRows
            .OrderByDescending(fact => fact.GrantedAt)
            .ThenBy(fact => fact.PackageId)
            .First();
        return new ActiveStudentGrantSummary(
            facts.Key,
            latestFact.PackageId,
            factRows.Min(fact => fact.GrantedAt),
            factRows.Max(fact => fact.GrantedAt),
            factRows.Length,
            factRows.Select(fact => fact.PackageId).Distinct().ToArray());
    }

    private static TeacherStudentDto ToDto(
        ActiveStudentGrantSummary summary,
        User user,
        IReadOnlyDictionary<Guid, TeacherStudentPackageDto> packagesById,
        bool includeExportDetails)
    {
        var profile = user.StudentProfile;
        var packages = summary.PackageIds.Select(id => packagesById[id])
            .OrderBy(package => package.Name).ToArray();
        return new TeacherStudentDto(
            user.Id,
            user.FullName,
            user.PhoneNumber,
            packagesById[summary.LatestPackageId].Name,
            summary.FirstActivationAt,
            profile?.StudentCode,
            profile?.SecondaryPhone,
            profile?.ParentPhone,
            profile?.MotherPhone,
            profile?.Governorate,
            profile?.District,
            profile?.Address,
            profile?.EducationStage.ToString() ?? string.Empty,
            profile?.GradeLevel.ToString() ?? string.Empty,
            profile?.StudyTrack?.ToString(),
            profile?.SchoolName,
            profile?.SchoolType?.ToString(),
            packages.Length,
            summary.ActiveGrantCount,
            summary.LastActivationAt,
            profile?.Gender.ToString(),
            packages,
            includeExportDetails ? new TeacherStudentExportDetails(
                profile?.SecondaryParentPhone, profile?.Nationality, profile?.DateOfBirth,
                profile?.FatherDateOfBirth, profile?.MotherDateOfBirth, profile?.IsFatherAlive,
                profile?.IsMotherAlive, user.IsActive, user.CreatedAt) : null);
    }

    private sealed record ActiveStudentGrantSummary(
        Guid UserId,
        Guid LatestPackageId,
        DateTime FirstActivationAt,
        DateTime LastActivationAt,
        int ActiveGrantCount,
        Guid[] PackageIds);
}

public class GetPendingTeacherEssaysQueryHandler : IRequestHandler<GetPendingTeacherEssaysQuery, ApiResponse<List<PendingEssayDto>>>
{
    private readonly IAppDbContext _db;

    public GetPendingTeacherEssaysQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<PendingEssayDto>>> Handle(GetPendingTeacherEssaysQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<List<PendingEssayDto>>.Fail("حساب المعلم غير موجود");
        }

        var essays = await _db.EssaySubmissions
            .AsNoTracking()
            .Include(e => e.Student)
            .Include(e => e.Question)
            .Include(e => e.Attempt)
                .ThenInclude(a => a.Exam)
            .Where(e => e.Status == EssaySubmissionStatus.WaitTeacher && e.Question.CreatedByTeacherId == teacherProfile.Id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new PendingEssayDto(
                e.Id,
                e.Student.FullName,
                e.Question.Text,
                e.Attempt.Exam.Title,
                e.CreatedAt,
                e.Status.ToString(),
                e.AnswerText,
                e.AudioUrl,
                e.AiInitialScore,
                e.AiFeedback,
                _db.ExamQuestions.Where(eq => eq.ExamId == e.Attempt.ExamId && eq.QuestionBankItemId == e.QuestionId).Select(eq => (decimal?)eq.Points).FirstOrDefault() ?? e.Question.DefaultPoints
            ))
            .ToListAsync(ct);

        return ApiResponse<List<PendingEssayDto>>.Ok(essays);
    }
}

public class GetTeacherProfileQueryHandler : IRequestHandler<GetTeacherProfileQuery, ApiResponse<TeacherProfileDto>>
{
    private readonly IAppDbContext _db;

    public GetTeacherProfileQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<TeacherProfileDto>> Handle(GetTeacherProfileQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<TeacherProfileDto>.Fail("حساب المعلم غير موجود");
        }

        var dto = new TeacherProfileDto(
            teacherProfile.Id,
            teacherProfile.UserId,
            teacherProfile.Bio,
            teacherProfile.Specialization,
            teacherProfile.ProfileImageUrl,
            teacherProfile.ContactInfo,
            teacherProfile.AssistantPhoneNumbers,
            teacherProfile.FacebookUrl,
            teacherProfile.YouTubeUrl,
            teacherProfile.TelegramUrl,
            teacherProfile.IntroVideoUrl
        );

        return ApiResponse<TeacherProfileDto>.Ok(dto);
    }
}

public class UpdateTeacherProfileCommandHandler : IRequestHandler<UpdateTeacherProfileCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public UpdateTeacherProfileCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<bool>> Handle(UpdateTeacherProfileCommand request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<bool>.Fail("حساب المعلم غير موجود");
        }

        teacherProfile.Bio = request.Bio;
        teacherProfile.Specialization = request.Specialization;
        teacherProfile.ContactInfo = request.ContactInfo;
        teacherProfile.ProfileImageUrl = request.ProfileImageUrl;
        teacherProfile.AssistantPhoneNumbers = request.AssistantPhoneNumbers;
        teacherProfile.FacebookUrl = request.FacebookUrl;
        teacherProfile.YouTubeUrl = request.YouTubeUrl;
        teacherProfile.TelegramUrl = request.TelegramUrl;

        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }
}
