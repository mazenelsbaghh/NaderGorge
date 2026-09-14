const YOUTUBE_VIDEO_ID = /^[A-Za-z0-9_-]{11}$/;
const YOUTUBE_HOSTS = new Set([
  'youtube.com',
  'www.youtube.com',
  'm.youtube.com',
  'music.youtube.com',
  'youtube-nocookie.com',
  'www.youtube-nocookie.com',
]);

function canonicalUrl(videoId: string | undefined) {
  if (!videoId || !YOUTUBE_VIDEO_ID.test(videoId)) return undefined;
  return `https://www.youtube.com/watch?v=${videoId}`;
}

/**
 * Returns a canonical public YouTube URL that is safe to send to Gemini.
 * Tracking parameters, alternate hosts and unrelated path/query data are dropped.
 */
export function normalizePublicYouTubeUrl(source: string) {
  const value = source.trim();
  if (YOUTUBE_VIDEO_ID.test(value)) return canonicalUrl(value);

  let parsed: URL;
  try {
    parsed = new URL(value);
  } catch {
    return undefined;
  }

  if (!['http:', 'https:'].includes(parsed.protocol) || parsed.username || parsed.password) {
    return undefined;
  }

  const host = parsed.hostname.toLowerCase();
  if (host === 'youtu.be' || host === 'www.youtu.be') {
    return canonicalUrl(parsed.pathname.split('/').filter(Boolean)[0]);
  }
  if (!YOUTUBE_HOSTS.has(host)) return undefined;

  if (parsed.pathname === '/watch') return canonicalUrl(parsed.searchParams.get('v') || undefined);
  const [route, videoId] = parsed.pathname.split('/').filter(Boolean);
  if (route && ['embed', 'shorts', 'live'].includes(route)) return canonicalUrl(videoId);
  return undefined;
}
