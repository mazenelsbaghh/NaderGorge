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
    public int? TimePerQuestionSeconds { get; set; }
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
    public string Type { get; set; } = "MCQ"; // "MCQ" or "Essay"
    public decimal Points { get; set; }
    public int Order { get; set; }
    public List<InlineExamOptionDto> Options { get; set; } = new();
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
            TimePerQuestionSeconds = request.TimePerQuestionSeconds
        };

        _db.Exams.Add(exam);

        // 3. Process Questions
        foreach (var q in request.Questions)
        {
            var qType = q.Type.Equals("Essay", StringComparison.OrdinalIgnoreCase) ? QuestionType.Essay : QuestionType.MCQ;

            var qbItem = new QuestionBankItem
            {
                Text = q.Text,
                Type = qType,
                DefaultPoints = q.Points,
                Tags = "Inline"
            };

            _db.QuestionBankItems.Add(qbItem);

            if (qType == QuestionType.MCQ)
            {
                foreach (var opt in q.Options)
                {
                    var optEntity = new QuestionOption
                    {
                        Text = opt.Text,
                        IsCorrect = opt.IsCorrect,
                        QuestionBankItemId = qbItem.Id,
                        Question = qbItem
                    };
                    _db.QuestionOptions.Add(optEntity);
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
            var qType = q.Type.Equals("Essay", StringComparison.OrdinalIgnoreCase) ? QuestionType.Essay : QuestionType.MCQ;

            var qbItem = new QuestionBankItem
            {
                Text = q.Text,
                Type = qType,
                DefaultPoints = q.Points,
                Tags = "Added"
            };

            _db.QuestionBankItems.Add(qbItem);

            if (qType == QuestionType.MCQ)
            {
                foreach (var opt in q.Options)
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
