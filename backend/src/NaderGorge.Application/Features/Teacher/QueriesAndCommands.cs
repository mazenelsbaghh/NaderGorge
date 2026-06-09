using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Teacher;

public record GetTeacherDashboardStatsQuery(Guid TeacherUserId) : IRequest<ApiResponse<TeacherDashboardStatsDto>>;

public record TeacherDashboardStatsDto(
    int ActiveStudentsCount,
    int PackagesCount,
    int ExamsCount,
    int PendingEssaysCount
);

public record GetTeacherStudentsQuery(Guid TeacherUserId) : IRequest<ApiResponse<List<TeacherStudentDto>>>;

public record TeacherStudentDto(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string ActivatedPackageName,
    DateTime ActivatedAt
);

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
    string ContactInfo
);

public record UpdateTeacherProfileCommand(
    Guid TeacherUserId,
    string Bio,
    string Specialization,
    string ContactInfo,
    string? ProfileImageUrl
) : IRequest<ApiResponse<bool>>;

public class GetTeacherDashboardStatsQueryHandler : IRequestHandler<GetTeacherDashboardStatsQuery, ApiResponse<TeacherDashboardStatsDto>>
{
    private readonly IAppDbContext _db;

    public GetTeacherDashboardStatsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<TeacherDashboardStatsDto>> Handle(GetTeacherDashboardStatsQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<TeacherDashboardStatsDto>.Fail("حساب المعلم غير موجود");
        }

        var activeStudentsCount = await _db.StudentAccessGrants
            .Where(s => s.PackageId != null && s.IsActive && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .Where(s => _db.Packages.Any(p => p.Id == s.PackageId && p.TeacherId == teacherProfile.Id))
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(ct);

        var packagesCount = await _db.Packages.CountAsync(p => p.TeacherId == teacherProfile.Id, ct);

        var examsCount = await _db.Exams.CountAsync(e => e.CreatedByTeacherId == teacherProfile.Id, ct);

        var pendingEssaysCount = await _db.EssaySubmissions
            .Include(es => es.Question)
            .CountAsync(es => es.Status == EssaySubmissionStatus.WaitTeacher && es.Question.CreatedByTeacherId == teacherProfile.Id, ct);

        var dto = new TeacherDashboardStatsDto(activeStudentsCount, packagesCount, examsCount, pendingEssaysCount);
        return ApiResponse<TeacherDashboardStatsDto>.Ok(dto);
    }
}

public class GetTeacherStudentsQueryHandler : IRequestHandler<GetTeacherStudentsQuery, ApiResponse<List<TeacherStudentDto>>>
{
    private readonly IAppDbContext _db;

    public GetTeacherStudentsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<TeacherStudentDto>>> Handle(GetTeacherStudentsQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<List<TeacherStudentDto>>.Fail("حساب المعلم غير موجود");
        }

        var students = await _db.StudentAccessGrants
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.PackageId != null && s.IsActive && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .Where(s => _db.Packages.Any(p => p.Id == s.PackageId && p.TeacherId == teacherProfile.Id))
            .Select(s => new TeacherStudentDto(
                s.User.Id,
                s.User.FullName,
                s.User.PhoneNumber,
                _db.Packages.Where(p => p.Id == s.PackageId).Select(p => p.Name).FirstOrDefault() ?? string.Empty,
                s.GrantedAt
            ))
            .Distinct()
            .ToListAsync(ct);

        return ApiResponse<List<TeacherStudentDto>>.Ok(students);
    }
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
            teacherProfile.ContactInfo
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

        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }
}
