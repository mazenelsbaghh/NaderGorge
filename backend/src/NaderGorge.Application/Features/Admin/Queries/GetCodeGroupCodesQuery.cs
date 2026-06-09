using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetCodeGroupCodesQuery(Guid GroupId) : IRequest<ApiResponse<List<CodeDetailDto>>>;

public record CodeDetailDto(
    string Code,
    long SerialNumber,
    bool IsUsed,
    DateTime? UsedAt,
    Guid? UsedByUserId,
    string? UsedByStudentName,
    string? UsedByStudentPhone);

public class GetCodeGroupCodesQueryHandler : IRequestHandler<GetCodeGroupCodesQuery, ApiResponse<List<CodeDetailDto>>>
{
    private readonly IAppDbContext _db;

    public GetCodeGroupCodesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<CodeDetailDto>>> Handle(GetCodeGroupCodesQuery request, CancellationToken ct)
    {
        var group = await _db.CodeGroups
            .Include(cg => cg.AccessCodes)
                .ThenInclude(ac => ac.ConsumedByUser)
            .FirstOrDefaultAsync(cg => cg.Id == request.GroupId, ct);

        if (group == null) return ApiResponse<List<CodeDetailDto>>.Fail("Code Group not found");

        var dtos = group.AccessCodes.OrderBy(c => c.CreatedAt).Select(c => new CodeDetailDto(
            c.CodePlaintext ?? c.CodeHash,
            c.SerialNumber,
            c.IsConsumed,
            c.ConsumedAt,
            c.ConsumedByUserId,
            c.ConsumedByUser != null ? c.ConsumedByUser.FullName : null,
            c.ConsumedByUser != null ? c.ConsumedByUser.PhoneNumber : null
        )).ToList();

        return ApiResponse<List<CodeDetailDto>>.Ok(dtos);
    }
}
