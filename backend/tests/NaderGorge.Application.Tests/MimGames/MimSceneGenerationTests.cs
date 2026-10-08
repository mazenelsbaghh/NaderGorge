using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.MimStudio;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.MimStudio;

namespace NaderGorge.Application.Tests.MimGames;

public sealed class MimSceneGenerationTests
{
    [Fact]
    public async Task SequentialScenesSurviveReloadAndRejectStaleVersions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var (lesson, actor) = await SeedAsync(db);
        var handler = new WriterHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["WORKER_URL"]="http://worker.test", ["WORKER_ADMIN_TOKEN"]="test-only" }).Build();
        var service = new LessonMimStudioService(db, new MimSceneWriter(new HttpClient(handler), config));
        var text = string.Concat(Enumerable.Repeat("شرح دورة الماء: يتبخر الماء ثم يتكثف ويسقط مطرًا. ", 4));
        var first = await service.GenerateAsync(actor, lesson, new(null, null, 0, text, 0), CancellationToken.None);
        Assert.Single(first.Document.Scenes);
        Assert.Null(first.SourceVideoId);
        db.ChangeTracker.Clear();
        var reloaded = await service.ReadAsync(lesson, CancellationToken.None);
        Assert.Equal(text, reloaded!.Document.SourceText);
        await Assert.ThrowsAsync<MimStudioConflictException>(() => service.GenerateAsync(actor, lesson, new(null,null,0,text,0), CancellationToken.None));
        var second = await service.GenerateAsync(actor, lesson, new(first.Version,null,0,text,1), CancellationToken.None);
        Assert.Equal(2, second.Document.Scenes.Length);
        Assert.Equal(first.Document.Scenes[0].Title, second.Document.Scenes[0].Title);
        Assert.Single(handler.Contexts[1].GetProperty("previousScenes").GetProperty("scenes").EnumerateArray());
    }

    [Fact]
    public async Task SelectedVideoSuppliesOnlyItsSummariesAndCannotChangeMidStory()
    {
        await using var sqlite = new SqliteConnection("Data Source=:memory:");
        await sqlite.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(sqlite).Options);
        await db.Database.EnsureCreatedAsync();
        var (lesson, actor) = await SeedAsync(db);
        var type = new VideoType { Name="شرح", NormalizedName="شرح" };
        var selected = new LessonVideo { Title="دورة الماء", LessonId=lesson, VideoType=type,
            VideoChapters=[new VideoChapter { Title="التبخر", SummaryText="تتحول المياه إلى بخار بفعل الحرارة." }] };
        var other = new LessonVideo { Title="درس آخر", LessonId=lesson, VideoType=type,
            VideoChapters=[new VideoChapter { Title="معلومة أخرى", SummaryText="UNSELECTED_EXPLANATION" }] };
        db.AddRange(selected, other); await db.SaveChangesAsync();
        var handler = new WriterHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["WORKER_URL"]="http://worker.test", ["WORKER_ADMIN_TOKEN"]="test-only" }).Build();
        var service = new LessonMimStudioService(db,new MimSceneWriter(new HttpClient(handler),config));
        var saved = await service.GenerateAsync(actor,lesson,new(null,selected.Id,0,null,0),default);
        Assert.Equal(selected.Id, saved.SourceVideoId);
        Assert.Contains("تتحول المياه", handler.Contexts.Single().GetProperty("source").GetProperty("chapters")[0].GetProperty("summary").GetString());
        Assert.DoesNotContain("UNSELECTED_EXPLANATION", handler.Contexts.Single().GetRawText());
        await Assert.ThrowsAsync<MimStudioConflictException>(() => service.GenerateAsync(actor,lesson,new(saved.Version,other.Id,0,null,1),default));
    }

    [Fact]
    public async Task FailedWriterReleasesClaimWithoutLosingExistingScene()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var (lesson, actor) = await SeedAsync(db);
        var handler = new WriterHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["WORKER_URL"]="http://worker.test", ["WORKER_ADMIN_TOKEN"]="test-only" }).Build();
        var service = new LessonMimStudioService(db, new MimSceneWriter(new HttpClient(handler),config));
        var text = new string('ش', 120);
        var first = await service.GenerateAsync(actor, lesson, new(null,null,0,text,0), CancellationToken.None);
        db.ChangeTracker.Clear(); handler.Fail = true;
        await Assert.ThrowsAsync<MimStudioGenerationException>(() => service.GenerateAsync(actor, lesson, new(first.Version,null,0,text,1), CancellationToken.None));
        db.ChangeTracker.Clear();
        var saved = await service.ReadAsync(lesson, CancellationToken.None);
        Assert.Single(saved!.Document.Scenes);
        Assert.False(saved.Generating);
    }

    [Theory]
    [InlineData(false, "seedance_2_5")]
    [InlineData(true, "seedance_2_5")]
    [InlineData(false, "wan3_0_prime")]
    [InlineData(true, "wan3_0_prime")]
    public async Task VideoRequiresCostApprovalAndNeverResubmitsAcceptedOrUncertainRequest(bool loseReply, string model)
    {
        await using var sqlite = new SqliteConnection("Data Source=:memory:");
        await sqlite.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(sqlite).Options);
        await db.Database.EnsureCreatedAsync();
        var (lesson, actor) = await SeedAsync(db);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["WORKER_URL"]="http://worker.test", ["WORKER_ADMIN_TOKEN"]="test-only" }).Build();
        var script = new LessonMimStudioService(db, new MimSceneWriter(new HttpClient(new WriterHandler()),config));
        await script.GenerateAsync(actor,lesson,new(null,null,0,new string('ش',120),0),default);
        var protection = new EphemeralDataProtectionProvider();
        var session = JsonSerializer.Serialize(new { accessToken="synthetic-token", expires=DateTimeOffset.UtcNow.AddHours(1) });
        db.Add(new HiggsfieldMcpConnection { AdminUserId=actor, ClientId="test", ProtectedSession=protection.CreateProtector("Massar.HiggsfieldMcp.v1", actor.ToString("N")).Protect(session) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var provider = new VideoHandler(loseReply) { Reject = loseReply && model == "wan3_0_prime" };
        var mcp = new HiggsfieldMcpConnectionService(db,new ClientFactory(provider),protection,config);
        var videos = new MimSceneVideoService(db,script,mcp);
        var quote = await videos.QuoteAsync(actor,new(lesson,0,model),default);
        Assert.Equal("quoted", quote.State);
        Assert.Empty(provider.PaidRequests);
        if (loseReply) await Assert.ThrowsAsync<HiggsfieldMcpException>(() => videos.SubmitAsync(actor,lesson,0,new(quote.Version),default));
        else Assert.Equal("running", (await videos.SubmitAsync(actor,lesson,0,new(quote.Version),default)).State);
        db.ChangeTracker.Clear();
        var repeated = await videos.SubmitAsync(actor,lesson,0,new(quote.Version),default);
        Assert.Equal(loseReply ? "unknown" : "running", repeated.State);
        if (loseReply)
        {
            var reloaded = await videos.ReadAsync(actor,lesson,0,default);
            Assert.NotNull(reloaded!.Error);
            if (provider.Reject) Assert.Contains("INVALID_MEDIA", reloaded.Error);
            await Assert.ThrowsAsync<ArgumentException>(() => videos.ReviewAsync(actor,new(lesson,0,new(reloaded.Version,false)),default));
            await Assert.ThrowsAsync<MimStudioConflictException>(() => videos.ReviewAsync(actor,new(lesson,0,new(reloaded.Version,true)),default));
            var row = await db.Set<MimSceneVideo>().SingleAsync();
            await db.Set<MimSceneVideo>().Where(x => x.Id == row.Id).ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedAt, DateTime.UtcNow.AddMinutes(-6)));
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<MimStudioConflictException>(() => videos.ReviewAsync(actor,new(lesson,0,new(Guid.NewGuid(),true)),default));
            var reviewed = await videos.ReviewAsync(actor,new(lesson,0,new(reloaded.Version,true)),default);
            Assert.Equal("retry_ready", reviewed.State);
            Assert.Equal("retry_ready", (await videos.SubmitAsync(actor,lesson,0,new(quote.Version),default)).State);
            Assert.Single(provider.PaidRequests);
            db.ChangeTracker.Clear();
            var newQuote = await videos.QuoteAsync(actor,new(lesson,0,model),default);
            Assert.Null(newQuote.Error);
            Assert.Contains("no_generation_and_no_charge", (await db.Set<MimSceneVideo>().SingleAsync()).ResultJson);
            await Assert.ThrowsAsync<MimStudioConflictException>(() => videos.SubmitAsync(actor,lesson,0,new(quote.Version),default));
            Assert.Single(provider.PaidRequests);
        }
        var paid = Assert.Single(provider.PaidRequests);
        Assert.Equal(2, paid.GetProperty("medias").GetArrayLength());
        Assert.All(paid.GetProperty("medias").EnumerateArray(), media => Assert.Equal("image_references", media.GetProperty("role").GetString()));
        Assert.Equal(model, paid.GetProperty("model").GetString());
        Assert.Equal(model, quote.Model);
        if (model == "seedance_2_5") Assert.Equal("omni_reference", paid.GetProperty("mode").GetString());
        else Assert.False(paid.TryGetProperty("mode", out _));
        Assert.Equal("24bae836-2c4a-48e0-89b6-49fcc0b21612", paid.GetProperty("declined_preset_id").GetString());
        Assert.Equal(30, paid.GetProperty("duration").GetInt32());
        Assert.Equal(1, paid.GetProperty("count").GetInt32());
        Assert.DoesNotContain("OLD_STORYBOARD", paid.GetProperty("prompt").GetString());
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler:false);
    }
    private sealed class VideoHandler(bool loseReply) : HttpMessageHandler
    {
        public List<JsonElement> PaidRequests { get; } = [];
        public bool Reject { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.GetProperty("method").GetString() == "notifications/initialized") return new(HttpStatusCode.Accepted);
            object result;
            if (root.GetProperty("method").GetString() == "initialize") result = new { protocolVersion="2025-11-25" };
            else
            {
                var args=root.GetProperty("params");
                var name=args.GetProperty("name").GetString();
                object payload = new { id=args.GetProperty("arguments").TryGetProperty("model_id", out var requestedModel) ? requestedModel.GetString() : "seedance_2_5",
                    aspect_ratios=new[]{"16:9"}, parameters=new[]{new { name="duration", max=30 }}, medias=new[]{new { roles=new[]{"image_references"} }} };
                if (name == "media_import_url") payload = new { media_id=Guid.NewGuid() };
                if (name == "generate_video")
                {
                    var parameters=args.GetProperty("arguments").GetProperty("params");
                    if (parameters.TryGetProperty("get_cost",out var cost) && cost.GetBoolean())
                        payload = parameters.TryGetProperty("declined_preset_id", out _)
                            ? new { cost = new { credits=210, credits_exact=210 }, adjustments = new { } }
                            : new { notice = new { type = "preset_recommendation", data = new { retry_literal_with = new { declined_preset_id = "24bae836-2c4a-48e0-89b6-49fcc0b21612" } } } };
                    else
                    {
                        PaidRequests.Add(parameters.Clone());
                        if (Reject) return new(HttpStatusCode.OK) { Content=JsonContent.Create(new { jsonrpc="2.0", id=root.GetProperty("id").GetString(), result=new { isError=true, structuredContent=new { error=new { code="INVALID_MEDIA", message="Reference image unavailable" } } } }) };
                        if (loseReply) throw new HttpRequestException("lost synthetic response");
                        payload=new { job_id=Guid.NewGuid() };
                    }
                }
                result=new { structuredContent=payload };
            }
            return new(HttpStatusCode.OK) { Content=JsonContent.Create(new { jsonrpc="2.0", id=root.GetProperty("id").GetString(), result }) };
        }
    }

    private static async Task<(Guid Lesson, Guid Actor)> SeedAsync(AppDbContext db)
    {
        var actor = new User { FullName="Studio tester", PhoneNumber="01088812345", PasswordHash="test" };
        var subject = new Subject { Name="Science", NormalizedName="SCIENCE" };
        var package = new Package { Name="Science", Subject=subject, Teacher=new TeacherProfile { User=actor } };
        var term = new Term { Title="Term", Package=package };
        var section = new ContentSection { Title="Unit", Term=term };
        var lesson = new Lesson { Title="دورة الماء", ContentSection=section, Order=1 };
        db.AddRange(actor,lesson); await db.SaveChangesAsync();
        return (lesson.Id,actor.Id);
    }
    private sealed class WriterHandler : HttpMessageHandler
    {
        public bool Fail { get; set; }
        public List<JsonElement> Contexts { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Contexts.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone());
            if (Fail) return new(HttpStatusCode.ServiceUnavailable);
            var chapters = Contexts.Last().GetProperty("source").GetProperty("chapters").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
            var scene = new MimScene("رحلة الماء", "التبخر", chapters, Enumerable.Range(0,6).Select(i=>new MimShot(i*5,(i+1)*5,"القطرة","تتبخر القطرة","لقطة قريبة","","")).ToArray(), "Character references.\nSCENE 1:\nOLD_STORYBOARD");
            return new(HttpStatusCode.OK) { Content=JsonContent.Create(new MimStudioDocument(1,"دورة الماء","رحلة ميم","3D","نفس الشخصيات",[scene])) };
        }
    }
}
