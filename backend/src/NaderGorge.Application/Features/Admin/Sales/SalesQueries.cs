using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Sales;

public sealed record GetSalesRulesQuery : IRequest<ApiResponse<IReadOnlyList<SalesRuleDto>>>;
public sealed record GetSalesCouponsQuery : IRequest<ApiResponse<IReadOnlyList<SalesCouponDto>>>;
public sealed record GetSalesCouponByIdQuery(Guid Id) : IRequest<ApiResponse<SalesCouponDto>>;
public sealed record GetStackingPoliciesQuery : IRequest<ApiResponse<IReadOnlyList<StackingPolicyDto>>>;
public sealed record GetPrintableTemplatesQuery : IRequest<ApiResponse<IReadOnlyList<PrintableTemplateDto>>>;
public sealed record GetPrintableBatchesQuery : IRequest<ApiResponse<IReadOnlyList<PrintableBatchDto>>>;
public sealed record GetPublicExamProductsQuery(bool PublishedOnly, Guid? StudentId = null) : IRequest<ApiResponse<IReadOnlyList<PublicExamProductDto>>>;

public sealed class GetSalesRulesQueryHandler : IRequestHandler<GetSalesRulesQuery, ApiResponse<IReadOnlyList<SalesRuleDto>>>
{
    private readonly IAppDbContext _db;
    public GetSalesRulesQueryHandler(IAppDbContext db) => _db = db;
    public async Task<ApiResponse<IReadOnlyList<SalesRuleDto>>> Handle(GetSalesRulesQuery request, CancellationToken ct)
        => ApiResponse<IReadOnlyList<SalesRuleDto>>.Ok(await _db.SalesRules.OrderByDescending(x => x.CreatedAt).Select(x => new SalesRuleDto(x.Id, x.TargetType, x.TargetId, x.TeacherId, x.SubjectId, x.GradeLevel, x.VideoTypeId, x.IsActive)).ToListAsync(ct));
}

public sealed class GetSalesCouponsQueryHandler : IRequestHandler<GetSalesCouponsQuery, ApiResponse<IReadOnlyList<SalesCouponDto>>>
{
    private readonly IAppDbContext _db;
    public GetSalesCouponsQueryHandler(IAppDbContext db) => _db = db;
    public async Task<ApiResponse<IReadOnlyList<SalesCouponDto>>> Handle(GetSalesCouponsQuery request, CancellationToken ct)
        => ApiResponse<IReadOnlyList<SalesCouponDto>>.Ok(await _db.SalesCoupons.OrderByDescending(x => x.CreatedAt).Select(x => CreateSalesCouponCommandHandler.ToDto(x, Array.Empty<SalesCouponUsageDto>())).ToListAsync(ct));
}

