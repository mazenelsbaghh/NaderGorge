using MediatR;
using NaderGorge.Application.Common;

namespace NaderGorge.Application.Features.Homework.Commands;

public record StudentAnswerInput(Guid QuestionId, string ProvidedAnswer);

public record SubmitHomeworkCommand(Guid HomeworkId, Guid StudentId, List<StudentAnswerInput> Answers) : IRequest<ApiResponse<bool>>;
