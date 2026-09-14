export function resolveTrackableDurationSeconds(duration: number): number | null {
  if (!Number.isFinite(duration) || duration <= 0) {
    return null;
  }

  const roundedDuration = Math.round(duration);
  return roundedDuration > 0 ? roundedDuration : null;
}

/**
 * Server-authoritative providers may accept a zero duration and resolve their
 * immutable asset/session snapshot server-side. Other providers must wait for
 * a valid player duration so a client cannot invent the tracking threshold.
 */
export function resolveProgressReportDurationSeconds(
  duration: number,
  serverCanResolveDuration: boolean,
): number | null {
  return resolveTrackableDurationSeconds(duration) ?? (serverCanResolveDuration ? 0 : null);
}

export function resolveStableVideoDuration(
  currentDuration: number | null,
  reportedDuration: unknown,
): number | null {
  const current = currentDuration === null
    ? null
    : resolveTrackableDurationSeconds(currentDuration);
  if (current !== null) return current;

  return resolveTrackableDurationSeconds(Number(reportedDuration));
}

export function resolveWatchThresholdSeconds(
  durationSeconds: number,
  thresholdPercentage: number,
): number {
  const safeDuration = resolveTrackableDurationSeconds(durationSeconds) ?? 1;
  const safePercentage = Math.min(100, Math.max(1, thresholdPercentage));
  const rawThreshold = safeDuration * (safePercentage / 100);
  const lower = Math.floor(rawThreshold);
  const fraction = rawThreshold - lower;

  // Match System.Math.Round's default midpoint-to-even behavior used by the
  // backend, including the exact x.5 values common with percentage settings.
  const rounded = Math.abs(fraction - 0.5) < 1e-9
    ? (lower % 2 === 0 ? lower : lower + 1)
    : Math.round(rawThreshold);
  return Math.max(1, rounded);
}
