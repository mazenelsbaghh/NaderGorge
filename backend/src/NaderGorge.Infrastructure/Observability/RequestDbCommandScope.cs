namespace NaderGorge.Infrastructure.Observability;

public sealed class RequestDbCommandScope : IDisposable
{
    private static readonly AsyncLocal<RequestDbCommandScope?> CurrentScope = new();
    private readonly RequestDbCommandScope? _previousScope;
    private long _commandCount;
    private long _durationTicks;
    private bool _disposed;
    private readonly List<CommandTiming> _commands = [];
    private double _connectionMilliseconds;
    public sealed record CommandTiming(string Operation, double Milliseconds, bool Success);
    public CommandTiming[] Commands { get { lock (_commands) return _commands.ToArray(); } }
    public double ConnectionMilliseconds { get { lock (_commands) return _connectionMilliseconds; } }

    public static void RecordConnection(TimeSpan duration)
    {
        var scope = CurrentScope.Value;
        if (scope is not null) lock (scope._commands) scope._connectionMilliseconds += duration.TotalMilliseconds;
    }

    public static void RecordTransactionCommit(TimeSpan duration) => RecordDetail("transaction_commit", duration, true);

    internal static void RecordDetail(string operation, TimeSpan duration, bool success)
    {
        var scope = CurrentScope.Value;
        if (scope is null) return;
        lock (scope._commands)
            if (scope._commands.Count < 24)
                scope._commands.Add(new(operation, Math.Round(duration.TotalMilliseconds, 2), success));
    }

    private RequestDbCommandScope(RequestDbCommandScope? previousScope)
    {
        _previousScope = previousScope;
    }

    public long CommandCount => Interlocked.Read(ref _commandCount);

    public double CommandDurationMilliseconds =>
        TimeSpan.FromTicks(Interlocked.Read(ref _durationTicks)).TotalMilliseconds;

    public static RequestDbCommandScope Begin()
    {
        var scope = new RequestDbCommandScope(CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    internal static void Record(TimeSpan duration)
    {
        var scope = CurrentScope.Value;
        if (scope is null)
        {
            return;
        }

        Interlocked.Increment(ref scope._commandCount);
        Interlocked.Add(ref scope._durationTicks, duration.Ticks);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CurrentScope.Value = _previousScope;
        _disposed = true;
    }
}
