export const VIDEO_PLAYBACK_RATES = [0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];

export function usesNativeProviderControls(provider: string): boolean {
  return provider.toLowerCase() === 'bunny';
}
