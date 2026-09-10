export const MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS = 2;
export const BUNNY_PLAYBACK_STABILITY_WINDOW_MS = 10_000;

export function canRetryBunnyPlayback(provider: string, attempts: number): boolean {
  return ['bunny', 'bunny-hls'].includes(provider.toLowerCase())
    && attempts < MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS;
}

export function isExpiredHlsSourceError(status: number, signedExpiresAtMs: number, nowMs: number): boolean {
  return (status === 401 || status === 403)
    && signedExpiresAtMs > 0 && signedExpiresAtMs <= nowMs;
}

export function isBunnyPlaybackError(provider: unknown): boolean {
  return typeof provider === 'string' && provider.toLowerCase() === 'bunny';
}

export function isBunnyPlaybackStable(readyAtMs: number, nowMs: number): boolean {
  return readyAtMs > 0 && nowMs - readyAtMs >= BUNNY_PLAYBACK_STABILITY_WINDOW_MS;
}

export function isCurrentVideoSession(responseSessionId: string, activeSessionId: string | null): boolean {
  return responseSessionId === activeSessionId;
}
