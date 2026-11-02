using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Interfaces;
using NaderGorge.Infrastructure.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

public sealed class StudentAssessmentAudioTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("Explain how audio travels through air.", false)]
    [InlineData("The URL /uploads/audio/example.mp3 is mentioned in this sentence.", false)]
    [InlineData("/uploads/audio/answer.webm", true)]
    [InlineData("  /UPLOADS/AUDIO/answer.mp3?x=1 ", true)]
    [InlineData("https://example.test/uploads/audio/answer.ogg", true)]
    [InlineData("https://example.test/uploads/%61udio/answer.ogg", true)]
    [InlineData("data:audio/wav;base64,AA==", true)]
    [InlineData("blob:https://example.test/answer", true)]
    public void RecordingReferencesAreBlockedWithoutRejectingOrdinaryText(string? answer, bool blocked) =>
        Assert.Equal(blocked, StudentAnswerPolicy.IsAudioReference(answer));

    [Fact]
    public void OldClientsCannotUploadStudentRecordings()
    {
        var response = Assert.IsType<ObjectResult>(new StudentController(null!).UploadStudentAudio());
        Assert.Equal(StatusCodes.Status410Gone, response.StatusCode);
    }

    [Theory]
    [InlineData("Student", false, false)]
    [InlineData("Teacher", false, false)]
    [InlineData("Teacher", true, true)]
    [InlineData("Admin", false, true)]
    public async Task ExplanationAudioRequiresExamManagementPermission(string role, bool permission, bool allowed)
    {
        await using var db = TestAppDbContextFactory.Create();
        var policy = Assert.Single(typeof(QuestionExplanationAudioController).GetCustomAttributes<HasPermissionAttribute>());
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (permission) claims.Add(new("permission", "exams.manage"));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
        await new PermissionFilter((string)policy.Arguments![0], db).OnAuthorizationAsync(context);
        if (allowed) Assert.Null(context.Result);
        else Assert.IsType<ForbidResult>(context.Result);
    }

    [Theory]
    [InlineData("explanation.mp3", true)]
    [InlineData("explanation.html", false)]
    public async Task TeacherExplanationUploadPreservesAudioAndRejectsNonAudioExtensions(string filename, bool accepted)
    {
        var root = Path.Combine(Path.GetTempPath(), "explanation-audio-" + Guid.NewGuid());
        try
        {
            var storage = new SharedFileStorage(new Dictionary<SharedFileArea, string> { [SharedFileArea.Public] = root });
            byte[] bytes = [0x49, 0x44, 0x33, 0x04, 0x00];
            using var stream = new MemoryStream(bytes);
            var audio = new FormFile(stream, 0, bytes.Length, "audio", filename) { Headers = new HeaderDictionary(), ContentType = "audio/mpeg" };
            var response = await new QuestionExplanationAudioController().Upload(audio, storage, default);
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            if (accepted)
            {
                Assert.IsType<OkObjectResult>(response);
                Assert.Equal(bytes, await File.ReadAllBytesAsync(Assert.Single(files)));
            }
            else
            {
                Assert.IsType<BadRequestObjectResult>(response);
                Assert.Empty(files);
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("audio")]
    [InlineData("text")]
    [InlineData("selectedText")]
    public async Task ExamRejectsRecordingsInEveryAnswerFieldWithoutSavingOtherAnswers(string field)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "exam-audio-disabled");
        var (exam, mcq, essay, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        const string audio = "/uploads/audio/answer.webm";
        var answer = new AnswerSubmissionDto(essay.Id, null, field == "text" ? audio : null,
            SelectedText: field == "selectedText" ? audio : null, AudioUrl: field == "audio" ? audio : null);
        var response = await new SubmitExamCommandHandler(db).Handle(new(exam.Id, attempt.Id, student.Id,
            [new(mcq.Id, correct.Id, null), answer]), default);
        Assert.False(response.Success);
        Assert.Contains("الإجابات الصوتية", response.Message);
        db.ChangeTracker.Clear();
        Assert.Null((await db.StudentExamAttempts.SingleAsync()).SubmittedAt);
        Assert.Empty(await db.EssaySubmissions.ToListAsync());
        Assert.Empty(await db.OutboxEvents.ToListAsync());
        Assert.All(await db.StudentAnswers.ToListAsync(), a =>
        { Assert.Null(a.SelectedOptionId); Assert.Null(a.SubmittedText); });
    }

    [Fact]
    public async Task AudioDraftCannotBeSavedOrSubmittedAndCanBeReplacedWithTypedAnswers()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "draft-audio-disabled");
        var (exam, _, essay, _, _, _, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        var scope = new ExamDraftScope(exam.Id, attempt.Id, student.Id, null);
        var answers = new Dictionary<Guid, string> { [essay.Id] = "/uploads/audio/legacy.webm" };
        var saver = new SaveExamDraftCommandHandler(db);
        Assert.False((await saver.Handle(new(scope, new(0, Guid.NewGuid(), answers)), default)).Success);
        Assert.Empty(await db.StudentExamDrafts.ToListAsync());
        db.StudentExamDrafts.Add(new StudentExamDraft { AttemptId = attempt.Id, RevisionId = Guid.Empty,
            Version = 1, AnswersJson = JsonSerializer.Serialize(answers) });
        await db.SaveChangesAsync();
        var submitter = new SubmitExamCommandHandler(db);
        var rejected = await submitter.Handle(new(exam.Id, attempt.Id, student.Id, [], DraftVersion: 1), default);
        Assert.False(rejected.Success);
        Assert.Contains("الإجابات الصوتية", rejected.Message);
        Assert.Empty(await db.EssaySubmissions.ToListAsync());
        Assert.Null((await db.StudentExamAttempts.SingleAsync()).SubmittedAt);
        var typed = new Dictionary<Guid, string> { [essay.Id] = "Gravity pulls objects together." };
        Assert.True((await saver.Handle(new(scope, new(1, Guid.NewGuid(), typed)), default)).Success);
        var submitted = await submitter.Handle(new(exam.Id, attempt.Id, student.Id, [], DraftVersion: 2), default);
        Assert.True(submitted.Success, submitted.Message);
        Assert.Equal(typed[essay.Id], (await db.EssaySubmissions.SingleAsync()).AnswerText);
    }
}
