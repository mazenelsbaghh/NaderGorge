using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/questions/explanation-audio")]
[HasPermission("exams.manage")]
public sealed class QuestionExplanationAudioController : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile audio,
        [FromServices] ISharedFileStorage sharedStorage,
        CancellationToken ct)
    {
        var extension = Path.GetExtension(audio.FileName).ToLowerInvariant();
        string[] allowedExtensions = [".mp3", ".wav", ".m4a", ".webm", ".ogg", ".aac", ".amr", ".flac"];
        if (audio.Length == 0 || !audio.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            || !allowedExtensions.Contains(extension))
            return BadRequest(ApiResponse.Fail("يجب اختيار ملف صوتي صالح للشرح."));
        var filename = $"{Guid.NewGuid():N}{extension}";
        await using var stream = audio.OpenReadStream();
        await sharedStorage.WriteAsync(SharedFileArea.Public, Path.Combine("uploads", "audio", filename), stream, ct);
        return Ok(ApiResponse<string>.Ok($"/uploads/audio/{filename}"));
    }
}
