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
