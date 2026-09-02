export const MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS = 2;
export const BUNNY_PLAYBACK_STABILITY_WINDOW_MS = 10_000;

export function canRetryBunnyPlayback(provider: string, attempts: number): boolean {
  return provider.toLowerCase() === 'bunny'
    && attempts < MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS;
}

export function isBunnyPlaybackStable(readyAtMs: number, nowMs: number): boolean {
  return readyAtMs > 0 && nowMs - readyAtMs >= BUNNY_PLAYBACK_STABILITY_WINDOW_MS;
}
