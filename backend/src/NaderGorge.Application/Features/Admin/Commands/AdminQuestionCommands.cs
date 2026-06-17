using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateQuestionOptionDto(string Text, bool IsCorrect);

public record CreateQuestionCommand(
    string Text, 
    QuestionType Type, 
    decimal DefaultPoints, 
    string Tags, 
    string? AudioUrl, 
    string? ImageUrl,
    string? WrittenCorrection, 
    string? HintText, 
    List<CreateQuestionOptionDto> Options, 
    string? BaseText = null, 
    int? MistakeStartIndex = null, 
    int? MistakeEndIndex = null,
    Guid? SubjectId = null,
    Guid? TeacherId = null,
    Guid? CurrentUserId = null) : IRequest<ApiResponse<Guid>>;

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

        var subjectId = request.SubjectId ?? Guid.Empty;
        if (subjectId == Guid.Empty)
        {
            return ApiResponse<Guid>.Fail("Subject is required.");
        }

        var subjectExists = await _db.Subjects.AnyAsync(s => s.Id == subjectId, ct);
        if (!subjectExists)
        {
            return ApiResponse<Guid>.Fail("Subject not found.");
        }

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
            if (!request.TeacherId.HasValue || request.TeacherId.Value == Guid.Empty)
            {
                return ApiResponse<Guid>.Fail("Teacher is required.");
            }

            var teacherExists = await _db.TeacherProfiles.AnyAsync(tp => tp.Id == request.TeacherId.Value, ct);
            if (!teacherExists)
                return ApiResponse<Guid>.Fail("Selected teacher not found.");

            teacherId = request.TeacherId.Value;
        }

        var teachesSubject = await _db.TeacherSubjects.AnyAsync(ts => ts.TeacherId == teacherId && ts.SubjectId == subjectId, ct);
        if (!teachesSubject)
        {
            return ApiResponse<Guid>.Fail("Selected teacher does not teach this subject.");
        }

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
                ImageUrl = request.ImageUrl,
                WrittenCorrection = request.WrittenCorrection,
                HintText = request.HintText,
                BaseText = request.BaseText ?? string.Empty,
                MistakeStartIndex = request.MistakeStartIndex ?? 0,
                MistakeEndIndex = request.MistakeEndIndex ?? 0,
                Options = request.Options?.Select(o => new QuestionOption { Text = o.Text, IsCorrect = o.IsCorrect }).ToList() ?? new List<QuestionOption>(),
                CreatedByTeacherId = teacherId,
                SubjectId = subjectId
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
                    ImageUrl = request.ImageUrl,
                    WrittenCorrection = request.WrittenCorrection,
                    HintText = request.HintText,
                    Options = request.Options?.Select(o => new QuestionOption
                    {
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    }).ToList() ?? new List<QuestionOption>(),
                    CreatedByTeacherId = teacherId,
                    SubjectId = subjectId
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
                    ImageUrl = request.ImageUrl,
                    WrittenCorrection = request.WrittenCorrection,
                    HintText = request.HintText,
                    Options = request.Options?.Select(o => new QuestionOption
                    {
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    }).ToList() ?? new List<QuestionOption>(),
                    CreatedByTeacherId = teacherId,
                    SubjectId = subjectId
                };
            }
        }

        _db.QuestionBankItems.Add(question);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(question.Id);
    }
}

public record UploadQuestionAudioCommand(Guid QuestionId, string Base64Audio, string FileName, Guid? CurrentUserId = null) : IRequest<ApiResponse<string>>;

public class UploadQuestionAudioCommandHandler : IRequestHandler<UploadQuestionAudioCommand, ApiResponse<string>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public UploadQuestionAudioCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<string>> Handle(UploadQuestionAudioCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessQuestionAsync(request.CurrentUserId.Value, request.QuestionId, ct);
            if (!canAccess) return ApiResponse<string>.Fail("Unauthorized access to this question.");
        }

        var question = await _db.QuestionBankItems.FindAsync(new object[] { request.QuestionId }, ct);
        if (question == null) return ApiResponse<string>.Fail("Question not found.");

        var extension = System.IO.Path.GetExtension(request.FileName)?.ToLowerInvariant() ?? "";
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var audioUrl = $"/uploads/audio/{uniqueFileName}";

        var uploadsFolder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audio");
        System.IO.Directory.CreateDirectory(uploadsFolder);

        var physicalPath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);
        var bytes = Convert.FromBase64String(request.Base64Audio);
        await System.IO.File.WriteAllBytesAsync(physicalPath, bytes, ct);

        question.AudioUrl = audioUrl;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<string>.Ok(audioUrl);
    }
}
