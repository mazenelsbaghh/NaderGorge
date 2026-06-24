using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed class LiveSupportAIWorkerPreviewClient(HttpClient httpClient, IConfiguration configuration)
    : ILiveSupportAIWorkerPreviewClient
{
    public async Task<LiveSupportAIWorkerPreviewResultDto> PreviewAsync(
        LiveSupportAIWorkerClaimDto context,
        CancellationToken cancellationToken)
    {
        var workerUrl = configuration["WORKER_URL"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("AI_WORKER_NOT_CONFIGURED");
        var token = configuration["WORKER_ADMIN_TOKEN"]
            ?? throw new InvalidOperationException("AI_WORKER_NOT_CONFIGURED");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{workerUrl}/internal/live-support/preview")
        {
            Content = JsonContent.Create(context)
        };
        request.Headers.Authorization = new("Bearer", token);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity
                ? "AI_PREVIEW_DECISION_INVALID"
                : "AI_PREVIEW_UNAVAILABLE");

        return await response.Content.ReadFromJsonAsync<LiveSupportAIWorkerPreviewResultDto>(
                   new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
               ?? throw new InvalidOperationException("AI_PREVIEW_INVALID_RESPONSE");
    }
}
