using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetCodeGroupCodesQuery(Guid GroupId) : IRequest<ApiResponse<List<CodeDetailDto>>>;

public record CodeDetailDto(string Code, bool IsUsed, DateTime? UsedAt, Guid? UsedByUserId);

public class GetCodeGroupCodesQueryHandler : IRequestHandler<GetCodeGroupCodesQuery, ApiResponse<List<CodeDetailDto>>>
{
    private readonly IAppDbContext _db;

    public GetCodeGroupCodesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<CodeDetailDto>>> Handle(GetCodeGroupCodesQuery request, CancellationToken ct)
    {
        var group = await _db.CodeGroups
            .Include(cg => cg.AccessCodes)
            .FirstOrDefaultAsync(cg => cg.Id == request.GroupId, ct);

        if (group == null) return ApiResponse<List<CodeDetailDto>>.Fail("Code Group not found");

        var dtos = group.AccessCodes.OrderBy(c => c.CreatedAt).Select(c => new CodeDetailDto(
            c.CodePlaintext ?? c.CodeHash,
            c.IsConsumed,
            c.ConsumedAt,
            c.ConsumedByUserId
        )).ToList();

        return ApiResponse<List<CodeDetailDto>>.Ok(dtos);
    }
}
