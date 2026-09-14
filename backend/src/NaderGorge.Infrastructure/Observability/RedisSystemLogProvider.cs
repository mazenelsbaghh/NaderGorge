using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace NaderGorge.Infrastructure.Observability;

public sealed class RedisSystemLogProvider(IConnectionMultiplexer redis) : ILoggerProvider
{
    public const string RedisKey = "system:logs:v1";
    public const int Capacity = 2_000;

    public ILogger CreateLogger(string categoryName) => new RedisSystemLogger(redis, categoryName);
    public void Dispose() { }

    private sealed class RedisSystemLogger(IConnectionMultiplexer redis, string category) : ILogger
    {
        private static readonly Regex SensitiveValues = new(
            @"(?i)(token|secret|password|authorization|cookie)\s*[:=]\s*[^\s,;]+",
            RegexOptions.Compiled);
        private static readonly Regex Urls = new(@"https?://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;

        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level) || category.StartsWith("StackExchange.Redis", StringComparison.Ordinal)) return;

            var entry = JsonSerializer.Serialize(new
            {
                id = Guid.NewGuid(),
                timestamp = DateTimeOffset.UtcNow,
                source = "backend",
                level = level.ToString().ToLowerInvariant(),
                category,
                message = Redact(formatter(state, exception)),
                exception = exception is null ? null : Redact(exception.ToString())
            });

            _ = StoreAsync(entry);
        }

        private async Task StoreAsync(string entry)
        {
            try
            {
                var database = redis.GetDatabase();
                await database.ListRightPushAsync(RedisKey, entry);
                await database.ListTrimAsync(RedisKey, -Capacity, -1);
            }
            catch (RedisException)
            {
                // Logging must not make the application unavailable when Redis is degraded.
            }
            catch (ObjectDisposedException)
            {
                // Shutdown can dispose Redis while late log entries are still being flushed.
            }
        }

        private static string Redact(string text)
        {
            var boundedText = text.Length > 12_000 ? text[..12_000] : text;
            return Urls.Replace(SensitiveValues.Replace(boundedText, "$1=[redacted]"), "[redacted-url]");
        }
    }
}
