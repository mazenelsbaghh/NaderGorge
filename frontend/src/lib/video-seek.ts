export const DOUBLE_TAP_SEEK_SECONDS = 10;
export const DOUBLE_TAP_WINDOW_MS = 325;

export type SeekDirection = 'backward' | 'forward';

interface SeekTap {
  direction: SeekDirection;
  timestamp: number;
}

export function isDoubleTapSeek(previous: SeekTap | null, current: SeekTap): boolean {
  if (!previous || previous.direction !== current.direction) return false;
  const elapsed = current.timestamp - previous.timestamp;
  return elapsed >= 0 && elapsed <= DOUBLE_TAP_WINDOW_MS;
}

export function resolveSeekTarget(
  currentTime: number,
  duration: number,
  direction: SeekDirection,
): number {
  const safeCurrentTime = Number.isFinite(currentTime) ? Math.max(0, currentTime) : 0;
  const delta = direction === 'forward' ? DOUBLE_TAP_SEEK_SECONDS : -DOUBLE_TAP_SEEK_SECONDS;
  const requestedTime = Math.max(0, safeCurrentTime + delta);
  return Number.isFinite(duration) && duration > 0
    ? Math.min(duration, requestedTime)
    : requestedTime;
}
