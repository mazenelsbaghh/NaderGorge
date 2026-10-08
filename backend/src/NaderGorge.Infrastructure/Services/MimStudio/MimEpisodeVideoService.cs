using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.MimStudio;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public sealed record MimEpisodeVideoView(string State, int Progress, int SceneCount, int Duration, string? Error = null);
public sealed record MimEpisodeApproval(Guid Version);
internal sealed record MimEpisodeInputs(string Id, string[] Urls);
internal sealed record MimEditStatus(string State, int Progress, string? Error);

public sealed class MimEpisodeVideoService(AppDbContext db, LessonMimStudioService studio, HttpClient client, IConfiguration configuration)
{
    public async Task<MimEpisodeVideoView> ReadAsync(Guid actor, Guid lesson, CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(lesson, ct);
        var inputs = await InputsAsync(actor, snapshot, lesson, ct);
        if (inputs is null) return new("waiting", 0, snapshot.Document.TargetSceneCount, snapshot.Document.TargetSceneCount * 30,
            "كمّل كتابة العدد المطلوب وولّد كل فيديوهاته، ثم حدّث حالة الحلقة لبدء المونتاج.");
        var status = await StatusAsync(inputs, ct);
        return View(status, inputs.Urls.Length);
    }

    public async Task<MimEpisodeVideoView> AssembleAsync(Guid actor, Guid lesson, MimEpisodeApproval approval, CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(lesson, ct);
        if (snapshot.Version != approval.Version) throw new MimStudioConflictException("الاسكربت اتغيّر. حدّث الحلقة قبل بدء المونتاج.");
        var inputs = await InputsAsync(actor, snapshot, lesson, ct) ?? throw new ArgumentException("كمّل كل مشاهد الحلقة وفيديوهاتها أولًا.");
        using var request = Request(HttpMethod.Post, "/internal/mim-studio/episodes");
        request.Content = JsonContent.Create(new { inputs.Id, inputs.Urls });
        var status = await ReplyAsync(request, ct);
        return View(status, inputs.Urls.Length);
    }

    public async Task<HttpResponseMessage> FileAsync(Guid actor, Guid lesson, string? range, CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(lesson, ct);
        var inputs = await InputsAsync(actor, snapshot, lesson, ct) ?? throw new ArgumentException("فيديو الحلقة غير جاهز.");
        if ((await StatusAsync(inputs, ct)).State != "completed") throw new MimStudioConflictException("المونتاج لسه ما اكتملش.");
        using var request = Request(HttpMethod.Get, $"/internal/mim-studio/episodes/{inputs.Id}/file");
        if (!string.IsNullOrEmpty(range)) request.Headers.TryAddWithoutValidation("Range", range);
        HttpResponseMessage response;
        try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException && !ct.IsCancellationRequested)
        { throw new MimStudioGenerationException("تعذر تحميل فيديو الحلقة. حدّث الحالة وأعد المحاولة."); }
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new MimStudioGenerationException("تعذر تحميل فيديو الحلقة. حدّث الحالة وأعد المحاولة.");
        }
        return response;
    }

    private async Task<MimStudioSnapshot> SnapshotAsync(Guid lesson, CancellationToken ct)
    {
        var snapshot = await studio.ReadAsync(lesson, ct) ?? throw new ArgumentException("اكتب الحلقة واحفظها أولًا.");
        if (snapshot.Stale || snapshot.Generating) throw new MimStudioConflictException("راجع مصدر الحلقة وانتظر اكتمال الكتابة قبل المونتاج.");
        return snapshot;
    }

    private async Task<MimEpisodeInputs?> InputsAsync(Guid actor, MimStudioSnapshot snapshot, Guid lesson, CancellationToken ct)
    {
        var count = snapshot.Document.TargetSceneCount;
        if (snapshot.Document.Scenes.Length != count) return null;
        var rows = await db.Set<MimSceneVideo>().AsNoTracking().Where(row => row.LessonId == lesson && row.SceneIndex >= 0 && row.SceneIndex < count)
            .OrderBy(row => row.SceneIndex).ToArrayAsync(ct);
        if (rows.Any(row => row.AdminUserId != actor)) throw new ArgumentException("فيديوهات الحلقة مرتبطة بحساب إدارة آخر.");
        if (rows.Length != count || rows.Any(row => row.State != "completed" || MimVideoOutcome.Urls(row.ResultJson).Length != 1)) return null;
        foreach (var row in rows)
        {
            using var parameters = JsonDocument.Parse(row.ParametersJson);
            var prompt = parameters.RootElement.GetProperty("prompt").GetString() ?? "";
            if (prompt != MimVideoPrompt.Build(snapshot.Document, row.SceneIndex))
                throw new MimStudioConflictException("حوار أو حركة أحد المشاهد اتغيّر بعد توليد فيديوه. راجع الفيديو والاسكربت قبل المونتاج.");
        }
        var urls = rows.Select(row => MimVideoOutcome.Urls(row.ResultJson)[0]).ToArray();
        // Authoritative SQL inputs define an immutable edit. Redis only coordinates rendering;
        // the private, atomically published file survives queue loss and duplicate requests.
        var identity = JsonSerializer.Serialize(new { actor, lesson, snapshot.Version, clips = rows.Select(row => new { row.Id, row.Version }), urls });
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        return new(id, urls);
    }

    private async Task<MimEditStatus> StatusAsync(MimEpisodeInputs inputs, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, $"/internal/mim-studio/episodes/{inputs.Id}");
        return await ReplyAsync(request, ct);
    }
    private async Task<MimEditStatus> ReplyAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new MimStudioGenerationException("خدمة المونتاج غير متاحة حاليًا. حدّث الحالة قبل المحاولة.");
            return await response.Content.ReadFromJsonAsync<MimEditStatus>(cancellationToken: ct)
                ?? throw new MimStudioGenerationException("لم تصل حالة المونتاج كاملة.");
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is TaskCanceledException && !ct.IsCancellationRequested)
        { throw new MimStudioGenerationException("تعذر التأكد من حالة المونتاج. حدّث الحالة قبل إعادة المحاولة."); }
    }
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var url = configuration["WORKER_URL"]?.TrimEnd('/');
        var token = configuration["WORKER_ADMIN_TOKEN"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token)) throw new MimStudioGenerationException("خدمة المونتاج غير مهيأة على السيرفر.");
        var request = new HttpRequestMessage(method, url + path);
        request.Headers.Authorization = new("Bearer", token);
        return request;
    }
    private static MimEpisodeVideoView View(MimEditStatus status, int count) => new(status.State, status.Progress, count, count * 30, status.Error);
}
