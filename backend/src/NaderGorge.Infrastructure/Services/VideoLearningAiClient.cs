using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.VideoLearning;

namespace NaderGorge.Infrastructure.Services;

public sealed class VideoLearningAiClient(HttpClient client, IConfiguration configuration) : IVideoLearningAi
{
    public async Task<LearningAiResult> GenerateAsync(string mode, string question, string context, CancellationToken ct)
    {
        var url = configuration["WORKER_URL"]?.TrimEnd('/');
        var token = configuration["WORKER_ADMIN_TOKEN"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("AI_NOT_CONFIGURED");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/internal/video-learning")
        { Content = JsonContent.Create(new { mode, question, context }) };
        request.Headers.Authorization = new("Bearer", token);
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("AI_UNAVAILABLE");
        return await response.Content.ReadFromJsonAsync<LearningAiResult>(cancellationToken: ct) ?? throw new InvalidOperationException("AI_INVALID_RESPONSE");
    }
}
