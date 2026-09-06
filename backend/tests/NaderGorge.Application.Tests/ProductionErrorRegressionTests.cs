using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Middleware;
using NaderGorge.Infrastructure.Services;
using Npgsql;

namespace NaderGorge.Application.Tests;

public sealed class ProductionErrorRegressionTests
{
    [Theory]
    [InlineData(true, 499)]
    [InlineData(false, 500)]
    public async Task CancelledRequest_DoesNotBecomeServerFailureUnlessClientIsStillConnected(bool disconnected, int expectedStatus)
    {
        using var cancellation = new CancellationTokenSource();
        if (disconnected) cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(new OperationCanceledException("private cancellation detail")),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        if (disconnected) Assert.Equal(0, context.Response.Body.Length);
        else Assert.True(context.Response.Body.Length > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Incident20260906_DatabaseConnectionFailure_Returns503WithRetryGuidance(bool wrapped)
    {
        Exception failure = new NpgsqlException("private connection detail", new IOException("connection interrupted"));
        if (wrapped) failure = new InvalidOperationException("transient database failure", failure);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => Task.FromException(failure),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("5", context.Response.Headers.RetryAfter);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Contains("DATABASE_TEMPORARILY_UNAVAILABLE", body.RootElement.GetRawText());
        Assert.DoesNotContain("private connection detail", body.RootElement.GetRawText());
    }

    [Theory]
    [InlineData("40001", null, true, 409)]
    [InlineData("23505", "IX_live_support_events_ConversationId_Sequence", true, 409)]
    [InlineData("23505", "IX_users_PhoneNumber", false, 409)]
    [InlineData("42703", null, false, 500)]
    public async Task Incident20260906_DatabaseFailures_AreClassifiedWithoutLeakingDetails(
        string sqlState, string? constraint, bool retryable, int statusCode)
    {
        Exception failure = new DbUpdateException("database write failed",
            new PostgresException("private database detail", "ERROR", "ERROR", sqlState, constraintName: constraint));
        if (sqlState == "40001")
            failure = new InvalidOperationException("An exception has been raised that is likely due to a transient failure.", failure);
        Assert.Equal(retryable, LiveSupportWriteConflict.IsRetryable(failure));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => Task.FromException(failure),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(statusCode, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.DoesNotContain("private database detail", body.RootElement.GetRawText());
        if (sqlState == "40001")
            Assert.Contains("CONCURRENT_WRITE_CONFLICT", body.RootElement.GetRawText());
    }
}
