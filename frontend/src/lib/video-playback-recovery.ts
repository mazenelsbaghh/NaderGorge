export const MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS = 2;

export function canRetryBunnyPlayback(provider: string, attempts: number): boolean {
  return provider.toLowerCase() === 'bunny'
    && attempts < MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS;
}
