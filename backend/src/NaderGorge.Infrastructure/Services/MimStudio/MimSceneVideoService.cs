using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.MimStudio;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public sealed record MimVideoView(Guid Version, string State, string Quote, DateTime ExpiresAt, Guid? JobId, string[] Urls, string Model);
public sealed record MimVideoApproval(Guid Version);

public sealed class MimSceneVideoService(AppDbContext db, LessonMimStudioService studio, HiggsfieldMcpConnectionService connection)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ReferenceUrls = [
        "https://admin.massar-academy.net/mim-studio/meem-character-sheet.png",
        "https://admin.massar-academy.net/mim-studio/papa-nader-character-sheet.png"
    ];

    public async Task<MimVideoView?> ReadAsync(Guid actor, Guid lesson, int scene, CancellationToken ct)
    {
        var row = await FindAsync(actor, lesson, scene, ct);
        if (row?.JobId is not null && row.State is "running" or "submitting" or "unknown")
        {
            var reply = await connection.CallStudioToolAsync(actor, "job_status", new { jobId = row.JobId }, ct);
            var payload = HiggsfieldStudioReply.Payload(reply);
            var result = HiggsfieldStudioReply.Job(payload);
            row.State = result.State;
            row.ResultJson = JsonSerializer.Serialize(result.Urls);
            row.Version = Guid.NewGuid();
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return row is null ? null : View(row);
    }

    public async Task<MimVideoView> QuoteAsync(Guid actor, MimVideoQuoteTarget target, CancellationToken ct)
    {
        var (lesson, scene, model) = target;
        MimVideoModels.Require(model);
        var script = await CurrentScriptAsync(lesson, scene, ct);
        var row = await FindAsync(actor, lesson, scene, ct);
        if (row is not null && row.State is not ("quoted" or "failed")) return View(row);
        if (scene > 0 && !await db.Set<MimSceneVideo>().AnyAsync(x => x.LessonId == lesson && x.SceneIndex == scene - 1 && x.State == "completed", ct))
            throw new ArgumentException("ولّد فيديو المشهد السابق وراجعه أولاً، ثم ابدأ هذا المشهد.");
        var discovered = await connection.CallStudioToolAsync(actor, "models_explore", new { action = "get", model_id = model }, ct);
        MimVideoModels.ValidateCapabilities(HiggsfieldStudioReply.Payload(discovered), model);
        var media = new JsonArray();
        foreach (var url in ReferenceUrls)
        {
            var imported = await connection.CallStudioToolAsync(actor, "media_import_url", new { url, type = "image" }, ct);
            var mediaId = HiggsfieldStudioReply.RequiredId(HiggsfieldStudioReply.Payload(imported), "media_id", "id");
            media.Add(new JsonObject { ["value"] = mediaId.ToString(), ["role"] = "image_references" });
        }
        var costParams = MimVideoModels.Parameters(model, Prompt(script.Document, scene), media);
        costParams["get_cost"] = true;
        var cost = await connection.CallStudioToolAsync(actor, "generate_video", new { @params = costParams }, ct);
        var payload = HiggsfieldStudioReply.Payload(cost);
        if (HiggsfieldStudioReply.LiteralPresetToDecline(payload) is Guid preset)
        {
            // The approved storyboard must be generated literally. Decline only this named preset;
            // keep get_cost=true and never follow arbitrary recovery tools or repeat paid requests.
            costParams["declined_preset_id"] = preset.ToString();
            cost = await connection.CallStudioToolAsync(actor, "generate_video", new { @params = costParams }, ct);
            payload = HiggsfieldStudioReply.Payload(cost);
        }
        var quote = HiggsfieldStudioReply.Quote(payload);
        costParams.Remove("get_cost");
        if (row is null) { row = new MimSceneVideo { LessonId = lesson, SceneIndex = scene, AdminUserId = actor }; db.Add(row); }
        row.State = "quoted";
        row.JobId = null;
        row.ResultJson = "[]";
        row.ScriptVersion = script.Version;
        row.ParametersJson = costParams.ToJsonString(JsonOptions);
        row.QuoteText = quote;
        row.QuoteExpiresAt = DateTime.UtcNow.AddMinutes(5);
        row.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        return View(row);
    }

    public async Task<MimVideoView> SubmitAsync(Guid actor, Guid lesson, int scene, MimVideoApproval approval, CancellationToken ct)
    {
        var row = await FindAsync(actor, lesson, scene, ct) ?? throw new ArgumentException("اعرض تكلفة المشهد أولاً.");
        if (row.State != "quoted") return View(row); // Never duplicate an accepted or uncertain submission.
        if (row.Version != approval.Version || row.QuoteExpiresAt <= DateTime.UtcNow)
            throw new MimStudioConflictException("عرض التكلفة اتغيّر أو انتهى. اعرض التكلفة من جديد.");
        var script = await CurrentScriptAsync(lesson, scene, ct);
        if (script.Version != row.ScriptVersion) throw new MimStudioConflictException("الاسكربت اتعدّل بعد التسعير. اعرض التكلفة من جديد.");
        var preflightParameters = JsonNode.Parse(row.ParametersJson)!.AsObject();
        preflightParameters["get_cost"] = true;
        var recheck = await connection.CallStudioToolAsync(actor, "generate_video", new { @params = preflightParameters }, ct);
        if (HiggsfieldStudioReply.Quote(HiggsfieldStudioReply.Payload(recheck)) != row.QuoteText)
            throw new MimStudioConflictException("تكلفة Higgsfield اتغيّرت. اعرض التكلفة الجديدة قبل التوليد.");
        row.State = "submitting";
        row.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct); // Claim before any paid call, across all nodes and browser retries.
        try
        {
            var parameters = JsonSerializer.Deserialize<JsonElement>(row.ParametersJson);
            var submitted = await connection.CallStudioToolAsync(actor, "generate_video", new { @params = parameters }, ct);
            var payload = HiggsfieldStudioReply.Payload(submitted);
            if (payload.TryGetProperty("generation", out var generation)) payload = generation;
            row.JobId = HiggsfieldStudioReply.RequiredId(payload, "job_id", "jobId", "id");
            row.State = "running";
            row.Version = Guid.NewGuid();
            await db.SaveChangesAsync(CancellationToken.None);
            return View(row);
        }
        catch
        {
            // No automatic retry: a lost response may still have spent credits.
            await db.Set<MimSceneVideo>().Where(x => x.Id == row.Id && x.State == "submitting")
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.State, "unknown"), CancellationToken.None);
            throw;
        }
    }

    private async Task<MimStudioSnapshot> CurrentScriptAsync(Guid lesson, int scene, CancellationToken ct)
    {
        var snapshot = await studio.ReadAsync(lesson, ct) ?? throw new ArgumentException("اكتب المشهد واحفظه أولاً.");
        if (snapshot.Stale || snapshot.Generating) throw new MimStudioConflictException("راجع مصدر الاسكربت وانتظر انتهاء الكتابة قبل توليد الفيديو.");
        if (scene < 0 || scene >= snapshot.Document.Scenes.Length) throw new ArgumentException("المشهد غير موجود.");
        return snapshot;
    }
    private async Task<MimSceneVideo?> FindAsync(Guid actor, Guid lesson, int scene, CancellationToken ct)
    {
        var row = await db.Set<MimSceneVideo>().SingleOrDefaultAsync(x => x.LessonId == lesson && x.SceneIndex == scene, ct);
        if (row is not null && row.AdminUserId != actor) throw new ArgumentException("توليد هذا المشهد مرتبط بحساب إدارة آخر.");
        return row;
    }
    private static MimVideoView View(MimSceneVideo row) => new(row.Version, row.State, row.QuoteText, row.QuoteExpiresAt, row.JobId,
        row.ResultJson.StartsWith('[') ? JsonSerializer.Deserialize<string[]>(row.ResultJson)! : [],
        JsonNode.Parse(row.ParametersJson)?["model"]?.GetValue<string>() ?? "seedance_2_5");
    private static string Prompt(MimStudioDocument doc, int index) => string.Join("\n\n", new[] {
        "Create one 30-second cinematic 3D animation with Egyptian Arabic speech. Use attached reference 1 for Meem and reference 2 for Papa Nader. Preserve their exact appearance and clothes. Sheets are identity references only; never show the sheets or their collages in the video. No titles or subtitles.",
        doc.Style, doc.Continuity, System.Text.RegularExpressions.Regex.Split(doc.Scenes[index].Prompt, @"\nSCENE \d+:")[0],
        index > 0 ? "Continue the previous scene's final situation: " + doc.Scenes[index - 1].Shots.Last().Action : "Opening scene for this lesson.",
        string.Join("\n", doc.Scenes[index].Shots.Select(s => $"{s.Start}-{s.End}s: {s.Title}\nAction: {s.Action}\nCamera: {s.Camera}\nEXACT Egyptian dialogue (speaker labels are not spoken): {s.Dialogue}\nSound: {s.Sound}"))
    });
}
