function isVcdnVideoId(videoId: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(videoId);
}

export function parseVcdnVideoId(source: string): string | null {
  let candidate = source.trim();
  if (/^<iframe\b/i.test(candidate)) candidate = candidate.match(/\bsrc\s*=\s*["']([^"']+)["']/i)?.[1] ?? '';
  if (isVcdnVideoId(candidate)) return candidate;
  let url: URL;
  try { url = new URL(candidate); } catch { return null; }
  if (url.protocol !== 'https:' || url.port || url.username || url.password || url.search || url.hash) return null;
  const path = url.pathname.slice(1);
  const videoId = url.hostname === 'embed.vcdn.me' ? path.replace(/^embed\//, '')
    : url.hostname === 'stream.vcdn.me' && path.endsWith('/master.m3u8') ? path.slice(0, -'/master.m3u8'.length) : '';
  return isVcdnVideoId(videoId) ? videoId : null;
}
