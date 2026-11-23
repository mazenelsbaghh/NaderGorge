using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

internal sealed class LiveSupportCoursePurchaseService(IAppDbContext db, IMediator mediator)
{
    public async Task<IReadOnlyList<LiveSupportPurchaseCourseDto>> CoursesAsync(Guid studentId, CancellationToken ct)
        => (await AvailablePackagesAsync(studentId, ct)).Select(Course).OrderBy(course => course.TeacherName).ThenBy(course => course.Name).ToArray();

    public async Task<LiveSupportPurchasePreviewDto> PreviewAsync(Guid studentId, Guid packageId, CancellationToken ct)
    {
        var quote = await QuoteAsync(studentId, packageId, ct);
        return new(Course(quote.Package), quote.Funding, quote.Version, "");
    }

    public async Task<string> PurchaseAsync(Guid studentId, Guid actorId, JsonElement payload, CancellationToken ct)
    {
        if (!payload.TryGetProperty("packageId", out var packageValue) || packageValue.ValueKind != JsonValueKind.String
            || !packageValue.TryGetGuid(out var packageId) || !payload.TryGetProperty("quoteVersion", out var version)
            || version.ValueKind != JsonValueKind.String)
            throw new LiveSupportException("VALIDATION_ERROR", "اختر الكورس وراجع التكلفة أولًا.");
        var quote = await QuoteAsync(studentId, packageId, ct);
        if (quote.Version != version.GetString())
            throw new LiveSupportException("PURCHASE_QUOTE_STALE", "تغير سعر الكورس أو رصيد الطالب. راجع التكلفة مرة أخرى.");
        if (!quote.Funding.IsSufficient)
            throw new LiveSupportException("INSUFFICIENT_BALANCE", "رصيد الطالب المتاح لا يكفي لشراء الكورس.");
        var result = await mediator.Send(new PurchaseContentCommand(studentId, quote.Type, quote.ContentId,
            ExpectedPrice: quote.Funding.DiscountedPrice, PerformedByUserId: actorId), ct);
        if (!result.Success)
            throw new LiveSupportException(result.Errors?.Contains("PURCHASE_PRICE_CHANGED") == true ? "PURCHASE_QUOTE_STALE" : "ACTION_VALIDATION_FAILED",
                result.Message ?? "تعذر شراء الكورس.");
        return "تم شراء الكورس من رصيد الطالب بنجاح.";
    }

    private async Task<CourseQuote> QuoteAsync(Guid studentId, Guid packageId, CancellationToken ct)
    {
        var package = (await AvailablePackagesAsync(studentId, ct)).SingleOrDefault(package => package.Id == packageId)
            ?? throw new LiveSupportException("COURSE_UNAVAILABLE", "الكورس غير متاح للشراء لهذا الطالب أو مشترك فيه بالفعل.");
        var target = RootTarget(package);
        var response = await mediator.Send(new GetPurchaseFundingPreviewQuery(studentId, target.Type, target.Id), ct);
        if (!response.Success || response.Data is null)
            throw new LiveSupportException("PURCHASE_PREVIEW_FAILED", response.Message ?? "تعذر حساب تكلفة الشراء.");
        var fingerprint = JsonSerializer.Serialize(new { studentId, packageId, target.Type, target.Id, funding = response.Data });
        return new(package, target.Type, target.Id, response.Data, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))));
    }

    private sealed record CourseQuote(PackageDto Package, CodeType Type, Guid ContentId, PurchaseFundingPreviewDto Funding, string Version);

    private async Task<IReadOnlyList<PackageDto>> AvailablePackagesAsync(Guid studentId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(user => user.Id == studentId && user.IsActive
            && user.UserRoles.Any(role => role.Role.Type == RoleType.Student), ct))
            throw new LiveSupportException("STUDENT_UNAVAILABLE", "حساب الطالب غير متاح للشراء.");
        var response = await mediator.Send(new GetPackagesQuery(studentId), ct);
        if (!response.Success || response.Data is null)
            throw new LiveSupportException("COURSE_LOOKUP_FAILED", response.Message ?? "تعذر تحميل الكورسات.");
        return response.Data.Where(package => package.IsActive && !package.HasDirectPackageAccess && !package.HasRootContentAccess
            && package.ArchiveMode == ContentArchiveMode.None
            && !(package.ContentMode == PackageContentMode.TermWithSections && !package.AllowFullPackagePurchase)
            && HasRootTarget(package)).ToArray();
    }

    private static bool HasRootTarget(PackageDto package) => package.ContentMode switch
    {
        PackageContentMode.SectionWithLessons => package.RootTermId.HasValue,
        PackageContentMode.LessonsOnly => package.RootSectionId.HasValue,
        PackageContentMode.SingleLesson => package.DirectLessons.Count > 0,
        _ => true
    };

    private static (CodeType Type, Guid Id) RootTarget(PackageDto package) => package.ContentMode switch
    {
        PackageContentMode.SectionWithLessons => (CodeType.Term, package.RootTermId!.Value),
        PackageContentMode.LessonsOnly => (CodeType.Month, package.RootSectionId!.Value),
        PackageContentMode.SingleLesson => (CodeType.Lesson, package.DirectLessons[0].Id),
        _ => (CodeType.Package, package.Id)
    };

    private static LiveSupportPurchaseCourseDto Course(PackageDto package)
        => new(package.Id, package.Name, package.TeacherName, package.SubjectName, package.Price);
}
