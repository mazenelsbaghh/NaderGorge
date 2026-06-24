using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities.Student;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Parent.Queries;

public record GetStudentAcademicDetailsQuery(Guid StudentProfileId) : IRequest<ApiResponse<StudentAcademicDetailsDto>>;

public record StudentAcademicDetailsDto(
    string StudentName,
    string Grade,
    string? School,
    string? AvatarSlug,
    AttendanceDetailsDto Attendance,
    List<ExamDetailDto> Exams,
    List<HomeworkDetailDto> Homeworks,
    List<WarningDetailDto> Warnings
);

public record AttendanceDetailsDto(
    int TotalLessons,
    int WatchedLessons,
    double CompletionRate
);

public record ExamDetailDto(
    string ExamTitle,
    decimal Score,
    decimal TotalScore,
    double Percentage,
    DateTime SubmittedAt,
    string Status
);

public record HomeworkDetailDto(
    string Title,
    bool IsSubmitted,
    string SubmissionState,
    string? Grade,
    DateTime? SubmittedAt
);

public record WarningDetailDto(
    string Reason,
    string Severity,
    DateTime CreatedAt
);

public class GetStudentAcademicDetailsQueryHandler : IRequestHandler<GetStudentAcademicDetailsQuery, ApiResponse<StudentAcademicDetailsDto>>
{
    private readonly IAppDbContext _db;

    public GetStudentAcademicDetailsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<StudentAcademicDetailsDto>> Handle(GetStudentAcademicDetailsQuery request, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == request.StudentProfileId, ct);

        if (profile == null)
        {
            return ApiResponse<StudentAcademicDetailsDto>.Fail("ملف الطالب غير موجود");
        }

        // Get all packages the student has active access to
        var activePackageIds = await _db.StudentAccessGrants
            .AsNoTracking()
            .Where(g => g.UserId == profile.UserId && g.IsActive && g.PackageId != null)
            .Select(g => g.PackageId!.Value)
            .ToListAsync(ct);

        // Get all lesson IDs in those packages
        var lessonIds = await _db.Lessons
            .AsNoTracking()
            .Where(l => activePackageIds.Contains(l.ContentSection.Term.PackageId))
            .Select(l => l.Id)
            .ToListAsync(ct);

        var totalLessons = lessonIds.Count;
        var watchedLessons = await _db.LessonProgresses
            .AsNoTracking()
            .Where(lp => lp.UserId == profile.UserId && lp.IsCompleted && lessonIds.Contains(lp.LessonId))
            .CountAsync(ct);

        var completionRate = totalLessons > 0 ? Math.Round((double)watchedLessons / totalLessons * 100, 2) : 0.0;

        var attendance = new AttendanceDetailsDto(totalLessons, watchedLessons, completionRate);

        // Fetch Exam attempts
        var exams = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == profile.UserId)
            .Include(a => a.Exam)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ExamDetailDto(
                a.Exam.Title,
                a.ScoreAchieved,
                a.Exam.TotalScore,
                a.Exam.TotalScore > 0 ? (double)Math.Round((a.ScoreAchieved / a.Exam.TotalScore) * 100, 2) : 0.0,
                a.CreatedAt,
                a.IsPassed ? "Passed" : "Failed"
            ))
            .ToListAsync(ct);

        // Fetch Homework submissions
        var homeworksRaw = await _db.Homeworks
            .AsNoTracking()
            .Where(h => lessonIds.Contains(h.LessonId))
            .Select(h => new
            {
                h.Title,
                Submission = _db.HomeworkSubmissions
                    .AsNoTracking()
                    .FirstOrDefault(s => s.HomeworkId == h.Id && s.StudentId == profile.UserId)
            })
            .ToListAsync(ct);

        var homeworks = homeworksRaw.Select(h => new HomeworkDetailDto(
            h.Title,
            h.Submission != null && h.Submission.SubmittedAt != null,
            h.Submission == null ? "NotSubmitted" : h.Submission.Status.ToString(),
            h.Submission != null ? (h.Submission.Evaluation ?? h.Submission.OverallScore.ToString("G29")) : null,
            h.Submission?.SubmittedAt
        )).ToList();

        // Fetch Warning events
        var warnings = await _db.WarningEvents
            .AsNoTracking()
            .Where(w => w.StudentId == profile.UserId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WarningDetailDto(
                w.TriggerReason,
                w.Severity.ToString(),
                w.CreatedAt
            ))
            .ToListAsync(ct);

        var gradeAr = MapGradeLevelAr(profile.GradeLevel.ToString());

        var dto = new StudentAcademicDetailsDto(
            profile.User.FullName,
            gradeAr,
            profile.SchoolName,
            profile.AvatarSlug,
            attendance,
            exams,
            homeworks,
            warnings
        );

        return ApiResponse<StudentAcademicDetailsDto>.Ok(dto);
    }

    private static string MapGradeLevelAr(string grade)
    {
        return grade switch
        {
            "FirstPrimary" => "أولى ابتدائي",
            "SecondPrimary" => "ثانية ابتدائي",
            "ThirdPrimary" => "ثالثة ابتدائي",
            "FourthPrimary" => "رابعة ابتدائي",
            "FifthPrimary" => "خامسة ابتدائي",
            "SixthPrimary" => "سادسة ابتدائي",
            "FirstPreparatory" => "أولى إعدادي",
            "SecondPreparatory" => "ثانية إعدادي",
            "ThirdPreparatory" => "ثالثة إعدادي",
            "FirstSecondary" => "أولى ثانوي",
            "SecondSecondary" => "ثانية ثانوي",
            "ThirdSecondary" => "ثالثة ثانوي",
            _ => grade
        };
    }
}
