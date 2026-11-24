using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.Controllers;

public partial class TeacherController
{
    [HttpGet("lessons/{lessonId:guid}/cockpit")]
    public async Task<IActionResult> GetTeacherLessonCockpit(Guid lessonId, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Content)) return Forbid();
        if (!await OwnsContentAsync(context.TeacherId, "lesson", lessonId, ct)) return NotFound();

        var response = await _mediator.Send(new GetLessonCockpitQuery(lessonId, GetUserId()), ct);
        if (!response.Success || response.Data is null) return NotFound(response);
        return Ok(ApiResponse<LessonCockpitDto>.Ok(response.Data with
        {
            Videos = response.Data.Videos.Select(WithoutVideoSource).ToList()
        }));
    }

    private static LessonCockpitVideoDto WithoutVideoSource(LessonCockpitVideoDto video) => video with
    {
        Provider = string.Empty,
        Url = string.Empty,
        BunnyLibrary = null,
        BunnyEncodeProgress = null,
        PendingBunnyReplacement = null,
        LastBunnyReplacementOutcome = null,
        BunnyPlaybackMode = BunnyPlaybackMode.BunnyPlayer,
        YouTubeQualityEnabled = false,
        YouTubeHlsEnabled = false
    };

    [HttpPatch("lessons/{lessonId:guid}/title")]
    public async Task<IActionResult> RenameLesson(Guid lessonId, [FromBody] ContentTitleRequest request, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Content)) return Forbid();
        var lesson = await _db.Lessons.FirstOrDefaultAsync(
            lesson => lesson.Id == lessonId && lesson.ContentSection.Term.Package.TeacherId == context.TeacherId, ct);
        if (lesson is null) return NotFound();
        if (!ValidContentTitle(request.Title)) return BadRequest(ApiResponse.Fail("الاسم مطلوب ولا يزيد عن ٢٠٠ حرف."));

        var title = request.Title.Trim();
        if (lesson.Title == title) return Ok(ApiResponse.Ok());
        RecordTitleChange(new TitleChange(nameof(Lesson), lesson.Id, lesson.Title, title));
        lesson.Title = title;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPatch("videos/{videoId:guid}/title")]
    public async Task<IActionResult> RenameVideo(Guid videoId, [FromBody] ContentTitleRequest request, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Content)) return Forbid();
        var video = await _db.LessonVideos.FirstOrDefaultAsync(
            video => video.Id == videoId && video.Lesson.ContentSection.Term.Package.TeacherId == context.TeacherId, ct);
        if (video is null) return NotFound();
        if (!ValidContentTitle(request.Title)) return BadRequest(ApiResponse.Fail("الاسم مطلوب ولا يزيد عن ٢٠٠ حرف."));

        var title = request.Title.Trim();
        if (video.Title == title) return Ok(ApiResponse.Ok());
        RecordTitleChange(new TitleChange(nameof(LessonVideo), video.Id, video.Title, title));
        video.Title = title;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok());
    }

    private static bool ValidContentTitle(string? title) => !string.IsNullOrWhiteSpace(title) && title.Trim().Length <= 200;

    private sealed record TitleChange(string EntityType, Guid EntityId, string PreviousTitle, string Title);

    private void RecordTitleChange(TitleChange change) => _db.AuditLogs.Add(new AuditLog
    {
        Action = "TeacherContentTitleUpdated",
        EntityType = change.EntityType,
        EntityId = change.EntityId,
        PerformedByUserId = GetUserId(),
        OldValues = JsonSerializer.Serialize(new { Title = change.PreviousTitle }),
        NewValues = JsonSerializer.Serialize(new { change.Title })
    });

    public sealed record ContentTitleRequest([Required, StringLength(200)] string Title);
}
