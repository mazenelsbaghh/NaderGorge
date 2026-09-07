export type BunnyPlaybackMode = 0 | 1 | 'BunnyPlayer' | 'PlatformHls';

export function bunnyPlaybackSelection(mode?: BunnyPlaybackMode): 0 | 1 {
  return mode === 1 || mode === 'PlatformHls' ? 1 : 0;
}
