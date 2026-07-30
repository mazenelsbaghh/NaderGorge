namespace NaderGorge.Application.Common;

public static class SerializationRetryHelper
{
    public const int DefaultMaxAttempts = 3;
    private const string SerializationFailureSqlState = "40001";

    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        int maxAttempts = DefaultMaxAttempts)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be positive.");

        Exception? lastFailure = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (IsSerializationFailure(ex) && attempt < maxAttempts)
            {
                lastFailure = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw lastFailure ?? new InvalidOperationException("Serialization retry helper exited without executing the operation.");
    }

    public static bool IsSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current)?.ToString();
            if (sqlState == SerializationFailureSqlState)
                return true;

            if (current.Message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
