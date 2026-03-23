using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateQuestionOptionDto(string Text, bool IsCorrect);

public record CreateQuestionCommand(string Text, decimal DefaultPoints, string Tags, List<CreateQuestionOptionDto> Options) : IRequest<ApiResponse<Guid>>;

public class CreateQuestionCommandHandler : IRequestHandler<CreateQuestionCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateQuestionCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateQuestionCommand request, CancellationToken ct)
    {
        if (request.Options == null || request.Options.Count < 2)
            return ApiResponse<Guid>.Fail("A question must have at least 2 options.");

        if (!request.Options.Any(o => o.IsCorrect))
            return ApiResponse<Guid>.Fail("At least one option must be marked as correct.");

        var question = new QuestionBankItem
        {
            Text = request.Text,
            DefaultPoints = request.DefaultPoints,
            Tags = request.Tags,
            Options = request.Options.Select(o => new QuestionOption
            {
                Text = o.Text,
                IsCorrect = o.IsCorrect
            }).ToList()
        };

        _db.QuestionBankItems.Add(question);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(question.Id);
    }
}
