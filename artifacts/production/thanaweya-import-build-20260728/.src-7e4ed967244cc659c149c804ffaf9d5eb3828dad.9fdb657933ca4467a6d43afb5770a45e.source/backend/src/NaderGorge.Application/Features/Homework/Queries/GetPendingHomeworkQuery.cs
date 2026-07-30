using MediatR;
using NaderGorge.Application.Common;

namespace NaderGorge.Application.Features.Homework.Queries;

public record PendingHomeworkDto(Guid Id, string Title, string? Description, DateTime? DueDate, int QuestionsCount);

public record GetPendingHomeworkQuery(Guid StudentId) : IRequest<ApiResponse<List<PendingHomeworkDto>>>;
