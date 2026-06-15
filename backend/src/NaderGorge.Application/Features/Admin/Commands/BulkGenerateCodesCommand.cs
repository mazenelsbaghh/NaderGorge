using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Services;
using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;

namespace NaderGorge.Application.Features.Admin.Commands;

/// <summary>
/// Phase 3: Expanded code generation supporting all 6 code types.
/// </summary>
public record BulkGenerateCodesCommand(
    string GroupName,
    CodeType CodeType,
    int Count,
    int CodeLength,
    Guid AdminId,
    // Target references (one required depending on CodeType)
    Guid? PackageId = null,
    Guid? TermId = null,
    Guid? ContentSectionId = null,
    Guid? LessonId = null,
    Guid? ExamId = null,
    // Video targets (for CodeType.Video)
    List<Guid>? VideoTargetIds = null,
    // Balance (for CodeType.Balance)
    decimal? BalanceAmount = null,
    // Optional
    decimal? DiscountPercentage = null,
    DateTime? ExpiresAt = null
) : IRequest<ApiResponse<BulkGenerateCodesResponse>>;

public record BulkGenerateCodesResponse(Guid CodeGroupId, int CodesGenerated, List<string> Codes);

public class BulkGenerateCodesCommandHandler : IRequestHandler<BulkGenerateCodesCommand, ApiResponse<BulkGenerateCodesResponse>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;

    public BulkGenerateCodesCommandHandler(IAppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<BulkGenerateCodesResponse>> Handle(BulkGenerateCodesCommand request, CancellationToken ct)
    {
        if (request.Count <= 0 || request.Count > 10_000)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("Count must be between 1 and 10,000");

        if (request.CodeLength < 6 || request.CodeLength > 20)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("Code length must be between 6 and 20");

        // Validate target based on CodeType
        var validationError = ValidateTargets(request);
        if (validationError != null)
            return ApiResponse<BulkGenerateCodesResponse>.Fail(validationError);

        // Resolve user role
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.TeacherProfile)
            .FirstOrDefaultAsync(u => u.Id == request.AdminId, ct);

        if (user == null)
            return ApiResponse<BulkGenerateCodesResponse>.Fail("User not found.");

        var isTeacher = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher);
        if (isTeacher)
        {
            return ApiResponse<BulkGenerateCodesResponse>.Fail("Unauthorized: Teachers are not allowed to generate codes.");
        }

        var permissionsList = new List<string>();
        foreach (var ur in user.UserRoles)
        {
            if (ur.Role != null && !string.IsNullOrEmpty(ur.Role.PermissionsJson))
            {
                try
                {
                    var perms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(ur.Role.PermissionsJson);
                    if (perms != null)
                        permissionsList.AddRange(perms);
                }
                catch { }
            }
        }

        if (!permissionsList.Contains("codes.manage"))
        {
            return ApiResponse<BulkGenerateCodesResponse>.Fail("Unauthorized: You do not have permission to manage codes.");
        }

        Guid groupTeacherId = Guid.Empty;
        if (false) // Legacy teacher branch disabled
        {
            groupTeacherId = Guid.Empty;
        }
        else
        {
            // Resolve teacher from target
            if (request.PackageId.HasValue)
            {
                var pkg = await _db.Packages.FindAsync(new object[] { request.PackageId.Value }, ct);
                if (pkg != null) groupTeacherId = pkg.TeacherId;
            }
            else if (request.TermId.HasValue)
            {
                var term = await _db.Terms.Include(t => t.Package).FirstOrDefaultAsync(t => t.Id == request.TermId.Value, ct);
                if (term?.Package != null) groupTeacherId = term.Package.TeacherId;
            }
            else if (request.ContentSectionId.HasValue)
            {
                var sec = await _db.ContentSections.Include(s => s.Term).ThenInclude(t => t.Package).FirstOrDefaultAsync(s => s.Id == request.ContentSectionId.Value, ct);
                if (sec?.Term?.Package != null) groupTeacherId = sec.Term.Package.TeacherId;
            }
            else if (request.LessonId.HasValue)
            {
                var les = await _db.Lessons.Include(l => l.ContentSection).ThenInclude(s => s.Term).ThenInclude(t => t.Package).FirstOrDefaultAsync(l => l.Id == request.LessonId.Value, ct);
                if (les?.ContentSection?.Term?.Package != null) groupTeacherId = les.ContentSection.Term.Package.TeacherId;
            }
            else if (request.ExamId.HasValue)
            {
                var exam = await _db.Exams.FindAsync(new object[] { request.ExamId.Value }, ct);
                if (exam != null) groupTeacherId = exam.CreatedByTeacherId;
            }
            else if (request.CodeType == CodeType.Video && request.VideoTargetIds != null && request.VideoTargetIds.Any())
            {
                var vid = await _db.LessonVideos.Include(v => v.Lesson).ThenInclude(l => l.ContentSection).ThenInclude(s => s.Term).ThenInclude(t => t.Package).FirstOrDefaultAsync(v => v.Id == request.VideoTargetIds.First(), ct);
                if (vid?.Lesson?.ContentSection?.Term?.Package != null) groupTeacherId = vid.Lesson.ContentSection.Term.Package.TeacherId;
            }

            if (groupTeacherId == Guid.Empty)
            {
                var firstTeacher = await _db.TeacherProfiles.FirstOrDefaultAsync(ct);
                if (firstTeacher != null)
                {
                    groupTeacherId = firstTeacher.Id;
                }
            }

            if (groupTeacherId == Guid.Empty)
            {
                return ApiResponse<BulkGenerateCodesResponse>.Fail("Could not resolve teacher context for the access codes.");
            }
        }

        // Create code group
        var group = new CodeGroup
        {
            Id = Guid.NewGuid(),
            Name = request.GroupName ?? $"Batch-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            TotalCodes = request.Count,
            CodeType = request.CodeType,
            PackageId = request.PackageId,
            TermId = request.TermId,
            ContentSectionId = request.ContentSectionId,
            LessonId = request.LessonId,
            ExamId = request.ExamId,
            BalanceAmount = request.BalanceAmount,
            DiscountPercentage = request.DiscountPercentage,
            ExpiresAt = request.ExpiresAt,
            CreatedByUserId = request.AdminId,
            TeacherId = groupTeacherId
        };
        _db.CodeGroups.Add(group);

        // Add video targets if Video code type
        if (request.CodeType == CodeType.Video && request.VideoTargetIds != null)
        {
            foreach (var videoId in request.VideoTargetIds)
            {
                _db.CodeVideoTargets.Add(new CodeVideoTarget
                {
                    CodeGroupId = group.Id,
                    LessonVideoId = videoId
                });
            }
        }

        // Generate codes
        var maxSerial = await _db.AccessCodes.MaxAsync(c => (long?)c.SerialNumber, ct) ?? 10000000;
        var codes = new List<AccessCode>(request.Count);
        var plaintexts = new List<string>(request.Count);

        for (int i = 0; i < request.Count; i++)
        {
            var plaintext = GenerateSecureCode(request.CodeLength);
            var hash = HashCode(plaintext);
            maxSerial++;

            codes.Add(new AccessCode
            {
                Id = Guid.NewGuid(),
                CodeHash = hash,
                CodePlaintext = plaintext,
                CodeGroupId = group.Id,
                IsConsumed = false,
                ExpiresAt = request.ExpiresAt,
                SerialNumber = maxSerial
            });

            plaintexts.Add(plaintext);
        }

        _db.AccessCodes.AddRange(codes);

        var codeGroupCreatedEvent = new OutboxEvent
        {
            Type = "CodeGroupCreated",
            TargetUserId = request.AdminId.ToString(),
            TargetGroup = null,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                codeGroupId = group.Id,
                name = group.Name,
                codeType = group.CodeType.ToString(),
                totalCodes = group.TotalCodes,
                createdAt = DateTime.UtcNow
            })
        };
        _db.OutboxEvents.Add(codeGroupCreatedEvent);

        var codeGroupExportReadyEvent = new OutboxEvent
        {
            Type = "CodeGroupExportReady",
            TargetUserId = request.AdminId.ToString(),
            TargetGroup = null,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                codeGroupId = group.Id,
                name = group.Name,
                totalCodes = group.TotalCodes,
                exportStatus = "Ready"
            })
        };
        _db.OutboxEvents.Add(codeGroupExportReadyEvent);

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            action: "BulkGenerateCodes",
            entityType: "CodeGroup",
            entityId: group.Id,
            userId: request.AdminId,
            newValues: new { Count = request.Count, CodeType = request.CodeType.ToString() }
        );

        return ApiResponse<BulkGenerateCodesResponse>.Ok(
            new BulkGenerateCodesResponse(group.Id, request.Count, plaintexts));
    }

    private static string? ValidateTargets(BulkGenerateCodesCommand request)
    {
        return request.CodeType switch
        {
            CodeType.Package when request.PackageId == null => "PackageId is required for Package codes",
            CodeType.Term when request.TermId == null => "TermId is required for Term codes",
            CodeType.Month when request.ContentSectionId == null => "ContentSectionId is required for Month codes",
            CodeType.Lesson when request.LessonId == null => "LessonId is required for Lesson codes",
            CodeType.Exam when request.ExamId == null => "ExamId is required for Exam codes",
            CodeType.Video when (request.VideoTargetIds == null || request.VideoTargetIds.Count == 0) => "VideoTargetIds are required for Video codes",
            CodeType.Balance when (request.BalanceAmount == null || request.BalanceAmount <= 0) => "BalanceAmount must be > 0 for Balance codes",
            _ => null
        };
    }

    private static string GenerateSecureCode(int length)
    {
        const string chars = "0123456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }

    private static string HashCode(string plaintext)
    {
        var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(hashBytes);
    }
}
