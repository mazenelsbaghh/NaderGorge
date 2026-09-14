namespace NaderGorge.Infrastructure.Observability;

public sealed class RequestDbCommandScope : IDisposable
{
    private static readonly AsyncLocal<RequestDbCommandScope?> CurrentScope = new();
    private readonly RequestDbCommandScope? _previousScope;
    private long _commandCount;
    private long _durationTicks;
    private bool _disposed;

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
