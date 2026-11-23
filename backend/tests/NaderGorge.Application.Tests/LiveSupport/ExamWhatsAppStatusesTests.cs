using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class ExamWhatsAppStatusesTests
{
    [Fact]
    public async Task SignedRequest_ReturnsOnlyRequestedMessagesForExamAccount()
    {
        await using var scenario = await Scenario.CreateAsync();
        scenario.Db.ExamWhatsAppDeliveryEvents.AddRange(
            Receipt("one", "wamid.ours", "exam-phone", "read"),
            Receipt("two", "wamid.ours", "other-phone", "delivered"),
            Receipt("three", "wamid.other", "exam-phone", "read"));
        await scenario.Db.SaveChangesAsync();
        var result = await scenario.Controller(Request("exam-waba", "wamid.ours"))
            .ReadStatuses(CancellationToken.None);
        var response = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(response.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var stored = Assert.Single(document.RootElement.GetProperty("events").EnumerateArray());
        Assert.Equal("read", stored.GetProperty("status").GetString());
        Assert.Equal("wamid.ours", stored.GetProperty("messageId").GetString());
        Assert.DoesNotContain("wamid.other", json);
        Assert.DoesNotContain("other-phone", json);
        Assert.DoesNotContain("exam-secret", json);
    }

    [Theory]
    [InlineData("wrong-secret", "exam-waba", 0)]
    [InlineData("exam-secret", "other-waba", 0)]
    [InlineData("exam-secret", "exam-waba", -600)]
    public async Task WrongSignatureAccountOrExpiredRequest_IsRejected(string secret, string waba, int age)
    {
        await using var scenario = await Scenario.CreateAsync();
        var controller = scenario.Controller(Request(waba, "wamid.ours"), secret, age);
        Assert.IsType<UnauthorizedResult>(await controller.ReadStatuses(CancellationToken.None));
    }

    [Fact]
    public async Task ModifiedBodyAfterSigning_IsRejected()
    {
        await using var scenario = await Scenario.CreateAsync();
        var controller = scenario.Controller(Request("exam-waba", "wamid.ours"));
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Request("exam-waba", "wamid.other")));
        Assert.IsType<UnauthorizedResult>(await controller.ReadStatuses(CancellationToken.None));
    }

    private static string Request(string waba, string id) => JsonSerializer.Serialize(new
    {
        businessAccountId = waba, phoneNumberId = "exam-phone", messageIds = new[] { id }
    });

    private static ExamWhatsAppDeliveryEvent Receipt(string fingerprint, string id, string phone, string status) =>
        new() { Fingerprint = fingerprint, BusinessAccountId = "exam-waba", PhoneNumberId = phone,
            MessageId = id, Status = status, EventUnixTime = 100 };

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

        public ExamWhatsAppStatusesController Controller(string body, string secret = "exam-secret", int age = 0)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExamWhatsAppCloud:AppSecret"] = "exam-secret",
                ["ExamWhatsAppCloud:BusinessAccountId"] = "exam-waba",
                ["ExamWhatsAppCloud:PhoneNumberId"] = "exam-phone"
            }).Build();
            var controller = new ExamWhatsAppStatusesController(Db, config)
                { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + age).ToString(CultureInfo.InvariantCulture);
            var signed = Encoding.UTF8.GetBytes($"MASSAR-EXAM-RECEIPTS-v1\n{timestamp}\n{body}");
            controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            controller.Request.Headers["X-Massar-Timestamp"] = timestamp;
            controller.Request.Headers["X-Massar-Signature"] = Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed));
            return controller;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
