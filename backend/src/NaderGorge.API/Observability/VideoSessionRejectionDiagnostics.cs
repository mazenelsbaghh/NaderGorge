using Microsoft.Extensions.Logging;

namespace NaderGorge.API.Observability;

/// <summary>Samples rejection reasons without retaining request data or changing playback results.</summary>
public sealed class VideoSessionRejectionDiagnostics(TimeProvider? timeProvider = null)
{
    private static readonly string[] Codes =
    [
        "UNCLASSIFIED", "VIDEO_NOT_FOUND", "ACCESS_DENIED", "WATCH_LIMIT_REACHED", "EXAM_LOCKED",
        "BUNNY_VIDEO_NOT_READY", "INVALID_PROVIDER", "YOUTUBE_HLS_SOURCE_INVALID", "BUNNY_LIBRARY_MISSING",
        "BUNNY_HLS_VALIDATION_UNAVAILABLE", "BUNNY_HLS_CONFIG_INCOMPLETE", "BUNNY_HLS_SIGNING_FAILED",
        "GIFT_LIMIT_REACHED", "BUNNY_HLS_VIDEO_INVALID", "BUNNY_HLS_TIMEOUT", "BUNNY_HLS_UNREACHABLE",
        "BUNNY_HLS_AUTH_REJECTED", "BUNNY_HLS_NATIVE_AUTH_REJECTED", "BUNNY_HLS_CORS_REJECTED",
        "BUNNY_HLS_MANIFEST_INVALID", "BUNNY_HLS_NOT_FOUND", "BUNNY_HLS_HTTP_ERROR"
    ];
    private static readonly EventId RejectedEvent = new(15001, "VideoSessionCreationRejected");
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);
    public static VideoSessionRejectionDiagnostics Shared { get; } = new();
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly Slot[] _slots = new Slot[Codes.Length];
    private readonly object _gate = new();

    public void Log(ILogger logger, string? errorCode, double handlerMilliseconds)
    {
        try
        {
            if (!logger.IsEnabled(LogLevel.Warning)) return;
            var index = Array.IndexOf(Codes, errorCode);
            if (index < 0) index = 0;
            var now = _clock.GetTimestamp();
            long suppressed;
            lock (_gate)
            {
                ref var slot = ref _slots[index];
                if (slot.InFlight || (slot.HasSample && _clock.GetElapsedTime(slot.Timestamp, now) < SampleInterval))
                {
                    if (slot.Suppressed < long.MaxValue) slot.Suppressed++;
                    return;
                }
                suppressed = slot.Suppressed;
                slot.Suppressed = 0;
                slot.InFlight = true;
            }

            try
            {
                var milliseconds = double.IsFinite(handlerMilliseconds) && handlerMilliseconds >= 0
                    ? Math.Round(handlerMilliseconds, 2) : 0;
                // Only constants and numbers enter the log. Never retain/log the original error value or exception.
                logger.LogWarning(RejectedEvent,
                    "Video session creation rejected. Route=api/student/video-session ReasonCode={ReasonCode} HandlerMs={HandlerMs} Suppressed={Suppressed}",
                    Codes[index], milliseconds, suppressed);
            }
            finally
            {
                // Keep the reservation during logger I/O and start the next minute after it completes.
                // A stale pre-lock timestamp can only suppress a call, never admit an early duplicate.
                var completedAt = _clock.GetTimestamp();
                lock (_gate)
                {
                    ref var slot = ref _slots[index];
                    slot.Timestamp = completedAt;
                    slot.HasSample = true;
                    slot.InFlight = false;
                }
            }
        }
        catch (Exception)
        {
            // Diagnostics, including a failing provider, must not change the student's response.
        }
    }

    private struct Slot
    {
        public bool HasSample;
        public bool InFlight;
        public long Timestamp;
        public long Suppressed;
    }
}
