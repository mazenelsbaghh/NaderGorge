export function clearVideoPlaybackCookies(sessionId?: string): void {
  if (typeof window === 'undefined') return;
  // Start synchronously so navigation/logout cannot discard the cleanup request.
  const endpoint = '/api/video/session' + (sessionId ? `?s=${encodeURIComponent(sessionId)}` : '');
  void fetch(endpoint, {
    method: 'DELETE', credentials: 'same-origin', keepalive: true, cache: 'no-store',
  }).catch(() => {
    // Offline logout still clears local auth; playback cookies expire independently.
  });
}
