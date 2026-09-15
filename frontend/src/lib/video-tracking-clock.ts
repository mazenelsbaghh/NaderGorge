/** Recover delayed timer ticks only when the media clock confirms continuous playback. */
export function trackedPlaybackTickSeconds(wallSeconds: number, mediaDelta: number, playbackRate: number): number {
  const mediaWallSeconds = mediaDelta / playbackRate;
  if (wallSeconds > 1.5 && Math.abs(mediaWallSeconds - wallSeconds) <= 0.5) {
    return Math.min(wallSeconds, mediaWallSeconds);
  }
  return Math.min(1.5, wallSeconds);
}
