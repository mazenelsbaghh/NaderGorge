using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class ExamWhatsAppWebhookTests
{
    [Theory]
    [InlineData("exam-token", true)]
    [InlineData("platform-token", false)]
    [InlineData("", false)]
    public void ExamChallenge_RequiresExamTokenInsteadOfPlatformToken(string token, bool accepted)
    {
        var controller = new ExamWhatsAppWebhookController(Configuration(), null!);
        var response = controller.VerifyWebhook("subscribe", token, "challenge");
        if (accepted) Assert.Equal("challenge", Assert.IsType<ContentResult>(response).Content);
        else Assert.IsType<ForbidResult>(response);
    }

    [Theory]
    [InlineData("platform-secret")]
    [InlineData("wrong-secret")]
    public async Task ExamEvents_RejectSignatureFromOtherApp(string secret)
    {
        await using var scenario = await Scenario.CreateAsync();
        var controller = scenario.Controller("{}", secret);
        Assert.IsType<UnauthorizedResult>(await controller.ReceiveWebhook(CancellationToken.None));
        Assert.Empty(await scenario.Db.ExamWhatsAppDeliveryEvents.ToListAsync());
    }

    [Fact]
    public async Task DuplicateAndOutOfOrderDelivery_KeepOneReceiptPerEventWithoutPlatformMessages()
    {
        await using var scenario = await Scenario.CreateAsync();
        var statuses = new[] { Status("read", "200"), Status("sent", "100") };
        var webhook = Webhook("exam-waba", "exam-phone", statuses);
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.IsType<OkObjectResult>(await scenario.Controller(webhook).ReceiveWebhook(CancellationToken.None));
        var stored = await scenario.Db.ExamWhatsAppDeliveryEvents.OrderBy(x => x.EventUnixTime).ToListAsync();
        Assert.Equal(new[] { "sent", "read" }, stored.Select(x => x.Status));
        Assert.All(stored, x => Assert.Equal("exam-waba", x.BusinessAccountId));
        Assert.Empty(await scenario.Db.LiveSupportMessages.ToListAsync());
    }

    [Theory]
    [InlineData("platform-waba", "exam-phone")]
    [InlineData("exam-waba", "platform-phone")]
    public async Task OtherAccountOrPhone_ProducesNoExamReceipts(string waba, string phone)
    {
        await using var scenario = await Scenario.CreateAsync();
        var controller = scenario.Controller(Webhook(waba, phone, [Status("delivered", "100")]));
        Assert.IsType<OkObjectResult>(await controller.ReceiveWebhook(CancellationToken.None));
        Assert.Empty(await scenario.Db.ExamWhatsAppDeliveryEvents.ToListAsync());
    }

    [Theory]
    [InlineData("unknown", "100")]
    [InlineData("sent", "-1")]
    [InlineData("sent", "invalid")]
    public async Task InvalidReceiptInBatch_RejectsEntireBatchWithoutPartialWrites(string status, string timestamp)
    {
        await using var scenario = await Scenario.CreateAsync();
        var body = Webhook("exam-waba", "exam-phone", [Status("read", "100"), Status(status, timestamp)]);
        Assert.IsType<BadRequestResult>(await scenario.Controller(body).ReceiveWebhook(CancellationToken.None));
        Assert.Empty(await scenario.Db.ExamWhatsAppDeliveryEvents.ToListAsync());
    }

    [Fact]
    public async Task FailureReceipt_RecordsNumericErrorWithoutRecipientOrProviderText()
    {
        await using var scenario = await Scenario.CreateAsync();
        var failure = new { id = "wamid.test", status = "failed", timestamp = "100", recipient_id = "201000000000",
            errors = new[] { new { code = 131026, message = "private provider detail" } } };
        var body = Webhook("exam-waba", "exam-phone", [failure]);
        Assert.IsType<OkObjectResult>(await scenario.Controller(body).ReceiveWebhook(CancellationToken.None));
        var receipt = Assert.Single(await scenario.Db.ExamWhatsAppDeliveryEvents.ToListAsync());
        Assert.Equal(131026, receipt.ErrorCode);
        var serialized = JsonSerializer.Serialize(receipt);
        Assert.DoesNotContain("201000000000", serialized);
        Assert.DoesNotContain("private provider detail", serialized);
    }

    private static object Status(string status, string timestamp) => new { id = "wamid.test", status, timestamp };

    private static string Webhook(string waba, string phone, object[] statuses) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[] { new { id = waba, changes = new[] { new { field = "messages",
            value = new { metadata = new { phone_number_id = phone }, statuses } } } } }
    });

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["ExamWhatsAppCloud:VerifyToken"] = "exam-token",
            ["ExamWhatsAppCloud:AppSecret"] = "exam-secret",
            ["ExamWhatsAppCloud:BusinessAccountId"] = "exam-waba",
            ["ExamWhatsAppCloud:PhoneNumberId"] = "exam-phone",
            ["WhatsAppCloudApi:VerifyToken"] = "platform-token",
            ["WhatsAppCloudApi:AppSecret"] = "platform-secret"
        }).Build();

    private sealed record Scenario(SqliteConnection Connection, AppDbContext Db) : IAsyncDisposable
    {
        public static async Task<Scenario> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new Scenario(connection, db);
        }

        public ExamWhatsAppWebhookController Controller(string body, string secret = "exam-secret")
        {
            var config = Configuration();
            var controller = new ExamWhatsAppWebhookController(config, new ExamWhatsAppWebhookService(Db, config));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            controller.Request.Headers["X-Hub-Signature-256"] = "sha256=" +
                Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
            return controller;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