public sealed class GetSalesCouponByIdQueryHandler : IRequestHandler<GetSalesCouponByIdQuery, ApiResponse<SalesCouponDto>>
{
    private readonly IAppDbContext _db;
    public GetSalesCouponByIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<SalesCouponDto>> Handle(GetSalesCouponByIdQuery request, CancellationToken ct)
    {
        var coupon = await _db.SalesCoupons.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (coupon == null) return ApiResponse<SalesCouponDto>.Fail("الكوبون غير موجود.", new List<string> { "NOT_FOUND" });

        var recentUsages = await _db.SalesCouponUsages
            .Include(x => x.Student)
            .Where(x => x.CouponId == request.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .Select(x => new SalesCouponUsageDto(
                x.Id,
                x.StudentId,
                x.Student.FullName,
                x.TargetType,
                x.TargetId,
                x.GrossAmount,
                x.DiscountAmount,
                x.CreatedAt))
            .ToListAsync(ct);

        return ApiResponse<SalesCouponDto>.Ok(CreateSalesCouponCommandHandler.ToDto(coupon, recentUsages));
    }
}

public sealed class GetStackingPoliciesQueryHandler : IRequestHandler<GetStackingPoliciesQuery, ApiResponse<IReadOnlyList<StackingPolicyDto>>>
{
    private readonly IAppDbContext _db;
    public GetStackingPoliciesQueryHandler(IAppDbContext db) => _db = db;
    public async Task<ApiResponse<IReadOnlyList<StackingPolicyDto>>> Handle(GetStackingPoliciesQuery request, CancellationToken ct)
        => ApiResponse<IReadOnlyList<StackingPolicyDto>>.Ok(await _db.DiscountStackingPolicies.OrderByDescending(x => x.CreatedAt).Select(x => SaveStackingPolicyCommandHandler.ToDto(x)).ToListAsync(ct));
}

public sealed class GetPrintableTemplatesQueryHandler : IRequestHandler<GetPrintableTemplatesQuery, ApiResponse<IReadOnlyList<PrintableTemplateDto>>>
{
    private readonly IAppDbContext _db;
    public GetPrintableTemplatesQueryHandler(IAppDbContext db) => _db = db;
    public async Task<ApiResponse<IReadOnlyList<PrintableTemplateDto>>> Handle(GetPrintableTemplatesQuery request, CancellationToken ct)
        => ApiResponse<IReadOnlyList<PrintableTemplateDto>>.Ok(await _db.PrintableCodeTemplates.OrderByDescending(x => x.CreatedAt).Select(x => SavePrintableTemplateCommandHandler.ToDto(x)).ToListAsync(ct));
}

public sealed class GetPrintableBatchesQueryHandler : IRequestHandler<GetPrintableBatchesQuery, ApiResponse<IReadOnlyList<PrintableBatchDto>>>
{
    private readonly IAppDbContext _db;
    public GetPrintableBatchesQueryHandler(IAppDbContext db) => _db = db;
    public async Task<ApiResponse<IReadOnlyList<PrintableBatchDto>>> Handle(GetPrintableBatchesQuery request, CancellationToken ct)
    {
        var batches = await _db.PrintableCodeBatches.Include(x => x.Codes).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return ApiResponse<IReadOnlyList<PrintableBatchDto>>.Ok(batches.Select(CreatePrintableBatchCommandHandler.ToDto).ToList());
    }
}

public sealed class GetPublicExamProductsQueryHandler : IRequestHandler<GetPublicExamProductsQuery, ApiResponse<IReadOnlyList<PublicExamProductDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;
    private readonly IContentArchiveAccessService? _archiveAccess;

    public GetPublicExamProductsQueryHandler(IAppDbContext db, IAcademicScopeService? academicScope = null, IContentArchiveAccessService? archiveAccess = null)
    {
        _db = db;
        _academicScope = academicScope;
        _archiveAccess = archiveAccess;
    }

    public async Task<ApiResponse<IReadOnlyList<PublicExamProductDto>>> Handle(GetPublicExamProductsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = _db.PublicExamProducts
            .Include(x => x.Exam)
            .Where(x => x.TeacherId == null || (x.Teacher != null && x.Teacher!.IsVisibleToStudents && x.Teacher!.IsContentVisibleToStudents))
            .AsQueryable();
        if (request.PublishedOnly)
        {
            query = query.Where(x => x.IsPublished && x.DisabledAt == null && (x.AvailableFrom == null || x.AvailableFrom <= now) && (x.AvailableUntil == null || x.AvailableUntil > now));
        }

        var rows = await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new PublicExamProductDto(x.Id, x.ExamId, x.Exam.Title, x.Slug, x.IsPublished, x.IsPaid, x.Price, x.TeacherId, x.SubjectId, x.GradeLevel, x.IsPlatformWide, x.AvailableFrom, x.AvailableUntil, x.DisabledAt, null, x.Exam.ArchiveMode, x.Exam.ArchivedAt))
            .ToListAsync(ct);

        if (request.PublishedOnly && _archiveAccess != null)
        {
            var visibleRows = new List<PublicExamProductDto>(rows.Count);
            foreach (var row in rows)
            {
                if (request.StudentId.HasValue
                    ? await _archiveAccess.CanViewAsync(request.StudentId.Value, ContentArchiveTargetType.Exam, row.ExamId, ct)
                    : row.ArchiveMode == ContentArchiveMode.None)
                {
                    visibleRows.Add(row);
                }
            }
            rows = visibleRows;
        }

        if (request.StudentId.HasValue && _academicScope != null)
        {
            var eligibleRows = new List<PublicExamProductDto>();
            foreach (var row in rows)
            {
                if (await _academicScope.IsOwnerEligibleForStudentAsync(
                        StudentFacingScopeOwnerType.PublicExamProduct,
                        row.Id,
                        request.StudentId.Value,
                        ct))
                {
                    eligibleRows.Add(row);
                }
            }

            rows = eligibleRows;
        }

        return ApiResponse<IReadOnlyList<PublicExamProductDto>>.Ok(rows);
    }
}
