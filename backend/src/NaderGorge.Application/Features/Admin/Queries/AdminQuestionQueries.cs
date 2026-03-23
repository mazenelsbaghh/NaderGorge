using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ListQuestionsQuery(int Page = 1, int PageSize = 20, string? Search = null) : IRequest<ApiResponse<PagedResult<QuestionBankItemDto>>>;

public record QuestionOptionDto(Guid Id, string Text, bool IsCorrect);

public record QuestionBankItemDto(Guid Id, string Text, decimal DefaultPoints, string Tags, List<QuestionOptionDto> Options);

public class ListQuestionsQueryHandler : IRequestHandler<ListQuestionsQuery, ApiResponse<PagedResult<QuestionBankItemDto>>>
{
    private readonly IAppDbContext _db;

    public ListQuestionsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<PagedResult<QuestionBankItemDto>>> Handle(ListQuestionsQuery request, CancellationToken ct)
    {
        var query = _db.QuestionBankItems.Include(q => q.Options).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(q => q.Text.Contains(request.Search) || q.Tags.Contains(request.Search));
        }

        var total = await query.CountAsync(ct);

        var questions = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = questions.Select(q => new QuestionBankItemDto(
            q.Id,
            q.Text,
            q.DefaultPoints,
            q.Tags,
            q.Options.Select(o => new QuestionOptionDto(o.Id, o.Text, o.IsCorrect)).ToList()
        )).ToList();

        return ApiResponse<PagedResult<QuestionBankItemDto>>.Ok(new PagedResult<QuestionBankItemDto>(dtos, total, request.Page, request.PageSize));
    }
}
