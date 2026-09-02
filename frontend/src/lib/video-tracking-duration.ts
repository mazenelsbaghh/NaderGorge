export function resolveTrackableDurationSeconds(duration: number): number | null {
  if (!Number.isFinite(duration) || duration <= 0) {
    return null;
  }

  const roundedDuration = Math.round(duration);
  return roundedDuration > 0 ? roundedDuration : null;
}
