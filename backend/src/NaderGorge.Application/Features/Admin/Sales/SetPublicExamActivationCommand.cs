using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Sales;

public sealed record SetPublicExamActivationCommand(Guid ProductId, bool IsEnabled, Guid ActorId)
    : IRequest<ApiResponse<PublicExamProductDto>>;

public sealed class SetPublicExamActivationCommandHandler(IAppDbContext db)
    : IRequestHandler<SetPublicExamActivationCommand, ApiResponse<PublicExamProductDto>>
{
    public async Task<ApiResponse<PublicExamProductDto>> Handle(SetPublicExamActivationCommand request, CancellationToken ct)
    {
        var product = await db.PublicExamProducts.Include(product => product.Exam)
            .SingleOrDefaultAsync(product => product.Id == request.ProductId, ct);
        if (product is null)
            return ApiResponse<PublicExamProductDto>.Fail("الامتحان العام غير موجود.", ["NOT_FOUND"]);
        if (request.IsEnabled && product.Exam.ArchiveMode != ContentArchiveMode.None)
            return ApiResponse<PublicExamProductDto>.Fail("ألغِ أرشفة الامتحان أولًا قبل تفعيله.");
        if (request.IsEnabled)
        {
            var availability = PublicExamAvailabilityRules.Resolve(product.AvailableFrom, product.AvailableUntil, true);
            if (availability.ValidationError is not null)
                return ApiResponse<PublicExamProductDto>.Fail(availability.ValidationError);
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = "PublicExamActivationChanged", EntityType = nameof(PublicExamProduct), EntityId = product.Id,
            PerformedByUserId = request.ActorId,
            OldValues = System.Text.Json.JsonSerializer.Serialize(new { product.IsPublished, product.DisabledAt, product.Exam.IsActive }),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { request.IsEnabled })
        });
        product.IsPublished = request.IsEnabled;
        product.DisabledAt = request.IsEnabled ? null : DateTime.UtcNow;
        product.DisabledByUserId = request.IsEnabled ? null : request.ActorId;
        product.DisableReason = request.IsEnabled ? null : "إيقاف من زر تفعيل الامتحان العام";
        if (request.IsEnabled) product.Exam.IsActive = true;
        await db.SaveChangesAsync(ct);
        return ApiResponse<PublicExamProductDto>.Ok(
            await SavePublicExamProductCommandHandler.ToPublicExamProductDtoAsync(db, product, product.Exam.Title, ct),
            request.IsEnabled ? "تم تفعيل الامتحان العام." : "تم إيقاف الامتحان العام مع حفظ المحاولات والدرجات.");
    }
}
