using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public class CreateInlineExamCommand : IRequest<ApiResponse<Guid>>
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PassingScore { get; set; }
    public decimal TotalScore { get; set; }
    public int? DurationMinutes { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool IsRandomized { get; set; } = false;
    public int? DisplayQuestionCount { get; set; }
    public ExamTargetDto Target { get; set; } = new();
    public List<InlineExamQuestionDto> Questions { get; set; } = new();
}

public class ExamTargetDto
{
    public string Type { get; set; } = "Lesson"; // "Lesson" or "Video"
    public Guid Id { get; set; } // LessonId or LessonVideoId
}

public class InlineExamQuestionDto
{
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "MCQ"; // "MCQ", "Essay", or "FindTheMistake"
    public decimal Points { get; set; }
    public int Order { get; set; }
    public List<InlineExamOptionDto> Options { get; set; } = new();
    public string? AudioUrl { get; set; }
    public string? WrittenCorrection { get; set; }
    public string? HintText { get; set; }
    public string? BaseText { get; set; }
    public int? MistakeStartIndex { get; set; }
    public int? MistakeEndIndex { get; set; }
}

public class InlineExamOptionDto
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}

public class CreateInlineExamCommandHandler : IRequestHandler<CreateInlineExamCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateInlineExamCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateInlineExamCommand request, CancellationToken ct)
    {
        // 1. Validate Target
        Lesson? lesson = null;
        LessonVideo? video = null;

        if (request.Target.Type.Equals("Lesson", StringComparison.OrdinalIgnoreCase))
        {
            lesson = await _db.Lessons.FindAsync(new object[] { request.Target.Id }, ct);
            if (lesson == null) return ApiResponse<Guid>.Fail("Lesson not found");
        }
        else if (request.Target.Type.Equals("Video", StringComparison.OrdinalIgnoreCase))
        {
            video = await _db.LessonVideos.FindAsync(new object[] { request.Target.Id }, ct);
            if (video == null) return ApiResponse<Guid>.Fail("Video not found");
        }
        else
        {
            return ApiResponse<Guid>.Fail("Invalid target type");
        }

        if (request.PassingScore > request.TotalScore)
        {
            return ApiResponse<Guid>.Fail("Passing score cannot be greater than the total score.");
        }

        var exam = new Exam
        {
            Title = request.Title,
            Description = request.Description,
            PassingScore = request.PassingScore,
            TotalScore = request.TotalScore,
            DurationMinutes = request.DurationMinutes,
            IsMandatory = request.IsMandatory,
            IsRandomized = request.IsRandomized,
            DisplayQuestionCount = request.DisplayQuestionCount
        };

        _db.Exams.Add(exam);

        // 3. Process Questions
        foreach (var q in request.Questions)
        {
            var qType = q.Type switch
            {
                "Essay" => QuestionType.Essay,
                "FindTheMistake" => QuestionType.FindTheMistake,
                _ => QuestionType.MCQ
            };

            QuestionBankItem qbItem;

            if (qType == QuestionType.FindTheMistake)
            {
                qbItem = new FindTheMistakeQuestion
                {
                    Text = q.Text ?? "اكتشف الغلطة",
                    Type = qType,
                    DefaultPoints = q.Points,
                    Tags = "Inline",
                    AudioUrl = q.AudioUrl,
                    WrittenCorrection = q.WrittenCorrection,
                    HintText = q.HintText,
                    BaseText = q.BaseText ?? string.Empty,
                    MistakeStartIndex = q.MistakeStartIndex ?? 0,
                    MistakeEndIndex = q.MistakeEndIndex ?? 0,
                    Options = q.Options?.Select(o => new QuestionOption { Text = o.Text, IsCorrect = o.IsCorrect }).ToList() ?? new List<QuestionOption>()
                };
            }
            else
            {
                if (qType == QuestionType.Essay)
                {
                    qbItem = new EssayQuestion
                    {
                        Text = q.Text ?? string.Empty,
                        Type = qType,
                        DefaultPoints = q.Points,
                        Tags = "Inline",
                        AudioUrl = q.AudioUrl,
                        WrittenCorrection = q.WrittenCorrection,
                        HintText = q.HintText,
                    };
                }
                else
                {
                    qbItem = new QuestionBankItem
                    {
                        Text = q.Text ?? string.Empty,
                        Type = qType,
                        DefaultPoints = q.Points,
                        Tags = "Inline",
                        AudioUrl = q.AudioUrl,
                        WrittenCorrection = q.WrittenCorrection,
                        HintText = q.HintText,
                    };
                }
            }

            _db.QuestionBankItems.Add(qbItem);

            if (qType == QuestionType.MCQ)
            {
                foreach (var opt in q.Options ?? new List<InlineExamOptionDto>())
                {
                    _db.QuestionOptions.Add(new QuestionOption
                    {
                        Text = opt.Text,
                        IsCorrect = opt.IsCorrect,
                        QuestionBankItemId = qbItem.Id,
                        Question = qbItem
                    });
                }
            }

            var eq = new ExamQuestion
            {
                ExamId = exam.Id,
                Exam = exam,
                QuestionBankItemId = qbItem.Id,
                Question = qbItem,
                Order = q.Order,
                Points = q.Points
            };

            _db.ExamQuestions.Add(eq);
        }

        // 4. Link to Target
        if (lesson != null)
        {
            lesson.ExamId = exam.Id;
        }
        else if (video != null)
        {
            video.ExamId = exam.Id;
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(exam.Id);
    }
}

