using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NaderGorge.Infrastructure.Observability;

internal enum DbCommandOutcome
{
    Success,
    Failure
}

public sealed class DbCommandMetricsInterceptor : DbCommandInterceptor
{
    public const string MeterName = "NaderGorge.Database";
    public const string CommandCountName = "db.client.commands";
    public const string CommandDurationName = "db.client.command.duration";

    private static readonly Meter DatabaseMeter = new(MeterName);
    private static readonly Counter<long> CommandCount =
        DatabaseMeter.CreateCounter<long>(CommandCountName);
    private static readonly Histogram<double> CommandDuration =
        DatabaseMeter.CreateHistogram<double>(CommandDurationName, "ms");

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        RecordCommand(command, eventData, DbCommandOutcome.Success);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command, eventData, DbCommandOutcome.Success);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        RecordCommand(command, eventData, DbCommandOutcome.Success);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command, eventData, DbCommandOutcome.Success);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        RecordCommand(command, eventData, DbCommandOutcome.Success);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command, eventData, DbCommandOutcome.Success);
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        RecordCommand(command, eventData, DbCommandOutcome.Failure);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RecordCommand(command, eventData, DbCommandOutcome.Failure);
        return Task.CompletedTask;
    }

    private static void RecordCommand(
        DbCommand command,
        CommandEndEventData eventData,
        DbCommandOutcome outcome)
    {
        var operation = command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.OrdinalIgnoreCase)
            ? "advisory_lock" : OperationName(eventData.ExecuteMethod);
        RequestDbCommandScope.RecordDetail(operation, eventData.Duration, outcome == DbCommandOutcome.Success);
        RecordCommand(eventData.ExecuteMethod, eventData.Duration, outcome);
    }

    internal static void RecordCommand(
        DbCommandMethod method,
        TimeSpan duration,
        DbCommandOutcome outcome)
    {
        RequestDbCommandScope.Record(duration);

        TagList tags = default;
        tags.Add("operation", OperationName(method));
        tags.Add("success", outcome == DbCommandOutcome.Success);

        CommandCount.Add(1, tags);
        CommandDuration.Record(duration.TotalMilliseconds, tags);
    }

    private static string OperationName(DbCommandMethod method) =>
        method switch
        {
            DbCommandMethod.ExecuteReader => "reader",
            DbCommandMethod.ExecuteNonQuery => "non_query",
            DbCommandMethod.ExecuteScalar => "scalar",
            _ => "unknown"
        };
}
