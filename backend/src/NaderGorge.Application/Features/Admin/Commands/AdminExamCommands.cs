using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
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
    public Guid? CurrentUserId { get; set; }
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
    private readonly TeacherAuthorizationService _auth;

    public CreateInlineExamCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateInlineExamCommand request, CancellationToken ct)
    {
        // 1. Validate Target
        Lesson? lesson = null;
        LessonVideo? video = null;

        if (request.Target.Type.Equals("Lesson", StringComparison.OrdinalIgnoreCase))
        {
            lesson = await _db.Lessons.FindAsync(new object[] { request.Target.Id }, ct);
            if (lesson == null) return ApiResponse<Guid>.Fail("Lesson not found");

            if (request.CurrentUserId.HasValue)
            {
                var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, lesson.Id, ct);
                if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this lesson.");
            }
        }
        else if (request.Target.Type.Equals("Video", StringComparison.OrdinalIgnoreCase))
        {
            video = await _db.LessonVideos.FindAsync(new object[] { request.Target.Id }, ct);
            if (video == null) return ApiResponse<Guid>.Fail("Video not found");

            if (request.CurrentUserId.HasValue)
            {
                var canAccess = await _auth.CanAccessLessonAsync(request.CurrentUserId.Value, video.LessonId, ct);
                if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this video's lesson.");
            }
        }
        else
        {
            return ApiResponse<Guid>.Fail("Invalid target type");
        }

        if (request.PassingScore > request.TotalScore)
        {
            return ApiResponse<Guid>.Fail("Passing score cannot be greater than the total score.");
        }

        // 2. Resolve Teacher and Subject Context
        var teacherId = Guid.Empty;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, ct);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                if (user.TeacherProfile == null)
                    return ApiResponse<Guid>.Fail("Teacher profile not onboarded.");
                teacherId = user.TeacherProfile.Id;
            }
        }

        if (teacherId == Guid.Empty)
        {
            var targetLessonId = lesson?.Id ?? video?.LessonId;
            if (targetLessonId.HasValue)
            {
                var targetLesson = await _db.Lessons
                    .Include(l => l.ContentSection)
                        .ThenInclude(s => s.Term)
                            .ThenInclude(t => t.Package)
                    .FirstOrDefaultAsync(l => l.Id == targetLessonId.Value, ct);
                if (targetLesson?.ContentSection?.Term?.Package != null)
                {
                    teacherId = targetLesson.ContentSection.Term.Package.TeacherId;
                }
            }
        }

        if (teacherId == Guid.Empty)
        {
            return ApiResponse<Guid>.Fail("Could not resolve teacher context for the exam.");
        }

        var subjectId = Guid.Empty;
        var targetLessonIdForSubject = lesson?.Id ?? video?.LessonId;
        if (targetLessonIdForSubject.HasValue)
        {
            var targetLesson = await _db.Lessons
                .Include(l => l.ContentSection)
                    .ThenInclude(s => s.Term)
                        .ThenInclude(t => t.Package)
                .FirstOrDefaultAsync(l => l.Id == targetLessonIdForSubject.Value, ct);
            if (targetLesson?.ContentSection?.Term?.Package != null)
            {
                subjectId = targetLesson.ContentSection.Term.Package.SubjectId;
            }
        }

        if (subjectId == Guid.Empty)
        {
            var firstSubject = await _db.Subjects.FirstOrDefaultAsync(ct);
            if (firstSubject != null)
            {
                subjectId = firstSubject.Id;
            }
        }

        if (subjectId == Guid.Empty)
        {
            return ApiResponse<Guid>.Fail("Could not resolve subject context for the exam.");
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
            DisplayQuestionCount = request.DisplayQuestionCount,
            CreatedByTeacherId = teacherId
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
                    Options = q.Options?.Select(o => new QuestionOption { Text = o.Text, IsCorrect = o.IsCorrect }).ToList() ?? new List<QuestionOption>(),
                    CreatedByTeacherId = teacherId,
                    SubjectId = subjectId
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
                        CreatedByTeacherId = teacherId,
                        SubjectId = subjectId
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
                        CreatedByTeacherId = teacherId,
                        SubjectId = subjectId
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
    public Guid? CurrentUserId { get; set; }
}

public class AddQuestionsToExamCommandHandler : IRequestHandler<AddQuestionsToExamCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public AddQuestionsToExamCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<Guid>> Handle(AddQuestionsToExamCommand request, CancellationToken ct)
    {
        var exam = await _db.Exams.FindAsync(new object[] { request.ExamId }, ct);
        if (exam == null) return ApiResponse<Guid>.Fail("Exam not found");

        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessExamAsync(request.CurrentUserId.Value, request.ExamId, ct);
            if (!canAccess) return ApiResponse<Guid>.Fail("Unauthorized access to this exam.");
        }

        var teacherId = exam.CreatedByTeacherId;
        
        var subjectId = Guid.Empty;
        var referencingLesson = await _db.Lessons
            .Include(l => l.ContentSection)
                .ThenInclude(s => s.Term)
                    .ThenInclude(t => t.Package)
            .FirstOrDefaultAsync(l => l.ExamId == exam.Id, ct);

        if (referencingLesson?.ContentSection?.Term?.Package != null)
        {
            subjectId = referencingLesson.ContentSection.Term.Package.SubjectId;
        }
        else
        {
            var referencingVideo = await _db.LessonVideos
                .Include(v => v.Lesson)
                    .ThenInclude(l => l.ContentSection)
                        .ThenInclude(s => s.Term)
                            .ThenInclude(t => t.Package)
                .FirstOrDefaultAsync(v => v.ExamId == exam.Id, ct);

            if (referencingVideo?.Lesson?.ContentSection?.Term?.Package != null)
            {
                subjectId = referencingVideo.Lesson.ContentSection.Term.Package.SubjectId;
            }
        }

        if (subjectId == Guid.Empty)
        {
            var firstSubject = await _db.Subjects.FirstOrDefaultAsync(ct);
            if (firstSubject != null)
            {
                subjectId = firstSubject.Id;
            }
        }

        if (subjectId == Guid.Empty)
        {
            return ApiResponse<Guid>.Fail("Could not resolve subject context for adding questions.");
        }

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
                    Options = q.Options?.Select(o => new QuestionOption { Text = o.Text, IsCorrect = o.IsCorrect }).ToList() ?? new List<QuestionOption>(),
                    CreatedByTeacherId = teacherId,
                    SubjectId = subjectId
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
                        CreatedByTeacherId = teacherId,
                        SubjectId = subjectId
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
                        CreatedByTeacherId = teacherId,
                        SubjectId = subjectId
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

public record DeleteExamQuestionCommand(Guid ExamId, Guid ExamQuestionId, Guid? CurrentUserId = null) : IRequest<ApiResponse<bool>>;

public class DeleteExamQuestionCommandHandler : IRequestHandler<DeleteExamQuestionCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public DeleteExamQuestionCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteExamQuestionCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessExamAsync(request.CurrentUserId.Value, request.ExamId, ct);
            if (!canAccess) return ApiResponse<bool>.Fail("Unauthorized access to this exam.");
        }

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

