using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ListQuestionsQuery(int Page = 1, int PageSize = 20, string? Search = null, Guid? CurrentUserId = null) : IRequest<ApiResponse<PagedResult<QuestionBankItemDto>>>;

public record QuestionOptionDto(Guid Id, string Text, bool IsCorrect);

public record QuestionBankItemDto(Guid Id, string Text, decimal DefaultPoints, string Tags, List<QuestionOptionDto> Options, Guid CreatedByTeacherId, Guid SubjectId);

public class ListQuestionsQueryHandler : IRequestHandler<ListQuestionsQuery, ApiResponse<PagedResult<QuestionBankItemDto>>>
{
    private readonly IAppDbContext _db;

    public ListQuestionsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<PagedResult<QuestionBankItemDto>>> Handle(ListQuestionsQuery request, CancellationToken ct)
    {
        Guid? teacherId = null;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, ct);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                teacherId = user.TeacherProfile?.Id;
            }
        }

        var query = _db.QuestionBankItems.Include(q => q.Options)
            .Where(q => q.Tags != "Inline" && q.Tags != "Added")
            .AsQueryable();

        if (teacherId.HasValue)
        {
            query = query.Where(q => q.CreatedByTeacherId == teacherId.Value);
        }

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
            q.Options.Select(o => new QuestionOptionDto(o.Id, o.Text, o.IsCorrect)).ToList(),
            q.CreatedByTeacherId,
            q.SubjectId
        )).ToList();

        return ApiResponse<PagedResult<QuestionBankItemDto>>.Ok(new PagedResult<QuestionBankItemDto>(dtos, total, request.Page, request.PageSize));
    }
}
