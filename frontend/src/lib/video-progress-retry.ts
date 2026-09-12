/** Keep failed progress queued while respecting the server's recovery window. */
export function videoProgressRetryDelayMs(error: unknown, nowMs: number): number {
  const response = (error as { response?: { status?: number; headers?: Record<string, unknown>; data?: { retryAfterSeconds?: number } } })?.response;
  // A finished or paused video has no playback tick to retry a network failure.
  if (!response || response.status === undefined || response.status >= 500 && response.status !== 503) return 5_000;
  if (response.status !== 429 && response.status !== 503) return 0;
  const header = response.headers?.['retry-after'];
  const seconds = Number(header ?? response.data?.retryAfterSeconds);
  const milliseconds = Number.isFinite(seconds) && seconds > 0
    ? seconds * 1000
    : typeof header === 'string' ? Date.parse(header) - nowMs : Number.NaN;
  return Math.min(300_000, Math.max(1000, Number.isFinite(milliseconds) ? milliseconds : 60_000));
}
