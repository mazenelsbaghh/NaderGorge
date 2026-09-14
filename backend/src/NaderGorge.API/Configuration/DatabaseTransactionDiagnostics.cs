using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using Npgsql;

namespace NaderGorge.API.Configuration;

public sealed class DatabaseTransactionDiagnostics(
    ILogger<DatabaseTransactionDiagnostics> logger,
    IHttpContextAccessor httpContextAccessor) : DbTransactionInterceptor
{
    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        => LogFailure(eventData);

    public override Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogFailure(eventData);
        return Task.CompletedTask;
    }

    private void LogFailure(TransactionErrorEventData eventData)
    {
        var context = httpContextAccessor.HttpContext;
        var cause = eventData.Exception.GetBaseException();
        // Exception messages and SQL parameters can contain student or payment data.
        logger.LogWarning(
            "Database transaction failed. Operation={Operation} ExceptionType={ExceptionType} SqlState={SqlState} Transient={Transient} RequestAborted={RequestAborted} CorrelationId={CorrelationId} Route={Route}",
            eventData.Action, cause.GetType().Name, (cause as PostgresException)?.SqlState,
            DatabaseFailureClassifier.IsTransient(eventData.Exception),
            context?.RequestAborted.IsCancellationRequested ?? false,
            context?.Items["CorrelationId"]?.ToString() ?? context?.TraceIdentifier,
            (context?.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText);
    }
}
