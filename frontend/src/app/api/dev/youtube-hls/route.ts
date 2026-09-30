import { readFile } from 'node:fs/promises';
import { generateYouTubeHlsEmbedHtml } from '@/lib/youtube-hls-embed';
import { getYouTubeHlsPlaylist, resolveYouTubeHlsSource } from '@/lib/youtube-hls-source';
import { relayYouTubeHlsMedia } from '@/lib/youtube-hls-media';

const previewSession = '11111111-1111-4111-8111-111111111111';
const productionPath = '/api/video/youtube-hls';
const previewPath = '/api/dev/youtube-hls';
const publicPreviewVideoId = 'aqz-KE-bpKQ';

async function previewVideoId(): Promise<string> {
  try {
    const fixture = JSON.parse(await readFile('/tmp/codex-youtube-unlisted-metadata.json', 'utf8'));
    return fixture.videoId;
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== 'ENOENT') throw error;
    return publicPreviewVideoId;
  }
}

export async function GET(request: Request) {
  const url = new URL(request.url);
  if (process.env.NODE_ENV !== 'development'
    || !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)
    || !/^(localhost|127\.0\.0\.1|\[::1\])(?::\d+)?$/i.test(request.headers.get('host') ?? '')) {
    return new Response(null, { status: 404 });
  }
  const headers = { 'Cache-Control': 'no-store', 'X-Frame-Options': 'SAMEORIGIN' };
  try {
    if (url.searchParams.get('player') === '1') {
      const html = generateYouTubeHlsEmbedHtml(`${productionPath}?s=${previewSession}&playlist=master`, 'تجربة محلية', '');
      return new Response(html.replaceAll(productionPath, previewPath), {
        headers: { ...headers, 'Content-Type': 'text/html; charset=utf-8', 'Content-Security-Policy': "frame-ancestors 'self'" },
      });
    }
    // Prefer the owner's private fixture when present; otherwise use a public demo video.
    const source = await resolveYouTubeHlsSource(await previewVideoId(), url.searchParams.get('v') ?? undefined);
    if (url.searchParams.has('media') || url.searchParams.has('part')) {
      if (!url.searchParams.has('media') || !url.searchParams.has('part') || !url.searchParams.has('v')) {
        return new Response(null, { status: 400, headers });
      }
      return await relayYouTubeHlsMedia(source, url.searchParams.get('media')!, url.searchParams.get('part')!, request.signal);
    }
    if (url.searchParams.get('info') === '1') {
      return Response.json({ qualities: source.qualities, durationSeconds: source.durationSeconds,
        version: source.version, expiresAt: source.expiresAt, serverNowMs: Date.now() }, { headers });
    }
    const playlist = getYouTubeHlsPlaylist(source, { sessionId: previewSession,
      playlist: url.searchParams.get('playlist') ?? 'master', quality: url.searchParams.get('quality') ?? undefined,
      relay: url.searchParams.get('relay') === '1' });
    return new Response(playlist.replaceAll(productionPath, previewPath), {
      headers: { ...headers, 'Content-Type': 'application/vnd.apple.mpegurl' },
    });
  } catch {
    return Response.json({ message: 'بيانات التجربة المحلية غير متاحة.' }, { status: 503, headers });
  }
}
