using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Sales;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/public-exams")]
public sealed class PublicExamsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISalesRedemptionService _redemption;
    private readonly IAppDbContext _db;

    public PublicExamsController(IMediator mediator, ISalesRedemptionService redemption, IAppDbContext db)
    {
        _mediator = mediator;
        _redemption = redemption;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var hasStudent = Guid.TryParse(userIdClaim, out var studentId) && studentId != Guid.Empty;
        var response = await _mediator.Send(new GetPublicExamProductsQuery(PublishedOnly: true, StudentId: hasStudent ? studentId : null), ct);
        var products = response.Data ?? Array.Empty<PublicExamProductDto>();

        if (!hasStudent)
        {
            return Ok(response);
        }

        var now = DateTime.UtcNow;
        var productIds = products.Select(x => x.Id).ToList();
        var examIds = products.Select(x => x.ExamId).ToList();

        var accessProductIds = await _db.StudentAccessGrants
            .Where(g => g.UserId == studentId
                && g.IsActive
                && g.GrantType == CodeType.Exam
                && g.PublicExamProductId.HasValue
                && productIds.Contains(g.PublicExamProductId.Value)
                && (g.ExpiresAt == null || g.ExpiresAt > now))
            .Select(g => g.PublicExamProductId!.Value)
            .ToListAsync(ct);

        var accessExamIds = await _db.StudentAccessGrants
            .Where(g => g.UserId == studentId
                && g.IsActive
                && g.GrantType == CodeType.Exam
                && g.ExamId.HasValue
                && examIds.Contains(g.ExamId.Value)
                && (g.ExpiresAt == null || g.ExpiresAt > now))
            .Select(g => g.ExamId!.Value)
            .ToListAsync(ct);

        var completedAttempts = await _db.StudentExamAttempts
            .Where(a => a.UserId == studentId
                && examIds.Contains(a.ExamId)
                && (_db.StudentAnswers.Any(answer => answer.StudentExamAttemptId == a.Id)
                    || _db.EssaySubmissions.Any(essay => essay.StudentExamAttemptId == a.Id)))
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .Select(a => new { a.Id, a.ExamId, a.IsPassed, a.ScoreAchieved })
            .ToListAsync(ct);

        var accessProductSet = accessProductIds.ToHashSet();
        var accessExamSet = accessExamIds.ToHashSet();
        var attemptsByExam = completedAttempts
            .GroupBy(a => a.ExamId)
            .ToDictionary(group => group.Key, group => group.First());

        var data = products.Select(product =>
        {
            var hasAccess = !product.IsPaid || accessProductSet.Contains(product.Id) || accessExamSet.Contains(product.ExamId);
            attemptsByExam.TryGetValue(product.ExamId, out var attempt);
            return new
            {
                product.Id,
                product.ExamId,
                product.ExamTitle,
                product.Slug,
                product.IsPublished,
                product.IsPaid,
                product.Price,
                product.TeacherId,
                product.SubjectId,
                product.GradeLevel,
                product.IsPlatformWide,
                product.AvailableFrom,
                product.AvailableUntil,
                product.DisabledAt,
                HasAccess = hasAccess,
                HasCompletedAttempt = attempt != null,
                LatestAttemptId = attempt?.Id,
                LatestAttemptIsPassed = attempt?.IsPassed,
                LatestAttemptScore = attempt?.ScoreAchieved
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPost("redeem")]
    [Authorize]
    public async Task<IActionResult> Redeem([FromBody] RedeemPrintableCodeRequest request, CancellationToken ct)
    {
        var result = await _redemption.RedeemPrintableCodeAsync(User.RequireUserId(), request.RequestId ?? Guid.NewGuid(), request.Code, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public sealed record RedeemPrintableCodeRequest(string Code, Guid? RequestId);
