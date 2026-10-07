using System.Net.Http.Json;
using NaderGorge.Application.Features.MimStudio;
using Microsoft.Extensions.Configuration;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public sealed record MimWritingSource(Guid? Id, string Title, int Revision, MimWritingChapter[] Chapters, string? Text);
public sealed record MimWritingChapter(Guid Id, string Title, string Summary);
public sealed record MimWritingContext(string LessonTitle, MimWritingSource Source, MimStudioDocument? PreviousScenes, MimScene? PreviousLessonOpening);
public sealed class MimSceneWriter(HttpClient client, IConfiguration configuration)
{
    public async Task<MimStudioDocument> WriteAsync(MimWritingContext context, CancellationToken ct)
    {
        var url = configuration["WORKER_URL"]?.TrimEnd('/');
        var token = configuration["WORKER_ADMIN_TOKEN"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            throw new MimStudioGenerationException("خدمة كتابة المشاهد غير مهيأة على السيرفر.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/internal/mim-studio/scene")
        { Content = JsonContent.Create(context) };
        request.Headers.Authorization = new("Bearer", token);
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new MimStudioGenerationException("تعذر كتابة المشهد. المشاهد المحفوظة لم تتغير؛ أعد المحاولة.");
            return await response.Content.ReadFromJsonAsync<MimStudioDocument>(cancellationToken: ct)
                ?? throw new MimStudioGenerationException("لم تصل بيانات المشهد كاملة.");
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException || e is TaskCanceledException && !ct.IsCancellationRequested)
        { throw new MimStudioGenerationException("انتهت مهلة كتابة المشهد. حدّث حالة الاستوديو قبل المحاولة مرة أخرى."); }
    }
}
public sealed class MimStudioGenerationException(string message) : Exception(message);