public class AddQuestionsToExamCommand : IRequest<ApiResponse<Guid>>
{
    public Guid ExamId { get; set; }
    public List<InlineExamQuestionDto> Questions { get; set; } = new();
}

public class AddQuestionsToExamCommandHandler : IRequestHandler<AddQuestionsToExamCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public AddQuestionsToExamCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(AddQuestionsToExamCommand request, CancellationToken ct)
    {
        var exam = await _db.Exams.FindAsync(new object[] { request.ExamId }, ct);
        if (exam == null) return ApiResponse<Guid>.Fail("Exam not found");

        foreach (var q in request.Questions)
        {
            var qType = q.Type switch
            {
                "Essay" => QuestionType.Essay,
                "FindTheMistake" => QuestionType.FindTheMistake,
                _ => QuestionType.MCQ
            };

            QuestionBankItem qbItem;

            if (qType == QuestionType.FindTheMistake)
            {
                qbItem = new FindTheMistakeQuestion
                {
                    Text = q.Text ?? "اكتشف الغلطة",
                    Type = qType,
                    DefaultPoints = q.Points,
                    Tags = "Added",
                    AudioUrl = q.AudioUrl,
                    WrittenCorrection = q.WrittenCorrection,
                    HintText = q.HintText,
                    BaseText = q.BaseText ?? string.Empty,
                    MistakeStartIndex = q.MistakeStartIndex ?? 0,
                    MistakeEndIndex = q.MistakeEndIndex ?? 0,
                    Options = q.Options?.Select(o => new QuestionOption { Text = o.Text, IsCorrect = o.IsCorrect }).ToList() ?? new List<QuestionOption>()
                };
            }
            else
            {
                if (qType == QuestionType.Essay)
                {
                    qbItem = new EssayQuestion
                    {
                        Text = q.Text ?? string.Empty,
                        Type = qType,
                        DefaultPoints = q.Points,
                        Tags = "Added",
                        AudioUrl = q.AudioUrl,
                        WrittenCorrection = q.WrittenCorrection,
                        HintText = q.HintText,
                    };
                }
                else
                {
                    qbItem = new QuestionBankItem
                    {
                        Text = q.Text ?? string.Empty,
                        Type = qType,
                        DefaultPoints = q.Points,
                        Tags = "Added",
                        AudioUrl = q.AudioUrl,
                        WrittenCorrection = q.WrittenCorrection,
                        HintText = q.HintText,
                    };
                }
            }

            _db.QuestionBankItems.Add(qbItem);

            if (qType == QuestionType.MCQ)
            {
                foreach (var opt in q.Options ?? new List<InlineExamOptionDto>())
                {
                    _db.QuestionOptions.Add(new QuestionOption
                    {
                        Text = opt.Text,
                        IsCorrect = opt.IsCorrect,
                        QuestionBankItemId = qbItem.Id,
                        Question = qbItem
                    });
                }
            }

            _db.ExamQuestions.Add(new ExamQuestion
            {
                ExamId = exam.Id,
                Exam = exam,
                QuestionBankItemId = qbItem.Id,
                Question = qbItem,
                Order = q.Order,
                Points = q.Points
            });
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(exam.Id);
    }
}

public record DeleteExamQuestionCommand(Guid ExamId, Guid ExamQuestionId) : IRequest<ApiResponse<bool>>;

public class DeleteExamQuestionCommandHandler : IRequestHandler<DeleteExamQuestionCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public DeleteExamQuestionCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<bool>> Handle(DeleteExamQuestionCommand request, CancellationToken ct)
    {
        var examQuestion = await _db.ExamQuestions
            .Include(eq => eq.Question)
            .FirstOrDefaultAsync(eq => eq.Id == request.ExamQuestionId && eq.ExamId == request.ExamId, ct);

        if (examQuestion == null)
            return ApiResponse<bool>.Fail("السؤال غير موجود في هذا الامتحان.");

        var questionBankItem = examQuestion.Question;

        _db.ExamQuestions.Remove(examQuestion);
        _db.QuestionBankItems.Remove(questionBankItem);

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}
