using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.CenterDesktop;
using NaderGorge.Application.Common;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/center-desktop")]
[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[HttpLogging(HttpLoggingFields.None)]
public sealed class AdminCenterDesktopController(CenterDesktopSupportClient support) : ControllerBase
{
    [HttpGet("status")]
    public Task<IActionResult> Status(CancellationToken cancellationToken) => Json(() => support.StatusAsync(cancellationToken));

    [HttpGet("uploads")]
    public Task<IActionResult> Uploads([FromQuery] int limit = 50, [FromQuery] string? after = null,
        CancellationToken cancellationToken = default) => Json(() => support.UploadsAsync(limit, after, cancellationToken));

    [HttpGet("uploads/{id}/diagnostics")]
    public Task<IActionResult> Diagnostics(string id, [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default) => Json(() => support.DiagnosticsAsync(id, limit, cancellationToken));

    [HttpGet("releases")]
    public Task<IActionResult> Releases(CancellationToken cancellationToken) => Json(() => support.ReleasesAsync(cancellationToken));

    [HttpGet("uploads/{id}/download")]
    public async Task<IActionResult> Download(string id, CancellationToken cancellationToken)
    {
        try { return new DesktopBundleResult(await support.DownloadAsync(id, cancellationToken), id); }
        catch (DesktopSupportException error) { return StatusCode(error.StatusCode, ApiResponse.Fail(error.Message)); }
    }

    private async Task<IActionResult> Json<T>(Func<Task<T>> action)
    {
        try { return Ok(ApiResponse<T>.Ok(await action())); }
        catch (DesktopSupportException error) { return StatusCode(error.StatusCode, ApiResponse.Fail(error.Message)); }
    }

    private sealed class DesktopBundleResult(DesktopSupportDownload download, string id) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            using (download)
            {
                var response = context.HttpContext.Response;
                response.ContentType = "application/json";
                response.Headers.CacheControl = "no-store";
                response.Headers.ContentDisposition = $"attachment; filename=\"massar-desktop-{Guid.Parse(id):D}.json\"";
                response.Headers["X-Content-Type-Options"] = "nosniff";
                try { await download.CopyToAsync(response.Body, context.HttpContext.RequestAborted); }
                catch (Exception error) when (error is IOException or HttpRequestException or OperationCanceledException or DesktopSupportException)
                {
                    // A truncated private download is never replaced with JSON error text after streaming starts.
                    context.HttpContext.Abort();
                }
            }
        }
    }
}
