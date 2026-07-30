using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class SalesRedemptionService : ISalesRedemptionService
{
    private readonly IAppDbContext _db;

    public SalesRedemptionService(IAppDbContext db) => _db = db;

    public async Task<SalesRedemptionResult> RedeemPrintableCodeAsync(Guid studentId, Guid requestId, string code, CancellationToken cancellationToken = default)
    {
        var hash = DiscountEngine.HashCode(code);
        var printable = await _db.PrintableSalesCodes
            .Include(x => x.Batch)
            .FirstOrDefaultAsync(x => x.CodeHash == hash, cancellationToken);

        if (printable == null)
            return new SalesRedemptionResult(false, "الكود غير موجود.", null, null);

        if (printable.Batch.Behavior != PrintableCodeBehavior.DirectAccess)
            return new SalesRedemptionResult(false, "هذا الكود يستخدم كخصم أثناء الشراء وليس فتح مباشر.", null, null);

        if (printable.Status != SalesStatus.Active || printable.Batch.Status != SalesStatus.Active || printable.UsedCount >= printable.UsageLimit)
            return new SalesRedemptionResult(false, "الكود غير صالح أو تم استخدامه.", null, null);

        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GrantType = ToCodeType(printable.Batch.TargetType),
            GrantedAt = DateTime.UtcNow,
            IsActive = true
        };

        switch (printable.Batch.TargetType)
        {
            case SalesTargetType.Package:
                grant.PackageId = printable.Batch.TargetId;
                break;
            case SalesTargetType.Term:
                grant.TermId = printable.Batch.TargetId;
                break;
            case SalesTargetType.ContentSection:
                grant.ContentSectionId = printable.Batch.TargetId;
                break;
            case SalesTargetType.Lesson:
                grant.LessonId = printable.Batch.TargetId;
                break;
            case SalesTargetType.SpecificVideo:
                grant.LessonVideoId = printable.Batch.TargetId;
                break;
            case SalesTargetType.VideoType:
                grant.VideoTypeId = printable.Batch.TargetId;
                break;
            case SalesTargetType.PublicExam:
                grant.PublicExamProductId = printable.Batch.TargetId;
                if (printable.Batch.TargetId is Guid publicExamId)
                {
                    grant.ExamId = await _db.PublicExamProducts.Where(x => x.Id == publicExamId).Select(x => (Guid?)x.ExamId).FirstOrDefaultAsync(cancellationToken);
                }
                break;
            default:
                return new SalesRedemptionResult(false, "نوع الهدف غير مدعوم للفتح المباشر.", null, null);
        }

        printable.UsedCount++;
        printable.Status = printable.UsedCount >= printable.UsageLimit ? SalesStatus.Consumed : SalesStatus.Active;
        printable.ConsumedByUserId = studentId;
        printable.ConsumedAt ??= DateTime.UtcNow;
        printable.Batch.UsedCount++;

        _db.StudentAccessGrants.Add(grant);
        _db.PrintableCodeRedemptions.Add(new PrintableCodeRedemption
        {
            PrintableCodeId = printable.Id,
            StudentId = studentId,
            RequestId = requestId,
            TargetType = printable.Batch.TargetType,
            TargetId = printable.Batch.TargetId ?? Guid.Empty,
            AppliedAmount = 0
        });

        await _db.SaveChangesAsync(cancellationToken);
        return new SalesRedemptionResult(true, "تم تفعيل الكود بنجاح.", grant.Id, null);
    }

    private static CodeType ToCodeType(SalesTargetType targetType) => targetType switch
    {
        SalesTargetType.Package => CodeType.Package,
        SalesTargetType.Term => CodeType.Term,
        SalesTargetType.ContentSection => CodeType.Month,
        SalesTargetType.Lesson => CodeType.Lesson,
        SalesTargetType.SpecificVideo => CodeType.Video,
        SalesTargetType.VideoType => CodeType.Video,
        SalesTargetType.PublicExam => CodeType.Exam,
        _ => CodeType.Balance
    };
}
