using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateQuestionOptionDto(string Text, bool IsCorrect);

public record CreateQuestionCommand(string Text, QuestionType Type, decimal DefaultPoints, string Tags, string? AudioUrl, string? WrittenCorrection, string? HintText, List<CreateQuestionOptionDto> Options, string? BaseText = null, int? MistakeStartIndex = null, int? MistakeEndIndex = null) : IRequest<ApiResponse<Guid>>;

public class CreateQuestionCommandHandler : IRequestHandler<CreateQuestionCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateQuestionCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateQuestionCommand request, CancellationToken ct)
    {
        if (request.Type != QuestionType.Essay && (request.Options == null || request.Options.Count < 2))
            return ApiResponse<Guid>.Fail("MCQ questions must have at least 2 options.");

        if (request.Type == QuestionType.MCQ && !request.Options.Any(o => o.IsCorrect))
            return ApiResponse<Guid>.Fail("At least one option must be marked as correct.");

        QuestionBankItem question;

        if (request.Type == QuestionType.FindTheMistake)
        {
            question = new FindTheMistakeQuestion
            {
                Text = request.Text ?? "Find the Mistake",
                Type = request.Type,
                DefaultPoints = request.DefaultPoints,
                Tags = request.Tags,
                AudioUrl = request.AudioUrl,
                WrittenCorrection = request.WrittenCorrection,
                HintText = request.HintText,
                BaseText = request.BaseText ?? string.Empty,
                MistakeStartIndex = request.MistakeStartIndex ?? 0,
                MistakeEndIndex = request.MistakeEndIndex ?? 0,
                Options = request.Options?.Select(o => new QuestionOption { Text = o.Text, IsCorrect = o.IsCorrect }).ToList() ?? new List<QuestionOption>()
            };
        }
        else
        {
            if (request.Type == QuestionType.Essay)
            {
                question = new EssayQuestion
                {
                    Text = request.Text,
                    Type = request.Type,
                    DefaultPoints = request.DefaultPoints,
                    Tags = request.Tags,
                    AudioUrl = request.AudioUrl,
                    WrittenCorrection = request.WrittenCorrection,
                    HintText = request.HintText,
                    Options = request.Options?.Select(o => new QuestionOption
                    {
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    }).ToList() ?? new List<QuestionOption>()
                };
            }
            else
            {
                question = new QuestionBankItem
                {
                    Text = request.Text,
                    Type = request.Type,
                    DefaultPoints = request.DefaultPoints,
                    Tags = request.Tags,
                    AudioUrl = request.AudioUrl,
                    WrittenCorrection = request.WrittenCorrection,
                    HintText = request.HintText,
                    Options = request.Options?.Select(o => new QuestionOption
                    {
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    }).ToList() ?? new List<QuestionOption>()
                };
            }
        }

        _db.QuestionBankItems.Add(question);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(question.Id);
    }
}

public record UploadQuestionAudioCommand(Guid QuestionId, string Base64Audio, string FileName) : IRequest<ApiResponse<string>>;

public class UploadQuestionAudioCommandHandler : IRequestHandler<UploadQuestionAudioCommand, ApiResponse<string>>
{
    private readonly IAppDbContext _db;

    public UploadQuestionAudioCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<string>> Handle(UploadQuestionAudioCommand request, CancellationToken ct)
    {
        var question = await _db.QuestionBankItems.FindAsync(new object[] { request.QuestionId }, ct);
        if (question == null) return ApiResponse<string>.Fail("Question not found.");

        // NOTE: In production, upload Base64Audio to S3/Cloud Storage and return the URL.
        // For now, we simulate by generating a fake URL or saving locally.
        var audioUrl = $"/uploads/audio/{Guid.NewGuid()}_{request.FileName}";
        
        question.AudioUrl = audioUrl;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<string>.Ok(audioUrl);
    }
}
